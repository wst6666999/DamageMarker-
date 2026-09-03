using System;
using System.Globalization;
using System.Windows.Data;
using DamageMaker.Models;

namespace DamageMaker.Converters
{
    public class CategoryConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int damageTypeId)
            {
                var record = Records.DamageCategoryData.FirstOrDefault(r => r.Id == damageTypeId);
                return record?.CategoryName ?? "未知类型";
            }

            if (value is float floatDamageTypeId)
            {
                var record = Records.DamageCategoryData.FirstOrDefault(r => r.Id == (int)floatDamageTypeId);
                return record?.CategoryName ?? "未知类型";
            }

            if (value is long longDamageTypeId)
            {
                var record = Records.DamageCategoryData.FirstOrDefault(r => r.Id == (int)longDamageTypeId);
                return record?.CategoryName ?? "未知类型";
            }

            if (value is string damageTypeString)
            {
                // 如果已经是字符串，直接返回
                return damageTypeString;
            }

            return "无伤损";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}