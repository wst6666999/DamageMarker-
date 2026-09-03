using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media;

namespace DamageMaker.Models
{
    public partial class BoxSelectedControl : ObservableObject
    {
        [ObservableProperty]
        private SolidColorBrush rectColor;

        [ObservableProperty]
        private string buttonContent;

        [ObservableProperty]
        private int rectX;

        [ObservableProperty]
        private int rectY;

        [ObservableProperty]
        private int rectWidth;

        [ObservableProperty]
        private int rectHeight;

        [ObservableProperty]
        private double rectRadiusX;

        [ObservableProperty]
        private double rectRadiusY;

        [ObservableProperty]
        private double rectOpacity;

        [ObservableProperty]
        private double buttonX;

        [ObservableProperty]
        private double buttonY;

        [ObservableProperty]
        private float[]? tag; 
    }
}
