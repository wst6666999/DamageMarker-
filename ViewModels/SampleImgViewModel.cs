using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Policy;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DamageMaker.Common;
using DamageMaker.FileHandle;
using DamageMaker.Models;
using DamageMaker.Properties;
using DamageMaker.SqliteServer;
using DamageMaker.Views;
using DamageMarker.ViewModels;
using DamageMarker.Views;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.EMMA;
using HandyControl.Controls;
using HandyControl.Tools.Extension;
using static DamageMaker.DamageDataProcessing.DataConversion;

namespace DamageMaker.ViewModels
{

    public partial class SampleImgViewModel
        : ObservableObject,
            IDialogResultable<DamageCategory>
    {

        internal int currentImgIndex = 0; //当前图片的索引
        private BoxSelectedControl boxSelecteObject; //当前选择的框
        public string ImgFullPath; //图片名称

        [ObservableProperty]
        int leftLineX = 0;
        [ObservableProperty]
        int rightLineX = 0;

        [ObservableProperty]
        Visibility isLineVisibility;

        [ObservableProperty]
        ImageSource? imgSource;

        [ObservableProperty]
        double canvasWidth;

        [ObservableProperty]
        double canvasHeight;
        SqlImgInfo? sqlImgInfo; //当前图片的SqlImgInfo信息

        internal List<DamageData>? damageDataList { get; set; } //json探伤数据序列化后
        internal List<float[]>? damagePoints;

        ObservableCollection<BoxSelectedControl> boxedStack = new(); //当前图片所有的框框
        public ObservableCollection<BoxSelectedControl> BoxedStack
        {
            get => boxedStack;
            set => boxedStack = value;
        }
        [ObservableProperty]
        string outImgPath;

        #region 弹出对话框变量
        DamageCategory result;
        public DamageCategory Result
        {
            get => result;
            set => SetProperty(ref result, value);
        }
        public Action CloseAction { get; set; }

        //ConboBox的绑定
        [ObservableProperty]
        ObservableCollection<DamageCategory> categorys;
        public bool IsNotSave { get; set; } = false;

        #endregion


        public SampleImgViewModel()
        {
            IsLineVisibility=Settings.Default.IsDistictRepeat ? Visibility.Visible : Visibility.Collapsed;

            if (MainWindowViewModel.NeedSavedInfo != null)
            {
                RightLineX = MainWindowViewModel.NeedSavedInfo.ScreenshotWidthPx - MainWindowViewModel.NeedSavedInfo.ScreenshotOffset / 2;
                LeftLineX = MainWindowViewModel.NeedSavedInfo.ScreenshotOffset / 2;
            }
            OutImgPath = "";
            var CategorysTemp = Enum.GetValues(typeof(DamageCategory))
                .Cast<DamageCategory>()
                .ToList();
                Categorys = new ObservableCollection<DamageCategory>(CategorysTemp);

        }
        [RelayCommand]
        private void SelectCategory(string indexString)
        {
            int index = int.Parse(indexString);
            if (index >= 0 && index < Categorys.Count)
            {
                Result = Categorys[index];
            }
        }


        public SampleImgViewModel(
            ImageSource Img,
            double Width,
            double Height,
            List<DamageData>? DamageData,
            List<float[]> DamagePoints,
            int cruuentImgIndex,
            string imgFullPath,
            SqlImgInfo? sqlImgInfo
        ): this()
        {
            this.currentImgIndex = cruuentImgIndex;
            canvasWidth = Width;
            canvasHeight = Height;
            ImgSource = Img;
            this.damageDataList = DamageData;
            this.damagePoints = DamagePoints;
            this.ImgFullPath = imgFullPath;
            this.sqlImgInfo = sqlImgInfo;
            int count = 0;
            //把面积大的放在下面,
            this.damagePoints = DamagePoints.OrderByDescending(x => x[2] * x[3]).ToList();
            foreach (var point in this.damagePoints)
            {
                boxedStack.Add(
                    new BoxSelectedControl()
                    {
                        RectColor = DamageIdToBrush(point[4]),
                        RectX = (int)point[0],
                        RectY = (int)point[1],
                        RectWidth = (int)point[2],
                        RectHeight = (int)point[3],
                        RectRadiusX = point[2] / 2 * (1 - point[5]),
                        RectRadiusY = point[3] / 2 * (1 - point[5]),
                        RectOpacity = point[5],
                        ButtonContent = DamageIdToDamageName(point[4]) + " " + count,
                    }
                );
                count++;
            }
        }

        DamageCategory? CategoryDataResult { get; set; }
        [RelayCommand]
        async void OpenDialog(BoxSelectedControl O)
        {
            boxSelecteObject = O;
            if (!string.IsNullOrEmpty(O.ButtonContent))
            {
                var cate = Regex.Replace(O.ButtonContent, @"\d+$", "");//去除末尾的数字
                cate=cate.Replace("(", "_").Replace(")","");

                Result = (DamageCategory)Enum.Parse(typeof(DamageCategory), cate);
            }
            var dialog = Dialog.Show<BoxSelectedCategoryDialog>();
            CategoryDataResult = await dialog.GetResultAsync<DamageCategory>();//必须用这个语句才能注册close与result
        }

        [RelayCommand]
        void Close()
        {

            IsNotSave = true;
            CloseAction?.Invoke();
            boxSelecteObject = null;
        }


        [RelayCommand]
       async void Modify()
        {
            IsNotSave = false;
            if (boxSelecteObject != null)//进入修改流程
            {
                boxSelecteObject.ButtonContent = Result.ToString();
                boxSelecteObject.RectColor = DamageIdToBrush((float)(int)Result);
                boxSelecteObject.RectColor = DamageIdToBrush((float)Result);
                damagePoints = damagePoints
               .Select(x =>
               {
                   if (x[0] == boxSelecteObject.RectX && x[1] == boxSelecteObject.RectY)
                   {
                       x[4] = (float)Result;
                       x[5] = 1;
                   }
                   return x;
               })
              .ToList();
            }
            //if ((int)Result == 46)//如果选择为厂焊
            //{
            //    //var dialog = Dialog.Show<MileageInputDialog, MileageInputDialogViewModel>();
            //    var dialog = Dialog.Show<MileageInputDialog>().Initialize<MileageInputDialogViewModel>(x => { 

            //      var  FileName = Path.GetFileNameWithoutExtension(ImgFullPath);
            //        var index = FileName.IndexOf("_");
            //        if (index > 0) x.Mileage=FileName.Substring(0, index);               
            //    });
            //    var result = await dialog.GetResultAsync<string>();
            //    if (!string.IsNullOrEmpty(result))
            //    {
            //        // 1. 读取图片文件为 byte[]
            //        byte[] imageBytes = null;
            //        if (File.Exists(ImgFullPath))
            //        {
            //            imageBytes = File.ReadAllBytes(ImgFullPath);
            //            // 2. 更新数据库中的 ImageData 字段
            //            if (sqlImgInfo != null)
            //            {
            //                sqlImgInfo.ImageData = imageBytes;
            //                DataAccess.UpdateImageData(MainWindow.MainVm.SqlImgInfos, ImgFullPath, imageBytes);
            //            }
            //        }
            //        DataAccess.AppendImageRemark(sqlImgInfo, ImgFullPath, $"当前图片存在厂焊,里程数为{result}");
            //        HandyControl.Controls.MessageBox.Info($"输入的里程数为:{result}");
            //        // 伤损信息转为字符串
            //        string damageStr = "";
            //        var data = MainWindow.MainVm.DamageDataList.Where(x => Path.GetFileName(x.Url) == Path.GetFileName(ImgFullPath)).FirstOrDefault();
            //        if (data != null)
            //        {
            //            data.DamagePoint = damagePoints.ToArray();
            //        }
            //        if (damagePoints != null && damagePoints.Count > 0)
            //        {
            //            damageStr = string.Join(";", damagePoints.Select(arr => string.Join(",", arr)));
            //        }
            //        DataAccess.AppendImageRemark(sqlImgInfo, ImgFullPath, $"，伤损点:{damageStr}");
            //        HandyControl.Controls.MessageBox.Info($"伤损点: {damageStr}");
            //        // 获取作业区间
            //        string workRange = "";
            //        var railInfo = MainWindowViewModel.NeedSavedInfo?.RailWayInfo;
            //        DataAccess.AppendImageRemark(sqlImgInfo, ImgFullPath, $"，作业区间:{railInfo?.WorkSection ?? "未知"}");
            //        HandyControl.Controls.MessageBox.Info($"作业区间: {railInfo?.WorkSection ?? "未知"}");

            //    }
            //}
            if ((int)Result == 46) // 如果选择为厂焊
            {
                var dialog = Dialog.Show<MileageInputDialog>().Initialize<MileageInputDialogViewModel>(x => {
                    var FileName = Path.GetFileNameWithoutExtension(ImgFullPath);
                    var index = FileName.IndexOf("_");
                    if (index > 0) x.Mileage = FileName.Substring(0, index);
                });
                var result = await dialog.GetResultAsync<string>();

                if (!string.IsNullOrEmpty(result))
                {
                    // 伤损信息转为字符串
                    string damageStr = "";
                    if (damagePoints != null && damagePoints.Count > 0)
                    {
                        damageStr = string.Join(";", damagePoints.Select(arr => string.Join(",", arr)));
                    }
                    var railInfo = MainWindowViewModel.NeedSavedInfo?.RailWayInfo;
                    string workRange = railInfo?.WorkSection ?? "未知";

                    // 组装remark
                    string remark = $"当前图片存在厂焊,里程数为{result}，伤损点:{damageStr}，作业区间:{workRange}";

                    // 读取图片文件为 byte[]
                    byte[] imageBytes = File.ReadAllBytes(ImgFullPath.OutReplaceInString());

                    Console.WriteLine($"厂焊图片的路径:{ImgFullPath.OutReplaceInString()}");
                    // 更新数据库和内存
                    DataAccess.UpdateImageRemark(MainWindow.MainVm.SqlImgInfos, ImgFullPath, remark);
                    DataAccess.UpdateImageData(MainWindow.MainVm.SqlImgInfos, ImgFullPath, imageBytes);

                    //重新加载项目总览
                    MainWindow.MainVm.LoadProjectOverview();
                    //更新result.json
                    if (MainWindow.MainVm.DamageDataList != null)
                    {
                        var data = MainWindow.MainVm.DamageDataList
                            .FirstOrDefault(x => Path.GetFileName(x.Url) == Path.GetFileName(ImgFullPath));
                        if (data != null)
                        {
                            data.DamagePoint = damagePoints.ToArray();
                        }
                    }
                    AboutJson.SaveJson<List<DamageData>>(MainWindow.MainVm.DamageDataList, Path.Combine(Settings.Default.InPath, MainWindow.MainVm.ImgFolderName), "result.json");
                    HandyControl.Controls.MessageBox.Info($"输入的里程数为:{result}");
                    //HandyControl.Controls.MessageBox.Info($"伤损点: {damageStr}");
                    HandyControl.Controls.MessageBox.Info($"作业区间: {workRange}");
                }
            }

            if (string.IsNullOrEmpty(OutImgPath))
            {
                OutImgPath = this.ImgFullPath.InReplaceOutString();
            }
            boxSelecteObject = null; //防止新增框选的时候，误修改上一个对象
            CloseAction?.Invoke();

            Console.WriteLine(Result.ToString());

        }

        [RelayCommand]
        void Delete()
        {
            IsNotSave = true;
            var result = System.Windows.MessageBox.Show("确定删除吗 ??", "删除?", MessageBoxButton.YesNo);

            if (result == MessageBoxResult.Yes && boxSelecteObject != null)
            {
                BoxedStack.Remove(boxSelecteObject);
                damagePoints = damagePoints?.Where(x => !((int)x[0] == boxSelecteObject.RectX && (int)x[1] == boxSelecteObject.RectY))
                    .ToList();

                if (string.IsNullOrEmpty(OutImgPath))
                {
                    OutImgPath = this.ImgFullPath.InReplaceOutString();
                }
                CloseAction?.Invoke();
            }
            else if (boxSelecteObject == null)
            {
                HandyControl.Controls.MessageBox.Warning("当前没有选中任何对象");
            }
        }
        [RelayCommand]
        void DeleteAll()
        {
            IsNotSave = true;
            var result = System.Windows.MessageBox.Show("确定删除吗 ??", "删除?", MessageBoxButton.YesNo);
            if (result == MessageBoxResult.Yes)
            {
                BoxedStack.Clear();
                damagePoints = new List<float[]>();
                if (string.IsNullOrEmpty(OutImgPath))
                {
                    OutImgPath = this.ImgFullPath.InReplaceOutString();
                }
                CloseAction?.Invoke();
            }
        }



        [RelayCommand]
        void SampleClosing()
        {
            if (currentImgIndex != -1)//如果等于-1 则代表这个图片以前没有任何数据
            {
                if (!Utilities.AreArraysEqual<float>(MainWindow.MainVm.DamageDataList[currentImgIndex].DamagePoint, damagePoints.ToArray()))
                {
                    MainWindow.MainVm.DamageDataList[currentImgIndex].DamagePoint = damagePoints.ToArray();
                    MainWindow.MainVm.damagePoints = damagePoints.ToArray();
                    MainWindow.MainVm.IsModifyResultJson = true;
                }
            }
            else
            {
                if (damagePoints.Count > 0)
                {
                    MainWindow.MainVm.DamageDataList.Add(new DamageData() { Url = this.ImgFullPath, DamagePoint = damagePoints.ToArray() });
                    MainWindow.MainVm.damagePoints = damagePoints.ToArray();
                    MainWindow.MainVm.IsModifyResultJson = true;
                }
            }


        }

    }
}
