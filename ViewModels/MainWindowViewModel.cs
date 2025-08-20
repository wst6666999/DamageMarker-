using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using DamageMaker.Automation;
using DamageMaker.Common;
using DamageMaker.DamageDataProcessing;
using DamageMaker.FileHandle;
using DamageMaker.GenerateReport;
using DamageMaker.ImageProcessing;
using DamageMaker.Message;
using DamageMaker.Models;
using DamageMaker.Properties;
using DamageMaker.SqliteServer;
using DamageMaker.ViewModels;
using DamageMaker.Views;
using DamageMarker.Models;
using DamageMarker.Views;
using HandyControl.Controls;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Formats.Asn1;
using System.Globalization;
using System.IO;
using System.Management;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Xceed.Words.NET;

using static DamageMaker.DamageDataProcessing.DataConversion;

using static DamageMaker.Models.Records;
using static DamageMarker.App;

using Application = System.Windows.Application;
using Brushes = System.Windows.Media.Brushes;
using File = System.IO.File;
using FormattedText = System.Windows.Media.FormattedText;
using MessageBox = HandyControl.Controls.MessageBox;
using Orientation = Xceed.Document.NET.Orientation;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;

namespace DamageMarker.ViewModels
{
    public partial class MainWindowViewModel : ObservableObject
    {
        //用于判定停止截图之后是否停止后续判定
        public bool isAnalyze = false;
        bool isHideNormal = true;

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

        [ObservableProperty]
        SolidColorBrush imgBorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(211, 211, 211));

        [ObservableProperty]
        string appTitle;

        bool isDistinctErrorBoxed = false;
        [ObservableProperty]
        private string version;
        [ObservableProperty]

        private string yoloPath = Settings.Default?.YoloLocation;

        [ObservableProperty]
        private bool isShowAllBoxSelected;

        [ObservableProperty]
        private SetupContent setup = new SetupContent();
        public bool IsModifyResultJson { get; set; } = false;
        Stopwatch stopwatch = new Stopwatch();

        [ObservableProperty]
        int allImgCount = 0;
        [ObservableProperty]
        int singleImgCount = 0;
        int currentImgIndex;//当前图片在damageDatalist中的索引
        [ObservableProperty]
        ImgInfo selectedThumbnailImg;
        [ObservableProperty]
        List<SqlImgInfo> sqlImgInfos = new List<SqlImgInfo>();

        [ObservableProperty]
        private List<DamageCategorySummaryTree> damageTree;

        public static event Action<string> DataAnalyzeFinished;

        public Sprite Cap; //截图窗口
        List<string> DamageImgPaths;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ShowAllBoxSelectedCommand))]
        int stepIndex = 0;

        [ObservableProperty]
        private System.Windows.Media.Brush background = Brushes.White;


        // 数据库中当前文件夹的ID
        int sqlFolderId;

        [ObservableProperty]
        Visibility progressVisibility = Visibility.Hidden;

        [ObservableProperty]
        private int currentProgress = 0;

        [ObservableProperty]
        private int totalProgress = 100;

        CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private string[]? imgFiles; //所有未判伤图片

        [ObservableProperty]
        private bool isFilterThumbnail = false;

        [ObservableProperty]
        private bool isEnableThumbnail;

        public List<DamageData>? DamageDataList; //json探伤数据序列化后
        public List<OcrData>? OcrDataList;
        private Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
        public static event Action<object, EventArgs>? ScreenShotPopuped;
        static readonly HttpClient client = new HttpClient();
        private bool isDataExist; //表明单个图片是否有对应的伤损数据

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ModifyImgCommand))]
        int selectedIndex;

        string ImgPath;
        [ObservableProperty]
        string imgFolderName; 
        [ObservableProperty]
        string url = "http://127.0.0.1:3333/endpoint";
        //停止的url
        string stopUrl = "http://127.0.0.1:3333/stop_processing";

        [ObservableProperty]
        string parameter; // 发送给后端的参数
        ObservableCollection<ImgInfo> thumbnailImgInfos = new(); //缩略图的信息
        public ObservableCollection<ImgInfo> ThumbnailImgInfos
        {
            get => thumbnailImgInfos;
            set => thumbnailImgInfos = value;
        }

        ObservableCollection<ImgInfo> originalThumbnailImgInfos = new(); //原始缩略图的信息

        [ObservableProperty]
        string? jsonData;

        public float[][]? damagePoints;

        [NotifyCanExecuteChangedFor(nameof(NoDamageCheckCommand), nameof(DamageCheckCommand))]

        [ObservableProperty]
        ImageSource? imgSource;



        ObservableCollection<MenuItem> menuItems = new ObservableCollection<MenuItem>();
        public ObservableCollection<MenuItem> MenuItems
        {
            get => menuItems;
            set => menuItems = value;
        }
        ObservableCollection<DamageDetails> detailsList = new ObservableCollection<DamageDetails>(); //单个图片伤损信息列表
        public ObservableCollection<DamageDetails> DetailsList
        {
            get => detailsList;
            set => detailsList = value;
        }
        public static ScreenshotInfo? NeedSavedInfo { get; set; } = null;
        string damageImgFolderName;

        public MainWindowViewModel()
        {
            Version = "V0.82";
            AppTitle = "探伤仪检测数据智能分析系统";
            SelectedIndex = -1;
            //ScreenshotOffset = DamageMaker.Properties.Settings.Default.ScreenshotOffset;
            //ScreenshotInterval = DamageMaker.Properties.Settings.Default.ScreenshotInterval;
            //MouseMovePixel = DamageMaker.Properties.Settings.Default.MouseMovePixel;

            client.Timeout = TimeSpan.FromSeconds(3600);

            DamageFoldersListViewModel.OpenedDamageFolder += SwitchDamageFolder;
            DamageFoldersListViewModel.OpenedDamageFolder += ShowThumbnails;
            SetupContentViewModel.RulesCountChange += InitCategorySummary;


            foreach (var AppPath in DamageMaker.Properties.Settings.Default.AppLocation)
            {
                MenuItems.Add(
                    new MenuItem
                    {
                        Header = Path.GetFileName(AppPath),
                        CommandParameter = AppPath,
                        Command = ProcessStartCommand
                    }
                );
            }   
            MenuItems.Add(new MenuItem
            {
                Header = "✚ 添加回放软件",
                CommandParameter = null,
                Command = SelectExeFileCommand
            });
            CheckAppStatusPeriodically();
            PlaybackWindow.ScreenshotStart += OnScreenShotStart;
            PlaybackWindow.ScreenshotFinished += OnScreenShotFinished;
            // 注册消息监听
            WeakReferenceMessenger.Default.Register<TrackShieldingMessage>(this, (_, msg) =>
            {
                _activeBeforeShield = msg.BeforeCount;
                _activeAfterShield = msg.AfterCount;
                ApplyShieldingToImages();
            });

            WeakReferenceMessenger.Default.Register<ConcealFishScaleMessage>(this, (_, msg) =>
            {
                Settings.Default.IsConcealFishScale = msg.IsConcealFishScale;
                ApplyConcealFishScale();
            });
        }
        private async void ApplyConcealFishScale()
        {
            await SaveAllBoxSelectedImg(DamageImgPaths, damageImgFolderName);
        }

        private int _activeBeforeShield;
        private int _activeAfterShield;

        private void ApplyShieldingToImages()
        {
            // 如果缩略图列表为空或没有图片，直接返回
            if (ThumbnailImgInfos == null || ThumbnailImgInfos.Count == 0)
                return;

            // 首先重置所有图片的测试轨标记
            foreach (var img in ThumbnailImgInfos)
            {
                img.IsTestTrack = false;
                img.TestTrackType = "";
            }

            // 处理前屏蔽（标记前N张为"Before"测试轨）
            int 前屏蔽数量 = Math.Min(_activeBeforeShield, ThumbnailImgInfos.Count);
            for (int i = 0; i < 前屏蔽数量; i++)
            {
                ThumbnailImgInfos[i].IsTestTrack = true;
                ThumbnailImgInfos[i].TestTrackType = "Before";
            }

            // 处理后屏蔽（标记后M张为"After"测试轨）
            int 后屏蔽数量 = Math.Min(_activeAfterShield, ThumbnailImgInfos.Count);
            int 后屏蔽起始索引 = Math.Max(0, ThumbnailImgInfos.Count - 后屏蔽数量);
            for (int i = 后屏蔽起始索引; i < ThumbnailImgInfos.Count; i++)
            {
                ThumbnailImgInfos[i].IsTestTrack = true;
                ThumbnailImgInfos[i].TestTrackType = "After";
            }

            // 刷新UI显示
            InitCategorySummary();  // 更新分类统计
            UpdataThumbnail();     // 更新缩略图显示
        }

        [RelayCommand]
        void ScreenShotFunc()
        {
            ScreenShotPopuped?.Invoke(this, EventArgs.Empty);
            Cap = Sprite.Show(new Capturing());
        }

        public async Task UpdateImgSource(string imgPath)
        {
            await Task.Run(() =>
            {
                var bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad; // 确保图像数据加载到内存中
                bitmapImage.UriSource = new Uri(imgPath, UriKind.Relative);
                bitmapImage.EndInit();

                // 在 UI 线程上更新 ImgSource
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ImgSource = BitmapFrame.Create(bitmapImage);
                    //ImgSo   urce =bitmapImage;
                });
            });
        }

        bool? isConfirmDamage = null;

        [RelayCommand]
        void ImgSelectionChanged()
        {
            if (SelectedIndex != -1)
            {
                DetailsList.Clear();
                ImgPath = ThumbnailImgInfos[SelectedIndex].Path;

                isConfirmDamage = SqlImgInfos.Where(x => Path.GetFileName(x.ImgPath) == Path.GetFileName(ImgPath)).FirstOrDefault()?.IsConfirmDamage;
                if (isConfirmDamage == true)
                {
                    HasDamageImg = true;
                    HasNoDamageImg = false;
                }
                else if (isConfirmDamage == false)
                {
                    HasNoDamageImg = true;
                    HasDamageImg = false;
                }
                else
                {
                    HasNoDamageImg = false;
                    HasDamageImg = false;
                }

                // DamageRemark = SqlImgInfos.Where(x => Path.GetFileName(x.ImgPath) == Path.GetFileName(ImgPath)).FirstOrDefault()?.Remark;

                UpdateImgSource(ImgPath);

                SearchMileageText = Path.GetFileNameWithoutExtension(ImgPath).Split('_')[0].Trim();
                (damagePoints, currentImgIndex) = GetDamagePointsAndIndex(ImgPath);
                isDataExist = damagePoints == null ? false : true;
                ModifyImgCommand.NotifyCanExecuteChanged();
                //  ShowBoxSelectedCommand.NotifyCanExecuteChanged();
                if (isDataExist)
                {
                    InitDamageDetails(damagePoints);
                }
            }
            else
            {
                damagePoints = null;
                ImgPath = null;
                ImgSource = null;
            }
        }

        private void UpdateIsConfirmDamage(string imgPath, bool? isConfirmDamage)
        {
            var target = SqlImgInfos.FirstOrDefault(info => Path.GetFileName(info.ImgPath) == Path.GetFileName(imgPath));
            if (target != null)
            {
                if (target.IsConfirmDamage != isConfirmDamage)
                {
                    using (var sqlHelper = new SQLHelper(Settings.Default.SqlPath))
                    {
                        int rowsAffected = sqlHelper.UpdateImageIsConfirmDamage(target.ImgId, isConfirmDamage);
                        if (rowsAffected > 0)
                        {
                            target.IsConfirmDamage = isConfirmDamage;
                        }
                    }
                }
            }
            else
            {
                Console.WriteLine($"未找到路径为 {imgPath} 的图片信息。");
            }
        }

        DamageFoldersList foldersListWindow;


        #region 打开历史数据后弹出的窗口
        [RelayCommand]
        async Task SelectedPath() //选择文件夹
        {

            foldersListWindow = new DamageFoldersList();
            foldersListWindow.Owner = Application.Current.MainWindow;
            foldersListWindow.Show();
        }




        /// <summary>
        /// 切换伤损文件夹
        /// </summary>
        /// <param name="FolderPath">需要切换伤损文件夹的路径</param>
        private void SwitchDamageFolder(string FolderPath)
        {
            NeedSavedInfo = null;
            ClearImgAndData();
            if (IsModifyResultJson)
            {
                AboutJson.SaveJson<List<DamageData>>(DamageDataList, Path.Combine(Settings.Default.InPath, ImgFolderName), "result.json");
            }
            IsModifyResultJson = false;
            IsShowAllBoxSelected = false;
            //NeedSavedInfo = AboutJson.DeserializeJson<ScreenshotInfo>(Path.Combine(FolderPath, "info.json"));
        }


        #endregion

        [RelayCommand]
        void FilterImg() //点击数据筛选按钮
        {
            IsShowAllBoxSelected = false;
            if (DamageImgPaths == null)
            {
                InitializeThumbnailImgInfos();
            }
            else
            {
                UpdataThumbnail();
            }
        }

        public async void ShowThumbnails(string SelectedPath)
        {
            ImgBorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(211, 211, 211));
            Parameter = SelectedPath;
            var mainData = new MainData(SelectedPath);
            OcrDataList = mainData.OcrDataList;

            ImgFolderName = mainData.ImgFolderName;
            imgFiles = mainData.ImgPaths;
            damageImgFolderName = SelectedPath.InReplaceOutString();
            DamageDataList = mainData.DamageDataList;
            NeedSavedInfo = mainData.NeedSavedInfo;




            if (NeedSavedInfo != null)
            {
                Console.WriteLine(Settings.Default.SqlPath);
                using (var sqlHelper = new SQLHelper(Settings.Default.SqlPath))
                {
                    // 获取 FolderId
                    sqlFolderId = sqlHelper.GetFolderIdByPath(SelectedPath);

                    // 如果数据库中没有该文件夹的数据，先插入文件夹信息
                    if (sqlFolderId < 0)
                    {
                        sqlFolderId = DataAccess.InsertFolderInfo(sqlHelper, SelectedPath, NeedSavedInfo);
                    }
                    if (sqlFolderId < 0)//插入失败的原因
                    {
                        Console.WriteLine(sqlHelper.GetLastError());
                    }
                    else
                    {
                        var para = new Dictionary<string, object>
                    {
                        { "@FolderId", sqlFolderId }
                    };
                        var SqlimgCount = sqlHelper.GetCount("Images", para);
                        if (SqlimgCount == 0)
                        {
                            foreach (var file in imgFiles)
                            {
                                SqlImgInfos.Add(new SqlImgInfo
                                {
                                    ImgPath = file,
                                    FolderId = sqlFolderId
                                });
                            }
                            SaveSqlImgInfos(SqlImgInfos);
                            SqlImgInfos = sqlHelper.GetImagesByFolderId(sqlFolderId);

                        }
                        else if (SqlimgCount > 0)
                        {
                            SqlImgInfos = sqlHelper.GetImagesByFolderId(sqlFolderId);
                        }
                        else if (SqlimgCount < 0)
                        {
                            Console.WriteLine($"{sqlHelper.GetLastError()}");
                        }
                    }
                }

            }
            else

            {
              
                MessageBox.Warning("这个文件夹没有对应的info.json文件,请输入数据后重新打开该文件夹");
            }

            if (!Directory.Exists(damageImgFolderName))
            {
                Directory.CreateDirectory(damageImgFolderName);
            }

            string jsonFilePath = Path.Combine(Parameter, "result.json"); //寻找json文件的路径
            if (DamageDataList != null)
            {
                DamageDataList = mainData.ProcessData();

                Console.WriteLine("-----------后端未处理数据详情-----------" + Environment.NewLine);
                var damageCount = ObtainInfo.GetCategoryAndCount(DamageDataList);
                damageCount.ForEach(x =>
                {
                    Console.WriteLine($"{DataConversion.DamageIdToDamageName(x.Value)} id为{x.Value} 共有{x.Key}处伤损");
                });
                Console.WriteLine("----------------------------------" + Environment.NewLine);



                DamageImgPaths = DamageDataList.Select(x => x.Url).ToList();
                DamageImgPaths = DamageImgPaths.OrderBy(File.GetCreationTime).ToList();
                AllImgCount = imgFiles.Length;
                SingleImgCount = DamageImgPaths.Count;
                

                ModifyImgCommand.NotifyCanExecuteChanged();
                InitCategorySummary();
                IsEnableThumbnail = true;
                ExportReportCommand.NotifyCanExecuteChanged();
                GetJsonResultCommand.NotifyCanExecuteChanged();
                ThumbnailImgInfos.Clear();



                await UpdataThumbnail();
                await SaveAllBoxSelectedImg(DamageImgPaths, damageImgFolderName);

                StepIndex = 2;

                //自动聚焦到第一张图片
                if (ThumbnailImgInfos.Count > 0)
                {
                    SelectedIndex = 0;
                    ImgSelectionChanged();


                    if (CurrentProgress >= TotalProgress)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show("数据已完成分析!", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                        });
                    }

                }

            }
            else
            {
                ClearDamagePage();
               
                MessageBox.Warning("这个文件夹没有对应的损伤数据,请请求数据后重新打开该文件夹");
            }

        }

        private void SaveSqlImgInfos(List<SqlImgInfo> infos)
        {
            using (var sqlHelper = new SQLHelper(Settings.Default.SqlPath))
            {
                sqlHelper.EnsureConnectionOpen();

                var p = new Dictionary<string, object>
                    {
                        { "@FolderId", sqlFolderId }
                    };
                var SqlimgCount = sqlHelper.GetCount("Images", p);
                if (SqlimgCount == 0)
                {

                    using (var transaction = sqlHelper.Connection.BeginTransaction())
                    {
                        try
                        {
                            foreach (SqlImgInfo info in infos)
                            {
                                var para = new Dictionary<string, object>
                    {

                        { "@FolderId", info.FolderId },
                        { "@ImgPath", info.ImgPath },
                        { "@IsConfirmDamage", info.IsConfirmDamage },
                        { "@Remark", info.Remark },
                        { "@ImageData",info.ImageData },
                    };
                                var result = sqlHelper.InsertTableWithTransaction(para, "Images", transaction);
                                if (result < 0)
                                {
                                    Console.WriteLine($"插入图片信息失败: {sqlHelper.GetLastError()}");
                                }

                            }
                            transaction.Commit(); // 提交事务
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback(); // 回滚事务
                            Console.WriteLine($"批量插入失败: {ex.Message}");
                        }
                    }

                }
                else if (SqlimgCount > 0)
                {



                }
            }
        }


        private void ClearDamagePage()
        {
            AllImgCount = 0;
            SingleImgCount = 0;
            ThumbnailImgInfos.Clear();
            ImgSource = null;
            StepIndex = 1;
            IsEnableThumbnail = false;
            DamageDataList = null;
            GetJsonResultCommand.NotifyCanExecuteChanged();
        }


        [ObservableProperty]
        private Visibility imgProgressVisibility = Visibility.Collapsed;

        [ObservableProperty]
        private int _currentImgProgress;

        [ObservableProperty]
        private int _totalImgProgress;
        /// <summary>
        /// 
        /// </summary>
        /// <param name="Imgfiles"></param>
        /// <param name="SavePath"></param>
        /// <returns></returns>
        async Task SaveAllBoxSelectedImg(List<string> Imgfiles, [NotNull] string SavePath)
        {
            int saveImgCount = 0;
            Directory.CreateDirectory(SavePath);
            bool hideFishScale = Settings.Default.IsConcealFishScale;

            var renderResults = new List<(string FilePath, BitmapFrame Frame)>();
            // 先过滤出需要处理的文件
            var filesToProcess = Imgfiles
                //.Where(imgFile => File.Exists(imgFile))
                //.Where(imgFile => !File.Exists(Path.Combine(SavePath, Path.GetFileName(imgFile))))
                .ToList();
            //首先初始化进度
            // Initialize progress
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                ImgProgressVisibility = Visibility.Visible;
                CurrentImgProgress = 0;
                TotalImgProgress = filesToProcess.Count;
            });

            // 分批次处理，每批处理N个文件后允许UI响应
            int batchSize = 5; // 根据实际情况调整
            for (int i = 0; i < filesToProcess.Count; i += batchSize)
            {
                var batch = filesToProcess.Skip(i).Take(batchSize);

                await Application.Current.Dispatcher.Invoke(() =>
                {
                    foreach (var imgFile in batch)
                    {
                        string fileName = Path.GetFileName(imgFile);
                        string fileNamePath = Path.Combine(SavePath, fileName);

                        var frame = ProcessImage(imgFile, hideFishScale);
                        frame.Freeze();
                        renderResults.Add((fileNamePath, frame));
                    }

                    return Task.CompletedTask;
                });
                // Update progress
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    CurrentImgProgress = Math.Min(i + batchSize, filesToProcess.Count);
                });
                // 允许UI处理消息
                await Task.Delay(1);
            }

            // 并行保存（不涉及 UI 操作）
            await Task.Run(() =>
            {
                Parallel.ForEach(renderResults, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, item =>
                {
                    try
                    {
                        SaveBitmapToPng(item.FilePath, item.Frame);
                        Interlocked.Increment(ref saveImgCount);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"保存图片 {item.FilePath} 时出错: {ex}");
                    }
                });
            });
            // Hide progress when done
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                ImgProgressVisibility = Visibility.Collapsed;
            });
            Console.WriteLine($"图片伤损绘制{saveImgCount}张完成");
        }
        // 提取图片处理方法，可在多线程中调用
        private BitmapFrame ProcessImage(string imgFile, bool hideFishScale)
        {
            // 1. 加载图片（非 UI 操作，但 BitmapFrame.Create 需要 STA）
            BitmapFrame sourceImage = BitmapFrame.Create(new Uri(imgFile));
            if (sourceImage.Width <= 0 || sourceImage.Height <= 0)
                throw new InvalidOperationException($"图片 {imgFile} 尺寸无效");

            // 2. 创建并配置 DamageDisplayBox（必须在 UI 线程）
            var damageDisplay = new DamageDisplayBox();
            var damageData = GetDamagePointsAndIndex(imgFile); // 确保这是线程安全的

            damageDisplay.SourceImage = sourceImage;
            damageDisplay.DamagePoints = damageData.Item1;
            damageDisplay.ShowGuidelines = false;
            damageDisplay.HideFishScale = hideFishScale;

            // 3. 测量和布局（必须在 UI 线程）
            damageDisplay.Measure(new System.Windows.Size(sourceImage.Width, sourceImage.Height));
            damageDisplay.Arrange(new Rect(0, 0, sourceImage.Width, sourceImage.Height));
            damageDisplay.UpdateLayout();

            if (damageDisplay.ActualWidth <= 0 || damageDisplay.ActualHeight <= 0)
                throw new InvalidOperationException("控件尺寸无效");

            // 4. 渲染（必须在 UI 线程）
            var renderTarget = new RenderTargetBitmap(
                (int)damageDisplay.ActualWidth,
                (int)damageDisplay.ActualHeight,
                96, 96, PixelFormats.Pbgra32);
            renderTarget.Render(damageDisplay);

            return BitmapFrame.Create(renderTarget);
        }


        public async Task SaveSingleOverviewImage(string imgFile, string savePath)
        {
            if (string.IsNullOrWhiteSpace(imgFile) || !File.Exists(imgFile))
            {
                Console.WriteLine("图片路径无效或文件不存在。");
                return;
            }

            // 创建 Overview 子目录
            string overviewFolder = Path.Combine(savePath, "Overview");
            Directory.CreateDirectory(overviewFolder);

            bool hideFishScale = Settings.Default.IsConcealFishScale;

            // UI线程执行渲染
            var frame = await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var processed = ProcessImage(imgFile, hideFishScale);
                processed.Freeze();
                return processed;
            });

            // 构造保存路径（保持原文件名）
            string fileName = Path.GetFileNameWithoutExtension(imgFile) + "_overview.png";
            string saveFilePath = Path.Combine(overviewFolder, fileName);

            // 后台线程保存图片
            await Task.Run(() =>
            {
                try
                {
                    SaveBitmapToPng(saveFilePath, frame);
                    
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"保存 Overview 图像失败: {ex}");
                }
            });
        }



        #region
        [ObservableProperty]
        bool isHideNormalMarker;

        bool CanHideNormalMarker() => damagePoints != null;
        [RelayCommand(CanExecute = nameof(CanHideNormalMarker))]

        async void HideNormalMarker()
        {
            Settings.Default.IsHideNormalMarker = IsHideNormalMarker;
            Console.WriteLine("隐藏正常标记:" + IsHideNormalMarker);

            //显示加载状态
            ImgProgressVisibility=Visibility.Visible;
            CurrentImgProgress = 0;
            TotalImgProgress = DamageImgPaths.Count;

            await SaveAllBoxSelectedImg(DamageImgPaths, damageImgFolderName);
            await UpdataThumbnail();
            ImgProgressVisibility = Visibility.Collapsed;

            if (ThumbnailImgInfos.Count > 0)
            {
                SelectedIndex = 0;
                ImgSelectionChanged();
            }
        }

        private void InitDamageDetails([NotNull] float[][] damagePoints)
        {
            for (int i = 0; i < damagePoints.GetLength(0); i++)
            {
                if (IsHideNormalMarker == true)
                {
                    if (DamageIdToBrush(damagePoints[i][4]) != Brushes.Green)
                    {
                        DetailsList.Add(
                            new DamageDetails
                            {
                                Id = i,
                                MarkerColor = DamageIdToBrush(damagePoints[i][4]),
                                DamageCategory = DamageIdToDamageName(damagePoints[i][4]) + $" {i} ",
                                Similarity = (damagePoints[i][5] * 100).ToString("0.0") + "%",
                            }
                        );
                    }
                }
                else
                {
                    DetailsList.Add(
                            new DamageDetails
                            {
                                Id = i,
                                MarkerColor = DamageIdToBrush(damagePoints[i][4]),
                                DamageCategory = DamageIdToDamageName(damagePoints[i][4]) + $" {i} ",
                                Similarity = (damagePoints[i][5] * 100).ToString("0.0") + "%",
                            }
                        );
                }
            }
        }
        #endregion
        private async Task InitializeThumbnailImgInfos()
        {

            var task = Task.Run(() =>
            {
                return Directory.GetFiles(Parameter, "*.png")
                    .OrderBy(File.GetCreationTime)
                    .ToArray();
            });
            imgFiles = await task.ContinueWith(
                t => t.Result,
                TaskScheduler.FromCurrentSynchronizationContext()
            );

            //if (isHideNormal)
            //{
            //    DamageImgPaths = FilterNormalDamage(DamageDataList).Select(x => x.Url).ToList();
            //}
            //else
            //{
            DamageImgPaths = DamageDataList.Select(x => x.Url).ToList();
            //}
            //DamageImgPaths = FilterNormalDamage(DamageDataList).Select(x => x.Url).ToList();
            DamageImgPaths = DamageImgPaths.OrderBy(File.GetCreationTime).ToList();
            AllImgCount = imgFiles.Length;
            SingleImgCount = DamageImgPaths.Count;
            UpdataThumbnail();
            StepIndex = 2;
        }

        private async Task UpdataThumbnail()
        {
            string[] ThumbnailFiles;
            if (IsFilterThumbnail)
            {
                ThumbnailFiles = DamageImgPaths.ToArray();
            }
            else
            {
                ThumbnailFiles = imgFiles;
            }

            ThumbnailImgInfos.Clear();
            await dispatcher.InvokeAsync(
               () =>
               {
                   foreach (var file in ThumbnailFiles)
                   {
                       ThumbnailImgInfos.Add(
                           new ImgInfo { Path = file, Name = Path.GetFileName(file) }
                       );
                   }
                   Growl.Info("缩略图初始化完成");
               },
               DispatcherPriority.Background
           );
            ShowAllBoxSelectedCommand.NotifyCanExecuteChanged();
            IsShowAllBoxSelected = true;
            ShowAllBoxSelected();
        }

        #region 发送请求获取result.json文件

        /// <summary>
        /// 
        /// </summary>
        /// <returns>分析所消耗时间</returns>
        [RelayCommand(CanExecute = nameof(IsGetJsonResult))]
        public async Task GetJsonResult()
        {
            if (await IsPortInUse(3333))
            {
                   stopwatch.Restart();

                //删除out里面缓存文件
                if (Directory.Exists(Parameter.InReplaceOutString()))
                {
                    var jsonPath = Path.Combine(Parameter, "result.json");
                  if ( File.Exists(jsonPath)) 
                        File.Delete(jsonPath);

                    var files = Directory.GetFiles(Parameter.InReplaceOutString());


                    if (files.Length > 0)
                    {
                        Directory.Delete(Parameter.InReplaceOutString(), true);
                    }
                }

                ClearDamagePage();
                
                GetProgress(Url);
                string Result = await GetJsonFile(Url, Parameter, Parameter);
                stopwatch.Stop();

                Growl.InfoGlobal(Result);
                string Elapsed = $"{stopwatch.Elapsed.Minutes}分{stopwatch.Elapsed.Seconds}秒";
                Console.WriteLine(Elapsed);
                DataAnalyzeFinished?.Invoke(Elapsed);
                ShowThumbnails(Parameter);
               
                // 重新加载缩略图
                await InitializeThumbnailImgInfos();
                //重新加载项目总览
                InitCategorySummary();
            }
            else
            {
                MessageBox.Warning("当前还未启动探伤后台,请启动探伤后台后重试");
               
            }
        }

        public static string railClass="single";
        /// <summary>
        /// 执行方法后在wpath处生成result.json文件
        /// </summary>
        /// <param name="url">发送请求地址</param>
        /// <param name="rpath">读取文件夹的位置</param>
        /// <param name="wpath">写入result.json文件的位置</param>
        /// <returns>保存result的路径</returns>
        async Task<string> GetJsonFile(string url, string rpath, string wpath)
        {
            var curInstruments = MainWindowViewModel.NeedSavedInfo?.RailWayInfo.Instruments;
            if (curInstruments == "8C" || curInstruments == "8D" || curInstruments == "19型" || curInstruments == "gt-20" || curInstruments == "6M")
                //更改为单轨的模型
                railClass = "single";
            else if (curInstruments == "双轨501" || curInstruments == "双轨502")
                //更改为双轨的模型
                railClass = "double";
           
                var content = new FormUrlEncodedContent(
                    new[]
                    {
                    new KeyValuePair<string, string>("rpath", rpath),
                    new KeyValuePair<string, string>("wpath", wpath),
                    new KeyValuePair<string, string>("rail_class", railClass),
                    }
                );
            try
            {
                HttpResponseMessage response = await client.PostAsync(url, content);
                response.EnsureSuccessStatusCode();
                var progressTask = GetProgress(url);
                
                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Error(ex.Message);
                throw;
            }
            finally
            {
                ProgressVisibility = Visibility.Hidden;
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource.Dispose();
            }
        }

        private async Task GetProgress(string url)
        {
            TotalProgress = 100;
            _cancellationTokenSource = new CancellationTokenSource();
            ProgressVisibility = Visibility.Visible;

            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    var content = new FormUrlEncodedContent(new[]
                        {
                             new KeyValuePair<string, string>("get_progress", "true"),
                         });

                    HttpResponseMessage response = await client.PostAsync(url, content);
                    response.EnsureSuccessStatusCode();
                    var progressData = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<ProgressData>(progressData);
                    if (string.IsNullOrEmpty(result.progress))
                    {
                        Console.WriteLine("Progress data is null or empty.");
                        continue;
                    }

                    var ProgressArr = result.progress.Split("/");
                    dispatcher.Invoke(() =>
                    {
                        CurrentProgress = int.Parse(ProgressArr[0]);
                        TotalProgress = int.Parse(ProgressArr[1]);
                        
                    });
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("Request canceled.");
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred: {ex.Message}");
                }
                await Task.Delay(500, _cancellationTokenSource.Token);
            }
        }

        private int _isStopping = 0; // 防止重复请求
        [RelayCommand]
        private async Task StopProgress()
        {
            if (Interlocked.CompareExchange(ref _isStopping, 1, 0) != 0)
                return;
            try
            {

                // 立即触发本地取消令牌
                if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
                {
                    _cancellationTokenSource.Cancel();
                    Console.WriteLine("本地操作已取消");
                }
                try
                {
                    var content = new FormUrlEncodedContent(new[]
                    {
                     new KeyValuePair<string, string>("stop_processing", "true"),
                     new KeyValuePair<string, string>("rpath", Parameter),
                     new KeyValuePair<string, string>("wpath", Parameter),
                     new KeyValuePair<string, string>("rail_class", railClass),
                 });

                    using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                    HttpResponseMessage response = await client.PostAsync(stopUrl, content, timeoutCts.Token);
                    response.EnsureSuccessStatusCode();
                    if (response.IsSuccessStatusCode)
                    {
                        var result = await response.Content.ReadFromJsonAsync<JsonNode>();
                        Console.WriteLine($"已停止！已处理图片数: {result?["processed"]}");
                    }
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("服务端停止请求超时");
                    //await RetryStopRequest(url, content);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"通知服务端停止时出错: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"停止处理时发生意外错误: {ex.Message}");
            }
            finally
            {
                _isStopping = 0;
            }
        }
        #endregion


        #region 展示详细图片的代码
        private (float[][]?, int) GetDamagePointsAndIndex(string ImgPath)
        {
            var DamageData = DamageDataList
                .Where(x => Path.GetFileName(x.Url) == Path.GetFileName(ImgPath)).FirstOrDefault();

            var result = DamageData?.DamagePoint;
            var index = DamageDataList.IndexOf(DamageData);
            return (result, index);
        }


        bool CanShowAllBoxSelected => ThumbnailImgInfos != null && ThumbnailImgInfos.Count != 0;
        [RelayCommand(CanExecute = nameof(CanShowAllBoxSelected))]
        public void ShowAllBoxSelected( string str="")
        {
            if(str=="键盘触发")
                IsShowAllBoxSelected =! IsShowAllBoxSelected;
           
            if (IsFilterThumbnail)
            {

                if (IsShowAllBoxSelected)
                {
                    foreach (var ThumbnailImgInfo in ThumbnailImgInfos)
                    {
                        ThumbnailImgInfo.Path = ThumbnailImgInfo.Path.InReplaceOutString();
                    };
                }
                else
                {
                    foreach (var ThumbnailImgInfo in ThumbnailImgInfos)
                    {
                        ThumbnailImgInfo.Path = ThumbnailImgInfo.Path.OutReplaceInString();
                    };

                }
            }
            else
            {
                foreach (var ThumbnailImgInfo in ThumbnailImgInfos)
                {
                    if (
                        DamageImgPaths.Any(x =>
                            Path.GetFileName(x) == Path.GetFileName(ThumbnailImgInfo.Path)
                        )
                    )
                    {

                        if (IsShowAllBoxSelected)
                            ThumbnailImgInfo.Path = ThumbnailImgInfo.Path.InReplaceOutString();
                        else
                            ThumbnailImgInfo.Path = ThumbnailImgInfo.Path.OutReplaceInString();
                    }
                }
                ;
            }


            if (SelectedIndex != -1)
            {
                ImgPath = ThumbnailImgInfos[SelectedIndex].Path;
                UpdateImgSource(ImgPath);
            }
            StepIndex = 3;

        }

        [RelayCommand]
        void ProcessStart(string exePath)
        {
            Utilities.StartProcess(exePath);
            Growl.InfoGlobal("应用程序已成功启动!");
            ScreenShotFunc();
        }


        void OnStartedProcessExit(object? sender, EventArgs e)
        {
            if (Cap != null)
            {
                dispatcher.Invoke(() =>
                {
                    Cap.Close();
                });
            }
        }


        private bool IsDataExists() => isDataExist;
        private bool IsSelectedImg() => SelectedIndex != -1;

        bool IsGetJsonResult() => (Parameter != null) ? true : false;

        bool IsDamageDataListNotEmpty() => DamageDataList == null ? false : true;


        #endregion


        #region 导出报告代码
        [ObservableProperty]
        bool isImporting;

        DocX document;

        [RelayCommand(CanExecute = nameof(IsDamageDataListNotEmpty))]
        public async Task ExportReportAsync()
        {
            try
            {
                IsImporting = true;

                string docxsPath = Settings.Default.DocxPath;
                string fileName = ImgFolderName + "钢轨探伤检测报告.docx";
                string saveFilePath = Path.Combine(docxsPath, fileName);

                await Task.Run(() =>
                {
                    // 若已存在同名文件，则删除以避免 SaveAs 报错
                    if (File.Exists(saveFilePath))
                    {
                        File.Delete(saveFilePath);
                    }

                    var doc = new ExportWord(Parameter);
                    doc.GenerateWord(saveFilePath); // 自动保存到新文件
                });

                await App.Current.Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Success($"{ImgFolderName} 钢轨探伤检测报告已保存在：{saveFilePath}（已覆盖旧文件）");
                });
            }
            catch (Exception ex)
            {
                await App.Current.Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Error("导出失败：" + ex.Message);
                });
            }
            finally
            {
                IsImporting = false;
            }
        }










        #endregion
        #region 判断判伤后台是否在线
        [ObservableProperty]
        Visibility stopPythonVisible;

        [ObservableProperty]
        SolidColorBrush portStatusColor;

        [ObservableProperty]
        string portStatusText;

        async Task CheckAppStatusPeriodically()
        {
            while (true)
            {
                //异步检查两个程序是否正在运行
                IsDetectionAsynEnable =
                    (Is6MRunning = await JGT_6M.IsRunning()) ||
                    (Is8CRunning = await JGT_8C.IsRunning());

                if (IsDetectionAsynEnable == false)
                {
                    IsDetectionAsynChecked = false;
                }

                if (await IsPortInUse(3333))
                {
                    dispatcher.Invoke(() =>
                    {
                        PortStatusColor = Brushes.Green;
                        PortStatusText = "判伤后台正在运行";
                        StartPythonVisible = Visibility.Hidden;
                        StopPythonVisible = Visibility.Visible;
                    });
                }
                else
                {
                    dispatcher.Invoke(() =>
                    {
                        PortStatusColor = Brushes.Red;
                        PortStatusText = "判伤后台未在运行";
                        StartPythonVisible = Visibility.Visible;
                        StopPythonVisible = Visibility.Hidden;
                    });
                }
                await Task.Delay(1300);
            }
        }

        Task<bool> IsPortInUse(int portNumber)
        {
            return Task.Run(() =>
              {
                  IPGlobalProperties ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
                  var tcpListeners = ipGlobalProperties.GetActiveTcpListeners();
                  return tcpListeners.Select(x => x.Port).Contains(portNumber);


              });
        }
        #endregion
        [ObservableProperty]
        Visibility startPythonVisible;

        [ObservableProperty]
        bool isStartPython;

        [RelayCommand]
        async Task StartPythonScript()
        {
#if WINDOWS
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "python", // 这里使用 "python" 表示使用系统环境变量中的 Python 解释器
                Arguments = YoloPath, // 传递 Python 脚本的路径作为参数
                WorkingDirectory = Path.GetDirectoryName(YoloPath),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using (Process process = new Process { StartInfo = startInfo })
            {
                process.Start();
                //var t1 = ReadStreamAsync(process.StandardOutput);

                await Task.Delay(10000);
                //  await process.WaitForExitAsync();


            }
#elif Linux

#endif



        }

        [RelayCommand]

        async Task StopPythonScript()
        {
            IsStartPython = false;
            await Task.Run(() =>
            {
                Process[] processes = Process.GetProcessesByName("python");
                foreach (var process in processes)
                {
                    process.Kill();
                }
            });
        }


        
        #region 伤损总结


        List<Details> GetCategorySummary(float CategoryIndex, List<DamageData> damageDataListPara, bool IsSortBySimilarity = false)
        {
            Console.WriteLine("IsSortBySimilarity:" + IsSortBySimilarity);
            var result = new List<Details>();

            // 处理超速（类别48）的特殊逻辑
            if (CategoryIndex == 48)
            {
                // 从OCR数据中提取超速记录
                if (OcrDataList != null)
                {
                    foreach (var ocr in OcrDataList)
                    {
                        if (!string.IsNullOrWhiteSpace(ocr.speedvalue))
                        {
                            var speeds = ocr.speedvalue.Split(',');
                            foreach (var speedStr in speeds)
                            {
                                if (float.TryParse(speedStr, out float speed) && speed > 3f)
                                {
                                    result.Add(new Details()
                                    {
                                        FileName = Path.GetFileName(ocr.ImgFullPath), // OCR关联的图片
                                        Count = 1,
                                        weight = speed // 用速度值作为权重（可选）
                                    });
                                }
                            }
                        }
                    }
                }
                return result;
            }

            // 处理普通伤损类别（非48）
            foreach (DamageData damageData in damageDataListPara)
            {
                Details tempDetails = new();
                int count = 0;
                List<float> weights = new();

                for (int i = 0; i < damageData.DamagePoint.GetLength(0); i++)
                {
                    if (damageData.DamagePoint[i][4] == CategoryIndex)
                    {
                        if (IsSortBySimilarity)
                        {
                            weights.Add(damageData.DamagePoint[i][5]);
                        }
                        count++;
                        tempDetails = new Details()
                        {
                            FileName = Path.GetFileName(damageData.Url),
                            Count = count
                        };
                    }

                    if (i == damageData.DamagePoint.GetLength(0) - 1 && tempDetails.FileName != null)
                    {
                        if (IsSortBySimilarity)
                        {
                            tempDetails.weight = weights.Count > 0 ? weights.Max() : 0;
                        }
                        result.Add(tempDetails);
                    }
                }
            }
            return result;


        }
        [RelayCommand]
        void LocationImg(string Fpath)
        {
          
            var item = ThumbnailImgInfos.Where(x => x.Name == Fpath).FirstOrDefault();
            if (item != null)
            {
                SelectedThumbnailImg = item;
            }
        }


        public void LoadProjectOverview()
        {
            InitCategorySummary();
        }
        //private void InitCategorySummary(int DisplayedRulesCount=10)
        //{

        //    // 正确做法：统一数据源
        //    var testTrackFiles = DamageDataList
        //        .Select(data => Path.GetFileName(data.Url.Split('?')[0]))
        //        .Intersect(ThumbnailImgInfos
        //            .Where(img => img.IsTestTrack)
        //            .Select(img => Path.GetFileName(img.Path.Split('?')[0]))
        //        .ToHashSet());

        //    // 正确过滤非测试轨数据
        //    var nonTestTrackDatas = DamageDataList
        //        .Where(data => !testTrackFiles.Contains(Path.GetFileName(data.Url.Split('?')[0])))
        //        .ToList();

        //    // 4. 使用过滤后的数据进行后续处理
        //    var categoryAndCount = ObtainInfo.GetCategoryAndCount(nonTestTrackDatas);

        //    var 标记Sum = CalculateSum(categoryAndCount, 14, 15, 16, 17, 18, 19, 39, 46, 47);
        //    var 焊缝Sum = CalculateSum(categoryAndCount, 2, 11, 22);
        //    var 轨头Sum = CalculateSum(categoryAndCount, 5, 6, 36, 13);
        //    var 核伤Sum = CalculateSum(categoryAndCount, 5, 6);
        //    var 轨腰Sum = CalculateSum(categoryAndCount, 7, 9, 29);
        //    var 轨底Sum = CalculateSum(categoryAndCount, 37, 8);
        //    var 作业违规Sum = CalculateSum(categoryAndCount, 21, 39, 48);

        //    List<DamageCategorySummaryTree> RootCategoryList = new List<DamageCategorySummaryTree>();
        //    RootCategoryList.Add(new DamageCategorySummaryTree() { Name = "正常标记", Children = new() });
        //    RootCategoryList.Add(new DamageCategorySummaryTree() { Name = "伤损标记", Children = new() });
        //    RootCategoryList.Add(new DamageCategorySummaryTree() { Name = $"作业标记 总次数{标记Sum}", Children = new() });

        //    // 2. 测试轨节点（核心修改：添加子项）
        //    RootCategoryList.Add(new DamageCategorySummaryTree()
        //    {
        //        Name = $"测试轨 (前{_activeBeforeShield}后{_activeAfterShield})",
        //        Children = new List<ITreeNode>()
        //{
        //    // 屏蔽前测试轨子项
        //    new DamageCategoryTree()
        //    {
        //        Name = "屏蔽前测试轨",
        //        Count = _activeBeforeShield,
        //        ColorBrush = Brushes.Gray,
        //        // 绑定前屏蔽测试轨的详情列表（通过GetTestTrackItems获取）
        //        Children = GetTestTrackItems("Before")
        //    },
        //    // 屏蔽后测试轨子项
        //    new DamageCategoryTree()
        //    {
        //        Name = "屏蔽后测试轨",
        //        Count = _activeAfterShield,
        //        ColorBrush = Brushes.Gray,
        //        // 绑定后屏蔽测试轨的详情列表
        //        Children = GetTestTrackItems("After")
        //    }
        //}
        //    });

        //    // 正常类里面细分
        //    RootCategoryList[0].Children.Add(new DamageCategorySummaryTree()
        //    {
        //        Name = $"焊缝 总次数{焊缝Sum}",
        //        Children = new()
        //    });






        //        RootCategoryList[1].Children.Add(new DamageCategorySummaryTree()
        //        {
        //            Name = $"轨头 总次数{轨头Sum}",
        //            Children = new()
        //    {
        //        new DamageCategorySummaryTree()
        //        {
        //            Name = $"核伤 总次数{核伤Sum}",
        //            Children = new()
        //        },
        //    }
        //        });
        //        RootCategoryList[1].Children.Add(new DamageCategorySummaryTree()
        //        {
        //            Name = $"轨腰 总次数{轨腰Sum}",
        //            Children = new()
        //        });
        //        RootCategoryList[1].Children.Add(new DamageCategorySummaryTree()
        //        {
        //            Name = $"轨底 总次数{轨底Sum}",
        //            Children = new()
        //        });


        //    RootCategoryList[2].Children.Add(new DamageCategorySummaryTree()
        //    {
        //        Name = $"作业违规 总次数{作业违规Sum}",
        //        Children = new()
        //    });

        //    var 轨头Category = (RootCategoryList[1].Children[0] as DamageCategorySummaryTree)?.Children;
        //    var 轨腰Category = (RootCategoryList[1].Children[1] as DamageCategorySummaryTree)?.Children;
        //    var 轨底Category = (RootCategoryList[1].Children[2] as DamageCategorySummaryTree)?.Children;



        //    foreach (var x in categoryAndCount)
        //    {
        //        if (DamageIdToBrush(x.Value) == Brushes.Green)
        //        {
        //            if (x.Value == 2 || x.Value == 11 || x.Value == 22)
        //            {
        //                (RootCategoryList[0].Children[0] as DamageCategorySummaryTree)
        //                    ?.Children.Add(new DamageCategoryTree()
        //                    {
        //                        Name = $"{DamageIdToDamageName(x.Value)}",
        //                        Count = x.Key,
        //                        ColorBrush = Brushes.Green,
        //                        Children = GetCategorySummary(x.Value, nonTestTrackDatas)
        //                        .OrderByDescending(x => x.Count).ToList()
        //                    });
        //            }
        //            else
        //            {
        //                RootCategoryList[0].Children.Add(new DamageCategoryTree()
        //                {
        //                    Name = $"{DamageIdToDamageName(x.Value)}",
        //                    Count = x.Key,
        //                    ColorBrush = Brushes.Green,
        //                    Children = GetCategorySummary(x.Value, nonTestTrackDatas)
        //                .OrderByDescending(x => x.Count).ToList()
        //                });
        //            }
        //        }
        //        else if (DamageIdToBrush(x.Value) == Brushes.Red)
        //        {


        //                if (x.Value == 5 || x.Value == 6)
        //                {
        //                    (轨头Category?[0] as DamageCategorySummaryTree)?.Children.Add(new DamageCategoryTree()
        //                    {
        //                        Name = $"{DamageIdToDamageName(x.Value)}",
        //                        Count = x.Key,
        //                        Children = GetCategorySummary(x.Value, nonTestTrackDatas, true)
        //                        .OrderByDescending(x => x.weight).ToList()
        //                    });
        //                }
        //                else if (x.Value == 36 || x.Value == 13)
        //                {
        //                    轨头Category.Add(new DamageCategoryTree()
        //                    {
        //                        Name = $"{DamageIdToDamageName(x.Value)}",
        //                        Count = x.Key,
        //                        Children = GetCategorySummary(x.Value, nonTestTrackDatas, true)
        //                        .OrderByDescending(x => x.weight).ToList()
        //                    });
        //                }
        //                else if (x.Value == 9 || x.Value == 7||x.Value==29)
        //                {
        //                    轨腰Category.Add(new DamageCategoryTree()
        //                    {
        //                        Name = $"{DamageIdToDamageName(x.Value)}",
        //                        Count = x.Key,
        //                        Children = GetCategorySummary(x.Value, nonTestTrackDatas, true)
        //                        .OrderByDescending(x => x.weight).ToList()
        //                    });
        //                }
        //                else if (x.Value == 37 || x.Value == 8)
        //                {
        //                    轨底Category.Add(new DamageCategoryTree()
        //                    {
        //                        Name = $"{DamageIdToDamageName(x.Value)}",
        //                        Count = x.Key,
        //                        Children = GetCategorySummary(x.Value, nonTestTrackDatas, true)
        //                        .OrderByDescending(x => x.weight).ToList()
        //                    });
        //                }

        //        }
        //        else if (DamageIdToBrush(x.Value) == Brushes.YellowGreen)
        //        {
        //            if (x.Value == 21 || x.Value == 39 || x.Value == 48)
        //            {
        //                (RootCategoryList[2].Children[0] as DamageCategorySummaryTree).Children.Add(new DamageCategoryTree()
        //                {
        //                    Name = $"{DamageIdToDamageName(x.Value)}",
        //                    Count = x.Key,
        //                    ColorBrush = Brushes.YellowGreen,
        //                    Children = GetCategorySummary(x.Value, nonTestTrackDatas)
        //                                      .OrderByDescending(x => x.Count).ToList()
        //                });
        //            }
        //            else
        //            {
        //                RootCategoryList[2].Children.Add(new DamageCategoryTree()
        //                {
        //                    Name = $"{DamageIdToDamageName(x.Value)}",
        //                    Count = x.Key,
        //                    ColorBrush = Brushes.YellowGreen,
        //                    Children = GetCategorySummary(x.Value, nonTestTrackDatas)
        //                                      .OrderByDescending(x => x.Count).ToList()
        //                });
        //            }
        //        }
        //    }
        //    DamageTree = RootCategoryList;
        //}

        private void InitCategorySummary(int DisplayedRulesCount = 10)
        {
            // 正确做法：统一数据源
            var testTrackFiles = DamageDataList
                .Select(data => Path.GetFileName(data.Url.Split('?')[0]))
                .Intersect(ThumbnailImgInfos
                    .Where(img => img.IsTestTrack)
                    .Select(img => Path.GetFileName(img.Path.Split('?')[0]))
                .ToHashSet());

            // 正确过滤非测试轨数据
            var nonTestTrackDatas = DamageDataList
                .Where(data => !testTrackFiles.Contains(Path.GetFileName(data.Url.Split('?')[0])))
                .ToList();

            // 4. 使用过滤后的数据进行后续处理
            var categoryAndCount = ObtainInfo.GetCategoryAndCount(nonTestTrackDatas, false, OcrDataList); // 传入OCR数据

            var 标记Sum = CalculateSum(categoryAndCount, 14, 15, 16, 17, 18, 19, 39, 46, 47);
            var 焊缝Sum = CalculateSum(categoryAndCount, 2, 11, 22);
            var 轨头Sum = CalculateSum(categoryAndCount, 5, 6, 36, 13);
            var 核伤Sum = CalculateSum(categoryAndCount, 5, 6);
            var 轨腰Sum = CalculateSum(categoryAndCount, 7, 9, 29);
            var 轨底Sum = CalculateSum(categoryAndCount, 37, 8);
            var 作业违规Sum = CalculateSum(categoryAndCount, 21, 39, 48)    ; // 48是超速类别

            List<DamageCategorySummaryTree> RootCategoryList = new List<DamageCategorySummaryTree>();
            RootCategoryList.Add(new DamageCategorySummaryTree() { Name = "正常标记", Children = new() });
            RootCategoryList.Add(new DamageCategorySummaryTree() { Name = "伤损标记", Children = new() });
            RootCategoryList.Add(new DamageCategorySummaryTree() { Name = $"作业标记 总次数{标记Sum}", Children = new() });

            // 测试轨节点
            RootCategoryList.Add(new DamageCategorySummaryTree()
            {
                Name = $"测试轨 (前{_activeBeforeShield}后{_activeAfterShield})",
                Children = new List<ITreeNode>()
        {
            new DamageCategoryTree()
            {
                Name = "屏蔽前测试轨",
                Count = _activeBeforeShield,
                ColorBrush = Brushes.Gray,
                Children = GetTestTrackItems("Before")
            },
            new DamageCategoryTree()
            {
                Name = "屏蔽后测试轨",
                Count = _activeAfterShield,
                ColorBrush = Brushes.Gray,
                Children = GetTestTrackItems("After")
            }
        }
            });

            // 正常类里面细分
            RootCategoryList[0].Children.Add(new DamageCategorySummaryTree()
            {
                Name = $"焊缝 总次数{焊缝Sum}",
                Children = new()
            });

            RootCategoryList[1].Children.Add(new DamageCategorySummaryTree()
            {
                Name = $"轨头 总次数{轨头Sum}",
                Children = new()
     {
         new DamageCategorySummaryTree()
         {
             Name = $"核伤 总次数{核伤Sum}",
             Children = new()
         },
     }
            });
            RootCategoryList[1].Children.Add(new DamageCategorySummaryTree()
            {
                Name = $"轨腰 总次数{轨腰Sum}",
                Children = new()
            });
            RootCategoryList[1].Children.Add(new DamageCategorySummaryTree()
            {
                Name = $"轨底 总次数{轨底Sum}",
                Children = new()
            });

            // 作业违规分类
            RootCategoryList[2].Children.Add(new DamageCategorySummaryTree()
            {
                Name = $"作业违规 总次数{作业违规Sum}",
                Children = new()
            });

            var 轨头Category = (RootCategoryList[1].Children[0] as DamageCategorySummaryTree)?.Children;
            var 轨腰Category = (RootCategoryList[1].Children[1] as DamageCategorySummaryTree)?.Children;
            var 轨底Category = (RootCategoryList[1].Children[2] as DamageCategorySummaryTree)?.Children;

            foreach (var x in categoryAndCount)
            {
                var summaryList = GetCategorySummary(x.Value, nonTestTrackDatas);
                int imageCount = summaryList.Count;
                if (DamageIdToBrush(x.Value) == Brushes.Green)
                {
                    if (x.Value == 2 || x.Value == 11 || x.Value == 22)
                    {
                        (RootCategoryList[0].Children[0] as DamageCategorySummaryTree)
                            ?.Children.Add(new DamageCategoryTree()
                            {
                                Name = $"{DamageIdToDamageName(x.Value)}",
                                Count = x.Key,
                                ColorBrush = Brushes.Green,
                                Children = GetCategorySummary(x.Value, nonTestTrackDatas)
                                .OrderByDescending(x => x.Count).ToList()
                            });
                    }
                    else
                    {
                        RootCategoryList[0].Children.Add(new DamageCategoryTree()
                        {
                            Name = $"{DamageIdToDamageName(x.Value)}",
                            Count = x.Key,
                            ColorBrush = Brushes.Green,
                            Children = GetCategorySummary(x.Value, nonTestTrackDatas)
                            .OrderByDescending(x => x.Count).ToList()
                        });
                    }
                }
                else if (DamageIdToBrush(x.Value) == Brushes.Red)
                {
                    if (x.Value == 5 || x.Value == 6)
                    {
                        (轨头Category?[0] as DamageCategorySummaryTree)?.Children.Add(new DamageCategoryTree()
                        {
                            Name = $"{DamageIdToDamageName(x.Value)}",
                            Count = x.Key,
                            Children = GetCategorySummary(x.Value, nonTestTrackDatas, true)
                            .OrderByDescending(x => x.weight).ToList()
                        });
                    }
                    else if (x.Value == 36 || x.Value == 13)
                    {
                        轨头Category.Add(new DamageCategoryTree()
                        {
                            Name = $"{DamageIdToDamageName(x.Value)}",
                            Count = x.Key,
                            Children = GetCategorySummary(x.Value, nonTestTrackDatas, true)
                            .OrderByDescending(x => x.weight).ToList()
                        });
                    }
                    else if (x.Value == 9 || x.Value == 7 || x.Value == 29)
                    {
                        轨腰Category.Add(new DamageCategoryTree()
                        {
                            Name = $"{DamageIdToDamageName(x.Value)}",
                            Count = x.Key,
                            Children = GetCategorySummary(x.Value, nonTestTrackDatas, true)
                            .OrderByDescending(x => x.weight).ToList()
                        });
                    }
                    else if (x.Value == 37 || x.Value == 8)
                    {
                        轨底Category.Add(new DamageCategoryTree()
                        {
                            Name = $"{DamageIdToDamageName(x.Value)}",
                            Count = x.Key,
                            Children = GetCategorySummary(x.Value, nonTestTrackDatas)
                            .OrderByDescending(x => x.weight).ToList()
                        });
                    }
                }
                else if (DamageIdToBrush(x.Value) == Brushes.YellowGreen)
                {
                    if (x.Value == 21 || x.Value == 39 || x.Value == 48) // 48是超速类别
                    {
                        (RootCategoryList[2].Children[0] as DamageCategorySummaryTree).Children.Add(new DamageCategoryTree()
                        {
                            Name = $"{DamageIdToDamageName(x.Value)}",
                            Count = x.Key,
                            ColorBrush = Brushes.YellowGreen,
                            Children = GetCategorySummary(x.Value, nonTestTrackDatas)
                                              .OrderByDescending(x => x.Count).ToList()
                        });
                    }
                    else
                    {
                        RootCategoryList[2].Children.Add(new DamageCategoryTree()
                        {
                            Name = $"{DamageIdToDamageName(x.Value)}",
                            Count = x.Key,
                            ColorBrush = Brushes.YellowGreen,
                            Children = GetCategorySummary(x.Value, nonTestTrackDatas)
                                              .OrderByDescending(x => x.Count).ToList()
                        });
                    }
                }
            }
            DamageTree = RootCategoryList;
        }

        private int CalculateSum(IEnumerable<KeyValuePair<int, float>> categoryAndCount, params int[] values)
        {
            return categoryAndCount
                .Where(x => values.Contains((int)x.Value))
                .Select(x => x.Key)
                .Sum();
        }
        #endregion

        SampleImg SampleWin;
        #region 修改当前图片
        [RelayCommand(CanExecute = nameof(IsSelectedImg))]
        void ModifyImg()
        {
            var sampleImg = BitmapFrame.Create(new Uri(ImgPath.OutReplaceInString(), UriKind.Relative));
            var target = SqlImgInfos.FirstOrDefault(info => Path.GetFileName(info.ImgPath) == Path.GetFileName(ImgPath));

            var vm = new SampleImgViewModel(
                sampleImg,
                sampleImg.Width,
                sampleImg.Height,
                DamageDataList,
                damagePoints?.ToList() ?? new List<float[]>(),
                currentImgIndex,
                ImgPath,
                target
            );
            SampleWin = new SampleImg(vm);
            SampleWin.Owner = Application.Current.MainWindow;
            SampleWin.ScreenShotTaken += SampleWin_ScreenShotTaken;
            SampleWin.Closed += ChildWindow_Closed;
            SampleWin.ShowDialog();
        }
       


        private async void SampleWin_ScreenShotTaken(object? sender, BitmapSource e)
        {
            if (e == null)
            {
                ImgSelectionChanged();
            }
            else if (e != null)
            {
                if (IsShowAllBoxSelected)//当显示所有框选按钮点击后才切换图片
                {

                    ImgSource = BitmapFrame.Create(e);
                }
                else
                {
                    ImgSource = BitmapFrame.Create(new Uri(ImgPath.OutReplaceInString(), UriKind.Relative));
                }
                var OutSavePath = ((sender as SampleImg).DataContext as SampleImgViewModel).OutImgPath;
                SaveBitmapToPng(OutSavePath, e);
                Growl.InfoGlobal($"{Path.GetFileName(ImgPath)}图片保存成功");
            }
        }

        private void SaveBitmapToPng(string path, BitmapSource image)
        {
            try
            {
                using var fileStream = new System.IO.FileStream(path, FileMode.Create);
                BitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(image));
                encoder.Save(fileStream);
            }
            catch (System.IO.IOException ex)
            {
                MessageBox.Error($"文件保存异常,异常原因:{ex.Message}");
            }
        }

        private async void ChildWindow_Closed(object? sender, EventArgs e)
        {
            DetailsList.Clear();
            if (this.damagePoints != null && damagePoints.Length != 0)
            {
                InitDamageDetails(damagePoints);
            }
            else
            {
                InitCategorySummary();
            }
            //;
            //UpdataThumbnail();
            // 子窗口关闭后的逻辑处理
            if (currentImgIndex == -1)
            {
                (_, currentImgIndex) = GetDamagePointsAndIndex(ImgPath);
            }


            SampleWin.ScreenShotTaken -= SampleWin_ScreenShotTaken;
            SampleWin.Closed -= ChildWindow_Closed; // 取消订阅事件
            SampleWin = null; // 释放资源
        }
        [RelayCommand]
        void WinClosing()
        {
            ImgSource = null;
            ThumbnailImgInfos.Clear();
            ClearImgAndData();
            if (IsModifyResultJson)
            {
                AboutJson.SaveJson<List<DamageData>>(DamageDataList, Path.Combine(Settings.Default.InPath, ImgFolderName), "result.json");
            }

            SaveSqlImgInfos(SqlImgInfos);
        }


        void ClearImgAndData() //清除在out文件夹下面没有数据的图片还有相关图片
        {
            if (DamageDataList != null && !string.IsNullOrEmpty(ImgFolderName))
            {

                DamageDataList = DamageDataList//清理 damagePoint 为空的数据
                    .Where(x => x.DamagePoint.Length != 0 && x.DamagePoint != null)
                    .Select(x => x).ToList();
               var imgFolderPath = Path.Combine(Settings.Default.OutPath, ImgFolderName);
                if (!Directory.Exists(imgFolderPath))
                {
                    return;
                }

                var outImgPaths = Directory.GetFiles(Path.Combine(Settings.Default.OutPath, ImgFolderName), "*.png");
                var outImgFileNames = outImgPaths.Select(x => Path.GetFileName(x));
                var shouldBeDelateImgs = outImgFileNames.Except(DamageDataList.Select(x => Path.GetFileName(x.Url)));

                foreach (var img in shouldBeDelateImgs)
                {
                    File.Delete(Path.Combine(Settings.Default.OutPath, ImgFolderName, img));
                }
            }
        }
        #endregion
        #region 搜索ocr里程
        [ObservableProperty]
        bool isDetectionAsynEnable = false;
        [ObservableProperty]
        bool isDetectionAsynChecked = false;
        [ObservableProperty]
        bool is6MRunning = false;
        [ObservableProperty]
        bool is8CRunning = false;
        [ObservableProperty]
        string searchMileageText;

        ObservableCollection<Records.OcrData> locationMileageImgs = new ObservableCollection<Records.OcrData>();
        public ObservableCollection<Records.OcrData> LocationMileageImgs { get => locationMileageImgs; set => locationMileageImgs = value; }

        [RelayCommand]
        private void SearchMileage(string mileage)
        {
            LocationMileageImgs.Clear();
            if (IsDetectionAsynChecked)
            {
                if (Is6MRunning && Is8CRunning)
                {
                    HandyControl.Controls.MessageBox.Warning("请不要同时打开6M和8C探伤软件的里程定位对话框");
                }
                else if (Is6MRunning)
                {
                    JGT_6M.LocationMileage(mileage);
                }
                else if (Is8CRunning)
                {
                    JGT_8C.LocationMileageAsync(mileage);
                }
            }
            string[] mils = mileage.Split(new string[2] { "km", "KM" }, StringSplitOptions.RemoveEmptyEntries);
            if (Parameter != null && File.Exists(Path.Combine(Parameter, "OcrResult.json")))
            {
                string km = mils.FirstOrDefault();
                string i = mils.LastOrDefault().Replace("m", "").Replace("M", "");
                string mileageFilePath = Path.Combine(Parameter, "OcrResult.json");
                List<Records.OcrData> ocrData = AboutJson.DeserializeJson<List<Records.OcrData>>(mileageFilePath);
                List<Records.OcrData> resutl = ocrData.Where((Records.OcrData x) => x.MileageText.Contains(km) && x.MileageText.Contains(i)).ToList();
                LocationMileageImgs = new ObservableCollection<Records.OcrData>(resutl.Select((Records.OcrData x) => new Records.OcrData(Path.GetFileName(x.ImgFullPath), x.MileageText,x.speedvalue)));
                if (LocationMileageImgs.Count != 0)
                {
                    SelectMileageWindow win = new SelectMileageWindow();
                    win.Owner = System.Windows.Application.Current.MainWindow;
                    win.ShowDialog();

                }
                else
                {
                    HandyControl.Controls.MessageBox.Info("没有找到任何符合里程数的图片,请检查输入格式是否正确");
                }
            }
            else
            {
                HandyControl.Controls.MessageBox.Error("当前集合没有找到ocr里程文件");
            }
        }

        private List<Details> GetTestTrackItems(string testTrackType)
        {
            var result = new List<Details>();
            if (ThumbnailImgInfos == null) return result;

            foreach (var img in ThumbnailImgInfos)
            {
                // 筛选出“前屏蔽”或“后屏蔽”的测试轨
                if (img.IsTestTrack && img.TestTrackType == testTrackType)
                {
                    result.Add(new Details
                    {
                        FileName = img.Name, // 显示测试轨文件名
                        Count = 1 // 每个测试轨计为1条
                    });
                }
            }
            return result;
        }

        #endregion
        #region 铁轨信息输入
        [ObservableProperty]
        string railwayName;
        [ObservableProperty]
        //铁轨起始里程
        string railwayStartMileage;
        [ObservableProperty]
        //铁轨终止里程
        string railwayEndMileage;
        [ObservableProperty]
        //操作人
        string operatorName;
        [ObservableProperty]
        string detectionTime;

        [RelayCommand]
        void SaveRailWayInfo(HandyControl.Controls.Window win)
        {
            if ((!string.IsNullOrEmpty(RailwayName) && !string.IsNullOrEmpty(RailwayStartMileage) && !string.IsNullOrEmpty(RailwayEndMileage) && !string.IsNullOrEmpty(OperatorName)))
            {
                MessageBox.Success("信息保存完成");
                win.Close();
            }
            else
            {
                MessageBox.Info("信息输入不完整,请填写完整信息");
            }

        }

        #endregion
        #region 截图事件发生与结束时处理函数
        public static string? FilePathIn;
        void OnScreenShotStart(object sender, EventArgs e)
        {

         

        }
        async void OnScreenShotFinished(object sender, string time)
        {

            string Elapsed="";
            bool IsAnalyse = true;
            this.Parameter = FilePathIn;
            this.ImgFolderName = Path.GetFileName(FilePathIn) ?? "没有文件夹";

            //var tcs = new TaskCompletionSource<bool>();
            //Growl.AskGlobal("是否开始分析", (x) => {
            //    Growl.InfoGlobal(x.ToString());
            //    tcs.TrySetResult(x); // 用 TrySetResult 防止多次调用
            //    return true;
            //});

            //// 等待用户选择或5秒超时
            //var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(5000));
            //bool isAnalyse = completedTask == tcs.Task ? tcs.Task.Result : true;

            //if (isAnalyse)
            //{
            //    await this.GetJsonResult();
            //}
            //else
            //{
            //    await Task.Delay(10000);
            //}

            //Growl.ClearGlobal();

            //if (Cap != null)
            //{
            //    dispatcher.Invoke(() =>
            //    {
            //        Cap.Close();
            //    });
            //}
            try
            {
                // 先显示主窗口（立即显示，不等待）
                dispatcher.Invoke(() =>
                {
                    Application.Current.MainWindow?.Show();
                    Application.Current.MainWindow?.Activate();
                });

                // 然后执行耗时操作（可选 Task.Run）
                await this.GetJsonResult();
            }
            catch(Exception ex)
            {
                Console.WriteLine($"分析过程中出错: {ex.Message}");
            }
            finally
            {
                if (Cap != null)
                {
                    dispatcher.Invoke(() =>
                    {
                        Cap.Close();
                    });
                }
            }

        }

        #endregion

        [RelayCommand]
        private void SelectExeFile()
        {
            System.Windows.Forms.OpenFileDialog openFileDialog = new();
            string selectedFilePath = null;
            openFileDialog.Filter = "可执行文件 (*.exe)|*.exe|所有文件 (*.*)|*.*";
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                selectedFilePath = openFileDialog.FileName;
            }
            HandyControl.Controls.MessageBox.Info((selectedFilePath == null) ? "当前没有选择任何程序" : ("当前选择程序的路径为" + selectedFilePath));
            if (selectedFilePath != null)
            {
                MenuItems.Insert(MenuItems.Count - 1, new MenuItem
                {
                    Header = Path.GetFileName(selectedFilePath),
                    CommandParameter = selectedFilePath,
                    Command = ProcessStartCommand
                });
                Settings.Default.AppLocation.Add(selectedFilePath);
                Settings.Default.Save();
            }
        }

        [RelayCommand]
        private void StartSnipaste()
        {

            Utilities.StartProcess(Settings.Default.Snipaste);
            HandyControl.Controls.MessageBox.Info("Snipaste已启动,按F1按键截图 按F3按键贴图");
        }

        #region 人工判断单个图片是否有伤

        [ObservableProperty]
        bool isReadRemarkOnly = true;

        string damageRemark;
        public string DamageRemark
        {
            get { return damageRemark; }
            set
            {
                if (value == damageRemark) return; // 无变化则跳过
                SetProperty(ref damageRemark, value);

                    DataAccess.UpdateImageRemark(SqlImgInfos, ImgPath, value);
                
            }
        }





        [ObservableProperty]
        bool hasNoDamageImg;


        [ObservableProperty]
        bool hasDamageImg;

        private bool isNoDamageBtnEnabled = true;
        public bool IsNoDamageBtnEnabled
        {
            get => isNoDamageBtnEnabled;
            set => SetProperty(ref isNoDamageBtnEnabled, value);
        }

        private bool isDamageBtnEnabled = true;
        public bool IsDamageBtnEnabled
        {
            get => isDamageBtnEnabled;
            set => SetProperty(ref isDamageBtnEnabled, value);
        }

        bool CanNoDamageCheck() => ImgSource != null;
        bool CanDamageCheck() => ImgSource != null;

        [RelayCommand(CanExecute = nameof(CanNoDamageCheck))]
        void NoDamageCheck()
        {
            Console.WriteLine("NoDamageCheck called");
            int? isconfirmDamage = GetIsConfirmDamage();
            Console.WriteLine($"当前图片的isconfirmDamage值为：{isconfirmDamage}");

            if (isconfirmDamage == 1)
            {
                HasDamageImg = true;
                HasNoDamageImg = false;
            }
            else if (isconfirmDamage == 0)
            {
                HasDamageImg = false;
                HasNoDamageImg = true;
            }
            else
            {
                HasDamageImg = false;
                HasNoDamageImg = false;
            }

            if (HasNoDamageImg)
            {
                // 二次点击：取消选择
                HasNoDamageImg = false;
                ResetState();
                UpdateConfirmDamageAndRemark(null, "");
                IsDamageBtnEnabled = true;
                
            }
            else
            {
                // 选择无伤
                HasNoDamageImg = true;
                HasDamageImg = false;

                var r = GetRemark();
                string remarkToSet = string.IsNullOrWhiteSpace(r) ? "无伤" : r;
                UpdateConfirmDamageAndRemark(false, remarkToSet);

                DamageRemark = remarkToSet;
                IsReadRemarkOnly = false;
                ImgBorderBrush = Brushes.LightGreen;
                IsDamageBtnEnabled = false;
            }

            NoDamageCheckCommand.NotifyCanExecuteChanged();
            DamageCheckCommand.NotifyCanExecuteChanged();

            
        }

        [RelayCommand(CanExecute = nameof(CanDamageCheck))]
        void DamageCheck()
        {

            int? isconfirmDamage = GetIsConfirmDamage();
            if (isconfirmDamage == 1)
            {
                HasDamageImg = true;
                HasNoDamageImg = false;
            }
            else if (isconfirmDamage == 0)
            {
                HasDamageImg = false;
                HasNoDamageImg = true;
            }
            else
            {
                HasDamageImg = false;
                HasNoDamageImg = false;
            }

            if (HasDamageImg)
            {
                // 二次点击：取消选择
                HasDamageImg = false;
                ResetState();
                UpdateConfirmDamageAndRemark(null, "");
                
                IsNoDamageBtnEnabled = true; // 解锁无伤按钮
            }
            else
            {
                // 选择有伤
                HasDamageImg = true;
                HasNoDamageImg = false;

                var r = GetRemark();
                string remarkToSet = string.IsNullOrWhiteSpace(r) ? "疑似有伤" : r;
                UpdateConfirmDamageAndRemark(true, remarkToSet);

                DamageRemark = remarkToSet;
                IsReadRemarkOnly = false;
                ImgBorderBrush = Brushes.OrangeRed;
                IsNoDamageBtnEnabled = false; // 锁定无伤按钮
                
            }

            //先通过当前点击图片是否有伤标记


            // 首先判定改图片是否无伤标记


            NoDamageCheckCommand.NotifyCanExecuteChanged();
            DamageCheckCommand.NotifyCanExecuteChanged();

            
        }

        void ResetState()
        {
            DamageRemark = "";
            IsReadRemarkOnly = true;
            ImgBorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(211, 211, 211));
        }

        string? GetRemark()
        {
            return SqlImgInfos
                .FirstOrDefault(x => Path.GetFileName(x.ImgPath) == Path.GetFileName(ImgPath))
                ?.Remark;
        }

        void UpdateConfirmDamageAndRemark(bool? isConfirmDamage, string? remark)
        {
            var target = SqlImgInfos
                .FirstOrDefault(x => Path.GetFileName(x.ImgPath) == Path.GetFileName(ImgPath));

            if (target != null)
            {
                using var sqlHelper = new SQLHelper(Settings.Default.SqlPath);

                if (target.IsConfirmDamage != isConfirmDamage)
                {
                    sqlHelper.UpdateImageIsConfirmDamage(target.ImgId, isConfirmDamage);
                    target.IsConfirmDamage = isConfirmDamage;
                }

                if (target.Remark != remark)
                {
                    sqlHelper.UpdateImageRemark(target.ImgId, remark);
                    target.Remark = remark;
                }
            }


        }

        int? GetIsConfirmDamage()
        {
            var target = SqlImgInfos
                    .FirstOrDefault(x => Path.GetFileName(x.ImgPath) == Path.GetFileName(ImgPath));
            if (target == null || string.IsNullOrWhiteSpace(ImgPath))
            {
                return null; // 如果没有找到目标或ImgPath为空，返回null
            }

            using var sqlHelper = new SQLHelper(Settings.Default.SqlPath);
            int? isconfirmdamage = sqlHelper.GetIsConfirmDamage(target.ImgId);
            return isconfirmdamage;

        }
        partial void OnImgSourceChanged(ImageSource? value)
        {
            var fileName = Path.GetFileName(ImgPath);
            var info = SqlImgInfos.FirstOrDefault(x => Path.GetFileName(x.ImgPath) == fileName);

            if (info != null)
            {
                if (info.IsConfirmDamage == true) // 疑似有伤
                {
                    HasDamageImg = true;
                    HasNoDamageImg = false;

                    DamageRemark = string.IsNullOrWhiteSpace(info.Remark) ? "疑似有伤" : info.Remark;
                    ImgBorderBrush = Brushes.OrangeRed;
                    IsReadRemarkOnly = false;

                    IsNoDamageBtnEnabled = false;
                    IsDamageBtnEnabled = true;
                }
                else if (info.IsConfirmDamage == false) // 无伤
                {
                    HasNoDamageImg = true;
                    HasDamageImg = false;

                    DamageRemark = string.IsNullOrWhiteSpace(info.Remark) ? "无伤" : info.Remark;
                    ImgBorderBrush = Brushes.LightGreen;
                    IsReadRemarkOnly = false;

                    IsDamageBtnEnabled = false;
                    IsNoDamageBtnEnabled = true;
                }
                else // null，未标记
                {
                    HasDamageImg = false;
                    HasNoDamageImg = false;
                    ResetState();

                    IsNoDamageBtnEnabled = true;
                    IsDamageBtnEnabled = true;
                }
            }
            else
            {
                // 如果数据库中没有记录，视作未标记
                HasDamageImg = false;
                HasNoDamageImg = false;
                ResetState();

                IsNoDamageBtnEnabled = true;
                IsDamageBtnEnabled = true;
            }

            NoDamageCheckCommand.NotifyCanExecuteChanged();
            DamageCheckCommand.NotifyCanExecuteChanged();
        }

        #endregion




        [RelayCommand]
        
        private void TreeItemClick(DamageCategoryTree? clickedItem)
        {
            Debug.WriteLine($"点击节点：{clickedItem?.Name}");
            if (clickedItem == null) return;

            clickedItem.ColorBrush = Brushes.YellowGreen;
        }


        
            [RelayCommand]
            private void DetailItemClick(Details? clickedDetail)
            {
                if (clickedDetail == null)
                    return;

                // 设置为绿色（你可以自定义颜色）
                clickedDetail.ColorBrush = Brushes.YellowGreen;
            }

        public ObservableCollection<Details> DetailsList1 { get; set; } = new();

        
        
    }
}

