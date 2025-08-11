using DamageMaker.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static DamageMaker.Models.Records;

namespace DamageMaker.DamageDataProcessing
{
    public static class ObtainInfo
    {
        public static List<KeyValuePair<int, float>> GetCategoryAndCount(
        List<DamageData> damageDataListPara,
        bool ShowNotDamage = false,
        List<OcrData>? ocrDataList = null)
        {
            List<float> damageCategory = new List<float>();

            foreach (var d in damageDataListPara)
            {
                var a = GetColumn4(d.DamagePoint);
                damageCategory.AddRange(a);
            }

            var result = damageCategory
                .GroupBy(x => x)
                .OrderByDescending(x => x.Count())
                .Select(g => new KeyValuePair<int, float>(g.Count(), g.Key))
                .ToList();

            // 如果需要显示未出现的类别
            Records.DamageCategoryData.ForEach(x =>
            {
                if (result.All(y => y.Value != x.Id) && ShowNotDamage)
                {
                    result.Add(new KeyValuePair<int, float>(0, x.Id));
                }
            });

            // 加入 OCR 速度信息统计（类别 ID 48）
            if (ocrDataList != null)
            {
                int speedCount = 0;

                foreach (var ocr in ocrDataList)
                {
                    if (!string.IsNullOrWhiteSpace(ocr.speedvalue))
                    {
                        var parts = ocr.speedvalue.Split(',');
                        foreach (var part in parts)
                        {
                            if (float.TryParse(part, out float speed) && speed > 1f)
                            {
                                speedCount++;
                            }
                        }
                    }
                }

                if (speedCount > 0)
                {
                    result.Add(new KeyValuePair<int, float>(speedCount, 48));
                }
            }

            return result;
        }





        public static float[] GetColumn4(float[][] damageDatas)
        {
            return damageDatas.Select(x => x[4]).ToArray();
        }


    }
}
