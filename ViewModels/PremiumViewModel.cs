using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CosmicMusic.Models;
using CosmicMusic.Services;
using CosmicMusic.Views;
using System.Collections.ObjectModel;

namespace CosmicMusic.ViewModels
{
    public partial class PremiumViewModel : ObservableObject
    {
        private readonly FirestoreService _firestoreService;

        public ObservableCollection<Subscription> SubscriptionPackages { get; set; } = new();

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private bool _isPaymentVisible = false;

        private Subscription _selectedSubscription;

        public PremiumViewModel(FirestoreService firestoreService)
        {
            _firestoreService = firestoreService;
            LoadPackages();
        }

        private async void LoadPackages()
        {
            IsLoading = true;
            var packages = await _firestoreService.GetSubscriptionsAsync();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                SubscriptionPackages.Clear();

                var sortedPackages = packages.OrderBy(p => p.DurationInMonths).ToList();

                foreach (var p in sortedPackages)
                {
                    SubscriptionPackages.Add(p);
                }

                IsLoading = false;
            });
        }

        // ==========================================
        // LOGIC CHỌN GÓI CƯỚC THÔNG MINH
        // ==========================================
        [RelayCommand]
        public async Task BuySubscription(Subscription sub)
        {
            if (sub == null) return;

            // 1. Kiểm tra xem người dùng đã là VIP chưa và đang dùng gói nào
            bool isPremium = Preferences.Get("IsPremium", false);
            string currentPackage = Preferences.Get("CurrentPackageName", "");

            if (isPremium)
            {
                // Trạng thái 1: Bấm vào đúng gói đang sử dụng
                if (currentPackage == sub.Name)
                {
                    await Shell.Current.DisplayAlert("Thông báo", $"Bạn đã đăng ký {sub.Name} rồi. Hãy tiếp tục tận hưởng âm nhạc không giới hạn nhé!", "OK");
                    return; // Chặn không cho mua lại
                }
                // Trạng thái 2: Đang là VIP nhưng muốn đổi sang gói khác (vd từ 1 tháng sang 1 năm)
                else
                {
                    bool wantToUpgrade = await Shell.Current.DisplayAlert("Đổi gói cước",
                        $"Bạn đang sử dụng {currentPackage}. Bạn có muốn chuyển sang đăng ký {sub.Name} không?",
                        "Có, Nâng cấp", "Hủy");

                    if (!wantToUpgrade) return;
                }
            }

            // Mở Popup thanh toán nếu qua được các bước kiểm tra
            _selectedSubscription = sub;
            IsPaymentVisible = true;
        }

        // ==========================================
        // XỬ LÝ THANH TOÁN THÀNH CÔNG
        // ==========================================
        [RelayCommand]
        public async Task ConfirmPayment()
        {
            IsBusy = true;
            await Task.Delay(2000); // Giả lập chờ xử lý

            try
            {
                string uid = Preferences.Get("UserId", "");
                string email = Preferences.Get("UserEmail", "");
                string name = Preferences.Get("UserName", "Cosmic User");

                if (string.IsNullOrEmpty(uid))
                {
                    await Shell.Current.DisplayAlert("Lỗi", "Không tìm thấy thông tin tài khoản.", "OK");
                    IsBusy = false;
                    return;
                }

                // Lưu lên Firebase
                await _firestoreService.UpdateUserAsync(uid, email, name, true);

                // Lưu xuống máy: TRẠNG THÁI VIP & TÊN GÓI CƯỚC
                string packageName = _selectedSubscription != null ? _selectedSubscription.Name : "Premium";
                Preferences.Set("IsPremium", true);
                Preferences.Set("CurrentPackageName", packageName); // <-- Lưu tên gói để lần sau kiểm tra

                // Dọn dẹp
                string oldKey = $"VIP_{email}";
                if (Preferences.ContainsKey(oldKey)) Preferences.Remove(oldKey);

                IsBusy = false;
                IsPaymentVisible = false;

                await Shell.Current.DisplayAlert("Thành công! 🎉", $"Bạn đã đăng ký thành công {packageName}. Chào mừng VIP Member!", "Tuyệt vời");

                // Về trang chủ
                await Shell.Current.GoToAsync($"//HomeTab");
            }
            catch (Exception ex)
            {
                IsBusy = false;
                await Shell.Current.DisplayAlert("Lỗi", "Lỗi đồng bộ: " + ex.Message, "OK");
            }
        }

        // ==========================================
        // CÁC NÚT ĐIỀU KHIỂN UI
        // ==========================================
        [RelayCommand]
        public void Cancel()
        {
            IsPaymentVisible = false;
            _selectedSubscription = null;
        }

        [RelayCommand]
        public void ShowPaymentPopup()
        {
            // Nút "GET 3 MONTHS FREE" ở phần Header tĩnh
            _selectedSubscription = new Subscription { Name = "Gói dùng thử 3 tháng" };
            IsPaymentVisible = true;
        }

        // ĐÃ SỬA LỖI NÚT BACK: Vì trang này là 1 Tab, nên lùi lại bằng cách chuyển sang Tab Home
        [RelayCommand]
        public async Task GoBack()
        {
            await Shell.Current.GoToAsync("//HomeTab");
        }
    }
}