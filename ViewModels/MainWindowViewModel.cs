using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DamageMaker.Automation;
using DamageMaker.Common;
using DamageMaker.DamageDataProcessing;
using DamageMaker.FileHandle;
using DamageMaker.GenerateReport;
using DamageMaker.ImageProcessing;
using DamageMaker.Models;
using DamageMaker.Properties;
using DamageMaker.SqliteServer;
using DamageMaker.ViewModels;
using DamageMaker.Views;
using DamageMarker.Models;
using DamageMarker.Views;
using HandyControl.Controls;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Formats.Asn1;
using System.Globalization;
using System.IO;
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
            get { return isDistinctDamage;}
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
        BitmapFrame? imgSource;

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
            AppTitle = " 探伤仪检测数据智能分析系统";
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
            foldersListWindow.ShowDialog();
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
                await  SaveAllBoxSelectedImg(DamageImgPaths, damageImgFolderName);

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


        /// <summary>
        /// 
        /// </summary>
        /// <param name="Imgfiles"></param>
        /// <param name="SavePath"></param>
        /// <returns></returns>
        async Task SaveAllBoxSelectedImg(List<string> Imgfiles, [NotNull] string SavePath)
        {

            int saveImgCount = 0;
            await Task.Run(delegate
            {
                Parallel.ForEach((IEnumerable<string>)Imgfiles, (Action<string>)delegate (string imgFile)
                {
                    string fileName = Path.GetFileName(imgFile);
                    string fileNamePath = Path.Combine(SavePath, fileName);

                    if (!File.Exists(fileNamePath))
                    {
                        BitmapFrame boxSelectedImg = BitmapFrame.Create(new Uri(imgFile));
                        float[][] item = GetDamagePointsAndIndex(imgFile).Item1;
                        //if (isHideNormal)
                        //{
                        //    item = MaskDamge(item);
                        //}
                        //后续查清楚item在那里为空,为什么,必须解决
                        //if (item?.Length != 0)
                        //{

                        BitmapFrame image = BoxSelected(boxSelectedImg, item);

                        SaveBitmapToPng(Path.Combine(SavePath, fileName), image);
                        
                        saveImgCount++;
                        //  }
                    }
                });
            });

            Console.WriteLine($"图片伤损绘制{saveImgCount}张完成,");
        }

        /// <summary>
        /// 从remark中提取作业区间、伤损点和里程数
        /// </summary>
        /// <summary>
        /// 从remark中提取作业区间、伤损点和里程数
        /// </summary>
        (string? WorkSection, float[][] DamagePoints, string? Mileage) ParseRemarkInfo(string? remark)
        {
            if (string.IsNullOrEmpty(remark))
                return (null, Array.Empty<float[]>(), null);

            string? workSection = null;
            string? mileage = null;
            float[][] damagePoints = Array.Empty<float[]>();

            // 提取作业区间 - 改为从字符串末尾开始查找
            int wsIdx = remark.LastIndexOf("作业区间");
            if (wsIdx >= 0)
            {
                int start = wsIdx + "作业区间".Length;
                while (start < remark.Length && (remark[start] == ':' || remark[start] == '：' || remark[start] == '为' || char.IsWhiteSpace(remark[start])))
                    start++;
                // 作业区间应该是最后一个字段，直接取到字符串末尾
                workSection = remark.Substring(start).Trim();
            }

            // 提取里程数 - 添加更多可能的前缀
            int mileageIdx = remark.IndexOf("里程数为");
            if (mileageIdx < 0) mileageIdx = remark.IndexOf("里程");
            if (mileageIdx >= 0)
            {
                int start = mileageIdx + (remark[mileageIdx] == '里' ? "里程数为".Length : "里程".Length);
                while (start < remark.Length && (remark[start] == ':' || remark[start] == '：' || remark[start] == '为' || char.IsWhiteSpace(remark[start])))
                    start++;
                int end = remark.IndexOf("，伤损点", start);
                if (end == -1) end = remark.Length;
                mileage = remark.Substring(start, end - start).Trim();
            }

            // 提取伤损点 - 更精确地确定范围
            int damageIdx = remark.IndexOf("伤损点");
            if (damageIdx >= 0)
            {
                int start = damageIdx + "伤损点".Length;
                while (start < remark.Length && (remark[start] == ':' || remark[start] == '：' || char.IsWhiteSpace(remark[start])))
                    start++;
                int end = remark.IndexOf("，作业区间", start);
                if (end == -1) end = remark.Length;
                string damageStr = remark.Substring(start, end - start).Trim();
                if (!string.IsNullOrEmpty(damageStr))
                {
                    var arrs = damageStr.Split(';', StringSplitOptions.RemoveEmptyEntries);
                    var result = new List<float[]>();
                    foreach (var arr in arrs)
                    {
                        var nums = arr.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                      .Select(s => float.TryParse(s, out var f) ? f : 0).ToArray();
                        if (nums.Length > 0) result.Add(nums);
                    }
                    damagePoints = result.ToArray();
                }
            }

            return (workSection, damagePoints, mileage);
        }
        /// <summary>
        /// 根据目标 idx 查找 Imgfiles 中匹配的文件路径。
        /// </summary>
        public static string FindFilePathByIdx(int targetIdx, List<string> imgFiles)
        {
            foreach (string filePath in imgFiles)
            {

                ParseFileName(filePath, out int currentIdx, out string mileage);
                if (currentIdx == targetIdx)
                {
                    return Path.GetFullPath(filePath); 
                }
            }
            return null; // 未找到
        }

        /// <summary>
        /// 检查数据库、比对里程、组装图片、发送后端、保存结果
        /// </summary>
        private async Task CheckAndSendToBackend(int idx, string? mileage, List<string> Imgfiles)
        {
            List<SqlImgInfo> allSqlImgInfos;
            using (var sqlHelper = new SQLHelper(Settings.Default.SqlPath))
            {
                allSqlImgInfos = sqlHelper.GetAllImages();
            }

            var damageDataList = MainWindow.MainVm.DamageDataList;

            var sendList = new List<object>();

            // 当前图片的作业区间和里程数
            var curWorkSection = MainWindowViewModel.NeedSavedInfo?.RailWayInfo.WorkSection;
            var curMileage = mileage;

            string Normalize(string? s)
            {
                if (string.IsNullOrEmpty(s)) return "";
                return Regex.Replace(s, @"\s+", "")
                    .Replace("，", ",")
                    .Replace("。", ".")
                    .Replace("：", ":")
                    .Replace(";", ";")
                    .ToLowerInvariant();
            }

            // 先查找所有作业区间匹配的 FolderId
                var matchedFolderIds = allSqlImgInfos
                    .Where(x => x.FolderId != sqlFolderId && !string.IsNullOrEmpty(x.Remark))
                    .Select(x => new { Info = x, Parsed = ParseRemarkInfo(x.Remark) })
                    .Where(x => !string.IsNullOrEmpty(x.Parsed.WorkSection) && Normalize(x.Parsed.WorkSection) == Normalize(curWorkSection))
                    .Select(x => x.Info.FolderId)
                    .Distinct()
                    .ToList();

            // 再在这些文件夹下查找里程数匹配的图片
            var dbImgInfo = allSqlImgInfos
                .Where(x => matchedFolderIds.Contains(x.FolderId) && !string.IsNullOrEmpty(x.Remark))
                .Select(x => new { Info = x, Parsed = ParseRemarkInfo(x.Remark) })
                .Where(x => !string.IsNullOrEmpty(x.Parsed.Mileage) && Normalize(x.Parsed.Mileage) == Normalize(curMileage))
                .FirstOrDefault()?.Info;

            // 只在查到数据库图片时才继续
            if (dbImgInfo == null)
                return;

            var (dbWorkSection, dbDamage, dbMileage) = ParseRemarkInfo(dbImgInfo.Remark);

            // 比对作业区间和里程数
            bool isWorkRangeEqual = string.Equals(dbWorkSection?.Trim(), curWorkSection?.Trim(), StringComparison.OrdinalIgnoreCase);
            bool inWorkRange = string.Equals(dbMileage?.Trim(), curMileage?.Trim(), StringComparison.OrdinalIgnoreCase);

            // 只有在作业区间内且里程数一致时才处理
            if (inWorkRange && isWorkRangeEqual)
            {
                // 临时文件夹路径（确保存在）
                string tempDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Temp");
                if (!Directory.Exists(tempDir))
                {
                    Directory.CreateDirectory(tempDir);
                }

                // 生成唯一临时文件名，避免并发冲突
                string tempFileName = Path.GetFileName(dbImgInfo.ImgPath);
                string tempPath = Path.Combine(tempDir, tempFileName);

                try
                {
                    if (dbImgInfo.ImageData != null && dbImgInfo.ImageData.Length > 0)
                    {
                        //// 验证伤损点数据
                        //Console.WriteLine($"伤损点数量: {dbDamage?.Length ?? 0}");
                        //if (dbDamage != null)
                        //{
                        //    foreach (var point in dbDamage.Take(5))
                        //    {
                        //        Console.WriteLine($"伤损点: {string.Join(",", point)}");
                        //    }
                        //}

                        // 保存并标记图片
                        File.WriteAllBytes(tempPath, dbImgInfo.ImageData);
                        Console.WriteLine($"已保存图片到临时路径: {tempPath}");
                    }
                    else
                    {
                        Console.WriteLine("ImageData 为空或长度为0，未写入图片。");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"文件操作错误: {ex.Message}");
                }

                // 直接用解析出来的伤损点
                sendList.Add(new { url = tempPath, damage = dbDamage ?? new float[0][] });
                // 组装左中右三张图片和数据库图片
                for (int i = idx - 1; i <= idx + 1; i++)
                {
                        var path = FindFilePathByIdx(i,Imgfiles);
                     Console.WriteLine($"当前传给后端图片:{i}\n,{path}");
                    if (path != null)
                    {
                        var damage = damageDataList?.Where(x => Path.GetFileName(x.Url) == Path.GetFileName(path)).FirstOrDefault()?.DamagePoint ?? new float[0][];
                        sendList.Add(new
                        {
                            url = path,
                            damage
                        });
                    }                        
                }

                // 发送给后端
                using var httpClient = new HttpClient();
                string apiUrl = "http://127.0.0.1:3333/process_CH";
                var response = await httpClient.PostAsJsonAsync(apiUrl, sendList);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"后端返回: {json}");
                    var damageData = JsonSerializer.Deserialize<List<DamageData>>(json);

                    // 首先更新主数据源DamageDataList
                    foreach (var result in damageData)
                    {
                        // 找到对应的原始数据
                        var originalData = DamageDataList.FirstOrDefault(x =>
                            x != null &&
                            !string.IsNullOrEmpty(x.Url) &&
                            Path.GetFileName(x.Url) == Path.GetFileName(result.Url));

                        if (originalData != null)
                        {
                            originalData.DamagePoint = result.DamagePoint;
                        }
                        // 立即保存到result.json
                        AboutJson.SaveJson(DamageDataList,
                            Path.Combine(Settings.Default.InPath, MainWindow.MainVm.ImgFolderName),
                            "result.json");
                        // 更新数据库
                        var imgInfo = SqlImgInfos.FirstOrDefault(x => Path.GetFileName(x.ImgPath) == Path.GetFileName(result.Url));
                        if (imgInfo != null)
                        {
                            // 伤损信息转为字符串
                            string damageStr = "";
                            if (originalData.DamagePoint != null)
                            {
                                damageStr = string.Join(";", originalData.DamagePoint.Select(arr => string.Join(",", arr)));
                            }
                            var railInfo = MainWindowViewModel.NeedSavedInfo?.RailWayInfo;
                            string workRange = railInfo?.WorkSection ?? "未知";

                            // 组装remark
                            string remark = $"当前图片存在厂焊,里程数为{curMileage}，伤损点:{damageStr}，作业区间:{workRange}";

                            // 读取图片文件为 byte[]
                            byte[] imageBytes = File.ReadAllBytes(result.Url.OutReplaceInString());
                            DataAccess.UpdateImageRemark(SqlImgInfos, imgInfo.ImgPath, remark);
                            DataAccess.UpdateImageData(SqlImgInfos, imgInfo.ImgPath, imageBytes);
                        }
                    }

                    foreach (var result in damageData)
                    {
                        string imgPath = result.Url;
                        float[][] damagePoints = result.DamagePoint;

                        if (imgPath != tempPath && File.Exists(imgPath.InReplaceOutString()))
                        {
                            BitmapFrame boxSelectedImg = BitmapFrame.Create(new Uri(imgPath));
                            BitmapFrame image = BoxSelected(boxSelectedImg, damagePoints);
                            SaveBitmapToPng(imgPath.InReplaceOutString(), image); // 覆盖保存
                            Console.WriteLine($"图片已重绘并保存: {imgPath.InReplaceOutString()}");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 从文件名中解析编号和里程信息。
        /// 支持格式：
        /// 1. "编号_里程"（如 "397_175KM999M"）
        /// 2. "里程_编号"（如 "175KM999M_397"）
        /// 3. 只有里程（如 "175KM999M"）
        /// 4. 只有编号（如 "397"）
        /// </summary>
        /// <param name="fileName">文件名（不含路径和扩展名）</param>
        /// <param name="idx">输出的编号（若不存在则为0）</param>
        /// <param name="mileage">输出的里程（若不存在则为空字符串）</param>
        public static void ParseFileName(string fileName, out int idx, out string mileage)
        {
            idx = 0;
            mileage = string.Empty;

            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
            string[] parts = fileNameWithoutExt.Split('_');

            if (parts.Length >= 2)
            {
                // 情况1和2：两部分，可能是"编号_里程"或"里程_编号"
                bool firstIsIndex = int.TryParse(parts[0], out idx);
                bool firstIsMileage = parts[0].Contains("KM") && parts[0].Contains("M");

                bool secondIsIndex = int.TryParse(parts[1], out int tempIndex);
                bool secondIsMileage = parts[1].Contains("KM") && parts[1].Contains("M");

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
            else if (parts.Length == 1)
            {
                // 情况3和4：只有一部分，可能是里程或编号
                if (parts[0].Contains("KM") && parts[0].Contains("M"))
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
        async Task ProcessCH(List<string> Imgfiles, string SavePath)
        {
            // 当前作业区间
            var curWorkSection = MainWindowViewModel.NeedSavedInfo?.RailWayInfo?.WorkSection;

            // 规范化方法
            string Normalize(string? s)
            {
                if (string.IsNullOrEmpty(s)) return "";
                return Regex.Replace(s, @"\s+", "")
                    .Replace("，", ",")
                    .Replace("。", ".")
                    .Replace("：", ":")
                    .Replace(";", ";")
                    .ToLowerInvariant();
            }

            // 获取所有图片信息
            List<SqlImgInfo> allSqlImgInfos;
            using (var sqlHelper = new SQLHelper(Settings.Default.SqlPath))
            {
                allSqlImgInfos = sqlHelper.GetAllImages();
            }

            // 查找作业区间匹配的图片（非当前文件夹）
            var matchedImgs = allSqlImgInfos
                .Where(x => x.FolderId != sqlFolderId && !string.IsNullOrEmpty(x.Remark))
                .Select(x => new { Info = x, Parsed = ParseRemarkInfo(x.Remark) })
                .Where(x => !string.IsNullOrEmpty(x.Parsed.WorkSection) && Normalize(x.Parsed.WorkSection) == Normalize(curWorkSection))
                .ToList();

            // 获取所有匹配图片的 FolderId（去重）
            var matchedFolderIds = matchedImgs.Select(x => x.Info.FolderId).Distinct().ToList();
            Console.WriteLine($"匹配的文件夹ID数量: {matchedFolderIds.Count}");
            int saveImgCount = 0;

            foreach (var imgFile in Imgfiles)
            {
                string fileName = Path.GetFileName(imgFile);
                string fileNamePath = Path.Combine(SavePath.OutReplaceInString(), fileName);

                if (File.Exists(fileNamePath))
                {
                    string fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileNamePath);
                    string[] parts = fileNameWithoutExt.Split('_');

                    // 更安全的解析方式
                    int idx = 0;
                    string mileage = string.Empty;

                    ParseFileName(fileName,out idx,out mileage);

                    //166KM602M
                    var curMileage = mileage;

                    // 在所有匹配的文件夹下查找里程数匹配的图片
                    var dbImgInfo = allSqlImgInfos
                        .Where(x => matchedFolderIds.Contains(x.FolderId) && !string.IsNullOrEmpty(x.Remark))
                        .Select(x => new { Info = x, Parsed = ParseRemarkInfo(x.Remark) })
                        .Where(x => !string.IsNullOrEmpty(x.Parsed.Mileage) && Normalize(x.Parsed.Mileage) == Normalize(curMileage))
                        .FirstOrDefault()?.Info;

                    if (dbImgInfo != null)
                    {
                        Console.WriteLine($"正在处理图片: {fileNamePath}");
                        Console.WriteLine($"当前图片的索引:{idx}");
                        await CheckAndSendToBackend(idx, curMileage, Imgfiles);
                        saveImgCount++;
                    }
                }
            }
            Console.WriteLine($"图片厂焊绘制{saveImgCount}张完成,");
        }
        #region
        [ObservableProperty]
        bool isHideNormalMarker;

        bool CanHideNormalMarker() => damagePoints != null;
        [RelayCommand(CanExecute = nameof(CanHideNormalMarker))]

        void HideNormalMarker()
        {
            DetailsList.Clear();
            InitDamageDetails(this.damagePoints);
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
                // 新增：自动触发厂焊分析
                if (DamageDataList != null && DamageDataList.Count > 0)
                {
                    await ProcessCH(DamageImgPaths, damageImgFolderName);
                    //Growl.Info("厂焊分析完成！");
                }
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

        public static string railClass;
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
            if (curInstruments == "8C" || curInstruments == "6M" || curInstruments == "8D" || curInstruments == "19型" || curInstruments == "gt-20")
                railClass = "single";
            else if (curInstruments == "双轨501" || curInstruments == "双轨502")
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

        private BitmapFrame BoxSelected(BitmapFrame boxSelectedImg, float[][] damagePoints)
        {
            try
            {
                int index = 0;
                // 获取Image控件中的源图片
                BitmapSource originalBitmap = (BitmapSource)boxSelectedImg;
                // 创建一个新的位图来存储绘制后的结果
                WriteableBitmap writableBitmap = new WriteableBitmap(originalBitmap);
                // 使用DrawingVisual来绘制
                DrawingVisual drawingVisual = new DrawingVisual();

                var drawingContext = drawingVisual.RenderOpen();
                // 将原图绘制到底层
                drawingContext.DrawImage(
                    writableBitmap,
                    new Rect(0, 0, writableBitmap.PixelWidth, writableBitmap.PixelHeight)
                );
                if (NeedSavedInfo != null && isDistinctDamage)
                {
                    // 绘制两条红色的竖虚线
                    Pen dashedPen = new Pen(Brushes.Red, 2)
                    {
                        DashStyle = new DashStyle(new double[] { 2, 2 }, 0)
                    };
                    var RightLineX = MainWindowViewModel.NeedSavedInfo.ScreenshotWidthPx - MainWindowViewModel.NeedSavedInfo.ScreenshotOffset / 2;
                    var LeftLineX = MainWindowViewModel.NeedSavedInfo.ScreenshotOffset / 2;
                    drawingContext.DrawLine(dashedPen, new Point(LeftLineX, 0), new Point(LeftLineX, writableBitmap.PixelHeight));
                    drawingContext.DrawLine(dashedPen, new Point(RightLineX, 0), new Point(RightLineX, writableBitmap.PixelHeight));
                }

                foreach (var damagePoint in damagePoints)
                {
                    if ((damagePoint[4]==36|| damagePoint[4] == 23)&&Settings.Default.IsConcealFishScale)
                    {
                        continue;
                    }


                    float x = damagePoint[0],
                          y = damagePoint[1],
                          width = damagePoint[2];
                    float height = damagePoint[3],
                          Similar = damagePoint[5] < 0.5 ? 0.5f : damagePoint[5];
                    SolidColorBrush color = DamageIdToBrush(damagePoint[4]);
                    string DamageCategory = DamageIdToDamageName(damagePoint[4]);
                    var boxBrush = new SolidColorBrush(
                        System.Windows.Media.Color.FromArgb(
                            Convert.ToByte(255 * Similar),
                            color.Color.R,
                            color.Color.G,
                            color.Color.B
                        )
                    );
                    if (x > writableBitmap.Width || x < 0 || y > writableBitmap.Height || y < 0 || x + width > writableBitmap.Width || y + height > writableBitmap.Height)
                    {
                        Console.WriteLine($"绘制图片{boxSelectedImg.Decoder.Frames.FirstOrDefault()}出错,");
                        Console.WriteLine($"第{index}个伤损超出范围,");
                        continue;
                    }

                    //防止画的框的宽度和高度过小
                    if (width > 5 && height > 5)
                    {
                        // 绘制一个框
                        drawingContext.DrawRoundedRectangle(
                            Brushes.Transparent,
                            new Pen(boxBrush, 5),
                            new Rect(x, y, width, height),
                            width / 2 * (1 - Similar),
                            height / 2 * (1 - Similar)
                        );
                    }
                    else
                    {
                        drawingContext.DrawRoundedRectangle(
                           Brushes.Transparent,
                           new Pen(boxBrush, 1),
                           new Rect(x, y, width, height),
                           width / 2 * (1 - Similar),
                           height / 2 * (1 - Similar)
                       );
                    }
                    drawingContext.DrawText(
                        new FormattedText(
                            DamageCategory + $" {index}",
                            CultureInfo.GetCultureInfo("zh-CN"),
                            System.Windows.FlowDirection.LeftToRight,
                            new Typeface("微软雅黑"),
                            12,
                            System.Windows.Media.Brushes.White,
                            96
                        ),
                        new System.Windows.Point(
                            x - 30 <= 0 ? x + 30 : x - 30,
                            y - 30 <= 0 ? y + 30 : y - 30
                        )
                    );
                    index++;
                }





                drawingContext.Close();
                // 将DrawingVisual的内容转换为BitmapSource
                RenderTargetBitmap rtb = new RenderTargetBitmap(
                    (int)writableBitmap.PixelWidth,
                    (int)writableBitmap.PixelHeight,
                    96,
                    96,
                    PixelFormats.Pbgra32
                );
                rtb.Render(drawingVisual);

                // 将结果转换为BitmapImage以显示
                BitmapImage bitmapImage = new BitmapImage();
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    PngBitmapEncoder encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(rtb));
                    encoder.Save(memoryStream);
                    memoryStream.Seek(0, SeekOrigin.Begin);
                    bitmapImage.BeginInit();
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.StreamSource = memoryStream;
                    bitmapImage.EndInit();
                }
                return BitmapFrame.Create((BitmapSource)bitmapImage);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"绘制图片时发生异常: {ex.Message}");
                return null;
            }
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
        async Task ExportReport()
        {

            var docxsPath = Settings.Default.DocxPath;
            await Task.Run(() =>
            {
                var doc = new ExportWord(Parameter);
                doc.GenerateWord(docxsPath + "\\" + ImgFolderName + "钢轨探伤检测报告");
                IsImporting = false;
                MessageBox.Success(ImgFolderName + $"钢轨探伤检测报告 保存在{docxsPath}");
            });
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
            var result = new List<Details>();

            foreach (DamageData damageData in damageDataListPara)
            {
                Details tempDetails = new();
                int count = 0;
                List<float> wights = new();
                for (int i = 0; i < damageData.DamagePoint.GetLength(0); i++)
                {
                    if (damageData.DamagePoint[i][4] == CategoryIndex)
                    {
                        if (IsSortBySimilarity)//根据不同类型来进行排序
                        {


                            wights.Add(damageData.DamagePoint[i][5]);

                        }
                        count++;
                        tempDetails = new Details()
                        {
                            FileName = Path.GetFileName(damageData.Url),
                            Count = count
                        };
                    }
                    if (
                        i == damageData.DamagePoint.GetLength(0) - 1
                        && tempDetails.FileName != null
                    )
                    {
                        if (IsSortBySimilarity)
                        {
                            tempDetails.weight = wights.Count > 0 ? wights.Max() : 0;
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
        private void InitCategorySummary(int DisplayedRulesCount=10)
        {

            List<DamageData> Datas = DamageDataList;
            var categoryAndCount = ObtainInfo.GetCategoryAndCount(Datas);

            var 标记Sum = CalculateSum(categoryAndCount, 14, 15, 16, 17, 18, 19, 39, 46, 47);
            var 焊缝Sum = CalculateSum(categoryAndCount, 2, 11, 22);
            var 轨头Sum = CalculateSum(categoryAndCount, 5, 6, 36, 13);
            var 核伤Sum = CalculateSum(categoryAndCount, 5, 6);
            var 轨腰Sum = CalculateSum(categoryAndCount, 7, 9,29);
            var 轨底Sum = CalculateSum(categoryAndCount, 37, 8);
            var 作业违规Sum = CalculateSum(categoryAndCount, 21, 39);

            List<DamageCategorySummaryTree> RootCategoryList = new List<DamageCategorySummaryTree>();
            RootCategoryList.Add(new DamageCategorySummaryTree() { Name = "正常标记", Children = new() });
            RootCategoryList.Add(new DamageCategorySummaryTree() { Name = "伤损标记", Children = new() });
            RootCategoryList.Add(new DamageCategorySummaryTree() { Name = $"作业标记 总次数{标记Sum}", Children = new() });

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
                                Children = GetCategorySummary(x.Value, DamageDataList)
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
                            Children = GetCategorySummary(x.Value, DamageDataList)
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
                                Children = GetCategorySummary(x.Value, DamageDataList,true)
                                .OrderByDescending(x => x.weight).ToList()
                            });
                        }
                        else if (x.Value == 36 || x.Value == 13)
                        {
                            轨头Category.Add(new DamageCategoryTree()
                            {
                                Name = $"{DamageIdToDamageName(x.Value)}",
                                Count = x.Key,
                                Children = GetCategorySummary(x.Value, DamageDataList, true)
                                .OrderByDescending(x => x.weight).ToList()
                            });
                        }
                        else if (x.Value == 9 || x.Value == 7||x.Value==29)
                        {
                            轨腰Category.Add(new DamageCategoryTree()
                            {
                                Name = $"{DamageIdToDamageName(x.Value)}",
                                Count = x.Key,
                                Children = GetCategorySummary(x.Value, DamageDataList, true)
                                .OrderByDescending(x => x.weight).ToList()
                            });
                        }
                        else if (x.Value == 37 || x.Value == 8)
                        {
                            轨底Category.Add(new DamageCategoryTree()
                            {
                                Name = $"{DamageIdToDamageName(x.Value)}",
                                Count = x.Key,
                                Children = GetCategorySummary(x.Value, DamageDataList, true)
                                .OrderByDescending(x => x.weight).ToList()
                            });
                        }
                    
                }
                else if (DamageIdToBrush(x.Value) == Brushes.YellowGreen)
                {
                    if (x.Value == 21 || x.Value == 39)
                    {
                        (RootCategoryList[2].Children[0] as DamageCategorySummaryTree).Children.Add(new DamageCategoryTree()
                        {
                            Name = $"{DamageIdToDamageName(x.Value)}",
                            Count = x.Key,
                            ColorBrush = Brushes.YellowGreen,
                            Children = GetCategorySummary(x.Value, DamageDataList)
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
                            Children = GetCategorySummary(x.Value, DamageDataList)
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

        ObservableCollection<OcrData> locationMileageImgs = new ObservableCollection<OcrData>();
        public ObservableCollection<OcrData> LocationMileageImgs { get => locationMileageImgs; set => locationMileageImgs = value; }

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
                LocationMileageImgs = new ObservableCollection<Records.OcrData>(resutl.Select((Records.OcrData x) => new Records.OcrData(Path.GetFileName(x.ImgFullPath), x.MileageText)));
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
                // 仅在非空时更新数据库
                if (!string.IsNullOrEmpty(value) || HasDamageImg || HasNoDamageImg)
                {
                    DataAccess.UpdateImageRemark(SqlImgInfos, ImgPath, value);
                }
            }
        }



        bool hasNoDamageImg;

     public   bool HasNoDamageImg
        {
            get => hasNoDamageImg;
            set
            {


                
                    SetProperty(ref hasNoDamageImg, value);
                    DamageCheckCommand.NotifyCanExecuteChanged();

                    if (value == true)
                    {
                        ImgBorderBrush = Brushes.LightGreen;
                        IsReadRemarkOnly = false;
                        UpdateIsConfirmDamage(ImgPath, false);
                        var r = SqlImgInfos.Where(x => Path.GetFileName(x.ImgPath) == Path.GetFileName(ImgPath)).FirstOrDefault()?.Remark;

                        if (r == null || r == "")
                        {
                        DamageRemark = "无伤";
                        }
                        else
                        {
                            DamageRemark = r;
                        }

                    }
                    else if (value == false && HasDamageImg == false)
                    {
                        UpdateIsConfirmDamage(ImgPath, null);
                        IsReadRemarkOnly = true;
                        DamageRemark = "";
                        ImgBorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(211, 211, 211));
                    

                }
            }
        }

       
        bool hasDamageImg;
        public bool HasDamageImg
        {
            get => hasDamageImg;
            set
            {
               
                    SetProperty(ref hasDamageImg, value);
                    NoDamageCheckCommand.NotifyCanExecuteChanged();

                    if (value == true)
                    {
                        ImgBorderBrush = Brushes.OrangeRed;
                        IsReadRemarkOnly = false;
                        UpdateIsConfirmDamage(ImgPath, true);



                        var r = SqlImgInfos.Where(x => Path.GetFileName(x.ImgPath) == Path.GetFileName(ImgPath)).FirstOrDefault()?.Remark;
                        if (r == null || r == "")
                        {
                            DamageRemark = "疑似有伤";
                        }
                        else
                        {
                            DamageRemark = r;
                        }
                    }
                    else if (value == false && HasNoDamageImg == false)
                    {
                        UpdateIsConfirmDamage(ImgPath, null);
                        IsReadRemarkOnly = true;
                        DamageRemark = "";
                        ImgBorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(211, 211, 211));
                    }               
            }
        }



        bool  CanNoDamageCheck() => !HasDamageImg&&ImgSource!=null;
        bool CanDamageCheck() => !HasNoDamageImg&&ImgSource != null;

        [RelayCommand(CanExecute =nameof(CanNoDamageCheck))]
        void NoDamageCheck(string str)
        {
            if (str == "鼠标点击") return;

            HasNoDamageImg = !HasNoDamageImg;
        }
        [RelayCommand(CanExecute = nameof(CanDamageCheck))]
        void DamageCheck(string str)
        {
            if (str=="鼠标点击") return;

            HasDamageImg = !HasDamageImg;
        }
        #endregion

        
    }
}

