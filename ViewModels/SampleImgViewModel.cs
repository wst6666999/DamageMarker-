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
using Microsoft.Data.Sqlite;
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

        private BoxSelectedControl? currentHighlightedBox = null;

        [ObservableProperty]
        string imageFileName;

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
            IsLineVisibility = Settings.Default.IsDistictRepeat ? Visibility.Visible : Visibility.Collapsed;

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


        ) : this()
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
            ImageFileName = Path.GetFileName(imgFullPath);
            //把面积大的放在下面,
            this.damagePoints = DamagePoints.OrderByDescending(x => x[2] * x[3]).ToList();
            foreach (var point in this.damagePoints)
            {
                var box = new BoxSelectedControl()
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

                    // 关键：绑定这个框对应的原始 damagePoint
                    Tag = point
                };

                boxedStack.Add(box);

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
                cate = cate.Replace("(", "_").Replace(")", "");

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
            Console.WriteLine("==== 进入 Modify ====");
            Console.WriteLine($"boxSelecteObject 是否为空: {boxSelecteObject == null}");

            IsNotSave = false;

            if (boxSelecteObject != null)//进入修改流程
            {
                Console.WriteLine($"修改前颜色: {boxSelecteObject.RectColor}");
                Console.WriteLine($"修改前内容: {boxSelecteObject.ButtonContent}");
                Console.WriteLine($"坐标: ({boxSelecteObject.RectX}, {boxSelecteObject.RectY})");

                // 1. 修改当前框 UI
                boxSelecteObject.ButtonContent = Result.ToString();
                boxSelecteObject.RectColor = DamageIdToBrush((float)(int)Result);

                // 关键：这些属性必须是 ObservableProperty，用来强制触发框体重绘
                boxSelecteObject.RectOpacity = 1;
                boxSelecteObject.RectRadiusX = 0;
                boxSelecteObject.RectRadiusY = 0;

                Console.WriteLine($"修改后颜色: {boxSelecteObject.RectColor}");
                Console.WriteLine($"修改后内容: {boxSelecteObject.ButtonContent}");

                // 2. 等待 WPF 真正完成渲染
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(
                    () => { },
                    System.Windows.Threading.DispatcherPriority.Render
                );

                // 3. 修改内存 damagePoints
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

                // 4. 更新数据库中对应坐标的损伤类型
                if (sqlImgInfo != null)
                {
                    using (var sqlHelper = new SQLHelper(Settings.Default.SqlPath))
                    {
                        await sqlHelper.UpdateDamageAnnotationAsync(
                            sqlImgInfo.ImgId,
                            boxSelecteObject.RectX,
                            boxSelecteObject.RectY,
                            (float)Result
                        );
                    }
                }
            }
            else
            {
                Console.WriteLine("没有进入修改分支，因为 boxSelecteObject == null");
            }

            var dialog = Dialog.Show<MileageInputDialog>().Initialize<MileageInputDialogViewModel>(x =>
            {
                var FileName = Path.GetFileNameWithoutExtension(ImgFullPath);
                var index = FileName.IndexOf("_");
                if (index > 0) x.Mileage = FileName.Substring(0, index);
            });

            var result = await dialog.GetResultAsync<string>();

            if (!string.IsNullOrEmpty(result))
            {
                // 保存里程到数据库
                if (sqlImgInfo != null)
                {
                    sqlImgInfo.Mileage = result;
                    using (var sqlHelper = new SQLHelper(Settings.Default.SqlPath))
                    {
                        await sqlHelper.UpdateSqlImgInfoAsync(sqlImgInfo);
                    }
                }
            }

            // 5. 不管里程有没有输入，都同步 result.json
            if (MainWindow.MainVm.DamageDataList != null)
            {
                var data = MainWindow.MainVm.DamageDataList
                    .FirstOrDefault(x => Path.GetFileName(x.Url) == Path.GetFileName(ImgFullPath));

                if (data != null)
                {
                    data.DamagePoint = damagePoints.ToArray();
                }

                AboutJson.SaveJson<List<DamageData>>(
                    MainWindow.MainVm.DamageDataList,
                    Path.Combine(Settings.Default.InPath, MainWindow.MainVm.ImgFolderName),
                    "result.json"
                );

                MainWindow.MainVm.damagePoints = damagePoints.ToArray();
            }

            if (string.IsNullOrEmpty(OutImgPath))
            {
                OutImgPath = this.ImgFullPath.InReplaceOutString();
            }

            // 6. 再等一次渲染，避免关闭窗口截图时还是旧颜色
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(
                () => { },
                System.Windows.Threading.DispatcherPriority.Render
            );

            boxSelecteObject = null; //防止新增框选的时候，误修改上一个对象
            CloseAction?.Invoke();

            Console.WriteLine(Result.ToString());
            MainWindow.MainVm.LoadProjectOverview();
        }

        public async Task<bool> AutoModifyTargetBoxToNormalWeldAsync()
        {
            const int NORMAL_WELD_ID = 2; // 普通焊缝
            int[] targetIds = { 52 };

            if (damagePoints == null || damagePoints.Count == 0)
                return false;

            var targetPoint = damagePoints
                .FirstOrDefault(x => x.Length >= 5 && targetIds.Contains((int)x[4]));

            if (targetPoint == null)
                return false;

            Result = (DamageCategory)NORMAL_WELD_ID;

            // 1. 改之前，先把旧 id 和坐标存到 Images 表，供再次点击“无伤”时回退
            if (sqlImgInfo != null)
            {
                using var sqlHelper = new SQLHelper(Settings.Default.SqlPath);
                sqlHelper.EnsureConnectionOpen();

                string sql = @"
                    UPDATE Images
                    SET AutoModifyOldDamageType = @OldType,
                        AutoModifyX = @X,
                        AutoModifyY = @Y
                    WHERE ImageId = @ImageId";

                using var cmd = new SqliteCommand(sql, sqlHelper.Connection);
                cmd.Parameters.AddWithValue("@OldType", (int)targetPoint[4]);
                cmd.Parameters.AddWithValue("@X", targetPoint[0]);
                cmd.Parameters.AddWithValue("@Y", targetPoint[1]);
                cmd.Parameters.AddWithValue("@ImageId", sqlImgInfo.ImgId);
                cmd.ExecuteNonQuery();
            }

            // 2. 改内存数据：52 -> 2
            targetPoint[4] = NORMAL_WELD_ID;
            targetPoint[5] = 1;

            // 直接通过引用找到对应框
            var targetBox = BoxedStack
                .FirstOrDefault(b => ReferenceEquals(b.Tag, targetPoint));

            if (targetBox != null)
            {
                targetBox.ButtonContent = DamageIdToDamageName(NORMAL_WELD_ID);
                targetBox.RectColor = DamageIdToBrush(NORMAL_WELD_ID);
                targetBox.RectOpacity = 1;
                targetBox.RectRadiusX = 0;
                targetBox.RectRadiusY = 0;
            }

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(
                () => { },
                System.Windows.Threading.DispatcherPriority.Render
            );

            // 4. 更新数据库 DamageAnnotations
            if (sqlImgInfo != null)
            {
                using var sqlHelper = new SQLHelper(Settings.Default.SqlPath);
                await sqlHelper.UpdateDamageAnnotationAsync(
                    sqlImgInfo.ImgId,
                    (int)targetPoint[0],
                    (int)targetPoint[1],
                    NORMAL_WELD_ID
                );
            }

            // 5. 弹里程输入框
            var dialog = Dialog.Show<MileageInputDialog>().Initialize<MileageInputDialogViewModel>(x =>
            {
                var fileName = Path.GetFileNameWithoutExtension(ImgFullPath);
                var index = fileName.IndexOf("_");
                if (index > 0) x.Mileage = fileName.Substring(0, index);
            });

            var result = await dialog.GetResultAsync<string>();

            if (!string.IsNullOrEmpty(result))
            {
                if (sqlImgInfo != null)
                {
                    sqlImgInfo.Mileage = result;
                    using var sqlHelper = new SQLHelper(Settings.Default.SqlPath);
                    await sqlHelper.UpdateSqlImgInfoAsync(sqlImgInfo);
                }
            }

            // 6. 不管里程有没有输入，都同步 result.json 和主窗口 damagePoints
            if (MainWindow.MainVm.DamageDataList != null)
            {
                var data = MainWindow.MainVm.DamageDataList
                    .FirstOrDefault(x => Path.GetFileName(x.Url) == Path.GetFileName(ImgFullPath));

                if (data != null)
                    data.DamagePoint = damagePoints.ToArray();

                AboutJson.SaveJson<List<DamageData>>(
                    MainWindow.MainVm.DamageDataList,
                    Path.Combine(Settings.Default.InPath, MainWindow.MainVm.ImgFolderName),
                    "result.json"
                );

                MainWindow.MainVm.damagePoints = damagePoints.ToArray();
            }

            if (string.IsNullOrEmpty(OutImgPath))
                OutImgPath = ImgFullPath.InReplaceOutString();

            MainWindow.MainVm.LoadProjectOverview();
            return true;
        }

        public async Task<bool> AutoRestoreModifiedBoxAsync(float oldType, float oldX, float oldY)
        {
            if (damagePoints == null || damagePoints.Count == 0)
                return false;

            // 回退时找“第一次被自动改成 id=2 的框”。
            // 优先按数据库记录的 oldX/oldY 找；如果坐标有轻微变化，就找最近的 id=2 框。
            var targetPoint = damagePoints
                .Where(x => x != null && x.Length >= 5 && (int)x[4] == 2)
                .OrderBy(x => Math.Abs(x[0] - oldX) + Math.Abs(x[1] - oldY))
                .FirstOrDefault();

            if (targetPoint == null)
                return false;

            float distance = Math.Abs(targetPoint[0] - oldX) + Math.Abs(targetPoint[1] - oldY);
            if (distance > 80)
            {
                Console.WriteLine($"自动回退失败：没有找到接近原坐标的 id=2 框。old=({oldX},{oldY}), nearest=({targetPoint[0]},{targetPoint[1]}), distance={distance}");
                return false;
            }

            // 1. 恢复本窗口内存数据
            targetPoint[4] = oldType;
            targetPoint[5] = 1;

            // 2. 同步修改窗口里的框体显示
            var targetBox = BoxedStack.FirstOrDefault(b => ReferenceEquals(b.Tag, targetPoint));

            if (targetBox == null)
            {
                targetBox = BoxedStack.FirstOrDefault(b =>
                    Math.Abs(b.RectX - targetPoint[0]) <= 3 &&
                    Math.Abs(b.RectY - targetPoint[1]) <= 3);
            }

            if (targetBox != null)
            {
                targetBox.ButtonContent = DamageIdToDamageName(oldType);
                targetBox.RectColor = DamageIdToBrush(oldType);
                targetBox.RectOpacity = 1;
                targetBox.RectRadiusX = 0;
                targetBox.RectRadiusY = 0;
            }

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(
                () => { },
                System.Windows.Threading.DispatcherPriority.Render
            );

            // 3. 更新数据库 DamageAnnotations，并清空 Images 表里的自动修改记录
            if (sqlImgInfo != null)
            {
                using var sqlHelper = new SQLHelper(Settings.Default.SqlPath);

                await sqlHelper.UpdateDamageAnnotationAsync(
                    sqlImgInfo.ImgId,
                    (int)targetPoint[0],
                    (int)targetPoint[1],
                    oldType
                );

                sqlHelper.EnsureConnectionOpen();

                string clearSql = @"
                    UPDATE Images
                    SET AutoModifyOldDamageType = NULL,
                        AutoModifyX = NULL,
                        AutoModifyY = NULL
                    WHERE ImageId = @ImageId";

                using var clearCmd = new SqliteCommand(clearSql, sqlHelper.Connection);
                clearCmd.Parameters.AddWithValue("@ImageId", sqlImgInfo.ImgId);
                clearCmd.ExecuteNonQuery();
            }

            // 4. 更新 result.json 和主窗口当前 damagePoints
            if (MainWindow.MainVm.DamageDataList != null)
            {
                var data = MainWindow.MainVm.DamageDataList
                    .FirstOrDefault(x => Path.GetFileName(x.Url) == Path.GetFileName(ImgFullPath));

                if (data != null)
                    data.DamagePoint = damagePoints.ToArray();

                AboutJson.SaveJson<List<DamageData>>(
                    MainWindow.MainVm.DamageDataList,
                    Path.Combine(Settings.Default.InPath, MainWindow.MainVm.ImgFolderName),
                    "result.json"
                );

                MainWindow.MainVm.damagePoints = damagePoints.ToArray();
            }

            if (string.IsNullOrEmpty(OutImgPath))
                OutImgPath = ImgFullPath.InReplaceOutString();

            MainWindow.MainVm.LoadProjectOverview();
            return true;
        }



        [RelayCommand]
        async void Delete()
        {
            IsNotSave = true;
            var result = System.Windows.MessageBox.Show("确定删除吗 ??", "删除?", MessageBoxButton.YesNo);

            if (result == MessageBoxResult.Yes && boxSelecteObject != null)
            {
                // 如果删除的是高亮框，清除高亮状态
                if (currentHighlightedBox == boxSelecteObject)
                {
                    currentHighlightedBox = null;
                }
                // 记录要删除的坐标
                int deleteX = boxSelecteObject.RectX;
                int deleteY = boxSelecteObject.RectY;

                // 从UI集合中删除
                BoxedStack.Remove(boxSelecteObject);

                // 先获取要删除的damagePoint（在过滤之前）
                float[] deleteDamagePoint = damagePoints?.FirstOrDefault(x =>
                    (int)x[0] == deleteX && (int)x[1] == deleteY);

                // 从内存数据中删除
                damagePoints = damagePoints?.Where(x => !((int)x[0] == deleteX && (int)x[1] == deleteY))
                    .ToList();

                // 更新数据库 - 删除对应坐标的标注
                if (sqlImgInfo != null)
                {
                    using (var sqlHelper = new SQLHelper(Settings.Default.SqlPath))
                    {
                        // 根据坐标定位并删除数据库中的标注记录
                        await sqlHelper.DeleteDamageAnnotationByCoordinatesAsync(
                            sqlImgInfo.ImgId, deleteDamagePoint
                        );
                    }
                }

                // 更新result.json
                UpdateResultJson();
                MainWindow.MainVm.LoadProjectOverview();
                if (string.IsNullOrEmpty(OutImgPath))
                {
                    OutImgPath = this.ImgFullPath.InReplaceOutString();
                }

                boxSelecteObject = null;
                CloseAction?.Invoke();
            }
            else if (boxSelecteObject == null)
            {
                HandyControl.Controls.MessageBox.Warning("当前没有选中任何对象");
            }
            MainWindow.MainVm.LoadProjectOverview();
        }

        [RelayCommand]
        async void DeleteAll()
        {
            IsNotSave = true;
            var result = System.Windows.MessageBox.Show("确定删除吗 ??", "删除?", MessageBoxButton.YesNo);
            if (result == MessageBoxResult.Yes)
            {
                // 清除高亮状态
                currentHighlightedBox = null;

                // 清空UI集合
                BoxedStack.Clear();

                // 清空内存数据
                damagePoints = new List<float[]>();

                // 更新数据库 - 删除该图片的所有标注
                if (sqlImgInfo != null)
                {
                    using (var sqlHelper = new SQLHelper(Settings.Default.SqlPath))
                    {
                        // 根据图片ID删除所有标注
                        await sqlHelper.DeleteAllDamageAnnotationsByImgIdAsync(sqlImgInfo.ImgId);
                    }
                }

                // 更新result.json
                UpdateResultJson();

                if (string.IsNullOrEmpty(OutImgPath))
                {
                    OutImgPath = this.ImgFullPath.InReplaceOutString();
                }
                CloseAction?.Invoke();
            }
            MainWindow.MainVm.LoadProjectOverview();
        }

        // 新增辅助方法：更新result.json文件
        private void UpdateResultJson()
        {
            if (MainWindow.MainVm?.DamageDataList != null)
            {
                var data = MainWindow.MainVm.DamageDataList
                    .FirstOrDefault(x => Path.GetFileName(x.Url) == Path.GetFileName(ImgFullPath));
                if (data != null)
                {
                    data.DamagePoint = damagePoints?.ToArray() ?? Array.Empty<float[]>();
                }

                // 保存更新后的结果
                AboutJson.SaveJson<List<DamageData>>(
                    MainWindow.MainVm.DamageDataList,
                    Path.Combine(Settings.Default.InPath, MainWindow.MainVm.ImgFolderName),
                    "result.json"
                );
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
