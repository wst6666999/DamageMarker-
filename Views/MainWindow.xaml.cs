using DamageMaker.Common;
using DamageMaker.Models;
using DamageMaker.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using DamageMarker.ViewModels;
using HandyControl.Controls;
using HandyControl.Interactivity;
using System.ComponentModel;
using System.Diagnostics.Eventing.Reader;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using System.Diagnostics;
using MenuItem = System.Windows.Controls.MenuItem;
using System.Windows.Forms;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using ListBox = System.Windows.Controls.ListBox;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBox = System.Windows.MessageBox;



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
            // 弹出确认对话框
            var result = HandyControl.Controls.MessageBox.Show("确定要退出软件吗？", "退出确认",
                                        MessageBoxButton.YesNo,
                                        MessageBoxImage.Question);

            if (result == MessageBoxResult.No)
            {
                e.Cancel = true; // 用户选择"否"，取消关闭
                return;
            }

            // 用户选择"是"，继续执行关闭逻辑
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

        private void ThumbnailList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is not ListBox listBox || listBox.SelectedItem == null)
                return;

            // 等 ListBoxItem 容器生成/布局完成后再判断它在可视区域中的真实位置。
            listBox.Dispatcher.BeginInvoke(new Action(() =>
            {
                listBox.UpdateLayout();

                var scrollViewer = FindScrollViewer(listBox);
                if (scrollViewer == null)
                {
                    ResetDamageViewerTransform();
                    return;
                }

                var selectedContainer =
                    listBox.ItemContainerGenerator.ContainerFromItem(listBox.SelectedItem) as ListBoxItem;

                if (selectedContainer == null)
                {
                    // 虚拟化时容器可能还没生成，先让当前项进入视野，再取一次容器。
                    listBox.ScrollIntoView(listBox.SelectedItem);
                    listBox.UpdateLayout();
                    selectedContainer =
                        listBox.ItemContainerGenerator.ContainerFromItem(listBox.SelectedItem) as ListBoxItem;
                }

                if (selectedContainer == null)
                {
                    ResetDamageViewerTransform();
                    return;
                }

                selectedContainer.UpdateLayout();

                System.Windows.Point position = selectedContainer
                    .TransformToAncestor(scrollViewer)
                    .Transform(new System.Windows.Point(0, 0));

                double itemWidth = selectedContainer.ActualWidth;
                if (itemWidth <= 1)
                    itemWidth = 180; // 兜底值，对应缩略图文本宽度

                double itemLeft = position.X;
                double itemRight = itemLeft + itemWidth;
                double viewportWidth = scrollViewer.ViewportWidth;
                double currentOffset = scrollViewer.HorizontalOffset;
                int index = listBox.SelectedIndex;

                // 用半个 item 宽度作为边缘区域，避免边界误差导致左侧点击没反应。
                double edgeBand = itemWidth * 0.5;

                bool isLeftEdgeItem = itemLeft <= edgeBand;
                bool isRightEdgeItem = itemRight >= viewportWidth - edgeBand;

                // 点击当前可视区域最左边的缩略图：向右退一张，露出上一张。
                if (isLeftEdgeItem && index > 0)
                {
                    scrollViewer.ScrollToHorizontalOffset(
                        Math.Max(0, currentOffset - itemWidth));
                }
                // 点击当前可视区域最右边的缩略图：向左进一张，露出下一张。
                else if (isRightEdgeItem && index < listBox.Items.Count - 1)
                {
                    scrollViewer.ScrollToHorizontalOffset(
                        Math.Min(scrollViewer.ScrollableWidth, currentOffset + itemWidth));
                }

                ResetDamageViewerTransform();
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void TextBlock_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)//外面那层显示备注
        {

            var tb = (sender as TextBlock);
            var t = tb?.Text ?? " ";
            //t=Regex.Replace(t, @" \d.次", String.Empty);
            //Console.WriteLine(t);
            var tb2 = VisualTreeHelper.GetParent(tb);
            RuleInfo.Text = Records.DamageCategoryData.Where(x => x.CategoryName == t).Select(x => x.Remark).FirstOrDefault();
        }

        private void Border_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)//里面那层显示备注
        {
            var sp = sender as Border;
            sp.Focus();

            // 方法2：使用更可靠的父元素查找
            var treeViewItem2 = FindVisualParent<TreeViewItem>(sp);
            if (treeViewItem2 != null)
            {
                var dataContext = treeViewItem2.DataContext;
                string categoryName = null;

                if (dataContext is DamageCategoryTree categoryTree)
                {
                    categoryName = categoryTree.Name;
                }
                else if (dataContext is DamageCategorySummaryTree summaryTree)
                {
                    categoryName = summaryTree.Name;
                }
                else if (dataContext is Details detail2)
                {
                    // 如果是 Details，需要找到其父分类
                    var parentTreeViewItem = FindVisualParent<TreeViewItem>(treeViewItem2);
                    if (parentTreeViewItem?.DataContext is DamageCategoryTree parentCategory)
                    {
                        categoryName = parentCategory.Name;
                    }
                }

                if (!string.IsNullOrEmpty(categoryName))
                {
                    var remark = Records.DamageCategoryData
                        .Where(x => x.CategoryName == categoryName)
                        .Select(x => x.Remark)
                        .FirstOrDefault();

                    RuleInfo.Text = remark;
                }
            }

            else
            {
                Console.WriteLine("未找到父元素");
            }

            if (sender is Border border && border.DataContext is Details detail)
            {
                string filePath = System.IO.Path.Combine(MainVm.damageImgFolderName.OutReplaceInString(), detail.FileName);

                if (MainVm.GetDamageConfirmationStatus(filePath) == 1)
                {
                    detail.Status = DamageStatus.HASDAMAGE; // 设置状态为有伤损
                }
                else if (MainVm.GetDamageConfirmationStatus(filePath) == 0)
                {
                    detail.Status = DamageStatus.NODAMAGE; // 设置状态为无伤损
                }
                else
                {
                    detail.Status = DamageStatus.KNOWN; // 设置状态为未知
                }
            }

            return;

        }

        // 辅助方法：查找视觉树中的父元素
        private static T FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            var parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;

            if (parentObject is T parent) return parent;

            return FindVisualParent<T>(parentObject);
        }

        // 辅助方法：查找逻辑父级
        private static T FindLogicalParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = LogicalTreeHelper.GetParent(child);

            if (parentObject == null)
                return null;

            if (parentObject is T parent)
                return parent;

            return FindLogicalParent<T>(parentObject);
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
        private void ExportOriginalImage_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 检查是否有图片源
                if (damageViewer.SourceImage == null)
                {
                    MessageBox.Show("没有可保存的图片！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 创建保存文件对话框
                SaveFileDialog saveFileDialog = new SaveFileDialog();

                // 设置文件过滤器
                saveFileDialog.Filter = "PNG 图片|*.png|JPEG 图片|*.jpg|BMP 图片|*.bmp|所有文件|*.*";
                saveFileDialog.FilterIndex = 1;
                saveFileDialog.DefaultExt = ".png";

                // 设置默认文件名（使用当前时间戳）
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                saveFileDialog.FileName = $"damage_image_{timestamp}";

                // 显示保存对话框
                if (saveFileDialog.ShowDialog() == true)
                {
                    // 获取选择的文件路径
                    string filePath = saveFileDialog.FileName;

                    // 根据文件扩展名选择编码器
                    BitmapEncoder encoder = GetEncoder(System.IO.Path.GetExtension(filePath));

                    // 将 SourceImage 转换为 BitmapSource
                    BitmapSource bitmapSource = damageViewer.SourceImage as BitmapSource;

                    if (bitmapSource != null)
                    {
                        // 编码并保存图片
                        encoder.Frames.Add(BitmapFrame.Create(bitmapSource));

                        using (FileStream stream = new FileStream(filePath, FileMode.Create))
                        {
                            encoder.Save(stream);
                        }

                        MessageBox.Show($"图片已保存到：{filePath}", "保存成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("无法获取图片数据！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存图片时出错：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 辅助方法：根据文件扩展名选择编码器

        private void DamageViewer_MouseMove(object sender, MouseEventArgs e)
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
        // 存储当前选中的项
        private object _currentImageData;
        private object _currentCategoryData;

        private void ImageContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            var contextMenu = sender as ContextMenu;
            if (contextMenu != null && contextMenu.PlacementTarget is FrameworkElement target)
            {
                // 获取右键点击的数据上下文
                _currentImageData = target.DataContext;
            }
            //Console.WriteLine("ContextMenu opened");
            //Console.WriteLine(_currentImageData);
        }

        private void CategoryContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            var contextMenu = sender as ContextMenu;
            if (contextMenu != null && contextMenu.PlacementTarget is FrameworkElement target)
            {
                // 获取右键点击的数据上下文
                _currentCategoryData = target.DataContext;
            }
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            if (menuItem == null) return;

            string action = menuItem.Tag as string;
            object dataContext = null;

            // 确定是哪个上下文菜单的项被点击
            var contextMenu = FindLogicalParent<ContextMenu>(menuItem);
            //Console.WriteLine(contextMenu);

            if (contextMenu != null)
            {
                string menuType = contextMenu.Tag as string;
                if (menuType == "ImageMenu")
                {
                    dataContext = _currentImageData;
                }
                else if (menuType == "CategoryMenu")
                {
                    dataContext = _currentCategoryData;
                }

            }

            //Console.WriteLine(action);
            //Console.WriteLine(dataContext);
            switch (action)
            {
                case "SaveMarked":
                    SaveMarkedImage(dataContext);
                    break;
                case "SaveUnmarked":
                    SaveUnmarkedImage(dataContext);
                    break;
                case "OpenImgFolder":
                    OpenImgFolder(dataContext);
                    break;
                case "ExportAllMarked":
                    ExportAllMarked(dataContext);
                    break;
                case "ExportAllUnmarked":
                    ExportAllUnmarked(dataContext);
                    break;
                case "OpenAllFolder":
                    OpenAllFolder(dataContext);
                    break;
                case "BatchSaveMarked":
                    BatchSaveMarked(dataContext);
                    break;
                case "BatchSaveUnmarked":
                    BatchSaveUnmarked(dataContext);
                    break;
                case "BatchImgSaveMarked":
                    BatchImgSaveMarked(dataContext);
                    break;
                case "BatchImgSaveUnmarked":
                    BatchImgSaveUnMarked(dataContext);
                    break;
            }
        }

        private void BatchImgSaveUnMarked(object? dataContext)
        {
            if (dataContext is Details detail)
            {
                foreach (var categorySummary in MainVm.DamageTree)
                {

                    foreach (var child in categorySummary.Children)
                    {
                        if (child is DamageCategoryTree damageCategory)
                        {
                            // 检查这个分类是否包含当前的 detail
                            if (damageCategory.Children != null && damageCategory.Children.Any(grandChild =>
                                grandChild.FileName == detail.FileName && grandChild.Id == detail.Id))
                            {
                                //Console.WriteLine($"找到对应分类: {damageCategory.Name}");
                                BatchSaveUnmarked(damageCategory);
                                return;
                            }

                        }
                        if (child is DamageCategorySummaryTree damageCategorySummary)
                        {
                            //遍历damageCategorySummary
                            foreach (var subChild in damageCategorySummary.Children)
                            {
                                if (subChild is DamageCategoryTree subDamageCategory)
                                {
                                    // 检查这个分类是否包含当前的 detail
                                    if (subDamageCategory.Children != null && subDamageCategory.Children.Any(grandChild =>
                                        grandChild.FileName == detail.FileName && grandChild.Id == detail.Id))
                                    {
                                        //Console.WriteLine($"找到对应分类: {subDamageCategory.Name}");
                                        BatchSaveUnmarked(subDamageCategory);
                                        return;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private void BatchImgSaveMarked(object? dataContext)
        {
            if (dataContext is Details detail)
            {
                foreach (var categorySummary in MainVm.DamageTree)
                {

                    foreach (var child in categorySummary.Children)
                    {
                        if (child is DamageCategoryTree damageCategory)
                        {
                            // 检查这个分类是否包含当前的 detail
                            if (damageCategory.Children != null && damageCategory.Children.Any(grandChild =>
                                grandChild.FileName == detail.FileName && grandChild.Id == detail.Id))
                            {
                                //Console.WriteLine($"找到对应分类: {damageCategory.Name}");
                                BatchSaveMarked(damageCategory);
                                return;
                            }

                        }
                        if (child is DamageCategorySummaryTree damageCategorySummary)
                        {
                            //遍历damageCategorySummary
                            foreach (var subChild in damageCategorySummary.Children)
                            {
                                if (subChild is DamageCategoryTree subDamageCategory)
                                {
                                    // 检查这个分类是否包含当前的 detail
                                    if (subDamageCategory.Children != null && subDamageCategory.Children.Any(grandChild =>
                                        grandChild.FileName == detail.FileName && grandChild.Id == detail.Id))
                                    {
                                        //Console.WriteLine($"找到对应分类: {subDamageCategory.Name}");
                                        BatchSaveMarked(subDamageCategory);
                                        return;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private void OpenImgFolder(object? dataContext)
        {
            if (dataContext is Details detail)
            {
                //图片路径
                string filePath = detail._tempImagePath;
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                {
                    //如果_tempImagePath为空或文件不存在，则使用默认路径
                    filePath = System.IO.Path.Combine(MainVm.damageImgFolderName.OutReplaceInString(), detail.FileName);
                }
                string folderPath = System.IO.Path.GetDirectoryName(filePath);
                if (Directory.Exists(folderPath))
                {
                    Process.Start("explorer.exe", folderPath);
                }
                else
                {
                    HandyControl.Controls.MessageBox.Show("文件夹不存在: " + folderPath);
                }
            }
        }

        private void BatchSaveUnmarked(object dataContext)
        {
            // 确保数据上下文是目标类型
            if (dataContext is not DamageCategoryTree categoryTree)
                return;

            // 1. 先统一选择一次保存路径（只弹一次对话框）
            string customSavePath = null;
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = "选择批量保存所有无标记图片的文件夹"; // 明确提示用途
                dialog.ShowNewFolderButton = true; // 允许用户新建文件夹

                // 只判断一次选择结果：选了路径就继续，取消就直接退出
                if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                {
                    return; // 用户取消选择，不执行后续操作
                }
                customSavePath = dialog.SelectedPath;
            }
            categoryTree._tempImagePath = customSavePath;

            // 2. 验证选择的路径有效性
            if (string.IsNullOrWhiteSpace(customSavePath))
            {
                HandyControl.Controls.MessageBox.Show("保存路径无效，请重新选择！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 3. 批量处理所有图片（循环内不再弹对话框）
            int successCount = 0; // 统计成功保存的数量
            List<string> failedFiles = new List<string>(); // 记录失败的文件（便于后续提示）

            foreach (var detail in categoryTree.Children.OfType<Details>())
            {
                if (detail.IsChecked)
                {
                    try
                    {
                        // 3.1 处理源图片路径
                        string sourceFilePath = System.IO.Path.Combine(
                            MainVm.damageImgFolderName.OutReplaceInString(),
                            detail.FileName
                        );

                        // 3.2 检查源文件是否存在（避免无效文件导致批量中断）
                        if (!System.IO.File.Exists(sourceFilePath))
                        {
                            failedFiles.Add($"{detail.FileName}（源文件不存在）");
                            continue; // 跳过不存在的文件，继续处理下一个
                        }

                        // 3.3 构建目标文件路径（所有图片保存到同一选择的路径）
                        string destFilePath = System.IO.Path.Combine(
                            customSavePath,
                            detail.FileName
                        );

                        // 3.4 复制图片（true 表示覆盖同名文件，可根据需求调整）
                        System.IO.File.Copy(sourceFilePath, destFilePath, overwrite: true);

                        // 3.5 保留原逻辑：记录临时路径
                        detail._tempImagePath = destFilePath;

                        successCount++; // 成功计数+1
                    }
                    catch (Exception ex)
                    {
                        // 捕获单个文件的异常，不中断批量处理
                        failedFiles.Add($"{detail.FileName}（错误：{ex.Message}）");
                    }
                }
            }

            // 4. 批量处理完成后，统一提示结果（清晰告知成功/失败情况）
            string resultMsg = $"批量导出完成！\n成功保存：{successCount} 张图片\n";
            if (failedFiles.Count > 0)
            {
                resultMsg += $"失败：{failedFiles.Count} 张图片\n" +
                             string.Join("\n", failedFiles); // 列出失败的文件详情
            }

            HandyControl.Controls.MessageBox.Show(
                resultMsg,
                "批量导出结果",
                MessageBoxButton.OK,
                failedFiles.Count > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information
            );
        }

        private void BatchSaveMarked(object dataContext)
        {
            // 确保数据上下文是目标类型
            if (dataContext is not DamageCategoryTree categoryTree)
                return;

            // 1. 先统一选择一次保存路径（只弹一次对话框）
            string customSavePath = null;
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = "选择批量保存所有无标记图片的文件夹"; // 明确提示用途
                dialog.ShowNewFolderButton = true; // 允许用户新建文件夹

                // 只判断一次选择结果：选了路径就继续，取消就直接退出
                if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                {
                    return; // 用户取消选择，不执行后续操作
                }
                customSavePath = dialog.SelectedPath;
            }
            categoryTree._tempImagePath = customSavePath;

            // 2. 验证选择的路径有效性
            if (string.IsNullOrWhiteSpace(customSavePath))
            {
                HandyControl.Controls.MessageBox.Show("保存路径无效，请重新选择！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 3. 批量处理所有图片（循环内不再弹对话框）
            int successCount = 0; // 统计成功保存的数量
            List<string> failedFiles = new List<string>(); // 记录失败的文件（便于后续提示）

            foreach (var detail in categoryTree.Children.OfType<Details>())
            {
                if (detail.IsChecked)
                {
                    try
                    {
                        // 3.1 处理源图片路径
                        string sourceFilePath = System.IO.Path.Combine(
                            MainVm.damageImgFolderName.InReplaceOutString(),
                            detail.FileName
                        );

                        // 3.2 检查源文件是否存在（避免无效文件导致批量中断）
                        if (!System.IO.File.Exists(sourceFilePath))
                        {
                            failedFiles.Add($"{detail.FileName}（源文件不存在）");
                            continue; // 跳过不存在的文件，继续处理下一个
                        }

                        // 3.3 构建目标文件路径（所有图片保存到同一选择的路径）
                        string destFilePath = System.IO.Path.Combine(
                            customSavePath,
                            detail.FileName
                        );

                        // 3.4 复制图片（true 表示覆盖同名文件，可根据需求调整）
                        System.IO.File.Copy(sourceFilePath, destFilePath, overwrite: true);

                        // 3.5 保留原逻辑：记录临时路径
                        detail._tempImagePath = destFilePath;

                        successCount++; // 成功计数+1
                    }
                    catch (Exception ex)
                    {
                        // 捕获单个文件的异常，不中断批量处理
                        failedFiles.Add($"{detail.FileName}（错误：{ex.Message}）");
                    }
                }
            }

            // 4. 批量处理完成后，统一提示结果（清晰告知成功/失败情况）
            string resultMsg = $"批量导出完成！\n成功保存：{successCount} 张图片\n";
            if (failedFiles.Count > 0)
            {
                resultMsg += $"失败：{failedFiles.Count} 张图片\n" +
                             string.Join("\n", failedFiles); // 列出失败的文件详情
            }

            HandyControl.Controls.MessageBox.Show(
                resultMsg,
                "批量导出结果",
                MessageBoxButton.OK,
                failedFiles.Count > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information
            );
        }


        private void OpenAllFolder(object dataContext)
        {
            // 确保数据上下文是目标类型
            if (dataContext is not DamageCategoryTree categoryTree)
                return;
            //直接打开categoryTree对应的文件夹
            string folderPath = categoryTree._tempImagePath;
            if (string.IsNullOrEmpty(folderPath))
            {
                folderPath = MainVm.damageImgFolderName.OutReplaceInString();
            }
            Process.Start("explorer.exe", folderPath);
        }

        private void ExportAllUnmarked(object dataContext)
        {
            // 确保数据上下文是目标类型
            if (dataContext is not DamageCategoryTree categoryTree)
                return;

            // 1. 询问是否需要重新排序序号
            var reorderResult = HandyControl.Controls.MessageBox.Show(
                "是否要重新排序序号？\n\n" +
                "• 是：文件名将从1开始重新编号\n" +
                "• 否：保持原有文件名不变",
                "重新排序确认",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question
            );

            // 用户取消操作
            if (reorderResult == MessageBoxResult.Cancel)
                return;

            bool shouldReorder = (reorderResult == MessageBoxResult.Yes);

            // 2. 选择保存路径
            string customSavePath = null;
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = "选择批量保存所有无标记图片的文件夹";
                dialog.ShowNewFolderButton = true;

                if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                {
                    return;
                }
                customSavePath = dialog.SelectedPath;
            }
            categoryTree._tempImagePath = customSavePath;

            // 3. 验证路径有效性
            if (string.IsNullOrWhiteSpace(customSavePath))
            {
                HandyControl.Controls.MessageBox.Show("保存路径无效，请重新选择！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 4. 批量处理所有图片
            int successCount = 0;
            int currentOrder = 1; // 重新排序的起始序号
            List<string> failedFiles = new List<string>();

            foreach (var detail in categoryTree.Children.OfType<Details>())
            {
                try
                {
                    // 4.1 处理源图片路径
                    string sourceFilePath = System.IO.Path.Combine(
                        MainVm.damageImgFolderName.OutReplaceInString(),
                        detail.FileName
                    );

                    // 4.2 检查源文件是否存在
                    if (!System.IO.File.Exists(sourceFilePath))
                    {
                        failedFiles.Add($"{detail.FileName}（源文件不存在）");
                        continue;
                    }

                    // 4.3 构建目标文件名
                    string targetFileName;
                    if (shouldReorder)
                    {
                        // 重新排序：从1开始编号，保留原文件扩展名
                        string fileExtension = System.IO.Path.GetExtension(detail.FileName);
                        targetFileName = $"{currentOrder}{fileExtension}"; // 格式化为3位数，如001.jpg
                        currentOrder++; // 序号递增
                    }
                    else
                    {
                        // 保持原文件名
                        targetFileName = detail.FileName;
                    }

                    // 4.4 构建目标文件路径
                    string destFilePath = System.IO.Path.Combine(customSavePath, targetFileName);

                    // 4.5 复制图片
                    System.IO.File.Copy(sourceFilePath, destFilePath, overwrite: true);

                    // 4.6 记录临时路径
                    detail._tempImagePath = destFilePath;

                    successCount++;
                }
                catch (Exception ex)
                {
                    failedFiles.Add($"{detail.FileName}（错误：{ex.Message}）");
                }
            }

            // 5. 显示批量处理结果
            string resultMsg = $"批量导出完成！\n" +
                              $"{(shouldReorder ? "已重新排序，" : "")}成功保存：{successCount} 张图片\n" +
                              $"保存路径：{customSavePath}";

            if (failedFiles.Count > 0)
            {
                resultMsg += $"\n\n失败：{failedFiles.Count} 张图片\n" +
                             string.Join("\n", failedFiles);
            }

            HandyControl.Controls.MessageBox.Show(
                resultMsg,
                "批量导出结果",
                MessageBoxButton.OK,
                failedFiles.Count > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information
            );
        }

        private void ExportAllMarked(object dataContext)
        {
            // 确保数据上下文是目标类型
            if (dataContext is not DamageCategoryTree categoryTree)
                return;

            // 1. 询问是否需要重新排序序号
            var reorderResult = HandyControl.Controls.MessageBox.Show(
                "是否要重新排序序号？\n\n" +
                "• 是：文件名将从1开始重新编号\n" +
                "• 否：保持原有文件名不变",
                "重新排序确认",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question
            );

            // 用户取消操作
            if (reorderResult == MessageBoxResult.Cancel)
                return;

            bool shouldReorder = (reorderResult == MessageBoxResult.Yes);

            // 2. 选择保存路径
            string customSavePath = null;
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = "选择批量保存所有有标记图片的文件夹";
                dialog.ShowNewFolderButton = true;

                if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                {
                    return;
                }
                customSavePath = dialog.SelectedPath;
            }

            categoryTree._tempImagePath = customSavePath;

            // 3. 验证路径有效性
            if (string.IsNullOrWhiteSpace(customSavePath))
            {
                HandyControl.Controls.MessageBox.Show("保存路径无效，请重新选择！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 4. 批量处理所有图片
            int successCount = 0;
            int currentOrder = 1; // 重新排序的起始序号
            List<string> failedFiles = new List<string>();

            foreach (var detail in categoryTree.Children.OfType<Details>())
            {
                try
                {
                    // 4.1 处理源图片路径
                    string sourceFilePath = System.IO.Path.Combine(
                        MainVm.damageImgFolderName.InReplaceOutString(),
                        detail.FileName
                    );

                    // 4.2 检查源文件是否存在
                    if (!System.IO.File.Exists(sourceFilePath))
                    {
                        failedFiles.Add($"{detail.FileName}（源文件不存在）");
                        continue;
                    }

                    // 4.3 构建目标文件名
                    string targetFileName;
                    if (shouldReorder)
                    {
                        // 重新排序：从1开始编号，保留原文件扩展名
                        string fileExtension = System.IO.Path.GetExtension(detail.FileName);
                        targetFileName = $"{currentOrder}{fileExtension}";
                        currentOrder++; // 序号递增
                    }
                    else
                    {
                        // 保持原文件名
                        targetFileName = detail.FileName;
                    }

                    // 4.4 构建目标文件路径
                    string destFilePath = System.IO.Path.Combine(customSavePath, targetFileName);

                    // 4.5 复制图片
                    System.IO.File.Copy(sourceFilePath, destFilePath, overwrite: true);

                    // 4.6 记录临时路径
                    detail._tempImagePath = destFilePath;

                    successCount++;
                }
                catch (Exception ex)
                {
                    failedFiles.Add($"{detail.FileName}（错误：{ex.Message}）");
                }
            }

            // 5. 显示批量处理结果
            string resultMsg = $"批量导出完成！\n" +
                              $"{(shouldReorder ? "已重新排序，" : "")}成功保存：{successCount} 张图片\n" +
                              $"保存路径：{customSavePath}";

            if (failedFiles.Count > 0)
            {
                resultMsg += $"\n\n失败：{failedFiles.Count} 张图片\n" +
                             string.Join("\n", failedFiles);
            }

            HandyControl.Controls.MessageBox.Show(
                resultMsg,
                "批量导出结果",
                MessageBoxButton.OK,
                failedFiles.Count > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information
            );
        }
        private void SaveUnmarkedImage(object dataContext)
        {
            //将dataContext转换为Details类型
            if (dataContext is Details detail)
            {
                //图片路径
                string filePath = System.IO.Path.Combine(MainVm.damageImgFolderName.OutReplaceInString(), detail.FileName);
                //将filePath图片另存为某个指定路径
                string customSavePath;
                using (FolderBrowserDialog dialog = new FolderBrowserDialog())
                {
                    if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        customSavePath = dialog.SelectedPath;
                    }
                    else
                    {
                        return;
                    }
                }
                string destFilePath = System.IO.Path.Combine(customSavePath, detail.FileName);
                detail._tempImagePath = destFilePath;
                File.Copy(filePath, destFilePath, true);
                HandyControl.Controls.MessageBox.Show("图片已保存到: " + destFilePath);
            }
        }

        /// <summary>
        /// 保存标记的图片
        /// </summary>
        /// <param name="dataContext"></param>
        private void SaveMarkedImage(object dataContext)
        {
            if (dataContext is Details detail)
            {
                //图片路径
                string filePath = System.IO.Path.Combine(MainVm.damageImgFolderName.InReplaceOutString(), detail.FileName);
                //将filePath图片另存为某个指定路径
                string customSavePath;
                using (FolderBrowserDialog dialog = new FolderBrowserDialog())
                {
                    if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        customSavePath = dialog.SelectedPath;
                    }
                    else
                    {
                        return;
                    }
                }
                string destFilePath = System.IO.Path.Combine(customSavePath, detail.FileName);
                detail._tempImagePath = destFilePath;
                File.Copy(filePath, destFilePath, true);
                HandyControl.Controls.MessageBox.Show("图片已保存到: " + destFilePath);

            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 检查是否有图片源
                if (damageViewer.SourceImage == null)
                {
                    MessageBox.Show("没有可保存的图片！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 创建保存文件对话框
                SaveFileDialog saveFileDialog = new SaveFileDialog();

                // 设置文件过滤器
                saveFileDialog.Filter = "PNG 图片|*.png|JPEG 图片|*.jpg|BMP 图片|*.bmp|所有文件|*.*";
                saveFileDialog.FilterIndex = 1;
                saveFileDialog.DefaultExt = ".png";

                // 设置默认文件名（使用当前时间戳）
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                saveFileDialog.FileName = $"damage_image_{timestamp}";

                // 显示保存对话框
                if (saveFileDialog.ShowDialog() == true)
                {
                    // 获取选择的文件路径
                    string filePath = saveFileDialog.FileName;

                    // 根据文件扩展名选择编码器
                    BitmapEncoder encoder = GetEncoder(System.IO.Path.GetExtension(filePath));

                    // 将 SourceImage 转换为 BitmapSource
                    BitmapSource bitmapSource = damageViewer.SourceImage as BitmapSource;

                    if (bitmapSource != null)
                    {
                        // 编码并保存图片
                        encoder.Frames.Add(BitmapFrame.Create(bitmapSource));

                        using (FileStream stream = new FileStream(filePath, FileMode.Create))
                        {
                            encoder.Save(stream);
                        }

                        MessageBox.Show($"图片已保存到：{filePath}", "保存成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("无法获取图片数据！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存图片时出错：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        // 根据文件扩展名获取对应的编码器
        private BitmapEncoder GetEncoder(string extension)
        {
            switch (extension.ToLower())
            {
                case ".jpg":
                case ".jpeg":
                    return new JpegBitmapEncoder() { QualityLevel = 95 };
                case ".png":
                    return new PngBitmapEncoder();
                case ".bmp":
                    return new BmpBitmapEncoder();
                case ".gif":
                    return new GifBitmapEncoder();
                case ".tiff":
                    return new TiffBitmapEncoder();
                default:
                    return new PngBitmapEncoder(); // 默认使用PNG
            }
        }
        private void damageViewer_Loaded_1(object sender, RoutedEventArgs e)
        {
            // 你的加载逻辑
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {

        }

        private void Button_Click_2()
        {

        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {

        }

        private void SearchBar_TextChanged_1(object sender, TextChangedEventArgs e)
        {

        }

        private System.Windows.Controls.ScrollViewer FindScrollViewer(DependencyObject parent)
        {
            if (parent == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is System.Windows.Controls.ScrollViewer result)
                    return result;

                var childResult = FindScrollViewer(child);
                if (childResult != null)
                    return childResult;
            }

            return null;
        }

        private void ToggleButton_Checked_2(object sender, RoutedEventArgs e)
        {

        }
    }
}