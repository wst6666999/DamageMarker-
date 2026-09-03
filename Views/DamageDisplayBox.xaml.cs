using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DamageMaker.DamageDataProcessing;
using DamageMaker.Models;
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

        private bool IsPureFishScaleDamage(float[] point, int index)
        {
            // 单个伤损
            if (point.Length <= 6 || point[6] < 100000)
            {
                return point[4] == 36 || point[4] == 23;
            }

            // 合并伤损
            if (_mergedDamageMap.TryGetValue(index, out var mergedIds))
            {
                return mergedIds.All(id => id == 36 || id == 23);
            }

            return point[4] == 36 || point[4] == 23;
        }

        private void UpdateDamageMarks()
        {
            OverlayCanvas.Children.Clear();
            _labelPositions.Clear();
            _mergedDamageMap.Clear();

            if (DamagePoints == null || SourceImage == null)
                return;

            // 预处理：合并重叠的红色伤损框
            List<float[]> mergedPoints = MergeOverlappingDamagePoints(DamagePoints);

            for (int i = 0; i < mergedPoints.Count; i++)
            {
                var point = mergedPoints[i];

                // 检查是否是纯鱼鳞伤
                bool isPureFishScale = IsPureFishScaleDamage(point, i);
                if (isPureFishScale && HideFishScale)
                {
                    continue;
                }

                float x = point[0], y = point[1];
                float width = point[2], height = point[3];

                // 修复：确保相似度在有效范围内
                float similarity = Math.Max(0, Math.Min(1, point[5])); // 限制在0-1之间

                // 修复：安全计算圆角半径
                double cornerRadiusX = CalculateSafeCornerRadius(width, similarity);
                double cornerRadiusY = CalculateSafeCornerRadius(height, similarity);

                if (Settings.Default.IsHideNormalMarker)
                {
                    var normalCategoryIds = new HashSet<float> { 0, 1, 2, 3, 4, 11, 12, 22, 24, 28 };
                    if (normalCategoryIds.Contains(point[4]))
                    {
                        continue;
                    }
                }

                // 创建标记框
                var border = new Border
                {
                    Width = width,
                    Height = height,
                    BorderThickness = new Thickness(4),
                    BorderBrush = GetDamageBrush(point[4], similarity),
                    CornerRadius = new CornerRadius(
                        Math.Max(0, cornerRadiusX),  // 确保非负
                        Math.Max(0, cornerRadiusX),
                        Math.Max(0, cornerRadiusY),
                        Math.Max(0, cornerRadiusY)),
                    Background = Brushes.Transparent
                };
                Canvas.SetLeft(border, x);
                Canvas.SetTop(border, y);
                OverlayCanvas.Children.Add(border);

                AddLabelWithAntiOverlap(i, point, x, y, width, height);
            }
        }

        /// <summary>
        /// 安全计算圆角半径，防止负值和过大值
        /// </summary>
        private double CalculateSafeCornerRadius(float dimension, float similarity)
        {
            // 确保相似度在0-1范围内
            float safeSimilarity = Math.Max(0, Math.Min(1, similarity));

            // 确保维度有效
            if (dimension <= 0) return 0;

            // 计算圆角半径，确保非负
            double radius = dimension / 2 * (1 - safeSimilarity);

            // 限制圆角半径不超过维度的一半
            return Math.Max(0, Math.Min(radius, dimension / 2));
        }
        public enum MergeStrategy
        {
            Union,      // 取所有框的并集（最大范围）
            Intersect,  // 取所有框的交集（共同区域）
            Average,    // 取所有框的平均位置和大小
            Largest,    // 取最大的那个框
            Smallest    // 取最小的那个框
        }

        private List<float[]> MergeOverlappingDamagePoints(float[][] points, MergeStrategy strategy = MergeStrategy.Union)
        {
            List<float[]> result = new List<float[]>();
            if (points == null || points.Length == 0) return result;

            // 使用数组代替List<bool>提高性能
            bool[] merged = new bool[points.Length];

            // 预缓存伤损ID对应的颜色，优化查找性能
            var redDamageCache = new Dictionary<int, bool>();
            foreach (var category in Records.DamageCategoryData)
            {
                redDamageCache[category.Id] = category.CategoryColor == Brushes.Red;
            }

            for (int i = 0; i < points.Length; i++)
            {
                if (merged[i]) continue;

                float[] current = EnsurePointLength(points[i], 7);
                Rect currentRect = CreateRect(current);

                List<Rect> rectsToMerge = new List<Rect> { currentRect };
                HashSet<int> mergedDamageIds = new HashSet<int> { (int)current[4] };

                for (int j = i + 1; j < points.Length; j++)
                {
                    if (merged[j]) continue;

                    float[] other = EnsurePointLength(points[j], 7);
                    Rect otherRect = CreateRect(other);

                    if (IsMergeable(redDamageCache, current, other, currentRect, otherRect))
                    {
                        rectsToMerge.Add(otherRect);
                        mergedDamageIds.Add((int)other[4]);
                        merged[j] = true;
                    }
                }

                Rect finalRect = CalculateMergedRect(rectsToMerge, strategy);
                float[] mergedPoint = CreateMergedPoint(current, finalRect, mergedDamageIds);

                if (mergedDamageIds.Count > 1)
                {
                    mergedPoint[6] = 100000 + mergedDamageIds.Count;
                    _mergedDamageMap[result.Count] = mergedDamageIds.OrderBy(id => id).ToList();
                }

                result.Add(mergedPoint);
            }

            return result;
        }

        // 辅助方法
        private float[] EnsurePointLength(float[] point, int length)
        {
            if (point.Length >= length) return point;

            float[] newPoint = new float[length];
            Array.Copy(point, newPoint, point.Length);
            newPoint[6] = -1; // 设置默认值
            return newPoint;
        }

        private Rect CreateRect(float[] point)
        {
            return new Rect(point[0], point[1], point[2], point[3]);
        }

        private bool IsMergeable(Dictionary<int, bool> redDamageCache, float[] current, float[] other, Rect currentRect, Rect otherRect)
        {
            bool isCurrentRed = redDamageCache.TryGetValue((int)current[4], out bool currentRed) && currentRed;
            bool isOtherRed = redDamageCache.TryGetValue((int)other[4], out bool otherRed) && otherRed;

            return isCurrentRed && isOtherRed && currentRect.IntersectsWith(otherRect);
        }

        private float[] CreateMergedPoint(float[] original, Rect finalRect, HashSet<int> mergedDamageIds)
        {
            float[] mergedPoint = new float[7];
            Array.Copy(original, mergedPoint, Math.Min(original.Length, 7));

            // 设置合并后的主ID（优先使用非鱼鳞伤ID）
            int mainId = mergedDamageIds.FirstOrDefault(id => !HideFishScale || (id != 36 && id != 23));
            mergedPoint[4] = mainId != 0 ? mainId : mergedDamageIds.First();

            mergedPoint[0] = (float)finalRect.X;
            mergedPoint[1] = (float)finalRect.Y;
            mergedPoint[2] = (float)finalRect.Width;
            mergedPoint[3] = (float)finalRect.Height;

            return mergedPoint;
        }

        /// <summary>
        /// 根据合并策略计算合并后的矩形
        /// </summary>
        /// <param name="rects"></param>
        /// <param name="strategy"></param>
        /// <returns></returns>
        private Rect CalculateMergedRect(List<Rect> rects, MergeStrategy strategy)
        {
            if (rects.Count == 1) return rects[0];

            switch (strategy)
            {
                case MergeStrategy.Union:
                    double left = rects.Min(r => r.Left);
                    double top = rects.Min(r => r.Top);
                    double right = rects.Max(r => r.Right);
                    double bottom = rects.Max(r => r.Bottom);
                    return new Rect(left, top, right - left, bottom - top);

                case MergeStrategy.Intersect:
                    double intersectLeft = rects.Max(r => r.Left);
                    double intersectTop = rects.Max(r => r.Top);
                    double intersectRight = rects.Min(r => r.Right);
                    double intersectBottom = rects.Min(r => r.Bottom);
                    if (intersectLeft > intersectRight || intersectTop > intersectBottom)
                        return rects[0]; // 无交集时返回第一个矩形
                    return new Rect(intersectLeft, intersectTop,
                                  intersectRight - intersectLeft,
                                  intersectBottom - intersectTop);

                case MergeStrategy.Average:
                    double avgX = rects.Average(r => r.X);
                    double avgY = rects.Average(r => r.Y);
                    double avgWidth = rects.Average(r => r.Width);
                    double avgHeight = rects.Average(r => r.Height);
                    return new Rect(avgX, avgY, avgWidth, avgHeight);

                case MergeStrategy.Largest:
                    var largest = rects.OrderByDescending(r => r.Width * r.Height).First();
                    return largest;

                case MergeStrategy.Smallest:
                    var smallest = rects.OrderBy(r => r.Width * r.Height).First();
                    return smallest;

                default:
                    return rects[0];
            }
        }
        // 辅助字典存储合并ID（临时方案）
        private Dictionary<int, List<int>> _mergedDamageMap = new Dictionary<int, List<int>>();

        /// <summary>
        /// 添加标签并避免与其他标签重叠
        /// </summary>
        // 修改标签添加方法
        private void AddLabelWithAntiOverlap(int index, float[] point, float x, float y, float width, float height)
        {
            string damageNames;

            // 处理合并伤损
            if (point.Length > 6 && point[6] >= 100000 && _mergedDamageMap.TryGetValue(index, out var mergedIds))
            {
                // 过滤鱼鳞伤文字（如果开启隐藏）
                var filteredIds = HideFishScale
                    ? mergedIds.Where(id => id != 36 && id != 23).ToList()
                    : mergedIds;

                if (filteredIds.Count == 0) return; // 没有可显示的内容

                // 修正：使用filteredIds中的每个id来获取名称
                damageNames = string.Join(", ", filteredIds.Select(id => DataConversion.DamageIdToDamageName(id)));
            }
            // 处理单个伤损
            else
            {
                if (HideFishScale && (point[4] == 36 || point[4] == 23))
                    return;

                damageNames = DataConversion.DamageIdToDamageName((int)point[4]);
            }
            // 剩余标签渲染逻辑保持不变...
            var textBlock = new TextBlock
            {
                Text = $"{damageNames} [{index}]",
                FontFamily = new FontFamily("微软雅黑"),
                FontSize = 12,
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.FromArgb(150, 0, 0, 0)),
                Padding = new Thickness(3),
                TextWrapping = TextWrapping.Wrap  // 允许文本换行
            };

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
        /// 根据伤损ID和相似度获取对应的画刷
        /// </summary>
        /// <param name="damageId"></param>
        /// <param name="similarity"></param>
        /// <returns></returns>
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