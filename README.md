# ShangHai-DamagerMarker SH5.28 双轨里程适配版

探伤仪检测数据智能分析系统（上海版，双轨里程适配分支），版本 **V26.2**，根命名空间 `DamageMaker`。

> 本仓库基于 V5.28 版本做双轨里程适配：`Images` 表增加 `LineType` 字段记录左右轨线别；单选 / 双轨依据 `info.json` 的 Instruments 字段自动判断。

---

## 1. 技术栈

| 类别 | 选型 |
|------|------|
| 框架 | .NET 8（`net8.0-windows10.0.26100.0`），WPF，`AnyCPU`（Debug / Release / BEIJING） |
| MVVM | CommunityToolkit.Mvvm（`[ObservableProperty]`、`[RelayCommand]`、`WeakReferenceMessenger`） |
| UI 控件 | HandyControl（窗口 / 按钮 / 抽屉 / 进度 / 对话框）、Microsoft.Xaml.Behaviors |
| 图像 | OpenCvSharp4（伤损标注绘制、缩放）、Tesseract（里程 OCR） |
| 自动化 | FlaUI.UIA3（桌面 UI 控制、模拟键鼠、回放软件识别） |
| 数据库 | Microsoft.Data.Sqlite + SQLitePCLRaw（本地 `DB\Dm.db`） |
| 报表 | ClosedXML（Excel）、DocX（Word）、Newtonsoft.Json |
| 其它 | IWshRuntimeLibrary（COM，快捷方式 / 脚本执行） |

> ⚠️ **编译注意**：依赖 COM 引用，`dotnet CLI` 无法编译，必须使用 Visual Studio 自带 MSBuild 编译，例如：
> `MSBuild.exe DamageMarker.csproj -t:Build -p:Configuration=Debug`
> `DamageMarker.sln` 引用了缺失的项目 `D:\DamageMakerTest\DamageMakerTest.csproj`，需单独编译 `DamageMarker.csproj`。

---

## 2. 主界面布局（MainWindow）

1920×1000 最大化窗口，左右分栏（左主区 + 右功能区 384px），顶部为标题「探伤仪检测数据智能分析系统」、右上角「探伤软件同步搜索」、6M/8C/8Cnew 运行指示灯、里程定位搜索框。

**左主区**（自上而下）：
- **缩略图列表**：横向滚动缩略图（`ThumbnailList`），绑定 `ThumbnailImgInfos`；含里程区间输入（起始/结束里程 + 搜索 / 复原）、当前图片文件夹名。
- **主图片区**：`DamageDisplayBox` 用 OpenCvSharp 绘制伤损框，支持滚轮缩放、鼠标拖拽。下方操作栏：伤损等级下拉（I/II/III）、查看周期对比图片、修改当前标记 `[Z]`、显示标记 `[S]`、探头颜色展示、无伤 `[O]`、疑似 `[P]`、备注文本框。
- **底部状态栏**：探伤服务连接灯 + 启动/停止探伤分析服务、周期对比/图片绘制/判伤进度条、标记图片数/当前图片数、版本号。

**右功能区** 共 3 个 Tab：`功能区`、`项目总览`、`单个总览`。

---

## 3. 功能区按钮

**第一组（数据捕获区）**
- **选择软件**：下拉菜单列出已登记的回放软件（来自 `Settings.AppLocation`），末尾「✚ 添加回放软件」可选取并登记 `.exe`。每项点击会启动对应回放软件。
- **数据捕获**：弹出 `Capturing` 截图调试工具栏。
- **开始分析**：手动获取探伤数据，把图片发给后台 Python 判伤服务（`http://127.0.0.1:3333/endpoint`）。

**第二组（工具区）**
- **系统设置**：打开左侧抽屉设置面板（`SetupContent`）。
- **历史回放**：打开历史判伤文件夹列表窗口 `DamageFoldersList`。
- **周期对比**：周期对比功能。
- **导出报告**：导出 Word / Excel 判伤报告。

**顶栏 / 底部其它入口**：探伤软件同步搜索、里程定位搜索；底部「启动/停止探伤分析服务」（对应后台 `test.py` / `response:/stop_processing`）。

---

## 4. 项目总览 / 单个总览

- **项目总览**：`隐藏正常标记` 开关；两种显示模式「按伤损类别」（`DamageTree`，TreeView 节点带勾选框 / 颜色 / 次数 / 权重）与「按图片显示」（`ImageTree`，按图片+类别分组）。`LocationImgCommand` 定位到图片。
- **单个总览**：`DataGrid` 绑定 `DetailsList`，展示单张图片伤损列表（编号、分类颜色、相似度、显示 / 返回按钮）。

---

## 5. 智能回放流程（Capturing 调试工具栏）

**Capturing**：主界面「数据捕获」弹出的浮窗（600×50，半透明黑底），`Sprite.Show` 承载。工具栏按钮（7 列）：

| 按钮 | 处理函数 | 作用 |
|------|----------|------|
| 录入信息 | `InputInfo` | 打开 `RailwayInfoInputWindow` 录入本次探伤信息（存 `SharedRailwayInfo`） |
| 手动选区 | `ToNewSelectionWindow_Click` | 提示确认后打开 `NewSelectionWindow` 手动框选区域 / 调拼图 |
| 显示选区 | `ToPlaybackWindow_Click` | 按当前鼠标框选区域创建并显示 `PlaybackWindow` |
| 智能选区 | `intelligenceBoxed` | 获取前台应用名并按软件名匹配预置 BoxSize 坐标，自动创建 `PlaybackWindow` |
| 向左截图 | `LeftMove` | 校验重复命名后向左播放并截图 |
| （停，已注释） | `StopMove` | 未启用 |
| 向右截图 | `RightMove` | 校验重复命名后向右播放并截图 |

相关窗口：
- **NewSelectionWindow**：手动选区窗口（拼图 / 点大小调整，提高识别率）。
- **RailwayInfoInputWindow**：录入本次探伤基本信息（脊柱号 / 作业区间 / 作业日期，绑定 `SharedRailwayInfo`）。
- **PlaybackWindow**：（核心智能回放单例）FlaUI 模拟方向键/快进、注册 `Ctrl+Shift+A/D/P` 与空格热键，按 `MouseMovePixel/Interval/Offset` 边移动回放边截屏（`CopyFromScreen`），OCR 裁剪里程与 LineType（8C 线别中文 OCR），逐帧保存截图到 in 文件夹，产出 `result.json` / `info.json`。

---

## 6. 权限控制（近期新增，⚠️ 待完善）

目标：本系统只允许使用 **8C（`RailTest8C`）** 回放软件，其它软件一律弹窗「当前没有权限使用！」。

已实现的拦截：
1. **菜单入口**：`MainWindowViewModel.ProcessStart` —— 点「选择软件」里非 8C 软件，立即弹窗且不启动程序。
2. **智能选区入口**：`Capturing.intelligenceBoxed` —— 拿到前台 `AppName` 后判断，非 8C 弹窗返回。
3. **回放截图入口**：`PlaybackWindow.Playback()` —— 所有回放截图（智能选区 / 显示选区 / 左/右截图）最终汇聚点，拿到 `PlaybackedAppName` 后判断，非 8C 复位 `PlaybackingYN` 并弹窗返回。**此层已堵死全部绕过路径。**

> 附注：`RailwayInfoInputViewModel.OnScreenShotFinished` 曾因 `ScreenshotFinished` 在非 UI 线程触发、跨线程访问主窗口而崩溃（跳转 331 行），已用 `Application.Current.Dispatcher.Invoke` 包裹整个方法体修复。

---

## 7. 数据与图片分析

**数据流**：回放软件（Automation 控制）→ 截图入库（in 文件夹 / SQLite `Images`）→ 后台 Python 判伤（`GetJsonResult` / `test.py`，endpoint:3333）→ `result.json` → `MainData.ProcessData` 过滤 → 绘图标注（OpenCvSharp）→ out 文件夹 → 分类汇总 / 图片树 → 可导出 Word / Excel 报告。

**主流程**（`MainWindowViewModel.ShowThumbnails` → `MainData`）：
- `AboutJson.LoadOcrDatas` 读 `OcrResult.json`；`JsonPathToData` 读 `result.json` 得 `DamageDataList` 与 `ScreenshotInfo`（含 `RailWayInfo`）。
- 图片按 PNG + 创建时间排序，批量事务入 SQLite `Images`（BLOB ImageData、Mileage、LineType，后台线程），随后补写 `DamageAnnotations` 表。
- `MainData.ProcessData()` 做伤损过滤：`DataFilter.DistictRepeatedDamage` 去重、去空伤损点、过滤 id=27、30/35 规则冲突等。
- `SaveAllBoxSelectedImg` 用 OpenCvSharp 在 UI 线程绘制带框伤损图，经 `Channel` 多线程保存到 out 文件夹。

**伤损标注 / 级别判断**：
- 伤损类别由 `DamageCategory` 枚举 + `Records.DamageCategoryData` 定义；`DataConversion` 做 id↔颜色/名称；`DamageTransformation` 做焊缝附近伤损点转化（near-weld）。
- `DamageCheck`（疑似，P）、`NoDamageCheck`（无伤，O）、`ModifyImg`（修改 Z）、`ShowAllBoxSelected`（显示 S）、`DamageLevel` 写入 SQLite。
- **OCR**：`ImageProcessing\TesseractOCR.cs`，数字白名单 `"0123456789kmKM/HF+:."` 识别里程。
- **图片树**：`DamageTree`（按类别）、`ImageTree`（按图片+类别）、`InitCategorySummary` 生成分类统计摘要。

---

## 8. 模块目录职责

| 目录 | 职责 |
|------|------|
| `Automation` | FlaUI 桌面自动化。`AppInfo` 取前台应用名；`JGT_6M.cs` / `JGT_8C.cs` / `JGT_8Cnew.cs` 封装各回放软件控制 |
| `Common` | 通用工具：`MileageRecognition`（里程识别）、`Utilities`（含 IsWindows10 判断）等 |
| `DB` | SQLite 数据库文件 `Dm.db` |
| `SqliteServer` | `DataAccess.cs` / `SQLHelper.cs`：建库、增删改查（文件夹 / 图片 / 伤损标注 / LineType） |
| `GenerateReport` | `ExportExcellmentation.cs`（ClosedXML / Excel）、`ExportWord.cs`（DocX / Word，模板见 Resources） |
| `ImageProcessing` | `ImgProcessing.cs`（OpenCvSharp 缩放裁剪）、`TesseractOCR.cs`（里程识别） |
| `FileHandle` | `AboutJson.cs`（JSON 读写、图片入库）、`AboutImg.cs`、`ExtractSamples.cs`、`FilesDetection.cs`（侦测新文件分析） |
| `DamageDataProcessing` | `MainData.cs`（主数据装配、过滤）、`DataConversion.cs`、`DataFilter.cs`（去重）、`DamageTransformation.cs`、`ObtainInfo.cs` |
| `TrackData` | 双轨里程数据目录（`Settings.TrackData`） |
| `Converters` | XAML 值转换器（AddOne、Category、NullToEmpty、WorkDateToString 等） |
| `Message` | `WeakReferenceMessenger` 消息：隐藏鱼鳞、测试轨屏蔽、超速、失底波等 |
| `Models` | 数据模型：`DamageData` / `DamageDetails` / `DamageAnnotation` / `DamageFoldersInfo` / `ImgInfo` / `SqlImgInfo` / `MenuItem` / `Records` / `Enums` / `ScreenshotInfo` 等 |
| `ViewModels` / `Views` | 全部 VM 与窗口 |
| `Services` | 预留服务层（当前空） |
| `Resources` | 图标、Logo、Excel / Word 模板（`excel模板.xlsx`、`excel模板SH.xlsx`、`word报告模板*.docx`、`钢轨探伤检测报告模板.docx`） |
| `ExcelsPath` | Excel 导出输出目录 |

---

## 9. 设置面板（SetupContent）

Tab 分类：
- **路径设置**：捕获 / 伤损 / Word / Excel 路径修改。
- **截图设置**：偏移、移动像素、间隔。
- **分析设置**：过滤重复、隐藏鱼鳞伤、规则标记展示数量。
- **屏蔽设置**：前 / 后测试轨屏蔽张数。
- **超速设置**：限速、焊缝速度。
- **失底波设置**：失底波长度。

---

## 10. 历史回放（DamageFoldersList）

分页 DataGrid，列：串号、线别、周期、疑似伤损数量、捕获数量、是否有报表、创建日期、备注。支持搜索、打开 Word / 导出 Word / Excel、删除、打开 in / Excel 目录、双击打开判伤集。

---

## 11. 开发备注

- 打快照时建议连同 `Settings.settings`、`TODO.md` 一起记录，方便回档。