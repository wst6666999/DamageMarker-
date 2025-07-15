using DamageMaker.ViewModels;
using HandyControl.Controls;

namespace DamageMaker.Views
{
    /// <summary>
    /// UserControl1.xaml 的交互逻辑
    /// </summary>
    public partial class RailwayInfoInputWindow : Window
    {
     static RailwayInfoInputViewModel  railVM =  new RailwayInfoInputViewModel();
        public RailwayInfoInputWindow()
        {
            InitializeComponent();
            DataContext = railVM;
            Capturing.RailWayInfoInputed += OnRailwayInfoInputed;
        }
        private void OnRailwayInfoInputed(object sender, EventArgs e)
        {
            this.ShowDialog();
        }

        private void Window_Closed(object sender, EventArgs e)
        {

            Capturing.RailWayInfoInputed -= OnRailwayInfoInputed;
        }  
    }
}
