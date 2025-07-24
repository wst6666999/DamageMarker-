using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using DamageMaker.Models;
using DamageMaker.Properties;
using DamageMarker.ViewModels;
using HandyControl.Controls;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DamageMaker.Message;

namespace DamageMaker.ViewModels
{
    //
    public  partial class SetupContentViewModel:ObservableObject
    {
        public static event Action<int> ?RulesCountChange;

        //定义静态实例
        public static SetupContentViewModel Instance { get; } = new();
        [ObservableProperty]
        int displayedRulesCount = 10;

        private int screenshotInterval= DamageMaker.Properties.Settings.Default.ScreenshotInterval; //截图时间间隔
        public int ScreenshotInterval
        {
            get { return screenshotInterval; }
            set
            {
                if (value != screenshotInterval)
                {
                    DamageMaker.Properties.Settings.Default.ScreenshotInterval = value;
                    SetProperty(ref screenshotInterval, value);
                }
            }
        }

        private bool isDistinctDamage = Settings.Default.IsDistictRepeat;
        public bool IsDistinctDamage
        {
            get { return isDistinctDamage; }
            set
            {
                if (value != isDistinctDamage)
                {
                    Settings.Default.IsDistictRepeat = value;
                    SetProperty(ref isDistinctDamage, value);
                }
            }
        }

        private bool isConcealFishScale = Settings.Default.IsConcealFishScale;
        public bool IsConcealFishScale
        {
            get { return isConcealFishScale;}
            set
            {
                if (value != isConcealFishScale)
                {
                    Settings.Default.IsConcealFishScale = value;
                    SetProperty(ref isConcealFishScale, value);
                }
            }
        }




        private string inPath = Settings.Default.InPath;
        public string InPath
        {
            get { return inPath; }
            set
            {
                if (value != inPath)
                {
                    Settings.Default.InPath = value;
                    SetProperty(ref inPath, value);
                }
            }
        }
       
        private string outPath = Settings.Default.OutPath;
        public string OutPath
        {
            get { return outPath; }
            set
            {
                if (value != outPath)
                {
                    Settings.Default.OutPath = value;
                    SetProperty(ref outPath, value);
                }
            }
        }
        
        private string docxPath = Settings.Default.DocxPath;

        public string DocxPath
        {
            get { return docxPath; }
            set
            {
                if (value != docxPath)
                {
                    Settings.Default.DocxPath = value;
                    SetProperty(ref docxPath, value);
                }
            }
        }

        private string excelsPath = Settings.Default.ExcelsPath;

        public string ExcelsPath
        {
            get { return excelsPath; }
            set
            {
                if (value != excelsPath)
                {
                    Settings.Default.ExcelsPath = value;
                    SetProperty(ref excelsPath, value);
                }
            }
        }






        public SetupContentViewModel() { 
        
        
        }
        [RelayCommand]
        void ConcealFishScaleChanged()
        {
          Console.WriteLine("隐藏鱼鳞设置已更改: " + IsConcealFishScale+"将清空所有out文件夹");
           Directory.Delete(Settings.Default.OutPath, true);


        }



        [RelayCommand]
        void DisplayedRulesCountChanged()
        {
           RulesCountChange?.Invoke(DisplayedRulesCount);
        }


        [RelayCommand]
        void ChangeFilePath(string path)
        {
            var FolderDialog = new OpenFolderDialog();
            FolderDialog.Title = "请选择文件夹";
            FolderDialog.InitialDirectory = path;
            var result = FolderDialog.ShowDialog();
            if (!(result ?? false))
            {
                MessageBox.Warning("当前未选择任何文件夹,请重新选择文件夹");
                return;
            }

            if (path == "in路径")
            {
                InPath = FolderDialog.FolderName;
              

                MessageBox.Info("截图路径已经更改为" + FolderDialog.FolderName);

            }
            else if (path == "out路径")
            {
                OutPath = FolderDialog.FolderName;
               
                MessageBox.Info("伤损图片路径已经更改为" + FolderDialog.FolderName);
            }
            else if (path == "word路径")
            {
                DocxPath = FolderDialog.FolderName;
               
                MessageBox.Info("word报告路径已经更改为" + FolderDialog.FolderName);
            }
            else if (path == "Excel路径")
            {
                ExcelsPath = FolderDialog.FolderName;

                MessageBox.Info("Excel报告路径已经更改为" + FolderDialog.FolderName);
            }
            Settings.Default.Save();
        }
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TotalHiddenTracks))]
        private int hideTestTrackCountBefore;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TotalHiddenTracks))]
        private int hideTestTrackCountAfter;

        // 计算总屏蔽数（用于UI显示）
        public int TotalHiddenTracks =>HideTestTrackCountBefore + HideTestTrackCountAfter;

        [RelayCommand]
        private void ApplyTrackFilter(object resetFlag)
        {
            // 处理重置逻辑
            if (resetFlag is bool shouldReset && shouldReset)
            {
                HideTestTrackCountBefore = 0;
                HideTestTrackCountAfter = 0;
                return;
            }

            // 正常验证逻辑
            if (HideTestTrackCountBefore < 0 || HideTestTrackCountAfter < 0)
            {
                Growl.Error("屏蔽数量不能为负数");
                return;
            }

            WeakReferenceMessenger.Default.Send(new TrackShieldingMessage(
                BeforeCount: HideTestTrackCountBefore,
                AfterCount: HideTestTrackCountAfter
            ));
        }
    }
}
