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
using System.Diagnostics;

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
        bool PlaybackStopYN;
        bool PlaybackingYN;
        string filePathIn;

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
            lock (_lock)
            {
                if (singlePlaybackWindow == null)
                {
                    singlePlaybackWindow = new PlaybackWindow(left, top, width, height);
                    Console.WriteLine(left);
                    Console.WriteLine(top);
                    Console.WriteLine(width);
                    Console.WriteLine(height);
                }
                else
                {
                    MessageBox.Warning("当前已经有一个回放窗口");
                }
            }

            return singlePlaybackWindow;

        }


        private PlaybackWindow(double left, double top, double width, double height)
        {

            InitializeComponent();
            Thickness thickness = new Thickness();
            thickness.Left = left;
            thickness.Top = top;
            selectionBorder.Margin = thickness;
            selectionBorder.Width = width;
            selectionBorder.Height = height;
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
        void Window_Loaded(object sender, RoutedEventArgs e)
        {
            temporaryWidth = selectionBorder.Width;
            temporaryHeight = selectionBorder.Height;
            temporaryLeft = this.PointToScreen(new System.Windows.Point((int)selectionBorder.Margin.Left, (int)selectionBorder.Margin.Top)).X;
            temporaryRight = this.PointToScreen(new System.Windows.Point((int)selectionBorder.Margin.Left + (int)temporaryWidth, (int)selectionBorder.Margin.Top)).X;
            temporaryTop = this.PointToScreen(new System.Windows.Point((int)selectionBorder.Margin.Left, (int)selectionBorder.Margin.Top)).Y;
        }

        Bitmap CaptureCurrentScreen()
        {
            //创建与屏幕大小相同的位图对象
            var bmpScreen = new Bitmap((int)selectionBorder.Width, (int)selectionBorder.Height);
            //使用位图对象来创建Graphics的对象
            using (Graphics g = Graphics.FromImage(bmpScreen))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;//设置平滑模式，抗锯齿
                g.CompositingQuality = CompositingQuality.HighQuality;//设置合成质量
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;//设置插值模式
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;//设置文本呈现的质量
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;//设置呈现期间，像素偏移的方式
                g.CopyFromScreen((int)temporaryLeft, (int)temporaryTop, 0, 0, bmpScreen.Size);//利用CopyFromScreen将当前屏幕截图并将内容存储在bmpScreen的位图中
            }
            return bmpScreen;
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
            //MoveRepeatPx = (int)(temporaryWidth - (temporaryWidth / 2 + temporaryWidth * screenshotOfferset * 0.01));
            ImgWidthPx = (int)temporaryWidth;
            //截图开始计时
            stopwatch.Restart();
            ScreenshotStart?.Invoke(this, EventArgs.Empty);
            if (PlaybackingYN) return;
            PlaybackingYN = true;

            Bitmap? temporaryBitmap1 = null;
            Bitmap? temporaryBitmap2 = null;
            int temporarySameNum = 0;
            filePathIn = MainWindowViewModel.FilePathIn;
            int count = 0; 

            
            //聚焦到应用
           
            
            //使用Flaui 获取聚焦应用名
            
            PlaybackedAppName= AppInfo.GetFocusedApplicationName();
            await Task.Delay (1000);
            if (PlaybackedAppName.Contains("JGT-6M"))
            {
                keystrokes = 2;
            }
          else  if (PlaybackedAppName.Contains("800A"))
            {
                RightKeyShort = VirtualKeyShort.OEM_PERIOD;
                LeftKeyShort = VirtualKeyShort.OEM_COMMA;
            }else if (PlaybackedAppName.Contains("gt_20_replayer.exe"))
            {
                Mouse.MoveTo(new Point((int)(400), (int)(400)));
                Mouse.LeftClick();
                await Task.Delay(100);
                Mouse.MoveTo(new Point((int)(1800), (int)(1000)));
            }
            //else if (PlaybackedAppName.Contains("CTKJ-B"))
            //{
            //    sleepTime = 120;
            //}



            while (PlaybackStopYN)
                {
                    if (temporaryBitmap1 != null)
                    {
                        temporaryBitmap2 = DeepCloneEx(temporaryBitmap1);
                    }

                    this.Dispatcher.Invoke(DispatcherPriority.Normal, (ThreadStart)delegate ()
                    {
                        Growl.ClearGlobal();
                        temporaryBitmap1 = CaptureCurrentScreen();
                    });

                    if (temporaryBitmap2 != null)
                    {
                        if (!ImageCompareString(temporaryBitmap2, temporaryBitmap1))
                        {
                            if (count == 1)
                            {
                                int MovePx = 0;
                                if (direction < 0)
                                { 
                                    if(PlaybackedAppName.Contains("RailTest")|| PlaybackedAppName.Contains("CTKJ")|| PlaybackedAppName.Contains("大仪器"))
                                    {
                                        MovePx = ImgProcessing.GetImgOffset(temporaryBitmap2, temporaryBitmap1, true);
                                    }
                                    else
                                    {
                                        MovePx = ImgProcessing.GetImgOffset(temporaryBitmap2, temporaryBitmap1, false);
                                    }

                                                               
                                }
                                else if (direction > 0)
                                {
                                    if (PlaybackedAppName.Contains("RailTest") || PlaybackedAppName.Contains("CTKJ")||PlaybackedAppName.Contains("大仪器"))
                                    {
                                        MovePx = ImgProcessing.GetImgOffset(temporaryBitmap2, temporaryBitmap1, false);
                                    }
                                    else
                                    {
                                        MovePx = ImgProcessing.GetImgOffset(temporaryBitmap2, temporaryBitmap1, true);                                  
                                    }
                                }
                                MoveRepeatPx = temporaryBitmap2.Width - MovePx;
                                Console.WriteLine($"图片偏移量:{MovePx}");
                            }



                            var Mileagedata = await MileageRecognition.CropAndRecognizeMileage(temporaryBitmap1, 45);
                            var mileage = Mileagedata.ExtractDistance(PlaybackedAppName);
                            string imgFullPath;
                            if (string.IsNullOrEmpty(mileage))
                            {
                                imgFullPath = filePathIn + $@"\{count++}.png";
                            }
                            else
                            {
                                imgFullPath = filePathIn + $@"\{mileage}_{count++}.png";
                            }

                            temporarySameNum = 0;
                        Console.WriteLine(imgFullPath);
                        temporaryBitmap1.Save(imgFullPath, System.Drawing.Imaging.ImageFormat.Png);
                            OcrDataList.Add(new OcrData(imgFullPath, Mileagedata));
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
                                Console.WriteLine($"请注意,当前截图出现重复,图片序列号为{count}.请适当降低截图移动像素");
                                continue;
                            }
                        }
                    }
                    else
                    {
                        var Mileagedata = await MileageRecognition.CropAndRecognizeMileage(temporaryBitmap1, 45);
                        var mileage = Mileagedata.ExtractDistance(PlaybackedAppName);
                        string imgFullPath;
                        if (string.IsNullOrEmpty(mileage))
                        {
                            imgFullPath = filePathIn + $@"\{count++}.png";
                        }
                        else
                        {
                            imgFullPath = filePathIn + $@"\{mileage} _{count++}.png";
                        }
                    Console.WriteLine("imgFullPath");
                        temporaryBitmap1.Save(imgFullPath, System.Drawing.Imaging.ImageFormat.Png);
                        OcrDataList.Add(new OcrData(imgFullPath, Mileagedata));
                    }

                    Mouse.MovePixelsPerMillisecond = Settings.Default.MouseMovePixel;
                    if (direction < 0)//向左截图
                    {

                    for (int i = 0; i < keystrokes; i++)
                    {
                        FlaUI.Core.Input.Keyboard.Pressing(LeftKeyShort);
                        await Task.Delay(sleepTime);
                        FlaUI.Core.Input.Keyboard.Release(LeftKeyShort);

                    }
                }
                else if (direction > 0)//向右边截图
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

            base.Close();
            PlaybackingYN = false;
            if (OcrDataList.Count > 0)
            {
                string jsonString = JsonSerializer.Serialize(OcrDataList, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(Path.Combine(filePathIn, "OcrResult.json"), jsonString);
            }
            else
            {
                MessageBox.Info("未识别到到有效里程数据");
            }
            stopwatch.Stop();
            string ElapsedTimeForScrrnshot = $"{stopwatch.Elapsed.Minutes}分{stopwatch.Elapsed.Seconds}秒";

            Console.WriteLine($"截图完成,耗时{stopwatch.Elapsed.Minutes}分{stopwatch.Elapsed.Seconds}秒");

            ScreenshotFinished?.Invoke(this, ElapsedTimeForScrrnshot);
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
