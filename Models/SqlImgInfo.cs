using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DamageMaker.Models
{
    public class SqlImgInfo
    {
       public long ImgId { get; set; }
       public string ImgPath { get; set; }
       public long FolderId { get; set; }
      public  bool ?IsConfirmDamage { get; set; }

       public string? Remark { get; set; }

        // 新增：用于存储图片二进制数据
        public byte[]? ImageData { get; set; }
    }
}
