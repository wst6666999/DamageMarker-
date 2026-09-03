using DamageMaker.Common;
using DamageMaker.Models;
using DamageMaker.Properties;
using DamageMarker.ViewModels;
using HandyControl.Controls;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using static DamageMaker.Models.Records;
using MessageBox = HandyControl.Controls.MessageBox;
using FlaUI.Core.WindowsAPI;
using FlaUI.Core.Input;
using DamageMaker.Automation;
using Mouse = FlaUI.Core.Input.Mouse;
using Point = System.Drawing.Point;
using System.Windows.Automation;
using DamageMaker.ImageProcessing;
using DamageMaker.SqliteServer;
using System.Diagnostics;
using System.Windows.Controls;

//添加引用System.Drawing用于截图

namespace DamageMarker.Views
{
    public partial class PlaybackWindow
    {
        private static PlaybackWindow? singlePlaybackWindow;
        public static event Action<object, string>? ScreenshotFinished;
        private int sleepTime =DamageMaker.Properties.Settings.Default.ScreenshotInterval;
        public static event Action<object, EventArgs>? ScreenshotStart;
        private int screenshotOfferset;
        public int MoveRepeatPx;
        public int ImgWidthPx;
        public MoveDirection? PlaybackDirection;
        string PlaybackedAppName;
        int keystrokes = 1;
        VirtualKeyShort RightKeyShort= VirtualKeyShort.RIGHT;
        VirtualKeyShort LeftKeyShort= VirtualKeyShort.LEFT;



        static object _lock = new object();
        [DllImport("user32.dll")]
        static extern int mouse_event(int dwFlags, int dx, int dy, int dwData, int dwExtraInfo);
        
        private const int SpaceKeyEventId = 10380;
        //模拟鼠标滚轮滚动操作，必须配合dwData参数

        const int MOUSEEVENTF_WHEEL = 0x0800;

        const int CtrlShiftLeftKeyEventId = 10343;
        const int CtrlShiftRightKeyEventId = 10362;
        const int CtrlShiftStopKeyEventId = 10370;
        double temporaryLeft;
        double temporaryRight;
        double temporaryTop;
        double temporaryWidth;
        double temporaryHeight;

        // 真正用于 CopyFromScreen 的屏幕绝对范围。
        // 普通软件由红框换算；8C 固定为 (44,103,1874,900)，与可见红框分离。
        private int screenCaptureLeft;
        private int screenCaptureTop;
        private int screenCaptureWidth;
        private int screenCaptureHeight;

        bool PlaybackStopYN;
        bool PlaybackingYN;
        string filePathIn;

        // OCR里程识别裁剪区域，坐标相对于红框截图 temporaryBitmap1
        private int mileageCropX = 0;
        private int mileageCropY = 0;
        private int mileageCropWidth = 0;
        private int mileageCropHeight = 45;
        private bool useDouble502MileageOcr = false;

        // 8C 专用：可见红框保持 709px；独立临时采集范围为 900px。
        private bool isRailTest8C = false;
        private int formalScreenshotHeight = 0;

        // 8C 的 LineType 中文 OCR 区域，坐标相对于固定 1874×900 临时完整截图。
        private int lineTypeCropX = 0;
        private int lineTypeCropY = 0;
        private int lineTypeCropWidth = 0;
        private int lineTypeCropHeight = 0;
        private bool useRailTest8CLineTypeOcr = false;

        Stopwatch stopwatch = new Stopwatch();


        [DllImport("user32.dll")]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        protected override void OnSourceInitialized(EventArgs e)
        {

            base.OnSourceInitialized(e);
            var handle = new WindowInteropHelper(this).Handle;
            var source = HwndSource.FromHwnd(handle);
            source?.AddHook(HwndHook);
            RegisterHotKey(handle, CtrlShiftLeftKeyEventId, (uint)ModifierKeys.Control | (uint)ModifierKeys.Shift, (uint)KeyInterop.VirtualKeyFromKey(Key.A));
            RegisterHotKey(handle, CtrlShiftRightKeyEventId, (uint)ModifierKeys.Control | (uint)ModifierKeys.Shift, (uint)KeyInterop.VirtualKeyFromKey(Key.D));
            RegisterHotKey(handle, CtrlShiftStopKeyEventId, (uint)ModifierKeys.Control | (uint)ModifierKeys.Shift, (uint)KeyInterop.VirtualKeyFromKey(Key.P));
            RegisterHotKey(handle, 10380, 0u, (uint)KeyInterop.VirtualKeyFromKey(Key.Space));

        }
        /// <summary>
        /// 处理快捷键事件
        /// </summary>
        /// <param name="hwnd"></param>
        /// <param name="msg"></param>
        /// <param name="wParam"></param>
        /// <param name="lParam"></param>
        /// <param name="handled"></param>
        /// <returns></returns>
        IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int wmHotkey = 0x0312;
            switch (msg)
            {
                case wmHotkey:
                    switch (wParam.ToInt32())
                    {
                        case CtrlShiftLeftKeyEventId:
                            ToLeftPlayback();
                            break;
                        case CtrlShiftRightKeyEventId:
                            ToRightPlayback();
                            break;
                        case CtrlShiftStopKeyEventId:
                            StopPlayback();
                            break;
                        case SpaceKeyEventId:
                            StopPlayback();
                            break;

                    }
                    break;
            }
            return IntPtr.Zero;
        }
        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            var handle = new WindowInteropHelper(this).Handle;
            //关闭窗口后取消注册
            UnregisterHotKey(handle, CtrlShiftLeftKeyEventId);
            UnregisterHotKey(handle, CtrlShiftRightKeyEventId);
            UnregisterHotKey(handle, CtrlShiftStopKeyEventId);
            singlePlaybackWindow = null;
        }
        static public PlaybackWindow GetInstance(double left, double top, double width, double height)
        {
            return GetInstance(left, top, width, height, null);
        }

        static public PlaybackWindow GetInstance(double left, double top, double width, double height, string? playbackAppName)
        {
            lock (_lock)
            {
                if (singlePlaybackWindow == null)
                {
                    singlePlaybackWindow = new PlaybackWindow(left, top, width, height, playbackAppName);
                    Console.WriteLine($"[回放窗口] 红框 left={left}, top={top}, width={width}, height={height}, appName={playbackAppName}");
                }
                else
                {
                    MessageBox.Warning("当前已经有一个回放窗口");
                }
            }

            return singlePlaybackWindow;

        }


        private PlaybackWindow(double left, double top, double width, double height, string? playbackAppName = null)
        {

            InitializeComponent();

            isRailTest8C = !string.IsNullOrEmpty(playbackAppName)
                && playbackAppName.IndexOf("RailTest8C", StringComparison.OrdinalIgnoreCase) >= 0;

            // 可见红框始终使用传入范围。8C 传入的仍是原来的 1874×709，
            // 900px 临时截图由 screenCapture* 独立控制，不改变红框。
            Canvas.SetLeft(selectionBorder, left);
            Canvas.SetTop(selectionBorder, top);
            selectionBorder.Width = width;
            selectionBorder.Height = height;

            formalScreenshotHeight = (int)Math.Round(height);

            if (isRailTest8C)
            {
                // 8C 临时完整采集范围使用屏幕绝对坐标。
                screenCaptureLeft = 44;
                screenCaptureTop = 103;
                screenCaptureWidth = 1874;
                screenCaptureHeight = 900;
            }

            // 默认蓝框：位于红框底部 45px。8C 和双轨502会在下面覆盖。
            Canvas.SetLeft(MileageBorder, left);
            Canvas.SetTop(MileageBorder, top + Math.Max(0, height - 45));
            MileageBorder.Width = width;
            MileageBorder.Height = 45;

            mileageCropX = 0;
            mileageCropY = (int)Math.Max(0, height - 45);
            mileageCropWidth = (int)width;
            mileageCropHeight = 45;
            useDouble502MileageOcr = false;

            var trackData = DamageMaker.Properties.Settings.Default.TrackData ?? string.Empty;
            bool isV30A = !string.IsNullOrEmpty(playbackAppName)
                && playbackAppName.Contains("回放软件V3.0A");
            bool isDouble502 = trackData.Contains("双轨502");

            Console.WriteLine(
                $"[蓝框判断] AppName={playbackAppName}, TrackData={trackData}, " +
                $"is8C={isRailTest8C}, isV30A={isV30A}, isDouble502={isDouble502}");

            if (isRailTest8C)
            {
                // 8C 里程蓝框固定为屏幕绝对坐标 (44, 767, 1874, 45)。
                const double blueScreenLeft = 44;
                const double blueScreenTop = 767;
                const double blueWidth = 1874;
                const double blueHeight = 45;

                Canvas.SetLeft(MileageBorder, blueScreenLeft);
                Canvas.SetTop(MileageBorder, blueScreenTop);
                MileageBorder.Width = blueWidth;
                MileageBorder.Height = blueHeight;

                // 相对于固定临时截图 (44,103,1874,900)：(0,664,1874,45)。
                mileageCropX = (int)Math.Round(blueScreenLeft - screenCaptureLeft);
                mileageCropY = (int)Math.Round(blueScreenTop - screenCaptureTop);
                mileageCropWidth = (int)blueWidth;
                mileageCropHeight = (int)blueHeight;
                useDouble502MileageOcr = false;

                // LineType 中文 OCR 的屏幕绝对坐标为 (130, 950, 240, 25)。
                // 换算到 8C 临时完整截图内部：当前为 (86, 847, 240, 25)。
                const double lineTypeScreenLeft = 130;
                const double lineTypeScreenTop = 950;
                const double lineTypeWidth = 240;
                const double lineTypeHeight = 25;

                lineTypeCropX = (int)Math.Round(lineTypeScreenLeft - screenCaptureLeft);
                lineTypeCropY = (int)Math.Round(lineTypeScreenTop - screenCaptureTop);
                lineTypeCropWidth = (int)lineTypeWidth;
                lineTypeCropHeight = (int)lineTypeHeight;
                useRailTest8CLineTypeOcr = true;

                Console.WriteLine(
                    $"[8C蓝框] 绝对坐标 x={blueScreenLeft}, y={blueScreenTop}, " +
                    $"w={blueWidth}, h={blueHeight}; 相对截图 x={mileageCropX}, " +
                    $"y={mileageCropY}, w={mileageCropWidth}, h={mileageCropHeight}");
                Console.WriteLine(
                    $"[8C-LineType] 相对完整截图 x={lineTypeCropX}, y={lineTypeCropY}, " +
                    $"w={lineTypeCropWidth}, h={lineTypeCropHeight}");
            }
            else if (isV30A || isDouble502)
            {
                // 双轨502 / V3.0A 保持现有固定蓝框逻辑。
                const double blueScreenLeft = 86;
                const double blueScreenTop = 370;
                const double blueWidth = 1775;
                const double blueHeight = 36;

                Canvas.SetLeft(MileageBorder, blueScreenLeft);
                Canvas.SetTop(MileageBorder, blueScreenTop);
                MileageBorder.Width = blueWidth;
                MileageBorder.Height = blueHeight;

                mileageCropX = (int)Math.Round(blueScreenLeft - left);
                mileageCropY = (int)Math.Round(blueScreenTop - top);
                mileageCropWidth = (int)blueWidth;
                mileageCropHeight = (int)blueHeight;
                useDouble502MileageOcr = true;

                Console.WriteLine(
                    $"[蓝框/OCR判断] 命中，蓝框绝对坐标 left={blueScreenLeft}, " +
                    $"top={blueScreenTop}, width={blueWidth}, height={blueHeight}");
                Console.WriteLine(
                    $"[蓝框/OCR判断] OCR相对红框 x={mileageCropX}, y={mileageCropY}, " +
                    $"w={mileageCropWidth}, h={mileageCropHeight}");
            }

            // 提示文字放在红框下方，保持提示可见。
            var tipText = this.FindName("TipText") as System.Windows.Controls.TextBlock;
            if (tipText != null)
            {
                Canvas.SetLeft(tipText, left + Math.Max(0, (width - 650) / 2));
                Canvas.SetTop(tipText, top + height + 10);
            }

            PlaybackStopYN = true;
            PlaybackingYN = false;
            screenshotOfferset = DamageMaker.Properties.Settings.Default.ScreenshotOffset;
            Scal.ScaleX = 1 / DamageMaker.Common.Monitor.ScaleX;
            Scal.ScaleY = 1 / DamageMaker.Common.Monitor.ScaleY;
            if (Scal.ScaleX != 1 || Scal.ScaleY != 1)
            {
                MessageBox.Error("请将窗口放置在主屏幕上，并将缩放调整到100%，以便准确截图");
            }
        }

        /// <summary>
        /// 校正8C红框显示位置。
        /// 只改变红框显示，不改变实际截图区域。
        /// </summary>
        private void Sync8CVisibleRedFrameToCaptureArea()
        {
            if (!isRailTest8C)
            {
                return;
            }

            // 红框只显示正式保存区域1874×709，
            // 不包含下面用于LineType OCR的191像素。
            int visibleWidthPx = screenCaptureWidth;
            int visibleHeightPx = formalScreenshotHeight;

            // 实际截图区域左上角、右下角，属于屏幕像素坐标。
            var screenTopLeft = new System.Windows.Point(
                screenCaptureLeft,
                screenCaptureTop);

            var screenBottomRight = new System.Windows.Point(
                screenCaptureLeft + visibleWidthPx,
                screenCaptureTop + visibleHeightPx);

            // 将屏幕像素坐标换算为RootCanvas内部坐标。
            // 可以自动处理DPI缩放和最大化窗口位置偏移。
            System.Windows.Point canvasTopLeft =
                RootCanvas.PointFromScreen(screenTopLeft);

            System.Windows.Point canvasBottomRight =
                RootCanvas.PointFromScreen(screenBottomRight);

            double displayWidth = Math.Max(
                1,
                canvasBottomRight.X - canvasTopLeft.X);

            double displayHeight = Math.Max(
                1,
                canvasBottomRight.Y - canvasTopLeft.Y);

            selectionBorder.Margin = new Thickness(0);

            Canvas.SetLeft(selectionBorder, canvasTopLeft.X);
            Canvas.SetTop(selectionBorder, canvasTopLeft.Y);

            selectionBorder.Width = displayWidth;
            selectionBorder.Height = displayHeight;

            // 让红框显示在蓝框等控件上层。
            Panel.SetZIndex(selectionBorder, 20);

            Console.WriteLine(
                $"[8C红框显示校正] 屏幕范围=" +
                $"({screenCaptureLeft},{screenCaptureTop}," +
                $"{visibleWidthPx},{visibleHeightPx})，" +
                $"Canvas范围=" +
                $"({canvasTopLeft.X:F2},{canvasTopLeft.Y:F2}," +
                $"{displayWidth:F2},{displayHeight:F2})");
        }

        /// <summary>
        /// 校正8C蓝色里程框的显示位置。
        /// 只改变蓝框显示，不改变实际OCR截图区域。
        /// </summary>
        private void Sync8CVisibleMileageFrameToCaptureArea()
        {
            if (!isRailTest8C)
            {
                return;
            }

            // 蓝框实际屏幕绝对坐标
            const double blueScreenLeft = 44;
            const double blueScreenTop = 767;
            const double blueScreenWidth = 1874;
            const double blueScreenHeight = 45;

            var screenTopLeft = new System.Windows.Point(
                blueScreenLeft,
                blueScreenTop);

            var screenBottomRight = new System.Windows.Point(
                blueScreenLeft + blueScreenWidth,
                blueScreenTop + blueScreenHeight);

            // 屏幕坐标转换成RootCanvas坐标
            System.Windows.Point canvasTopLeft =
                RootCanvas.PointFromScreen(screenTopLeft);

            System.Windows.Point canvasBottomRight =
                RootCanvas.PointFromScreen(screenBottomRight);

            double displayWidth = Math.Max(
                1,
                canvasBottomRight.X - canvasTopLeft.X);

            double displayHeight = Math.Max(
                1,
                canvasBottomRight.Y - canvasTopLeft.Y);

            MileageBorder.Margin = new Thickness(0);

            Canvas.SetLeft(MileageBorder, canvasTopLeft.X);
            Canvas.SetTop(MileageBorder, canvasTopLeft.Y);

            MileageBorder.Width = displayWidth;
            MileageBorder.Height = displayHeight;

            // 蓝框放在红框上层或同层
            Panel.SetZIndex(MileageBorder, 21);

            Console.WriteLine(
                $"[8C蓝框显示校正] 屏幕范围=" +
                $"({blueScreenLeft},{blueScreenTop}," +
                $"{blueScreenWidth},{blueScreenHeight})，" +
                $"Canvas范围=" +
                $"({canvasTopLeft.X:F2},{canvasTopLeft.Y:F2}," +
                $"{displayWidth:F2},{displayHeight:F2})");
        }

        void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // temporaryWidth/Height 始终代表正式红框图片尺寸。
            temporaryWidth = selectionBorder.Width;
            temporaryHeight = selectionBorder.Height;

            if (isRailTest8C)
            {
                // 只校正红框显示，不修改实际截图坐标。
                Sync8CVisibleRedFrameToCaptureArea();
                // 校正蓝框显示
                Sync8CVisibleMileageFrameToCaptureArea();
                // 8C 截图范围已经在构造函数中按绝对屏幕坐标固定，
                // 这里不能再经过 PointToScreen，否则截图起点会随窗口位置二次偏移。
                temporaryLeft = screenCaptureLeft;
                temporaryTop = screenCaptureTop;
                temporaryRight = screenCaptureLeft + screenCaptureWidth;

                Console.WriteLine(
                    $"[8C截图范围] 固定绝对坐标 x={screenCaptureLeft}, " +
                    $"y={screenCaptureTop}, w={screenCaptureWidth}, h={screenCaptureHeight}");
                return;
            }

            double selectionLeft = Canvas.GetLeft(selectionBorder);
            double selectionTop = Canvas.GetTop(selectionBorder);

            temporaryLeft = this.PointToScreen(new System.Windows.Point((int)selectionLeft, (int)selectionTop)).X;
            temporaryRight = this.PointToScreen(new System.Windows.Point((int)(selectionLeft + temporaryWidth), (int)selectionTop)).X;
            temporaryTop = this.PointToScreen(new System.Windows.Point((int)selectionLeft, (int)selectionTop)).Y;

            screenCaptureLeft = (int)Math.Round(temporaryLeft);
            screenCaptureTop = (int)Math.Round(temporaryTop);
            screenCaptureWidth = (int)Math.Round(temporaryWidth);
            screenCaptureHeight = (int)Math.Round(temporaryHeight);
        }

        Bitmap CaptureCurrentScreen()
        {
            // 8C 为固定 1874×900 临时图；其他软件等于可见红框范围。
            var bmpScreen = new Bitmap(screenCaptureWidth, screenCaptureHeight);
            //使用位图对象来创建Graphics的对象
            using (Graphics g = Graphics.FromImage(bmpScreen))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;//设置平滑模式，抗锯齿
                g.CompositingQuality = CompositingQuality.HighQuality;//设置合成质量
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;//设置插值模式
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;//设置文本呈现的质量
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;//设置呈现期间，像素偏移的方式
                g.CopyFromScreen(screenCaptureLeft, screenCaptureTop, 0, 0, bmpScreen.Size);//利用CopyFromScreen将当前屏幕截图并将内容存储在bmpScreen的位图中
            }
            return bmpScreen;
        }

        /// <summary>
        /// 8C 从 1874×900 的临时完整截图中，只截取顶部原始 1874×709 作为正式图片。
        /// 其他软件不会调用此方法。
        /// </summary>
        private Bitmap CropFormalScreenshot(Bitmap fullBitmap)
        {
            int safeWidth = Math.Min((int)temporaryWidth, fullBitmap.Width);
            int safeHeight = Math.Min(formalScreenshotHeight, fullBitmap.Height);

            if (safeWidth <= 0 || safeHeight <= 0)
            {
                throw new InvalidOperationException(
                    $"正式截图区域无效: width={safeWidth}, height={safeHeight}, " +
                    $"full={fullBitmap.Width}x{fullBitmap.Height}");
            }

            return fullBitmap.Clone(
                new Rectangle(0, 0, safeWidth, safeHeight),
                fullBitmap.PixelFormat);
        }
        
        public void ToLeftPlayback()
        {
            Growl.InfoGlobal("开始向左截图");
            selectionBorder.BorderBrush = System.Windows.Media.Brushes.Transparent;
            //  MileageBorder.BorderBrush = System.Windows.Media.Brushes.Transparent;
            MileageBorder.Stroke = System.Windows.Media.Brushes.Transparent;
            CloseButton.Visibility = Visibility.Hidden;
            Playback(-1, screenshotOfferset);
        }
        public void ToRightPlayback()
        {
            Growl.InfoGlobal("开始向右截图");
            selectionBorder.BorderBrush = System.Windows.Media.Brushes.Transparent;
            //  MileageBorder.BorderBrush = System.Windows.Media.Brushes.Transparent;
            CloseButton.Visibility = Visibility.Hidden;
            MileageBorder.Stroke = System.Windows.Media.Brushes.Transparent;
            Playback(1, screenshotOfferset);
        }
        public void StopPlayback()
        {
            Growl.InfoGlobal("停止截图");
            selectionBorder.BorderBrush = System.Windows.Media.Brushes.Red;
            //  MileageBorder.BorderBrush = System.Windows.Media.Brushes.Blue;
            MileageBorder.Stroke = System.Windows.Media.Brushes.Blue;
            PlaybackStopYN = false;
            this.Close();
        }

        #region 截图相关


        List<OcrData> OcrDataList = new List<OcrData>();

        async void Playback(int direction, int ScreenshotOffset)
        {
            PlaybackDirection = direction > 0 ? MoveDirection.Right : MoveDirection.Left;
            ImgWidthPx = (int)temporaryWidth;
            stopwatch.Restart();
            ScreenshotStart?.Invoke(this, EventArgs.Empty);

            if (PlaybackingYN)
                return;

            PlaybackingYN = true;

            Bitmap? temporaryBitmap1 = null; // 当前正式截图；8C 为 1874×709。
            Bitmap? temporaryBitmap2 = null; // 上一张正式截图，用于比较和偏移计算。
            int temporarySameNum = 0;
            filePathIn = MainWindowViewModel.FilePathIn;
            int count = 1;

            PlaybackedAppName = AppInfo.GetFocusedApplicationName();
            await Task.Delay(1000);

            if (PlaybackedAppName.Contains("JGT-6M"))
            {
                keystrokes = 2;
            }
            else if (PlaybackedAppName.Contains("800A"))
            {
                RightKeyShort = VirtualKeyShort.OEM_PERIOD;
                LeftKeyShort = VirtualKeyShort.OEM_COMMA;
            }
            else if (PlaybackedAppName.Contains("gt_20_replayer"))
            {
                Mouse.MoveTo(new Point(400, 400));
                Mouse.LeftClick();
                await Task.Delay(100);
                Mouse.MoveTo(new Point(1800, 1000));
            }

            try
            {
                // 持续截图，直到连续出现 6 次相同的正式截图。
                while (PlaybackStopYN)
                {
                    if (temporaryBitmap1 != null)
                    {
                        temporaryBitmap2?.Dispose();
                        temporaryBitmap2 = DeepCloneEx(temporaryBitmap1);
                    }

                    Bitmap? fullCaptureBitmap = null;

                    // 先截取临时完整范围。8C 得到 1874×900 临时图，再裁顶部 709。
                    this.Dispatcher.Invoke(DispatcherPriority.Normal, (ThreadStart)delegate ()
                    {
                        Growl.ClearGlobal();

                        Bitmap capturedBitmap = CaptureCurrentScreen();

                        temporaryBitmap1?.Dispose();
                        if (isRailTest8C)
                        {
                            fullCaptureBitmap = capturedBitmap;
                            temporaryBitmap1 = CropFormalScreenshot(capturedBitmap);
                        }
                        else
                        {
                            temporaryBitmap1 = capturedBitmap;
                        }
                    });

                    if (temporaryBitmap1 == null)
                    {
                        fullCaptureBitmap?.Dispose();
                        break;
                    }

                    bool isSameFrame = temporaryBitmap2 != null
                        && ImageCompareString(temporaryBitmap2, temporaryBitmap1);

                    if (!isSameFrame)
                    {
                        if (temporaryBitmap2 != null && count == 1)
                        {
                            int movePx = 0;
                            bool railLikeApp = PlaybackedAppName.Contains("RailTest")
                                || PlaybackedAppName.Contains("CTKJ")
                                || PlaybackedAppName.Contains("大仪器");

                            if (direction < 0)
                            {
                                movePx = ImgProcessing.GetImgOffset(
                                    temporaryBitmap2,
                                    temporaryBitmap1,
                                    railLikeApp);
                            }
                            else if (direction > 0)
                            {
                                movePx = ImgProcessing.GetImgOffset(
                                    temporaryBitmap2,
                                    temporaryBitmap1,
                                    !railLikeApp);
                            }

                            MoveRepeatPx = temporaryBitmap2.Width - movePx;
                            Console.WriteLine($"图片偏移量:{movePx}");
                        }

                        // 原里程 OCR 始终针对正式截图。8C 的固定蓝框仍位于顶部 709 内。
                        string mileageData = await MileageRecognition.CropAndRecognizeMileage(
                            temporaryBitmap1,
                            mileageCropX,
                            mileageCropY,
                            mileageCropWidth,
                            mileageCropHeight,
                            useDouble502MileageOcr);

                        string? mileage = mileageData.ExtractDistance(PlaybackedAppName);
                        string? speedValue = mileageData.ExtractVelocity(PlaybackedAppName);

                        // 只有 8C 使用 1874×900 临时完整截图识别 LineType。
                        string lineType = string.Empty;
                        if (useRailTest8CLineTypeOcr && fullCaptureBitmap != null)
                        {
                            lineType = await MileageRecognition.CropAndRecognizeLineType(
                                fullCaptureBitmap,
                                lineTypeCropX,
                                lineTypeCropY,
                                lineTypeCropWidth,
                                lineTypeCropHeight);
                        }

                        string imgFullPath = string.IsNullOrEmpty(mileage)
                            ? Path.Combine(filePathIn, $"{count++}.png")
                            : Path.Combine(filePathIn, $"{mileage}_{count++}.png");

                        temporarySameNum = 0;
                        Console.WriteLine(
                            $"[{DateTime.Now:HH:mm:ss.fff}] 截图 #{count}: {imgFullPath}, " +
                            $"LineType={lineType}");

                        // 只保存正式截图。8C 在这里保存的是原来的 1874×709。
                        temporaryBitmap1.Save(
                            imgFullPath,
                            System.Drawing.Imaging.ImageFormat.Png);

                        OcrDataList.Add(new OcrData(
                            imgFullPath,
                            mileageData,
                            speedValue ?? string.Empty,
                            lineType));
                    }
                    else
                    {
                        temporarySameNum++;

                        if (temporarySameNum >= 6)
                        {
                            Growl.InfoGlobal("相同");
                            PlaybackStopYN = false;
                        }
                        else
                        {
                            Console.WriteLine(
                                $"请注意,当前截图出现重复,图片序列号为{count}." +
                                "请适当降低截图移动像素");
                            fullCaptureBitmap?.Dispose();
                            continue;
                        }
                    }

                    fullCaptureBitmap?.Dispose();

                    Mouse.MovePixelsPerMillisecond = Settings.Default.MouseMovePixel;
                    if (direction < 0)
                    {
                        for (int i = 0; i < keystrokes; i++)
                        {
                            FlaUI.Core.Input.Keyboard.Pressing(LeftKeyShort);
                            await Task.Delay(sleepTime);
                            FlaUI.Core.Input.Keyboard.Release(LeftKeyShort);
                        }
                    }
                    else if (direction > 0)
                    {
                        for (int i = 0; i < keystrokes; i++)
                        {
                            FlaUI.Core.Input.Keyboard.Pressing(RightKeyShort);
                            await Task.Delay(sleepTime);
                            FlaUI.Core.Input.Keyboard.Release(RightKeyShort);
                        }
                    }

                    await Task.Delay(sleepTime);
                }
            }
            finally
            {
                temporaryBitmap1?.Dispose();
                temporaryBitmap2?.Dispose();
            }

            base.Close();
            PlaybackingYN = false;

            if (OcrDataList.Count > 0)
            {
                string jsonString = JsonSerializer.Serialize(
                    OcrDataList,
                    new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(Path.Combine(filePathIn, "OcrResult.json"), jsonString);

                // 截图结束阶段只保存图片和 OcrResult.json，不访问 SQLite。
                // LineType 会在 MainWindowViewModel 正式插入 Images 时，
                // 与 ImageData、Mileage 一起在后台事务中写入。
            }
            else
            {
                MessageBox.Info("未识别到到有效里程数据");
            }

            stopwatch.Stop();
            string elapsedTimeForScreenshot =
                $"{stopwatch.Elapsed.Minutes}分{stopwatch.Elapsed.Seconds}秒";

            Console.WriteLine(
                $"截图完成,耗时{stopwatch.Elapsed.Minutes}分{stopwatch.Elapsed.Seconds}秒");

            ScreenshotFinished?.Invoke(this, elapsedTimeForScreenshot);
        }
        #endregion

        bool ImageCompareString(Bitmap firstImage, Bitmap secondImage)
        {
            MemoryStream ms = new MemoryStream();
            firstImage.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            String firstBitmap = Convert.ToBase64String(ms.ToArray());
            ms.Position = 0;
            secondImage.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            String secondBitmap = Convert.ToBase64String(ms.ToArray());
            if (firstBitmap.Equals(secondBitmap))
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        Bitmap DeepCloneEx(Bitmap bitmap)
        {
            Bitmap dstBitmap = bitmap.Clone(new Rectangle(0, 0, bitmap.Width, bitmap.Height), bitmap.PixelFormat);
            return dstBitmap;
        }
    }
}