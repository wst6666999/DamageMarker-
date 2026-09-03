using System.Windows;
using DamageMaker.ViewModels;
using HandyControl.Controls;
using Window = HandyControl.Controls.Window;

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

        private void ComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {

        }
        private void ComboBox_LostFocus(object sender, RoutedEventArgs e)
        {
            // 可选：在失去焦点时立即保存
            var vm = DataContext as RailwayInfoInputViewModel;
            vm?.SaveManualRouteLine();
        }

        private void ComboBox_SelectionChanged_1(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {

        }
    }
}
