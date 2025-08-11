using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DamageMaker.FileHandle;
using DamageMaker.Models;
using DamageMaker.Properties;
using DamageMaker.SqliteServer;
using DamageMarker.ViewModels;
using DamageMarker.Views;
using HandyControl.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using MessageBox = HandyControl.Controls.MessageBox;

namespace DamageMaker.ViewModels
{
    public partial class RailwayInfoInputViewModel:ObservableObject
    {
        [ObservableProperty]
        string ?instruments;
        [ObservableProperty]
        string serialNumber;
        [ObservableProperty]
        string workDate;
        [ObservableProperty]
        string workSection;
        [ObservableProperty]
        string ?workLength;
     
        [ObservableProperty]
         string ?selectedLineType;
        [ObservableProperty]
        string ?workGroup;
        [ObservableProperty]
        string ?operatorName;

        string RailwayName;

        public static ScreenshotInfo NeedSavedInfo { get; set; } =new ScreenshotInfo();

        public RailwayInfoInputViewModel()
        {
            PlaybackWindow.ScreenshotStart += OnScreenShotStart;
            PlaybackWindow.ScreenshotFinished += OnScreenShotFinished;
            MainWindowViewModel.DataAnalyzeFinished += OnDataAnalyzeFinished;
        }

        private void OnDataAnalyzeFinished(string time)
        {
           NeedSavedInfo.RailWayInfo.ElapsedTimeForAnalyze = time;
            AboutJson.SaveJson(NeedSavedInfo, Path.Combine(Settings.Default.InPath, MainWindow.MainVm.ImgFolderName), "info.json");          
        }

        private void OnScreenShotStart(object sender, EventArgs args)
        {


            RailwayName = WorkSection + "+" + SerialNumber + "+" + WorkDate;
            MainWindowViewModel.FilePathIn = Path.GetFullPath(Path.Combine(Settings.Default.InPath, RailwayName));

            MainWindow.MainVm.ImgFolderName = RailwayName;

            if (!Directory.Exists(MainWindowViewModel.FilePathIn))
            {
                Directory.CreateDirectory(MainWindowViewModel.FilePathIn);
            }
            //else
            //{
            //    // 检查目录是否已存在且不为空
            //    if (Directory.GetFiles(MainWindowViewModel.FilePathIn).Length > 0)
            //    {
            //        // 弹出对话框询问用户
            //        var result = MessageBox.Show("截图目录已存在且不为空，是否继续截图？继续截图将会覆盖上次分析结果。",
            //                                  "目录已存在",
            //                                  MessageBoxButton.YesNo,
            //                                  MessageBoxImage.Question);

            //        if (result == MessageBoxResult.No)
            //        {
            //            // 用户选择不继续，停止截图流程
            //            var playbackWindow = Application.Current.Windows.OfType<PlaybackWindow>().FirstOrDefault();
            //            playbackWindow?.StopPlayback();
            //            MainWindow.MainVm.isAnalyze = false;
            //            return;
            //        }
            //    }
            //}
        }

        private void OnScreenShotFinished(object sender, string time)
        {
            var o = sender as PlaybackWindow;
            NeedSavedInfo = NeedSavedInfo ?? new();
            NeedSavedInfo.ScreenshotOffset = o?.MoveRepeatPx ?? 0;
            NeedSavedInfo.ScreenshotDirection = o?.PlaybackDirection ?? MoveDirection.Left;
            NeedSavedInfo.ScreenshotWidthPx = o?.ImgWidthPx ?? 0;

            NeedSavedInfo.RailWayInfo = new RailInfo()
            {
                RailWayName = RailwayName,
                Instruments = this.Instruments,
                SerialNumber = this.SerialNumber,
                WorkDate = WorkDate,
                WorkSection = this.WorkSection,
                WorkLength = this.WorkLength,
                SelectedLineType = this.SelectedLineType,
                WorkGroup = this.WorkGroup,
                OperatorName = this.OperatorName,
                AnalyzeTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                ElapsedTimeForScrrnshot = time,
            };
            //AboutJson.SaveJson(NeedSavedInfo, Path.Combine(Settings.Default.InPath, MainWindow.MainVm.ImgFolderName), "info.json");
            Application.Current.MainWindow.WindowState = WindowState.Maximized;
        }



        

        [RelayCommand]
        void SaveRailWayInfo(HandyControl.Controls.Window win)
        {
            if (!string.IsNullOrEmpty(SerialNumber) && WorkDate != null && !string.IsNullOrEmpty(WorkSection))
            {
                // ✅ 将数据存入共享静态变量
                SharedRailwayInfo.SerialNumber = SerialNumber;
                SharedRailwayInfo.WorkDate = WorkDate;
                SharedRailwayInfo.WorkSection = WorkSection;

                MessageBox.Success("信息保存完成");
                win.Close();
            }
            else
            {
                MessageBox.Info("信息输入不完整，请填写完整信息");
            }
        }
    }
}
