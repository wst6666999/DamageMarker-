using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace DamageMaker.DamageDataProcessing
{
    /// <summary>
    /// 用于将伤损 ID 转换为不同的属性（如颜色、名称、规则等），以及将伤损名称转换为 ID
    /// </summary>
    public static class DataConversion
    {
        public static SolidColorBrush DamageIdToBrush(float Id)
        {
            // 将伤损ID转换为对应的颜色
            int id = (int)Id;

            // 使用 LINQ 查询从 DamageCategoryData 中查找与 ID 匹配的记录，并返回其 CategoryColor 属性
            return Models.Records.DamageCategoryData
                .Where(x => x.Id == id)
                .Select(x => x.CategoryColor)
                .FirstOrDefault() ?? throw new Exception("不存在的伤损id");
        }
        public static string DamageIdToDamageName(float Id)
        {
            int id = (int)Id;
            return Models.Records.DamageCategoryData.Where(x => x.Id == id).Select(x => x.CategoryName).FirstOrDefault() ?? throw new Exception("不存在的伤损id");
        }

        public static bool DamageIdToIsRule(float Id)
        {
            int id = (int)Id;
            return Models.Records.DamageCategoryData.Where(x => x.Id == id).Select(x => x.IsRule).FirstOrDefault();
        }
        public static int? DamageNameToId(string Name)
        {
            try { 
            return Models.Records.DamageCategoryData.Where(x => x.CategoryName == Name).Select(x => x.Id).First();
            }catch(InvalidOperationException ex)
            {
                Console.WriteLine(ex.Message);
                return -1;
            }
        }
    }
}
