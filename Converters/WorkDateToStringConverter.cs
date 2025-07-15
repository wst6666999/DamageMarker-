using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace DamageMaker.Converters
{
    public class WorkDateToStringConverter: IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is String dateTime)
            {
           

             return DateTime.Parse(dateTime);

            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DateTime dateTime )
            {
                //return dateTime.ToString("yyyy年MM月d日");


                return dateTime.ToLongDateString();
            }
            return DateTime.Now;
        }
    }
}
