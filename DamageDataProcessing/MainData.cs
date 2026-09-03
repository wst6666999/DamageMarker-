using DamageMaker.FileHandle;
using DamageMaker.Models;
using DamageMaker.Properties;
using DamageMaker.SqliteServer;
using DamageMarker;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using static DamageMaker.Models.Records;

namespace DamageMaker.DamageDataProcessing
{
    internal  class MainData
    {
        public List<OcrData>? OcrDataList;
        public  List<DamageData>? DamageDataList;
        public  ScreenshotInfo? NeedSavedInfo;
        public int AllImgCount;
        public string ImgFolderName; //文件夹名称
        public string[] ImgPaths;
        public long FolderId;
        internal MainData(string DataPath)
        {
            
            ImgFolderName = Path.GetFileName(DataPath);
            OcrDataList = AboutJson.LoadOcrDatas(DataPath);

            (DamageDataList, NeedSavedInfo) = AboutJson.JsonPathToData(DataPath);
            
            ImgPaths=Directory.GetFiles(DataPath, "*.png")
                        .OrderBy(File.GetCreationTime)
                        .ToArray();
            AllImgCount = ImgPaths.Length;
            using (var sqlHelper = new SQLHelper(Settings.Default.SqlPath))
            {
                // 获取 FolderId
                FolderId = sqlHelper.GetFolderIdByPath(DataPath);
            }
        }

      public  List<DamageData>? ProcessData()
        {
            if (DamageDataList == null)
            {
                return null;
            }

            if (Settings.Default.IsDistictRepeat&&NeedSavedInfo!=null)
            {
                DamageDataList = DataFilter.DistictRepeatedDamage(DamageDataList, NeedSavedInfo);
            }
            //去除damage为空
            DamageDataList=DamageDataList
                .Where(x => x.DamagePoint != null && x.DamagePoint.Length > 0)
                .ToList();
            //将26和27这个伤损点过滤
            foreach (var damageData in DamageDataList)
            {
                damageData.DamagePoint = damageData.DamagePoint.Where(dp => dp[4] != 27).ToArray();
            }

            //将30和35这个图片里面如果出现0和39和2和6的将30和35给过滤掉
            var DamageDataListWith30Or35 = DamageDataList.Where(x => x.DamagePoint.Any(dp => dp[4] == 30 || dp[4] == 35)).ToList();
            foreach (var damageData in DamageDataListWith30Or35)
            {
                if (damageData.DamagePoint.Any(dp => dp[4] == 0 || dp[4] == 39 || dp[4] == 2 || dp[4] == 6 || dp[4]==11))
                {
                    damageData.DamagePoint = damageData.DamagePoint.Where(dp => dp[4] != 30 && dp[4] != 35).ToArray();
                }
            }
             
            return DamageDataList;
        }
    }
}
