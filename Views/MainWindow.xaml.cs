using DamageMaker.Common;
using DamageMaker.Models;
using DamageMaker.Views;
using DamageMarker.ViewModels;
using HandyControl.Controls;
using HandyControl.Interactivity;
using System.ComponentModel;
using System.Diagnostics.Eventing.Reader;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using static System.Net.Mime.MediaTypeNames;


namespace DamageMarker.Views
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow
    {

        public static MainWindowViewModel MainVm;
        public MainWindow()
        {
            InitializeComponent();
            NonClientAreaContent = new NonClient();
            MainVm = new MainWindowViewModel();
            DataContext = MainVm;
            MainWindowViewModel.ScreenShotPopuped += OnScreenShotPopuped;
            //PlaybackWindow.ScreenshotFinished += OnScreenshotFinished;
           
            Scal.ScaleX = 1 / DamageMaker.Common.Monitor.ScaleX;
            Scal.ScaleY = 1 / DamageMaker.Common.Monitor.ScaleY;            
        }
        private void OnScreenShotPopuped(object sender, EventArgs e)
        {
            WindowState = WindowState.Minimized;
        }
        private void OnScreenshotFinished(object sender, string time)
        {
            WindowState = WindowState.Maximized;
        }
        protected override void OnClosing(CancelEventArgs e)
        {

            base.OnClosing(e);
            if (MainVm.Cap != null)
            {
                MainVm.Cap.Close();
            }
        }



        private void bthClick(object sender, RoutedEventArgs e)
        {
            Geometry? EyeCloseGeometry = System.Windows.Application.Current.FindResource("EyeCloseGeometry") as Geometry;
            Geometry? EyeOpenGeometry = System.Windows.Application.Current.FindResource("EyeOpenGeometry") as Geometry;
            var bth = sender as ToggleButton;

            if (bth != null)
            {
                if (bth.IsChecked ?? false)
                {
                    HandyControl.Controls.IconElement.SetGeometry(bth, EyeCloseGeometry);
                }
                else
                {
                    HandyControl.Controls.IconElement.SetGeometry(bth, EyeOpenGeometry);
                }
            }
        }

        private void ThumbnailList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            ThumbnailList.ScrollIntoView((sender as ListBox).SelectedItem);
        }

        private void TextBlock_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)//外面那层显示备注
        {
            
                return;
        }

        private void Border_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)//里面那层显示备注
        {
            var sp = sender as Border;
            sp.Focus();          
           
                return;
         
        }


        public static T? FindLogicalParent<T>(DependencyObject child) where T : DependencyObject
        {
            // 获取父元素
            DependencyObject parentObject = VisualTreeHelper .GetParent(child);
            

            // 如果没有父元素，则返回 null
            if (parentObject == null) return null;

            // 如果父元素是指定类型，则返回父元素
            if (parentObject is T parent)
            {
                return parent;
            }
            else
            {
                // 递归查找父元素
                return FindLogicalParent<T>(parentObject);
            }
        }

        private void Border_GotFocus(object sender, RoutedEventArgs e)
        {
           
        }

        private void Border_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            
        }

        private void ThumbnailList_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            Console.WriteLine(e.Key);
            e.Handled = true;
        }

        private void ToggleButton_Checked(object sender, RoutedEventArgs e)
        {

        }

        private void TV_GotFocus(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("GotFocus");
        }

        private void ProgressButton_Checked(object sender, RoutedEventArgs e)
        {

        }

        private void SearchBar_TextChanged(object sender, TextChangedEventArgs e)
        {

        }
    }
}
