using DamageMarker.Views;
using HandyControl.Controls;
using System.Windows;
using DamageMaker.Common;
using Windows.UI.Popups;
using static DamageMarker.App;
using MessageBox = HandyControl.Controls.MessageBox;
using DamageMaker.ViewModels;
using DamageMaker.Models;
using System;
using System.Data;
using System.Data.SqlClient;
using DamageMaker.SqliteServer;
using DamageMaker.Properties;
using Microsoft.Data.Sqlite;
using DamageMarker.ViewModels;
using System.IO;
using System.Text;


namespace DamageMaker.Views
{
    /// <summary>
    /// Capturing.xaml 的交互逻辑
    /// </summary>
    public partial class Capturing
    {

        PlaybackWindow? PbWin;
        RailwayInfoInputWindow RailWin;
        public static event Action<object, EventArgs>? RailWayInfoInputed;
        public Capturing()
        {
            InitializeComponent();
        }
        void ToNewSelectionWindow_Click(object sender, RoutedEventArgs e)
        {
            var b = HandyControl.Controls.MessageBox.Ask("在智能回放前,请您确认是否点击拼图按钮并将点大小改为4以提高识别率") switch
            {
                MessageBoxResult.OK => true,
                _ => false,
            };
            if (b)
            {
                new NewSelectionWindow().Show();
            }

        }
        void ToPlaybackWindow_Click(object sender, RoutedEventArgs e)
        {
            if (RailWin == null)
            {
                MessageBox.Info("智能回放前请先输入铁轨信息");
                RailWin = new RailwayInfoInputWindow();
                RailWayInfoInputed?.Invoke(sender, e);
            }
            PbWin = PlaybackWindow.GetInstance((mouseEndX >= mouseStartX) ? mouseStartX : mouseEndX, (mouseEndY >= mouseStartY) ? mouseStartY : mouseEndY, Math.Abs(mouseEndX - mouseStartX), Math.Abs(mouseEndY - mouseStartY));
            PbWin.Show();
            var b = PbWin.Visibility;

        }

        private async void LeftMove(object sender, RoutedEventArgs e)
        {
            try
            {
                if (PbWin?.Activate() ?? false)
                {
                    using (var sqlHelper = new SQLHelper(Settings.Default.SqlPath))
                    {
                        sqlHelper.EnsureConnectionOpen();
                        string SerialNumber = SharedRailwayInfo.SerialNumber ?? null;
                        string WorkDate = SharedRailwayInfo.WorkDate ?? null;
                        string WorkSection = SharedRailwayInfo.WorkSection ?? null;

                        if (SerialNumber != null && WorkDate != null && WorkSection != null)
                        {
                            var isSerialNumberDuplicate = sqlHelper.CompareStringWithDatabase(SerialNumber, "DamageFolders", "SerialNumber");
                            var isWorkDateDuplicate = sqlHelper.CompareStringWithDatabase(WorkDate, "DamageFolders", "WorkDate");
                            var isWorkSectionDuplicate = sqlHelper.CompareStringWithDatabase(WorkSection, "DamageFolders", "WorkSection");
                            Console.WriteLine(isSerialNumberDuplicate);
                            Console.WriteLine(isWorkDateDuplicate);
                            Console.WriteLine(isWorkSectionDuplicate);
                            if (isWorkDateDuplicate && isSerialNumberDuplicate && isWorkSectionDuplicate)
                            {

                                // 2. 获取重复记录的ID
                                int duplicateId = sqlHelper.GetDamageFolderId(
                                    WorkDate,
                                    SerialNumber,
                                    WorkSection);

                                if (duplicateId > 0)
                                {
                                    // 3. 显示确认对话框
                                    var result2 = await Dispatcher.InvokeAsync(() =>
                                        HandyControl.Controls.MessageBox.Show(
                                            "命名重复，若继续截图会覆盖上次分析结果",
                                            "是否继续",
                                            MessageBoxButton.YesNo,
                                            MessageBoxImage.Warning));

                                    if (result2 == MessageBoxResult.Yes)
                                    {
                                        bool success = true;
                                        StringBuilder errorMsg = new StringBuilder();

                                        // 4. 删除文件夹
                                        string folderPath = Path.Combine(
                                            Settings.Default.InPath,
                                            $"{WorkSection}+{SerialNumber}+{WorkDate}");

                                        try
                                        {
                                            if (Directory.Exists(folderPath))
                                            {
                                                // 确保可以删除只读文件
                                                foreach (var file in Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories))
                                                {
                                                    File.SetAttributes(file, FileAttributes.Normal);
                                                }
                                                Directory.Delete(folderPath, true);
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            success = false;
                                            errorMsg.AppendLine($"删除文件夹失败: {ex.Message}");
                                        }

                                        // 5. 删除数据库记录
                                        try
                                        {
                                            if (!sqlHelper.DeleteImagesByFolderId(duplicateId))
                                            {
                                                success = false;
                                                errorMsg.AppendLine($"删除数据库记录失败: {sqlHelper.GetLastErrorMessage()}");
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            success = false;
                                            errorMsg.AppendLine($"数据库错误: {ex.Message}");
                                        }

                                        // 6. 显示操作结果
                                        if (success)
                                        {
                                            HandyControl.Controls.Growl.SuccessGlobal("删除成功，继续播放...");
                                            PbWin.ToLeftPlayback();
                                        }
                                        else
                                        {
                                            HandyControl.Controls.MessageBox.Error(
                                                $"删除操作未完全成功:\n{errorMsg}",
                                                "操作失败");
                                        }
                                        return;
                                    }
                                    else if (result2 == MessageBoxResult.No)
                                    {
                                        // 用户选择不继续，停止播放
                                        Console.WriteLine("1");
                                        HandyControl.Controls.Growl.InfoGlobal("操作已取消，停止截图...");
                                        return;
                                    }
                                }
                            }
                        }

                        // 没有重复数据或用户取消，继续播放
                        Console.WriteLine("2");
                        PbWin.ToLeftPlayback();
                    }
                }
                else
                {
                    HandyControl.Controls.MessageBox.Error("请框选标记出现后点击此按钮");
                }
            }
            catch (Exception ex)
            {
                HandyControl.Controls.MessageBox.Error(
                    $"发生未预期的错误:\n{ex.Message}",
                    "系统错误");
            }
        }



        private async void RightMove(object sender, RoutedEventArgs e)
        {
            try
            {
                if (PbWin?.Activate() ?? false)
                {
                    using (var sqlHelper = new SQLHelper(Settings.Default.SqlPath))
                    {
                        sqlHelper.EnsureConnectionOpen();
                        string SerialNumber = SharedRailwayInfo.SerialNumber ?? null;
                        string WorkDate = SharedRailwayInfo.WorkDate ?? null;
                        string WorkSection = SharedRailwayInfo.WorkSection ?? null;

                        if (SerialNumber != null && WorkDate != null && WorkSection != null)
                        {
                            var isSerialNumberDuplicate = sqlHelper.CompareStringWithDatabase(SerialNumber, "DamageFolders", "SerialNumber");
                            var isWorkDateDuplicate = sqlHelper.CompareStringWithDatabase(WorkDate, "DamageFolders", "WorkDate");
                            var isWorkSectionDuplicate = sqlHelper.CompareStringWithDatabase(WorkSection, "DamageFolders", "WorkSection");

                            if (isWorkDateDuplicate && isSerialNumberDuplicate && isWorkSectionDuplicate)
                            {

                                // 2. 获取重复记录的ID
                                int duplicateId = sqlHelper.GetDamageFolderId(
                                    WorkDate,
                                    SerialNumber,
                                    WorkSection);

                                if (duplicateId > 0)
                                {
                                    // 3. 显示确认对话框
                                    var result2 = await Dispatcher.InvokeAsync(() =>
                                        HandyControl.Controls.MessageBox.Show(
                                            "命名重复，若继续截图会覆盖上次分析结果",
                                            "是否继续",
                                            MessageBoxButton.YesNo,
                                            MessageBoxImage.Warning));

                                    if (result2 == MessageBoxResult.Yes)
                                    {
                                        bool success = true;
                                        StringBuilder errorMsg = new StringBuilder();

                                        // 4. 删除文件夹
                                        string folderPath = Path.Combine(
                                            Settings.Default.InPath,
                                            $"{WorkSection}+{SerialNumber}+{WorkDate}");

                                        try
                                        {
                                            if (Directory.Exists(folderPath))
                                            {
                                                // 确保可以删除只读文件
                                                foreach (var file in Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories))
                                                {
                                                    File.SetAttributes(file, FileAttributes.Normal);
                                                }
                                                Directory.Delete(folderPath, true);
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            success = false;
                                            errorMsg.AppendLine($"删除文件夹失败: {ex.Message}");
                                        }

                                        // 5. 删除数据库记录
                                        try
                                        {
                                            if (!sqlHelper.DeleteImagesByFolderId(duplicateId))
                                            {
                                                success = false;
                                                errorMsg.AppendLine($"删除数据库记录失败: {sqlHelper.GetLastErrorMessage()}");
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            success = false;
                                            errorMsg.AppendLine($"数据库错误: {ex.Message}");
                                        }

                                        // 6. 显示操作结果
                                        if (success)
                                        {
                                            HandyControl.Controls.Growl.SuccessGlobal("删除成功，继续播放...");
                                            PbWin.ToLeftPlayback();
                                        }
                                        else
                                        {
                                            HandyControl.Controls.MessageBox.Error(
                                                $"删除操作未完全成功:\n{errorMsg}",
                                                "操作失败");
                                        }
                                        return;
                                    }
                                    else if (result2 == MessageBoxResult.No)
                                    {
                                        // 用户选择不继续，停止播放
                                        Console.WriteLine("1");
                                        HandyControl.Controls.Growl.InfoGlobal("操作已取消，停止截图...");
                                        return;
                                    }
                                }
                            }
                        }

                        // 没有重复数据或用户取消，继续播放

                        PbWin.ToLeftPlayback();
                    }
                }
                else
                {
                    HandyControl.Controls.MessageBox.Error("请框选标记出现后点击此按钮");
                }
            }
            catch (Exception ex)
            {
                HandyControl.Controls.MessageBox.Error(
                    $"发生未预期的错误:\n{ex.Message}",
                    "系统错误");
            }
        }

        private void InputInfo(object sender, RoutedEventArgs e)
        {
            RailWin = new();
            RailWayInfoInputed?.Invoke(sender, e);
        }

        private void intelligenceBoxed(object sender, RoutedEventArgs e)
        {
            if (RailWin == null)
            {
                MessageBox.Info("智能回放前请先输入铁轨信息");
                RailWin = new RailwayInfoInputWindow();
                RailWayInfoInputed?.Invoke(sender, e);
            }

            var AppName = Automation.AppInfo.GetFocusedApplicationName();
            Console.WriteLine(AppName);

            // 在智能框选前验证必填字段
            var vm = RailWin.DataContext as RailwayInfoInputViewModel;
            if (vm == null ||
                string.IsNullOrWhiteSpace(vm.SerialNumber) ||
                string.IsNullOrWhiteSpace(vm.WorkSection) ||
                string.IsNullOrWhiteSpace(vm.WorkDate))
            {
                MessageBox.Warning("请先填写所有必填信息（仪器串号、作业区间、作业日期）");
                RailWin.Focus(); // 将焦点返回给输入窗口
                RailWin = new RailwayInfoInputWindow();
                RailWayInfoInputed?.Invoke(sender, e);
                return;
            }

            double[] BoxSize = AppName switch
            {
                "RailTest8D.exe" => new double[] { 35, 148, 1872, 706 },
                "RailTest8C.exe" => new double[] { 44, 103, 1874, 709 },
                "gt_20_replayer.exe" => new double[] { 227, 100, 1700, 803 },
                "JGT-6M信息管理.exe" => new double[] { 8, 116, 1918, 734 },
                "CTKJ-443.exe" => new double[] { 9, 126, 1915, 815 },
                "大仪器PlusV4.4.4_22.8.25.exe" => new double[] { 205, 35, 1723, 765 }, 
                _ => new double[] { }
            };

            if (BoxSize.Length == 0)
            {
                MessageBox.Warning("当前软件暂不支持智能回放");
                return;
            }
            else
            {
                PbWin = PlaybackWindow.GetInstance(BoxSize[0], BoxSize[1], BoxSize[2], Utilities.IsWindows10() ? BoxSize[3] + 9 : BoxSize[3]);
                PbWin.Show();
            }
        }
    }
}