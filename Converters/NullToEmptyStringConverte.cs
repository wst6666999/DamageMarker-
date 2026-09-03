using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace DamageMaker.Converters
{
    // 创建值转换器
    public class NullToEmptyStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // 将 int? 转换为显示字符串
            if (value == null)
                return " "; // 返回空白字符串
            else if (value is int intValue)
                return intValue.ToString();
            return " ";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // 将显示字符串转换回 int?
            if (value == null || string.IsNullOrWhiteSpace(value.ToString()) || value.ToString() == " ")
                return null;

            if (int.TryParse(value.ToString(), out int result))
                return result;

            return null;
        }
    }
}
