//using DamageMaker.Models;
//using DamageMaker.ViewModels;
//using System;
//using System.Collections.Generic;
//using System.Diagnostics.CodeAnalysis;
//using System.IO;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using static DamageMaker.Models.Records;

//namespace DamageMaker.DamageDataProcessing
//{
//    public static class ObtainInfo
//    {
//        public static List<KeyValuePair<int, float>> GetCategoryAndCount(
//        List<DamageData> damageDataListPara,
//        bool ShowNotDamage = false,
//        List<OcrData>? ocrDataList = null, float currentThroughWeldSpeed=0f,float currentLimitedSpeed=0f)
//        {
//            List<float> damageCategory = new List<float>();

//            foreach (var d in damageDataListPara)
//            {
//                var a = GetColumn4(d.DamagePoint);
//                damageCategory.AddRange(a);
//            }

//            var result = damageCategory
//                .GroupBy(x => x)
//                .OrderByDescending(x => x.Count())
//                .Select(g => new KeyValuePair<int, float>(g.Count(), g.Key))
//                .ToList();

//            // 如果需要显示未出现的类别
//            Records.DamageCategoryData.ForEach(x =>
//            {
//                if (result.All(y => y.Value != x.Id) && ShowNotDamage)
//                {
//                    result.Add(new KeyValuePair<int, float>(0, x.Id));
//                }
//            });

//            // 加入 OCR 速度信息统计
//            if (ocrDataList != null)
//            {
//                int generalOverspeedCount = 0;
//                int weldOverspeedCount = 0;
//                HashSet<string> overspeedImages = new HashSet<string>(); // 用于跟踪所有超速图片

//                foreach (var ocr in ocrDataList)
//                {
//                    string fileName = Path.GetFileName(ocr.ImgFullPath);
//                    bool hasGeneralOverspeed = false;
//                    bool hasWeldOverspeed = false;

//                    // 1. 检查整体速度是否超速
//                    if (!string.IsNullOrWhiteSpace(ocr.speedvalue))
//                    {
//                        var speeds = ocr.speedvalue.Split(',');
//                        foreach (var speedStr in speeds)
//                        {
//                            if (float.TryParse(speedStr, out float speed))
//                            {
//                                if (speed > currentLimitedSpeed)
//                                {
//                                    hasGeneralOverspeed = true;
//                                }
//                            }
//                        }
//                    }

//                    // 2. 检查焊缝速度是否超速
//                    var correspondingDamageData = damageDataListPara.FirstOrDefault(d =>
//                        Path.GetFileName(d.Url) == fileName);

//                    if (correspondingDamageData != null)
//                    {
//                        bool hasWeld = false;

//                        // 先检查图片中是否有焊缝（类别14）
//                        for (int i = 0; i < correspondingDamageData.DamagePoint.GetLength(0); i++)
//                        {
//                            if (correspondingDamageData.DamagePoint[i].Length > 4 &&
//                                correspondingDamageData.DamagePoint[i][4] == 14)
//                            {
//                                hasWeld = true;
//                                break;
//                            }
//                        }

//                        // 如果有焊缝，再检查速度是否超限
//                        if (hasWeld && !string.IsNullOrWhiteSpace(ocr.speedvalue))
//                        {
//                            var speeds = ocr.speedvalue.Split(',');
//                            foreach (var speedStr in speeds)
//                            {
//                                if (float.TryParse(speedStr, out float speed) && speed > currentThroughWeldSpeed)
//                                {
//                                    hasWeldOverspeed = true;
//                                }
//                            }
//                        }
//                    }

//                    // 统计超速次数（按图片去重）
//                    if (hasGeneralOverspeed || hasWeldOverspeed)
//                    {
//                        overspeedImages.Add(fileName); // 记录超速图片（自动去重）
//                    }

//                    if (hasGeneralOverspeed) generalOverspeedCount++;
//                    if (hasWeldOverspeed) weldOverspeedCount++;
//                }
//                // 统计不重复的超速图片数量
//                result.Add(new KeyValuePair<int, float>(overspeedImages.Count, 48));

//            }

//            return result;
//        }





//        public static float[] GetColumn4(float[][] damageDatas)
//        {
//            return damageDatas.Select(x => x[4]).ToArray();
//        }


//    }
//}
using DamageMaker.Models;
using DamageMaker.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
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
            List<OcrData>? ocrDataList = null,
            float currentThroughWeldSpeed = 0f,
            float currentLimitedSpeed = 0f)
        {
            // 创建一个字典来按伤损类型统计图片张数
            Dictionary<float, HashSet<string>> categoryImages = new Dictionary<float, HashSet<string>>();

            // 遍历所有伤损数据
            foreach (var damageData in damageDataListPara)
            {
                string imageName = Path.GetFileName(damageData.Url);
                var damageTypes = GetColumn4(damageData.DamagePoint);

                foreach (var damageType in damageTypes)
                {
                    if (!categoryImages.ContainsKey(damageType))
                    {
                        categoryImages[damageType] = new HashSet<string>();
                    }
                    categoryImages[damageType].Add(imageName);
                }
            }

            // 转换为结果列表：Key=图片张数，Value=伤损类型ID
            var result = categoryImages
                .Select(kv => new KeyValuePair<int, float>(kv.Value.Count, kv.Key))
                .OrderByDescending(x => x.Key)  // 按图片张数降序
                .ThenBy(x => x.Value)           // 图片张数相同按ID排序
                .ToList();

            // 如果需要显示未出现的类别
            if (ShowNotDamage)
            {
                Records.DamageCategoryData.ForEach(category =>
                {
                    if (result.All(y => Math.Abs(y.Value - category.Id) > 0.001f))
                    {
                        result.Add(new KeyValuePair<int, float>(0, category.Id));
                    }
                });
            }

            // 加入 OCR 速度信息统计
            if (ocrDataList != null)
            {
                int generalOverspeedCount = 0;
                int weldOverspeedCount = 0;
                HashSet<string> overspeedImages = new HashSet<string>(); // 用于跟踪所有超速图片

                foreach (var ocr in ocrDataList)
                {
                    string fileName = Path.GetFileName(ocr.ImgFullPath);
                    bool hasGeneralOverspeed = false;
                    bool hasWeldOverspeed = false;

                    // 1. 检查整体速度是否超速
                    if (!string.IsNullOrWhiteSpace(ocr.speedvalue))
                    {
                        var speeds = ocr.speedvalue.Split(',');
                        foreach (var speedStr in speeds)
                        {
                            if (float.TryParse(speedStr, out float speed))
                            {
                                if (speed > currentLimitedSpeed)
                                {
                                    hasGeneralOverspeed = true;
                                }
                            }
                        }
                    }

                    // 2. 检查焊缝速度是否超速
                    var correspondingDamageData = damageDataListPara.FirstOrDefault(d =>
                        Path.GetFileName(d.Url) == fileName);

                    if (correspondingDamageData != null)
                    {
                        bool hasWeld = false;

                        // 先检查图片中是否有焊缝（类别14）
                        for (int i = 0; i < correspondingDamageData.DamagePoint.GetLength(0); i++)
                        {
                            if (correspondingDamageData.DamagePoint[i].Length > 4 &&
                                correspondingDamageData.DamagePoint[i][4] == 14)
                            {
                                hasWeld = true;
                                break;
                            }
                        }

                        // 如果有焊缝，再检查速度是否超限
                        if (hasWeld && !string.IsNullOrWhiteSpace(ocr.speedvalue))
                        {
                            var speeds = ocr.speedvalue.Split(',');
                            foreach (var speedStr in speeds)
                            {
                                if (float.TryParse(speedStr, out float speed) && speed > currentThroughWeldSpeed)
                                {
                                    hasWeldOverspeed = true;
                                }
                            }
                        }
                    }

                    // 统计超速次数（按图片去重）
                    if (hasGeneralOverspeed || hasWeldOverspeed)
                    {
                        overspeedImages.Add(fileName); // 记录超速图片（自动去重）
                    }

                    if (hasGeneralOverspeed) generalOverspeedCount++;
                    if (hasWeldOverspeed) weldOverspeedCount++;
                }

                // 添加或更新超速统计（ID 48）
                int existingIndex = result.FindIndex(r => Math.Abs(r.Value - 48) < 0.001f);
                if (existingIndex >= 0)
                {
                    result[existingIndex] = new KeyValuePair<int, float>(overspeedImages.Count, 48);
                }
                else
                {
                    result.Add(new KeyValuePair<int, float>(overspeedImages.Count, 48));
                    // 重新排序
                    result = result
                        .OrderByDescending(x => x.Key)
                        .ThenBy(x => x.Value)
                        .ToList();
                }
            }

            return result;
        }

        // 保持向后兼容的旧方法（按伤损处数统计）
        public static List<KeyValuePair<int, float>> GetCategoryAndCountByDamage(
            List<DamageData> damageDataListPara,
            bool ShowNotDamage = false)
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

            return result;
        }

        public static float[] GetColumn4(float[][] damageDatas)
        {
            return damageDatas.Select(x => x[4]).ToArray();
        }
    }
}