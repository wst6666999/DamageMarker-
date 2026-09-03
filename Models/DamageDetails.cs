using System.Windows.Media;

namespace DamageMaker.Models
{
    public class DamageDetails
    {
        public int Id { get; set; }
        public SolidColorBrush MarkerColor { get; set;}
        public string DamageCategory { get; set;}
        public string Similarity { get; set;}new  
        public double Weight { get; set; }          // 权重
        public string FileName { get; set; }        // 文件名，用于分组
        public int DamageCategoryId { get; set; }   // 类别ID，用于逻辑处理
        public List<int> FinalCategoryIds { get; set; } = new List<int>();
    }
}
