using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DamageMaker.Common;
using DamageMaker.FileHandle;
using DamageMaker.GenerateReport;
using DamageMaker.Models;
using DamageMaker.Properties;
using DamageMaker.SqliteServer;
using DamageMarker;
using DamageMarker.ViewModels;
using HandyControl.Controls;
using Microsoft.Win32;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Windows.Storage.Pickers;
using static ABI.System.Windows.Input.ICommand_Delegates;
using MessageBox = HandyControl.Controls.MessageBox;

namespace DamageMaker.ViewModels
{
    public partial class DamageFoldersListViewModel : ObservableObject
    {
        public static event Action<string>? OpenedDamageFolder;

        // 当前已经打开的历史文件夹路径。
        // 再次打开历史记录窗口时，会优先定位并选中这个文件夹。
        public static string? CurrentOpenedFolderPath { get; private set; }

        public static void SetCurrentOpenedFolderPath(string? folderPath)
        {
            CurrentOpenedFolderPath = folderPath;
        }

        private ObservableCollection<DamageFoldersInfo> damageFolders = new();
        private readonly string _dbPath = Properties.Settings.Default.SqlPath;
        private Dictionary<string, (int folderId, string? remark, DateTime? lastOpenTime, string? instruments, int suspectedDamageCount)>? _folderInfoCache;
        private SQLHelper? _cachedSqlHelper;
        private static readonly Func<string, bool> HasJsonFile = path => File.Exists(Path.Combine(path, "result.json"));
        private static readonly Func<string, int> CountPngFiles = path => Directory.Exists(path) ? Directory.GetFiles(path, "*.png").Length : 0;
        private static readonly Func<string, string> GetOutPath = path => path.InReplaceOutString();
        private readonly SQLHelper _sqlHelper; // 共享实例

        // ===== 分页相关：完整数据放在 allFolders / filteredFolders，界面只显示当前页 DamageFolders =====
        private const int PageSize = 20;
        private List<DamageFoldersInfo> allFolders = new();
        private List<DamageFoldersInfo> filteredFolders = new();

        // 当前页详情加载缓存，避免翻页回来重复统计 PNG、报表、备注等信息
        private readonly HashSet<string> _detailsLoadedFolderNames = new(StringComparer.OrdinalIgnoreCase);
        private HashSet<string>? _docxFilesCache;
        private Dictionary<string, string>? _allRemarksCache;
        private readonly Dictionary<string, int> _suspectedCountsCache = new(StringComparer.OrdinalIgnoreCase);

        // 当前页详情加载版本号：快速翻页时，丢弃旧页后台结果，避免旧任务继续刷新 UI。
        private int _detailLoadVersion = 0;

        // 文件夹基础数据库信息缓存：翻页回来不重复读取线别、上下行、周期、串号、股别。
        private readonly Dictionary<string, FolderBaseDbInfo> _folderBaseDbInfoCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _folderBaseDbInfoCacheLock = new();

        private sealed class FolderBaseDbInfo
        {
            public string SelectedLineType { get; set; } = string.Empty;
            public string SelectedUpOrDown { get; set; } = string.Empty;
            public string SelectedRailType { get; set; } = string.Empty;
            public int CycleNumber { get; set; } = 0;
            public string SerialNumber { get; set; } = string.Empty;
        }

        private sealed class FolderDetailResult
        {
            public DamageFoldersInfo Folder { get; set; } = null!;
            public string SelectedLineType { get; set; } = string.Empty;
            public string SelectedUpOrDown { get; set; } = string.Empty;
            public string SelectedRailType { get; set; } = string.Empty;
            public int CycleNumber { get; set; } = 0;
            public string SerialNumber { get; set; } = string.Empty;
            public int PngCount { get; set; } = 0;
            public int DamagePngCount { get; set; } = 0;
            public bool HasDocx { get; set; } = false;
            public string Instruments { get; set; } = string.Empty;
            public string Remark { get; set; } = string.Empty;
            public int SuspectedDamageCount { get; set; } = 0;
        }

        // ===== 跨页勾选相关：DataGrid 翻页会清空可视选中项，所以这里单独保存所有页累计勾选结果 =====
        private readonly HashSet<string> _selectedFolderNames = new(StringComparer.OrdinalIgnoreCase);
        public bool IsChangingPage { get; private set; }

        // 点击“已勾选”下拉文件名后，View 层用这个文件名滚动到对应行。
        public string? FolderNameToScrollIntoView { get; private set; }

        public ObservableCollection<DamageFoldersInfo> DamageFolders
        {
            get => damageFolders;
            set => SetProperty(ref damageFolders, value);
        }

        [ObservableProperty]
        private int selectedIndex = -1;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PageInfo))]
        [NotifyCanExecuteChangedFor(nameof(FirstPageCommand))]
        [NotifyCanExecuteChangedFor(nameof(PreviousPageCommand))]
        [NotifyCanExecuteChangedFor(nameof(NextPageCommand))]
        [NotifyCanExecuteChangedFor(nameof(LastPageCommand))]
        private int currentPage = 1;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PageInfo))]
        [NotifyCanExecuteChangedFor(nameof(FirstPageCommand))]
        [NotifyCanExecuteChangedFor(nameof(PreviousPageCommand))]
        [NotifyCanExecuteChangedFor(nameof(NextPageCommand))]
        [NotifyCanExecuteChangedFor(nameof(LastPageCommand))]
        private int totalPages = 1;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PageInfo))]
        private int totalRecordCount = 0;

        public string PageInfo => TotalRecordCount == 0
            ? "第 0 / 0 页，共 0 条"
            : $"第 {CurrentPage} / {TotalPages} 页，共 {TotalRecordCount} 条，每页 {PageSize} 条";

        public string SelectedCountText => $"已勾选 {SelectedFolders.Count} 条";

        private ObservableCollection<DamageFoldersInfo> originalFolders = new();

        [ObservableProperty]
        private string searchHistoricalFileText = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SelectedCountText))]
        [NotifyCanExecuteChangedFor(nameof(OpenDocxCommand))]
        [NotifyCanExecuteChangedFor(nameof(ExportExcelCommand))]
        [NotifyCanExecuteChangedFor(nameof(DeleteFoldersCommand))]
        [NotifyCanExecuteChangedFor(nameof(OpenInFolderCommand))]
        [NotifyCanExecuteChangedFor(nameof(ExportReportCommand))]
        private ObservableCollection<DamageFoldersInfo> selectedFolders = new();

        public DamageFoldersListViewModel()
        {
            _sqlHelper = new SQLHelper(_dbPath);
            // 立即开始异步初始化，不阻塞UI
            _ = InitializeFoldersAsync();
        }

        private async Task InitializeFoldersAsync()
        {
            try
            {
                // 先清空列表和选择缓存
                ClearCrossPageSelection();

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    IsChangingPage = true;
                    DamageFolders.Clear();
                    SelectedIndex = -1;
                    IsChangingPage = false;
                });

                _detailsLoadedFolderNames.Clear();
                _docxFilesCache = null;
                _allRemarksCache = null;
                _suspectedCountsCache.Clear();
                _folderBaseDbInfoCache.Clear();

                // 检查目录
                var inPath = Settings.Default.InPath;
                if (!Directory.Exists(inPath))
                {
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        MessageBox.Warning("输入文件夹不存在");
                    });
                    return;
                }

                // 1. 快速获取所有文件夹路径
                var folderPaths = await Task.Run(() => Directory.GetDirectories(inPath));
                if (folderPaths.Length == 0)
                {
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        MessageBox.Warning("没有找到历史数据,请先进行数据采集");
                    });
                    return;
                }

                // 2. 只创建基础信息。PNG 数量、备注、仪器信息等详细字段只加载当前页 20 条。
                var basicInfos = await CreateBasicInfosAsync(folderPaths);

                // 3. 初始化分页。界面 DamageFolders 只显示当前页 20 条。
                await AddFoldersToUIInBatchesAsync(basicInfos);
            }
            catch (Exception ex)
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Error($"加载文件夹列表失败: {ex.Message}");
                });
            }
        }

        private async Task<List<DamageFoldersInfo>> CreateBasicInfosAsync(string[] folderPaths)
        {
            return await Task.Run(() =>
            {
                var basicInfos = new List<DamageFoldersInfo>();

                // 这里只获取轻量信息，避免打开历史页时一次性加载所有文件夹详情。
                foreach (var path in folderPaths)
                {
                    var folderName = Path.GetFileName(path);
                    var creationTime = Directory.GetCreationTime(path);
                    var hasJson = File.Exists(Path.Combine(path, "result.json"));

                    basicInfos.Add(new DamageFoldersInfo
                    {
                        FolderName = folderName,
                        CreatTime = creationTime,
                        HasDamage = hasJson,
                        SelectedLineType = string.Empty,
                        SelectedUpOrDown = string.Empty,
                        SelectedRailType = string.Empty,
                        CycleNumber = 0,
                        SerialNumber1 = string.Empty,
                        PngCount = 0,
                        DamagePngCount = 0,
                        HasDocx = false,
                        Remark = string.Empty,
                        Instruments = string.Empty,
                        SuspectedDamageCount = 0
                    });
                }

                // 按创建时间排序
                return basicInfos.OrderByDescending(x => x.CreatTime).ToList();
            });
        }

        private async Task AddFoldersToUIInBatchesAsync(List<DamageFoldersInfo> folders)
        {
            allFolders = folders;
            originalFolders = new ObservableCollection<DamageFoldersInfo>(allFolders);
            filteredFolders = allFolders;

            // 打开历史记录窗口时：
            // 1. 如果已经打开过某个历史文件夹，优先跳到该文件夹所在页并选中它；
            // 2. 如果没有打开过，或当前打开路径不在列表中，则默认选中第一个。
            int restoreIndex = GetDefaultSelectedFolderIndex(filteredFolders);
            await ResetPagedSourceAsync(filteredFolders, restoreIndex, true);
        }

        private int GetDefaultSelectedFolderIndex(List<DamageFoldersInfo> folders)
        {
            if (folders == null || folders.Count == 0)
            {
                return -1;
            }

            if (!string.IsNullOrWhiteSpace(CurrentOpenedFolderPath))
            {
                string openedFolderName = GetFolderNameFromPath(CurrentOpenedFolderPath);

                if (!string.IsNullOrWhiteSpace(openedFolderName))
                {
                    int openedIndex = folders.FindIndex(folder =>
                        string.Equals(folder.FolderName, openedFolderName, StringComparison.OrdinalIgnoreCase));

                    if (openedIndex >= 0)
                    {
                        return openedIndex;
                    }
                }
            }

            return 0;
        }

        private static string GetFolderNameFromPath(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                return string.Empty;
            }

            string normalizedPath = folderPath.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);

            return Path.GetFileName(normalizedPath) ?? string.Empty;
        }

        private async Task ResetPagedSourceAsync(List<DamageFoldersInfo> source, int targetGlobalIndex = 0, bool restoreSelection = false)
        {
            filteredFolders = source ?? new List<DamageFoldersInfo>();

            TotalRecordCount = filteredFolders.Count;
            TotalPages = TotalRecordCount == 0
                ? 1
                : (int)Math.Ceiling(TotalRecordCount / (double)PageSize);

            if (TotalRecordCount == 0)
            {
                CurrentPage = 1;
                await RefreshCurrentPageAsync(-1);
                return;
            }

            if (targetGlobalIndex < 0)
            {
                targetGlobalIndex = 0;
            }

            if (targetGlobalIndex >= TotalRecordCount)
            {
                targetGlobalIndex = TotalRecordCount - 1;
            }

            CurrentPage = targetGlobalIndex / PageSize + 1;
            int selectedIndexOnPage = restoreSelection ? targetGlobalIndex % PageSize : -1;

            // restoreSelection 为 true 时，用于首次打开窗口时默认选中一条记录。
            // 普通翻页和点击“已勾选”列表跳转时，不会清空原来的跨页多选结果。
            await RefreshCurrentPageAsync(selectedIndexOnPage, restoreSelection);
        }

        private async Task RefreshCurrentPageAsync(int selectedIndexOnPage = -1, bool syncSingleSelection = false)
        {
            int skip = (CurrentPage - 1) * PageSize;
            var pageItems = filteredFolders
                .Skip(skip)
                .Take(PageSize)
                .ToList();

            // 每次刷新页面都递增版本号。后台详情加载回来时，如果版本号不一致，说明用户已经翻页了，直接丢弃旧结果。
            int loadVersion = ++_detailLoadVersion;

            try
            {
                IsChangingPage = true;

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    DamageFolders.Clear();

                    for (int i = 0; i < pageItems.Count; i++)
                    {
                        pageItems[i].SerialNumber = skip + i + 1;
                        DamageFolders.Add(pageItems[i]);
                    }

                    SelectedIndex = selectedIndexOnPage >= 0 && selectedIndexOnPage < pageItems.Count
                        ? selectedIndexOnPage
                        : -1;

                    // 只有首次默认定位时才同步成单选。
                    // 普通翻页和“已勾选”按钮跳转时，需要保留跨页多选结果。
                    if (syncSingleSelection && SelectedIndex >= 0 && SelectedIndex < DamageFolders.Count)
                    {
                        SelectSingleFolder(DamageFolders[SelectedIndex]);
                    }
                    else
                    {
                        RefreshSelectionCommandStates();
                    }
                });
            }
            finally
            {
                IsChangingPage = false;
            }

            // 只加载当前页 20 条详情；不等待它完成，避免翻页/打开窗口被详情加载卡住。
            _ = LoadDetailsInBackgroundAsync(pageItems, loadVersion);
        }

        private void SelectSingleFolder(DamageFoldersInfo folder)
        {
            _selectedFolderNames.Clear();

            var key = GetFolderKey(folder);
            if (!string.IsNullOrWhiteSpace(key))
            {
                _selectedFolderNames.Add(key);
                SelectedFolders = new ObservableCollection<DamageFoldersInfo> { folder };
            }
            else
            {
                SelectedFolders = new ObservableCollection<DamageFoldersInfo>();
            }

            RefreshSelectionCommandStates();
        }

        private void RefreshSelectionCommandStates()
        {
            OpenDocxCommand.NotifyCanExecuteChanged();
            ExportExcelCommand.NotifyCanExecuteChanged();
            DeleteFoldersCommand.NotifyCanExecuteChanged();
            OpenInFolderCommand.NotifyCanExecuteChanged();
            ExportReportCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(SelectedCountText));
        }

        private bool CanGoToPreviousPage() => CurrentPage > 1;

        private bool CanGoToNextPage() => CurrentPage < TotalPages;

        [RelayCommand(CanExecute = nameof(CanGoToPreviousPage))]
        private async Task FirstPage()
        {
            if (CurrentPage <= 1) return;
            CurrentPage = 1;
            await RefreshCurrentPageAsync();
        }

        [RelayCommand(CanExecute = nameof(CanGoToPreviousPage))]
        private async Task PreviousPage()
        {
            if (CurrentPage <= 1) return;
            CurrentPage--;
            await RefreshCurrentPageAsync();
        }

        [RelayCommand(CanExecute = nameof(CanGoToNextPage))]
        private async Task NextPage()
        {
            if (CurrentPage >= TotalPages) return;
            CurrentPage++;
            await RefreshCurrentPageAsync();
        }

        [RelayCommand(CanExecute = nameof(CanGoToNextPage))]
        private async Task LastPage()
        {
            if (CurrentPage >= TotalPages) return;
            CurrentPage = TotalPages;
            await RefreshCurrentPageAsync();
        }

        [RelayCommand]
        private async Task JumpToSelectedFolder(DamageFoldersInfo? folder)
        {
            if (folder == null) return;

            var key = GetFolderKey(folder);
            if (string.IsNullOrWhiteSpace(key)) return;

            // 先在当前筛选结果中找。若搜索条件把它过滤掉了，则清空搜索并回到完整历史列表。
            int targetIndex = filteredFolders.FindIndex(item =>
                string.Equals(item.FolderName, key, StringComparison.OrdinalIgnoreCase));

            if (targetIndex < 0)
            {
                SearchHistoricalFileText = string.Empty;
                filteredFolders = allFolders;
                TotalRecordCount = filteredFolders.Count;
                TotalPages = TotalRecordCount == 0
                    ? 1
                    : (int)Math.Ceiling(TotalRecordCount / (double)PageSize);

                targetIndex = filteredFolders.FindIndex(item =>
                    string.Equals(item.FolderName, key, StringComparison.OrdinalIgnoreCase));
            }

            if (targetIndex < 0) return;

            // 保证被点击的文件仍在全局勾选集合里，同时不清空其它已勾选文件。
            _selectedFolderNames.Add(key);
            RebuildSelectedFoldersFromKeys();
            FolderNameToScrollIntoView = key;

            CurrentPage = targetIndex / PageSize + 1;
            int selectedIndexOnPage = targetIndex % PageSize;

            await RefreshCurrentPageAsync(selectedIndexOnPage, false);
        }

        public void UpdateCrossPageSelection(IEnumerable<DamageFoldersInfo> addedItems, IEnumerable<DamageFoldersInfo> removedItems)
        {
            if (IsChangingPage) return;

            foreach (var folder in removedItems)
            {
                var key = GetFolderKey(folder);
                if (!string.IsNullOrWhiteSpace(key))
                {
                    _selectedFolderNames.Remove(key);
                }
            }

            foreach (var folder in addedItems)
            {
                var key = GetFolderKey(folder);
                if (!string.IsNullOrWhiteSpace(key))
                {
                    _selectedFolderNames.Add(key);
                }
            }

            RebuildSelectedFoldersFromKeys();
        }

        public bool IsFolderGloballySelected(DamageFoldersInfo folder)
        {
            var key = GetFolderKey(folder);
            return !string.IsNullOrWhiteSpace(key) && _selectedFolderNames.Contains(key);
        }

        public void ClearCrossPageSelection()
        {
            _selectedFolderNames.Clear();
            SelectedFolders = new ObservableCollection<DamageFoldersInfo>();
            RefreshSelectionCommandStates();
        }

        private string GetFolderKey(DamageFoldersInfo folder)
        {
            return folder?.FolderName ?? string.Empty;
        }

        private void RebuildSelectedFoldersFromKeys()
        {
            var selected = allFolders
                .Where(folder => !string.IsNullOrWhiteSpace(folder.FolderName)
                                 && _selectedFolderNames.Contains(folder.FolderName))
                .ToList();

            SelectedFolders = new ObservableCollection<DamageFoldersInfo>(selected);
            RefreshSelectionCommandStates();
        }

        private async Task LoadDetailsInBackgroundAsync(List<DamageFoldersInfo> folders, int loadVersion)
        {
            if (folders.Count == 0) return;

            var pendingFolders = folders
                .Where(f => !string.IsNullOrWhiteSpace(f.FolderName)
                            && !_detailsLoadedFolderNames.Contains(f.FolderName))
                .ToList();

            if (pendingFolders.Count == 0) return;

            try
            {
                var docxFiles = await GetDocxFilesCacheAsync();
                var allRemarks = await GetAllRemarksCacheAsync();
                var suspectedCounts = await GetSuspectedCountsCacheAsync(pendingFolders);

                // 当前页最多 20 条，合并成一次后台处理，减少 Task 数量和 UI 线程切换次数。
                await ProcessBatchAsync(pendingFolders, docxFiles, allRemarks, suspectedCounts, loadVersion);

                if (loadVersion != _detailLoadVersion)
                    return;

                foreach (var folder in pendingFolders)
                {
                    if (!string.IsNullOrWhiteSpace(folder.FolderName))
                    {
                        _detailsLoadedFolderNames.Add(folder.FolderName);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"后台加载历史详情失败: {ex.Message}");
            }
        }

        private async Task<HashSet<string>> GetDocxFilesCacheAsync()
        {
            if (_docxFilesCache != null) return _docxFilesCache;

            _docxFilesCache = await Task.Run(() =>
            {
                if (Directory.Exists(Settings.Default.DocxPath))
                {
                    return Directory.GetFiles(Settings.Default.DocxPath, "*.docx")
                        .Select(Path.GetFileNameWithoutExtension)
                        .Where(name => name != null && name.EndsWith("钢轨探伤检测报告"))
                        .Select(name => name!.Replace("钢轨探伤检测报告", ""))
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);
                }

                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            });

            return _docxFilesCache;
        }

        private async Task<Dictionary<string, string>> GetAllRemarksCacheAsync()
        {
            if (_allRemarksCache != null) return _allRemarksCache;

            try
            {
                _allRemarksCache = await Task.Run(() => _sqlHelper.GetAllFolderRemarks()
                    .ToDictionary(x => x.Key, x => x.Value));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"获取备注失败: {ex.Message}");
                _allRemarksCache = new Dictionary<string, string>();
            }

            return _allRemarksCache;
        }

        private async Task<Dictionary<string, int>> GetSuspectedCountsCacheAsync(List<DamageFoldersInfo> folders)
        {
            var missingNames = folders
                .Select(f => f.FolderName)
                .Where(name => !string.IsNullOrWhiteSpace(name) && !_suspectedCountsCache.ContainsKey(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (missingNames.Count > 0)
            {
                try
                {
                    var counts = await Task.Run(() => _sqlHelper.GetSuspectedDamageCounts(missingNames));
                    foreach (var item in counts)
                    {
                        _suspectedCountsCache[item.Key] = item.Value;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"获取疑似伤损数量失败: {ex.Message}");
                    foreach (var name in missingNames)
                    {
                        _suspectedCountsCache[name] = 0;
                    }
                }
            }

            return _suspectedCountsCache;
        }

        private async Task ProcessBatchAsync(
            List<DamageFoldersInfo> batch,
            HashSet<string> docxFiles,
            Dictionary<string, string> allRemarks,
            Dictionary<string, int> suspectedCounts,
            int loadVersion)
        {
            var results = await Task.Run(() =>
            {
                var list = new List<FolderDetailResult>(batch.Count);

                // 一个批次只创建一个 SQLHelper，避免每个文件夹都重复打开数据库连接。
                using var sqlHelper = new SQLHelper(Settings.Default.SqlPath);

                foreach (var folder in batch)
                {
                    try
                    {
                        if (string.IsNullOrWhiteSpace(folder.FolderName))
                            continue;

                        var path = Path.Combine(Settings.Default.InPath, folder.FolderName);
                        var outPath = GetOutPath(path);
                        var dbInfo = GetFolderBaseDbInfoWithCache(sqlHelper, path, folder.FolderName);

                        string instruments = string.Empty;
                        var infoPath = Path.Combine(path, "info.json");
                        if (File.Exists(infoPath))
                        {
                            try
                            {
                                var (_, Info) = AboutJson.JsonPathToData(path);
                                instruments = Info?.RailWayInfo?.Instruments ?? string.Empty;
                            }
                            catch
                            {
                                instruments = string.Empty;
                            }
                        }

                        list.Add(new FolderDetailResult
                        {
                            Folder = folder,
                            SelectedLineType = dbInfo.SelectedLineType,
                            SelectedUpOrDown = dbInfo.SelectedUpOrDown,
                            CycleNumber = dbInfo.CycleNumber,
                            SerialNumber = dbInfo.SerialNumber,
                            SelectedRailType = dbInfo.SelectedRailType,
                            PngCount = CountFilesFast(path, "*.png"),
                            DamagePngCount = CountFilesFast(outPath, "*.png"),
                            HasDocx = docxFiles.Contains(folder.FolderName),
                            Instruments = instruments,
                            Remark = allRemarks.GetValueOrDefault(path, string.Empty),
                            SuspectedDamageCount = suspectedCounts.GetValueOrDefault(folder.FolderName, 0)
                        });
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"处理文件夹 {folder.FolderName} 失败: {ex.Message}");
                    }
                }

                return list;
            });

            // 如果用户快速翻页，旧页结果不再刷新 UI。
            if (loadVersion != _detailLoadVersion)
                return;

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                foreach (var item in results)
                {
                    item.Folder.SelectedLineType = item.SelectedLineType;
                    item.Folder.SelectedUpOrDown = item.SelectedUpOrDown;
                    item.Folder.CycleNumber = item.CycleNumber;
                    item.Folder.SerialNumber1 = item.SerialNumber;
                    item.Folder.SelectedRailType = item.SelectedRailType;
                    item.Folder.PngCount = item.PngCount;
                    item.Folder.DamagePngCount = item.DamagePngCount;
                    item.Folder.HasDocx = item.HasDocx;
                    item.Folder.Instruments = item.Instruments;
                    item.Folder.Remark = item.Remark;
                    item.Folder.SuspectedDamageCount = item.SuspectedDamageCount;
                }
            });
        }

        private FolderBaseDbInfo GetFolderBaseDbInfoWithCache(SQLHelper sqlHelper, string path, string folderName)
        {
            lock (_folderBaseDbInfoCacheLock)
            {
                if (_folderBaseDbInfoCache.TryGetValue(path, out var cached))
                {
                    return cached;
                }
            }

            var info = new FolderBaseDbInfo();

            try
            {
                // 这里仍然使用你原来的 SQLHelper 方法，保证兼容；优化点是减少 SQLHelper 创建次数，并增加缓存。
                info.SelectedLineType = sqlHelper.GetSelectedRouteLineByFolderPath(path);
                info.SelectedUpOrDown = sqlHelper.GetSelectedUpOrDownByFolderPath(path);
                info.CycleNumber = sqlHelper.GetCycleNumberByFolderPath(path);
                info.SerialNumber = sqlHelper.GetSerialNumberByFolderPath(path);
                info.SelectedRailType = sqlHelper.GetSelectedRailTypeByFolderPath(path);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"读取文件夹基础数据库信息失败 {folderName}: {ex.Message}");
            }

            lock (_folderBaseDbInfoCacheLock)
            {
                _folderBaseDbInfoCache[path] = info;
            }

            return info;
        }

        private static int CountFilesFast(string path, string searchPattern)
        {
            try
            {
                if (!Directory.Exists(path))
                    return 0;

                return Directory.EnumerateFiles(path, searchPattern, SearchOption.TopDirectoryOnly).Count();
            }
            catch
            {
                return 0;
            }
        }

        [RelayCommand]
        private async void SearchHistoricalFile(string searchText)
        {
            try
            {
                string keyword = searchText?.Trim() ?? string.Empty;

                // 搜索备注时需要备注缓存；只在搜索时读取一次全部备注。
                var remarks = await GetAllRemarksCacheAsync();
                foreach (var folder in allFolders)
                {
                    var path = Path.Combine(Settings.Default.InPath, folder.FolderName);
                    if (remarks.TryGetValue(path, out var remark))
                    {
                        folder.Remark = remark;
                    }
                }

                var source = string.IsNullOrWhiteSpace(keyword)
                    ? allFolders
                    : allFolders
                        .Where(folder =>
                            (folder.FolderName?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
                            (folder.Remark?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false))
                        .ToList();

                await ResetPagedSourceAsync(source, 0, false);
            }
            catch (Exception ex)
            {
                MessageBox.Error($"搜索历史文件失败: {ex.Message}");
            }
        }

        private void UpdateSerialNumbers()
        {
            int skip = (CurrentPage - 1) * PageSize;
            for (int i = 0; i < DamageFolders.Count; i++)
            {
                DamageFolders[i].SerialNumber = skip + i + 1;
            }
        }

        [RelayCommand]
        private void DoubleClickDamageFolder(object pa)
        {
            if (pa is not DamageFoldersInfo folderInfo)
                return;

            try
            {
                var folderName = folderInfo.FolderName;
                if (string.IsNullOrWhiteSpace(folderName))
                    return;

                var folderPath = Path.Combine(Settings.Default.InPath, folderName);

                // 先记录当前打开的文件夹，再通知外部打开并关闭窗口。
                // 这样下次打开历史记录窗口时，可以默认选中当前已经打开的文件夹。
                SetCurrentOpenedFolderPath(folderPath);
                App.LastSelectedFolderIndex = Math.Max(0, (CurrentPage - 1) * PageSize + SelectedIndex);

                // 先打开文件夹，数据库更新时间放到后台，提升双击响应速度。
                OpenedDamageFolder?.Invoke(folderPath);

                _ = UpdateLastOpenTimeInBackgroundAsync(folderPath, folderInfo);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"双击处理异常: {ex.Message}");
            }
        }

        private async Task UpdateLastOpenTimeInBackgroundAsync(string folderPath, DamageFoldersInfo folderInfo)
        {
            try
            {
                var now = DateTime.Now;

                await Task.Run(async () =>
                {
                    using var sqlHelper = new SQLHelper(_dbPath);
                    int folderId = await sqlHelper.GetFolderIdByPathAsync(folderPath);

                    if (folderId > 0)
                    {
                        sqlHelper.UpdateFolderLastOpenTime(folderId, now);
                    }
                });

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    folderInfo.LastOpenTime = now;
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"后台更新最后打开时间失败: {ex.Message}");
            }
        }

        bool IsSelectedFolder() => SelectedFolders.Count == 1;

        [RelayCommand(CanExecute = nameof(IsSelectedFolder))]
        async void OpenDocx(DamageFoldersInfo d)
        {
            try
            {
                d ??= SelectedFolders.FirstOrDefault();
                if (d == null)
                {
                    MessageBox.Warning("请先选择一个文件夹");
                    return;
                }

                if (!d.HasDamage)
                {
                    MessageBox.Warning("当前图片集没有伤损数据，无法生成报告");
                    return;
                }

                string docxPath = Path.GetFullPath(
                    Path.Combine(Settings.Default.DocxPath, d.FolderName + "钢轨探伤检测报告.docx")
                );

                // 先检查文件是否存在，避免重复生成
                if (File.Exists(docxPath) && d.HasDocx)
                {
                    Utilities.StartProcess(
                        DamageMaker.Properties.Settings.Default.OfficeLocation,
                        docxPath
                    );
                    return;
                }

                MessageBox.Info("正在生成Word报告，请稍候...");
                string folderPath = Path.Combine(Properties.Settings.Default.InPath, d.FolderName);

                bool exportSuccess = await Task.Run(() =>
                {
                    try
                    {
                        var exporter = new ExportWord(folderPath);
                        exporter.GenerateWord(docxPath);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"导出Word报告失败: {ex.Message}");
                        return false;
                    }
                });

                if (!exportSuccess)
                {
                    MessageBox.Error("Word报告生成失败");
                    return;
                }

                d.HasDocx = true;

                Utilities.StartProcess(
                    DamageMaker.Properties.Settings.Default.OfficeLocation,
                    docxPath
                );
            }
            catch (Exception ex)
            {
                MessageBox.Error($"打开Word文档失败: {ex.Message}");
            }
        }

        bool IsSelectedsFolder() => SelectedFolders.Count >= 1;

        [RelayCommand(CanExecute = nameof(IsSelectedsFolder))]
        async void ExportExcel()
        {
            try
            {
                // 这里的 SelectedFolders 是跨页累计勾选结果，不再只是当前页 SelectedItems。
                // 多选导出时，Excel 内部每条记录的“文件名/名称”必须以这里的 FolderName/FolderPath 为准。
                var selectedFoldersSnapshot = SelectedFolders
                    .Where(x => x != null && !string.IsNullOrWhiteSpace(x.FolderName))
                    .GroupBy(x => x.FolderName, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .ToList();

                var databaseFolders = selectedFoldersSnapshot
                    .Select(x => new
                    {
                        FolderName = x.FolderName,
                        FolderPath = Path.Combine(Properties.Settings.Default.InPath, x.FolderName)
                    })
                    .GroupBy(x => x.FolderPath, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .ToList();

                var databaseFolderPaths = databaseFolders
                    .Select(x => x.FolderPath)
                    .ToArray();

                if (databaseFolderPaths.Length == 0)
                {
                    MessageBox.Warning("当前没有可导出的数据库记录");
                    return;
                }

                Console.WriteLine($"[ExportExcel数据库] 导出数量={databaseFolders.Count}");
                foreach (var item in databaseFolders)
                {
                    Console.WriteLine($"[ExportExcel数据库] FolderName={item.FolderName}, FolderPath={item.FolderPath}");
                }

                string filePath = databaseFolders.Count == 1
                    ? Path.Combine(Properties.Settings.Default.ExcelsPath, $"{databaseFolders[0].FolderName}.xlsx")
                    : Path.Combine(Properties.Settings.Default.ExcelsPath, $"{DateTime.Now:yyyy年MM月dd日HH时mm分}.xlsx");

                // 检查文件是否已存在
                if (File.Exists(filePath))
                {
                    var result = MessageBox.Show(
                        $"文件 {Path.GetFileName(filePath)} 已存在，是否覆盖？",
                        "确认覆盖",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result != MessageBoxResult.Yes)
                    {
                        return;
                    }
                }

                var exc = new ExportExcellmentation();
                bool exportResult = await Task.Run(() => exc.BulkExportExcel(databaseFolderPaths, filePath));

                if (exportResult)
                {
                    MessageBox.Success($"已导出到{filePath}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Error($"导出Excel失败: {ex.Message}");
            }
        }

        [RelayCommand(CanExecute = nameof(IsSelectedsFolder))]
        async void DeleteFolders()
        {
            if (SelectedFolders.Count == 0) return;

            var confirmResult = MessageBox.Show(
                $"确定要删除选中的 {SelectedFolders.Count} 个文件夹吗？\n文件夹将移动到回收站。",
                "确认删除",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirmResult != MessageBoxResult.Yes) return;

            // 批量删除，减少数据库操作。这里支持跨页累计勾选。
            var selectedFoldersSnapshot = SelectedFolders.ToList();
            var folderPaths = selectedFoldersSnapshot
                .Select(item => (item.FolderName,
                    InPath: Path.Combine(Settings.Default.InPath, item.FolderName),
                    OutPath: Path.Combine(Settings.Default.OutPath, item.FolderName)))
                .ToList();

            try
            {
                var results = await Task.WhenAll(folderPaths.Select(async pathInfo =>
                {
                    try
                    {
                        await Task.Run(() =>
                        {
                            if (Directory.Exists(pathInfo.InPath))
                            {
                                Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(
                                    pathInfo.InPath,
                                    Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                                    Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin
                                );
                            }

                            if (Directory.Exists(pathInfo.OutPath))
                            {
                                Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(
                                    pathInfo.OutPath,
                                    Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                                    Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin
                                );
                            }
                        });

                        _sqlHelper.DeleteImagesAndAnnotationsByFolder(pathInfo.FolderName);
                        _sqlHelper.DeleteFolderInfo(pathInfo.InPath);

                        return (pathInfo.FolderName, Success: true, Error: (string?)null);
                    }
                    catch (Exception ex)
                    {
                        return (pathInfo.FolderName, Success: false, Error: ex.Message);
                    }
                }));

                // 清空跨页选择
                ClearCrossPageSelection();

                // 重新加载文件夹列表
                await InitializeFoldersAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Error($"删除失败: {ex.Message}");
            }
        }

        public void SaveRemarksToDatabase()
        {
            try
            {
                foreach (var folder in SelectedFolders)
                {
                    var folderPath = Path.Combine(Settings.Default.InPath, folder.FolderName);
                    _sqlHelper.UpdateFolderRemark(folderPath, folder.Remark);

                    if (_allRemarksCache != null)
                    {
                        _allRemarksCache[folderPath] = folder.Remark;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Error($"保存备注失败: {ex.Message}");
            }
        }

        [RelayCommand]
        void OpenInFolder(string pathName)
        {
            try
            {
                string folderPath = pathName switch
                {
                    "in文件夹" => SelectedFolders.Count is 0 or > 1
                        ? Path.GetFullPath(Settings.Default.InPath)
                        : Path.Combine(Settings.Default.InPath, SelectedFolders.First().FolderName),
                    "Excel文件夹" => Path.GetFullPath(Settings.Default.ExcelsPath),
                    _ => ""
                };

                if (!string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath))
                {
                    Utilities.StartProcess("explorer.exe", folderPath);
                }
                else
                {
                    MessageBox.Warning("文件夹路径不存在，请检查配置！");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Error($"打开文件夹失败: {ex.Message}");
            }
        }

        [RelayCommand(CanExecute = nameof(IsSelectedFolder))]
        async void ExportReport()
        {
            try
            {
                var selectedFolder = SelectedFolders.FirstOrDefault();
                if (selectedFolder == null)
                {
                    MessageBox.Warning("请先选择一个文件夹");
                    return;
                }

                // 弹出对话框让用户选择导出方式
                var dialogResult = MessageBox.Show(
                    "请选择导出方式：\n\n是(Y)：导出到默认文档文件夹\n否(N)：自定义保存位置\n取消：放弃导出",
                    "导出Word报告",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                string savePath = string.Empty;

                switch (dialogResult)
                {
                    case MessageBoxResult.Yes:
                        // 导出到默认文件夹
                        savePath = Path.Combine(
                            Settings.Default.DocxPath,
                            selectedFolder.FolderName + "钢轨探伤检测报告.docx"
                        );
                        break;

                    case MessageBoxResult.No:
                        // 自定义保存位置
                        var saveFileDialog = new SaveFileDialog
                        {
                            FileName = $"{selectedFolder.FolderName}钢轨探伤检测报告.docx",
                            DefaultExt = ".docx",
                            Filter = "Word文档 (*.docx)|*.docx",
                            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                        };

                        if (saveFileDialog.ShowDialog() != true)
                        {
                            return; // 用户取消了
                        }
                        savePath = saveFileDialog.FileName;
                        break;

                    default:
                        return; // 用户取消
                }

                // 检查文件是否已存在
                if (File.Exists(savePath))
                {
                    var overwriteResult = MessageBox.Show(
                        $"文件 {Path.GetFileName(savePath)} 已存在，是否覆盖？",
                        "确认覆盖",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (overwriteResult != MessageBoxResult.Yes)
                    {
                        return;
                    }
                }

                MessageBox.Info("正在生成Word报告，请稍候...");

                // 检查是否有伤损数据
                string folderPath = Path.Combine(Properties.Settings.Default.InPath, selectedFolder.FolderName);
                string resultJsonPath = Path.Combine(folderPath, "result.json");

                if (!File.Exists(resultJsonPath) || new FileInfo(resultJsonPath).Length == 0)
                {
                    MessageBox.Warning("当前图片集没有伤损数据，无法生成报告");
                    return;
                }

                bool exportSuccess = await Task.Run(() =>
                {
                    try
                    {
                        var exporter = new ExportWord(folderPath);
                        exporter.GenerateWord(savePath);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"导出Word报告失败: {ex.Message}");
                        return false;
                    }
                });

                if (exportSuccess)
                {
                    selectedFolder.HasDocx = (dialogResult == MessageBoxResult.Yes);

                    var openResult = MessageBox.Show(
                        $"报告已保存到: {savePath}\n\n是否立即打开文档？",
                        "导出成功",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);

                    if (openResult == MessageBoxResult.Yes)
                    {
                        Utilities.StartProcess(
                            DamageMaker.Properties.Settings.Default.OfficeLocation,
                            savePath
                        );
                    }
                }
                else
                {
                    MessageBox.Error("Word报告生成失败");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Error($"导出报告失败: {ex.Message}");
            }
        }
    }
}
