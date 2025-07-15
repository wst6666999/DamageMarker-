using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DamageMaker.FileHandle
{
    /// <summary>
    /// 该类用于获取当前文件夹的图片数量
    /// <return>返回当前文件夹的图片数量</return>
    /// </summary>
    public static class AboutImg
    {
        public static int GetImgCount(string folderPath)
        {
            var directoryInfo = new System.IO.DirectoryInfo(folderPath);
            var imgCount = directoryInfo.GetFiles().Count(f => f.Extension == ".jpg" || f.Extension == ".png");
            return imgCount;
        }
    }
}
