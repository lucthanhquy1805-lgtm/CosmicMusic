using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace CosmicMusic.Converters
{
    public class StringToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string str = value as string;

            // Mặc định là trả về True nếu chuỗi CÓ CHỮ (có URL ảnh)
            bool returnTrueIfNotEmpty = true;

            // Đọc tham số truyền vào từ XAML (True/False)
            if (parameter != null && bool.TryParse(parameter.ToString(), out bool paramBool))
            {
                returnTrueIfNotEmpty = paramBool;
            }

            bool isEmpty = string.IsNullOrWhiteSpace(str);

            // Trả về kết quả
            return returnTrueIfNotEmpty ? !isEmpty : isEmpty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}