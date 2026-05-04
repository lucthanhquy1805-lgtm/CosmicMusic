using Android.App;
using Android.Content;
using Android.OS;
using Android.Support.V4.Media;
using Android.Support.V4.Media.Session;
using Android.Graphics;
using AndroidX.Core.App;
using CommunityToolkit.Mvvm.Messaging;

namespace CosmicMusic.Platforms.Android
{
    [Service(Exported = true, ForegroundServiceType = global::Android.Content.PM.ForegroundService.TypeMediaPlayback)]
    public class MusicService : Service
    {
        private MediaSessionCompat _mediaSession;
        public const string CHANNEL_ID = "CosmicMusicChannel";
        public const int NOTIFICATION_ID = 1001;

        public string CurrentTitle = "Cosmic Music";
        public string CurrentArtist = "Unknown Artist";
        public string CurrentCoverUrl = "";
        public bool IsPlaying = false;
        public long Duration = 0;
        public long Position = 0;

        public override void OnCreate()
        {
            base.OnCreate();
            _mediaSession = new MediaSessionCompat(this, "CosmicMusicSession");
            _mediaSession.SetCallback(new MusicSessionCallback(this));
            _mediaSession.Active = true;
            CreateNotificationChannel();
        }

        public override StartCommandResult OnStartCommand(Intent intent, StartCommandFlags flags, int startId)
        {
            string action = intent?.Action;

            if (!string.IsNullOrEmpty(action))
            {
                HandleAction(action);
            }
            else if (intent != null && intent.HasExtra("isPlaying"))
            {
                CurrentTitle = intent.GetStringExtra("title") ?? CurrentTitle;
                CurrentArtist = intent.GetStringExtra("artist") ?? CurrentArtist;
                CurrentCoverUrl = intent.GetStringExtra("coverImage") ?? CurrentCoverUrl;
                IsPlaying = intent.GetBooleanExtra("isPlaying", true);
                Duration = intent.GetLongExtra("duration", 0);
                Position = intent.GetLongExtra("position", 0);

                // Tải ảnh bìa rồi cập nhật notification
                _ = UpdateNotificationWithCoverAsync();
            }

            return StartCommandResult.Sticky;
        }

        public void HandleAction(string action)
        {
            if (action == "PLAY_PAUSE") IsPlaying = !IsPlaying;
            WeakReferenceMessenger.Default.Send(new ViewModels.MediaControlMessage(action));
            // Giữ nguyên ảnh hiện tại khi điều khiển Play/Pause/Next
            UpdateNotificationAndSession();
        }

        // 👇 BỔ SUNG LỖI LOGIC: Xử lý khi người dùng kéo thanh tua nhạc trên màn hình khóa
        public void HandleSeek(long pos)
        {
            Position = pos;
            // Gửi mốc thời gian (millisecond) về cho MAUI
            WeakReferenceMessenger.Default.Send(new ViewModels.MediaControlMessage($"SEEK:{pos}"));
            UpdateNotificationAndSession();
        }

        // Tải ảnh bìa bất đồng bộ từ URL rồi cập nhật notification
        private async Task UpdateNotificationWithCoverAsync()
        {
            Bitmap coverBitmap = null;
            try
            {
                if (!string.IsNullOrEmpty(CurrentCoverUrl))
                {
                    using var httpClient = new System.Net.Http.HttpClient();
                    httpClient.Timeout = TimeSpan.FromSeconds(5);
                    var bytes = await httpClient.GetByteArrayAsync(CurrentCoverUrl);
                    coverBitmap = await BitmapFactory.DecodeByteArrayAsync(bytes, 0, bytes.Length);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lỗi tải ảnh bìa notification: {ex.Message}");
            }

            UpdateNotificationAndSession(coverBitmap);
        }

        public void UpdateNotificationAndSession(Bitmap coverBitmap = null)
        {
            var stateBuilder = new PlaybackStateCompat.Builder()
                .SetActions(PlaybackStateCompat.ActionPlay | PlaybackStateCompat.ActionPause | PlaybackStateCompat.ActionSkipToNext | PlaybackStateCompat.ActionSkipToPrevious | PlaybackStateCompat.ActionSeekTo)
                .SetState(IsPlaying ? PlaybackStateCompat.StatePlaying : PlaybackStateCompat.StatePaused, Position, 1.0f);
            _mediaSession.SetPlaybackState(stateBuilder.Build());

            var metadataBuilder = new MediaMetadataCompat.Builder()
                .PutString(MediaMetadataCompat.MetadataKeyTitle, CurrentTitle)
                .PutString(MediaMetadataCompat.MetadataKeyArtist, CurrentArtist)
                .PutLong(MediaMetadataCompat.MetadataKeyDuration, Duration);
            _mediaSession.SetMetadata(metadataBuilder.Build());

            var prevAction = new NotificationCompat.Action(global::Android.Resource.Drawable.IcMediaPrevious, "Previous", CreatePendingIntent("PREV", 1));
            var playPauseAction = new NotificationCompat.Action(
                IsPlaying ? global::Android.Resource.Drawable.IcMediaPause : global::Android.Resource.Drawable.IcMediaPlay,
                IsPlaying ? "Pause" : "Play",
                CreatePendingIntent("PLAY_PAUSE", 2));
            var nextAction = new NotificationCompat.Action(global::Android.Resource.Drawable.IcMediaNext, "Next", CreatePendingIntent("NEXT", 3));

            // 👇 BỔ SUNG LỖI LOGIC: Tạo vé thông hành mở App khi chạm vào thông báo
            var openAppIntent = new Intent(this, typeof(MainActivity));
            openAppIntent.AddFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);
            var pendingOpenApp = PendingIntent.GetActivity(this, 0, openAppIntent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

            var notification = new NotificationCompat.Builder(this, CHANNEL_ID)
                .SetContentTitle(CurrentTitle)
                .SetContentText(CurrentArtist)

                // ✅ Hiện ảnh bìa bài hát ở vòng tròn lớn bên trái notification
                .SetLargeIcon(coverBitmap)

                // Icon nhỏ góc phải (bắt buộc phải có) — dùng icon app
                .SetSmallIcon(Resource.Mipmap.appicon)

                // Gắn vé thông hành mở App
                .SetContentIntent(pendingOpenApp)

                .SetOngoing(IsPlaying)
                .AddAction(prevAction)
                .AddAction(playPauseAction)
                .AddAction(nextAction)
                .SetStyle(new AndroidX.Media.App.NotificationCompat.MediaStyle()
                    .SetMediaSession(_mediaSession.SessionToken)
                    .SetShowActionsInCompactView(0, 1, 2))
                .Build();

            StartForeground(NOTIFICATION_ID, notification);
        }

        private PendingIntent CreatePendingIntent(string action, int requestCode)
        {
            var intent = new Intent(this, typeof(MusicService));
            intent.SetAction(action);
            return PendingIntent.GetService(this, requestCode, intent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
        }

        public override IBinder OnBind(Intent intent) => null;

        public override void OnDestroy()
        {
            base.OnDestroy();
            if (_mediaSession != null)
            {
                _mediaSession.Active = false;
                _mediaSession.Release();
            }
        }

        private void CreateNotificationChannel()
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                var channel = new NotificationChannel(CHANNEL_ID, "Cosmic Trình Phát Nhạc", NotificationImportance.Low) { Description = "Controls" };
                var manager = (NotificationManager)GetSystemService(NotificationService);
                manager.CreateNotificationChannel(channel);
            }
        }
    }

    public class MusicSessionCallback : MediaSessionCompat.Callback
    {
        private readonly MusicService _service;
        public MusicSessionCallback(MusicService service) { _service = service; }

        public override void OnPlay() => _service.HandleAction("PLAY_PAUSE");
        public override void OnPause() => _service.HandleAction("PLAY_PAUSE");
        public override void OnSkipToNext() => _service.HandleAction("NEXT");
        public override void OnSkipToPrevious() => _service.HandleAction("PREV");

        // 👇 ĐÃ THÊM: Lắng nghe hành động Tua nhạc trên màn hình khóa 👇
        public override void OnSeekTo(long pos) => _service.HandleSeek(pos);
    }
}