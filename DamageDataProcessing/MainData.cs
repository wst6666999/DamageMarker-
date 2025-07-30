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

namespace DamageMaker.DamageDataProcessing
{
    internal  class MainData
    {
      public  List<DamageData>? DamageDataList;
      public  ScreenshotInfo? NeedSavedInfo;
       public int AllImgCount;
       public string ImgFolderName; //文件夹名称
        public string[] ImgPaths;
        public long FolderId;
        internal MainData(string DataPath)
        {
            ImgFolderName = Path.GetFileName(DataPath);
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
            return DamageDataList;
        }
    }
}
