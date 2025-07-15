using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace DamageMaker.Models
{
    public interface ITreeNode
    {
        string Name { get; set; }
    }

    public class DamageCategorySummaryTree:ITreeNode
    {
        public string Name { get; set; }
        public List<ITreeNode> Children { get; set; }
    }

    public class DamageCategoryTree : ITreeNode
    {
        public string Name { get; set; }

        public SolidColorBrush ColorBrush { get; set; }
        public int ? Count { get; set; }
        public List<Details> Children { get; set;}
    }
    public class Details
    {
        static int DetailsCount = 1;
        public Details()
        {
            Id = DetailsCount++;
        }

        public int? Id { get; set; }
        public string FileName { get; set; }
        public int Count { get; set; }
        public float? weight { get; set; }

    }
}
