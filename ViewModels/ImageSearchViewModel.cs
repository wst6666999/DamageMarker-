using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DamageMaker.Models;
using DamageMaker.Properties;
using DamageMaker.SqliteServer;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using MessageBoxButton = System.Windows.MessageBoxButton;
using System.Text.RegularExpressions;
using DamageMaker.FileHandle;
using DamageMarker.ViewModels;
using ClosedXML.Excel;
using System.Collections.Concurrent;
using OpenCvSharp;
using DamageMaker.Views;
using System.Diagnostics;
using DocumentFormat.OpenXml.Spreadsheet;

namespace DamageMaker.ViewModels
{
    public partial class ImageSearchViewModel : ObservableObject
    {
        #region 属性定义

        private List<SqlImgInfo> _imgInfos;
        private static readonly object _resultFileLock = new object();
        private static readonly object _jsonSaveLock = new object();
        private static readonly object _fileAndDataAccessLock = new object();

        [ObservableProperty]
        private ObservableCollection<SqlImgInfo> _currentPeriodResults = new();

        [ObservableProperty]
        private ObservableCollection<CompareResult> _comparePeriodResults = new();

        [ObservableProperty]
        private int _selectedIndex = -1;

        [ObservableProperty]
        private int _compareSelectedIndex = -1;

        [ObservableProperty]
        private string _selectedRailType = "全部";

        [ObservableProperty]
        private string _selectedLineDirection = "全部";

        [ObservableProperty]
        private string _selectedLineType = "全部";

        [ObservableProperty]
        private string _selectedWeldType = "全部";

        [ObservableProperty]
        private string _mileageFilter = string.Empty;

        [ObservableProperty]
        private string? _startMileage = string.Empty;

        [ObservableProperty]
        private string? _endMileage = string.Empty;

        [ObservableProperty]
        private ObservableCollection<DateTime> _availableCompareDates = new();

        [ObservableProperty]
        private DateTime? _selectedCompareDate;

        [ObservableProperty]
        private string _selectedComparePeriod = string.Empty;

        [ObservableProperty]
        private string _currentPeriodInfo = "当前搜索条件的数据";

        [ObservableProperty]
        private string _comparePeriodInfo = "请选择对比周期和日期";

        [ObservableProperty]
        private string _compareStatus = "";

        [ObservableProperty]
        private string _compareInfo = "";

        [ObservableProperty]
        private ObservableCollection<string> _availablePeriods = new();

        public List<string> LineDirections { get; } = new List<string> { "全部", "上行", "下行", "单行", "站线" };

        public List<string> WeldTypes { get; } = new List<string> {
            "全部", "接头", "零度迟到波", "普通焊缝", "七十度螺孔回波", "轨形变换", "有标记核伤",
            "焊缝核伤", "螺孔裂纹", "零度异常", "轨腰裂纹", "加固焊缝", "断面", "轨面剥离",
            "焊缝标记", "无伤标记", "焊缝且无伤标记", "轻伤标记", "轻伤发展", "重伤标志",
            "焊缝且无焊缝标记", "假像波", "岔心", "正常螺孔", "水平裂纹", "鱼鳞伤", "轨底裂纹",
            "接头出波不全", "厂焊", "铝热焊", "超速", "倒车", "失底波"
        };

        public List<string> LineTypes { get; }

        public List<string> LineRailTypes { get; } = new List<string> { "全部", "左股", "右股" };

        #endregion

        #region 构造函数

        public ImageSearchViewModel()
        {
            // 初始化空集合
            _imgInfos = new List<SqlImgInfo>();
            CurrentPeriodResults = new ObservableCollection<SqlImgInfo>();
            ComparePeriodResults = new ObservableCollection<CompareResult>();
            AvailablePeriods = new ObservableCollection<string> { "请选择对比周期" };
            AvailableCompareDates = new ObservableCollection<DateTime>();

            // 只加载配置数据
            var routeLinesFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "RouteLines.txt");
            if (File.Exists(routeLinesFilePath))
            {
                LineTypes = File.ReadAllLines(routeLinesFilePath, Encoding.UTF8)
                                     .Select(line => line.Trim())
                                     .Where(line => !string.IsNullOrEmpty(line))
                                     .ToList();
                LineTypes.Insert(0, "全部");
            }
            else
            {
                LineTypes = new List<string> { "全部", "京广", "合九" };
            }

            // 延迟加载数据库数据
            _ = InitializeDataAsync();
        }

        /// <summary>
        /// 异步初始化数据
        /// </summary>
        private async Task InitializeDataAsync()
        {
            try
            {
                CompareStatus = "正在初始化数据...";
                // 自动获取当前数据的线别、上下行、左右股
                LoadCurrentRouteInfo();
                await LoadDatabaseDataAsync();

                if (_imgInfos.Count > 0)
                {
                    await LoadCurrentPeriodDataAsync(_imgInfos);
                }

                await LoadAvailablePeriodsAsync();
                CompareStatus = "就绪";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"初始化数据失败: {ex.Message}");
                CompareStatus = "初始化失败";
            }
        }

        private async Task LoadDatabaseDataAsync()
        {
            try
            {
                await Task.Run(() =>
                {
                    using (var sqlHelper = new SQLHelper(Settings.Default.SqlPath))
                    {
                        var railWayName = MainWindowViewModel.NeedSavedInfo?.RailWayInfo.RailWayName;
                        long folderId = sqlHelper.GetFolderIdByRailWayName(railWayName);
                        _imgInfos = sqlHelper.GetImagesByFolderId(folderId);
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载数据库数据失败: {ex.Message}");
                _imgInfos = new List<SqlImgInfo>();
            }
        }
        #endregion

        //自动获取当前打开数据的线别、上下行、左右股，然后赋值给搜索页面
        private void LoadCurrentRouteInfo()
        {
            var railWayInfo = MainWindowViewModel.NeedSavedInfo?.RailWayInfo;

            if (railWayInfo == null)
            {
                SelectedLineType = "全部";
                SelectedLineDirection = "全部";
                SelectedRailType = "全部";
                StartMileage = string.Empty;
                EndMileage = string.Empty;
                return;
            }

            SelectedLineType = string.IsNullOrWhiteSpace(railWayInfo.SelectedRouteLine)
                ? "全部"
                : railWayInfo.SelectedRouteLine.Trim();

            SelectedLineDirection = string.IsNullOrWhiteSpace(railWayInfo.SelectedUpOrDown)
                ? "全部"
                : railWayInfo.SelectedUpOrDown.Trim();

            SelectedRailType = string.IsNullOrWhiteSpace(railWayInfo.SelectedRailType)
                ? "全部"
                : railWayInfo.SelectedRailType.Trim();

            StartMileage = string.IsNullOrWhiteSpace(railWayInfo.StartMileage)
                ? string.Empty
                : railWayInfo.StartMileage.Trim();

            EndMileage = string.IsNullOrWhiteSpace(railWayInfo.EndMileage)
                ? string.Empty
                : railWayInfo.EndMileage.Trim();

        }


        #region 属性变更处理

        partial void OnSelectedLineTypeChanged(string value)
        {
            _ = LoadAvailablePeriodsAsync();
        }

        partial void OnSelectedLineDirectionChanged(string value)
        {
            _ = LoadAvailablePeriodsAsync();
        }
        partial void OnSelectedRailTypeChanged(string value)
        {
            _ = LoadAvailablePeriodsAsync();
        }

        partial void OnSelectedComparePeriodChanged(string value)
        {
            if (string.IsNullOrEmpty(value) || value == "请选择对比周期")
            {
                return;
            }

            // 异步执行所有操作
            _ = HandleSelectedComparePeriodChangedAsync(value);
        }
        //debug周期对比
        private string DebugValue(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? "null" : value.Trim();
        }

        /// <summary>
        /// 异步处理对比周期变更
        /// </summary>
        private async Task HandleSelectedComparePeriodChangedAsync(string value)
        {
            try
            {
                // 1. 加载可用日期
                await LoadCompareDatesAsync();

                // 2. 获取对比周期号
                int comparePeriodNumber = 0;
                if (!string.IsNullOrEmpty(value))
                {
                    var numStr = value.Replace("第", "").Replace("周期", "");
                    if (!int.TryParse(numStr, out comparePeriodNumber))
                    {
                        comparePeriodNumber = 0;
                    }
                }

                if (comparePeriodNumber == 0)
                {
                    HandyControl.Controls.MessageBox.Show("无效的对比周期", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // 3. 空字符串转 null，避免空字符串参与数据库区间查询
                string? startMileageFilter = string.IsNullOrWhiteSpace(StartMileage)
                    ? "null"
                    : StartMileage.Trim();

                string? endMileageFilter = string.IsNullOrWhiteSpace(EndMileage)
                    ? "null"
                    : EndMileage.Trim();

                string? lineTypeFilter = SelectedLineType == "全部" ? null : SelectedLineType;
                string? lineDirectionFilter = SelectedLineDirection == "全部" ? null : SelectedLineDirection;
                string? railTypeFilter = SelectedRailType == "全部" ? null : SelectedRailType;


                Console.WriteLine("========== 查询对比周期焊缝数据 ==========");
                Console.WriteLine($"线别: [{DebugValue(lineTypeFilter)}]");
                Console.WriteLine($"上下行: [{DebugValue(lineDirectionFilter)}]");
                Console.WriteLine($"左右股: [{DebugValue(railTypeFilter)}]");
                Console.WriteLine($"周期: [{comparePeriodNumber}]");
                Console.WriteLine($"日期: [{SelectedCompareDate:yyyy-MM-dd}]");
                Console.WriteLine($"StartMileage: [{DebugValue(startMileageFilter)}]");
                Console.WriteLine($"EndMileage: [{DebugValue(endMileageFilter)}]");

                // 4. 获取对比周期的焊缝数据
                List<WeldPositionInfo> weldPositions;
                lock (_fileAndDataAccessLock)
                {
                    weldPositions = DataAccess.GetWeldPositionsByRouteAndDirectionAndCycle(
                        lineTypeFilter,
                        lineDirectionFilter,
                        railTypeFilter,
                        comparePeriodNumber,
                        startMileageFilter,
                        endMileageFilter
                    );
                }

                Console.WriteLine($"查询到的焊缝数量: [{weldPositions?.Count ?? 0}]");
                if (weldPositions == null || weldPositions.Count == 0)
                {
                    HandyControl.Controls.MessageBox.Show("未找到对比周期的焊缝数据", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 5. 加载对比周期图片
                await LoadComparePeriodImagesForStartComparison(weldPositions, comparePeriodNumber);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"处理对比周期变更失败: {ex.Message}");
                HandyControl.Controls.MessageBox.Show($"处理对比周期变更失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region 刷新当前周期和对比周期
        /// <summary>
        /// 开始对比时加载对比周期图片
        /// </summary>
        private async Task LoadComparePeriodImagesForStartComparison(List<WeldPositionInfo> weldPositions, int comparePeriodNumber)
        {
            try
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    ComparePeriodResults.Clear();
                });

                var compareImages = new List<SqlImgInfo>();
                using (var sqlHelper = new SQLHelper(Settings.Default.SqlPath))
                {
                    foreach (var weldPosition in weldPositions)
                    {
                        // 获取焊缝对应的图片
                        var image = await sqlHelper.GetImagesByImagePathAsync(weldPosition.ImagePath);
                        if (image != null)
                        {
                            compareImages.Add(image);
                        }
                    }
                }

                // 加载对比周期数据到界面
                await LoadComparePeriodDataAsync(compareImages);

                ComparePeriodInfo = $"对比周期: {SelectedComparePeriod} ({SelectedCompareDate?.ToString("yyyy-MM-dd")}) - {compareImages.Count}张图片";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载对比周期图片失败: {ex.Message}");
            }
        }
        private async Task LoadComparePeriodDataAsync(List<SqlImgInfo> images)
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                ComparePeriodResults.Clear();
            });

            // 每个里程显示一张对比图片
            foreach (var img in images)
            {
                using (var sqlHelper = new SQLHelper(Settings.Default.SqlPath))
                {
                    try
                    {
                        var damageTypes = await sqlHelper.GetDamageTypesByImageIdAsync(img.ImgId);
                        if (damageTypes != null && damageTypes.Any())
                        {
                            var typeNames = new List<string>();
                            foreach (var damageType in damageTypes)
                            {
                                try
                                {
                                    int damageTypeInt = Convert.ToInt32(damageType);
                                    var typeName = Records.DamageCategoryData
                                        .FirstOrDefault(r => r.Id == damageTypeInt)?.CategoryName;

                                    if (!string.IsNullOrEmpty(typeName))
                                    {
                                        typeNames.Add(typeName);
                                    }
                                    else
                                    {
                                        typeNames.Add("未知"); 
                                    }
                                }
                                catch
                                {
                                    typeNames.Add("未知");
                                }
                            }
                            img.DamageType = string.Join(",", typeNames.Distinct());
                        }
                        else
                        {
                            img.DamageType = "无伤损";
                        }
                    }
                    catch (Exception)
                    {
                        img.DamageType = "查询失败";
                    }
                }


                var compareResult = new CompareResult
                {
                    Mileage = img.Mileage,
                    DamageType = img.DamageType,
                    SaveTime = img.SaveTime
                };

                // 加载图片数据
                if (img.ImageData != null && img.ImageData.Length > 0)
                {
                    await Task.Run(() =>
                    {
                        using var stream = new MemoryStream(img.ImageData);
                        var bitmapImage = new BitmapImage();
                        bitmapImage.BeginInit();
                        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                        bitmapImage.StreamSource = stream;
                        bitmapImage.EndInit();
                        bitmapImage.Freeze();

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            compareResult.ImgData = bitmapImage;
                        });
                    });
                }

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    ComparePeriodResults.Add(compareResult);
                });
            }
        }

        /// <summary>
        /// 对比完成后重新加载当前周期图片
        /// </summary>
        private async Task ReloadCurrentPeriodImagesAfterComparison()
        {
            try
            {
                CompareStatus = "正在刷新当前周期数据...";

                await LoadCurrentPeriodDataAsync(_imgInfos);

                CurrentPeriodInfo = $"当前周期: {CurrentPeriodResults.Count} 条记录";
                CompareInfo = $"当前周期: {CurrentPeriodResults.Count} 条 | 对比周期: {ComparePeriodResults.Count} 条";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"刷新当前周期图片失败: {ex.Message}");
            }
        }
        private async Task LoadCurrentPeriodDataAsync(List<SqlImgInfo> images)
        {
            try
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    CurrentPeriodResults.Clear();
                });

                foreach (var img in images)
                {
                    var preparedImg = await PrepareSingleImageAsync(img);
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        CurrentPeriodResults.Add(preparedImg);
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载当前周期数据异常: {ex.Message}");
            }
        }
        private async Task<SqlImgInfo> PrepareSingleImageAsync(SqlImgInfo img)
        {
            var preparedImg = new SqlImgInfo
            {
                ImgId = img.ImgId,
                FolderId = img.FolderId,
                ImgPath = img.ImgPath,
                Mileage = img.Mileage,
                SaveTime = img.SaveTime,
                ImageData = img.ImageData,
                DamageType = img.DamageType
            };
            // 加载图片数据
            if (img.ImageData != null && img.ImageData.Length > 0)
            {
                try
                {
                    preparedImg.ImgData = await LoadBitmapImageAsync(img.ImageData);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"加载图片数据异常: {ex.Message}");
                }
            }

            return preparedImg;
        }
        private async Task<BitmapImage> LoadBitmapImageAsync(byte[] imageData)
        {
            return await Task.Run(() =>
            {
                using var stream = new MemoryStream(imageData);
                var bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.StreamSource = stream;
                bitmapImage.EndInit();
                bitmapImage.Freeze();

                return bitmapImage;
            });
        }

        #endregion
        private MainWindowViewModel MainViewModel
        {
            get
            {
                var instance = MainWindowViewModel.Instance;
                return instance;
            }
        }

        #region 线程安全控制
        // 添加一个全局的信号量，确保一次只有一个对比任务在执行数据访问
        private static readonly SemaphoreSlim _comparisonSemaphore = new SemaphoreSlim(1, 1);
        private readonly object _damageDataLock = new object();
        #endregion

        #region 周期对比
        [RelayCommand]
        private async Task StartCompareAsync()
        {
            if (string.IsNullOrEmpty(SelectedComparePeriod) || SelectedComparePeriod == "请选择对比周期" || SelectedCompareDate == null)
            {
                HandyControl.Controls.MessageBox.Show("请选择对比周期和日期", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // ============ 关键修改：使用信号量确保一次只有一个对比任务 ============
            if (!await _comparisonSemaphore.WaitAsync(TimeSpan.FromSeconds(0)))
            {
                HandyControl.Controls.MessageBox.Show("当前已有对比任务在执行中，请等待完成", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                CompareStatus = "正在启动焊缝对比处理...";

                // 1. 获取对比周期号
                int comparePeriodNumber = 0;
                if (!string.IsNullOrEmpty(SelectedComparePeriod))
                {
                    var numStr = SelectedComparePeriod.Replace("第", "").Replace("周期", "");
                    int.TryParse(numStr, out comparePeriodNumber);
                }

                if (comparePeriodNumber == 0)
                {
                    HandyControl.Controls.MessageBox.Show("无效的对比周期", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // 2. 获取当前周期的图片文件列表 - 放在锁内
                string[] currentPeriodImageFiles;
                List<DamageData> currentDamageData;
                lock (_fileAndDataAccessLock)
                {
                    var mainVm = MainViewModel;  
                    currentPeriodImageFiles = mainVm.imgFiles ?? Array.Empty<string>();
                    currentDamageData = mainVm.DamageDataList ?? new List<DamageData>();
                }



                if (currentPeriodImageFiles?.Length == 0)
                {
                    HandyControl.Controls.MessageBox.Show("当前周期没有找到图片文件", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                //将所有图片里面含有52的标签发送给后端分析
                var imagesWithSpecificTags = currentDamageData
                    .Where(d => d.DamagePoint != null && d.DamagePoint.Any(p => p.Length > 4 && (p[4] == 52)))
                    .Select(d => d.Url)
                    .ToHashSet();
                // 获取对应的DamageData对象
                var damageDataWithTags = currentDamageData
                    .Where(d => imagesWithSpecificTags.Contains(d.Url))
                    .ToList();
                if (damageDataWithTags.Count == 0)
                {
                    CompareStatus = "没有找到包含标签52的图片";
                    HandyControl.Controls.MessageBox.Show("当前周期没有找到包含标签52的图片", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                // 5. 将imagesWithSpecificTags一次性发送给后端处理
                if (damageDataWithTags.Count > 0)
                {
                    CompareStatus = $"正在预处理 {damageDataWithTags.Count} 张含有特定标签的图片...";

                    try
                    {
                        bool success = await SendDamageDataToBackendAsync(damageDataWithTags);

                        if (success)
                        {
                            CompareStatus = $"预处理完成，处理了 {damageDataWithTags.Count} 张图片";
                        }
                        else
                        {
                            CompareStatus = "预处理失败";
                            HandyControl.Controls.MessageBox.Show("预处理失败", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                    catch (Exception ex)
                    {
                        CompareStatus = "预处理异常";
                        Console.WriteLine($"预处理失败: {ex.Message}");
                        HandyControl.Controls.MessageBox.Show($"预处理失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }

                // 3. 获取对比周期的焊缝数据 - 数据库访问加锁
                List<WeldPositionInfo> weldPositions;

                string? startMileageFilter = string.IsNullOrWhiteSpace(StartMileage)
                    ? null
                    : StartMileage.Trim();

                string? endMileageFilter = string.IsNullOrWhiteSpace(EndMileage)
                    ? null
                    : EndMileage.Trim();

                string? lineTypeFilter = SelectedLineType == "全部" ? null : SelectedLineType;
                string? lineDirectionFilter = SelectedLineDirection == "全部" ? null : SelectedLineDirection;
                string? railTypeFilter = SelectedRailType == "全部" ? null : SelectedRailType;

                Console.WriteLine("========== 开始对比时查询焊缝数据 ==========");
                Console.WriteLine($"线别: [{DebugValue(lineTypeFilter)}]");
                Console.WriteLine($"上下行: [{DebugValue(lineDirectionFilter)}]");
                Console.WriteLine($"左右股: [{DebugValue(railTypeFilter)}]");
                Console.WriteLine($"周期: [{comparePeriodNumber}]");
                Console.WriteLine($"日期: [{SelectedCompareDate:yyyy-MM-dd}]");
                Console.WriteLine($"StartMileage: [{DebugValue(startMileageFilter)}]");
                Console.WriteLine($"EndMileage: [{DebugValue(endMileageFilter)}]");

                lock (_fileAndDataAccessLock)
                {
                    weldPositions = DataAccess.GetWeldPositionsByRouteAndDirectionAndCycle(
                        lineTypeFilter,
                        lineDirectionFilter,
                        railTypeFilter,
                        comparePeriodNumber,
                        startMileageFilter,
                        endMileageFilter
                    );
                }


                Console.WriteLine($"开始对比时查询到的焊缝数量: [{weldPositions?.Count ?? 0}]");
                if (weldPositions == null || weldPositions.Count == 0)
                {
                    HandyControl.Controls.MessageBox.Show("未找到对比周期的焊缝数据", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // ============ 第一步：加载对比周期图片 ============
                await LoadComparePeriodImagesForStartComparison(weldPositions, comparePeriodNumber);

                // 4. 串行处理每个焊缝（已经是串行的foreach）
                int totalCount = weldPositions.Count;
                int currentIndex = 0;

                foreach (var weldPosition in weldPositions)
                {
                    currentIndex++;
                    CompareStatus = $"正在处理焊缝对比 ({currentIndex}/{totalCount})...";

                    try
                    {
                        // 获取当前里程对应的所有图片（可能有多个）
                        var matchedImages = FindImagesByMileage(weldPosition.Mileage, currentPeriodImageFiles).GroupBy(Path.GetFileName) // 按文件名分组
                                    .Select(g => g.First())    // 取每组第一个
                                    .ToList(); ;

                        if (matchedImages.Count > 0)
                        {
                            // 遍历所有匹配的图片进行处理
                            foreach (var matchedImage in matchedImages)
                            {
                                // 调用对比方法 - 这里使用 await 确保串行执行
                                await CheckAndSendToBackendAsync(matchedImage, weldPosition);

                                // 每个图片处理之间的延迟
                                await Task.Delay(100);
                            }
                        }
                        else
                        {
                            Console.WriteLine($"未找到里程 {weldPosition.Mileage} 对应的图片");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"处理焊缝 {weldPosition.Mileage} 失败: {ex.Message}");
                    }

                    // 焊缝之间的延迟
                    await Task.Delay(200);
                }

                CompareStatus = $"焊缝对比完成，成功处理 {totalCount} 个焊缝";

                // ============ 第二步：对比完成后，重新加载当前周期图片 ============
                await ReloadCurrentPeriodImagesAfterComparison();

                // ============ 保存DamageDataList到Json文件 - 加锁 ============
                lock (_fileAndDataAccessLock)
                {
                    var mainVm = MainViewModel;
                    // 保存更新后的DamageDataList到Json文件
                    lock (_fileAndDataAccessLock)
                    {
                        // 重新保存到result.json1中
                        AboutJson.SaveJson<List<DamageData>>(mainVm.DamageDataList,
                            Path.Combine(Settings.Default.InPath, mainVm.ImgFolderName), "result1.json");
                    }

                    //删除outPath的ImgFolderName文件夹
                    var outPath = Settings.Default.OutPath;
                    var outFolderPath = Path.Combine(outPath, mainVm.ImgFolderName);
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
                    // 重新加载主界面缩略图（当前周期）
                    mainVm.ShowThumbnails(mainVm.Parameter);
                }

                await Task.Delay(500);
                CompareStatus = "就绪";
                HandyControl.Controls.MessageBox.Show($"焊缝对比处理完成! 成功处理 {totalCount} 个焊缝", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                CompareStatus = "对比处理失败";
                Console.WriteLine($"对比处理失败: {ex.Message}");
                HandyControl.Controls.MessageBox.Show($"开始对比时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // ============ 关键修改：释放信号量 ============
                _comparisonSemaphore.Release();
            }
        }

        /// <summary>
        /// 发送包含标签2或6的DamageData到后端分析
        /// </summary>
        private async Task<bool> SendDamageDataToBackendAsync(List<DamageData> damageDataList)
        {
            try
            {
                // 1. 准备请求数据，格式化为后端期望的JSON格式
                var requestData = new List<object>();

                foreach (var damageData in damageDataList)
                {
                    var entry = new
                    {
                        url = damageData.Url,
                        damage = damageData.DamagePoint
                    };

                    requestData.Add(entry);
                }

                // 2. 将数据转换为JSON格式 - 使用 System.Text.Json
                string jsonInput = JsonSerializer.Serialize(requestData, new JsonSerializerOptions
                {
                    WriteIndented = false // 对应 Formatting.None
                });

                // 3. 调用后端API进行分析
                using (var httpClient = new HttpClient())
                {
                    httpClient.Timeout = TimeSpan.FromMinutes(5);

                    var content = new MultipartFormDataContent();
                    content.Add(new StringContent(jsonInput), "input_text");

                    // 添加输出目录
                    var outputDir = Settings.Default.OutPath;
                    content.Add(new StringContent(outputDir), "output_dir");

                    var response = await httpClient.PostAsync("http://127.0.0.1:3333/rule_compare", content);

                    if (response.IsSuccessStatusCode)
                    {
                        var responseContent = await response.Content.ReadAsStringAsync();

                        // 使用 System.Text.Json 解析响应
                        using JsonDocument document = JsonDocument.Parse(responseContent);
                        var root = document.RootElement;

                        if (root.TryGetProperty("status", out var statusElement) &&
                            statusElement.GetString() == "success")
                        {
                            Console.WriteLine($"后端处理成功: {root.GetProperty("message").GetString()}");

                            // 5. 处理返回结果
                            string outputFilePath = root.GetProperty("output_file").GetString();
                            await ProcessBackendResponseAsync(outputFilePath, damageDataList);

                            return true;
                        }
                        else
                        {
                            string? message = root.TryGetProperty("message", out var msgElement)
                                ? msgElement.GetString()
                                : "未知错误";
                            Console.WriteLine($"后端处理失败: {message}");
                            return false;
                        }
                    }
                    else
                    {
                        Console.WriteLine($"HTTP请求失败: {response.StatusCode}");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"发送DamageData到后端失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 处理后端返回的结果
        /// </summary>
        private async Task ProcessBackendResponseAsync(string outputFilePath, List<DamageData> originalData)
        {
            try
            {
                if (!File.Exists(outputFilePath))
                {
                    Console.WriteLine("后端返回的结果文件不存在");
                    return;
                }

                // 读取后端返回的结果文件  
                string jsonContent;
                jsonContent = await File.ReadAllTextAsync(outputFilePath);

                // 使用 System.Text.Json 反序列化  
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true // 如果不确定大小写，可以启用这个选项  
                };

                var resultEntries = JsonSerializer.Deserialize<List<DamageData>>(jsonContent, options);

                if (resultEntries == null || resultEntries.Count == 0)
                {
                    Console.WriteLine("后端返回的结果文件内容为空");
                    return;
                }

                // 遍历结果，更新对应的DamageData对象  
                foreach (var resultEntry in resultEntries)
                {
                    var matchingData = MainViewModel.DamageDataList?.FirstOrDefault(d => d.Url == resultEntry.Url);
                    if (matchingData != null)
                    {
                        // 更新DamagePoint  
                        matchingData.DamagePoint = resultEntry.DamagePoint;
                    }
                }

                // 保存更新后的DamageDataList到Json文件  
                lock (_fileAndDataAccessLock)
                {
                    // 重新保存到result.json1中  
                    AboutJson.SaveJson<List<DamageData>>(MainViewModel.DamageDataList,
                        Path.Combine(Settings.Default.InPath, MainViewModel.ImgFolderName), "result1.json");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"处理后端响应失败: {ex.Message}");
            }
        }
        #endregion

        #region 核心对比逻辑方法
        private async Task CheckAndSendToBackendAsync(string imgFile, WeldPositionInfo weldPosition)
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
                for (int i = 0; i < 2; i++)
                {
                    firstSendList.Add(new
                    {
                        url = (string?)null,
                        damage = Array.Empty<float[]>()
                    });
                }
                firstSendList.Add(new
                {
                    url = imgFile,
                    damage = GetDamagePointsForImage(imgFile)
                });

                // 添加对比周期图片
                float[][] annotationArray = weldPosition.Annotations?.ToArray() ?? Array.Empty<float[]>();
                firstSendList.Add(new
                {
                    url = tempPath,
                    damage = annotationArray
                });

                var backendResponse = await SendToBackendAsync(firstSendList, processId);

                if (backendResponse?.Status == "success")
                {
                    // 处理结果
                    lock (_damageDataLock)
                        lock (_fileAndDataAccessLock)
                        {
                            ProcessResultFileSync(backendResponse.OutputFile, processId);
                        }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{processId}] ❌ 处理过程中出错: {ex.Message}");
            }
        }
        private async Task<BackendApiResponse> SendToBackendAsync(List<object> sendList, string processId)
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

                string sendListJson = System.Text.Json.JsonSerializer.Serialize(sendList);
                formData.Add(new StringContent(sendListJson, Encoding.UTF8, "application/json"), "input_text");

                var response = await httpClient.PostAsync("height_compare", formData);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    return System.Text.Json.JsonSerializer.Deserialize<BackendApiResponse>(responseContent);
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
        private void ProcessResultFileSync(string resultFilePath,string processId)
        {
            // ============ 注意：这个方法现在只在锁内被调用 ============
            try
            {
                string jsonContent;

                lock (_fileAndDataAccessLock)
                {
                    jsonContent = File.ReadAllText(resultFilePath);
                }

                var resultEntries = System.Text.Json.JsonSerializer.Deserialize<List<DamageData>>(jsonContent);
                if (resultEntries == null)
                {
                    Console.WriteLine($"[{processId}] ❌ 结果文件内容为空");
                    return;
                }
                var dataEntries = resultEntries.SkipLast(1).ToList();
                var mainVm = MainViewModel;
                var damageDataList = mainVm?.DamageDataList;
                var sqlImgInfos = mainVm?.SqlImgInfos;

                // 处理每张数据图
                for (int i = 0; i < dataEntries.Count; i++)
                {
                    var dataEntry = dataEntries[i];

                    string originalImagePath = dataEntry.Url;
                    string fileName = Path.GetFileName(originalImagePath);

                    // 1. 更新_imgInfos的信息
                    var imgInfoInList = _imgInfos.FirstOrDefault(x =>
                        !string.IsNullOrEmpty(x.ImgPath) &&
                        Path.GetFileName(x.ImgPath) == fileName);

                    if (imgInfoInList != null)
                    {
                        try
                        {
                            using (var sqlHelper = new SQLHelper(Settings.Default.SqlPath))
                            {
                                // 根据ImagePath从DamageAnnotations中获取所有damageType
                                var annotations = sqlHelper.GetDamageAnnotationsByImagePath(imgInfoInList.ImgPath);

                                if (annotations != null && annotations.Any())
                                {
                                    // 提取所有不重复的damageType数字
                                    var damageTypeIds = annotations
                                        .Select(a => (int)a.DamageType)
                                        .Distinct()
                                        .ToList();

                                    // 将数字ID转换为名称字符串
                                    var damageTypeNames = new List<string>();
                                    foreach (var damageTypeId in damageTypeIds)
                                    {
                                        var typeName = Records.DamageCategoryData
                                            .FirstOrDefault(r => r.Id == damageTypeId)?.CategoryName;

                                        if (!string.IsNullOrEmpty(typeName))
                                        {
                                            damageTypeNames.Add(typeName);
                                        }
                                        else
                                        {
                                            damageTypeNames.Add($"未知({damageTypeId})");
                                        }
                                    }

                                    imgInfoInList.DamageType = string.Join(",", damageTypeNames);
                                }
                                else
                                {
                                    imgInfoInList.DamageType = "无伤损";
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[{processId}] 更新_imgInfos伤损类型失败: {ex.Message}");
                        }
                    }

                    // 2. 更新DamageDataList
                    var originalData = damageDataList?.FirstOrDefault(x =>
                        x != null &&
                        !string.IsNullOrEmpty(x.Url) &&
                        Path.GetFileName(x.Url) == fileName);

                    if (originalData != null && dataEntry?.DamagePoint != null)
                    {
                        originalData.DamagePoint = dataEntry.DamagePoint;
                    }

                    // 3. 更新数据库标注
                    var imgInfo = sqlImgInfos?.FirstOrDefault(x =>
                        Path.GetFileName(x.ImgPath) == fileName);

                    if (imgInfo != null && dataEntry?.DamagePoint != null)
                    {
                        var damageList = dataEntry.DamagePoint.ToList();
                        UpdateDatabaseAnnotations(imgInfo, damageList, processId);
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
                    if (damagePoints != null && damagePoints.Any())
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

        #endregion
        #region 辅助方法





        private void ParseFileName(string fileName, out int idx, out string mileage)
        {
            idx = 0;
            mileage = string.Empty;

            try
            {
                var parts = Path.GetFileNameWithoutExtension(fileName).Split('_');
                if (parts.Length >= 2)
                {
                    mileage = parts[0];
                    if (int.TryParse(parts[1], out int parsedIdx))
                    {
                        idx = parsedIdx;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"解析文件名失败: {ex.Message}");
            }
        }
        private string? FindFilePathByIdx(int idx, string[] imgFiles)
        {
            if (imgFiles == null)
                return null;

            foreach (var file in imgFiles)
            {
                var fileName = Path.GetFileNameWithoutExtension(file);

                // 尝试从文件名中提取索引
                // 支持格式："299KM433M_149.png" 或 "149.png"
                var match = Regex.Match(fileName, @"\D*(\d+)$");
                if (match.Success)
                {
                    var fileIdx = int.Parse(match.Groups[1].Value);
                    if (fileIdx == idx)
                    {
                        return file;
                    }
                }
            }

            return null;
        }

        private float[][] GetDamagePointsForImage(string imagePath)
        {
            try
            {
                var mainVm = MainViewModel;
                if (mainVm?.DamageDataList != null)
                {
                    // 首先从DamageDataList查找最新的数据
                    var damageData = mainVm.DamageDataList.FirstOrDefault(x =>
                        x != null &&
                        !string.IsNullOrEmpty(x.Url) &&
                        Path.GetFileName(x.Url) == Path.GetFileName(imagePath));

                    // 如果找到了数据，直接返回
                    if (damageData?.DamagePoint != null)
                    {
                        return damageData.DamagePoint.ToArray();
                    }

                    // 如果DamageDataList中没有，尝试从_imgInfos中获取
                    var imgInfo = _imgInfos.FirstOrDefault(x =>
                        !string.IsNullOrEmpty(x.ImgPath) &&
                        Path.GetFileName(x.ImgPath) == Path.GetFileName(imagePath));

                    if (imgInfo != null)
                    {
                        // 从数据库获取最新的标注
                        using (var sqlHelper = new SQLHelper(Settings.Default.SqlPath))
                        {
                            var annotations = sqlHelper.GetDamageAnnotationsByImagePath(imgInfo.ImgPath);
                            if (annotations != null && annotations.Any())
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

        private List<string> FindImagesByMileage(string mileage, string[] imageFiles)
        {
            var result = new List<string>();

            if (string.IsNullOrEmpty(mileage) || imageFiles == null || imageFiles.Length == 0)
                return result;

            // 预先创建查找字典
            var damageDict = new Dictionary<string, DamageData>(StringComparer.OrdinalIgnoreCase);
            var damageByFileName = new Dictionary<string, DamageData>(StringComparer.OrdinalIgnoreCase);

            if (MainViewModel.DamageDataList != null)
            {
                foreach (var damage in MainViewModel.DamageDataList)
                {
                    if (damage.DamagePoint != null && damage.Url != null)
                    {
                        // 按完整路径索引
                        damageDict[damage.Url] = damage;

                        // 按文件名索引（作为备用）
                        var fileName = Path.GetFileName(damage.Url);
                        if (!string.IsNullOrEmpty(fileName))
                        {
                            damageByFileName[fileName] = damage;
                        }
                    }
                }
            }

            // 解析目标里程
            float targetMeters = ParseMileageToMeters(mileage);
            bool needRangeMatch = targetMeters > 0;

            //Console.WriteLine($"开始匹配里程: {mileage}, 目标里程(米): {targetMeters}");
            //Console.WriteLine($"总文件数: {imageFiles.Length}, 有损伤数据文件: {damageDict.Count}");

            // 使用 HashSet 来快速检查重复
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

            //Console.WriteLine($"匹配完成: 找到 {result.Count} 个文件");
            return result;
        }
        private float ParseMileageToMeters(string mileageString)
        {
            try
            {
                // 专门处理 "XXKMYYYM" 格式
                var match = Regex.Match(mileageString, @"(\d+)KM(\d+)M");
                if (match.Success)
                {
                    int km = int.Parse(match.Groups[1].Value);
                    int m = int.Parse(match.Groups[2].Value);
                    return km * 1000 + m;
                }

                // 如果没有匹配到预期的格式，返回0或抛出异常
                //Console.WriteLine($"无法解析里程: {mileageString}");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"解析里程 '{mileageString}' 时出错: {ex.Message}");
                return 0;
            }
        }
        #endregion


        #region 加载周期和日期
        private async Task LoadAvailablePeriodsAsync()
        {
            try
            {
                if (SelectedLineType == "全部" && SelectedLineDirection == "全部")
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        AvailablePeriods.Clear();
                        AvailablePeriods.Add("请选择对比周期");
                    });
                    return;
                }

                using (var sqlHelper = new SQLHelper(Settings.Default.SqlPath))
                {
                    string lineTypeFilter = SelectedLineType == "全部" ? null : SelectedLineType;
                    string lineDirectionFilter = SelectedLineDirection == "全部" ? null : SelectedLineDirection;
                    string railTypeFilter = SelectedRailType == "全部" ? null : SelectedRailType;

                    var folderIds = await sqlHelper.GetFolderIdsAsync(
                        lineTypeFilter,
                        lineDirectionFilter,
                        railTypeFilter);
                    if (folderIds.Count == 0)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            AvailablePeriods.Clear();
                            AvailablePeriods.Add("请选择对比周期");
                        });
                        return;
                    }
                    else
                    {
                        HashSet<int> periods = new HashSet<int>();
                        foreach (var id in folderIds)
                        {
                            var period = await sqlHelper.GetPeriodByFolderIdAsync(id);
                            periods.Add(period);
                        }

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            AvailablePeriods.Clear();
                            AvailablePeriods.Add("请选择对比周期");
                            foreach (var p in periods.OrderBy(x => x))
                            {
                                AvailablePeriods.Add($"第{p}周期");
                            }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    AvailablePeriods.Clear();
                    AvailablePeriods.Add("请选择对比周期");
                });
                Console.WriteLine($"加载可用周期时出错: {ex.Message}");
            }
        }

        private async Task LoadCompareDatesAsync()
        {
            int periodNumber = 0;
            if (!string.IsNullOrEmpty(SelectedComparePeriod))
            {
                var numStr = SelectedComparePeriod.Replace("第", "").Replace("周期", "");
                int.TryParse(numStr, out periodNumber);
            }

            string lineTypeFilter = SelectedLineType == "全部" ? null : SelectedLineType;
            string lineDirectionFilter = SelectedLineDirection == "全部" ? null : SelectedLineDirection;
            string railTypeFilter = SelectedRailType == "全部" ? null : SelectedRailType;

            using (var sqlHelper = new SQLHelper(Settings.Default.SqlPath))
            {
                var folderIds = await sqlHelper.GetFolderIdsAsync(
                    lineTypeFilter,
                    lineDirectionFilter,
                    railTypeFilter);

                HashSet<DateTime> dates = new HashSet<DateTime>();
                foreach (var id in folderIds)
                {
                    var dateList = await sqlHelper.GetDatesByFolderIdAndPeriodAsync(id, periodNumber);
                    foreach (var d in dateList)
                    {
                        dates.Add(d);
                    }
                }

                Application.Current.Dispatcher.Invoke(() =>
                {
                    AvailableCompareDates.Clear();
                    foreach (var d in dates.OrderBy(x => x))
                    {
                        AvailableCompareDates.Add(d);
                    }
                    SelectedCompareDate = AvailableCompareDates.FirstOrDefault();
                });
            }
        }
        #endregion
        #region 功能区
        [RelayCommand]
        private async Task SearchAsync()
        {
            try
            {
                CompareStatus = "正在搜索...";

                // 2. 获取当前搜索条件
                string lineTypeFilter = SelectedLineType == "全部" ? null : SelectedLineType;
                string lineDirectionFilter = SelectedLineDirection == "全部" ? null : SelectedLineDirection;
                string railTypeFilter = SelectedRailType == "全部" ? null : SelectedRailType;
                string weldTypeFilter = SelectedWeldType == "全部" ? null : SelectedWeldType;
                string mileageFilter = string.IsNullOrWhiteSpace(MileageFilter) ? null : MileageFilter.Trim();

                // 3. 执行筛选
                var filteredImages = _imgInfos.AsEnumerable();

                // 按线别和上下行类型筛选
                if (!string.IsNullOrEmpty(lineTypeFilter) ||
                    !string.IsNullOrEmpty(lineDirectionFilter) ||
                    !string.IsNullOrEmpty(railTypeFilter))
                {
                    // 根据线别、上下行、轨别找出符合条件的 FolderIds
                    using (var sqlHelper = new SQLHelper(Settings.Default.SqlPath))
                    {
                        var folderIds = await sqlHelper.GetFolderIdsAsync(
                            lineTypeFilter,
                            lineDirectionFilter,
                            railTypeFilter);

                        filteredImages = filteredImages.Where(img => folderIds.Contains(img.FolderId));
                    }
                }

                // 按伤损类型筛选
                if (!string.IsNullOrEmpty(weldTypeFilter))
                {
                    filteredImages = filteredImages.Where(img =>
                        !string.IsNullOrEmpty(img.DamageType) &&
                        img.DamageType.Contains(weldTypeFilter));
                }

                // 按里程筛选
                if (!string.IsNullOrEmpty(mileageFilter))
                {
                    filteredImages = filteredImages.Where(img =>
                        !string.IsNullOrEmpty(img.Mileage) &&
                        img.Mileage.Contains(mileageFilter));
                }

                // 4. 将结果转换为列表
                var resultList = filteredImages.ToList();

                // 5. 更新UI
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    CurrentPeriodResults.Clear();
                });

                // 加载数据到界面
                await LoadCurrentPeriodDataAsync(resultList);

                // 更新信息显示
                CurrentPeriodInfo = $"搜索结果: {resultList.Count} 条记录";
                CompareInfo = $"当前周期: {resultList.Count} 条 | 对比周期: {ComparePeriodResults.Count} 条";

                CompareStatus = "搜索完成";

                if (resultList.Count == 0)
                {
                    HandyControl.Controls.MessageBox.Show("未找到符合条件的图片", "提示",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                CompareStatus = "搜索失败";
                Console.WriteLine($"搜索时发生错误: {ex.Message}");
                HandyControl.Controls.MessageBox.Show($"搜索时发生错误: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        [RelayCommand]
        private void ClearCompareAsync()
        {
            ComparePeriodResults.Clear();
            ComparePeriodInfo = "对比数据已清除";
            CompareStatus = "";
            CompareInfo = $"当前周期: {CurrentPeriodResults.Count} 条";
        }

        [RelayCommand]
        private void ResetSearch()
        {
            // 不再重置为“全部”，而是重新获取当前数据的线别、上下行、左右股
            LoadCurrentRouteInfo();

            SelectedWeldType = "全部";
            MileageFilter = string.Empty;
            StartMileage = string.Empty;
            EndMileage = string.Empty;

            CurrentPeriodResults.Clear();
            ComparePeriodResults.Clear();
            CurrentPeriodInfo = "当前搜索条件的数据";
            ComparePeriodInfo = "请选择对比周期和日期";
            CompareInfo = "";

            _ = LoadAvailablePeriodsAsync();
        }

        #region 图片查看功能

        [RelayCommand]
        private void DoubleClickImage(SqlImgInfo selectedItem)
        {
            if (selectedItem == null) return;

            try
            {
                // 1. 获取当前里程的所有图片（当前周期）
                var currentMileage = selectedItem.Mileage;
                var currentImages = GetCurrentImagesByMileage(currentMileage);

                if (currentImages.Count == 0)
                {
                    HandyControl.Controls.MessageBox.Show("未找到该里程对应的图片数据", "提示",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 2. 查找对比周期的图片（从ComparePeriodResults中查找）
                BitmapImage compareImage = null;
                string compareMileage = null;

                // 从对比结果中查找相同里程的图片
                var compareResult = ComparePeriodResults.FirstOrDefault(x =>
                    x.Mileage == currentMileage);

                if (compareResult != null && compareResult.ImgData != null)
                {
                    compareImage = compareResult.ImgData;
                    compareMileage = compareResult.Mileage;
                }
                else
                {
                    // 如果没有直接匹配，尝试模糊匹配（里程可能不完全相同）
                    compareResult = ComparePeriodResults.FirstOrDefault(x =>
                        !string.IsNullOrEmpty(x.Mileage) &&
                        !string.IsNullOrEmpty(currentMileage) &&
                        (x.Mileage.Contains(currentMileage) || currentMileage.Contains(x.Mileage)));

                    if (compareResult != null && compareResult.ImgData != null)
                    {
                        compareImage = compareResult.ImgData;
                        compareMileage = compareResult.Mileage;
                    }
                }

                // 3. 创建并打开图片预览窗口
                var previewWindow = CreateImagePreviewWindow(currentImages, compareImage, currentMileage, compareMileage);
                previewWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                HandyControl.Controls.MessageBox.Show($"打开图片失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void DoubleClickCompareImage(CompareResult selectedItem)
        {
            ViewCompareImage(selectedItem);
        }

        [RelayCommand]
        private void ViewCompareImage(CompareResult selectedItem)
        {
            if (selectedItem == null) return;

            try
            {
                // 1. 获取对比周期的图片（单张）
                if (selectedItem.ImgData == null)
                {
                    HandyControl.Controls.MessageBox.Show("该对比图片数据为空", "提示",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 2. 获取当前周期的图片（可能有多张）
                var currentMileage = selectedItem.Mileage;
                List<BitmapImage> currentImages = new List<BitmapImage>();

                if (!string.IsNullOrEmpty(currentMileage))
                {
                    currentImages = GetCurrentImagesByMileage(currentMileage);
                }

                // 如果当前周期没有对应图片，至少显示对比图片
                if (currentImages.Count == 0)
                {
                    currentImages.Add(new BitmapImage()); // 空图片作为占位
                }

                // 3. 创建并打开图片预览窗口
                var previewWindow = CreateImagePreviewWindow(currentImages, selectedItem.ImgData, currentMileage, currentMileage);
                previewWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                HandyControl.Controls.MessageBox.Show($"打开对比图片失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 辅助方法：根据里程获取当前周期图片（BitmapImage列表）
        private List<BitmapImage> GetCurrentImagesByMileage(string mileage)
        {
            var images = new List<BitmapImage>();

            try
            {
                if (string.IsNullOrEmpty(mileage))
                    return images;

                // 方法1：从CurrentPeriodResults中查找
                var currentItems = CurrentPeriodResults
                    .Where(img => img.Mileage == mileage)
                    .ToList();

                foreach (var item in currentItems)
                {
                    if (item.ImgData is BitmapImage bitmap)
                    {
                        int idx = 0;
                        string parsedMileage = string.Empty;
                        ParseFileName(Path.GetFileName(item.ImgPath), out idx, out parsedMileage);
                        // 解析文件名获取索引
                        if (!string.IsNullOrEmpty(item.ImgPath))
                        {
                            // 查找相邻索引的图片（前一张、当前、后一张）
                            for (int i = idx - 1; i <= idx + 1; i++)
                            {
                                var path = FindFilePathByIdx(i, MainViewModel.imgFiles);
                                if (path != null && File.Exists(path))
                                {
                                    try
                                    {
                                        // 加载图片文件并转换为BitmapImage
                                        var adjacentBitmap = LoadImageFromFile(path);
                                        if (adjacentBitmap != null)
                                        {
                                            images.Add(adjacentBitmap);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine($"加载相邻图片失败，索引={i}, 路径={path}: {ex.Message}");
                                    }
                                }
                                else
                                {
                                    continue;
                                }
                            }
                        }
                        else
                        {
                            // 如果无法解析文件名，只添加原始图片
                            images.Add(bitmap);
                        }
                    }
                    else if (item.ImageData != null && item.ImageData.Length > 0)
                    {
                        var bitmapImage = LoadImageFromBytes(item.ImageData);
                        if (bitmapImage != null)
                        {
                            images.Add(bitmapImage);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"获取里程{mileage}的图片失败: {ex.Message}");
            }

            return images;
        }
        // 新增的辅助方法：从文件加载图片
        private BitmapImage LoadImageFromFile(string filePath)
        {
            try
            {
                var bitmapImage = new BitmapImage();

                using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    bitmapImage.BeginInit();
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.StreamSource = fileStream;
                    bitmapImage.EndInit();
                }

                bitmapImage.Freeze(); // WPF中使图片跨线程安全
                return bitmapImage;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载图片文件失败: {filePath}, 错误: {ex.Message}");
                return null;
            }
        }

        // 辅助方法：创建图片预览窗口
        private ImagePreviewWindow CreateImagePreviewWindow(List<BitmapImage> currentImages, BitmapImage compareImage,
                                                   string currentMileage = null, string compareMileage = null)
        {
            // 参数验证
            if (currentImages == null || !currentImages.Any())
                throw new ArgumentException("图片列表不能为空", nameof(currentImages));

            // 过滤掉 null 的图片
            var validImages = currentImages.Where(img => img != null).ToList();
            if (!validImages.Any())
                throw new ArgumentException("图片列表中所有图片都是 null", nameof(currentImages));

            // 创建窗口
            ImagePreviewWindow window;

            try
            {
                // 确保总是传递有效的图片列表
                if (compareImage != null)
                {
                    window = new ImagePreviewWindow(validImages, compareImage);
                }
                else
                {
                    // 调用单参数构造函数
                    window = new ImagePreviewWindow(validImages);
                }
            }
            catch (Exception ex)
            {
                // 添加更多调试信息
                var debugInfo = $"创建窗口失败: validImages数量={validImages.Count}, " +
                               $"compareImage={(compareImage != null ? "非空" : "空")}, " +
                               $"错误: {ex.Message}";
                throw new InvalidOperationException(debugInfo, ex);
            }

            // 设置标题
            string currentTitle = "当前周期";
            string compareTitle = "对比周期";

            if (!string.IsNullOrEmpty(currentMileage))
            {
                currentTitle = $"当前周期 - 里程: {currentMileage}";
            }

            if (!string.IsNullOrEmpty(compareMileage))
            {
                compareTitle = $"对比周期 - 里程: {compareMileage}";
            }

            window.SetTitles(currentTitle, compareTitle);

            // 安全地设置 Owner
            if (Application.Current?.MainWindow != null)
            {
                window.Owner = Application.Current.MainWindow;
            }

            return window;
        }

        // 加载图片（从字节数组）
        private BitmapImage LoadImageFromBytes(byte[] imageData)
        {
            if (imageData == null || imageData.Length == 0)
                return null;

            try
            {
                var bitmapImage = new BitmapImage();
                using (var stream = new MemoryStream(imageData))
                {
                    bitmapImage.BeginInit();
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.StreamSource = stream;
                    bitmapImage.EndInit();
                }
                bitmapImage.Freeze();
                return bitmapImage;
            }
            catch
            {
                return null;
            }
        }

        // 可选：从ComparePeriodResults中获取BitmapImage的字节数组版本（如果需要）
        [RelayCommand]
        private void ViewCompareImageWithBytes(CompareResult selectedItem)
        {
            if (selectedItem == null) return;

            try
            {
                // 1. 获取对比周期的图片（从ImgData转换为字节数组）
                byte[] compareImageData = null;
                if (selectedItem.ImgData != null)
                {
                    compareImageData = ConvertBitmapImageToBytes(selectedItem.ImgData);
                }

                if (compareImageData == null || compareImageData.Length == 0)
                {
                    HandyControl.Controls.MessageBox.Show("该对比图片数据为空", "提示",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 2. 获取当前周期的图片（字节数组列表）
                var currentMileage = selectedItem.Mileage;
                List<byte[]> currentImageDataList = new List<byte[]>();

                if (!string.IsNullOrEmpty(currentMileage))
                {
                    currentImageDataList = GetCurrentImageDataListByMileage(currentMileage);
                }

                // 3. 使用字节数组版本打开窗口
                var previewWindow = new ImagePreviewWindow(currentImageDataList, compareImageData);

                // 设置标题
                string currentTitle = string.IsNullOrEmpty(currentMileage)
                    ? "当前周期"
                    : $"当前周期 - 里程: {currentMileage}";

                string compareTitle = string.IsNullOrEmpty(currentMileage)
                    ? "对比周期"
                    : $"对比周期 - 里程: {currentMileage}";

                previewWindow.SetTitles(currentTitle, compareTitle);
                previewWindow.Owner = Application.Current.MainWindow;
                previewWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                HandyControl.Controls.MessageBox.Show($"打开对比图片失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 辅助方法：将BitmapImage转换为字节数组
        private byte[] ConvertBitmapImageToBytes(BitmapImage bitmapImage)
        {
            try
            {
                if (bitmapImage == null)
                    return null;

                byte[] data = null;
                JpegBitmapEncoder encoder = new JpegBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmapImage));
                using (MemoryStream ms = new MemoryStream())
                {
                    encoder.Save(ms);
                    data = ms.ToArray();
                }
                return data;
            }
            catch
            {
                return null;
            }
        }

        // 辅助方法：根据里程获取当前周期图片数据列表（字节数组）
        private List<byte[]> GetCurrentImageDataListByMileage(string mileage)
        {
            var imageDataList = new List<byte[]>();

            try
            {
                if (string.IsNullOrEmpty(mileage))
                    return imageDataList;

                // 从CurrentPeriodResults中查找
                var currentItems = CurrentPeriodResults
                    .Where(img => img.Mileage == mileage)
                    .ToList();

                foreach (var item in currentItems)
                {
                    if (item.ImageData != null && item.ImageData.Length > 0)
                    {
                        imageDataList.Add(item.ImageData);
                    }
                }

                // 如果当前结果中没有，从_imgInfos中查找
                if (imageDataList.Count == 0)
                {
                    var imgInfos = _imgInfos
                        .Where(img => img.Mileage == mileage)
                        .ToList();

                    foreach (var img in imgInfos)
                    {
                        if (img.ImageData != null && img.ImageData.Length > 0)
                        {
                            imageDataList.Add(img.ImageData);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"获取里程{mileage}的图片数据失败: {ex.Message}");
            }

            return imageDataList;
        }

        #endregion

        [RelayCommand]
        private async Task DeleteSelectedAsync()
        {
            var selectedItems = CurrentPeriodResults.Where(x => x.IsSelected == true).ToList();
            if (selectedItems.Count == 0)
            {
                HandyControl.Controls.MessageBox.Show("请先选择要删除的图片", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = HandyControl.Controls.MessageBox.Show($"确定要删除选中的 {selectedItems.Count} 张图片吗？", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                using (var sqlHelper = new SQLHelper(Settings.Default.SqlPath))
                {
                    foreach (var item in selectedItems)
                    {
                        await sqlHelper.DeleteImageAsync(item.ImgId);
                    }
                }

                foreach (var item in selectedItems)
                {
                    _imgInfos.RemoveAll(x => x.ImgId == item.ImgId);
                    CurrentPeriodResults.Remove(item);
                }

                HandyControl.Controls.MessageBox.Show($"成功删除 {selectedItems.Count} 张图片", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                CurrentPeriodInfo = $"当前周期: {CurrentPeriodResults.Count} 条记录";
                CompareInfo = $"当前周期: {CurrentPeriodResults.Count} 条 | 对比周期: {ComparePeriodResults.Count} 条";
            }
            catch (Exception ex)
            {
                HandyControl.Controls.MessageBox.Show($"删除图片时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task DeleteSingleAsync(SqlImgInfo item)
        {
            if (item == null) return;

            var result = HandyControl.Controls.MessageBox.Show("确定要删除这张图片吗？", "确认删除",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                using (var sqlHelper = new SQLHelper(Settings.Default.SqlPath))
                {
                    await sqlHelper.DeleteDamageAnnotationAsync(item.ImgId);
                    await sqlHelper.DeleteImageAsync(item.ImgId);
                }

                _imgInfos.RemoveAll(x => x.ImgId == item.ImgId);
                var itemToRemove = CurrentPeriodResults.FirstOrDefault(x => x.ImgId == item.ImgId);
                if (itemToRemove != null)
                {
                    CurrentPeriodResults.Remove(itemToRemove);
                }

                HandyControl.Controls.MessageBox.Show("图片删除成功", "成功",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                CurrentPeriodInfo = $"当前周期: {CurrentPeriodResults.Count} 条记录";
                CompareInfo = $"当前周期: {CurrentPeriodResults.Count} 条 | 对比周期: {ComparePeriodResults.Count} 条";
            }
            catch (Exception ex)
            {
                HandyControl.Controls.MessageBox.Show($"删除图片时发生错误: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion
    }
}