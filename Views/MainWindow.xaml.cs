using DamageMaker.Common;
using DamageMaker.Models;
using DamageMaker.Views;
using DamageMarker.ViewModels;
using DocumentFormat.OpenXml.Drawing;
using HandyControl.Controls;
using HandyControl.Interactivity;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using System.Windows.Input;
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
            ThumbnailList.ScrollIntoView((sender as System.Windows.Controls.ListBox).SelectedItem);
            ResetDamageViewerTransform();  // 切换图片时重置变换
        }

        private void TextBlock_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)//外面那层显示备注
        {
            return;
        }


        public ObservableCollection<Details> DetailsList { get; } = new ObservableCollection<Details>();

        private void Border_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)//里面那层显示备注
        {
            var sp = sender as Border;
            sp.Focus();
            if (sender is Border border && border.DataContext is Details selectedDetail)
            {
                if (DataContext is MainWindowViewModel vm)
                {
                    if ((border.Background is SolidColorBrush brush &&brush.Color == Colors.OrangeRed))
                    {
                        return;
                    }

                    if ((border.Background is SolidColorBrush brush1 && brush1.Color == Colors.Green))
                    {
                        return;
                    }
                    border.Focus();
                    // 设置选中项
                    border.Background = new SolidColorBrush(Colors.Yellow);
                }

            }
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

        private void ToggleButton_Checked_1(object sender, RoutedEventArgs e)
        {

        }

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is Details detail)
            {
                // 获取 ViewModel 实例并调用命令
                if (DataContext is MainWindowViewModel vm)
                {
                    vm.DetailItemClickCommand.Execute(detail);
                }
            }
        }

        private System.Windows.Point _lastMousePosition;
        private bool _isDragging = false;
        private double _zoomFactor = 1.0;


        private void ResetDamageViewerTransform()
        {
            _zoomFactor = 1.0;
            var transformGroup = new System.Windows.Media.TransformGroup();
            transformGroup.Children.Add(new ScaleTransform(1.0, 1.0));
            transformGroup.Children.Add(new TranslateTransform(0, 0));
            damageViewer.RenderTransform = transformGroup;
        }
        private void DamageViewer_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            // 获取鼠标相对于DamageDisplayBox的位置
            var mousePosition = e.GetPosition(damageViewer);

            // 计算缩放因子
            double zoom = e.Delta > 0 ? 1.1 : 0.9;
            _zoomFactor = Math.Clamp(_zoomFactor * zoom, 0.5, 3.0);

            // 应用缩放变换
            var transform = damageViewer.RenderTransform as System.Windows.Media.TransformGroup ?? new System.Windows.Media.TransformGroup();
            var scaleTransform = transform.Children.OfType<ScaleTransform>().FirstOrDefault();

            if (scaleTransform == null)
            {
                scaleTransform = new ScaleTransform();
                transform.Children.Add(scaleTransform);
                damageViewer.RenderTransform = transform;
            }

            // 基于鼠标位置的缩放
            scaleTransform.ScaleX = scaleTransform.ScaleY = _zoomFactor;

            // 调整位置使缩放中心在鼠标位置
            var translateTransform = transform.Children.OfType<TranslateTransform>().FirstOrDefault();
            if (translateTransform == null)
            {
                translateTransform = new TranslateTransform();
                transform.Children.Add(translateTransform);
            }

            translateTransform.X = mousePosition.X * (1 - zoom);
            translateTransform.Y = mousePosition.Y * (1 - zoom);
        }

        private void DamageViewer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                _isDragging = true;
                _lastMousePosition = e.GetPosition(this);
                damageViewer.CaptureMouse();
            }
        }

        private void DamageViewer_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                _isDragging = false;
                damageViewer.ReleaseMouseCapture();
            }
        }

        private void DamageViewer_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_isDragging)
            {
                var currentPosition = e.GetPosition(this);
                var transform = damageViewer.RenderTransform as System.Windows.Media.TransformGroup ?? new System.Windows.Media.TransformGroup();
                var translateTransform = transform.Children.OfType<TranslateTransform>().FirstOrDefault();

                if (translateTransform == null)
                {
                    translateTransform = new TranslateTransform();
                    transform.Children.Add(translateTransform);
                    damageViewer.RenderTransform = transform;
                }

                translateTransform.X += (currentPosition.X - _lastMousePosition.X);
                translateTransform.Y += (currentPosition.Y - _lastMousePosition.Y);

                _lastMousePosition = currentPosition;
            }
        }
    }
}
