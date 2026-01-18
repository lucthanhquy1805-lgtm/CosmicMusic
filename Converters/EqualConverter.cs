using System.Globalization;

namespace CosmicMusic.Converters
{
    public class EqualConverter : IValueConverter
    {
        // Hàm chuyển đổi: So sánh value (giá trị binding) và parameter (giá trị truyền vào)
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Chuyển cả 2 về chuỗi string và so sánh
            return value?.ToString() == parameter?.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Không dùng chiều ngược lại
            throw new NotImplementedException();
        }
    }
}