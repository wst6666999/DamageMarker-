using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace DamageMaker.Models
{
    public class WeldPositionInfo
    {
        public long ImgId { get; set; }
        public string ImagePath { get; set; } = string.Empty;
        public long FolderId { get; set; }
        public string Mileage { get; set; } = string.Empty;
        // 二维数组存储同一张图片的所有标注
        public List<float[]> Annotations { get; set; } = new List<float[]>();
        public byte[] ImageData { get; set; } = Array.Empty<byte>();
    }
    // 对比结果实体（单张对比图）
    public class CompareResult
    {
        public string? Mileage { get; set; }
        public string? DamageType { get; set; }
        public DateTime SaveTime { get; set; }
        public BitmapImage ImgData { get; set; } // 单张对比图
    }
}
