using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DamageMaker.Common;
using DamageMaker.GenerateReport;
using DamageMaker.Models;
using DamageMaker.Properties;
using DamageMarker.ViewModels;
using HandyControl.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Principal;
using DamageMarker;
using DamageMaker.SqliteServer;


namespace DamageMaker.ViewModels
{
    public partial class DamageFoldersListViewModel : ObservableObject
    {
        public static event Action<string>? OpenedDamageFolder;

        // 历史文件的数据信息
        private ObservableCollection<DamageFoldersInfo> damageFolders =
         new ObservableCollection<DamageFoldersInfo>();


        public ObservableCollection<DamageFoldersInfo> DamageFolders
        {
            get => damageFolders;
            set => damageFolders = value;
        }
        [ObservableProperty]
        int selectedIndex=-1;

        // 原始数据备份，用于搜索时恢复
        private ObservableCollection<DamageFoldersInfo> originalFolders = new ObservableCollection<DamageFoldersInfo>();

        [ObservableProperty]
        string searchHistoricalFileText;

        /// <summary>
        /// 搜索历史文件命令
        /// </summary>
        /// <param name="searchText">搜索关键词</param>
        [RelayCommand]
        private void SearchHistoricalFile(string searchText)
        {
            DamageFolders.Clear();

            if (string.IsNullOrWhiteSpace(searchText))
            {
                // 如果搜索文本为空，恢复显示所有数据
                foreach (var folder in originalFolders)
                {
                    DamageFolders.Add(folder);
                }
            }
            else
            {
                // 执行搜索（不区分大小写）
                var filtered = originalFolders
                    .Where(folder =>
                        folder.FolderName.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                        (folder.Remark != null && folder.Remark.Contains(searchText, StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                foreach (var folder in filtered)
                {
                    DamageFolders.Add(folder);
                }
            }

            // 重新生成序号
            int serialNumber = 1;
            foreach (var folder in DamageFolders)
            {
                folder.SerialNumber = serialNumber++;
            }

            // 重置选中索引
            SelectedIndex = -1;
        }



        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(OpenDocxCommand))]
        [NotifyCanExecuteChangedFor(nameof(ExportExcelCommand))]
        [NotifyCanExecuteChangedFor(nameof(DeleteFoldersCommand))]
        [NotifyCanExecuteChangedFor(nameof(OpenInFolderCommand))]
        private ObservableCollection<DamageFoldersInfo> selectedFolders = new ObservableCollection<DamageFoldersInfo>();

        public DamageFoldersListViewModel()
        {
            DamageFolders.Clear();
            var imgPaths = Directory.GetDirectories(Settings.Default.InPath);
            if (imgPaths.Length == 0 || imgPaths == null)
            {
                MessageBox.Warning("没有找到历史数据,请先进行数据采集");
                return;
            }

            var docxPaths = Directory.GetFiles(Settings.Default.DocxPath).Select(x => Path.GetFileName(x));
            int serialNumber = 1;//初始化序号
            foreach (var path in imgPaths)
            {
                DamageFolders.Add(
                    new DamageFoldersInfo
                    {
                        // 设置序号并递增
                        FolderName = Path.GetFileName(path),
                        HasDamage = File.Exists(Path.Combine(path, "result.json")),
                        CreatTime = Directory.GetCreationTime(path),
                        PngCount = Directory.GetFiles(path, "*.png").Count(),
                        DamagePngCount =
                        Directory.Exists(path.InReplaceOutString())
                        ? Directory.GetFiles(path.InReplaceOutString(), "*.png").Count()
                        : 0,
                        HasDocx = docxPaths.Any(x => x.StartsWith(Path.GetFileName(path))),
                        //HasMileage = File.Exists(Path.Combine(path, "OcrResult.json")),
                        HasFolderInfo = File.Exists(Path.Combine(path, "info.json")),
                    }
                );
            }

            DamageFolders = new ObservableCollection<DamageFoldersInfo>(DamageFolders
                .OrderByDescending(x => x.CreatTime));
            foreach (var d in DamageFolders)
            {
                d.SerialNumber = serialNumber++;
            }
            

            SelectedIndex = App.LastSelectedFolderIndex;
            originalFolders = new ObservableCollection<DamageFoldersInfo>(DamageFolders);
        }


        [RelayCommand]
        void DoubleClickDamageFolder(object pa)
        {
            if (pa != null)
            {
                var FolderName = (pa as DamageFoldersInfo).FolderName;
                var FolderPath = Path.Combine(Path.GetFullPath(Settings.Default.InPath), FolderName);
                OpenedDamageFolder?.Invoke(FolderPath);
                App.LastSelectedFolderIndex=SelectedIndex;
            }
            else
            {
                MessageBox.Warning("请双击文件夹");
            }
        }
        bool IsSelectedFolder() => SelectedFolders.Count > 1 ? false : true;

        [RelayCommand(CanExecute = nameof(IsSelectedFolder))]
        //[RelayCommand]
        void OpenDocx(DamageFoldersInfo d)
        {
            Console.WriteLine(SelectedFolders.Count);
            if (d.HasDocx)
            {
                Utilities.StartProcess(
                     DamageMaker.Properties.Settings.Default.OfficeLocation,
                     Path.GetFullPath(
                         Path.Combine(Settings.Default.DocxPath, d.FolderName + "钢轨探伤检测报告.docx")
                     )
                 );
            }
            else
            {
                MessageBox.Info("当前图片集没有报表文件,请选择当前图片集后点击导出报表按钮");
            }
        }
        bool IsSelectedsFolder() => SelectedFolders.Count >= 1 ? true : false;
        [RelayCommand(CanExecute = nameof(IsSelectedsFolder))]
        void ExportExcel()
        {

            var exc = new ExportExcellmentation();
            string[] haveJsonPath = SelectedFolders
                .Where(x => x.HasDamage)
                .Select(x => x.FolderName).ToArray();
            string[] noJsonPath = SelectedFolders
                .Where(x => !x.HasDamage)
                .Select(x => x.FolderName).ToArray();
            haveJsonPath = haveJsonPath.Select(x => Path.Combine(Properties.Settings.Default.InPath, x)).ToArray();
            if (haveJsonPath.Length == 0 && noJsonPath.Length == 0)
            {
                MessageBox.Warning("当前选择的图片集没有检测结果,无法导出");
                return;
            }
            else if (haveJsonPath.Length == 0 && noJsonPath.Length != 0)
            {
                MessageBox.Warning("当前选择的图片集还没有伤损数据,无法导出");
                return;
            }
            else if (haveJsonPath.Length != 0 && noJsonPath.Length != 0)
            {
                string filePath;
                if (haveJsonPath.Length == 1)
                {
                    filePath = Path.Combine(Properties.Settings.Default.ExcelsPath, $"{SelectedFolders[0].FolderName}.xlsx");
                }
                else
                {
                    filePath = Path.Combine(Properties.Settings.Default.ExcelsPath, $"{DateTime.Now.ToLongDateString() + DateTime.Now.Hour + "时" + DateTime.Now.Minute + "分"}.xlsx");
                }
                var b = exc.BulkExportExcel(haveJsonPath, filePath);
                if (b)
                {
                    MessageBox.Warning($"部分图片集没有伤损数据,已导出到{filePath}");
                }

                return;
            }
            else if (haveJsonPath.Length != 0 && noJsonPath.Length == 0)
            {
                string filePath;
                if (haveJsonPath.Length == 1)
                {
                    filePath = Path.Combine(Properties.Settings.Default.ExcelsPath, $"{SelectedFolders[0].FolderName}.xlsx");
                }
                else
                {
                    filePath = Path.Combine(Properties.Settings.Default.ExcelsPath, $"{DateTime.Now.ToLongDateString() + DateTime.Now.Hour + "时" + DateTime.Now.Minute + "分"}.xlsx");
                }

                var b = exc.BulkExportExcel(haveJsonPath, filePath);
                if (b)
                {
                    MessageBox.Success($"已导出到{filePath}");
                }
            }
        }
        [RelayCommand(CanExecute = nameof(IsSelectedsFolder))]
        void DeleteFolders()
        {
            if (SelectedFolders.Count == 0)
            {
                MessageBox.Warning("请先选择要删除的文件夹！");
                return;
            }

            foreach (var item in SelectedFolders)
            {
                // 删除文件夹对应的物理路径
                var folderInPath = Path.Combine(Settings.Default.InPath, item.FolderName);
                var folderOutPath = Path.Combine(Settings.Default.OutPath, item.FolderName);
                var pathsToDelete = new[] { folderInPath, folderOutPath };

                //在数据库里面删除文件夹信息
                using (var sqlHelper = new SQLHelper(Settings.Default.SqlPath))
                {
                    sqlHelper.DeleteFolderInfo(folderInPath);
                }

                foreach (var path in pathsToDelete)
                {
                    if (Directory.Exists(path))
                    {
                        try
                        {
                            Directory.Delete(path, true); // 递归删除文件夹
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Error($"删除文件夹 {item.FolderName} 失败: {ex.Message}");
                            continue;
                        }
                    }
                }
                // 从集合中移除
                DamageFolders.Remove(item);
            }

            // 重新生成序号
            int serialNumber = 1;
            foreach (var folder in DamageFolders)
            {
                folder.SerialNumber = serialNumber++;
            }
            

            MessageBox.Success("选中的文件夹已成功删除！");
        }
        [RelayCommand]
        void OpenInFolder(string pathName)
        {
            try
            {
                string FolderPath;

                if (pathName=="in文件夹")
                {
                    if(SelectedFolders.Count==0  || SelectedFolders.Count > 1)
                    {
                    FolderPath=Path.GetFullPath(Settings.Default.InPath);

                    }
                    else
                    {
                        var path = Path.Combine(Settings.Default.InPath, SelectedFolders.FirstOrDefault().FolderName);
                        FolderPath = Path.GetFullPath(path);
                    }
                }
                else if (pathName == "Excel文件夹")
                {
                    FolderPath = Path.GetFullPath(Settings.Default.ExcelsPath);
                }
                else
                {
                    FolderPath = "";
                }


                // 检查路径是否存在
                if (Directory.Exists(FolderPath))
                {
                    // 打开文件夹
                    Utilities.StartProcess("explorer.exe",FolderPath);
                    //Process.Start(FolderPath);
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
    }
}

