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
using DocumentFormat.OpenXml.Wordprocessing;
using Settings = DamageMaker.Properties.Settings;
using System.Drawing;
using System.Windows.Forms;
using System.Diagnostics;
using System.Windows.Input;


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
                                        
                                        HandyControl.Controls.Growl.InfoGlobal("操作已取消，停止截图...");
                                        return;
                                    }
                                }
                            }
                        }
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
                                            PbWin.ToRightPlayback();
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
                                        HandyControl.Controls.Growl.InfoGlobal("操作已取消，停止截图...");
                                        return;
                                    }

                                    Thread.Sleep(100);
                                    //隐藏鼠标
                                    Mouse.AddGotMouseCaptureHandler(this, (s, args) =>
                                    {
                                        System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.None;
                                    });

                                }
                            }
                        }

                        // 没有重复数据或用户取消，继续播放

                        PbWin.ToRightPlayback();
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
            //var AppName = Automation.AppInfo.GetFocusedApplicationName();
            //Console.WriteLine(AppName);

            //// 通道颜色截取BoxSize（使用您确定的坐标 [x, y, width, height]）
            //double[] BoxSize = AppName switch
            //{
            //    "RailTest8C.exe" => new double[] { 36, 835, 1155, 203 }, // 您确定的坐标
            //    "RailTest8D.exe" => new double[] { 35, 886, 1294, 103 }, // 可以先用相同的坐标测试
            //    "JGT-6M信息管理.exe" => new double[] { 7, 855, 1840, 183 },
            //    "CTKJ-443.exe" => new double[] { 12, 952, 644, 90 },
            //    "gt_20_replayer.exe" => new double[] { 295, 108, 1631, 792 },
            //    "GT2PlusReplayer.exe" => new double[] { 216, 98, 1698, 790 },
            //    "大仪器PlusV4.4.4_22.8.25.exe" => new double[] { 226, 853, 1021, 114 },
            //    "DL探伤数据回放软件.exe" => new double[] { 223, 854, 1031, 106 },
            //    _ => new double[] { }
            //};

            //if (BoxSize.Length == 0)
            //{
            //    MessageBox.Warning("未截取到有效数据");
            //    return;
            //}
            //else
            //{
            //    // 只截取一次通道颜色
            //    if (BoxSize.Length == 4)
            //    {
            //        Console.WriteLine($"开始截取信息，应用程序: {AppName}");
            //        Console.WriteLine($"信息区域: x={BoxSize[0]}, y={BoxSize[1]}, width={BoxSize[2]}, height={BoxSize[3]}");
            //        //创建一个临时目录
            //        string currentDirectory = Settings.Default.InPath;
            //        string saveDirectory = Path.Combine(currentDirectory, "information");
            //        string informationPath = CaptureInformation(BoxSize, saveDirectory);

            //        if (!string.IsNullOrEmpty(informationPath))
            //        {
            //            Console.WriteLine($"截取信息保存到: {informationPath}");
            //        }
            //        else
            //        {
            //            Console.WriteLine("信息截取失败");
            //        }
            //        //删除Information文件夹
            //        try
            //        {
            //            if (Directory.Exists(saveDirectory))
            //            {
            //                // 确保可以删除只读文件
            //                foreach (var file in Directory.GetFiles(saveDirectory, "*", SearchOption.AllDirectories))
            //                {
            //                    File.SetAttributes(file, FileAttributes.Normal);
            //                }
            //                Directory.Delete(saveDirectory, true);
            //            }
            //        }
            //        catch (Exception ex)
            //        {
            //            Console.WriteLine($"删除临时信息文件夹失败: {ex.Message}");
            //        }
            //    }
            //}
            RailWin = new();
            RailWin.Activate(); // 激活窗口
            RailWin.Topmost = true; // 设置为最顶层
            RailWin.Focus(); // 获取焦点
            RailWayInfoInputed?.Invoke(sender, e);
        }

        private string CaptureInformation(double[] boxSize, string saveDirectory)
        {
            try
            {
                if (boxSize.Length != 4)
                {
                    Console.WriteLine("BoxSize参数错误，需要4个值");
                    return null;
                }

                // 解析坐标 [x, y, width, height]
                int x = (int)boxSize[0];
                int y = (int)boxSize[1];
                int width = (int)boxSize[2];
                int height = (int)boxSize[3];

                Console.WriteLine($"截取参数: x={x}, y={y}, width={width}, height={height}");

                // 验证参数
                if (width <= 0 || height <= 0)
                {
                    Console.WriteLine($"错误: 尺寸必须为正数 width={width}, height={height}");
                    return null;
                }

                // 检查屏幕边界
                var screenBounds = Screen.PrimaryScreen.Bounds;
                Console.WriteLine($"屏幕边界: {screenBounds}");

                if (x < 0 || y < 0 || x + width > screenBounds.Width || y + height > screenBounds.Height)
                {
                    Console.WriteLine($"警告: 截取区域可能超出屏幕范围");
                    Console.WriteLine($"截取区域: ({x},{y}) - ({x + width},{y + height})");
                    Console.WriteLine($"屏幕范围: (0,0) - ({screenBounds.Width},{screenBounds.Height})");
                }

                // 确保目录存在
                if (!Directory.Exists(saveDirectory))
                {
                    Directory.CreateDirectory(saveDirectory);
                }

                // 截屏
                using (Bitmap bitmap = new Bitmap(width, height))
                {
                    using (Graphics g = Graphics.FromImage(bitmap))
                    {
                        g.CopyFromScreen(x, y, 0, 0, new System.Drawing.Size(width, height));
                        Console.WriteLine("屏幕截取完成");
                    }

                    // 保存文件
                    string fileName = "Information.png";
                    string filePath = Path.Combine(saveDirectory, fileName);

                    // 删除已存在的文件
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                    }

                    bitmap.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);
                    Console.WriteLine($"文件保存成功: {filePath}");

                    return filePath;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"截取时出错: {ex.Message}");
                Console.WriteLine($"异常类型: {ex.GetType().Name}");
                Console.WriteLine($"堆栈跟踪: {ex.StackTrace}");
                return null;
            }
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

            // 权限控制：智能回放只允许使用 8C 回放软件（RailTest8C），
            // 其余软件（含用户手动在电脑上打开的）一律提示无权限。
            if (AppName.IndexOf("RailTest8C", StringComparison.OrdinalIgnoreCase) < 0)
            {
                MessageBox.Info("当前没有权限使用！");
                return;
            }

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
                "RailTest8D" => new double[] { 35, 148, 1872, 706 },
                "RailTest8C" => new double[] { 44, 103, 1874, 709 },
                "RailTest-new" => new double[] { 3, 98, 1915, 710 },
                "gt_20_replayer" => new double[] { 295, 108, 1631,792 },
                "GT2PlusReplayer" => new double[] { 216, 98, 1698, 790 },
                "JGT-6M信息管理" => new double[] { 8, 116, 1918, 695 },
                "CTKJ-443" => new double[] { 9, 126, 1915, 815 },
                "大仪器PlusV4.4.4_22.8.25" => new double[] { 205, 35, 1723, 765 },
                "DL双轨回放软件" => new double[] { 205, 35, 1720, 765 },
                "DL探伤数据回放软件"=>new double[] { 205, 35, 1720, 765 },
                "EGT-60Replayer32" => new double[] { 223, 131, 1701, 794 },
                "回放软件V3.0A" => new double[] { 86, 47, 1775, 682 },
                _ => new double[] { }

            };
            Console.WriteLine($"AppName = [{AppName}]");
            Console.WriteLine($"BoxSize Length = {BoxSize.Length}");

            if (BoxSize.Length == 4)
            {
                Console.WriteLine($"BoxSize = x:{BoxSize[0]}, y:{BoxSize[1]}, w:{BoxSize[2]}, h:{BoxSize[3]}");
            }

            //// 通道颜色截取BoxSize（使用您确定的坐标 [x, y, width, height]）
            //double[] ChannelColorBoxSize = AppName switch
            //{
            //    "RailTest8C.exe" => new double[] { 39, 869, 1107, 84 }, // 您确定的坐标
            //    "RailTest8D.exe" => new double[] { 35, 886, 1294, 103 }, // 可以先用相同的坐标测试
            //    "JGT-6M信息管理.exe" => new double[] { 7, 911, 1412, 42 },
            //    "CTKJ-443.exe" => new double[] { 12, 952, 644, 90 },
            //    "大仪器PlusV4.4.4_22.8.25.exe" => new double[] { 226, 853, 1021, 114 },
            //    "DL双轨回放软件.exe" => new double[] { 223, 854, 1031, 106 },
            //    _ => new double[] { }
            //};

            if (BoxSize.Length == 0)
            {
                MessageBox.Warning("当前软件暂不支持智能回放");
                return;
            }
            else
            {
                //// 只截取一次通道颜色
                //if (ChannelColorBoxSize.Length == 4)
                //{
                //    Console.WriteLine($"开始截取通道颜色，应用程序: {AppName}");
                //    Console.WriteLine($"通道颜色区域: x={ChannelColorBoxSize[0]}, y={ChannelColorBoxSize[1]}, width={ChannelColorBoxSize[2]}, height={ChannelColorBoxSize[3]}");
                //    var RailwayName = SharedRailwayInfo.WorkSection + "+" + SharedRailwayInfo.SerialNumber + "+" + SharedRailwayInfo.WorkDate;
                //    string currentDirectory = Path.GetFullPath(Path.Combine(Settings.Default.InPath, RailwayName));
                //    string saveDirectory = Path.Combine(currentDirectory, "color");
                //    string channelColorsPath = CaptureChannelColors(ChannelColorBoxSize, saveDirectory);

                //    if (!string.IsNullOrEmpty(channelColorsPath))
                //    {
                //        Console.WriteLine($"通道颜色已保存到: {channelColorsPath}");
                //    }
                //    else
                //    {
                //        Console.WriteLine("通道颜色截取失败");
                //    }
                //}

                // 8C 可见红框保持原来的 709 高；900 高临时采集范围由 PlaybackWindow 内部独立控制。
                // 其他软件继续保留 Windows 10 下补 9px 的原逻辑。
                double playbackHeight = AppName == "RailTest8C"
                    ? 709
                    : (Utilities.IsWindows10() ? BoxSize[3] + 9 : BoxSize[3]);

                PbWin = PlaybackWindow.GetInstance(
                    BoxSize[0],
                    BoxSize[1],
                    BoxSize[2],
                    playbackHeight,
                    AppName);
                PbWin.Show();
            }

        }

        /// <summary>
        /// 截取通道颜色（使用确定的坐标）
        /// </summary>
        private string CaptureChannelColors(double[] channelBoxSize, string saveDirectory)
        {
            try
            {
                if (channelBoxSize.Length != 4)
                {
                    Console.WriteLine("通道颜色BoxSize参数错误，需要4个值");
                    return null;
                }

                // 解析坐标 [x, y, width, height]
                int x = (int)channelBoxSize[0];
                int y = (int)channelBoxSize[1];
                int width = (int)channelBoxSize[2];
                int height = (int)channelBoxSize[3];

                Console.WriteLine($"截取参数: x={x}, y={y}, width={width}, height={height}");

                // 验证参数
                if (width <= 0 || height <= 0)
                {
                    Console.WriteLine($"错误: 尺寸必须为正数 width={width}, height={height}");
                    return null;
                }

                // 检查屏幕边界
                var screenBounds = Screen.PrimaryScreen.Bounds;
                Console.WriteLine($"屏幕边界: {screenBounds}");

                if (x < 0 || y < 0 || x + width > screenBounds.Width || y + height > screenBounds.Height)
                {
                    Console.WriteLine($"警告: 截取区域可能超出屏幕范围");
                    Console.WriteLine($"截取区域: ({x},{y}) - ({x + width},{y + height})");
                    Console.WriteLine($"屏幕范围: (0,0) - ({screenBounds.Width},{screenBounds.Height})");
                }

                // 确保目录存在
                if (!Directory.Exists(saveDirectory))
                {
                    Directory.CreateDirectory(saveDirectory);
                }

                // 截屏
                using (Bitmap bitmap = new Bitmap(width, height))
                {
                    using (Graphics g = Graphics.FromImage(bitmap))
                    {
                        g.CopyFromScreen(x, y, 0, 0, new System.Drawing.Size(width, height));
                        Console.WriteLine("屏幕截取完成");
                    }

                    // 保存文件
                    string fileName = "ChannelColor.png";
                    string filePath = Path.Combine(saveDirectory, fileName);

                    // 删除已存在的文件
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                    }

                    bitmap.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);
                    Console.WriteLine($"文件保存成功: {filePath}");

                    return filePath;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"截取通道颜色时出错: {ex.Message}");
                Console.WriteLine($"异常类型: {ex.GetType().Name}");
                Console.WriteLine($"堆栈跟踪: {ex.StackTrace}");
                return null;
            }
        }
    }
}