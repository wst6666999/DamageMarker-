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
using HandyControl.Tools.Extension;
using Microsoft.Data.Sqlite;
using OpenCvSharp;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data;
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
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Xceed.Words.NET;
using System.Threading.Channels;


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
        // 正常分析的 rail_class 按 info.json 里的 Instruments 自动判断；界面不再提供单轨/双轨手动切换。
        // 周期对比接口只发送 input_text，不额外发送 single/double。


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
        public string[]? imgFiles; //所有未判伤图片

        private string[]? originalImgFiles; // 原始全量图片列表




        [ObservableProperty]
        private bool isMileageRangeFiltered = false;

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
            set => SetProperty(ref thumbnailImgInfos, value);
        }

        // 仅用于本次打开过程的内存缓存，不写数据库、不改文件。
        // 作用：避免生成带框图、切换图片时反复遍历 DamageDataList。
        private readonly Dictionary<string, (float[][]? Points, int Index)> _damagePointsCacheByFileName = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _damageImageFileNameSet = new(StringComparer.OrdinalIgnoreCase);

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
        public string damageImgFolderName;
        // 当前选中图片的伤损等级
        private int? _damageLevel = null;
        public int? DamageLevel
        {
            get => _damageLevel;
            set
            {
                if (SetProperty(ref _damageLevel, value))
                {
                    UpdateSelectedImageDamageLevel(value);
                }
            }
        }

        private void UpdateSelectedImageDamageLevel(int? damageLevel)
        {
            if (SelectedIndex < 0 || SqlImgInfos == null || SelectedIndex >= SqlImgInfos.Count)
                return;

            var selectedImgInfo = SqlImgInfos[SelectedIndex];

            // 更新 SqlImgInfo 对象
            selectedImgInfo.DamageLevel = damageLevel;

            // 更新数据库
            using (var sqlHelper = new SQLHelper(Settings.Default.SqlPath))
            {
                int result = sqlHelper.UpdateDamageLevel(selectedImgInfo.ImgId, damageLevel);
            }

            // Growl.InfoGlobal("已更新当前图片的伤损等级！");
        }
        public static MainWindowViewModel Instance { get; private set; }
        public MainWindowViewModel()
        {
            Instance = this;
            Version = "V26.2";
            AppTitle = "探伤仪检测数据智能分析系统";
            SelectedIndex = -1;
            // 防止窗口刚初始化、尚未打开数据时处理中提示误显示。
            ResultProcessingVisibility = Visibility.Collapsed;
            //ScreenshotOffset = DamageMaker.Properties.Settings.Default.ScreenshotOffset;
            //ScreenshotInterval = DamageMaker.Properties.Settings.Default.ScreenshotInterval;
            //MouseMovePixel = DamageMaker.Properties.Settings.Default.MouseMovePixel;

            client.Timeout = TimeSpan.FromSeconds(3600);

            DamageFoldersListViewModel.OpenedDamageFolder += SwitchDamageFolder;
            DamageFoldersListViewModel.OpenedDamageFolder += ShowThumbnails;
            SetupContentViewModel.RulesCountChange += InitCategorySummary;


            foreach (var AppPath in Settings.Default.AppLocation)
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

            StartBackgroundStatusCheck();
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

            WeakReferenceMessenger.Default.Register<SpeedSettingMessage>(this, (r, m) =>
            {
                CurrentLimitedSpeed = m.LimitedSpeed;
                CurrentThroughWeldSpeed = m.ThroughWeldSpeed;
                RefreshCategorySummaryIfReady();
            });

            WeakReferenceMessenger.Default.Register<LostSettingMessage>(this, (r, m) =>
            {
                currentLimitedLost = m.LimitedLost;

                InitCategorySummary();
            });
        }

        private float currentLimitedLost;
        private void RefreshCategorySummaryIfReady()
        {
            if (DamageDataList != null && DamageDataList.Count > 0 &&
                ThumbnailImgInfos != null &&
                OcrDataList != null)
            {
                InitCategorySummary();
            }
        }

        private void StartBackgroundStatusCheck()
        {
            // 使用 Task.Run 在后台线程执行，避免阻塞UI
            Task.Run(async () =>
            {
                try
                {
                    await CheckAppStatusPeriodically();
                }
                catch (Exception ex)
                {
                    // 处理异常，比如记录日志
                    Debug.WriteLine($"状态检查任务异常: {ex.Message}");
                }
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
            InitImageTreeSync();// 更新图片树显示
        }

        //[RelayCommand]
        //void ScreenShotFunc()
        //{
        //    ScreenShotPopuped?.Invoke(this, EventArgs.Empty);
        //    Cap = Sprite.Show(new Capturing());
        //}
        [RelayCommand]
        void ScreenShotFunc()
        {
            ScreenShotPopuped?.Invoke(this, EventArgs.Empty);

            var capturingPanel = new Capturing();
            Cap = Sprite.Show(capturingPanel);

            capturingPanel.Dispatcher.BeginInvoke(new Action(() =>
            {
                System.Windows.Window window = System.Windows.Window.GetWindow(capturingPanel);
                if (window != null)
                {
                    // 使用更安全的位置计算
                    double screenWidth = SystemParameters.WorkArea.Width;
                    double screenHeight = SystemParameters.WorkArea.Height;

                    window.WindowStartupLocation = WindowStartupLocation.Manual;
                    window.Left = screenWidth - window.Width - 20;
                    window.Top = screenHeight - window.Height - 40;

                    // 强制激活窗口
                    window.Activate();
                    capturingPanel.Focus();
                }
            }), System.Windows.Threading.DispatcherPriority.Render);
        }

        public async Task UpdateImgSource(string imgPath)
        {
            if (string.IsNullOrWhiteSpace(imgPath) || !File.Exists(imgPath))
            {
                ImgSource = null;
                return;
            }

            // 只改加载方式：后台解码并 Freeze，避免大图读取/解码堵住 UI。
            // 不改变文件、不改变数据库。
            var bitmapImage = await Task.Run(() =>
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.UriSource = new Uri(imgPath, UriKind.Absolute);
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                image.EndInit();
                image.Freeze();
                return image;
            });

            ImgSource = bitmapImage;
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

        DamageFoldersList foldersListWindow;


        #region 打开历史数据后弹出的窗口
        [RelayCommand]
        public async Task SelectedPath() //选择文件夹
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
            try
            {
                bool needCycleCompare = false;
                ImgBorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(211, 211, 211));
                Parameter = SelectedPath;
                var mainData = new MainData(SelectedPath);
                OcrDataList = mainData.OcrDataList;
                ImgFolderName = mainData.ImgFolderName;
                originalImgFiles = mainData.ImgPaths;
                imgFiles = originalImgFiles.ToArray();
                IsMileageRangeFiltered = false;
                StartMileage = string.Empty;
                EndMileage = string.Empty;

                damageImgFolderName = SelectedPath.InReplaceOutString();
                DamageDataList = mainData.DamageDataList;
                NeedSavedInfo = mainData.NeedSavedInfo;
                if (NeedSavedInfo != null)
                {


                    if (NeedSavedInfo?.RailWayInfo?.CycleNumber > 1 && !NeedSavedInfo.isAnalyzed)
                    {
                        NeedSavedInfo.isAnalyzed = true;
                        AboutJson.SaveJson<ScreenshotInfo>(NeedSavedInfo, SelectedPath, "info.json");
                        needCycleCompare = true;   // 先记下来，后面拿到 DamageImgPaths 再跑
                    }

                    Console.WriteLine(Settings.Default.SqlPath);
                    using (var sqlHelper = new SQLHelper(Settings.Default.SqlPath))
                    {
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
                            if (SqlimgCount >= 0)
                            {
                                // 释放当前查询连接后再启动后台写入，避免两个 SQLite 连接相互等待。
                                sqlHelper.Connection.Close();

                                // Images 中包含图片 BLOB，读取和写入都放到后台线程。
                                // 后续流程仍然 await 等待入库完成，保证周期对比读取 LineType 时数据已经存在；
                                // 但 UI 线程不会被图片读取和 SQLite 批量事务堵住。
                                ResultProcessingVisibility = Visibility.Visible;
                                await Application.Current.Dispatcher.InvokeAsync(
                                    () => { },
                                    DispatcherPriority.Render);

                                string[] filesForDatabase = imgFiles?.ToArray() ?? Array.Empty<string>();
                                int folderIdForDatabase = sqlFolderId;
                                int existingImageCount = SqlimgCount;
                                var lineTypeLookup = BuildNormalizedLineTypeLookup(OcrDataList);

                                var imageDatabaseResult = await Task.Run(() =>
                                {
                                    var currentFolderImages = new List<SqlImgInfo>();
                                    var failedImages = new List<string>();
                                    bool saveSucceeded = true;
                                    int lineTypeUpdatedCount = 0;

                                    // ImageData 和 Mileage 完全沿用项目原有 Images 入库流程。
                                    // 不新增第二套图片/里程写入逻辑，只在原 INSERT 中追加 LineType 字段。
                                    if (existingImageCount == 0)
                                    {
                                        foreach (var file in filesForDatabase)
                                        {
                                            try
                                            {
                                                ParseFileName(file, out int idx, out string mileage);
                                                byte[] imageData = ReadImageBytesWithRetry(file);

                                                currentFolderImages.Add(new SqlImgInfo
                                                {
                                                    ImgPath = file,
                                                    FolderId = folderIdForDatabase,
                                                    Mileage = mileage,
                                                    ImageData = imageData,
                                                });
                                            }
                                            catch (Exception ex)
                                            {
                                                failedImages.Add(file);
                                                Console.WriteLine(
                                                    $"[Images入库] 图片读取最终失败：file={file}，error={ex.Message}");
                                            }
                                        }

                                        Console.WriteLine(
                                            $"[Images入库] 后台准备完成：FolderId={folderIdForDatabase}，" +
                                            $"应读取={filesForDatabase.Length}，成功={currentFolderImages.Count}，" +
                                            $"失败={failedImages.Count}");

                                        if (failedImages.Count == 0)
                                        {
                                            // 继续使用项目原有 Images 批量入库区域。
                                            // ImageData、Mileage 的获取方式和写入时机不变，只在同一条 INSERT 中追加 LineType。
                                            saveSucceeded = SaveSqlImgInfos(
                                                currentFolderImages,
                                                lineTypeLookup);
                                        }
                                        else
                                        {
                                            saveSucceeded = false;
                                        }
                                    }

                                    // 已经存在 Images 记录的旧数据不会再次 INSERT。
                                    // 此时只补写 LineType，不修改 ImageData、Mileage 或其他既有字段。
                                    if (existingImageCount > 0 &&
                                        saveSucceeded &&
                                        lineTypeLookup.Count > 0)
                                    {
                                        using var lineTypeHelper = new SQLHelper(Settings.Default.SqlPath);
                                        lineTypeUpdatedCount = lineTypeHelper.UpdateImageLineTypesOnly(
                                            folderIdForDatabase,
                                            lineTypeLookup);
                                    }

                                    List<SqlImgInfo> loadedImages;
                                    using (var verifyHelper = new SQLHelper(Settings.Default.SqlPath))
                                    {
                                        loadedImages = verifyHelper.GetImagesByFolderId(folderIdForDatabase);
                                    }

                                    return (
                                        ExpectedCount: existingImageCount == 0
                                            ? currentFolderImages.Count
                                            : loadedImages.Count,
                                        FailedImages: failedImages,
                                        SaveSucceeded: saveSucceeded,
                                        LineTypeUpdatedCount: lineTypeUpdatedCount,
                                        LoadedImages: loadedImages);
                                });

                                // ObservableProperty 只在 await 回到 UI 线程后赋值。
                                SqlImgInfos = imageDatabaseResult.LoadedImages;

                                if (imageDatabaseResult.FailedImages.Count > 0)
                                {
                                    MessageBox.Error(
                                        $"有 {imageDatabaseResult.FailedImages.Count} 张图片读取失败，" +
                                        "本次图片入库已停止。\n请查看后台日志中的 [Images入库] 信息后重新打开该数据。");
                                }
                                else if (!imageDatabaseResult.SaveSucceeded ||
                                         (existingImageCount == 0 &&
                                          SqlImgInfos.Count != imageDatabaseResult.ExpectedCount))
                                {
                                    MessageBox.Error(
                                        $"图片数据库保存不完整：应保存 {imageDatabaseResult.ExpectedCount} 张，" +
                                        $"实际查询到 {SqlImgInfos.Count} 张。请查看后台日志。");
                                }
                                else
                                {
                                    Console.WriteLine(
                                        $"[Images入库] 后台保存及校验完成：FolderId={folderIdForDatabase}，" +
                                        $"图片数={SqlImgInfos.Count}，旧数据LineType补写={imageDatabaseResult.LineTypeUpdatedCount}");
                                }

                                // 伤损标注不要在 ProcessData() 之前保存。
                                // 这里先只保存/读取 Images，后面 DamageDataList = mainData.ProcessData() 后再统一补写 DamageAnnotations。
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

                string jsonFilePath = Path.Combine(Parameter, "result1.json");
                if (!File.Exists(Path.Combine(SelectedPath, "result.json")))
                {
                    jsonFilePath = Path.Combine(Parameter, "result.json");
                }
                if (DamageDataList != null)
                {
                    DamageDataList = mainData.ProcessData();
                    BuildDisplayCaches();

                    // 修复伤损标注入库：必须在 ProcessData() 之后再保存 DamageAnnotations。
                    // 如果当前文件夹已有 Images 但 DamageAnnotations 为空，则只补写标注，不重复插入图片。
                    EnsureDamageAnnotationsSavedForCurrentFolder();

                    Console.WriteLine("-----------后端未处理数据详情-----------" + Environment.NewLine);
                    var damageCount = ObtainInfo.GetCategoryAndCount(DamageDataList);

                    damageCount.ForEach(x =>
                    {
                        Console.WriteLine($"{DataConversion.DamageIdToDamageName(x.Value)} id为{x.Value} 共有{x.Key}处伤损");
                    });
                    Console.WriteLine("----------------------------------" + Environment.NewLine);

                    DamageImgPaths = DamageDataList.Select(x => x.Url).ToList();
                    DamageImgPaths = DamageImgPaths.OrderBy(File.GetCreationTime).ToList();
                    if (needCycleCompare)
                    {
                        await ProcessHF(DamageImgPaths, damageImgFolderName);

                        // 周期对比会替换 DamageDataList 中的 DamagePoint。
                        // 必须重新建立绘图缓存，否则后端已经判成 id=2，绘图仍可能使用对比前的 id=52。
                        BuildDisplayCaches();
                        Console.WriteLine("【周期对比】ShowThumbnails 已重建最新绘图缓存");
                    }

                    AllImgCount = imgFiles.Length;
                    SingleImgCount = DamageImgPaths.Count;

                    ModifyImgCommand.NotifyCanExecuteChanged();
                    InitCategorySummary();
                    IsEnableThumbnail = true;
                    ExportReportCommand.NotifyCanExecuteChanged();
                    GetJsonResultCommand.NotifyCanExecuteChanged();
                    ThumbnailImgInfos.Clear();

                    await UpdataThumbnail();

                    // 结果整理/缩略图初始化已经完成，马上进入正式绘图阶段，隐藏处理中提示。
                    ResultProcessingVisibility = Visibility.Collapsed;
                    await SaveAllBoxSelectedImg(DamageImgPaths, damageImgFolderName, true);

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
            finally
            {
                ResultProcessingVisibility = Visibility.Collapsed;
            }
        }

        public static void ParseFileName(string fileName, out int idx, out string mileage)
        {
            idx = 0;
            mileage = string.Empty;

            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
            string[] parts = fileNameWithoutExt.Split('_');

            // 处理可能包含后缀的情况（如 _r, _l）
            if (parts.Length >= 3)
            {
                // 格式可能是：里程_编号_后缀 或 编号_里程_后缀
                bool firstIsIndex = int.TryParse(parts[0], out idx);
                bool firstIsMileage = IsMileageFormat(parts[0]);

                bool secondIsIndex = int.TryParse(parts[1], out int tempIndex);
                bool secondIsMileage = IsMileageFormat(parts[1]);

                // 检查第三部分是否为已知后缀（如 "r", "l" 等）
                bool thirdIsSuffix = IsKnownSuffix(parts[2]);

                if (thirdIsSuffix)
                {
                    if (firstIsIndex && secondIsMileage)
                    {
                        // 格式：编号_里程_后缀 (如 "397_175KM999M_r")
                        idx = int.Parse(parts[0]);
                        mileage = parts[1];
                    }
                    else if (firstIsMileage && secondIsIndex)
                    {
                        // 格式：里程_编号_后缀 (如 "175KM999M_397_l")
                        mileage = parts[0];
                        idx = int.Parse(parts[1]);
                    }
                    // 如果格式不匹配，继续尝试其他解析方式
                }
            }

            // 如果上面的3部分解析没有成功，或者parts.Length为2，尝试2部分解析
            if (idx == 0 && string.IsNullOrEmpty(mileage) && parts.Length >= 2)
            {
                // 情况1和2：两部分，可能是"编号_里程"或"里程_编号"
                bool firstIsIndex = int.TryParse(parts[0], out idx);
                bool firstIsMileage = IsMileageFormat(parts[0]);

                bool secondIsIndex = int.TryParse(parts[1], out int tempIndex);
                bool secondIsMileage = IsMileageFormat(parts[1]);

                if (firstIsIndex && secondIsMileage)
                {
                    // 格式：编号_里程 (如 "397_175KM999M")
                    idx = int.Parse(parts[0]);
                    mileage = parts[1];
                }
                else if (firstIsMileage && secondIsIndex)
                {
                    // 格式：里程_编号 (如 "175KM999M_397")
                    mileage = parts[0];
                    idx = int.Parse(parts[1]);
                }
            }
            else if (parts.Length == 1 || (idx == 0 && string.IsNullOrEmpty(mileage)))
            {
                // 情况3和4：只有一部分，可能是里程或编号
                if (IsMileageFormat(parts[0]))
                {
                    // 只有里程 (如 "175KM999M")
                    mileage = parts[0];
                }
                else if (int.TryParse(parts[0], out idx))
                {
                    // 只有编号 (如 "397")
                    // idx 已通过 TryParse 赋值
                }
            }
        }

        /// <summary>
        /// 检查字符串是否为已知的后缀（如 "r", "l" 等）
        /// </summary>
        /// <param name="v"></param>
        /// <returns></returns>
        private static bool IsKnownSuffix(string input)
        {
            string[] knownSuffixes = { "r", "l", "right", "left", "R", "L" };
            return knownSuffixes.Contains(input.ToLower());
        }

        /// <summary>
        /// 检查字符串是否符合里程格式（包含"KM"和"M"）
        /// </summary>
        private static bool IsMileageFormat(string input)
        {
            return input.Contains("KM") && input.Contains("M");
        }
        /// <summary>
        /// 保存单个图片的伤损标注信息
        /// </summary>
        /// <param name="file"></param>
        /// <param name="sqlFolderId"></param>
        /// <summary>
        /// 保存单个图片的所有伤损标注信息（从damagePoints二维数据）
        /// </summary>
        private void SaveAllDamageAnnotations(List<string> imgFiles, int sqlFolderId)
        {
            if (imgFiles == null || imgFiles.Count == 0)
            {
                Console.WriteLine("DamageAnnotations 入库跳过：imgFiles 为空");
                return;
            }

            if (DamageDataList == null || DamageDataList.Count == 0)
            {
                Console.WriteLine("DamageAnnotations 入库跳过：DamageDataList 为空");
                return;
            }

            using (var sqlHelper = new SQLHelper(Settings.Default.SqlPath))
            {
                sqlHelper.EnsureConnectionOpen();

                // 1. 先获取所有图片的ID映射。这里同时支持完整路径和文件名匹配，避免路径分隔符或 in/out 路径差异导致匹配失败。
                var imageIdMap = GetImageIdMap(sqlHelper, imgFiles, sqlFolderId);

                // 2. 建立伤损数据索引。优先按完整路径匹配，匹配不到再按文件名匹配。
                var damageByFullPath = DamageDataList
                    .Where(x => x != null && !string.IsNullOrWhiteSpace(x.Url))
                    .GroupBy(x => NormalizePathKey(x.Url), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

                var damageByFileName = DamageDataList
                    .Where(x => x != null && !string.IsNullOrWhiteSpace(x.Url))
                    .GroupBy(x => Path.GetFileName(x.Url), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

                int insertedCount = 0;
                int matchedImageCount = 0;
                int unmatchedImageCount = 0;

                // 3. 批量插入所有损伤标注
                using (var transaction = sqlHelper.Connection.BeginTransaction())
                {
                    try
                    {
                        foreach (var file in imgFiles)
                        {
                            if (!imageIdMap.TryGetValue(file, out int imageId))
                            {
                                unmatchedImageCount++;
                                Console.WriteLine($"DamageAnnotations 入库跳过：未找到 ImageId，file={file}");
                                continue;
                            }

                            DamageData? damageData = null;
                            string fullPathKey = NormalizePathKey(file);
                            string fileNameKey = Path.GetFileName(file);

                            if (!damageByFullPath.TryGetValue(fullPathKey, out damageData))
                            {
                                damageByFileName.TryGetValue(fileNameKey, out damageData);
                            }

                            if (damageData == null || damageData.DamagePoint == null || !damageData.DamagePoint.Any())
                            {
                                continue;
                            }

                            matchedImageCount++;

                            foreach (var damagePoint in damageData.DamagePoint)
                            {
                                if (damagePoint == null || damagePoint.Length < 6)
                                {
                                    continue;
                                }

                                var annotation = new DamageAnnotation
                                {
                                    ImageId = imageId,
                                    X = damagePoint[0],
                                    Y = damagePoint[1],
                                    Width = damagePoint[2],
                                    Height = damagePoint[3],
                                    DamageType = damagePoint[4],
                                    Confidence = damagePoint[5],
                                    CreatedTime = DateTime.Now,
                                    UpdatedTime = DateTime.Now,
                                    ImagePath = file
                                };

                                var annotationPara = new Dictionary<string, object>
                                {
                                    { "@ImageId", annotation.ImageId },
                                    { "@X", annotation.X },
                                    { "@Y", annotation.Y },
                                    { "@Width", annotation.Width },
                                    { "@Height", annotation.Height },
                                    { "@DamageType", annotation.DamageType },
                                    { "@Confidence", annotation.Confidence },
                                    { "@CreatedTime", annotation.CreatedTime },
                                    { "@UpdatedTime", annotation.UpdatedTime },
                                    { "@ImagePath", annotation.ImagePath }
                                };

                                var result = sqlHelper.InsertTableWithTransaction(annotationPara, "DamageAnnotations", transaction);
                                if (result >= 0)
                                {
                                    insertedCount++;
                                }
                                else
                                {
                                    Console.WriteLine($"插入 DamageAnnotations 失败: {sqlHelper.GetLastError()}");
                                }
                            }
                        }

                        transaction.Commit();
                        Console.WriteLine(
                            $"DamageAnnotations 入库完成：匹配图片={matchedImageCount}，" +
                            $"未找到ImageId={unmatchedImageCount}，插入标注={insertedCount}");
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        Console.WriteLine($"批量插入 DamageAnnotations 失败: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// 检查当前文件夹是否已经有伤损标注。若没有，则在 ProcessData() 之后补写 DamageAnnotations。
        /// </summary>
        private void EnsureDamageAnnotationsSavedForCurrentFolder()
        {
            if (sqlFolderId < 0)
            {
                Console.WriteLine("DamageAnnotations 补写跳过：sqlFolderId 无效");
                return;
            }

            if (imgFiles == null || imgFiles.Length == 0)
            {
                Console.WriteLine("DamageAnnotations 补写跳过：imgFiles 为空");
                return;
            }

            if (DamageDataList == null || DamageDataList.Count == 0)
            {
                Console.WriteLine("DamageAnnotations 补写跳过：DamageDataList 为空");
                return;
            }

            int annotationCount = GetDamageAnnotationCountByFolderId(sqlFolderId);
            Console.WriteLine($"当前文件夹 FolderId={sqlFolderId}，DamageAnnotations数量={annotationCount}");

            if (annotationCount == 0)
            {
                Console.WriteLine("检测到 Images 已存在但 DamageAnnotations 为空，开始补写伤损标注...");
                SaveAllDamageAnnotations(imgFiles.ToList(), sqlFolderId);
                int newCount = GetDamageAnnotationCountByFolderId(sqlFolderId);
                Console.WriteLine($"DamageAnnotations 补写结束，当前数量={newCount}");
            }
            else if (annotationCount > 0)
            {
                Console.WriteLine("当前文件夹已有 DamageAnnotations，跳过补写，避免重复入库");
            }
            else
            {
                Console.WriteLine("查询 DamageAnnotations 数量失败，跳过补写");
            }
        }

        private int GetDamageAnnotationCountByFolderId(int folderId)
        {
            try
            {
                using (var sqlHelper = new SQLHelper(Settings.Default.SqlPath))
                {
                    sqlHelper.EnsureConnectionOpen();

                    using var cmd = sqlHelper.Connection.CreateCommand();
                    cmd.CommandText = @"
SELECT COUNT(*)
FROM DamageAnnotations da
INNER JOIN Images i ON da.ImageId = i.ImageId
WHERE i.FolderId = @FolderId;";

                    var parameter = cmd.CreateParameter();
                    parameter.ParameterName = "@FolderId";
                    parameter.Value = folderId;
                    cmd.Parameters.Add(parameter);

                    var result = cmd.ExecuteScalar();
                    return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"查询 DamageAnnotations 数量失败: {ex.Message}");
                return -1;
            }
        }

        private static string NormalizePathKey(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            try
            {
                return Path.GetFullPath(path).Trim().Replace('/', '\\');
            }
            catch
            {
                return path.Trim().Replace('/', '\\');
            }
        }

        /// <summary>
        /// 根据 OcrResult.json 中的图片路径构建 LineType 查找表。
        /// Key 同时保存完整路径和文件名，Value 在进入数据库前只保留汉字。
        /// </summary>
        private static Dictionary<string, string> BuildNormalizedLineTypeLookup(
            IEnumerable<OcrData>? ocrDataList)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (ocrDataList == null)
            {
                return result;
            }

            foreach (var ocrData in ocrDataList)
            {
                if (ocrData == null || string.IsNullOrWhiteSpace(ocrData.ImgFullPath))
                {
                    continue;
                }

                string normalizedLineType =
                    SQLHelper.NormalizeLineTypeForStorage(ocrData.LineType);

                if (string.IsNullOrWhiteSpace(normalizedLineType))
                {
                    continue;
                }

                string fullPathKey = NormalizePathKey(ocrData.ImgFullPath);
                if (!string.IsNullOrWhiteSpace(fullPathKey))
                {
                    result[$"PATH|{fullPathKey}"] = normalizedLineType;
                }

                string fileName = Path.GetFileName(ocrData.ImgFullPath);
                if (!string.IsNullOrWhiteSpace(fileName))
                {
                    result[$"FILE|{fileName}"] = normalizedLineType;
                }
            }

            return result;
        }

        /// <summary>
        /// 按完整路径优先、文件名兜底，取得待写入 Images.LineType 的规范值。
        /// </summary>
        private static string ResolveNormalizedLineTypeForImage(
            string? imagePath,
            IReadOnlyDictionary<string, string>? lineTypeLookup)
        {
            if (string.IsNullOrWhiteSpace(imagePath) ||
                lineTypeLookup == null ||
                lineTypeLookup.Count == 0)
            {
                return string.Empty;
            }

            string fullPathKey = NormalizePathKey(imagePath);
            if (!string.IsNullOrWhiteSpace(fullPathKey) &&
                lineTypeLookup.TryGetValue($"PATH|{fullPathKey}", out string? lineTypeByPath))
            {
                return SQLHelper.NormalizeLineTypeForStorage(lineTypeByPath);
            }

            string fileName = Path.GetFileName(imagePath);
            if (!string.IsNullOrWhiteSpace(fileName) &&
                lineTypeLookup.TryGetValue($"FILE|{fileName}", out string? lineTypeByFileName))
            {
                return SQLHelper.NormalizeLineTypeForStorage(lineTypeByFileName);
            }

            return string.Empty;
        }

        private void BuildDisplayCaches()
        {
            _damagePointsCacheByFileName.Clear();
            _damageImageFileNameSet.Clear();

            if (DamageDataList == null)
                return;

            for (int i = 0; i < DamageDataList.Count; i++)
            {
                var item = DamageDataList[i];
                if (item == null || string.IsNullOrWhiteSpace(item.Url))
                    continue;

                var fileName = Path.GetFileName(item.Url);
                if (string.IsNullOrWhiteSpace(fileName))
                    continue;

                if (!_damagePointsCacheByFileName.ContainsKey(fileName))
                {
                    _damagePointsCacheByFileName[fileName] = (item.DamagePoint, i);
                }

                _damageImageFileNameSet.Add(fileName);
            }
        }

        /// <summary>
        /// 批量获取所有图片的ID映射
        /// </summary>
        private Dictionary<string, int> GetImageIdMap(SQLHelper sqlHelper, List<string> imgFiles, int folderId)
        {
            var imageIdMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            try
            {
                // 只查 ImageId / ImgPath，不再调用 GetImagesByFolderId 读取 ImageData 大字段。
                // 这是纯读取优化，不写数据库、不改变任何持久化数据。
                var imagesByFullPath = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                var imagesByFileName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                using var cmd = sqlHelper.Connection.CreateCommand();
                cmd.CommandText = @"
SELECT ImageId, ImgPath
FROM Images
WHERE FolderId = @FolderId;";
                cmd.Parameters.AddWithValue("@FolderId", folderId);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    int imageId = Convert.ToInt32(reader["ImageId"]);
                    string imgPath = reader["ImgPath"]?.ToString() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(imgPath))
                        continue;

                    string fullPathKey = NormalizePathKey(imgPath);
                    string fileNameKey = Path.GetFileName(imgPath);

                    if (!string.IsNullOrWhiteSpace(fullPathKey) && !imagesByFullPath.ContainsKey(fullPathKey))
                    {
                        imagesByFullPath[fullPathKey] = imageId;
                    }

                    if (!string.IsNullOrWhiteSpace(fileNameKey) && !imagesByFileName.ContainsKey(fileNameKey))
                    {
                        imagesByFileName[fileNameKey] = imageId;
                    }
                }

                foreach (var file in imgFiles)
                {
                    string fullPathKey = NormalizePathKey(file);
                    string fileNameKey = Path.GetFileName(file);

                    if (imagesByFullPath.TryGetValue(fullPathKey, out int imageId) ||
                        imagesByFileName.TryGetValue(fileNameKey, out imageId))
                    {
                        imageIdMap[file] = imageId;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"获取图片ID映射失败: {ex.Message}");
            }

            return imageIdMap;
        }

        /// <summary>
        /// 首次分析完成后，图片文件可能仍处于写入或短暂占用状态。
        /// 使用允许读写共享的方式读取，并在失败时短暂重试。
        /// </summary>
        private static byte[] ReadImageBytesWithRetry(
            string filePath,
            int maxRetries = 8,
            int delayMilliseconds = 250)
        {
            Exception? lastException = null;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    using var stream = new FileStream(
                        filePath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.ReadWrite);

                    if (stream.Length <= 0)
                    {
                        throw new IOException("图片文件长度为 0，可能仍未写入完成");
                    }

                    using var memoryStream = new MemoryStream();
                    stream.CopyTo(memoryStream);
                    var data = memoryStream.ToArray();

                    if (data.Length <= 0)
                    {
                        throw new IOException("读取到的图片数据为空");
                    }

                    return data;
                }
                catch (IOException ex)
                {
                    lastException = ex;
                    Console.WriteLine(
                        $"[Images入库] 图片读取失败，准备重试 {attempt}/{maxRetries}：" +
                        $"file={filePath}，error={ex.Message}");

                    if (attempt < maxRetries)
                    {
                        System.Threading.Thread.Sleep(delayMilliseconds);
                    }
                }
                catch (UnauthorizedAccessException ex)
                {
                    lastException = ex;
                    Console.WriteLine(
                        $"[Images入库] 图片访问失败，准备重试 {attempt}/{maxRetries}：" +
                        $"file={filePath}，error={ex.Message}");

                    if (attempt < maxRetries)
                    {
                        System.Threading.Thread.Sleep(delayMilliseconds);
                    }
                }
            }

            throw new IOException($"图片经过 {maxRetries} 次尝试仍无法读取：{filePath}", lastException);
        }

        private bool SaveSqlImgInfos(
            List<SqlImgInfo> infos,
            IReadOnlyDictionary<string, string>? lineTypeLookup = null)
        {
            using (var sqlHelper = new SQLHelper(Settings.Default.SqlPath))
            {
                sqlHelper.EnsureConnectionOpen();
                sqlHelper.EnsureImagesLineTypeColumn();
                sqlHelper.RemoveLegacyLineTypePendingArtifacts();

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
                            // 保留项目原有 ImageData、Mileage 写入区域和事务结构。
                            // 仅在同一条 INSERT 中追加 LineType，确保一张图片只对应一个 LineType。
                            using var command = new SqliteCommand(@"
INSERT INTO Images
    (FolderId, ImgPath, IsConfirmDamage, Remark, ImageData, Mileage, LineType)
VALUES
    (@FolderId, @ImgPath, @IsConfirmDamage, @Remark, @ImageData, @Mileage, @LineType);",
                                sqlHelper.Connection, transaction);

                            var folderIdParam = command.Parameters.Add("@FolderId", SqliteType.Integer);
                            var imgPathParam = command.Parameters.Add("@ImgPath", SqliteType.Text);
                            var isConfirmDamageParam = command.Parameters.Add("@IsConfirmDamage", SqliteType.Integer);
                            var remarkParam = command.Parameters.Add("@Remark", SqliteType.Text);
                            var imageDataParam = command.Parameters.Add("@ImageData", SqliteType.Blob);
                            var mileageParam = command.Parameters.Add("@Mileage", SqliteType.Text);
                            var lineTypeParam = command.Parameters.Add("@LineType", SqliteType.Text);

                            int insertedCount = 0;

                            foreach (SqlImgInfo info in infos)
                            {
                                folderIdParam.Value = info.FolderId;
                                imgPathParam.Value = info.ImgPath ?? (object)DBNull.Value;
                                isConfirmDamageParam.Value = info.IsConfirmDamage.HasValue
                                    ? (info.IsConfirmDamage.Value ? 1 : 0)
                                    : (object)DBNull.Value;
                                remarkParam.Value = info.Remark ?? (object)DBNull.Value;
                                imageDataParam.Value = info.ImageData ?? (object)DBNull.Value;
                                mileageParam.Value = info.Mileage ?? (object)DBNull.Value;

                                string normalizedLineType = ResolveNormalizedLineTypeForImage(
                                    info.ImgPath,
                                    lineTypeLookup);

                                lineTypeParam.Value = string.IsNullOrWhiteSpace(normalizedLineType)
                                    ? (object)DBNull.Value
                                    : normalizedLineType;

                                try
                                {
                                    var result = command.ExecuteNonQuery();
                                    if (result != 1)
                                    {
                                        throw new InvalidOperationException(
                                            $"Images 插入影响行数异常：result={result}，ImgPath={info.ImgPath}");
                                    }

                                    insertedCount += result;
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine(
                                        $"[Images入库] 单张插入失败：FolderId={info.FolderId}，" +
                                        $"ImgPath={info.ImgPath}，error={ex.Message}");
                                    throw;
                                }
                            }
                            transaction.Commit(); // 提交事务
                            Console.WriteLine(
                                $"[Images入库] 事务提交成功：FolderId={sqlFolderId}，插入数量={insertedCount}");
                            return insertedCount == infos.Count;
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback(); // 回滚事务
                            Console.WriteLine($"[Images入库] 批量插入失败，事务已回滚: {ex}");
                            return false;
                        }
                    }

                }
                else if (SqlimgCount > 0)
                {
                    Console.WriteLine(
                        $"[Images入库] FolderId={sqlFolderId} 已存在 {SqlimgCount} 张图片，跳过重复插入");
                    return true;
                }
                else
                {
                    Console.WriteLine($"[Images入库] 查询 Images 数量失败：{sqlHelper.GetLastError()}");
                    return false;
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
            ResultProcessingVisibility = Visibility.Collapsed;
            DamageDataList = null;
            _damagePointsCacheByFileName.Clear();
            _damageImageFileNameSet.Clear();
            GetJsonResultCommand.NotifyCanExecuteChanged();
        }


        [ObservableProperty]
        private Visibility imgProgressVisibility = Visibility.Collapsed;

        // 判伤进度结束后、正式开始绘制带框图片前显示的醒目提示。
        // 只控制界面提示，不改变分析、入库、缩略图、绘图流程。
        [ObservableProperty]
        private Visibility resultProcessingVisibility = Visibility.Collapsed;

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
        async Task SaveAllBoxSelectedImg(
    List<string> imgFiles,
    string savePath,
    bool adjust = false,
    CancellationToken ct = default)
        {
            Directory.CreateDirectory(savePath);

            bool hideFishScale = Settings.Default.IsConcealFishScale;

            var filesToProcess = imgFiles
                .Where(File.Exists)
                .Select(f => new
                {
                    Src = f,
                    Dst = Path.Combine(savePath, Path.GetFileName(f))
                })
                .Where(x => !adjust || !File.Exists(x.Dst))
                .ToList();

            // 初始化进度（UI）
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                ImgProgressVisibility = Visibility.Visible;
                CurrentImgProgress = 0;
                TotalImgProgress = filesToProcess.Count;
            });

            // 渲染结果通道：UI线程生产，后台线程消费保存
            var channel = Channel.CreateBounded<(string path, BitmapFrame frame)>(
                new BoundedChannelOptions(capacity: 16)
                {
                    SingleWriter = true,          // UI 线程单生产者
                    SingleReader = false,         // 多消费者
                    FullMode = BoundedChannelFullMode.Wait
                });

            int savedCount = 0;

            // 启动后台保存 workers
            int workerCount = Math.Clamp(Environment.ProcessorCount, 2, 6); // 过多反而抢 CPU
            var workers = Enumerable.Range(0, workerCount).Select(_ => Task.Run(async () =>
            {
                await foreach (var item in channel.Reader.ReadAllAsync(ct))
                {
                    try
                    {
                        SaveBitmapToPng(item.path, item.frame);
                        Interlocked.Increment(ref savedCount);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"保存失败: {item.path} - {ex}");
                    }
                }
            }, ct)).ToArray();

            // UI线程逐张渲染并推送给后台保存
            try
            {
                for (int i = 0; i < filesToProcess.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();

                    var f = filesToProcess[i];

                    // ⚠️ 渲染必须在 UI 线程
                    var frame = await Application.Current.Dispatcher.InvokeAsync(
                        () => ProcessImage(f.Src, hideFishScale),
                        DispatcherPriority.Background, // 让 UI 输入/绘制更优先一点
                        ct);

                    frame.Freeze(); // 关键：跨线程保存必须 Freeze

                    // 写入队列（可能等待，避免内存爆）
                    await channel.Writer.WriteAsync((f.Dst, frame), ct);

                    // 更新进度（UI）
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        CurrentImgProgress = i + 1;
                    }, DispatcherPriority.Background, ct);

                    // ✅ 关键：让出 UI 线程一次，避免“无响应”
                    await Dispatcher.Yield(DispatcherPriority.Background);
                }
            }
            finally
            {
                channel.Writer.TryComplete();
                try { await Task.WhenAll(workers); } catch { /* ignore */ }

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    ImgProgressVisibility = Visibility.Collapsed;
                });
            }

            Debug.WriteLine($"图片伤损绘制并保存完成：{savedCount} 张");
        }
        // 提取图片处理方法，可在多线程中调用
        private BitmapFrame ProcessImage(string imgFile, bool hideFishScale)
        {
            var decoder = BitmapDecoder.Create(
                new Uri(imgFile),
                BitmapCreateOptions.IgnoreImageCache, // 防止缓存
                BitmapCacheOption.OnLoad               // 立即加载并脱离源流
            );
            BitmapSource sourceImage = decoder.Frames[0];
            if (sourceImage.Width <= 0 || sourceImage.Height <= 0)
                throw new InvalidOperationException($"图片 {imgFile} 尺寸无效");

            // 2. 创建并配置 DamageDisplayBox（必须在 UI 线程）
            var damageDisplay = new DamageDisplayBox();
            var damageData = GetDamagePointsAndIndex(imgFile); // 确保这是线程安全的

            damageDisplay.SourceImage = sourceImage;
            damageDisplay.DamagePoints = ConvertDamagePointsForDisplay(damageData.Item1);
            damageDisplay.ShowGuidelines = false;
            damageDisplay.HideFishScale = hideFishScale;

            // 3. 测量和布局（必须在 UI 线程）
            damageDisplay.Measure(new System.Windows.Size(sourceImage.Width, sourceImage.Height));
            damageDisplay.Arrange(new System.Windows.Rect(0, 0, sourceImage.Width, sourceImage.Height));
            damageDisplay.UpdateLayout();

            if (damageDisplay.ActualWidth <= 0 || damageDisplay.ActualHeight <= 0)
                throw new InvalidOperationException("控件尺寸无效");

            // 4. 渲染（必须在 UI 线程）
            var renderTarget = new RenderTargetBitmap(
                (int)damageDisplay.ActualWidth,
                (int)damageDisplay.ActualHeight,
                96, 96, PixelFormats.Pbgra32);
            renderTarget.Render(damageDisplay);
            var result = BitmapFrame.Create(renderTarget);
            renderTarget = null; // 切断引用
            return result;
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
            ImgProgressVisibility = Visibility.Visible;
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
                int displayCategoryId = DisplayDamageId(damagePoints[i][4]);

                if (IsHideNormalMarker == true)
                {
                    if (DamageIdToBrush(displayCategoryId) != Brushes.Green)
                    {
                        DetailsList.Add(
                            new DamageDetails
                            {
                                Id = i,
                                MarkerColor = DamageIdToBrush(displayCategoryId),
                                DamageCategory = DamageIdToDamageName(displayCategoryId) + $" {i} ",
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
                            MarkerColor = DamageIdToBrush(displayCategoryId),
                            DamageCategory = DamageIdToDamageName(displayCategoryId) + $" {i} ",
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

            var newItems = await Task.Run(() =>
            {
                var collection = new ObservableCollection<ImgInfo>();
                if (ThumbnailFiles != null)
                {
                    foreach (var file in ThumbnailFiles)
                    {
                        collection.Add(new ImgInfo { Path = file, Name = Path.GetFileName(file) });
                    }
                }
                return collection;
            });

            await dispatcher.InvokeAsync(
               () =>
               {
                   ThumbnailImgInfos = newItems;
                   //Growl.Info("缩略图初始化完成");
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
                    if (File.Exists(jsonPath))
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

                // 判伤进度条结束后，后续还要整理结果、初始化缩略图；
                // 这里先显示一个醒目提示，避免用户误以为软件卡住。
                ResultProcessingVisibility = Visibility.Visible;
                await Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);

                ShowThumbnails(Parameter);

                //await ProcessHF(DamageImgPaths, damageImgFolderName);
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

        [RelayCommand]
        private async Task SmartCompare()
        {
            try
            {
                // =========================
                // 1. 检查当前选中图片
                // =========================
                if (SelectedIndex < 0 ||
                    ThumbnailImgInfos == null ||
                    SelectedIndex >= ThumbnailImgInfos.Count)
                {
                    MessageBox.Warning("请先选择一张图片");
                    return;
                }

                string selectedImgPath = ThumbnailImgInfos[SelectedIndex].Path;
                if (string.IsNullOrWhiteSpace(selectedImgPath) || !File.Exists(selectedImgPath))
                {
                    MessageBox.Warning("当前选中图片路径无效");
                    return;
                }

                // 自动周期对比开始前只处理原始 id=52 图片，
                // 但对比完成后结果可能保持 id=52，也可能被后端改为 id=2。
                // 因此手动“查看周期对比图片”允许当前最终结果含 id=2 或 id=52。
                float[][] currentDamagePoints = GetDamagePointsForImage(selectedImgPath);

                var currentComparedLabels = currentDamagePoints
                    .Where(point =>
                        point != null &&
                        point.Length > 4 &&
                        ((int)point[4] == 2 || (int)point[4] == 52))
                    .Select(point => (int)point[4])
                    .Distinct()
                    .OrderBy(label => label)
                    .ToList();

                if (currentComparedLabels.Count == 0)
                {
                    MessageBox.Warning(
                        "当前选中图片不含 id=2 或 id=52，" +
                        "不属于可查看的焊缝周期对比结果");
                    return;
                }

                string currentComparedLabelText = string.Join(
                    "/",
                    currentComparedLabels.Select(label => $"id={label}"));

                // =========================
                // 2. 获取当前周期信息
                // 与自动周期对比一致：只使用线别、周期、起止里程；
                // 不使用 SelectedUpOrDown（上下行）和 SelectedRailType（股别）。
                // =========================
                string? curSelectedRouteLine = NeedSavedInfo?.RailWayInfo.SelectedRouteLine;
                string? curStartMileage = NeedSavedInfo?.RailWayInfo.StartMileage;
                string? curEndMileage = NeedSavedInfo?.RailWayInfo.EndMileage;
                int? curCycleNumber = NeedSavedInfo?.RailWayInfo.CycleNumber;

                if (string.IsNullOrWhiteSpace(curSelectedRouteLine) ||
                    curCycleNumber == null)
                {
                    MessageBox.Warning("当前线别或周期信息不完整");
                    return;
                }

                int prevCycleNumber = curCycleNumber.Value - 1;
                if (prevCycleNumber <= 0)
                {
                    MessageBox.Warning("当前是第一周期，无法查看上一周期图片");
                    return;
                }

                ParseFileName(
                    Path.GetFileName(selectedImgPath),
                    out int idx,
                    out string currentMileage);

                if (string.IsNullOrWhiteSpace(currentMileage) ||
                    ParseMileageToMeters(currentMileage) <= 0)
                {
                    MessageBox.Warning($"无法解析当前图片的里程：{currentMileage}");
                    return;
                }

                // =========================
                // 3. 后台查询并按自动周期对比逻辑选择最佳参考图
                // 查询条件：线别 + 上一周期 + 起止里程；
                // 候选类型：id=52 / id=6 / id=2；
                // 二次筛选：LineType相同 + 里程±5米；
                // 排序规则：优先52/6，没有再选2。
                // =========================
                var queryResult = await Task.Run(() =>
                {
                    using var cycleSqlHelper = new SQLHelper(Settings.Default.SqlPath);
                    cycleSqlHelper.EnsureConnectionOpen();

                    int currentFolderId = sqlFolderId;
                    if (currentFolderId <= 0 && !string.IsNullOrWhiteSpace(Parameter))
                    {
                        currentFolderId = cycleSqlHelper.GetFolderIdByPath(Parameter);
                    }

                    if (currentFolderId <= 0)
                    {
                        return (
                            BestWeld: (WeldPositionInfo?)null,
                            CurrentLineType: string.Empty,
                            PreviousLineType: string.Empty,
                            Error: "当前文件夹尚未正确关联数据库，无法读取 LineType");
                    }

                    List<WeldPositionInfo> weldPositions =
                        cycleSqlHelper.GetWeldPositionsByRouteAndCycle(
                            curSelectedRouteLine,
                            prevCycleNumber,
                            curStartMileage,
                            curEndMileage);

                    if (weldPositions == null || weldPositions.Count == 0)
                    {
                        return (
                            BestWeld: (WeldPositionInfo?)null,
                            CurrentLineType: string.Empty,
                            PreviousLineType: string.Empty,
                            Error: "按线别、上一周期和起止里程未找到候选焊缝图片");
                    }

                    string currentLineType =
                        cycleSqlHelper.GetNormalizedLineTypeByImagePath(
                            selectedImgPath,
                            currentFolderId);

                    if (string.IsNullOrWhiteSpace(currentLineType))
                    {
                        return (
                            BestWeld: (WeldPositionInfo?)null,
                            CurrentLineType: string.Empty,
                            PreviousLineType: string.Empty,
                            Error: "当前图片的 Images.LineType 为空");
                    }

                    Dictionary<long, string> previousLineTypes =
                        cycleSqlHelper.GetNormalizedLineTypesByImageIds(
                            weldPositions.Select(weld => weld.ImgId));

                    WeldPositionInfo? bestPreviousWeld =
                        SelectBestPreviousWeldPosition(
                            currentMileage,
                            currentLineType,
                            weldPositions,
                            previousLineTypes);

                    if (bestPreviousWeld == null)
                    {
                        return (
                            BestWeld: (WeldPositionInfo?)null,
                            CurrentLineType: currentLineType,
                            PreviousLineType: string.Empty,
                            Error: $"上一周期没有找到 LineType={currentLineType} 且里程±5米内的可用参考图");
                    }

                    previousLineTypes.TryGetValue(
                        bestPreviousWeld.ImgId,
                        out string? previousLineType);

                    return (
                        BestWeld: bestPreviousWeld,
                        CurrentLineType: currentLineType,
                        PreviousLineType: SQLHelper.NormalizeLineTypeForStorage(previousLineType),
                        Error: string.Empty);
                });

                if (!string.IsNullOrWhiteSpace(queryResult.Error))
                {
                    MessageBox.Warning(queryResult.Error);
                    return;
                }

                WeldPositionInfo? bestWeld = queryResult.BestWeld;
                if (bestWeld == null)
                {
                    MessageBox.Warning("未找到可用的上一周期参考图片");
                    return;
                }

                // =========================
                // 4. 加载当前图和自动逻辑选中的唯一最佳参考图
                // =========================
                BitmapImage currentImage = LoadCurrentImages(selectedImgPath, idx);
                if (currentImage == null)
                {
                    MessageBox.Warning("无法加载当前周期图片");
                    return;
                }

                BitmapImage compareImage = LoadWeldImage(bestWeld);
                if (compareImage == null)
                {
                    MessageBox.Warning("无法加载上一周期参考图片数据");
                    return;
                }

                var compareImages = new List<BitmapImage> { compareImage };
                var imagePreviewWindow = new ImagePreviewWindow(currentImage, compareImages)
                {
                    Owner = Application.Current.MainWindow
                };

                string selectedLabel;
                if (HasAnnotationType(bestWeld, 52) ||
                    HasAnnotationType(bestWeld, 6))
                {
                    selectedLabel = HasAnnotationType(bestWeld, 52)
                        ? "id=52"
                        : "id=6";
                }
                else
                {
                    selectedLabel = "id=2";
                }

                string currentTitle =
                    $"当前周期 - {currentMileage} - " +
                    $"当前结果={currentComparedLabelText} - " +
                    $"LineType={queryResult.CurrentLineType}";

                string compareTitle =
                    $"第{prevCycleNumber}周期最佳参考 - " +
                    $"里程={bestWeld.Mileage} - " +
                    $"LineType={queryResult.PreviousLineType} - {selectedLabel}";

                imagePreviewWindow.SetTitles(currentTitle, compareTitle);
                imagePreviewWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"智能对比失败: {ex}");
                MessageBox.Error($"智能对比失败: {ex.Message}");
            }
        }

        #region 辅助方法

        /// <summary>
        /// 加载当前图片）
        /// </summary>
        private BitmapImage LoadCurrentImages(string currentImagePath, int currentIdx)
        {
            var currentImage = new BitmapImage();

            try
            {
                currentImage = LoadImageFromFile(currentImagePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载当前图片失败: {ex.Message}");
            }

            return currentImage;
        }

        private BitmapFrame ProcessCompareImage(Stream imgStream, List<float[]> damage, bool isConcealFishScale)
        {
            // 1. 加载图片（非 UI 操作，但 BitmapFrame.Create 需要 Stream 参数）  
            BitmapFrame sourceImage = BitmapFrame.Create(imgStream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            if (sourceImage.Width <= 0 || sourceImage.Height <= 0)
                throw new InvalidOperationException($"图片尺寸无效");

            // 2. 创建并配置 DamageDisplayBox（必须在 UI 线程）  
            var damageDisplay = new DamageDisplayBox();
            damageDisplay.SourceImage = sourceImage;
            damageDisplay.DamagePoints = ConvertDamagePointsForDisplay(damage.ToArray());
            damageDisplay.ShowGuidelines = false;
            damageDisplay.HideFishScale = isConcealFishScale;

            // 3. 测量和布局（必须在 UI 线程）  
            damageDisplay.Measure(new System.Windows.Size(sourceImage.Width, sourceImage.Height));
            damageDisplay.Arrange(new System.Windows.Rect(0, 0, sourceImage.Width, sourceImage.Height));
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

        private BitmapImage LoadWeldImage(WeldPositionInfo weld)
        {
            try
            {
                if (weld == null || weld.ImageData == null || weld.ImageData.Length == 0)
                    return null;

                using (var originalStream = new MemoryStream(weld.ImageData))
                {
                    var bitmapFrame = IsShowAllBoxSelected == false ? BitmapFrame.Create(originalStream) : ProcessCompareImage(originalStream, weld.Annotations, Settings.Default.IsConcealFishScale);

                    using (var memoryStream = new MemoryStream())
                    {
                        BitmapEncoder encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(bitmapFrame);
                        encoder.Save(memoryStream);
                        memoryStream.Position = 0;

                        var bitmapImage = new BitmapImage();
                        bitmapImage.BeginInit();
                        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                        bitmapImage.StreamSource = memoryStream;
                        bitmapImage.EndInit();
                        bitmapImage.Freeze();
                        return bitmapImage;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载焊缝图片失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 从文件路径加载图片
        /// </summary>
        private BitmapImage LoadImageFromFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return null;

                var bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.UriSource = new Uri(filePath, UriKind.Absolute);
                bitmapImage.EndInit();
                bitmapImage.Freeze();
                return bitmapImage;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载图片文件失败: {filePath}, 错误: {ex.Message}");
                return null;
            }
        }
        // 预先创建查找字典
        Dictionary<string, DamageData> damageDict = new Dictionary<string, DamageData>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, DamageData> damageByFileName = new Dictionary<string, DamageData>(StringComparer.OrdinalIgnoreCase);

        public async Task ProcessHF(List<string> Imgfiles, [NotNull] string SavePath)
        {
            // =========================
            // 1. 获取当前周期信息
            // 自动周期对比不再使用 SelectedUpOrDown（上下行）。
            // =========================
            var curSelectedRouteLine = NeedSavedInfo?.RailWayInfo.SelectedRouteLine;
            var curCycleNumber = NeedSavedInfo?.RailWayInfo.CycleNumber;
            var curStartMileage = NeedSavedInfo?.RailWayInfo.StartMileage;
            var curEndMileage = NeedSavedInfo?.RailWayInfo.EndMileage;

            if (string.IsNullOrWhiteSpace(curSelectedRouteLine) ||
                curCycleNumber == null)
            {
                Console.WriteLine("错误：未设置当前线别或周期信息，不执行周期对比");
                return;
            }

            // =========================
            // 2. 第一周期不进行周期对比
            // =========================
            var prevCycleNumber = curCycleNumber - 1;
            if (!prevCycleNumber.HasValue || prevCycleNumber <= 0)
            {
                Console.WriteLine($"当前是第 {curCycleNumber} 周期，没有上一周期，不执行周期对比");
                return;
            }

            Console.WriteLine(
                $"【周期对比】筛选条件=线别+周期+里程区间；不使用上下行和股别；" +
                $"LineType相同后才进入±5米和置信度排序。" +
                $"当前起止={curStartMileage ?? "NULL"} -> {curEndMileage ?? "NULL"}");

            if (DamageDataList == null || DamageDataList.Count == 0)
            {
                Console.WriteLine("DamageDataList 为空，不执行周期对比");
                return;
            }

            if (imgFiles == null || imgFiles.Length == 0)
            {
                Console.WriteLine("imgFiles 为空，不执行周期对比");
                return;
            }

            if (sqlFolderId <= 0)
            {
                Console.WriteLine($"当前文件夹数据库ID无效：sqlFolderId={sqlFolderId}，不执行周期对比");
                return;
            }

            using var cycleSqlHelper = new SQLHelper(Settings.Default.SqlPath);
            cycleSqlHelper.EnsureConnectionOpen();

            // =========================
            // 3. 查询上一周期所有焊缝记录
            // 查询规则仍保留 id=52 / id=6 / id=2，
            // 但数据库文件夹筛选只使用线别、上一周期和里程区间。
            // =========================
            List<WeldPositionInfo> weldPositions =
                cycleSqlHelper.GetWeldPositionsByRouteAndCycle(
                    curSelectedRouteLine,
                    prevCycleNumber.Value,
                    curStartMileage,
                    curEndMileage);

            if (weldPositions == null || weldPositions.Count == 0)
            {
                Console.WriteLine("未找到上一周期焊缝数据，不执行周期对比");
                return;
            }

            // 一次性读取上一周期候选图的 LineType，避免循环内重复查库。
            Dictionary<long, string> previousLineTypes =
                cycleSqlHelper.GetNormalizedLineTypesByImageIds(
                    weldPositions.Select(weld => weld.ImgId));

            // =========================
            // 4. 找出当前周期所有含 id=52 的图片
            // =========================
            var current52Images = DamageDataList
                .Where(dd =>
                    dd != null &&
                    !string.IsNullOrEmpty(dd.Url) &&
                    dd.DamagePoint != null &&
                    dd.DamagePoint.Any(point =>
                        point != null &&
                        point.Length > 4 &&
                        (int)point[4] == 52))
                .GroupBy(dd => Path.GetFileName(dd.Url), StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();

            if (current52Images.Count == 0)
            {
                Console.WriteLine("当前周期没有找到 id=52 图片，不执行周期对比");
                return;
            }

            // =========================
            // 5. 初始化进度条
            // =========================
            InitializeCycleCompareProgress(current52Images.Count);
            int processedImageCount = 0;

            try
            {
                // =========================
                // 6. 当前周期 id=52 图片逐张选择上一周期最佳参考对象
                // =========================
                foreach (var currentDamageData in current52Images)
                {
                    try
                    {
                        processedImageCount++;
                        UpdateCycleCompareProgress(
                            processedImageCount,
                            current52Images.Count);

                        string currentImagePath = currentDamageData.Url;
                        string currentFileName = Path.GetFileName(currentImagePath);

                        if (string.IsNullOrWhiteSpace(currentImagePath) ||
                            !File.Exists(currentImagePath))
                        {
                            Console.WriteLine($"当前图片路径无效，跳过: {currentImagePath}");
                            continue;
                        }

                        ParseFileName(
                            currentFileName,
                            out int idx,
                            out string currentMileage);

                        if (string.IsNullOrWhiteSpace(currentMileage))
                        {
                            Console.WriteLine($"当前图片无法解析里程，跳过: {currentFileName}");
                            continue;
                        }

                        // 当前52图片的LineType从Images表读取。
                        // SQLHelper读取后会再次执行“只保留汉字”规范化。
                        string currentLineType =
                            cycleSqlHelper.GetNormalizedLineTypeByImagePath(
                                currentImagePath,
                                sqlFolderId);

                        if (string.IsNullOrWhiteSpace(currentLineType))
                        {
                            Console.WriteLine(
                                $"当前图片 {currentFileName} 的 Images.LineType 为空，跳过周期对比");
                            continue;
                        }

                        // 必须同时满足：LineType相同 + 里程±5米。
                        // 之后仍按原规则优先52/6，找不到再选2。
                        WeldPositionInfo? bestPreviousWeld =
                            SelectBestPreviousWeldPosition(
                                currentMileage,
                                currentLineType,
                                weldPositions,
                                previousLineTypes);

                        if (bestPreviousWeld == null)
                        {
                            Console.WriteLine(
                                $"当前图片 {currentFileName}，LineType={currentLineType}，" +
                                "在上一周期未找到LineType相同且±5米内的可用参考对象");
                            continue;
                        }

                        previousLineTypes.TryGetValue(
                            bestPreviousWeld.ImgId,
                            out string? previousLineType);

                        Console.WriteLine(
                            $"当前图片 {currentFileName} 里程={currentMileage}，" +
                            $"LineType={currentLineType}，" +
                            $"选择上一周期参考={bestPreviousWeld.Mileage}，" +
                            $"上一周期LineType={previousLineType ?? string.Empty}，" +
                            $"是否含52={HasAnnotationType(bestPreviousWeld, 52)}，" +
                            $"id52最大权重={GetMaxAnnotationConfidence(bestPreviousWeld, 52):F5}，" +
                            $"是否含6={HasAnnotationType(bestPreviousWeld, 6)}，" +
                            $"id6最大权重={GetMaxAnnotationConfidence(bestPreviousWeld, 6):F5}，" +
                            $"id2最大权重={GetMaxAnnotationConfidence(bestPreviousWeld, 2):F5}");

                        await CheckAndSendToBackendByPathAsync(
                            currentImagePath,
                            bestPreviousWeld);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(
                            $"处理当前图片 {currentDamageData?.Url} 失败: {ex.Message}");
                    }

                    await Task.Delay(200);
                }
            }
            finally
            {
                CompleteCycleCompareProgress();
            }

            // 周期对比过程中后端会用新数组替换 DamageDataList.DamagePoint。
            // 在保存 result.json 和后续绘图前统一重建缓存，确保所有入口读取到最新标签。
            BuildDisplayCaches();
            Console.WriteLine("【周期对比】全部处理完成，已根据最新 DamageDataList 重建绘图缓存");

            // =========================
            // 7. 保存周期对比后的 result.json，并删除 out 缓存图
            // =========================
            lock (_fileAndDataAccessLock)
            {
                AboutJson.SaveJson<List<DamageData>>(
                    DamageDataList,
                    Path.Combine(Settings.Default.InPath, ImgFolderName),
                    "result.json");

                var outPath = Settings.Default.OutPath;
                var outFolderPath = Path.Combine(outPath, ImgFolderName);

                if (Directory.Exists(outFolderPath))
                {
                    try
                    {
                        Directory.Delete(outFolderPath, true);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"删除输出目录中的文件夹失败: {ex.Message}");
                    }
                }
            }
        }

        private bool HasAnnotationType(WeldPositionInfo weld, int targetType)
        {
            if (weld?.Annotations == null)
                return false;

            return weld.Annotations.Any(point =>
                point != null &&
                point.Length > 4 &&
                (int)point[4] == targetType);
        }

        private float GetMaxAnnotationConfidence(WeldPositionInfo weld, int targetType)
        {
            if (weld?.Annotations == null)
                return -1f;

            return weld.Annotations
                .Where(point =>
                    point != null &&
                    point.Length > 5 &&
                    (int)point[4] == targetType)
                .Select(point => point[5])
                .DefaultIfEmpty(-1f)
                .Max();
        }

        /// <summary>
        /// 自动周期对比时，按当前图片里程在上一周期 ±5 米范围内选择最佳参考图。
        /// 按 demo.py 默认 single 流程：不再区分 single/double，不再按上下半区选择参考图。
        /// 优先上一周期 id=52/id=6，没有再选 id=2。
        /// </summary>
        private WeldPositionInfo? SelectBestPreviousWeldPosition(
            string currentMileage,
            string currentLineType,
            List<WeldPositionInfo> weldPositions,
            IReadOnlyDictionary<long, string> previousLineTypes)
        {
            string normalizedCurrentLineType =
                SQLHelper.NormalizeLineTypeForStorage(currentLineType);

            if (string.IsNullOrWhiteSpace(currentMileage) ||
                string.IsNullOrWhiteSpace(normalizedCurrentLineType) ||
                weldPositions == null ||
                weldPositions.Count == 0)
            {
                return null;
            }

            float currentMeters = ParseMileageToMeters(currentMileage);
            if (currentMeters <= 0)
            {
                Console.WriteLine($"当前里程解析失败: {currentMileage}");
                return null;
            }

            var candidates = weldPositions
                .Select(weld =>
                {
                    float weldMeters = ParseMileageToMeters(weld.Mileage);
                    float distance = weldMeters > 0
                        ? Math.Abs(weldMeters - currentMeters)
                        : float.MaxValue;

                    previousLineTypes.TryGetValue(
                        weld.ImgId,
                        out string? previousLineType);

                    string normalizedPreviousLineType =
                        SQLHelper.NormalizeLineTypeForStorage(previousLineType);

                    return new
                    {
                        Weld = weld,
                        Distance = distance,
                        PreviousLineType = normalizedPreviousLineType,
                        IsSameLineType = string.Equals(
                            normalizedPreviousLineType,
                            normalizedCurrentLineType,
                            StringComparison.Ordinal),
                        Has52 = HasAnnotationType(weld, 52),
                        Has6 = HasAnnotationType(weld, 6),
                        Has2 = HasAnnotationType(weld, 2),
                        Max52Confidence = GetMaxAnnotationConfidence(weld, 52),
                        Max6Confidence = GetMaxAnnotationConfidence(weld, 6),
                        Max2Confidence = GetMaxAnnotationConfidence(weld, 2)
                    };
                })
                .Where(candidate =>
                    candidate.IsSameLineType &&
                    candidate.Distance <= 5)
                .ToList();

            if (candidates.Count == 0)
            {
                return null;
            }

            // 原规则保持不变：先选含52或6的候选。
            var best52Or6 = candidates
                .Where(candidate => candidate.Has52 || candidate.Has6)
                .OrderByDescending(candidate =>
                    Math.Max(
                        candidate.Max52Confidence,
                        candidate.Max6Confidence))
                .ThenBy(candidate => candidate.Distance)
                .FirstOrDefault();

            if (best52Or6 != null)
            {
                return best52Or6.Weld;
            }

            // ±5米且LineType相同的候选中没有52/6时，仍按原规则选id=2。
            var best2Original = candidates
                .Where(candidate => candidate.Has2)
                .OrderByDescending(candidate => candidate.Max2Confidence)
                .ThenBy(candidate => candidate.Distance)
                .FirstOrDefault();

            return best2Original?.Weld;
        }

        /// <summary>
        /// 从上一周期图片的全部标注中，选择真正发送给 hf_db 的单个焊缝参考框。
        /// 保留当前周期对比规则：优先 id=52/id=6，找不到再选 id=2；同类取置信度最高的框。
        /// </summary>
        private float[]? SelectPreviousReferenceAnnotation(WeldPositionInfo weld)
        {
            if (weld?.Annotations == null || weld.Annotations.Count == 0)
            {
                return null;
            }

            var validAnnotations = weld.Annotations
                .Where(point => point != null && point.Length >= 6)
                .ToList();

            var defectReference = validAnnotations
                .Where(point =>
                {
                    int label = (int)point[4];
                    return label == 52 || label == 6;
                })
                .OrderByDescending(point => point[5])
                .FirstOrDefault();

            if (defectReference != null)
            {
                return defectReference;
            }

            return validAnnotations
                .Where(point => (int)point[4] == 2)
                .OrderByDescending(point => point[5])
                .FirstOrDefault();
        }

        private async Task CheckAndSendToBackendByPathAsync(
            string currentImagePath,
            WeldPositionInfo weldPosition)
        {
            string processId = Guid.NewGuid().ToString().Substring(0, 8);

            try
            {
                if (string.IsNullOrWhiteSpace(currentImagePath) || !File.Exists(currentImagePath))
                {
                    Console.WriteLine($"[{processId}] 当前图片路径无效: {currentImagePath}");
                    return;
                }

                if (weldPosition == null)
                {
                    Console.WriteLine($"[{processId}] 上一周期参考对象为空，跳过");
                    return;
                }

                var referenceAnnotation = SelectPreviousReferenceAnnotation(weldPosition);
                if (referenceAnnotation == null)
                {
                    Console.WriteLine(
                        $"[{processId}] 上一周期图片没有可用的 id=52/id=6/id=2 焊缝框，跳过周期对比");
                    return;
                }

                string allPreviousAnnotations = weldPosition.Annotations == null
                    ? "无标注"
                    : string.Join(
                        " | ",
                        weldPosition.Annotations.Select((point, index) =>
                            point != null && point.Length > 5
                                ? $"#{index}:Label={(int)point[4]},Conf={point[5]:F5},BBox=[{point[0]},{point[1]},{point[2]},{point[3]}]"
                                : $"#{index}:无效数据"));

                Console.WriteLine($"[{processId}] 上一周期数据库全部标注：{allPreviousAnnotations}");
                Console.WriteLine(
                    $"[{processId}] 实际发送上一周期参考框：" +
                    $"Label={(int)referenceAnnotation[4]}，Conf={referenceAnnotation[5]:F5}，" +
                    $"BBox=[{referenceAnnotation[0]},{referenceAnnotation[1]},{referenceAnnotation[2]},{referenceAnnotation[3]}]");

                // =========================
                // 1. 保存上一周期参考图到临时目录
                // =========================
                string tempDir = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Resources",
                    "Temp");

                if (!Directory.Exists(tempDir))
                {
                    Directory.CreateDirectory(tempDir);
                }

                string previousFileNameWithoutExt = string.IsNullOrWhiteSpace(weldPosition.ImagePath)
                    ? "previous_ref"
                    : Path.GetFileNameWithoutExtension(weldPosition.ImagePath);

                string previousExt = string.IsNullOrWhiteSpace(weldPosition.ImagePath)
                    ? ".png"
                    : Path.GetExtension(weldPosition.ImagePath);

                if (string.IsNullOrWhiteSpace(previousExt))
                {
                    previousExt = ".png";
                }

                string tempFileName =
                    $"{previousFileNameWithoutExt}_{processId}{previousExt}";

                string tempPath = Path.Combine(tempDir, tempFileName);

                if (weldPosition.ImageData != null && weldPosition.ImageData.Length > 0)
                {
                    lock (_fileAndDataAccessLock)
                    {
                        File.WriteAllBytes(tempPath, weldPosition.ImageData);
                    }
                }
                else
                {
                    Console.WriteLine($"[{processId}] 上一周期参考图 ImageData 为空，跳过");
                    return;
                }

                // =========================
                // 2. 获取当前图片的 DamagePoint
                // =========================
                float[][] damagePoints;

                lock (_damageDataLock)
                {
                    damagePoints = GetDamagePointsForImage(currentImagePath);
                }

                // =========================
                // 3. 组装发送给后端的数据
                //
                // 后端 demo.py 的 /db_compare 只接收 input_text。
                // raw_data[0:3] = 当前周期待测图
                // raw_data[3]   = 上一周期参考图
                // =========================
                var firstSendList = new List<object>();

                // raw_data[0]：当前周期 id=52 图片
                firstSendList.Add(new
                {
                    url = currentImagePath,
                    damage = damagePoints ?? Array.Empty<float[]>()
                });

                // raw_data[1]、raw_data[2]：保持空位
                for (int i = 0; i < 2; i++)
                {
                    firstSendList.Add(new
                    {
                        url = (string?)null,
                        damage = Array.Empty<float[]>()
                    });
                }

                // raw_data[3]：上一周期参考图。
                // 只发送一个已经明确选中的焊缝参考框，避免 hf_db 使用 damage[0] 时取到其他标注。
                float[][] annotationArray =
                {
                    (float[])referenceAnnotation.Clone()
                };

                firstSendList.Add(new
                {
                    url = tempPath,
                    damage = annotationArray
                });

                // =========================
                // 4. 发送后端 db_compare
                // =========================
                string apiUrl = "http://127.0.0.1:3333/db_compare";
                var backendResponse = await SendToBackendAsync(apiUrl, firstSendList, processId);

                if (backendResponse?.Status == "success")
                {
                    lock (_damageDataLock)
                    lock (_fileAndDataAccessLock)
                    {
                        ProcessResultFileSync(
                            backendResponse.OutputFile,
                            tempPath,
                            0,
                            imgFiles,
                            processId);
                    }
                }
                else
                {
                    Console.WriteLine($"[{processId}] 后端周期对比失败或无返回");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{processId}] ❌ 按路径发送周期对比失败: {ex.Message}");
            }
        }

        private List<string> FindImagesByMileage(string mileage, string[] imageFiles)
        {
            var result = new List<string>();

            if (string.IsNullOrEmpty(mileage) || imageFiles == null || imageFiles.Length == 0)
                return result;



            // 解析目标里程
            float targetMeters = ParseMileageToMeters(mileage);
            bool needRangeMatch = targetMeters > 0;

            var resultSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var file in imageFiles)
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                var fullFileName = Path.GetFileName(file);

                // 快速检查是否有损伤数据
                bool hasDamageData = damageDict.ContainsKey(file) ||
                                    damageByFileName.ContainsKey(fullFileName);

                if (!hasDamageData)
                    continue;

                // 1. 直接匹配
                if (fileName.Contains(mileage))
                {
                    if (resultSet.Add(file))
                    {
                        result.Add(file);
                    }
                    continue;
                }

                // 2. 范围匹配
                if (needRangeMatch)
                {
                    float fileMeters = ParseMileageToMeters(fileName);
                    if (fileMeters > 0 && Math.Abs(fileMeters - targetMeters) <= 5)
                    {
                        if (resultSet.Add(file))
                        {
                            result.Add(file);
                        }
                    }
                }
            }
            return result;
        }

        private async Task<BackendApiResponse> SendToBackendAsync(string apiUrl, List<object> sendList, string processId)
        {
            try
            {
                // 每次都创建新的 HttpClient 实例
                using var httpClient = new HttpClient
                {
                    // 一次性设置所有属性
                    BaseAddress = new Uri("http://127.0.0.1:3333/"),
                    Timeout = TimeSpan.FromSeconds(30)
                };

                // 设置默认请求头（如果需要）
                httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

                using var formData = new MultipartFormDataContent();

                string sendListJson = JsonSerializer.Serialize(sendList);
                formData.Add(new StringContent(sendListJson, Encoding.UTF8, "application/json"), "input_text");

                var response = await httpClient.PostAsync("db_compare", formData);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    return JsonSerializer.Deserialize<BackendApiResponse>(responseContent);
                }
                else
                {
                    Console.WriteLine($"[{processId}] ❌ HTTP请求失败: {response.StatusCode}");
                }
            }
            catch (HttpRequestException httpEx)
            {
                Console.WriteLine($"[{processId}] ❌ HTTP请求异常: {httpEx.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{processId}] ❌ 发送请求失败: {ex.Message}");
            }

            return null;
        }
        private void ProcessResultFileSync(string resultFilePath, string tempPath, int currentIdx, string[] imgFiles, string processId)
        {
            // ============ 注意：这个方法现在只在锁内被调用 ============
            try
            {
                string jsonContent;

                lock (_fileAndDataAccessLock)
                {
                    jsonContent = File.ReadAllText(resultFilePath);
                }

                var resultEntries = JsonSerializer.Deserialize<List<DamageData>>(jsonContent);
                if (resultEntries == null || resultEntries.Count == 0)
                {
                    Console.WriteLine($"[{processId}] 周期对比结果为空，未写回数据");
                    return;
                }

                // 后端最后一项是上一周期参考图，只处理前面的当前周期数据。
                var dataEntries = resultEntries.SkipLast(1).ToList();

                foreach (var dataEntry in dataEntries)
                {
                    if (dataEntry == null || string.IsNullOrWhiteSpace(dataEntry.Url))
                    {
                        continue;
                    }

                    string fileName = Path.GetFileName(dataEntry.Url);
                    float[][] updatedDamagePoints = dataEntry.DamagePoint ?? Array.Empty<float[]>();

                    // 1. 先写回 DamageDataList，并立即刷新该图片的绘图缓存。
                    // 旧逻辑只替换 DamagePoint，但缓存仍指向旧数组，会导致后端判成2后绘图仍显示52。
                    var originalData = DamageDataList?.FirstOrDefault(x =>
                        x != null &&
                        !string.IsNullOrEmpty(x.Url) &&
                        Path.GetFileName(x.Url).Equals(fileName, StringComparison.OrdinalIgnoreCase));

                    if (originalData != null)
                    {
                        originalData.DamagePoint = updatedDamagePoints;

                        int originalIndex = DamageDataList.IndexOf(originalData);
                        _damagePointsCacheByFileName[fileName] =
                            (originalData.DamagePoint, originalIndex);
                        _damageImageFileNameSet.Add(fileName);

                        string labels = string.Join(
                            ",",
                            updatedDamagePoints
                                .Where(point => point != null && point.Length > 4)
                                .Select(point => ((int)point[4]).ToString()));

                        Console.WriteLine(
                            $"[{processId}] 已写回 DamageDataList 并刷新绘图缓存：" +
                            $"图片={fileName}，标签=[{labels}]");
                    }
                    else
                    {
                        Console.WriteLine(
                            $"[{processId}] 未在 DamageDataList 中找到返回图片：{fileName}");
                    }

                    // 2. 使用后端最新结果更新数据库标注。
                    var imgInfo = SqlImgInfos?.FirstOrDefault(x =>
                        !string.IsNullOrWhiteSpace(x.ImgPath) &&
                        Path.GetFileName(x.ImgPath).Equals(fileName, StringComparison.OrdinalIgnoreCase));

                    if (imgInfo != null)
                    {
                        UpdateDatabaseAnnotations(
                            imgInfo,
                            updatedDamagePoints.ToList(),
                            processId);
                    }

                    // 3. 界面分类直接使用后端返回的新标签，
                    // 不再先读取数据库中的周期对比前旧标签。
                    if (imgInfo != null)
                    {
                        try
                        {
                            var damageTypeIds = updatedDamagePoints
                                .Where(point => point != null && point.Length > 4)
                                .Select(point => DisplayDamageId(point[4]))
                                .Distinct()
                                .ToList();

                            if (damageTypeIds.Count == 0)
                            {
                                imgInfo.DamageType = "无伤损";
                            }
                            else
                            {
                                var damageTypeNames = damageTypeIds
                                    .Select(damageTypeId =>
                                        Records.DamageCategoryData
                                            .FirstOrDefault(r => r.Id == damageTypeId)
                                            ?.CategoryName
                                        ?? $"未知({damageTypeId})")
                                    .ToList();

                                imgInfo.DamageType = string.Join(",", damageTypeNames);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[{processId}] 更新界面伤损类型失败: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{processId}] ❌ 处理结果文件时出错: {ex.Message}");
            }
        }
        private void UpdateDatabaseAnnotations(SqlImgInfo imgInfo, List<float[]> damagePoints, string processId)
        {
            try
            {
                using (var sqlHelper = new SQLHelper(Settings.Default.SqlPath))
                {
                    // 先删除原有的伤损标注
                    sqlHelper.DeleteDamageAnnotationsByImagePath(imgInfo.ImgPath);

                    // 再插入新的伤损标注
                    if (damagePoints != null && damagePoints.Count != 0)
                    {
                        foreach (var point in damagePoints)
                        {
                            if (point.Length >= 5)
                            {
                                var annotation = new DamageAnnotation
                                {
                                    ImageId = imgInfo.ImgId,
                                    X = point[0],
                                    Y = point[1],
                                    Width = point[2],
                                    Height = point[3],
                                    DamageType = (int)point[4],
                                    Confidence = point.Length > 5 ? point[5] : 0.95f,
                                    CreatedTime = DateTime.Now,
                                    UpdatedTime = DateTime.Now,
                                    ImagePath = imgInfo.ImgPath
                                };

                                var annotationPara = new Dictionary<string, object>
                                {
                                    { "@ImageId", annotation.ImageId },
                                    { "@X", annotation.X },
                                    { "@Y", annotation.Y },
                                    { "@Width", annotation.Width },
                                    { "@Height", annotation.Height },
                                    { "@DamageType", annotation.DamageType },
                                    { "@Confidence", annotation.Confidence },
                                    { "@CreatedTime", annotation.CreatedTime },
                                    { "@UpdatedTime", annotation.UpdatedTime },
                                    { "@ImagePath", annotation.ImagePath }
                                };

                                sqlHelper.InsertTable(annotationPara, "DamageAnnotations");
                            }
                        }

                        //Console.WriteLine($"[{processId}] ✅ 已更新数据库标注: {imgInfo.ImgPath}, {damagePoints.Count} 个标注");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{processId}] ❌ 更新数据库标注时出错: {ex.Message}");
            }
        }

        private static readonly object _resultFileLock = new object();
        private static readonly object _jsonSaveLock = new object();
        private static readonly object _fileAndDataAccessLock = new object();
        private readonly object _damageDataLock = new object();
        // 周期对比进度条属性
        private Visibility _cycleCompareProgressVisibility = Visibility.Collapsed;
        public Visibility CycleCompareProgressVisibility
        {
            get => _cycleCompareProgressVisibility;
            set => SetProperty(ref _cycleCompareProgressVisibility, value);
        }

        private int _totalCycleCompareProgress;
        public int TotalCycleCompareProgress
        {
            get => _totalCycleCompareProgress;
            set => SetProperty(ref _totalCycleCompareProgress, value);
        }

        private int _currentCycleCompareProgress;
        public int CurrentCycleCompareProgress
        {
            get => _currentCycleCompareProgress;
            set => SetProperty(ref _currentCycleCompareProgress, value);
        }

        private string _cycleCompareProgressText = "准备中...";
        public string CycleCompareProgressText
        {
            get => _cycleCompareProgressText;
            set => SetProperty(ref _cycleCompareProgressText, value);
        }

        // 进度条操作方法
        private void InitializeCycleCompareProgress(int total)
        {
            TotalCycleCompareProgress = total;
            CurrentCycleCompareProgress = 0;
            CycleCompareProgressText = $"0/{total} 焊缝";
            CycleCompareProgressVisibility = Visibility.Visible;
        }

        private void UpdateCycleCompareProgress(int current, int total)
        {
            CurrentCycleCompareProgress = current;
            CycleCompareProgressText = $"{current}/{total} 焊缝";

            // 可选：添加百分比显示
            if (total > 0)
            {
                double percentage = (current * 100.0) / total;
                CycleCompareProgressText = $"{current}/{total} 焊缝 ({percentage:F1}%)";
            }
        }

        private void CompleteCycleCompareProgress()
        {
            CycleCompareProgressVisibility = Visibility.Collapsed;
            CurrentCycleCompareProgress = 0;
            TotalCycleCompareProgress = 0;
            CycleCompareProgressText = "准备中...";
        }
        private async Task CheckAndSendToBackendAsync(int idx, string mileage, string[] imgFiles, WeldPositionInfo weldPosition)
        {
            string processId = Guid.NewGuid().ToString().Substring(0, 8);

            // 记录哪些文件需要重新处理
            var filesToProcess = new List<string>();
            var damagePointsCache = new Dictionary<string, float[][]>();

            try
            {
                // 临时文件夹路径
                string tempDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Temp");
                if (!Directory.Exists(tempDir))
                {
                    Directory.CreateDirectory(tempDir);
                }

                // 生成临时文件名
                string tempFileName = $"{Path.GetFileNameWithoutExtension(weldPosition.ImagePath)}_{processId}{Path.GetExtension(weldPosition.ImagePath)}";
                string tempPath = Path.Combine(tempDir, tempFileName);

                // 保存数据库图片到临时文件
                if (weldPosition.ImageData != null && weldPosition.ImageData.Length > 0)
                {
                    lock (_fileAndDataAccessLock)
                    {
                        File.WriteAllBytes(tempPath, weldPosition.ImageData);
                    }
                }
                else
                {
                    return;
                }

                // 第一次：发送当前数据
                var firstSendList = new List<object>();
                for (int i = 1; i <= 1; i++)
                {
                    var path = FindFilePathByIdx(idx, imgFiles);
                    if (path != null && File.Exists(path))
                    {
                        float[][] damagePoints;
                        lock (_damageDataLock)
                        {
                            damagePoints = GetDamagePointsForImage(path);
                            // 缓存原始数据以便比较
                            string fileName = Path.GetFileName(path);
                            damagePointsCache[fileName] = damagePoints;
                        }

                        filesToProcess.Add(path);
                        firstSendList.Add(new
                        {
                            url = path,
                            damage = damagePoints
                        });

                        //Console.WriteLine($"[{processId}] 第一次发送 {path} 的DamagePoint:");
                        //if (damagePoints != null)
                        //{
                        //    foreach (var point in damagePoints)
                        //    {
                        //        if (point.Length >= 5)
                        //        {
                        //            Console.WriteLine($"[{processId}]   类型: {point[4]}");
                        //        }
                        //    }
                        //}
                    }
                    else
                    {
                        firstSendList.Add(new
                        {
                            url = (string?)null,
                            damage = Array.Empty<float[]>()
                        });
                    }
                }

                for (int i = 0; i < 2; i++)
                {
                    firstSendList.Add(new
                    {
                        url = (string?)null,
                        damage = Array.Empty<float[]>()
                    });
                }

                // 添加对比周期图片。即使以后重新启用这个旧入口，也只发送一个明确的焊缝参考框。
                var referenceAnnotation = SelectPreviousReferenceAnnotation(weldPosition);
                if (referenceAnnotation == null)
                {
                    Console.WriteLine(
                        $"[{processId}] 上一周期图片没有可用的 id=52/id=6/id=2 焊缝框，跳过周期对比");
                    return;
                }

                float[][] annotationArray =
                {
                    (float[])referenceAnnotation.Clone()
                };

                firstSendList.Add(new
                {
                    url = tempPath,
                    damage = annotationArray
                });

                // 第一次发送
                string apiUrl = "http://127.0.0.1:3333/db_compare";
                var backendResponse = await SendToBackendAsync(apiUrl, firstSendList, processId);

                if (backendResponse?.Status == "success")
                {
                    // 处理结果
                    lock (_damageDataLock)
                        lock (_fileAndDataAccessLock)
                        {
                            ProcessResultFileSync(backendResponse.OutputFile, tempPath, idx, imgFiles, processId);
                        }

                    foreach (var filePath in filesToProcess)
                    {
                        string fileName = Path.GetFileName(filePath);
                        float[][] newDamagePoints = GetDamagePointsForImage(filePath);
                        float[][] oldDamagePoints = damagePointsCache.ContainsKey(fileName) ?
                            damagePointsCache[fileName] : Array.Empty<float[]>();

                        //bool hasChanged = !AreDamagePointsEqual(oldDamagePoints, newDamagePoints);

                        //if (hasChanged)
                        //{
                        //    Console.WriteLine($"[{processId}] ⚠️  {fileName} 数据已变化，需要重新处理");
                        //    Console.WriteLine($"[{processId}]   旧数据:");
                        //    foreach (var point in oldDamagePoints)
                        //    {
                        //        if (point.Length >= 5)
                        //        {
                        //            Console.WriteLine($"[{processId}]     类型: {point[4]}");
                        //        }
                        //    }
                        //    Console.WriteLine($"[{processId}]   新数据:");
                        //    foreach (var point in newDamagePoints)
                        //    {
                        //        if (point.Length >= 5)
                        //        {
                        //            Console.WriteLine($"[{processId}]     类型: {point[4]}");
                        //        }
                        //    }
                        //}
                        //else
                        //{
                        //    Console.WriteLine($"[{processId}] ✅  {fileName} 数据未变化");
                        //}
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{processId}] ❌ 处理过程中出错: {ex.Message}");
            }
        }

        //private string? FindFilePathByIdx(int idx, string[] imgFiles)
        //{
        //    if (imgFiles == null)
        //        return null;

        //    foreach (var file in imgFiles)
        //    {
        //        var fileName = Path.GetFileNameWithoutExtension(file);

        //        // 尝试从文件名中提取索引
        //        // 支持格式："299KM433M_149.png" 或 "149.png"
        //        var match = Regex.Match(fileName, @"\D*(\d+)$");
        //        if (match.Success)
        //        {
        //            var fileIdx = int.Parse(match.Groups[1].Value);
        //            if (fileIdx == idx)
        //            {
        //                return file;
        //            }
        //        }
        //    }

        //    return null;
        //}
        private Dictionary<int, string>? _fileIndexCache;
        private string[]? _cachedImgFiles;

        private string? FindFilePathByIdx(int idx, string[] imgFiles)
        {
            if (imgFiles == null)
                return null;

            // 如果缓存不存在或文件数组已改变，重建缓存
            if (_fileIndexCache == null || !ReferenceEquals(_cachedImgFiles, imgFiles))
            {
                BuildFileIndexCache(imgFiles);
            }

            // 直接从字典查找
            return _fileIndexCache!.TryGetValue(idx, out var path) ? path : null;
        }

        private void BuildFileIndexCache(string[] imgFiles)
        {
            var indexCache = new Dictionary<int, string>();

            foreach (var file in imgFiles)
            {
                var fileName = Path.GetFileNameWithoutExtension(file);

                // 优化：使用 ValueSpan 避免字符串分配
                var match = Regex.Match(fileName, @"\D*(\d+)$");
                if (match.Success && int.TryParse(match.Groups[1].ValueSpan, out int fileIdx))
                {
                    // 注意：如果有重复索引，这里会覆盖之前的
                    indexCache[fileIdx] = file;
                }
            }

            _fileIndexCache = indexCache;
            _cachedImgFiles = imgFiles;
        }

        private float[][] GetDamagePointsForImage(string imagePath)
        {
            try
            {
                if (DamageDataList != null)
                {
                    // 首先从DamageDataList查找最新的数据
                    var damageData = DamageDataList.FirstOrDefault(x =>
                        x != null &&
                        !string.IsNullOrEmpty(x.Url) &&
                        Path.GetFileName(x.Url) == Path.GetFileName(imagePath));

                    // 如果找到了数据，直接返回
                    if (damageData?.DamagePoint != null)
                    {
                        return damageData.DamagePoint.ToArray();
                    }

                    // 如果DamageDataList中没有，尝试从_imgInfos中获取
                    var imgInfo = SqlImgInfos.FirstOrDefault(x =>
                        !string.IsNullOrEmpty(x.ImgPath) &&
                        Path.GetFileName(x.ImgPath) == Path.GetFileName(imagePath));

                    if (imgInfo != null)
                    {
                        // 从数据库获取最新的标注
                        using (var sqlHelper = new SQLHelper(Settings.Default.SqlPath))
                        {
                            var annotations = sqlHelper.GetDamageAnnotationsByImagePath(imgInfo.ImgPath);
                            if (annotations != null && annotations.Count != 0)
                            {
                                return annotations.Select(a => new float[]
                                {
                            a.X, a.Y, a.Width, a.Height,
                            (float)a.DamageType, a.Confidence
                                }).ToArray();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"获取伤损点数据失败: {ex.Message}");
            }
            return Array.Empty<float[]>();
        }
        /// <summary>
        /// 执行方法后在wpath处生成result.json文件
        /// </summary>
        /// <param name="url">发送请求地址</param>
        /// <param name="rpath">读取文件夹的位置</param>
        /// <param name="wpath">写入result.json文件的位置</param>
        /// <returns>保存result的路径</returns>
        public static string railClass = "single";

        async Task<string> GetJsonFile(string url, string rpath, string wpath)
        {
            var curInstruments = MainWindowViewModel.NeedSavedInfo?.RailWayInfo.Instruments;
            railClass = "single";

            if (curInstruments == "8C" ||
                curInstruments == "8D" ||
                curInstruments == "19型" ||
                curInstruments == "gt-20" ||
                curInstruments == "6M")
            {
                railClass = "single";
            }
            else if (curInstruments == "双轨501" ||
                     curInstruments == "双轨502")
            {
                railClass = "double";
            }

            Console.WriteLine($"GetJsonFile: Instruments=[{curInstruments ?? "NULL"}]，正常分析传 rail_class=[{railClass}]；周期对比不传 rail_class");

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
            if (!string.IsNullOrWhiteSpace(ImgPath))
            {
                var fileName = Path.GetFileName(ImgPath);
                if (!string.IsNullOrWhiteSpace(fileName) &&
                    _damagePointsCacheByFileName.TryGetValue(fileName, out var cached))
                {
                    return cached;
                }
            }

            var DamageData = DamageDataList
                .Where(x => Path.GetFileName(x.Url) == Path.GetFileName(ImgPath)).FirstOrDefault();

            var result = DamageData?.DamagePoint;
            var index = DamageDataList.IndexOf(DamageData);
            return (result, index);
        }


        bool CanShowAllBoxSelected => ThumbnailImgInfos != null && ThumbnailImgInfos.Count != 0;
        [RelayCommand(CanExecute = nameof(CanShowAllBoxSelected))]
        public void ShowAllBoxSelected(string str = "")
        {
            if (str == "键盘触发")
                IsShowAllBoxSelected = !IsShowAllBoxSelected;

            if (IsFilterThumbnail)
            {

                if (IsShowAllBoxSelected)
                {
                    foreach (var ThumbnailImgInfo in ThumbnailImgInfos)
                    {
                        ThumbnailImgInfo.Path = ThumbnailImgInfo.Path.InReplaceOutString();
                    }
                    ;
                }
                else
                {
                    foreach (var ThumbnailImgInfo in ThumbnailImgInfos)
                    {
                        ThumbnailImgInfo.Path = ThumbnailImgInfo.Path.OutReplaceInString();
                    }
                    ;

                }
            }
            else
            {
                foreach (var ThumbnailImgInfo in ThumbnailImgInfos)
                {
                    var thumbnailFileName = Path.GetFileName(ThumbnailImgInfo.Path);
                    if (_damageImageFileNameSet.Contains(thumbnailFileName))
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

                // 统一报告外部文件名和 Word 内部标题的来源。
                // 以前外部文件名用 ImgFolderName，Word 内标题用 info.json 的 RailWayName，二者不同就会货不对板。
                string reportTitle = !string.IsNullOrWhiteSpace(ImgFolderName)
                    ? ImgFolderName.Trim()
                    : (NeedSavedInfo?.RailWayInfo?.RailWayName ?? "未输入").Trim();

                string safeReportTitle = reportTitle;
                foreach (char c in Path.GetInvalidFileNameChars())
                {
                    safeReportTitle = safeReportTitle.Replace(c, '_');
                }

                string fileName = safeReportTitle + "钢轨探伤检测报告.docx";
                string saveFilePath = Path.Combine(docxsPath, fileName);

                await Task.Run(() =>
                {
                    // 若已存在同名文件，则删除以避免 SaveAs 报错
                    if (File.Exists(saveFilePath))
                    {
                        File.Delete(saveFilePath);
                    }

                    // 把 reportTitle 传给 ExportWord，强制 Word 内标题和外部文件名使用同一个标题。
                    var doc = new ExportWord(Parameter, reportTitle);
                    doc.GenerateWord(saveFilePath); // 自动保存到新文件
                });

                await App.Current.Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Success($"{reportTitle} 钢轨探伤检测报告已保存在：{saveFilePath}（已覆盖旧文件）");
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

        //async Task CheckAppStatusPeriodically()
        //{
        //    while (true)
        //    {
        //        //异步检查两个程序是否正在运行
        //        IsDetectionAsynEnable =
        //            (Is6MRunning = await JGT_6M.IsRunning()) ||
        //            (Is8CRunning = await JGT_8C.IsRunning());

        //        if (IsDetectionAsynEnable == false)
        //        {
        //            IsDetectionAsynChecked = false;
        //        }

        //        if (await IsPortInUse(3333))
        //        {
        //            dispatcher.Invoke(() =>
        //            {
        //                PortStatusColor = Brushes.Green;
        //                PortStatusText = "判伤后台正在运行";
        //                StartPythonVisible = Visibility.Hidden;
        //                StopPythonVisible = Visibility.Visible;
        //            });
        //        }
        //        else
        //        {
        //            dispatcher.Invoke(() =>
        //            {
        //                PortStatusColor = Brushes.Red;
        //                PortStatusText = "判伤后台未在运行";
        //                StartPythonVisible = Visibility.Visible;
        //                StopPythonVisible = Visibility.Hidden;
        //            });
        //        }
        //        await Task.Delay(1300);
        //    }
        //}
        async Task CheckAppStatusPeriodically()
        {
            while (true)
            {
                try
                {
                    // 并行执行所有检查
                    var task6M = JGT_6M.IsRunning();
                    var task8Cnew = JGT_8Cnew.IsRunning();
                    var task8C = JGT_8C.IsRunning();
                    var taskPort = IsPortInUse(3333);

                    // 等待所有任务完成
                    await Task.WhenAll(task6M, task8C, taskPort, task8Cnew);

                    // 先分别获取结果，避免相互影响
                    bool is6MRunning = task6M.Result;
                    bool is8CnewRunning = task8Cnew.Result;
                    bool is8CRunning = task8C.Result;
                    bool isPortInUse = taskPort.Result;

                    // 然后再赋值给属性
                    Is6MRunning = is6MRunning;
                    Is8CRunning = is8CRunning;
                    Is8CnewRunning = is8CnewRunning;

                    // 最后设置总启用状态
                    IsDetectionAsynEnable = is6MRunning || is8CRunning || is8CnewRunning;

                    if (IsDetectionAsynEnable == false)
                    {
                        IsDetectionAsynChecked = false;
                    }

                    // UI更新
                    dispatcher.Invoke(() =>
                    {
                        if (isPortInUse)
                        {
                            PortStatusColor = Brushes.Green;
                            PortStatusText = "判伤后台正在运行";
                            StartPythonVisible = Visibility.Hidden;
                            StopPythonVisible = Visibility.Visible;
                        }
                        else
                        {
                            PortStatusColor = Brushes.Red;
                            PortStatusText = "判伤后台未在运行";
                            StartPythonVisible = Visibility.Visible;
                            StopPythonVisible = Visibility.Hidden;
                        }
                    });

                    //Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 8C运行状态: {is8CRunning}, 6M运行状态: {is6MRunning}, 端口状态: {isPortInUse}");
                }
                catch (Exception ex)
                {
                    //// 记录异常但继续监控
                    //Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 状态检查异常: {ex.Message}");

                    // 可以在这里添加更详细的异常信息
                    if (ex is AggregateException aggEx)
                    {
                        foreach (var innerEx in aggEx.InnerExceptions)
                        {
                            Console.WriteLine($"  内部异常: {innerEx.Message}");
                        }
                    }
                }

                // 无论是否异常都继续下一次检查
                await Task.Delay(1000);
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
        [ObservableProperty]
        private float currentLimitedSpeed;

        [ObservableProperty]
        private float currentThroughWeldSpeed;



        public List<Details> GetCategorySummary(float CategoryIndex, List<DamageData> damageDataListPara, bool IsSortBySimilarity = false)
        {
            var result = new List<Details>();

            // 处理超速（类别48）的特殊逻辑
            if (CategoryIndex == 48)
            {
                var imageOverspeedDict = new Dictionary<string, (bool HasGeneral, bool HasWeld, float CurrentGeneralSpeed, float CurrentWeldSpeed)>();
                var currentOcrDataList = GetCurrentDisplayOcrData();
                foreach (var ocr in currentOcrDataList)
                {
                    string fileName = Path.GetFileName(ocr.ImgFullPath);
                    bool hasGeneralOverspeed = false;
                    bool hasWeldOverspeed = false;
                    float currentGeneralSpeed = 0;
                    float currentWeldSpeed = 0;

                    // 检查整体速度是否超速
                    if (!string.IsNullOrWhiteSpace(ocr.speedvalue))
                    {
                        var speeds = ocr.speedvalue.Split(',');
                        foreach (var speedStr in speeds)
                        {
                            if (float.TryParse(speedStr, out float speed) && speed > CurrentLimitedSpeed)
                            {
                                hasGeneralOverspeed = true;
                                currentGeneralSpeed = speed;
                            }
                        }
                    }

                    // 检查焊缝速度是否超速
                    var correspondingDamageData = damageDataListPara.FirstOrDefault(d =>
                        Path.GetFileName(d.Url) == fileName);

                    if (correspondingDamageData != null)
                    {
                        bool hasWeld = false;

                        // 检查图片中是否有焊缝（类别14）
                        for (int i = 0; i < correspondingDamageData.DamagePoint.GetLength(0); i++)
                        {
                            if (correspondingDamageData.DamagePoint[i].Length > 4 &&
                                correspondingDamageData.DamagePoint[i][4] == 14)
                            {
                                hasWeld = true;
                                break;
                            }
                        }

                        // 如果有焊缝，检查速度是否超限
                        if (hasWeld && !string.IsNullOrWhiteSpace(ocr.speedvalue))
                        {
                            var speeds = ocr.speedvalue.Split(',');
                            foreach (var speedStr in speeds)
                            {
                                if (float.TryParse(speedStr, out float speed) && speed > CurrentThroughWeldSpeed)
                                {
                                    hasWeldOverspeed = true;
                                    currentWeldSpeed = speed;
                                }
                            }
                        }
                    }

                    // 更新字典
                    if (imageOverspeedDict.ContainsKey(fileName))
                    {
                        var existing = imageOverspeedDict[fileName];
                        imageOverspeedDict[fileName] = (
                            existing.HasGeneral || hasGeneralOverspeed,
                            existing.HasWeld || hasWeldOverspeed,
                            hasGeneralOverspeed ? currentGeneralSpeed : existing.CurrentGeneralSpeed,
                            hasWeldOverspeed ? currentWeldSpeed : existing.CurrentWeldSpeed
                        );
                    }
                    else
                    {
                        imageOverspeedDict[fileName] = (hasGeneralOverspeed, hasWeldOverspeed, currentGeneralSpeed, currentWeldSpeed);
                    }
                }

                // 根据检测结果添加到结果列表
                foreach (var kvp in imageOverspeedDict)
                {
                    string fileName = kvp.Key;
                    var (hasGeneral, hasWeld, currentGeneralSpeed, currentWeldSpeed) = kvp.Value;

                    if (hasGeneral || hasWeld)
                    {
                        result.Add(new Details()
                        {
                            FileName = fileName,
                            Count = 1,
                            weight = Math.Max(currentGeneralSpeed, currentWeldSpeed)
                        });
                    }
                }
            }
            else
            {
                // 处理普通伤损类别（非48）
                foreach (DamageData damageData in damageDataListPara)
                {
                    Details tempDetails = new();
                    int count = 0;
                    List<float> weights = new();
                    List<float> lengthsInMeters = new();

                    double metersPerPixel = 1.0 / 459.0;   // 按你的上一版换算关系

                    for (int i = 0; i < damageData.DamagePoint.GetLength(0); i++)
                    {
                        if (damageData.DamagePoint[i][4] == CategoryIndex)
                        {
                            if (IsSortBySimilarity)
                            {
                                weights.Add(damageData.DamagePoint[i][5]);
                            }

                            if (CategoryIndex == 51)
                            {
                                float pixelLength = damageData.DamagePoint[i][2];
                                float lengthInMeters = (float)(pixelLength * metersPerPixel);
                                lengthsInMeters.Add(lengthInMeters);
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
                            if (CategoryIndex == 51)
                            {
                                tempDetails.length = lengthsInMeters.Count > 0 ? lengthsInMeters.Max() : (float?)null;
                                tempDetails.weight = lengthsInMeters.Count > 0 ? lengthsInMeters.Max() : 0;
                                tempDetails.WeightText = lengthsInMeters.Count > 0
                                    ? $"{lengthsInMeters.Max():F2}米"
                                    : "0.00米";
                            }
                            else if (IsSortBySimilarity)
                            {
                                tempDetails.weight = weights.Count > 0 ? weights.Max() : 0;
                            }

                            result.Add(tempDetails);
                        }
                    }
                }
            }

            // 统一排序逻辑
            if (IsSortBySimilarity || CategoryIndex == 48)
            {
                result = result.OrderByDescending(r => r.weight).ToList();
                // 重新设置连续的DisplayIndex
                for (int i = 0; i < result.Count; i++)
                {
                    result[i].DisplayIndex = i + 1;
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

        private void InitCategorySummary(int DisplayedRulesCount = 10)
        {
            var currentDamageDataList = GetCurrentDisplayDamageData();
            var currentOcrDataList = GetCurrentDisplayOcrData();

            if (currentDamageDataList == null || currentDamageDataList.Count == 0)
            {
                DamageTree = new List<DamageCategorySummaryTree>();
                return;
            }

            var testTrackFiles = currentDamageDataList
                .Select(data => GetNormalizedFileName(data.Url))
                .Intersect(ThumbnailImgInfos
                    .Where(img => img.IsTestTrack)
                    .Select(img => GetNormalizedFileName(img.Path))
                    .ToHashSet())
                .ToHashSet();

            var nonTestTrackDatas = currentDamageDataList
                .Where(data => !testTrackFiles.Contains(GetNormalizedFileName(data.Url)))
                .ToList();

            // 3. 构建 DamageDetails 列表
            var damageDetailsList = nonTestTrackDatas.Select(d => new DamageDetails
            {
                FileName = GetNormalizedFileName(d.Url),
                DamageCategoryId = GetDamageCategoryId(d) ?? 0,
                Weight = GetWeight(d),
                FinalCategoryIds = new List<int>()
            }).ToList();

            // 4. 应用倒车类分组规则
            var finalClassification = Process49CategoryGroups(damageDetailsList, nonTestTrackDatas);

            // 5. 获取所有最终归类到49类的图片
            var final49Files = finalClassification
               .Where(kv => kv.Value.Contains(49))
               .Select(kv => GetNormalizedFileName(kv.Key))
                .Distinct()
                 .ToHashSet();

            // 6. 过滤掉所有归入倒车类的 DamageData
            var filteredNonTestTrackDatas = nonTestTrackDatas
                .Where(d => !final49Files.Contains(GetNormalizedFileName(d.Url)))
                .ToList();

            // 调试检查：确保没有49类图片被漏过滤
            foreach (var data in filteredNonTestTrackDatas)
            {
                var fileName = GetNormalizedFileName(data.Url);
                if (finalClassification.ContainsKey(fileName) && finalClassification[fileName].Contains(49))
                {
                    Debug.WriteLine($"错误: 图片 {fileName} 应该被过滤掉，但仍然在 filteredNonTestTrackDatas 中");
                }
            }




            // 7. 计算各类图片数量（基于过滤后的数据）
            var categoryAndCount = ObtainInfo.GetCategoryAndCount(filteredNonTestTrackDatas, false, currentOcrDataList);

            // 8. 统计各类别数量
            Debug.WriteLine($"过滤后的非测试轨数据数量: {filteredNonTestTrackDatas.Count}");
            Debug.WriteLine($"49类文件数量: {final49Files.Count}");

            int 标记Sum = CalculateImageCount(filteredNonTestTrackDatas, 14, 15, 16, 17, 18, 19, 39, 46, 47, 48)
                          + final49Files.Count;
            int 焊缝Sum = CalculateImageCount(filteredNonTestTrackDatas, 2, 11, 22);
            int 轨头Sum = CalculateImageCount(filteredNonTestTrackDatas, 5, 6, 36, 13 ,52, 26);
            int 核伤Sum = CalculateImageCount(filteredNonTestTrackDatas, 5, 6, 52, 26);
            int 轨腰Sum = CalculateImageCount(filteredNonTestTrackDatas, 7, 9, 29);
            int 轨底Sum = CalculateImageCount(filteredNonTestTrackDatas, 37, 8);
            int 作业违规Sum = CalculateImageCount(filteredNonTestTrackDatas, 21, 39, 48);

            // 9. 构建树形结构
            List<DamageCategorySummaryTree> RootCategoryList = new List<DamageCategorySummaryTree>
{
    new DamageCategorySummaryTree() { Name = "正常标记", Children = new() },
    new DamageCategorySummaryTree() { Name = "伤损标记", Children = new() },
    new DamageCategorySummaryTree() { Name = $"作业标记 总次数{标记Sum}", Children = new() },
    new DamageCategorySummaryTree()
    {
        Name = $"测试轨 (前{_activeBeforeShield}后{_activeAfterShield})",
        Children = new List<ITreeNode>
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
    }
};

            // 正常类细分
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
                        }
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

            var 轨头Node = RootCategoryList[1].Children[0] as DamageCategorySummaryTree;
            var 轨腰Category = (RootCategoryList[1].Children[1] as DamageCategorySummaryTree)?.Children;
            var 轨底Category = (RootCategoryList[1].Children[2] as DamageCategorySummaryTree)?.Children;

            // 轨头下面的 Children，里面已经包含“核伤”节点
            var 轨头Category = 轨头Node?.Children;

            // 找到“核伤”节点
            var 核伤Node = 轨头Category?
                .OfType<DamageCategorySummaryTree>()
                .FirstOrDefault(x => x.Name.StartsWith("核伤"));

            // 核伤下面真正用来放 5、6、52 的 Children
            var 核伤Category = 核伤Node?.Children;

            // 10. 构建每个类别节点（排除倒车类图片）
            foreach (var x in categoryAndCount)
            {
                // 显示层合并：id=6 在总览树中并入 id=5，底层原始数据不改
                int displayCategoryId = Convert.ToInt32(x.Value) == 6 ? 5 : Convert.ToInt32(x.Value);

                // 如果 5 和 6 同时存在，由 id=5 这一轮统一展示，避免生成两个节点
                if (Convert.ToInt32(x.Value) == 6 && categoryAndCount.Any(c => Convert.ToInt32(c.Value) == 5))
                {
                    continue;
                }

                // 跳过类别26、27，不添加到任何分类
                if (displayCategoryId == 27)
                {
                    continue;
                }

                var sourceCategoryIds = new List<float> { Convert.ToSingle(x.Value) };
                if (displayCategoryId == 5 && Convert.ToInt32(x.Value) == 5 && categoryAndCount.Any(c => Convert.ToInt32(c.Value) == 6))
                {
                    sourceCategoryIds.Add(6f);
                }

                var summaryList = MergeDisplaySummaryDetails(
                    sourceCategoryIds
                        .SelectMany(rawCategoryId => GetCategorySummary(rawCategoryId, filteredNonTestTrackDatas, true))
                        .Where(d => !final49Files.Contains(GetNormalizedFileName(d.FileName)))  // 剔除倒车类
                );
                int imageCount = summaryList.Count;

                // 使用排序后的子项
                var displayChildren = summaryList.OrderByDescending(d => d.weight).ToList();
                var displayChildrenByCount = summaryList.OrderByDescending(d => d.Count).ToList();


                if (DamageIdToBrush(displayCategoryId) == Brushes.Green)
                {
                    if (displayCategoryId == 2 || displayCategoryId == 11 || displayCategoryId == 22)
                    {
                        (RootCategoryList[0].Children[0] as DamageCategorySummaryTree)
                            ?.Children.Add(new DamageCategoryTree()
                            {
                                Name = $"{DamageIdToDamageName(displayCategoryId)}",
                                Count = imageCount,
                                ColorBrush = Brushes.Green,
                                Children = summaryList.OrderByDescending(d => d.Count).ToList()
                            });
                    }
                    else
                    {
                        RootCategoryList[0].Children.Add(new DamageCategoryTree()
                        {
                            Name = $"{DamageIdToDamageName(displayCategoryId)}",
                            Count = imageCount,
                            ColorBrush = Brushes.Green,
                            Children = summaryList.OrderByDescending(d => d.Count).ToList()
                        });
                    }
                }
                else if (DamageIdToBrush(displayCategoryId) == Brushes.Red)
                {
                    // 根据类别添加到轨头/轨腰/轨底
                    List<ITreeNode>? targetCategory = null;

                    // 核伤类：放到 轨头 -> 核伤 下面
                    if (displayCategoryId == 5 || displayCategoryId == 6 || displayCategoryId == 52 || displayCategoryId == 26)
                    {
                        targetCategory = 核伤Category;
                    }
                    // 其他轨头伤损：仍然放到轨头下面
                    else if (displayCategoryId == 36 || displayCategoryId == 13 || displayCategoryId == 23 || displayCategoryId == 30 || displayCategoryId == 35)
                    {
                        targetCategory = 轨头Category;
                    }
                    // 轨腰
                    else if (displayCategoryId == 7 || displayCategoryId == 9 || displayCategoryId == 29)
                    {
                        targetCategory = 轨腰Category;
                    }
                    // 轨底
                    else
                    {
                        targetCategory = 轨底Category;
                    }

                    if (targetCategory != null)
                    {
                        var redChildren = summaryList
                            .Select(d => new Details
                            {
                                Id = d.Id,
                                FileName = d.FileName,
                                Count = d.Count,
                                weight = displayCategoryId == 23 ? (float?)d.weight : (float?)((d.weight) * 100),
                                WeightText = displayCategoryId == 23
                                    ? $"{d.weight:F2}米"
                                    : $"{(d.weight * 100):F0}%",
                                length = d.length,
                                DisplayIndex = d.DisplayIndex,
                                IsChecked = d.IsChecked,
                                Status = d.Status
                            })
                                .OrderByDescending(d => d.weight);

                        // 30 和 35 只显示前五
                        var finalChildren = (displayCategoryId == 30 || displayCategoryId == 35)
                            ? redChildren.Take(5).ToList()
                            : redChildren.ToList();

                        targetCategory.Add(new DamageCategoryTree()
                        {
                            Name = $"{DamageIdToDamageName(displayCategoryId)}",
                            Count = finalChildren.Count,   // 这里保留总数
                            Children = finalChildren         // 这里只展示前五
                        });
                    }
                }
                else if (DamageIdToBrush(displayCategoryId) == Brushes.YellowGreen)
                {

                    if (displayCategoryId == 21 || displayCategoryId == 39 || displayCategoryId == 48)
                    {
                        (RootCategoryList[2].Children[0] as DamageCategorySummaryTree)?.Children.Add(new DamageCategoryTree()
                        {
                            Name = $"{DamageIdToDamageName(displayCategoryId)}",
                            Count = imageCount,
                            ColorBrush = Brushes.YellowGreen,
                            Children = summaryList.OrderByDescending(d => d.Count).ToList()
                        });
                    }
                    else if (displayCategoryId == 51)
                    {
                        var filtered51Children = summaryList
                            .Select(d => new Details
                            {
                                Id = d.Id,
                                FileName = d.FileName,
                                Count = d.Count,
                                weight = (float?)((d.length ?? 0)),
                                WeightText = $"{((d.length ?? 0)):F2}米",
                                length = d.length,
                                DisplayIndex = d.DisplayIndex,
                                IsChecked = d.IsChecked,
                                Status = d.Status
                            })
                            .Where(d => (d.weight ?? 0) >= currentLimitedLost)
                            // id=51/失底波按图片文件名最后一段数字升序排列，方便下面按连续图片分区段。
                            // 例如：0KM0.1M_199.png 取最后的 199。
                            .OrderBy(d => ExtractNumber(d.FileName))
                            .ThenBy(d => d.FileName)
                            .ToList();

                        var lostWaveSectionNodes = BuildContinuousLostWaveSectionNodes(filtered51Children);

                        (RootCategoryList[2].Children[0] as DamageCategorySummaryTree)?.Children.Add(new DamageCategorySummaryTree()
                        {
                            Name = $"{DamageIdToDamageName(displayCategoryId)} 总次数{filtered51Children.Count}",
                            IsExpanded = false,
                            Children = lostWaveSectionNodes
                        });
                    }
                    else
                    {
                        RootCategoryList[2].Children.Add(new DamageCategoryTree()
                        {
                            Name = $"{DamageIdToDamageName(displayCategoryId)}",
                            Count = imageCount,
                            ColorBrush = Brushes.YellowGreen,
                            Children = summaryList.OrderByDescending(d => d.Count).ToList()
                        });
                    }
                }
            }

            DamageTree = RootCategoryList;
        }
        #endregion


        private List<ITreeNode> BuildContinuousLostWaveSectionNodes(List<Details> lostWaveChildren)
        {
            var sectionNodes = new List<ITreeNode>();

            if (lostWaveChildren == null || lostWaveChildren.Count == 0)
                return sectionNodes;

            var orderedChildren = lostWaveChildren
                .Where(d => d != null && !string.IsNullOrWhiteSpace(d.FileName))
                .OrderBy(d => ExtractNumber(d.FileName))
                .ThenBy(d => d.FileName)
                .ToList();

            var currentSection = new List<Details>();
            int? previousNumber = null;
            int sectionIndex = 1;

            foreach (var child in orderedChildren)
            {
                int currentNumber = ExtractNumber(child.FileName);

                bool isContinuous = previousNumber.HasValue
                    && currentNumber != int.MaxValue
                    && previousNumber.Value != int.MaxValue
                    && currentNumber == previousNumber.Value + 1;

                if (currentSection.Count > 0 && !isContinuous)
                {
                    sectionNodes.Add(CreateLostWaveSectionNode(sectionIndex, currentSection));
                    sectionIndex++;
                    currentSection = new List<Details>();
                }

                currentSection.Add(child);
                previousNumber = currentNumber;
            }

            if (currentSection.Count > 0)
            {
                sectionNodes.Add(CreateLostWaveSectionNode(sectionIndex, currentSection));
            }

            return sectionNodes;
        }

        private DamageCategoryTree CreateLostWaveSectionNode(int sectionIndex, List<Details> sectionChildren)
        {
            var children = sectionChildren
                .OrderBy(d => ExtractNumber(d.FileName))
                .ThenBy(d => d.FileName)
                .Select((d, index) => new Details
                {
                    Id = d.Id,
                    FileName = d.FileName,
                    Count = d.Count,
                    weight = d.weight,
                    WeightText = d.WeightText,
                    length = d.length,
                    DisplayIndex = index + 1,
                    IsChecked = d.IsChecked,
                    Status = d.Status
                })
                .ToList();

            return new DamageCategoryTree()
            {
                Name = $"第{ToChineseNumber(sectionIndex)}区段",
                Count = children.Count,
                ColorBrush = Brushes.YellowGreen,
                Children = children
            };
        }

        private string ToChineseNumber(int number)
        {
            if (number <= 0)
                return number.ToString();

            string[] digits = { "零", "一", "二", "三", "四", "五", "六", "七", "八", "九" };

            if (number < 10)
                return digits[number];

            if (number == 10)
                return "十";

            if (number < 20)
                return "十" + digits[number % 10];

            if (number < 100)
            {
                int tens = number / 10;
                int ones = number % 10;
                return digits[tens] + "十" + (ones == 0 ? string.Empty : digits[ones]);
            }

            return number.ToString();
        }

        #region 取图片序号进行顺序排列
        private int GetImageNumberFromFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return int.MaxValue;

            var name = System.IO.Path.GetFileNameWithoutExtension(fileName);

            // 取文件名最后一段数字
            // 例如：
            // 451.png -> 451
            // 9KM861M_447.png -> 447
            var match = System.Text.RegularExpressions.Regex.Match(name, @"(\d+)$");

            if (match.Success && int.TryParse(match.Groups[1].Value, out int number))
            {
                return number;
            }

            return int.MaxValue;
        }
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
        #endregion

        private async Task<bool> AutoOpenModifyAndChangeWeldAsync()
        {
            if (damagePoints == null || damagePoints.Length == 0)
                return false;

            bool hasTargetBox = damagePoints.Any(x =>
                x.Length >= 5 && (int)x[4] == 52);

            if (!hasTargetBox)
                return false;

            var sampleImg = BitmapFrame.Create(new Uri(ImgPath.OutReplaceInString(), UriKind.Relative));
            var target = SqlImgInfos.FirstOrDefault(info =>
                Path.GetFileName(info.ImgPath) == Path.GetFileName(ImgPath));

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

            SampleWin.Show();

            bool modified = await vm.AutoModifyTargetBoxToNormalWeldAsync();

            if (modified)
                SampleWin.Close();

            return modified;
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
        bool is8CnewRunning = false;
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
                if (Is6MRunning && Is8CRunning || Is8CRunning && Is8CnewRunning || Is6MRunning && Is8CnewRunning)
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
                else if (Is8CnewRunning)
                {
                    JGT_8Cnew.LocationMileageAsync(mileage);
                }
                else
                {
                    HandyControl.Controls.MessageBox.Warning("请至少打开一个探伤软件的里程定位对话框");
                }
            }
            string[] mils = mileage.Split(new string[2] { "km", "KM" }, StringSplitOptions.RemoveEmptyEntries);
            if (Parameter != null && File.Exists(Path.Combine(Parameter, "OcrResult.json")))
            {
                string km = mils.FirstOrDefault();
                string i = mils.LastOrDefault().Replace("m", "").Replace("M", "");
                string mileageFilePath = Path.Combine(Parameter, "OcrResult.json");
                List<Records.OcrData> ocrData = AboutJson.DeserializeJson<List<Records.OcrData>>(mileageFilePath);
                List<Records.OcrData> result = ocrData.Where((Records.OcrData x) => x.MileageText.Contains(km) && x.MileageText.Contains(i)).ToList();
                LocationMileageImgs = new ObservableCollection<Records.OcrData>(result.Select((Records.OcrData x) => new Records.OcrData(Path.GetFileName(x.ImgFullPath), x.MileageText, x.speedvalue)));
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

            int displayIndex = 1;
            foreach (var img in ThumbnailImgInfos)
            {
                // 筛选出“前屏蔽”或“后屏蔽”的测试轨
                if (img.IsTestTrack && img.TestTrackType == testTrackType)
                {
                    result.Add(new Details
                    {
                        FileName = img.Name, // 显示测试轨文件名
                        Count = 1 // 每个测试轨计为1条
                        ,
                        DisplayIndex = displayIndex++ // 连续的DisplayIndex
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

            string Elapsed = "";
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
            catch (Exception ex)
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
        private bool _isSettingDamageRemarkFromCode = false;
        public string DamageRemark
        {
            get { return damageRemark; }
            set
            {
                if (value == damageRemark) return; // 无变化则跳过
                SetProperty(ref damageRemark, value);

                // 手动修改备注时，立即保存到数据库。
                // 程序切换图片/按钮自动赋值时，通过 _isSettingDamageRemarkFromCode 避免重复写库。
                if (!_isSettingDamageRemarkFromCode)
                {
                    SaveCurrentImageRemark(value);
                }
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
        async void NoDamageCheck()
        {
            // 1. 如果当前图之前做过“52 -> 2”的自动修改，再点无伤时先回退
            if (await TryRestoreAutoModifiedBoxAsync())
            {
                NoDamageCheckCommand.NotifyCanExecuteChanged();
                DamageCheckCommand.NotifyCanExecuteChanged();
                return;
            }

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

            if (HasNoDamageImg)
            {
                // 二次点击：取消无伤
                HasNoDamageImg = false;
                ResetState();
                UpdateConfirmDamageAndRemark(null, "");
                IsDamageBtnEnabled = true;
            }
            else
            {
                // 有 id=52：只做自动改普通焊缝，不保存无伤、不画绿框
                if (HasTargetWeldBox())
                {
                    await AutoOpenModifyAndChangeWeldAsync();

                    NoDamageCheckCommand.NotifyCanExecuteChanged();
                    DamageCheckCommand.NotifyCanExecuteChanged();
                    return;
                }

                // 没有 id=52，才走原来的无伤逻辑
                HasNoDamageImg = true;
                HasDamageImg = false;

                var r = GetRemark();
                string remarkToSet = string.IsNullOrWhiteSpace(r) ? "无伤" : r;
                UpdateConfirmDamageAndRemark(false, remarkToSet);

                SetDamageRemarkFromCode(remarkToSet);
                IsReadRemarkOnly = false;
                ImgBorderBrush = Brushes.LightGreen;
                IsDamageBtnEnabled = false;
            }

            NoDamageCheckCommand.NotifyCanExecuteChanged();
            DamageCheckCommand.NotifyCanExecuteChanged();
        }

        private bool HasTargetWeldBox()
        {
            return damagePoints != null &&
                   damagePoints.Any(x =>
                       x != null &&
                       x.Length >= 5 &&
                       (int)x[4] == 52);
        }

        private async Task<bool> TryRestoreAutoModifiedBoxAsync()
        {
            if (string.IsNullOrWhiteSpace(ImgPath))
                return false;

            var target = SqlImgInfos?
                .FirstOrDefault(x => Path.GetFileName(x.ImgPath) == Path.GetFileName(ImgPath));

            if (target == null || target.ImgId <= 0)
                return false;

            double? oldType = null;
            double? oldX = null;
            double? oldY = null;

            using (var sqlHelper = new SQLHelper(Settings.Default.SqlPath))
            {
                sqlHelper.EnsureConnectionOpen();

                string query = @"
                    SELECT AutoModifyOldDamageType, AutoModifyX, AutoModifyY
                    FROM Images
                    WHERE ImageId = @ImageId";

                using var cmd = new SqliteCommand(query, sqlHelper.Connection);
                cmd.Parameters.AddWithValue("@ImageId", target.ImgId);

                using var reader = cmd.ExecuteReader();

                if (reader.Read())
                {
                    if (!reader.IsDBNull(0)) oldType = reader.GetDouble(0);
                    if (!reader.IsDBNull(1)) oldX = reader.GetDouble(1);
                    if (!reader.IsDBNull(2)) oldY = reader.GetDouble(2);
                }
            }

            // 没有自动修改记录，说明这次点击无伤应该继续走普通无伤流程
            if (oldType == null || oldX == null || oldY == null)
                return false;

            if (damagePoints == null || damagePoints.Length == 0)
                return false;

            // 第二次回退也打开修改窗口，并让 SampleImgViewModel 用同一套窗口数据回退。
            // 不在 MainWindowViewModel 里直接按坐标改，避免两套 damagePoints 数据源不同步。
            var sampleImg = BitmapFrame.Create(new Uri(ImgPath.OutReplaceInString(), UriKind.Relative));

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

            SampleWin.Show();

            bool restored = await vm.AutoRestoreModifiedBoxAsync(
                (float)oldType.Value,
                (float)oldX.Value,
                (float)oldY.Value
            );

            if (restored)
            {
                SampleWin.Close();
            }

            return restored;
        }

        [RelayCommand(CanExecute = nameof(CanDamageCheck))]
        async void DamageCheck()
        {
            if (await TryFillMileageOnlyAsync())
            {
                return;
            }
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

                SetDamageRemarkFromCode(remarkToSet);
                IsReadRemarkOnly = false;
                ImgBorderBrush = Brushes.OrangeRed;
                IsNoDamageBtnEnabled = false; // 锁定无伤按钮
               
            }


            NoDamageCheckCommand.NotifyCanExecuteChanged();
            DamageCheckCommand.NotifyCanExecuteChanged();
        }

        void ResetState()
        {
            SetDamageRemarkFromCode("");
            IsReadRemarkOnly = true;
            ImgBorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(211, 211, 211));
        }

        string? GetRemark()
        {
            return SqlImgInfos
                .FirstOrDefault(x => Path.GetFileName(x.ImgPath) == Path.GetFileName(ImgPath))
                ?.Remark;
        }

        SqlImgInfo? GetCurrentSqlImgInfo()
        {
            if (string.IsNullOrWhiteSpace(ImgPath))
                return null;

            return SqlImgInfos?
                .FirstOrDefault(x => Path.GetFileName(x.ImgPath) == Path.GetFileName(ImgPath));
        }

        private async Task<bool> TryFillMileageOnlyAsync()
        {
            var target = GetCurrentSqlImgInfo();

            if (target == null || target.ImgId <= 0)
                return false;

            // 里程不为空，不走补里程流程
            if (!string.IsNullOrWhiteSpace(target.Mileage))
                return false;

            var dialog = Dialog.Show<MileageInputDialog>().Initialize<MileageInputDialogViewModel>(x =>
            {
                var fileName = Path.GetFileNameWithoutExtension(ImgPath);
                var index = fileName.IndexOf("_");
                if (index > 0)
                    x.Mileage = fileName.Substring(0, index);
            });

            var result = await dialog.GetResultAsync<string>();

            if (string.IsNullOrWhiteSpace(result))
                return true;

            target.Mileage = result;

            using var sqlHelper = new SQLHelper(Settings.Default.SqlPath);
            await sqlHelper.UpdateSqlImgInfoAsync(target);

            return true;
        }

        void SetDamageRemarkFromCode(string? remark)
        {
            _isSettingDamageRemarkFromCode = true;
            try
            {
                DamageRemark = remark ?? string.Empty;
            }
            finally
            {
                _isSettingDamageRemarkFromCode = false;
            }
        }

        void SaveCurrentImageRemark(string? remark)
        {
            if (string.IsNullOrWhiteSpace(ImgPath))
                return;

            var target = SqlImgInfos?
                .FirstOrDefault(x => Path.GetFileName(x.ImgPath) == Path.GetFileName(ImgPath));

            if (target == null || target.ImgId <= 0)
                return;

            // 只有已经点过“疑似”或“无伤”的图片才允许保存备注。
            // 未标记图片的备注框是只读的，不在这里写库。
            if (target.IsConfirmDamage == null)
                return;

            string remarkToSave = remark ?? string.Empty;

            if (target.Remark == remarkToSave)
                return;

            using var sqlHelper = new SQLHelper(Settings.Default.SqlPath);
            sqlHelper.UpdateImageRemark(target.ImgId, remarkToSave);
            target.Remark = remarkToSave;
        }

        void UpdateConfirmDamageAndRemark(bool? isConfirmDamage, string? remark)
        {
            if (string.IsNullOrWhiteSpace(ImgPath))
                return;

            var target = SqlImgInfos?
                .FirstOrDefault(x => Path.GetFileName(x.ImgPath) == Path.GetFileName(ImgPath));

            if (target == null || target.ImgId <= 0)
            {
                Console.WriteLine($"未找到有效 ImageId，无法保存无伤/疑似状态：{ImgPath}");
                return;
            }

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

        int? GetIsConfirmDamage()
        {
            if (string.IsNullOrWhiteSpace(ImgPath))
                return null;

            var target = SqlImgInfos?
                .FirstOrDefault(x => Path.GetFileName(x.ImgPath) == Path.GetFileName(ImgPath));

            if (target == null || target.ImgId <= 0)
                return null;

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
                DamageLevel = info.DamageLevel;
                if (info.IsConfirmDamage == true) // 疑似有伤
                {
                    HasDamageImg = true;
                    HasNoDamageImg = false;

                    SetDamageRemarkFromCode(string.IsNullOrWhiteSpace(info.Remark) ? "疑似有伤" : info.Remark);
                    ImgBorderBrush = Brushes.OrangeRed;
                    IsReadRemarkOnly = false;

                    IsNoDamageBtnEnabled = false;
                    IsDamageBtnEnabled = true;
                }
                else if (info.IsConfirmDamage == false) // 无伤
                {
                    HasNoDamageImg = true;
                    HasDamageImg = false;

                    SetDamageRemarkFromCode(string.IsNullOrWhiteSpace(info.Remark) ? "无伤" : info.Remark);
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
                DamageLevel = null;
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

        private int CalculateImageCount(List<DamageData> dataList, params float[] categoryIds)
        {
            if (dataList == null || dataList.Count == 0 || categoryIds == null || categoryIds.Length == 0)
                return 0;

            int sum = 0;

            foreach (var id in categoryIds)
            {
                // 复用已有方法：按类别拿到该类下所有详情（每个详情对应一张图）
                var summaryList = GetCategorySummary(id, dataList);
                sum += summaryList.Count; // 直接累加，不去重
            }

            return sum;
        }

        private Dictionary<string, List<int>> Process49CategoryGroups(List<DamageDetails> allDetails, List<DamageData> nonTestTrackDatas)
        {
            Debug.WriteLine($"Process49CategoryGroups 被调用，allDetails数量: {allDetails.Count}");
            var specialCategories = new HashSet<int> { 5, 6, 36, 13, 7, 9, 29, 37, 8 };
            var finalClassification = new Dictionary<string, List<int>>();

            // 1. 初始化所有图片的最终分类为原始类别
            foreach (var detail in allDetails)
            {
                finalClassification[detail.FileName] = new List<int> { detail.DamageCategoryId };
            }




            // 2. 找到所有含49类的图片，按文件名序号排序
            var all49Details = allDetails
                .Where(d => d.DamageCategoryId == 49)
                .OrderBy(d => ExtractNumber(d.FileName))
                .ToList();

            // 3. 分组：连续含49类的图片组合成一组，并加上该组第一张的前一张图片（如果存在）
            var groups = Group49CategoriesWithPrevious(all49Details, allDetails);

            // 4. 处理每个组
            foreach (var group in groups)
            {
                // 找出组内所有特殊类别的图片
                var specialCategoryImages = new Dictionary<int, List<DamageDetails>>();

                // 收集组内所有特殊类别图片
                foreach (var detail in group)
                {
                    if (specialCategories.Contains(detail.DamageCategoryId))
                    {
                        if (!specialCategoryImages.ContainsKey(detail.DamageCategoryId))
                            specialCategoryImages[detail.DamageCategoryId] = new List<DamageDetails>();
                        specialCategoryImages[detail.DamageCategoryId].Add(detail);
                    }
                }

                // 对每个特殊类别，保留权重最大的一张，其余标记为49类
                foreach (var catId in specialCategoryImages.Keys)
                {
                    if (specialCategoryImages[catId].Count > 0)
                    {
                        // 按权重排序，保留权重最大的
                        var sorted = specialCategoryImages[catId].OrderByDescending(d => d.Weight).ToList();
                        var maxWeightDetail = sorted[0];

                        // 保留权重最大的图片的原类别
                        finalClassification[maxWeightDetail.FileName] = new List<int> { catId };

                        // 其余图片标记为49类
                        for (int i = 1; i < sorted.Count; i++)
                        {
                            var detail = sorted[i];
                            finalClassification[detail.FileName] = new List<int> { 49 };
                        }
                    }
                }

                // 处理组内剩余的49类图片（没有特殊类别的）
                foreach (var detail in group.Where(d => d.DamageCategoryId == 49))
                {
                    finalClassification[detail.FileName] = new List<int> { 49 };
                }
            }

            // 5. 输出调试信息，帮助诊断问题
            Debug.WriteLine($"总共处理了 {allDetails.Count} 张图片");
            Debug.WriteLine($"最终标记为49类的图片数量: {finalClassification.Count(kv => kv.Value.Contains(49))}");

            foreach (var kv in finalClassification.Where(kv => kv.Value.Contains(49)))
            {
                var originalCategory = allDetails.First(d => d.FileName == kv.Key).DamageCategoryId;
                Debug.WriteLine($"49类图片: {kv.Key}, 原类别: {originalCategory}");
            }

            foreach (var detail in allDetails.Where(d => d.DamageCategoryId == 49))
            {
                if (!finalClassification.ContainsKey(detail.FileName) ||
                    !finalClassification[detail.FileName].Contains(49))
                {
                    finalClassification[detail.FileName] = new List<int> { 49 };
                }
            }


            Debug.WriteLine($"分组数量: {groups.Count}");
            foreach (var group in groups)
            {
                Debug.WriteLine($"组内图片: {string.Join(", ", group.Select(d => d.FileName))}");
            }


            return finalClassification;
        }
        // ===== Group49CategoriesWithPrevious =====
        private List<List<DamageDetails>> Group49CategoriesWithPrevious(List<DamageDetails> all49Details, List<DamageDetails> allDetails)
        {
            var groups = new List<List<DamageDetails>>();
            if (all49Details.Count == 0) return groups;

            // 1. 创建所有文件的数字索引映射
            var allFiles = allDetails.Concat(all49Details).ToList();
            var fileIndices = new Dictionary<string, int>();
            var fileToDetails = new Dictionary<string, DamageDetails>();

            foreach (var file in allFiles)
            {
                fileIndices[file.FileName] = ExtractNumber(file.FileName);
                fileToDetails[file.FileName] = file;
            }

            // 2. 按数字索引排序所有49类文件
            var sorted49Files = all49Details
                .OrderBy(d => fileIndices[d.FileName])
                .ThenBy(d => d.FileName)
                .ToList();

            // 3. 使用访问标记避免重复
            var visited = new HashSet<string>();
            var currentGroup = new List<DamageDetails>();

            for (int i = 0; i < sorted49Files.Count; i++)
            {
                var currentFile = sorted49Files[i];
                if (visited.Contains(currentFile.FileName)) continue;

                int currentIndex = fileIndices[currentFile.FileName];

                // 检查是否应该开始新组
                if (currentGroup.Count == 0 ||
                    !IsContinuousWithLastFile(currentGroup, currentIndex, fileIndices))
                {
                    // 结束当前组并开始新组
                    if (currentGroup.Count > 0)
                    {
                        groups.Add(new List<DamageDetails>(currentGroup));
                        currentGroup.Clear();
                    }
                }

                // 添加前一张非49类文件（如果存在且数字连续）
                if (currentGroup.Count == 0)
                {
                    var prevIndex = currentIndex - 1;
                    var prevFile = allDetails.FirstOrDefault(d =>
                        fileIndices[d.FileName] == prevIndex &&
                        !visited.Contains(d.FileName));

                    if (prevFile != null)
                    {
                        currentGroup.Add(prevFile);
                        visited.Add(prevFile.FileName);
                    }
                }

                // 添加当前49类文件
                currentGroup.Add(currentFile);
                visited.Add(currentFile.FileName);

                // 向后查找连续的数字文件
                int nextIndex = currentIndex + 1;
                while (true)
                {
                    var next49File = sorted49Files.FirstOrDefault(d =>
                        fileIndices[d.FileName] == nextIndex &&
                        !visited.Contains(d.FileName));

                    var nextNormalFile = allDetails.FirstOrDefault(d =>
                        fileIndices[d.FileName] == nextIndex &&
                        !visited.Contains(d.FileName));

                    if (next49File != null)
                    {
                        currentGroup.Add(next49File);
                        visited.Add(next49File.FileName);
                        nextIndex++;
                    }
                    else if (nextNormalFile != null && IsContinuousWithLastFile(currentGroup, nextIndex, fileIndices))
                    {
                        currentGroup.Add(nextNormalFile);
                        visited.Add(nextNormalFile.FileName);
                        nextIndex++;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            // 添加最后一个组
            if (currentGroup.Count > 0)
            {
                groups.Add(currentGroup);
            }

            return groups;
        }

        private bool IsContinuousWithLastFile(List<DamageDetails> group, int nextIndex, Dictionary<string, int> fileIndices)
        {
            if (group.Count == 0) return true;

            var lastFile = group[group.Count - 1];
            int lastIndex = fileIndices[lastFile.FileName];

            return nextIndex == lastIndex + 1;
        }

        // 改进的数字提取方法 - 确保一致性
        private int ExtractNumber(string fileName)
        {
            fileName = Path.GetFileNameWithoutExtension(fileName).ToLower();

            // 提取最后一个数字序列
            var matches = Regex.Matches(fileName, @"\d+");
            if (matches.Count > 0)
            {
                // 总是取最后一个匹配的数字
                var lastMatch = matches[matches.Count - 1];
                if (int.TryParse(lastMatch.Value, out int number))
                    return number;
            }

            return int.MaxValue;
        }

        // 改进的文件相关性判断
        private bool AreFilesRelated(string file1, string file2)
        {
            var base1 = GetBaseFileName(file1);
            var base2 = GetBaseFileName(file2);

            // 如果基础文件名相同或相似，且数字连续，则认为相关
            if (base1 == base2) return true;

            // 或者基础文件名有包含关系（如166km797m和166km796m）
            if (base1.Contains(base2) || base2.Contains(base1))
                return true;

            // 或者有共同的前缀（至少5个字符相同）
            var commonPrefix = GetCommonPrefix(base1, base2);
            return commonPrefix.Length >= 5;
        }

        private string GetCommonPrefix(string s1, string s2)
        {
            int minLength = Math.Min(s1.Length, s2.Length);
            for (int i = 0; i < minLength; i++)
            {
                if (s1[i] != s2[i])
                    return s1.Substring(0, i);
            }
            return s1.Substring(0, minLength);
        }

        // 改进的基础文件名提取
        private string GetBaseFileName(string fileName)
        {
            fileName = Path.GetFileNameWithoutExtension(fileName).ToLower();

            // 移除末尾的数字和分隔符
            fileName = Regex.Replace(fileName, @"[_\-\s]*\d+$", "");

            // 移除常见的文件后缀模式
            fileName = Regex.Replace(fileName, @"[_\-\s]*(?:km|m|png|jpg|jpeg)$", "");

            return fileName.Trim('_', '-', ' ');
        }


        private int? GetDamageCategoryId(DamageData data)
        {
            // 如果 DamagePoint 有数据，取第一个类别 ID
            if (data.DamagePoint != null && data.DamagePoint.Length > 0)
            {
                return (int)data.DamagePoint[0][4]; // 注意：这里应该是索引4，表示类别ID
            }
            return null;
        }

        private float GetWeight(DamageData data)
        {
            // 假设权重可以用 DamagePoint 中每个类别的第五个值表示（相似度）
            if (data.DamagePoint != null && data.DamagePoint.Length > 0)
            {
                return data.DamagePoint[0][5]; // 索引5表示相似度
            }
            return 0;
        }

        [RelayCommand]
        private async Task HistorialPhotos()
        {
            var win = new ImageSearch();
            win.Owner = Application.Current.MainWindow;
            win.Show();
        }

        public int GetDamageConfirmationStatus(string imgPath)
        {
            if (string.IsNullOrEmpty(imgPath))
            {
                return -1;
            }

            using (var sqlHelper = new SQLHelper(Settings.Default.SqlPath))
            {
                try
                {
                    // 确保连接已打开
                    if (sqlHelper.Connection.State != ConnectionState.Open)
                    {
                        sqlHelper.Connection.Open();
                    }

                    const string sql = "SELECT IsConfirmDamage FROM Images WHERE ImgPath = @ImgPath LIMIT 1";

                    using (var cmd = new SqliteCommand(sql, sqlHelper.Connection))
                    {
                        cmd.Parameters.AddWithValue("@ImgPath", imgPath);

                        var result = cmd.ExecuteScalar();

                        if (result == null || result == DBNull.Value)
                        {
                            return -1;
                        }

                        return Convert.ToInt32(result) switch
                        {
                            1 => 1,
                            0 => 0,
                            _ => -1
                        };
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"查询伤损状态出错: {ex.Message}");
                    return -1;
                }
                finally
                {
                    // 可选：根据业务需求决定是否关闭连接
                    if (sqlHelper.Connection.State == ConnectionState.Open)
                    {
                        sqlHelper.Connection.Close();
                    }
                }
            }
        }
        // 在现有的 ObservableProperty 区域添加
        #region 模式切换属性

        private bool _isCategoryMode = true;
        private CancellationTokenSource _imageInitCts;

        public bool IsCategoryMode
        {
            get => _isCategoryMode;
            set
            {
                if (SetProperty(ref _isCategoryMode, value))
                {
                    OnPropertyChanged(nameof(IsImageMode));

                    // 取消之前的任务
                    _imageInitCts?.Cancel();
                    _imageInitCts = new CancellationTokenSource();
                    var token = _imageInitCts.Token;

                    // 延迟更新统计信息
                    Task.Delay(50).ContinueWith(_ =>
                    {
                        if (token.IsCancellationRequested) return;
                    }, token);

                    if (value)
                    {
                        InitCategorySummary();
                    }
                    else
                    {
                        // 直接调用，不在外层包装 Task.Run
                        InitImageTreeSync(token);
                    }
                }
            }
        }

        // 辅助方法：从float[] DamagePoint中提取损伤类型编号
        int GetDamageTypeCode(float[] damagePoint)
        {
            if (damagePoint.Length >= 4)
            {
                return (int)damagePoint[4]; // 直接转换为int，因为float可以转换为int
            }
            return -1; // 如果长度不够，返回无效值
        }

        // 辅助方法：规范化文件名（去除路径，只保留文件名）
        string GetNormalizedFileName(string filePath)
        {
            return System.IO.Path.GetFileName(filePath);
        }
        // 统一使用交集方法识别测试轨
        private HashSet<string> GetActualTestTrackFiles()
        {
            return DamageDataList
                .Select(data => GetNormalizedFileName(data.Url))
                .Intersect(ThumbnailImgInfos
                    .Where(img => img.IsTestTrack)
                    .Select(img => GetNormalizedFileName(img.Path))
                    .ToHashSet())
                .ToHashSet();
        }
        private async void InitImageTreeSync(CancellationToken cancellationToken = default)
        {
            try
            {
                ImageTree?.Clear();
                ImageTree = new ObservableCollection<ImageGroupTree>();
                TotalImages = 0;

                var currentDamageDataList = GetCurrentDisplayDamageData();
                if (currentDamageDataList == null || ThumbnailImgInfos == null)
                    return;

                var redDamageTypes = new HashSet<int> { 5, 6, 7, 8, 9, 13,26, 29, 36, 30, 35, 37, 52 };
                 
                var testTrackFiles = currentDamageDataList
                    .Select(data => GetNormalizedFileName(data.Url))
                    .Intersect(ThumbnailImgInfos
                        .Where(img => img.IsTestTrack)
                        .Select(img => GetNormalizedFileName(img.Path))
                        .ToHashSet())
                    .ToHashSet();

                var damageImages = currentDamageDataList
                    .Where(data => data.DamagePoint != null &&
                                   data.DamagePoint.Length > 0 &&
                                   !testTrackFiles.Contains(GetNormalizedFileName(data.Url)) &&
                                   data.DamagePoint.Any(point => point.Length >= 4 &&
                                                                redDamageTypes.Contains(GetDamageTypeCode(point))))
                    .ToList();

                TotalImages = damageImages.Count;
                // 使用 await 在后台线程处理数据
                var imageGroups = await Task.Run(() =>
                {
                    var groups = new List<ImageGroupTree>();

                    foreach (var damageData in damageImages)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var fileName = Path.GetFileName(damageData.Url);
                        var imageGroupTree = new ImageGroupTree
                        {
                            FileName = fileName,
                            DamageCategories = new List<ImageDamageCategory>()
                        };

                        // 按伤损类别分组
                        var damageGroups = damageData.DamagePoint
                            .GroupBy(point => DisplayDamageId(point[4]))
                            .ToList();

                        foreach (var damageGroup in damageGroups)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            var categoryId = damageGroup.Key;
                            var damagePoints = damageGroup.ToList();

                            var categoryTree = new ImageDamageCategory
                            {
                                Name = DamageIdToDamageName(categoryId),
                                Count = damagePoints.Count,
                                ColorBrush = DamageIdToBrush(categoryId),
                                Children = new List<ImageDamageDetail>()
                            };

                            for (int i = 0; i < damagePoints.Count; i++)
                            {
                                var point = damagePoints[i];
                                var detail = new ImageDamageDetail
                                {
                                    FileName = $"{DamageIdToDamageName(categoryId)} #{i + 1}",
                                    Count = 1,
                                    weight = point[5],
                                    DisplayIndex = i + 1,
                                    Status = GetDamageStatus(categoryId),
                                    ColorBrush = DamageIdToBrush(categoryId)
                                };
                                categoryTree.Children.Add(detail);
                            }

                            imageGroupTree.DamageCategories.Add(categoryTree);
                        }

                        groups.Add(imageGroupTree);
                    }

                    return groups;
                }, cancellationToken);

                // 检查是否被取消
                if (cancellationToken.IsCancellationRequested) return;

                // 回到UI线程更新集合
                foreach (var group in imageGroups)
                {
                    ImageTree.Add(group);
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("图片模式初始化被取消");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"初始化图片模式数据失败: {ex.Message}");
            }
        }
        private DamageStatus GetDamageStatus(int categoryId)
        {
            var brush = DamageIdToBrush(categoryId);
            return brush switch
            {
                var b when b == Brushes.Green => DamageStatus.NODAMAGE,
                var b when b == Brushes.Red => DamageStatus.HASDAMAGE,
                var b when b == Brushes.Yellow => DamageStatus.KNOWN,
                _ => DamageStatus.UNKNOWN
            };
        }
        public bool IsImageMode => !_isCategoryMode;

        private ObservableCollection<ImageGroupTree> _imageTree;

        private int _totalImages;
        public int TotalImages
        {
            get => _totalImages;
            set => SetProperty(ref _totalImages, value);
        }

        public ObservableCollection<ImageGroupTree> ImageTree
        {
            get => _imageTree;
            set
            {
                if (SetProperty(ref _imageTree, value))
                {
                    // 当 ImageTree 改变时，重新计算图片总数
                    CalculateTotalImages();
                }
            }
        }

        private void CalculateTotalImages()
        {
            if (ImageTree == null)
            {
                TotalImages = 0;
                return;
            }

            int total = 0;
            foreach (var group in ImageTree)
            {
                if (group.DamageCategories != null)
                {
                    foreach (var category in group.DamageCategories)
                    {
                        total += category.Children?.Count ?? 0;
                    }
                }
            }

            TotalImages = total;
        }
        #endregion

        [ObservableProperty]
        private string _startMileage;

        [ObservableProperty]
        private string _endMileage;

        [RelayCommand]
        private async Task SearchMileageRange()
        {
            try
            {
                if (string.IsNullOrEmpty(Parameter))
                {
                    MessageBox.Info("请先打开一个数据文件夹");
                    return;
                }

                if (string.IsNullOrWhiteSpace(StartMileage) || string.IsNullOrWhiteSpace(EndMileage))
                {
                    MessageBox.Info("请输入完整的起始里程和结束里程");
                    return;
                }

                float startMeters = ParseMileageToMeters(StartMileage);
                float endMeters = ParseMileageToMeters(EndMileage);

                Console.WriteLine($"StartMileage = {StartMileage}, startMeters = {startMeters}");
                Console.WriteLine($"EndMileage = {EndMileage}, endMeters = {endMeters}");

                if (startMeters < 0 || endMeters < 0)
                {
                    MessageBox.Info("里程格式不正确，请使用如：17KM、18KM、134KM307.8M");
                    return;
                }

                if (startMeters > endMeters)
                {
                    MessageBox.Info("起始里程不能大于结束里程");
                    return;
                }

                string ocrFilePath = Path.Combine(Parameter, "OcrResult.json");
                if (!File.Exists(ocrFilePath))
                {
                    MessageBox.Error($"未找到OCR结果文件: {ocrFilePath}");
                    return;
                }

                List<Records.OcrData> allOcrData;
                try
                {
                    allOcrData = AboutJson.DeserializeJson<List<Records.OcrData>>(ocrFilePath);
                }
                catch (Exception ex)
                {
                    MessageBox.Error($"读取OCR文件失败: {ex.Message}");
                    return;
                }

                if (allOcrData == null || allOcrData.Count == 0)
                {
                    MessageBox.Info("OCR数据为空，无法进行里程搜索");
                    return;
                }

                List<string> filteredImages = new List<string>();

                foreach (var ocr in allOcrData)
                {
                    try
                    {
                        string fileNameOnly = Path.GetFileNameWithoutExtension(ocr.ImgFullPath);
                        float imgMileage = ParseMileageToMeters(fileNameOnly);

                        if (imgMileage < 0)
                        {
                            imgMileage = ParseMileageToMeters(ocr.MileageText);
                        }

                        if (imgMileage < 0)
                            continue;

                        if (imgMileage >= startMeters && imgMileage <= endMeters)
                        {
                            string fullPath = Path.IsPathRooted(ocr.ImgFullPath)
                                ? ocr.ImgFullPath
                                : Path.Combine(Parameter, ocr.ImgFullPath);

                            if (File.Exists(fullPath))
                            {
                                filteredImages.Add(fullPath);
                            }
                            else
                            {
                                string fileName = Path.GetFileName(ocr.ImgFullPath);
                                string altPath = Path.Combine(Parameter, fileName);

                                if (File.Exists(altPath))
                                {
                                    filteredImages.Add(altPath);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"处理OCR条目失败: {ocr?.ImgFullPath}, Mileage={ocr?.MileageText}, 错误: {ex.Message}");
                    }
                }

                filteredImages = filteredImages
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(File.GetCreationTime)
                    .ToList();

                if (filteredImages.Count == 0)
                {
                    MessageBox.Info($"在里程范围 {StartMileage} 到 {EndMileage} 内未找到图片");
                    return;
                }

                imgFiles = filteredImages.ToArray();
                IsMileageRangeFiltered = true;

                await RefreshViewAfterMileageFilterAsync(
                    true,
                    $"搜索完成，共找到 {filteredImages.Count} 张图片");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"里程范围搜索失败: {ex.Message}");
                MessageBox.Error($"里程范围搜索失败: {ex.Message}");
            }
        }
        /// <summary>
        /// 将里程字符串解析为米数
        /// </summary>
        /// <param name="startMileage"></param>
        /// <returns></returns>
        private float ParseMileageToMeters(string mileageString)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(mileageString))
                    return -1;

                mileageString = mileageString.Trim().Replace(" ", "").ToUpper();

                // 1. 优先匹配：134KM307.8M / 134KM307M
                var match1 = Regex.Match(mileageString, @"(\d+(?:\.\d+)?)KM(\d+(?:\.\d+)?)M");
                if (match1.Success)
                {
                    float km = float.Parse(match1.Groups[1].Value, CultureInfo.InvariantCulture);
                    float m = float.Parse(match1.Groups[2].Value, CultureInfo.InvariantCulture);
                    return km * 1000 + m;
                }

                // 2. 匹配：17KM / 17.5KM
                var match2 = Regex.Match(mileageString, @"(\d+(?:\.\d+)?)KM");
                if (match2.Success)
                {
                    float km = float.Parse(match2.Groups[1].Value, CultureInfo.InvariantCulture);
                    return km * 1000;
                }

                // 3. 匹配：307.8M
                var match3 = Regex.Match(mileageString, @"(\d+(?:\.\d+)?)M");
                if (match3.Success)
                {
                    return float.Parse(match3.Groups[1].Value, CultureInfo.InvariantCulture);
                }

                Console.WriteLine($"无法解析里程: {mileageString}");
                return -1;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"解析里程 '{mileageString}' 时出错: {ex.Message}");
                return -1;
            }
        }

        private List<DamageData> GetCurrentDisplayDamageData()
        {
            if (DamageDataList == null)
                return new List<DamageData>();

            if (!IsMileageRangeFiltered || imgFiles == null || imgFiles.Length == 0)
                return DamageDataList.ToList();

            var currentNames = imgFiles
                .Select(Path.GetFileName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return DamageDataList
                .Where(x => x != null
                            && !string.IsNullOrEmpty(x.Url)
                            && currentNames.Contains(Path.GetFileName(x.Url)))
                .ToList();
        }

        private List<OcrData> GetCurrentDisplayOcrData()
        {
            if (OcrDataList == null)
                return new List<OcrData>();

            if (!IsMileageRangeFiltered || imgFiles == null || imgFiles.Length == 0)
                return OcrDataList.ToList();

            var currentNames = imgFiles
                .Select(Path.GetFileName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return OcrDataList
                .Where(x => x != null
                            && !string.IsNullOrEmpty(x.ImgFullPath)
                            && currentNames.Contains(Path.GetFileName(x.ImgFullPath)))
                .ToList();
        }

        private async Task RefreshViewAfterMileageFilterAsync(bool showSuccessMessage = false, string? message = null)
        {
            await UpdataThumbnail();

            if (ThumbnailImgInfos != null && ThumbnailImgInfos.Count > 0)
            {
                SelectedIndex = 0;
                ImgSelectionChanged();
            }
            else
            {
                SelectedIndex = -1;
                ImgSource = null;
                DetailsList.Clear();
            }

            if (IsCategoryMode)
                InitCategorySummary();
            else
                InitImageTreeSync();

            if (showSuccessMessage && !string.IsNullOrWhiteSpace(message))
                MessageBox.Success(message);
        }

        [RelayCommand]
        private async Task ResetMileageRangeFilter()
        {
            try
            {
                if (originalImgFiles == null || originalImgFiles.Length == 0)
                {
                    MessageBox.Info("当前没有可复原的数据");
                    return;
                }

                imgFiles = originalImgFiles.ToArray();
                IsMileageRangeFiltered = false;

                StartMileage = string.Empty;
                EndMileage = string.Empty;

                await RefreshViewAfterMileageFilterAsync(
                    true,
                    $"已复原，共恢复 {imgFiles.Length} 张图片");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"复原里程筛选失败: {ex.Message}");
                MessageBox.Error($"复原里程筛选失败: {ex.Message}");
            }
        }


        /// <summary>
        /// 只用于界面显示的类别映射：底层 result.json / 数据库标注仍保留原始 id。
        /// 目前把 id=6、id=30、id=35 在显示层统一并入 id=5。
        /// </summary>
        private int DisplayDamageId(float id)
        {
            int damageId = (int)id;

            return damageId == 6 || damageId == 30 || damageId == 35
                ? 5
                : damageId;
        }

        private float[][] ConvertDamagePointsForDisplay(float[][]? rawDamagePoints)
        {
            if (rawDamagePoints == null || rawDamagePoints.Length == 0)
                return Array.Empty<float[]>();

            return rawDamagePoints
                .Select(point =>
                {
                    var displayPoint = (float[])point.Clone();
                    if (displayPoint.Length > 4)
                    {
                        displayPoint[4] = DisplayDamageId(displayPoint[4]);
                    }
                    return displayPoint;
                })
                .ToArray();
        }

        private List<Details> MergeDisplaySummaryDetails(IEnumerable<Details> details)
        {
            return details
                .Where(d => d != null && !string.IsNullOrWhiteSpace(d.FileName))
                .GroupBy(d => d.FileName)
                .Select(group =>
                {
                    var first = group.First();
                    return new Details
                    {
                        Id = first.Id,
                        FileName = first.FileName,
                        Count = group.Sum(d => d.Count),
                        weight = group.Max(d => d.weight),
                        WeightText = first.WeightText,
                        length = group.Max(d => d.length),
                        DisplayIndex = first.DisplayIndex,
                        IsChecked = group.Any(d => d.IsChecked),
                        Status = first.Status
                    };
                })
                .OrderByDescending(d => d.weight)
                .ToList();
        }
    }
}
#endregion