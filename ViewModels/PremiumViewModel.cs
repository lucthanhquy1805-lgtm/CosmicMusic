using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CosmicMusic.Services;
using CosmicMusic.Views;

namespace CosmicMusic.ViewModels
{
    public partial class PremiumViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private bool _isPaymentVisible = true; // Mặc định hiện Popup khi vào trang

        private readonly FirestoreService _firestoreService;

        public PremiumViewModel(FirestoreService firestoreService)
        {
            _firestoreService = firestoreService;
        }

        [RelayCommand]
        public async Task ConfirmPayment()
        {
            IsBusy = true;
            await Task.Delay(2000); // Giả lập chờ

            try
            {
                string uid = Preferences.Get("UserId", "");
                string email = Preferences.Get("UserEmail", "");
                string name = Preferences.Get("UserName", "Cosmic User");

                if (string.IsNullOrEmpty(uid))
                {
                    await Shell.Current.DisplayAlert("Lỗi", "Không tìm thấy thông tin tài khoản.", "OK");
                    return;
                }

                // 1. Lưu lên Firestore
                await _firestoreService.UpdateUserAsync(uid, email, name, true);

                // 2. Lưu xuống máy
                Preferences.Set("IsPremium", true);

                // Dọn dẹp key cũ (nếu có)
                string oldKey = $"VIP_{email}";
                if (Preferences.ContainsKey(oldKey)) Preferences.Remove(oldKey);

                IsBusy = false;
                IsPaymentVisible = false;

                await Shell.Current.DisplayAlert("Thành công! 🎉", "Chào mừng VIP Member!", "Tuyệt vời");

                // Về trang chủ
                await Shell.Current.GoToAsync($"//{nameof(HomePage)}");
            }
            catch (Exception ex)
            {
                IsBusy = false;
                await Shell.Current.DisplayAlert("Lỗi", "Lỗi đồng bộ: " + ex.Message, "OK");
            }
        }

        // 👇 [ĐÃ SỬA] Logic Hủy bỏ đúng: Chỉ ẩn Popup, không thoát trang
        [RelayCommand]
        public void Cancel()
        {
            IsPaymentVisible = false;
        }

        // 👇 [THÊM MỚI] Hàm để mở lại Popup (gắn vào nút "Mua Ngay" ở giao diện nền)
        [RelayCommand]
        public void ShowPaymentPopup()
        {
            IsPaymentVisible = true;
        }
    }
}