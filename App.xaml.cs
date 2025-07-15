using DamageMaker.Properties;
using IWshRuntimeLibrary;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Application = System.Windows.Application;
using File = System.IO.File;

namespace DamageMarker
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
#if BEIJING
        public static bool IsBeiJIngOnly = true;
#else
        public static bool IsBeiJIngOnly = false;
#endif
        public static int LastSelectedFolderIndex ;

        private static Mutex mutex = null;

        public static double mouseStartX { get; set; }
        public static double mouseStartY { get; set; }
        public static double mouseEndX { get; set; }
        public static double mouseEndY { get; set; }
     

        App()
        {
            Settings.Default.InPath = Path.GetFullPath(Settings.Default.InPath);
            Settings.Default.OutPath = Path.GetFullPath(Settings.Default.OutPath);
            Settings.Default.DocxPath = Path.GetFullPath(Settings.Default.DocxPath);

        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            if (!File.Exists(Settings.Default.DocxPath))
            {
                Directory.CreateDirectory(Settings.Default.DocxPath);
            }
            if (!File.Exists(Settings.Default.InPath))
            {
                Directory.CreateDirectory($"{Settings.Default.InPath}");
            }
            if (!File.Exists(Settings.Default.OutPath))
            {
                Directory.CreateDirectory($"{Settings.Default.OutPath}");
            }
            if (!File.Exists("./Logs"))
            {
                Directory.CreateDirectory("./Logs");
            }
            if (!File.Exists("./TrackData"))
            {
                Directory.CreateDirectory("./TrackData");
            }
            if (!File.Exists("./ExcelsPath"))
            {
                Directory.CreateDirectory("./ExcelsPath");
            }

          


            const string mutexName = "YourUniqueMutexName";
            bool createdNew;
            mutex = new Mutex(true, mutexName, out createdNew);
            //if (!createdNew)
            //{
            //    // 如果程序已经在运行，则关闭新实例
            //    MessageBox.Show("程序已经在运行中。");
            //    Application.Current.Shutdown();
            //    return;
            //}


            if (!System.Diagnostics.Debugger.IsAttached)
            {
                AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
                DispatcherUnhandledException += OnDispatcherUnhandledException;
            }
            // 调用 ExtractSamples 类
            string rootFolderPath = DamageMaker.Properties.Settings.Default.InPath;//根路径
            string DocumentFile = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);//目标保存路径
            string baseFolderPath=Path.Combine(DocumentFile, "ExtractSamples");
            int intervalDays = 7;//周期天数

            ExtractSamples extractSamples = new ExtractSamples(rootFolderPath, baseFolderPath, intervalDays);
            extractSamples.ProcessingSample();
           
        }
        private void CreateShortcut(string targetPath, string shortcutName)
        {
            string shortcutLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), $"{shortcutName}.lnk");
            WshShell shell = new WshShell();
            IWshShortcut shortcut = (IWshShortcut)shell.CreateShortcut(shortcutLocation);
            shortcut.Description = "Shortcut to TrackData";
            shortcut.TargetPath = targetPath;
            shortcut.Save();
        }


        #region 关于异常捕获处理
        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            LogException((Exception)e.ExceptionObject);
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            LogException(e.Exception);
            HandyControl.Controls.MessageBox.Error(e.Exception.Message);
            e.Handled = true; // 标记异常已处理
        }
        private void LogException(Exception ex)
        {
            string logFilePath = $"./Logs/error.log";
            using (
                StreamWriter writer = new StreamWriter(logFilePath, true))
            {
                writer.WriteLine($"[{DateTime.Now}] \n\rUnhandled Exception:");
              
                writer.WriteLine("--------------------------------------------------");
                writer.WriteLine(ex.StackTrace);
                writer.WriteLine("--------------------------------------------------");
            }
        }

        #endregion

        protected override void OnExit(ExitEventArgs e)
        {
            DamageMaker.Properties.Settings.Default.Save();

            string tempDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Temp");
            if (Directory.Exists(tempDir))
            {
                try
                {
                    foreach (var file in Directory.GetFiles(tempDir))
                    {
                        try
                        {
                            File.Delete(file);
                        }
                        catch (Exception ex)
                        {
                            // 日志记录
                        }
                    }
                }
                catch (Exception ex)
                {
                    // 日志记录
                }
            }
            base.OnExit(e);
        }
    }
}
