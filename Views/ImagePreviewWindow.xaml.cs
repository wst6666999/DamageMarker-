using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DamageMaker.Views
{
    public partial class ImagePreviewWindow : Window
    {
        private List<BitmapImage> _currentImages = new List<BitmapImage>();
        private List<BitmapImage> _compareImages = new List<BitmapImage>();
        private int _currentImageIndex = 0;
        private int _compareImageIndex = 0;

        // 拖拽相关变量
        private bool _isDraggingCurrent = false;
        private bool _isDraggingCompare = false;
        private Point _startPointCurrent;
        private Point _startPointCompare;
        private Point _lastPositionCurrent;
        private Point _lastPositionCompare;

        // XAML中已定义的变换，这里直接使用
        // TranslateTransform currentTranslateTransform
        // ScaleTransform currentScaleTransform
        // TranslateTransform compareTranslateTransform
        // ScaleTransform compareScaleTransform

        // 默认构造函数
        public ImagePreviewWindow()
        {
            try
            {
                InitializeComponent();

                // 绑定事件
                sliderZoom.ValueChanged += SliderZoom_ValueChanged;
                sliderZoom.Value = 1.0; // 默认缩放100%

                // 初始化默认图片
                _currentImages.Add(CreateDefaultImage());

                // 等待窗口加载完成后显示图片
                this.Loaded += (s, e) =>
                {
                    ShowCurrentImage(0);
                    SetSingleImageMode();
                    UpdateZoomText();
                    ResetZoomToFit();
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"窗口初始化失败: {ex.Message}");
                throw;
            }
        }

        // 构造函数1：当前周期多图，对比周期单图
        public ImagePreviewWindow(List<BitmapImage> currentImages, BitmapImage compareImage = null)
        {
            try
            {
                // 准备数据
                _currentImages = currentImages?.Where(img => img != null).ToList() ?? new List<BitmapImage>();
                if (compareImage != null)
                {
                    _compareImages.Add(compareImage);
                }

                // 确保至少有一张图片
                if (!_currentImages.Any())
                {
                    _currentImages.Add(CreateDefaultImage());
                }

                InitializeComponent();

                // 绑定事件
                sliderZoom.ValueChanged += SliderZoom_ValueChanged;
                sliderZoom.Value = 1.0; // 默认缩放100%

                // 等待窗口加载完成后显示图片
                this.Loaded += (s, e) =>
                {
                    ShowCurrentImage(0);

                    if (_compareImages.Any())
                    {
                        ShowCompareImage(0);
                    }
                    else
                    {
                        SetSingleImageMode();
                    }

                    UpdateZoomText();

                    // 根据图片尺寸自动调整缩放
                    ResetZoomToFit();
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"构造函数1失败: {ex.Message}");
                throw;
            }
        }

        // 构造函数2：当前周期多图，对比周期单图（字节数组）
        public ImagePreviewWindow(List<byte[]> currentImageData, byte[] compareImageData = null)
        {
            try
            {
                // 准备数据
                _currentImages = currentImageData?
                    .Select(data => LoadImageFromBytes(data))
                    .Where(img => img != null)
                    .ToList() ?? new List<BitmapImage>();

                if (compareImageData != null)
                {
                    var compareImage = LoadImageFromBytes(compareImageData);
                    if (compareImage != null)
                    {
                        _compareImages.Add(compareImage);
                    }
                }

                // 确保至少有一张图片
                if (!_currentImages.Any())
                {
                    _currentImages.Add(CreateDefaultImage());
                }

                InitializeComponent();

                // 绑定事件
                sliderZoom.ValueChanged += SliderZoom_ValueChanged;
                sliderZoom.Value = 1.0; // 默认缩放100%

                // 等待窗口加载完成后显示图片
                this.Loaded += (s, e) =>
                {
                    ShowCurrentImage(0);

                    if (_compareImages.Any())
                    {
                        ShowCompareImage(0);
                    }
                    else
                    {
                        SetSingleImageMode();
                    }

                    UpdateZoomText();
                    ResetZoomToFit();
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"构造函数2失败: {ex.Message}");
                throw;
            }
        }

        // 构造函数3：当前周期单图，对比周期多图
        public ImagePreviewWindow(BitmapImage currentImage, List<BitmapImage> compareImages = null)
        {
            try
            {
                // 准备数据
                if (currentImage != null)
                {
                    _currentImages.Add(currentImage);
                }
                _compareImages = compareImages?.Where(img => img != null).ToList() ?? new List<BitmapImage>();

                // 确保至少有一张当前图片
                if (!_currentImages.Any())
                {
                    _currentImages.Add(CreateDefaultImage());
                }

                InitializeComponent();

                // 绑定事件
                sliderZoom.ValueChanged += SliderZoom_ValueChanged;
                sliderZoom.Value = 1.0; // 默认缩放100%

                // 等待窗口加载完成后显示图片
                this.Loaded += (s, e) =>
                {
                    ShowCurrentImage(0);

                    if (_compareImages.Any())
                    {
                        ShowCompareImage(0);
                    }
                    else
                    {
                        SetSingleImageMode();
                    }

                    UpdateZoomText();
                    ResetZoomToFit();
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"构造函数3失败: {ex.Message}");
                throw;
            }
        }

        // 构造函数7：带标题的构造函数
        public ImagePreviewWindow(string currentTitle, string compareTitle, List<BitmapImage> currentImages, BitmapImage compareImage = null)
        {
            try
            {
                // 准备数据
                _currentImages = currentImages?.Where(img => img != null).ToList() ?? new List<BitmapImage>();
                if (compareImage != null)
                {
                    _compareImages.Add(compareImage);
                }

                // 确保至少有一张图片
                if (!_currentImages.Any())
                {
                    _currentImages.Add(CreateDefaultImage());
                }

                InitializeComponent();

                // 绑定事件
                sliderZoom.ValueChanged += SliderZoom_ValueChanged;
                sliderZoom.Value = 1.0; // 默认缩放100%

                // 设置标题
                SetTitles(currentTitle, compareTitle);

                // 等待窗口加载完成后显示图片
                this.Loaded += (s, e) =>
                {
                    ShowCurrentImage(0);

                    if (_compareImages.Any())
                    {
                        ShowCompareImage(0);
                    }
                    else
                    {
                        SetSingleImageMode();
                    }

                    UpdateZoomText();
                    ResetZoomToFit();
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"构造函数7失败: {ex.Message}");
                throw;
            }
        }

        // 设置单图模式
        private void SetSingleImageMode()
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    // 找到最外层的Grid
                    var mainGrid = this.FindName("MainGrid") as Grid;
                    if (mainGrid != null && mainGrid.ColumnDefinitions.Count >= 3)
                    {
                        mainGrid.ColumnDefinitions[1].Width = new GridLength(0);
                        mainGrid.ColumnDefinitions[2].Width = new GridLength(0);
                    }

                    txtCompareTitle.Visibility = Visibility.Collapsed;
                    imgComparePreview.Visibility = Visibility.Collapsed;
                    compareImageControls.Visibility = Visibility.Collapsed;
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"设置单图模式失败: {ex.Message}");
            }
        }

        // 显示当前周期指定索引的图片
        private void ShowCurrentImage(int index)
        {
            try
            {
                if (_currentImages == null || _currentImages.Count == 0)
                    return;

                if (index >= 0 && index < _currentImages.Count)
                {
                    _currentImageIndex = index;
                    var image = _currentImages[index];

                    if (image != null)
                    {
                        imgCurrentPreview.Source = image;

                        // 重置位置和缩放
                        ResetImagePosition(imgCurrentPreview, currentTranslateTransform);

                        // 等待图片加载完成后自动适配
                        if (image.IsDownloading)
                        {
                            image.DownloadCompleted += (s, e) =>
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    AutoFitImageSize(true);
                                });
                            };
                        }
                        else
                        {
                            AutoFitImageSize(true);
                        }
                    }
                    else
                    {
                        imgCurrentPreview.Source = CreateDefaultImage();
                    }

                    UpdateCurrentPageInfo();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"显示图片失败: {ex.Message}");
                imgCurrentPreview.Source = CreateDefaultImage();
            }
        }

        // 显示对比图片的方法
        private void ShowCompareImage(int index)
        {
            try
            {
                if (_compareImages == null || _compareImages.Count == 0)
                    return;

                if (index >= 0 && index < _compareImages.Count)
                {
                    _compareImageIndex = index;
                    var image = _compareImages[index];

                    if (image != null)
                    {
                        imgComparePreview.Source = image;

                        // 重置位置和缩放
                        ResetImagePosition(imgComparePreview, compareTranslateTransform);

                        // 等待图片加载完成后自动适配
                        if (image.IsDownloading)
                        {
                            image.DownloadCompleted += (s, e) =>
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    AutoFitImageSize(false);
                                });
                            };
                        }
                        else
                        {
                            AutoFitImageSize(false);
                        }
                    }
                    else
                    {
                        imgComparePreview.Source = CreateDefaultImage();
                    }

                    UpdateComparePageInfo();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"显示对比图片失败: {ex.Message}");
                imgComparePreview.Source = CreateDefaultImage();
            }
        }

        // 自动适配图片尺寸
        // 自动适配图片尺寸 - 修改版本
        private void AutoFitImageSize(bool isCurrentImage)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    var image = isCurrentImage ? imgCurrentPreview : imgComparePreview;
                    var container = isCurrentImage ? currentImageContainer : compareImageContainer;
                    var scaleTransform = isCurrentImage ? currentScaleTransform : compareScaleTransform;

                    if (image.Source is BitmapImage bitmap && bitmap.PixelWidth > 0 && bitmap.PixelHeight > 0)
                    {
                        // 获取容器可用尺寸（减去内边距）
                        double containerWidth = container.ActualWidth - 20; // 10px padding * 2
                        double containerHeight = container.ActualHeight - 20;

                        if (containerWidth > 0 && containerHeight > 0)
                        {
                            // 计算合适的缩放比例
                            double scaleX = containerWidth / bitmap.PixelWidth;
                            double scaleY = containerHeight / bitmap.PixelHeight;
                            double fitScale = Math.Min(scaleX, scaleY);

                            // 修改这里：移除最大缩放限制，设置默认缩放为105%
                            double defaultScale = 1.05; // 105%
                            fitScale = Math.Max(fitScale, 0.1);

                            // 如果自动适配的缩放小于105%，则使用105%
                            if (fitScale < defaultScale)
                            {
                                fitScale = defaultScale;
                            }

                            // 应用缩放
                            scaleTransform.ScaleX = fitScale;
                            scaleTransform.ScaleY = fitScale;

                            // 更新Slider
                            sliderZoom.Value = fitScale;
                        }
                        else
                        {
                            // 如果容器尺寸无效，直接设置为105%
                            scaleTransform.ScaleX = 1.05;
                            scaleTransform.ScaleY = 1.05;
                            sliderZoom.Value = 1.05;
                        }
                    }
                    else
                    {
                        // 如果图片无效，直接设置为105%
                        scaleTransform.ScaleX = 1.05;
                        scaleTransform.ScaleY = 1.05;
                        sliderZoom.Value = 1.05;
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"自动适配图片尺寸失败: {ex.Message}");
            }
        }
        // 重置缩放以适配窗口
        private void ResetZoomToFit()
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    // 重置当前图片
                    if (imgCurrentPreview.Source != null)
                    {
                        AutoFitImageSize(true);
                    }

                    // 重置对比图片
                    if (imgComparePreview.Visibility == Visibility.Visible && imgComparePreview.Source != null)
                    {
                        AutoFitImageSize(false);
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"重置缩放失败: {ex.Message}");
            }
        }

        // 更新当前周期页码信息
        private void UpdateCurrentPageInfo()
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    if (_currentImages != null && _currentImages.Count > 1)
                    {
                        txtCurrentPage.Text = $"{_currentImageIndex + 1}/{_currentImages.Count}";
                        currentImageControls.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        txtCurrentPage.Text = string.Empty;
                        currentImageControls.Visibility = Visibility.Collapsed;
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"更新当前页码信息失败: {ex.Message}");
            }
        }

        // 更新对比图片页码信息
        private void UpdateComparePageInfo()
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    if (_compareImages != null && _compareImages.Count > 1)
                    {
                        txtComparePage.Text = $"{_compareImageIndex + 1}/{_compareImages.Count}";
                        compareImageControls.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        txtComparePage.Text = string.Empty;
                        compareImageControls.Visibility = Visibility.Collapsed;
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"更新对比页码信息失败: {ex.Message}");
            }
        }

        // 创建默认图片
        private BitmapImage CreateDefaultImage()
        {
            try
            {
                // 创建一个简单的默认图片（黑色背景，白色文字）
                int width = 400;
                int height = 400;

                var renderBitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                var visual = new DrawingVisual();

                using (DrawingContext context = visual.RenderOpen())
                {
                    // 背景
                    context.DrawRectangle(Brushes.Black, null, new Rect(0, 0, width, height));

                    // 文字
                    var formattedText = new FormattedText(
                        "无图片",
                        System.Globalization.CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        new Typeface("Arial"),
                        24,
                        Brushes.White,
                        1.0);

                    context.DrawText(formattedText, new Point(width / 2 - 40, height / 2 - 12));
                }

                renderBitmap.Render(visual);

                // 转换为 BitmapImage
                var bitmapImage = new BitmapImage();
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(renderBitmap));

                using (var stream = new MemoryStream())
                {
                    encoder.Save(stream);
                    stream.Seek(0, SeekOrigin.Begin);

                    bitmapImage.BeginInit();
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.StreamSource = stream;
                    bitmapImage.EndInit();
                    bitmapImage.Freeze();
                }

                return bitmapImage;
            }
            catch
            {
                // 如果创建失败，返回 null
                return null;
            }
        }

        // 加载图片
        private BitmapImage LoadImageFromBytes(byte[] imageData)
        {
            if (imageData == null || imageData.Length == 0)
                return null;

            try
            {
                var bitmapImage = new BitmapImage();
                using (var stream = new MemoryStream(imageData))
                {
                    bitmapImage.BeginInit();
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.StreamSource = stream;
                    bitmapImage.EndInit();
                }
                bitmapImage.Freeze();
                return bitmapImage;
            }
            catch
            {
                return null;
            }
        }

        // 事件处理：当前周期上一张
        private void BtnPrevCurrent_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_currentImages == null || _currentImages.Count <= 1) return;

                _currentImageIndex--;
                if (_currentImageIndex < 0)
                {
                    _currentImageIndex = _currentImages.Count - 1;
                }

                ShowCurrentImage(_currentImageIndex);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"上一张按钮点击失败: {ex.Message}");
            }
        }

        // 事件处理：当前周期下一张
        private void BtnNextCurrent_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_currentImages == null || _currentImages.Count <= 1) return;

                _currentImageIndex++;
                if (_currentImageIndex >= _currentImages.Count)
                {
                    _currentImageIndex = 0;
                }

                ShowCurrentImage(_currentImageIndex);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"下一张按钮点击失败: {ex.Message}");
            }
        }

        // 缩放控制
        private void SliderZoom_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            try
            {
                ApplyZoom(e.NewValue);
                UpdateZoomText();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"缩放控制失败: {ex.Message}");
            }
        }

        // 应用缩放
        private void ApplyZoom(double scale)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    // 应用缩放变换到当前图片
                    if (currentScaleTransform != null)
                    {
                        currentScaleTransform.ScaleX = scale;
                        currentScaleTransform.ScaleY = scale;
                    }

                    // 应用缩放变换到对比图片
                    if (imgComparePreview.Visibility == Visibility.Visible && compareScaleTransform != null)
                    {
                        compareScaleTransform.ScaleX = scale;
                        compareScaleTransform.ScaleY = scale;
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"应用缩放失败: {ex.Message}");
            }
        }

        // 更新缩放百分比
        private void UpdateZoomText()
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    int percentage = (int)(sliderZoom.Value * 100);
                    txtZoomLevel.Text = $"{percentage}%";
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"更新缩放文本失败: {ex.Message}");
            }
        }

        // 设置标题
        public void SetTitles(string currentTitle = "当前周期", string compareTitle = "对比周期")
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    txtCurrentTitle.Text = currentTitle;
                    txtCompareTitle.Text = compareTitle;
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"设置标题失败: {ex.Message}");
            }
        }

        // 事件处理：对比周期上一张
        private void BtnPrevCompare_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_compareImages == null || _compareImages.Count <= 1) return;

                _compareImageIndex--;
                if (_compareImageIndex < 0)
                {
                    _compareImageIndex = _compareImages.Count - 1;
                }

                ShowCompareImage(_compareImageIndex);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"对比上一张按钮点击失败: {ex.Message}");
            }
        }

        // 事件处理：对比周期下一张
        private void BtnNextCompare_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_compareImages == null || _compareImages.Count <= 1) return;

                _compareImageIndex++;
                if (_compareImageIndex >= _compareImages.Count)
                {
                    _compareImageIndex = 0;
                }

                ShowCompareImage(_compareImageIndex);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"对比下一张按钮点击失败: {ex.Message}");
            }
        }

        // 鼠标按下事件 - 开始拖拽
        private void Image_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var image = sender as Image;
            if (image == null) return;

            // 判断是当前图片还是对比图片
            bool isCurrentImage = image == imgCurrentPreview;
            var container = isCurrentImage ? currentImageContainer : compareImageContainer;

            if (isCurrentImage)
            {
                _isDraggingCurrent = true;
                _startPointCurrent = e.GetPosition(container);
                _lastPositionCurrent = new Point(currentTranslateTransform.X, currentTranslateTransform.Y);
                image.CaptureMouse();
                Cursor = Cursors.Hand;
            }
            else
            {
                _isDraggingCompare = true;
                _startPointCompare = e.GetPosition(container);
                _lastPositionCompare = new Point(compareTranslateTransform.X, compareTranslateTransform.Y);
                image.CaptureMouse();
                Cursor = Cursors.Hand;
            }
        }

        // 鼠标移动事件 - 执行拖拽
        private void Image_MouseMove(object sender, MouseEventArgs e)
        {
            var image = sender as Image;
            if (image == null) return;

            if (_isDraggingCurrent && image == imgCurrentPreview)
            {
                var currentPoint = e.GetPosition(currentImageContainer);
                double deltaX = currentPoint.X - _startPointCurrent.X;
                double deltaY = currentPoint.Y - _startPointCurrent.Y;

                currentTranslateTransform.X = _lastPositionCurrent.X + deltaX;
                currentTranslateTransform.Y = _lastPositionCurrent.Y + deltaY;
            }
            else if (_isDraggingCompare && image == imgComparePreview)
            {
                var currentPoint = e.GetPosition(compareImageContainer);
                double deltaX = currentPoint.X - _startPointCompare.X;
                double deltaY = currentPoint.Y - _startPointCompare.Y;

                compareTranslateTransform.X = _lastPositionCompare.X + deltaX;
                compareTranslateTransform.Y = _lastPositionCompare.Y + deltaY;
            }
        }

        // 鼠标释放事件 - 结束拖拽
        private void Image_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var image = sender as Image;
            if (image == null) return;

            if (_isDraggingCurrent && image == imgCurrentPreview)
            {
                _isDraggingCurrent = false;
                image.ReleaseMouseCapture();
                Cursor = Cursors.Arrow;
            }
            else if (_isDraggingCompare && image == imgComparePreview)
            {
                _isDraggingCompare = false;
                image.ReleaseMouseCapture();
                Cursor = Cursors.Arrow;
            }
        }

        // 重置位置方法
        private void ResetImagePosition(Image image, TranslateTransform transform)
        {
            try
            {
                if (image != null && transform != null)
                {
                    transform.X = 0;
                    transform.Y = 0;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"重置图片位置失败: {ex.Message}");
            }
        }

        // 重置位置按钮事件
        private void BtnResetPosition_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 重置当前图片位置
                ResetImagePosition(imgCurrentPreview, currentTranslateTransform);

                // 重置对比图片位置
                if (imgComparePreview.Visibility == Visibility.Visible)
                {
                    ResetImagePosition(imgComparePreview, compareTranslateTransform);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"重置位置按钮点击失败: {ex.Message}");
            }
        }

        // 重置缩放按钮事件
        private void BtnResetZoom_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ResetZoomToFit();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"重置缩放按钮点击失败: {ex.Message}");
            }
        }

        // 清理资源
        protected override void OnClosed(EventArgs e)
        {
            try
            {
                sliderZoom.ValueChanged -= SliderZoom_ValueChanged;

                // 清理图片资源
                if (_currentImages != null)
                {
                    foreach (var image in _currentImages)
                    {
                        if (image is BitmapImage bitmap && bitmap.StreamSource != null)
                        {
                            bitmap.StreamSource.Dispose();
                        }
                    }
                }

                if (_compareImages != null)
                {
                    foreach (var image in _compareImages)
                    {
                        if (image is BitmapImage bitmap && bitmap.StreamSource != null)
                        {
                            bitmap.StreamSource.Dispose();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"清理资源失败: {ex.Message}");
            }
            finally
            {
                base.OnClosed(e);
            }
        }
    }
}