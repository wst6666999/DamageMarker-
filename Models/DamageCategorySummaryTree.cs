using CommunityToolkit.Mvvm.ComponentModel;
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
    public interface ISelectableNode : ITreeNode
    {
        bool IsSelected { get; set; }

       

    }



    public class DamageCategorySummaryTree:ITreeNode
    {
        public string Name { get; set; }
        public List<ITreeNode> Children { get; set; }
    }

    public partial class DamageCategoryTree : ObservableObject, ITreeNode
    {
        [ObservableProperty]
        private string name;

        [ObservableProperty]
        private List<Details> children; // 子节点

       

        [ObservableProperty]
        private int count;

        [ObservableProperty]
        private SolidColorBrush colorBrush;
    }
    public partial class Details : ObservableObject, ISelectableNode
    {
        static int DetailsCount = 1;
        public Details()
        {
            Id = DetailsCount++;
        }

        public string Name { get; set; }
        public int? Id { get; set; }

        [ObservableProperty]
        private string fileName;

        [ObservableProperty]
        private int count;

        
        public float? weight;

        [ObservableProperty]
        private SolidColorBrush colorBrush = Brushes.Transparent;

        [ObservableProperty]
        private bool isSelected;

        [ObservableProperty]
        private bool hasDamageImg;

        [ObservableProperty]
        private bool hasNoDamageImg;

        [ObservableProperty]
        private int? isConfirmDamage;

        [ObservableProperty]
        private Brush background = Brushes.White;


    }

   

}
