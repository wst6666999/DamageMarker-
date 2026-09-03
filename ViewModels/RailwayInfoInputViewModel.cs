using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DamageMaker.FileHandle;
using DamageMaker.Models;
using DamageMaker.Properties;
using DamageMaker.SqliteServer;
using DamageMarker.ViewModels;
using DamageMarker.Views;
using HandyControl.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using MessageBox = HandyControl.Controls.MessageBox;

namespace DamageMaker.ViewModels
{
    public partial class RailwayInfoInputViewModel:ObservableObject
    {
        [ObservableProperty]
        string ?instruments;
        [ObservableProperty]
        string serialNumber;
        [ObservableProperty]
        string workDate;
        [ObservableProperty]
        string workSection;
        [ObservableProperty]
        string ?workLength;
        [ObservableProperty]
        string? selectedRailType;
        [ObservableProperty]
         string ?selectedLineType;
        [ObservableProperty]
        string ?workGroup;
        [ObservableProperty]
        string ?operatorName;
        [ObservableProperty]
        string? selectedRouteLine;
        [ObservableProperty]
        string? selectedUpOrDown;
        [ObservableProperty]
        string? startMileage;
        [ObservableProperty]
        string? endMileage;
        string RailwayName;

        [ObservableProperty]
        private string? selectedCycleNumber;

        public static ScreenshotInfo NeedSavedInfo { get; set; } =new ScreenshotInfo();

        public ObservableCollection<string> RouteLines { get; } = new ObservableCollection<string>();

        private readonly string _routeLinesFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "RouteLines.txt");

        [ObservableProperty]
        string? selectedRouteLineText; // 新增属性，用于绑定编辑文本
        public RailwayInfoInputViewModel()
        {
            PlaybackWindow.ScreenshotStart += OnScreenShotStart;
            PlaybackWindow.ScreenshotFinished += OnScreenShotFinished;
            MainWindowViewModel.DataAnalyzeFinished += OnDataAnalyzeFinished;
            //AutoFillFromOCR();
            try
            {
                // 从文本文件加载路线数据
                LoadRouteLinesFromFile();

                // 如果没有数据，添加默认值
                if (RouteLines.Count == 0)
                {
                    RouteLines.Add("京广");
                    RouteLines.Add("合九");
                    // 保存默认值到文件
                    SaveRouteLinesToFile();
                }
            }
            catch (Exception ex)
            {
                // 容错处理
                Console.WriteLine($"加载路线数据失败: {ex.Message}");
                RouteLines.Add("京广");
                RouteLines.Add("合九");
            }

            SelectedRouteLine = RouteLines.FirstOrDefault();
            SelectedRouteLineText = SelectedRouteLine; // 初始化文本绑定
                                                       // 添加：初始化周期选择为空
            SelectedCycleNumber = null;

        }
        // 从文本文件加载路线数据
        private void LoadRouteLinesFromFile()
        {
            try
            {
                // 确保配置文件目录存在
                var configDirectory = Path.GetDirectoryName(_routeLinesFilePath);
                if (!Directory.Exists(configDirectory))
                {
                    Directory.CreateDirectory(configDirectory);
                }

                // 如果文件不存在，创建空文件
                if (!File.Exists(_routeLinesFilePath))
                {
                    File.WriteAllText(_routeLinesFilePath, "");
                    return;
                }

                // 读取文件内容
                var lines = File.ReadAllLines(_routeLinesFilePath, Encoding.UTF8);

                // 清空现有数据
                RouteLines.Clear();

                // 添加非空行
                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();
                    if (!string.IsNullOrEmpty(trimmedLine) && !RouteLines.Contains(trimmedLine))
                    {
                        RouteLines.Add(trimmedLine);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"读取路线文件失败: {ex.Message}");
                throw;
            }
        }

        // 保存路线数据到文本文件
        private void SaveRouteLinesToFile()
        {
            try
            {
                // 确保配置文件目录存在
                var configDirectory = Path.GetDirectoryName(_routeLinesFilePath);
                if (!Directory.Exists(configDirectory))
                {
                    Directory.CreateDirectory(configDirectory);
                }

                // 写入文件
                File.WriteAllLines(_routeLinesFilePath, RouteLines, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"保存路线文件失败: {ex.Message}");
                throw;
            }
        }
        // 当SelectedRouteLine改变时，同步更新SelectedRouteLineText
        partial void OnSelectedRouteLineChanged(string? value)
        {
            SelectedRouteLineText = value;
        }
        private void AutoFillFromOCR()
        {
            try
            {
                string pythonScriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test.py");
                // 确定工作目录和图片路径
                string workingDirectory = Path.Combine(Settings.Default.InPath, "information");
                string imagePath = Path.Combine(workingDirectory, "Information.png");

                                // 1. 先删除旧的OCR结果文件，确保读取到新数据
                string ocrResultFile = Path.Combine(workingDirectory, "out.json");

                if (File.Exists(ocrResultFile))
                {
                    try
                    {
                        File.Delete(ocrResultFile);
                        Debug.WriteLine("已删除旧的OCR结果文件: " + ocrResultFile);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("删除旧OCR结果文件失败: " + ex.Message);
                        // 继续执行，不因为删除失败而中断整个流程
                    }
                }
                if (File.Exists(pythonScriptPath))
                {
                    ProcessStartInfo start = new ProcessStartInfo();
                    start.FileName = "python";
                    start.Arguments = $"\"{pythonScriptPath}\" \"{imagePath}\"";
                    start.WorkingDirectory = workingDirectory;
                    start.UseShellExecute = false;
                    start.RedirectStandardOutput = true;
                    start.RedirectStandardError = true;
                    start.CreateNoWindow = true;

                    using (Process process = Process.Start(start))
                    {
                        using (StreamReader reader = process.StandardOutput)
                        {
                            string result = reader.ReadToEnd();
                            Debug.WriteLine("Python OCR 输出: " + result);
                        }

                        string error = process.StandardError.ReadToEnd();
                        if (!string.IsNullOrEmpty(error))
                        {
                            Debug.WriteLine("Python OCR 错误: " + error);
                        }

                        process.WaitForExit();

                        if (process.ExitCode == 0)
                        {
                            Debug.WriteLine("OCR 处理完成");
                        }
                        else
                        {
                            Debug.WriteLine($"OCR 处理失败，退出代码: {process.ExitCode}");
                        }
                    }
                }
                else
                {
                    Console.WriteLine("OCR 脚本不存在: " + pythonScriptPath);
                }

                if (File.Exists(ocrResultFile))
                {
                    string jsonContent = File.ReadAllText(ocrResultFile, Encoding.UTF8);
                    var ocrData = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonContent);

                    if (ocrData != null)
                    {
                        // 自动填充属性 - 根据JSON键名匹配
                        if (ocrData.ContainsKey("串号") && !string.IsNullOrEmpty(ocrData["串号"]))
                            this.SerialNumber = ocrData["串号"];

                        if (ocrData.ContainsKey("探伤日期") && !string.IsNullOrEmpty(ocrData["探伤日期"]))
                            this.WorkDate = ocrData["探伤日期"];

                        if (ocrData.ContainsKey("机型") && !string.IsNullOrEmpty(ocrData["机型"]))
                        {
                            if (ocrData["机型"].Equals("GCT-8C/11"))
                            {
                                this.Instruments = "8C";
                            } else if (ocrData["机型"].Equals("JGT-6M")) 
                            {
                                this.Instruments = "6M";
                            }
                        }
                            

                        if (ocrData.ContainsKey("回放员") && !string.IsNullOrEmpty(ocrData["回放员"]))
                            this.OperatorName = ocrData["回放员"];
                    }
                    else
                    {
                        Debug.WriteLine("OCR数据解析失败");
                    }
                }
                else
                {
                    Debug.WriteLine("OCR结果文件不存在: " + ocrResultFile);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OCR自动填充失败: {ex.Message}");
            }
        }

        private void OnDataAnalyzeFinished(string time)
        {
           NeedSavedInfo.RailWayInfo.ElapsedTimeForAnalyze = time;
            AboutJson.SaveJson(NeedSavedInfo, Path.Combine(Settings.Default.InPath, MainWindow.MainVm.ImgFolderName), "info.json");          
        }

        private void OnScreenShotStart(object sender, EventArgs args)
        {


            RailwayName = WorkSection + "+" + SerialNumber + "+" + WorkDate;
            MainWindowViewModel.FilePathIn = Path.GetFullPath(Path.Combine(Settings.Default.InPath, RailwayName));

            MainWindow.MainVm.ImgFolderName = RailwayName;

            if (!Directory.Exists(MainWindowViewModel.FilePathIn))
            {
                Directory.CreateDirectory(MainWindowViewModel.FilePathIn);
            }
            
        }

        private void OnScreenShotFinished(object sender, string time)
        {
            var o = sender as PlaybackWindow;
            NeedSavedInfo = NeedSavedInfo ?? new();
            NeedSavedInfo.ScreenshotOffset = o?.MoveRepeatPx ?? 0;
            NeedSavedInfo.ScreenshotDirection = o?.PlaybackDirection ?? MoveDirection.Left;
            NeedSavedInfo.ScreenshotWidthPx = o?.ImgWidthPx ?? 0;

            NeedSavedInfo.RailWayInfo = new RailInfo()
            {
                RailWayName = RailwayName,
                Instruments = this.Instruments,
                SerialNumber = this.SerialNumber,
                WorkDate = WorkDate,
                WorkSection = this.WorkSection,
                WorkLength = this.WorkLength,
                SelectedRailType = this.SelectedRailType,
                SelectedLineType = this.SelectedLineType,
                WorkGroup = this.WorkGroup,
                StartMileage = this.startMileage,
                EndMileage = this.endMileage,
                OperatorName = this.OperatorName,
                AnalyzeTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                ElapsedTimeForScrrnshot = time,
                SelectedRouteLine = this.SelectedRouteLine,
                SelectedUpOrDown = this.SelectedUpOrDown,
                CycleNumber = ParseCycleNumber(SelectedCycleNumber)
            };
            //AboutJson.SaveJson(NeedSavedInfo, Path.Combine(Settings.Default.InPath, MainWindow.MainVm.ImgFolderName), "info.json");
            Application.Current.MainWindow.WindowState = WindowState.Maximized;
        }

        public int ParseCycleNumber(object selectedItem)
        {
            if (selectedItem == null)
                return 0;

            // 获取 Content 属性值
            string cycleText;

            if (selectedItem is ComboBoxItem comboBoxItem)
            {
                // 如果是 ComboBoxItem，获取其 Content
                cycleText = comboBoxItem.Content?.ToString();
            }
            else if (selectedItem is string)
            {
                // 如果是字符串，直接使用
                cycleText = selectedItem.ToString();
            }
            else
            {
                // 其他类型，尝试转换为字符串
                cycleText = selectedItem.ToString();
            }

            if (string.IsNullOrEmpty(cycleText))
                return 0;

            // 提取数字部分
            var match = System.Text.RegularExpressions.Regex.Match(cycleText, @"\d+");
            if (match.Success && int.TryParse(match.Value, out int result))
            {
                return result;
            }

            return 0; // 默认值或错误处理
        }



        [RelayCommand]
        void SaveRailWayInfo(HandyControl.Controls.Window win)
        {
            if (!string.IsNullOrEmpty(SerialNumber) && WorkDate != null && !string.IsNullOrEmpty(WorkSection))
            {
                // 保存当前选择的仪器类型，供 PlaybackWindow 判断是否需要使用“双轨502”的特殊蓝框。
                Settings.Default.TrackData = Instruments ?? string.Empty;
                Settings.Default.Save();
                Console.WriteLine($"[保存仪器类型] TrackData = {Settings.Default.TrackData}");

                // ✅ 将数据存入共享静态变量
                SharedRailwayInfo.SerialNumber = SerialNumber;
                SharedRailwayInfo.WorkDate = WorkDate;
                SharedRailwayInfo.WorkSection = WorkSection;
                MessageBox.Success("信息保存完成");
                win.Close();
            }
            else
            {
                MessageBox.Info("信息输入不完整，请填写完整信息");
            }
        }

        // 新增方法：保存手动输入的线别到文件
        public void SaveManualRouteLine()
        {
            try
            {
                // 如果用户手动输入了新线别，且不在现有列表中
                if (!string.IsNullOrEmpty(SelectedRouteLineText) &&
                    !string.IsNullOrWhiteSpace(SelectedRouteLineText) &&
                    !RouteLines.Contains(SelectedRouteLineText.Trim()))
                {
                    // 添加到内存集合（去除首尾空格）
                    string newRouteLine = SelectedRouteLineText.Trim();
                    RouteLines.Add(newRouteLine);

                    // 保存到文件
                    SaveRouteLinesToFile();

                    // 更新当前选择的线别
                    SelectedRouteLine = newRouteLine;

                    Debug.WriteLine($"成功保存新线别: {newRouteLine}");
                }
                else if (!string.IsNullOrEmpty(SelectedRouteLineText))
                {
                    // 如果线别已存在，直接使用（去除首尾空格）
                    string trimmedLine = SelectedRouteLineText.Trim();
                    if (RouteLines.Contains(trimmedLine))
                    {
                        SelectedRouteLine = trimmedLine;
                    }
                }
            }
            catch (Exception ex)
            {
                // 容错处理
                Debug.WriteLine($"保存新线别失败: {ex.Message}");
            }
        }
    }
}
