using DamageMaker.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using static DamageMaker.DamageDataProcessing.DataConversion;

namespace DamageMaker.DamageDataProcessing
{
   internal static class DataFilter
    {
        /// <summary>
        /// 过滤掉所有的正常伤损
        /// </summary>
        /// <param name="data">伤损数据</param>
        /// <returns></returns>
        internal static List<DamageData> FilterNormalDamage(List<DamageData> data)
        {
            var result = data.Where(x => x.DamagePoint.Any(y => DamageIdToBrush(y[4]) == Brushes.Red))
                .Where(x => x.DamagePoint?.Length != 0).ToList();
            return result;
        }

        internal static List<DamageData> DistictRepeatedDamage(List<DamageData> data, ScreenshotInfo NeedSavedInfo)
        {

            if (NeedSavedInfo == null)
            {
                return data;
            }
            var root = Path.GetDirectoryName(data.FirstOrDefault().Url);//必须保证是绝对路径

            var FileNames = Directory.GetFiles(root, "*.png")
                          .OrderBy(File.GetCreationTime)
                          .ToArray();
            var FirstImg = FileNames[0];
            var LastImg = FileNames[FileNames.Length - 1];

            if (NeedSavedInfo.ScreenshotDirection == MoveDirection.Left)
            {
                data = data.Select(d =>
                {
                    if (d.Url == FirstImg)
                    {
                        d.DamagePoint = d.DamagePoint.Where(dp => dp[0] >= NeedSavedInfo.ScreenshotOffset / 2).ToArray();
                    }
                    else if (d.Url == LastImg)
                    {
                        d.DamagePoint = d.DamagePoint.Where(dp => dp[0] <= NeedSavedInfo.ScreenshotWidthPx - NeedSavedInfo.ScreenshotOffset / 2).ToArray();
                    }
                    else
                    {
                        d.DamagePoint = d.DamagePoint.Where(dp =>
                            dp[0] >= NeedSavedInfo.ScreenshotOffset / 2 &&
                            dp[0] <= NeedSavedInfo.ScreenshotWidthPx - NeedSavedInfo.ScreenshotOffset / 2
                        ).ToArray();
                    }
                    return d;
                }).ToList();
            }
            else if (NeedSavedInfo.ScreenshotDirection == MoveDirection.Right)
            {
                data = data.Select(d =>
                {
                    if (d.Url == FirstImg)
                    {
                        d.DamagePoint = d.DamagePoint.Where(dp => dp[0] < NeedSavedInfo.ScreenshotWidthPx - NeedSavedInfo.ScreenshotOffset / 2).ToArray();
                    }
                    else if (d.Url == LastImg)
                    {
                        d.DamagePoint = d.DamagePoint.Where(dp => dp[0] >= NeedSavedInfo.ScreenshotOffset / 2).ToArray();
                    }
                    else
                    {
                        d.DamagePoint = d.DamagePoint.Where(dp =>
                            dp[0] >= NeedSavedInfo.ScreenshotOffset / 2 &&
                            dp[0] < NeedSavedInfo.ScreenshotWidthPx - NeedSavedInfo.ScreenshotOffset / 2
                        ).ToArray();
                    }
                    return d;
                }).ToList();
            }
            return data;
        }
    }
}
