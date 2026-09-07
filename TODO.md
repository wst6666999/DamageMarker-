# TODO 记录

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

## 待办 / 备注

- `dotnet build` / `dotnet` CLI 无法编译此项目（依赖 COM 引用，需 .NET Framework 版 MSBuild），专门情况下需用 VS 的 MSBuild.exe。
- 解决方案 `DamageMarker.sln` 里引用了缺失的项目 `D:\DamageMakerTest\DamageMakerTest.csproj`，构建 sln 时会报错，需单独编译 `DamageMarker.csproj`。