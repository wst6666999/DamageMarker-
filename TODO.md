# TODO 记录

> **工作约定**：每次代码/界面改动后，必须立即把改动（需求、文件、位置、内容、验证结果）追加记录到本文件的「已完成任务」中，不等用户提醒。

## 已完成任务

### 1. 权限功能：只允许启动 8C 软件

- **需求**：主界面左侧功能区「选择软件」菜单项里，除了 8C 软件外，点击其他软件时需要弹窗提示"当前没有权限使用"。
- **改动文件**：`ViewModels/MainWindowViewModel.cs`
- **改动位置**：`ProcessStart` 命令方法（所有菜单软件项的点击唯一入口）。
- **改动内容**：
  - 在启动前判断所选的 exe 文件名是否包含 `RailTest8C`（与项目现有 8C 识别方式一致，见 `Automation/AppInfo.cs`、`Views/Capturing.xaml.cs`）。
  - 若文件名不包含 `RailTest8C`，弹出 `MessageBox.Info("当前没有权限使用！")` 并直接返回，不启动程序。
  - 仅当是 8C 软件时才执行原来的启动、提示、截图逻辑。
- **验证**：使用 Visual Studio2022 编译通过，0 错误（仅有原有警告）。

### 2. 权限功能增强：拦截用户手动打开的其他回放软件

- **需求**：用户即使不通过菜单、直接在自己电脑上打开其他（非 8C）回放软件，只要进入「智能回放」流程也必须被拦下。
- **改动文件**：`Views/Capturing.xaml.cs`
- **改动位置**：`intelligenceBoxed` 方法（智能回放核心入口，通过 `GetFocusedApplicationName()` 拿到前台应用名 `AppName`）。
- **改动内容**：
  - 在拿到 `AppName` 后、验证必填字段之前判断，若 `AppName` 不包含 `RailTest8C`，弹出 `MessageBox.Info("当前没有权限使用！")` 并直接返回。
  - 这样无论软件是通过主界面菜单启动、还是用户手动双击 exe 打开，只要进入智能回放就会被拦截。
- **权限拦截点汇总**：
  - 第 1 层（菜单入口）：`MainWindowViewModel.ProcessStart` —— 点菜单其他软件立即弹窗且不启动。
  - 第 2 层（回放入口）：`Capturing.intelligenceBoxed` —— 覆盖用户手动打开软件的情况。
- **验证**：Visual Studio2022 编译通过，0 错误。

### 3. 修复：截图结束跨线程异常（RailwayInfoInputViewModel.cs:325 附近）

- **现象**：打开 8C → 智能回放 → 框体出来后切到别的软件截图，截图未成功却抛出未处理异常（跳转到 `RailwayInfoInputViewModel` 第 331 行）。
- **根因**：`PlaybackWindow.Playback()` 是 `async void`，`ScreenshotFinished` 事件可能在非 UI 线程触发；其订阅者 `RailwayInfoInputViewModel.OnScreenShotFinished` 里的 `Application.Current.MainWindow.WindowState = WindowState.Maximized` 直接访问 WPF 主窗口对象，跨线程访问抛 InvalidOperationException（与权限无关，属线程序隐患，本次被触发）。
- **改动文件**：`ViewModels/RailwayInfoInputViewModel.cs`
- **改动内容**：把整个 `OnScreenShotFinished` 方法体用 `Application.Current.Dispatcher.Invoke(() => {...})` 包起来，保证在主 UI 线程执行。

### 4. 权限功能：堵死「显示选区/左右截图」绕过路径

- **需求**：上次只拦截了「智能选区」，用户仍可通过「显示选区 + 左右截图」绕过；且测试正是用该路径切到别的软件截图。
- **改动文件**：`Views/PlaybackWindow.xaml.cs`
- **改动位置**：`Playback` 方法（所有回放截图最终汇聚点，且已调用 `GetFocusedApplicationName()`）。
- **改动内容**：拿到 `PlaybackedAppName` 后判断，若非 `RailTest8C`，复位 `PlaybackingYN=false` 并弹「当前没有权限使用！」后 return。
- **效果**：智能选区、显示选区、左/右截图三条入口全部被堵死，无法再用其他回放软件截图。
- **验证**：Visual Studio2022 编译通过，0 错误。

### 5. 界面风格：选择软件下拉菜单背景改绿色

- **需求**：把「选择软件」点开后的下拉菜单项背景从 `PrimaryBrush`（HandyControl 主题蓝）改为 `#2D9F4C` 绿色，与「开始分析」按钮同色。
- **改动文件**：`Views/MainWindow.xaml`
- **改动位置**：第 648 行，`MenuItem.ItemTemplate` 里的 `MenuItem.Background`。
- **改动内容**：`Background="{StaticResource PrimaryBrush}"` → `Background="{StaticResource ShanHePurpleBrush}"`（`ShanHePurpleBrush` 定义在同文件第 54 行，颜色 `#2D9F4C`）。
- **备注**：菜单项鼠标悬停时 WPF 默认高亮色仍会覆盖背景，如需悬停也保持绿色需另加触发器。
- **验证**：Visual Studio2022 编译通过，0 错误。

### 6. 界面风格：样本图片窗口蓝色字体改绿色

- **需求**：把 SampleImg 窗口里蓝色的字体改为绿色 `#2D9F4C`，与「开始分析」按钮、选择软件下拉菜单同色。
- **改动文件**：`Views/SampleImg.xaml`
- **改动位置**：第 97-101 行，`Window.Resources` 里的隐式 TextBlock 样式。
- **改动内容**：`<Style BasedOn="{StaticResource TextBlockDefaultPrimary}" TargetType="TextBlock" />` → 增加 `<Setter Property="Foreground" Value="#2D9F4C" />`。
- **影响范围**：窗口内所有继承该隐式样式的文字（「样本图片」标题、坐标显示 pos、保存路径 OutPath 等）变为绿色；显式指定 `Foreground="Black"` 的文件名文字和 `Foreground="White"` 的伤损按钮文字不受影响。
- **验证**：Visual Studio2022 编译通过，0 错误。

### 7. 界面风格：历史回放窗口（DamageFoldersList）所有蓝色按钮改绿色

- **需求**：把 `Views/DamageFoldersList.xaml` 里所有蓝色按钮改为绿色 `#2D9F4C`，与整体风格统一。
- **改动文件**：`Views/DamageFoldersList.xaml`
- **改动位置**：第 53-62 行，`Window.Resources` 里的隐式按钮样式（`BasedOn={StaticResource ButtonPrimary}`）。
- **改动内容**：新增 `<Setter Property="Background" Value="#2D9F4C" />` 和 `<Setter Property="BorderBrush" Value="#2D9F4C" />`。
- **影响范围**：窗口内所有继承该样式的按钮（首页/上一页/下一页/尾页、已选文件夹菜单、打开/导出Word报告、导出Excel、删除选中项、打开In根目录、打开Excel目录）全部变为绿色。
- **备注**：DataGrid 表头背景仍是蓝色 `#326cf3`（表头非按钮，未改），如需要可一并调整。
- **验证**：Visual Studio2022 编译通过，0 错误。

### 8. 界面风格：历史回放窗口 DataGrid 表头背景改绿色

- **需求**：历史回放窗口（DamageFoldersList）里 DataGrid 表头（列标题栏）背景原为蓝色 `#326cf3`，改为绿色与按钮统一。
- **改动文件**：`Views/DamageFoldersList.xaml`
- **改动位置**：第 37-47 行，`DataGridColumnHeader` 隐式样式。
- **改动内容**：`Background` `#326cf3` → `#2D9F4C`；`BorderBrush` `#FF01579B` → `#1B5E20`（深绿，保持表头下边框层次）。
- **验证**：Visual Studio2022 编译通过，0 错误。

### 9. 界面风格：导出Excel / 打开Excel目录两个按钮恢复蓝色

- **需求**：历史回放窗口里「导出Excel」「打开Excel目录」两个按钮不跟随绿色风格，改回原来的主题蓝。
- **改动文件**：`Views/DamageFoldersList.xaml`
- **改动位置**：第 268 行（导出Excel）、第 277-281 行（打开Excel目录）。
- **改动内容**：给这两个按钮显式加 `Background="{DynamicResource PrimaryBrush}"` 和 `BorderBrush="{DynamicResource PrimaryBrush}"`，覆盖隐式样式的绿色 `#2D9F4C`（元素级属性优先级高于样式 setter）。
- **效果**：其余按钮仍为绿色，仅这两个 Excel 相关按钮恢复蓝色。
- **验证**：Visual Studio2022 编译通过，0 错误。

### 10. 修复：导出报告显示成功但实际未导出

- **现象**：点「导出报告」弹成功提示，但 Word 文件实际没生成。
- **根因**：
  - `ExportWord.cs` 模板路径写死相对路径 `.\Resources\上海报告.docx`，但 csproj 漏配该文件的复制规则，输出目录里根本没有此文件 → `DocX.Load` 抛 FileNotFoundException。
  - `GenerateWord` 的 `catch (IOException)` 只 `Console.WriteLine` 不重新抛出 → 上层 `ExportReportAsync` 误以为成功，照弹"已保存"。
  - 顺带：`ExportExcellmentation.cs` Excel 模板路径同样指向不存在的 `.\Resources\上海表格.xlsx`（实际应有文件为 `excel模板SH.xlsx`）。
- **改动文件**：`DamageMarker.csproj`、`GenerateReport/ExportWord.cs`、`GenerateReport/ExportExcellmentation.cs`
- **改动内容**：
  1. csproj 补 `Resources\上海报告.docx` 的 `CopyToOutputDirectory=PreserveNewest` 复制规则。
  2. `ExportWord.GenerateWord`：模板路径改 `Path.Combine(AppContext.BaseDirectory, "Resources", "上海报告.docx")`；模板缺失时抛 `FileNotFoundException`（带路径信息）；`catch` 内 `throw` 重新抛出，让上层感知失败。
  3. `ExportExcellmentation` 构造函数：模板路径改为 `Path.Combine(AppContext.BaseDirectory, "Resources", "excel模板SH.xlsx")`。
- **验证**：Visual Studio2022 编译 0 错误；确认输出目录 `bin\Debug\net8.0-windows10.0.26100.0\Resources\` 已包含 `上海报告.docx`。

### 11. 修复：导出报告报告人被错误替换为「合肥平行线机器人」

- **现象**：模板「上海报告.docx」报告人写的是"张三"，导出报告却变成"合肥平行线机器人"。
- **根因**：`ExportWord.cs` 的 `RepleaceInfo` 里，`newOperatorName = NeedSavedInfo?.RailWayInfo.OperatorName ?? "合肥平行线机器人"` ——当回放人员 `OperatorName` 为空时，兜底默认值"合肥平行线机器人"把模板里的"张三"覆盖了（该默认值疑为其他项目残留）。
- **改动文件**：`GenerateReport/ExportWord.cs`
- **改动内容**：报告人替换逻辑改为——仅当 `OperatorName` 非空时才替换模板中的"张三"；为空时保留模板原样，不再用兜底默认值覆盖。
- **验证**：Visual Studio2022 编译 0 错误。

### 12. 模板命名规范统一：Word=上海报告，Excel=上海表格

- **需求**：模板命名规范——Word 导出模板叫「上海报告.docx」，Excel 导出模板叫「上海表格.xlsx」；代码引用统一指向这两个文件。
- **改动文件**：`Resources/上海表格.xlsx`（新建）、`GenerateReport/ExportExcellmentation.cs`、`DamageMarker.csproj`
- **改动内容**：
  1. 将 `Resources/excel模板SH.xlsx` 复制一份为 `Resources/上海表格.xlsx`（SH = 上海版 Excel 模板）。
  2. `ExportExcellmentation` 构造函数中 `ExcelTemplatePath` 由 `excel模板SH.xlsx` 改为 `Path.Combine(AppContext.BaseDirectory, "Resources", "上海表格.xlsx")`。
  3. csproj 新增 `Resources\上海表格.xlsx` 的 `CopyToOutputDirectory=PreserveNewest` 复制规则。
- **备注**：`excel模板SH.xlsx` 原文件仍保留未删除，可后续清理。
- **验证**：Visual Studio2022 编译 0 错误；输出目录 `bin\Debug\net8.0-windows10.0.26100.0\Resources\` 已同时包含 `上海报告.docx` 与 `上海表格.xlsx`。

### 13. 修复：导出Excel报「数据库中未找到文件夹记录」

- **现象**：历史回放勾选文件夹 → 导出Excel，报 `数据库中未找到文件夹记录: ...\ImgFile\in\1+1+2026年8月6日`。
- **根因**：历史回放列表是从 `InPath` 目录**扫描磁盘文件夹**得到的（`DamageFoldersListViewModel.cs:189`），但 `DamageFolders` 表只在**智能回放截图完成后**由 `MainWindowViewModel.cs:599` 的 `DataAccess.InsertFolderInfo` 写入。手动拷入、或从未在本系统通过智能回放打开的文件夹，数据库无记录 → `FindFolderId` 返回 -1 → 导出报错。
- **改动文件**：`GenerateReport/ExportExcellmentation.cs`
- **改动内容**：`LoadExportDataFromDatabase` 中，`FindFolderId` 查不到记录时，新增自动补录：读取该文件夹 `info.json` → `DataAccess.InsertFolderInfo` 插入 `DamageFolders` → 返回新 FolderId。补录失败（目录不存在 / 缺 info.json / 信息不完整）仍报原错误。
- **要点**：
  - `AboutJson.DeserializeJson<ScreenshotInfo>` 读 info.json；
  - `InsertTable` 对 null 参数用 `DBNull.Value` 兜底，`RailInfo` 字段与插入参数全部对得上，无崩溃风险；
  - 补录成功后 `LoadScreenshotInfo/LoadDamageInfo/LoadImageCount` 均按 FolderId 正常读取。
- **验证**：Visual Studio2022 编译 0 错误。

### 14. 修复：数据库旧结构缺列导致导出Excel/查询报错

- **现象**：调试台连续报 `SQLite Error 1: 'no such column: SelectedRailType'`（GetSelectedUpOrDownByFolderPath），且导出 Excel 补录时报 `table DamageFolders has no column named StartMileage`。
- **根因**：代码已全面使用 `DamageFolders` 新列（`StartMileage` / `EndMileage` / `SelectedRailType`，模型与插入/查询 SQL 均引用），但存量数据库 `DamageFolders` 表仍是旧结构（缺这三列），历史上没有迁移逻辑补列。
- **改动文件**：`SqliteServer/SQLHelper.cs`
- **改动内容**：
  1. 新增 `EnsureDamageFoldersNewColumns()`：用 `PRAGMA table_info` 检查，缺失时 `ALTER TABLE DamageFolders ADD COLUMN` 补 `StartMileage TEXT` / `EndMileage TEXT` / `SelectedRailType TEXT`；幂等，异常（库或表不存在）catch 后不阻断启动。
  2. 在 `SQLHelper` 构造函数末尾调用该方法 → 每次 new SQLHelper 自动迁移，覆盖所有调用点（含 `MainWindowViewModel.cs:5795` 直接 `.Connection.Open()` 的路径）。
- **验证**：
  - Visual Studio2022 编译 0 错误；
  - 用 python 对 `DB/Dm.db` 模拟迁移成功（三列加入，原列保留）；
  - 实测 `bin/Release/.../ImgFile/in/1+1+2026年8月6日/info.json` 含全部插入字段；Release 库运行新版程序后会自动加列，无需手工改库。

### 15. 删除演示用「切换主题」功能

- **需求**：删除主界面标题栏右侧演示用的「切换主题」下拉按钮（默认/黑色/紫色主题）。
- **改动文件**：
  - `Views/NonClient.xaml` —— 删掉整个 `<hc:SplitButton>「切换主题」</hc:SplitButton>` 块，并清理设计时 DataContext 与 viewmodels 命名空间引用。
  - `Views/NonClient.xaml.cs` —— 删掉 `DataContext = new NonClientViewModel();` 及对应 using。
  - `ViewModels/NonClientViewModel.cs` —— 整个文件删除（含 `ToggleTheme` 命令及 HandyControl SkinType 切换逻辑）。
- **保留**：`NonClient` 控件本身（`Views/NonClient.xaml(.cs)`）继续承载 HandyControl 窗口的标题栏区域（`MainWindow.xaml.cs:43` 的 `NonClientAreaContent = new NonClient()` 不动），只是内部不再有主题切换按钮。
- **验证**：Visual Studio2022 编译 0 错误；全项目无 `NonClientViewModel` / `ToggleTheme` 残留引用。

## 待办 / 备注

- `dotnet build` / `dotnet` CLI 无法编译此项目（依赖 COM 引用，需 .NET Framework 版 MSBuild），专门情况下需用 VS 的 MSBuild.exe。
- 解决方案 `DamageMarker.sln` 里引用了缺失的项目 `D:\DamageMakerTest\DamageMakerTest.csproj`，构建 sln 时会报错，需单独编译 `DamageMarker.csproj`。