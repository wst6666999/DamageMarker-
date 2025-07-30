using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DamageMaker.DamageDataProcessing;
using DamageMaker.Properties;

namespace DamageMaker.Views
{
    /// <summary>
    /// DamageDisplayBox.xaml 的交互逻辑
    /// </summary>
    public partial class DamageDisplayBox : UserControl
    {
        // 用于跟踪已放置的标签位置，避免重叠
        private List<Rect> _labelPositions = new List<Rect>();

        public DamageDisplayBox()
        {
            InitializeComponent();
            this.SizeChanged += OnSizeChanged;

            // 初始化变换组
            var transformGroup = new TransformGroup();
            transformGroup.Children.Add(new ScaleTransform());
            transformGroup.Children.Add(new TranslateTransform());
            this.RenderTransform = transformGroup;
            this.RenderTransformOrigin = new Point(0.5, 0.5);
        }

        public static readonly DependencyProperty SourceImageProperty =
            DependencyProperty.Register("SourceImage", typeof(ImageSource), typeof(DamageDisplayBox),
                new PropertyMetadata(null, OnSourceImageChanged));

        public static readonly DependencyProperty DamagePointsProperty =
            DependencyProperty.Register("DamagePoints", typeof(float[][]), typeof(DamageDisplayBox),
                new PropertyMetadata(null, OnDamagePointsChanged));

        public static readonly DependencyProperty ShowGuidelinesProperty =
            DependencyProperty.Register("ShowGuidelines", typeof(bool), typeof(DamageDisplayBox),
                new PropertyMetadata(false, OnGuidelinesChanged));

        public static readonly DependencyProperty LeftGuidePositionProperty =
            DependencyProperty.Register("LeftGuidePosition", typeof(double), typeof(DamageDisplayBox),
                new PropertyMetadata(0.0, OnGuidelinesChanged));

        public static readonly DependencyProperty RightGuidePositionProperty =
            DependencyProperty.Register("RightGuidePosition", typeof(double), typeof(DamageDisplayBox),
                new PropertyMetadata(0.0, OnGuidelinesChanged));

        // 修改HideFishScale的DependencyProperty定义
        public static readonly DependencyProperty HideFishScaleProperty =
            DependencyProperty.Register(
                "HideFishScale",
                typeof(bool),
                typeof(DamageDisplayBox),
                new FrameworkPropertyMetadata(
                    false,
                    FrameworkPropertyMetadataOptions.AffectsRender,
                        OnDamagePointsChanged));

        public ImageSource SourceImage
        {
            get => (ImageSource)GetValue(SourceImageProperty);
            set => SetValue(SourceImageProperty, value);
        }

        public float[][] DamagePoints
        {
            get => (float[][])GetValue(DamagePointsProperty);
            set => SetValue(DamagePointsProperty, value);
        }

        public bool ShowGuidelines
        {
            get => (bool)GetValue(ShowGuidelinesProperty);
            set => SetValue(ShowGuidelinesProperty, value);
        }

        public double LeftGuidePosition
        {
            get => (double)GetValue(LeftGuidePositionProperty);
            set => SetValue(LeftGuidePositionProperty, value);
        }

        public double RightGuidePosition
        {
            get => (double)GetValue(RightGuidePositionProperty);
            set => SetValue(RightGuidePositionProperty, value);
        }

        public bool HideFishScale
        {
            get => (bool)GetValue(HideFishScaleProperty);
            set => SetValue(HideFishScaleProperty, value);
        }

        private static void OnSourceImageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (DamageDisplayBox)d;
            control.BaseImage.Source = control.SourceImage;
        }

        private static void OnDamagePointsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (DamageDisplayBox)d;
            control.UpdateDamageMarks();
        }

        private static void OnGuidelinesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (DamageDisplayBox)d;
            control.UpdateGuidelines();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateGuidelines();
            UpdateDamageMarks();
        }

        private void UpdateGuidelines()
        {
            if (ShowGuidelines)
            {
                LeftGuideLine.Visibility = Visibility.Visible;
                RightGuideLine.Visibility = Visibility.Visible;

                LeftGuideLine.X1 = LeftGuideLine.X2 = LeftGuidePosition;
                LeftGuideLine.Y1 = 0;
                LeftGuideLine.Y2 = ActualHeight;

                RightGuideLine.X1 = RightGuideLine.X2 = RightGuidePosition;
                RightGuideLine.Y1 = 0;
                RightGuideLine.Y2 = ActualHeight;
            }
            else
            {
                LeftGuideLine.Visibility = Visibility.Collapsed;
                RightGuideLine.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateDamageMarks()
        {
            OverlayCanvas.Children.Clear();
            _labelPositions.Clear(); // 清空标签位置记录

            if (DamagePoints == null || SourceImage == null)
                return;

            for (int i = 0; i < DamagePoints.Length; i++)
            {
                var point = DamagePoints[i];

                // 跳过鱼鳞伤条件
                if ((point[4] == 36 || point[4] == 23) && HideFishScale)
                {
                    Console.WriteLine($"当前鱼鳞伤ID: {point[4]}, HideFishScale状态: {HideFishScale}");
                    continue;
                }

                float x = point[0], y = point[1];
                float width = point[2], height = point[3];
                float similarity = point[5] < 0.5f ? 0.5f : point[5];

                // 计算圆角半径
                double cornerRadiusX = width / 2 * (1 - similarity);
                double cornerRadiusY = height / 2 * (1 - similarity);

                // 创建标记框
                var border = new Border
                {
                    Width = width,
                    Height = height,
                    BorderThickness = new Thickness(4),
                    BorderBrush = GetDamageBrush(point[4], similarity),
                    CornerRadius = new CornerRadius(
                        cornerRadiusX,  // topLeft
                        cornerRadiusX,  // topRight
                        cornerRadiusY,  // bottomRight
                        cornerRadiusY), // bottomLeft
                    Background = Brushes.Transparent
                };

                Canvas.SetLeft(border, x);
                Canvas.SetTop(border, y);
                OverlayCanvas.Children.Add(border);

                // 添加标签（使用优化的位置计算）
                AddLabelWithAntiOverlap(i, point, x, y, width, height);
            }
        }

        /// <summary>
        /// 添加标签并避免与其他标签重叠
        /// </summary>
        private void AddLabelWithAntiOverlap(int index, float[] point, float x, float y, float width, float height)
        {
            var textBlock = new TextBlock
            {
                Text = $"{DataConversion.DamageIdToDamageName(point[4])} {index}",
                FontFamily = new FontFamily("微软雅黑"),
                FontSize = 12,
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.FromArgb(150, 0, 0, 0)),
                Padding = new Thickness(3)
            };//将伤损ID转换为对应的名称显示出来

            // 强制布局计算
            OverlayCanvas.Children.Add(textBlock);
            textBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            textBlock.Arrange(new Rect(0, 0, textBlock.DesiredSize.Width, textBlock.DesiredSize.Height));
            double textWidth = textBlock.ActualWidth;
            double textHeight = textBlock.ActualHeight;
            OverlayCanvas.Children.Remove(textBlock);

            // 生成可能的标签位置
            var possiblePositions = GenerateLabelPositions(x, y, width, height, textWidth, textHeight);

            // 检查每个候选位置
            foreach (var pos in possiblePositions)
            {
                Rect newRect = new Rect(pos.X, pos.Y, textWidth, textHeight);

                if (IsPositionValid(newRect))
                {
                    Canvas.SetLeft(textBlock, pos.X);
                    Canvas.SetTop(textBlock, pos.Y);
                    OverlayCanvas.Children.Add(textBlock);
                    _labelPositions.Add(newRect);
                    return;
                }
            }

            // 保底方案：使用默认位置并记录重叠
            double defaultX = Math.Max(0, Math.Min(x - 30, OverlayCanvas.ActualWidth - textWidth));
            double defaultY = Math.Max(0, Math.Min(y - 30, OverlayCanvas.ActualHeight - textHeight));
            Canvas.SetLeft(textBlock, defaultX);
            Canvas.SetTop(textBlock, defaultY);
            OverlayCanvas.Children.Add(textBlock);
            _labelPositions.Add(new Rect(defaultX, defaultY, textWidth, textHeight));
        }

        private List<Point> GenerateLabelPositions(float x, float y, float width, float height, double textWidth, double textHeight)
        {
            var positions = new List<Point>();

            // 1. 优先沿着损伤框边缘放置
            positions.Add(new Point(x + width + 5, y)); // 右侧
            positions.Add(new Point(x - textWidth - 5, y)); // 左侧
            positions.Add(new Point(x, y + height + 5)); // 下方
            positions.Add(new Point(x, y - textHeight - 5)); // 上方

            // 2. 四个角落位置
            positions.Add(new Point(x + width + 5, y + height - textHeight));
            positions.Add(new Point(x - textWidth - 5, y + height - textHeight));
            positions.Add(new Point(x + width - textWidth, y - textHeight - 5));
            positions.Add(new Point(x, y - textHeight - 5));

            // 3. 螺旋向外搜索位置（更智能的备选方案）
            for (int step = 1; step <= 5; step++)
            {
                int stepSize = step * 15;
                positions.Add(new Point(x + width + stepSize, y));
                positions.Add(new Point(x - textWidth - stepSize, y));
                positions.Add(new Point(x, y + height + stepSize));
                positions.Add(new Point(x, y - textHeight - stepSize));
            }

            return positions;
        }

        private bool IsPositionValid(Rect newRect)
        {
            // 检查是否在画布范围内
            if (newRect.Left < 0 || newRect.Top < 0 ||
                newRect.Right > OverlayCanvas.ActualWidth ||
                newRect.Bottom > OverlayCanvas.ActualHeight)
            {
                return false;
            }

            // 检查与现有标签的重叠（带缓冲区域）
            foreach (var existingRect in _labelPositions)
            {
                if (newRect.IntersectsWith(existingRect))
                {
                    // 增加5像素的缓冲区域
                    Rect expandedExisting = new Rect(
                        existingRect.Left - 5,
                        existingRect.Top - 5,
                        existingRect.Width + 10,
                        existingRect.Height + 10);

                    if (newRect.IntersectsWith(expandedExisting))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// 添加标签并智能避免与其他标签重叠
        /// </summary>
        //private void AddLabelWithAntiOverlap(int index, float[] point, float x, float y, float width, float height)
        //{
        //    var textBlock = new TextBlock
        //    {
        //        Text = $"{DataConversion.DamageIdToDamageName(point[4])} {index}",
        //        FontFamily = new FontFamily("微软雅黑"),
        //        FontSize = 12,
        //        Foreground = Brushes.White,
        //        Background = new SolidColorBrush(Color.FromArgb(150, 0, 0, 0)),
        //        Padding = new Thickness(3)
        //    };

        //    // 预计算文本块尺寸（更高效的方式）
        //    textBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        //    textBlock.Arrange(new Rect(0, 0, textBlock.DesiredSize.Width, textBlock.DesiredSize.Height));
        //    double textWidth = textBlock.DesiredSize.Width;
        //    double textHeight = textBlock.DesiredSize.Height;

        //    // 生成候选位置并按优先级排序
        //    var possiblePositions = GenerateLabelPositions(x, y, width, height, textWidth, textHeight)
        //        .OrderBy(p => p.Priority)
        //        .Select(p => p.Point);

        //    // 检查每个候选位置
        //    foreach (var pos in possiblePositions)
        //    {
        //        Rect newRect = new Rect(pos.X, pos.Y, textWidth, textHeight);

        //        if (IsPositionValid(newRect))
        //        {
        //            OverlayCanvas.Children.Add(textBlock);
        //            Canvas.SetLeft(textBlock, pos.X);
        //            Canvas.SetTop(textBlock, pos.Y);
        //            _labelPositions.Add(newRect);
        //            return;
        //        }
        //    }

        //    // 保底方案：使用最佳可能位置（即使有轻微重叠）
        //    var bestPosition = FindLeastOverlappingPosition(x, y, textWidth, textHeight);
        //    OverlayCanvas.Children.Add(textBlock);
        //    Canvas.SetLeft(textBlock, bestPosition.X);
        //    Canvas.SetTop(textBlock, bestPosition.Y);
        //    _labelPositions.Add(new Rect(bestPosition.X, bestPosition.Y, textWidth, textHeight));
        //}

        //private List<(Point Point, int Priority)> GenerateLabelPositions(float x, float y, float width, float height, double textWidth, double textHeight)
        //{
        //    var positions = new List<(Point, int)>();

        //    // 优先级1：紧邻损伤框的最佳位置（最高优先级）
        //    positions.Add((new Point(x + width + 5, y), 1)); // 右侧
        //    positions.Add((new Point(x - textWidth - 5, y), 1)); // 左侧
        //    positions.Add((new Point(x, y + height + 5), 2)); // 下方
        //    positions.Add((new Point(x, y - textHeight - 5), 2)); // 上方

        //    // 优先级2：角落位置
        //    positions.Add((new Point(x + width + 5, y + height - textHeight), 3));
        //    positions.Add((new Point(x - textWidth - 5, y + height - textHeight), 3));
        //    positions.Add((new Point(x + width - textWidth, y - textHeight - 5), 3));
        //    positions.Add((new Point(x, y - textHeight - 5), 3));

        //    // 优先级3：螺旋向外搜索位置（带角度变化）
        //    int steps = 5;
        //    double angleStep = Math.PI / 4; // 45度角变化
        //    double startRadius = 15;

        //    for (int i = 1; i <= steps; i++)
        //    {
        //        double radius = startRadius * i;
        //        for (double angle = 0; angle < 2 * Math.PI; angle += angleStep)
        //        {
        //            double offsetX = radius * Math.Cos(angle);
        //            double offsetY = radius * Math.Sin(angle);
        //            positions.Add((new Point(x + offsetX, y + offsetY), 4 + i));
        //        }
        //    }

        //    return positions;
        //}

        //private bool IsPositionValid(Rect newRect)
        //{
        //    // 快速检查是否在画布范围内
        //    if (newRect.Left < 0 || newRect.Top < 0 ||
        //        newRect.Right > OverlayCanvas.ActualWidth ||
        //        newRect.Bottom > OverlayCanvas.ActualHeight)
        //    {
        //        return false;
        //    }

        //    // 使用空间分区或四叉树优化大型数据集
        //    // 这里简化处理，实际项目中可考虑优化
        //    const double padding = 5;
        //    Rect expandedNewRect = new Rect(
        //        newRect.Left - padding,
        //        newRect.Top - padding,
        //        newRect.Width + 2 * padding,
        //        newRect.Height + 2 * padding);

        //    foreach (var existingRect in _labelPositions)
        //    {
        //        if (expandedNewRect.IntersectsWith(existingRect))
        //        {
        //            return false;
        //        }
        //    }

        //    return true;
        //}

        private Point FindLeastOverlappingPosition(double x, double y, double width, double height)
        {
            // 尝试找到重叠面积最小的位置
            var candidates = new List<Point>
    {
        new Point(Math.Max(0, x - width - 5), Math.Max(0, y - height - 5)),
        new Point(Math.Max(0, x - width - 5), Math.Min(OverlayCanvas.ActualHeight - height, y + height + 5)),
        new Point(Math.Min(OverlayCanvas.ActualWidth - width, x + width + 5), Math.Max(0, y - height - 5)),
        new Point(Math.Min(OverlayCanvas.ActualWidth - width, x + width + 5), Math.Min(OverlayCanvas.ActualHeight - height, y + height + 5))
    };

            // 评估每个候选位置的重叠程度
            var bestPosition = candidates[0];
            double minOverlap = double.MaxValue;

            foreach (var candidate in candidates)
            {
                Rect rect = new Rect(candidate.X, candidate.Y, width, height);
                double overlap = CalculateTotalOverlap(rect);

                if (overlap < minOverlap)
                {
                    minOverlap = overlap;
                    bestPosition = candidate;
                }
            }

            return bestPosition;
        }

        private double CalculateTotalOverlap(Rect rect)
        {
            const double padding = 5;
            Rect expandedRect = new Rect(
                rect.Left - padding,
                rect.Top - padding,
                rect.Width + 2 * padding,
                rect.Height + 2 * padding);

            double totalOverlap = 0;

            foreach (var existingRect in _labelPositions)
            {
                if (expandedRect.IntersectsWith(existingRect))
                {
                    Rect intersection = Rect.Intersect(expandedRect, existingRect);
                    totalOverlap += intersection.Width * intersection.Height;
                }
            }

            return totalOverlap;
        }
        private Brush GetDamageBrush(float damageId, float similarity)
        {
            var baseColor = DataConversion.DamageIdToBrush(damageId).Color;
            return new SolidColorBrush(Color.FromArgb(
                (byte)(255 * similarity),
                baseColor.R,
                baseColor.G,
                baseColor.B));
        }
    }
}