using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace DamageMaker.Models
{
    public partial class BoxSelectedControl : ObservableObject
    {

        [ObservableProperty]
        public SolidColorBrush rectColor;
        public int RectX { get; set; }
        public int RectY { get; set; }
        public int RectWidth { get; set; }
        public int RectHeight { get; set; }
        public double RectRadiusX { get; set; }
        public double RectRadiusY { get; set; }
        public double RectOpacity { get; set; }
        [ObservableProperty]
        public string buttonContent;
        public double ButtonX { get; set; }
        public double ButtonY { get; set; }
    }
}
