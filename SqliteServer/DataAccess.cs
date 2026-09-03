using DamageMaker.Models;
using DamageMaker.Properties;
using DamageMarker.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DamageMaker.SqliteServer
{
    public static class DataAccess
    {

        /// <summary>
        /// 插入文件夹信息到数据库
        /// </summary>
        public static int InsertFolderInfo(SQLHelper sqlHelper, string folderPath,ScreenshotInfo NeedSavedInfo)
        {
             

            var parameters = new Dictionary<string, object>
    {
        { "@FolderPath", folderPath },
        { "@ScreenshotOffset", NeedSavedInfo.ScreenshotOffset },
        { "@ScreenshotWidthPx", NeedSavedInfo.ScreenshotWidthPx },
        { "@ScreenshotDirection", NeedSavedInfo.ScreenshotDirection.ToString() },
        { "@RailWayName", NeedSavedInfo.RailWayInfo.RailWayName },
        { "@Instruments", NeedSavedInfo.RailWayInfo.Instruments },
        { "@SerialNumber", NeedSavedInfo.RailWayInfo.SerialNumber },
        { "@WorkDate", NeedSavedInfo.RailWayInfo.WorkDate },
        { "@WorkSection", NeedSavedInfo.RailWayInfo.WorkSection },
        { "@WorkLength", NeedSavedInfo.RailWayInfo.WorkLength },
        { "@StartMileage", NeedSavedInfo.RailWayInfo.StartMileage },
        { "@EndMileage", NeedSavedInfo.RailWayInfo.EndMileage },
        { "@SelectedRailType", NeedSavedInfo.RailWayInfo.SelectedRailType },
        { "@SelectedLineType", NeedSavedInfo.RailWayInfo.SelectedLineType },
        { "@WorkGroup", NeedSavedInfo.RailWayInfo.WorkGroup },
        { "@OperatorName", NeedSavedInfo.RailWayInfo.OperatorName },
        { "@AnalyzeTime", NeedSavedInfo.RailWayInfo.AnalyzeTime },
        { "@ElapsedTimeForScrrnshot", NeedSavedInfo.RailWayInfo.ElapsedTimeForScrrnshot },
        { "@ElapsedTimeForAnalyze", NeedSavedInfo.RailWayInfo.ElapsedTimeForAnalyze },
        { "@SelectedRouteLine" , NeedSavedInfo.RailWayInfo.SelectedRouteLine},
        { "@SelectedUpOrDown" , NeedSavedInfo.RailWayInfo.SelectedUpOrDown},
        { "@CycleNumber",NeedSavedInfo.RailWayInfo.CycleNumber  }
    };

            int rowsAffected = sqlHelper.InsertTable(parameters, "DamageFolders");
            if (rowsAffected > 0)
            {
                Console.WriteLine("文件夹信息插入成功！");
                return sqlHelper.GetFolderIdByPath(folderPath); // 获取新插入的 FolderId
            }
            else
            {
                Console.WriteLine($"文件夹信息插入失败！错误信息：{sqlHelper.GetLastError()}");
                return -1;
            }
        }

        static public void UpdateImageRemark(List<SqlImgInfo> SqlImgInfos, string imgPath, string? remark)
        {
            var target = SqlImgInfos.FirstOrDefault(info => Path.GetFileName(info.ImgPath) == Path.GetFileName(imgPath));
            if (target != null)
            {
                if (target.Remark != remark)
                {
                    using (var sqlHelper = new SQLHelper(Settings.Default.SqlPath))
                    {
                        int rowsAffected = sqlHelper.UpdateImageRemark(target.ImgId, remark);
                        if (rowsAffected > 0)
                        {
                            target.Remark = remark;
                        }
                    }
                }
            }
            else
            {
                Console.WriteLine($"未找到路径为 {imgPath} 的图片信息。");
            }
        }

        public static void UpdateImageData(List<SqlImgInfo> sqlImgInfos, string imgFullPath, byte[] imageBytes)
        {
            using (var sqlHelper = new SQLHelper(Settings.Default.SqlPath))
            {
                var imgInfo = sqlImgInfos.FirstOrDefault(x => Path.GetFileName(x.ImgPath) == Path.GetFileName(imgFullPath));
                if (imgInfo != null)
                {
                    int rows = sqlHelper.UpdateImageData(imgInfo.ImgId, imageBytes);
                    if (rows > 0)
                    {
                        imgInfo.ImageData = imageBytes; // 同步内存
                    }
                }
            }
        }

        /// <summary>
        /// 根据路线、方向和周期号获取焊缝位置信息
        /// </summary>
        /// <param name="routeLine"></param>
        /// <param name="direction"></param>
        /// <param name="cycleNumber"></param>
        /// <returns></returns>
        public static List<WeldPositionInfo> GetWeldPositionsByRouteAndDirectionAndCycle(
            string routeLine, string direction, string? railType, int cycleNumber ,string? startMileage,string? endMileage)
        {
            using(var sqlHelper = new SQLHelper(Settings.Default.SqlPath))
            {
                return sqlHelper.GetWeldPositionsByRouteAndDirectionAndCycle(routeLine, direction, railType, cycleNumber,startMileage, endMileage);
            }
        }
    }
}
