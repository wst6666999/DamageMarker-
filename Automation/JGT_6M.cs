using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.UIA3;
using HandyControl.Controls;

namespace DamageMaker.Automation;

public static class JGT_6M
{
	public static async Task<bool> IsRunning()
	{
		return await Task.Run(() => Process.GetProcessesByName("JGT-6M信息管理").FirstOrDefault()) != null;
	}

	public static async void LocationMileage(string Mileage)
	{
		string Mileage2 = Mileage;
		if (!(await IsRunning()))
		{
			MessageBox.Warning("JGT-6M信息管理 未运行");
			return;
		}
		await Task.Run(delegate
		{
			string[] source = Mileage2.Split(new string[2] { "km", "KM" }, StringSplitOptions.RemoveEmptyEntries);
			uint[] array = source.Select((string p) => (uint)float.Parse(p.Replace("M", "").Replace("m", ""))).ToArray();
			if (array.Length == 0)
			{
				MessageBox.Warning("未找到里程数");
			}
			else
			{
				if (array.Length <= 2)
				{
					using (UIA3Automation uIA3Automation = new UIA3Automation())
					{
						FlaUI.Core.AutomationElements.Window window = null;
						FlaUI.Core.AutomationElements.Window window2 = null;
						AutomationElement desktop = uIA3Automation.GetDesktop();
						AutomationElement[] array2 = desktop.FindAllChildren((ConditionFactory cf) => cf.ByClassName("Qt5QWindowIcon").And(cf.ByControlType(ControlType.Window)));
						if (array2 != null)
						{
							if (array2.Length == 1)
							{
								window = array2[0].AsWindow();
							}
							else
							{
								AutomationElement[] array3 = array2;
								foreach (AutomationElement automationElement in array3)
								{
									AutomationElement automationElement2 = automationElement.FindFirstChild();
									if (automationElement2.Patterns.Value.Pattern.Value == "上海皓天回放软件")
									{
										window = automationElement.AsWindow();
									}
									else if (automationElement2.Patterns.Value.Pattern.Value == "文件1位置查找")
									{
										window2 = automationElement.AsWindow();
									}
								}
							}
							if (window == null)
							{
								window = desktop.FindFirstChild((ConditionFactory cf) => cf.ByClassName("Qt5QWindowIcon")).AsWindow();
							}
							if (window == null)
							{
								MessageBox.Warning("未找到窗口");
							}
							else
							{
								if ((WindowVisualState)window.Patterns.Window.Pattern.WindowVisualState != WindowVisualState.Maximized)
								{
									window.Patterns.Window.Pattern.SetWindowVisualState(WindowVisualState.Maximized);
								}
								if (window2 == null)
								{
									window2 = OpenMenuAndGetFindWin(window, desktop);
								}
								AutomationElement[] array4 = window2.FindAllChildren((ConditionFactory cf) => cf.ByControlType(ControlType.Edit));
								AutomationElement automationElement3 = window2.FindFirstChild((ConditionFactory cf) => cf.ByControlType(ControlType.Button));
								if ((WindowVisualState)window2.Patterns.Window.Pattern.WindowVisualState != 0)
								{
									window2.Patterns.Window.Pattern.SetWindowVisualState(WindowVisualState.Normal);
								}
								window2.SetForeground();
								array4[0].DoubleClick();
								Keyboard.Type(array[0].ToString());
								if (array.Length == 2)
								{
									array4[1].DoubleClick();
									Keyboard.Type(array[1].ToString());
								}
								automationElement3.Click();
							}
						}
						else
						{
							MessageBox.Warning("未找到窗口");
						}
						return;
					}
				}
				MessageBox.Warning("里程数解析出错");
			}
		});
	}

	private static FlaUI.Core.AutomationElements.Window? OpenMenuAndGetFindWin(FlaUI.Core.AutomationElements.Window mainQtWin, AutomationElement DeskTop)
	{
		mainQtWin.Focus();//聚焦到主窗口
		mainQtWin.Patterns.Window.Pattern.SetWindowVisualState(WindowVisualState.Maximized);
		mainQtWin.SetForeground();//置顶窗口
		mainQtWin.WaitUntilClickable();//等待窗口可交互
		mainQtWin.RightClick(); // 在窗口上模拟右键点击
        AutomationElement menu = DeskTop.FindFirstChild((ConditionFactory cf) => cf.ByControlType(ControlType.Menu));
		if (menu != null)
		{
			AutomationElement items = menu.FindFirstChild((ConditionFactory cf) => cf.ByName("位置查找(P键)"));
			items.Click();
			return DeskTop.FindFirstChild((ConditionFactory cf) => cf.ByName("文件1位置查找")).AsWindow();
		}
		MessageBox.Warning("未能打开菜单");
		return null;
	}

	private static void TypeEditBox(AutomationElement FindLocatiEdit_KM, uint content)
	{
		FindLocatiEdit_KM.DoubleClick();
		Thread.Sleep(150);
		Keyboard.Type(content.ToString());
	}
}
