using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DamageMarker.Views;
using DocumentFormat.OpenXml.Bibliography;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using HandyControl.Controls;

namespace DamageMaker.Automation;

public static class JGT_8C
{
	private static AutomationElement DeskTop;

	private static FlaUI.Core.AutomationElements.Window? fxWins;


	private static int count;

	static JGT_8C()
	{
		count = 0;
	}

	public static async Task<bool> IsRunning()
	{
		await Task.Run(delegate
		{
			Process process = Process.GetProcessesByName("RailTest8C").FirstOrDefault();
			if (process == null)
			{
				fxWins = null;
			}
			else
			{
				if (count++ == 0)
				{
					using UIA3Automation uIA3Automation = new UIA3Automation();
					DeskTop = uIA3Automation.GetDesktop();
				}

                //fxWins = DeskTop.FindFirstChild((ConditionFactory cf) => cf.ByClassName("SunAwtFrame").And(cf.ByName("位置查找"))).AsWindow();
                var windows = DeskTop.FindAllChildren((ConditionFactory cf) => cf.ByClassName("SunAwtFrame"));
                fxWins = windows.FirstOrDefault(w => w.Name.Contains("位置查找"))?.AsWindow();
            }

		});

		return fxWins != null;
	}

    //public static async Task LocationMileageAsync(string Mileage)
    //{
    //	string Mileage2 = Mileage;
    //	if (fxWins != null || await IsRunning())
    //	{
    //		await Task.Run(delegate
    //		{
    //			string[] source = Mileage2.Split(new string[2] { "km", "KM" }, StringSplitOptions.RemoveEmptyEntries);
    //			uint[] array = source.Select((string p) => (uint)float.Parse(p.Replace("M", "").Replace("m", ""))).ToArray();
    //			if (array.Length == 0)
    //			{
    //				MessageBox.Warning("未找到里程数");
    //			}
    //			else if (array.Length > 2)
    //			{
    //				MessageBox.Warning("里程数解析出错");
    //			}
    //			else
    //			{
    //				fxWins.Patterns.Window.Pattern.SetWindowVisualState(WindowVisualState.Normal);
    //				Rectangle boundingRectangle = fxWins.BoundingRectangle;
    //                   Thread.Sleep(200); // 增加延迟，避免操作过快
    //                   TypeEditBox(boundingRectangle, array);
    //                   // 查找并显示“GCT-8C/11 5.4 1”窗口
    //                   using (var automation = new UIA3Automation())
    //                   {
    //                       var DeskTop = automation.GetDesktop();
    //                       var gctWin = DeskTop.FindFirstChild(cf =>
    //                           cf.ByClassName("SunAwtFrame").And(cf.ByName("GCT-8C/11 5.4 1"))
    //                       )?.AsWindow();
    //                       if (gctWin != null)
    //                       {
    //                           gctWin.Patterns.Window.Pattern.SetWindowVisualState(WindowVisualState.Normal);
    //                           gctWin.Focus();
    //                       }
    //                   }
    //               }
    //		});
    //	}
    //	else
    //	{
    //		MessageBox.Show("请打开位置查找窗口！");
    //	}
    //}

    //private static void TypeEditBox(Rectangle rect, uint[] content)
    //{
    //	Point tb1 = new Point(rect.Right - (int)((double)rect.Width * 0.63), rect.Top + (int)((double)rect.Height * 0.06));
    //	Point tb2 = new Point(rect.Right - (int)((double)rect.Width * 0.42), rect.Top + (int)((double)rect.Height * 0.06));
    //	Mouse.DoubleClick(tb1);
    //	Thread.Sleep(100);
    //	Keyboard.Type(content[0].ToString());
    //	Keyboard.Press(VirtualKeyShort.TAB);
    //	Mouse.DoubleClick(tb2);
    //	if (content.Length == 2)
    //	{
    //		Thread.Sleep(100);
    //		Keyboard.Type(content[1].ToString());
    //	}
    //	Keyboard.Press(VirtualKeyShort.RETURN);
    //}
    public static async Task LocationMileageAsync(string Mileage)
    {
        string Mileage2 = Mileage;
        if (fxWins != null || await IsRunning())
        {
            await Task.Run(delegate
            {
                string[] source = Mileage2.Split(new string[2] { "km", "KM" }, StringSplitOptions.RemoveEmptyEntries);
                uint[] array = source.Select((string p) => (uint)float.Parse(p.Replace("M", "").Replace("m", ""))).ToArray();
                if (array.Length == 0)
                {
                    MessageBox.Warning("未找到里程数");
                }
                else if (array.Length > 2)
                {
                    MessageBox.Warning("里程数解析出错");
                }
                else
                {
                    fxWins.Patterns.Window.Pattern.SetWindowVisualState(WindowVisualState.Normal);
                    Rectangle boundingRectangle = fxWins.BoundingRectangle;
                    Thread.Sleep(200); // 增加延迟，避免操作过快
                    TypeEditBox(boundingRectangle, array);
                    // 查找并显示"GCT-8C/11 5.4 1"窗口
                    using (var automation = new UIA3Automation())
                    {
                        var DeskTop = automation.GetDesktop();
                        var gctWin = DeskTop.FindFirstChild(cf =>
                            cf.ByClassName("SunAwtFrame").And(cf.ByName("GCT-8C/11 5.4 1"))
                        )?.AsWindow();
                        if (gctWin != null)
                        {
                            gctWin.Patterns.Window.Pattern.SetWindowVisualState(WindowVisualState.Normal);
                            gctWin.Focus();
                        }
                    }
                }
            });
        }
        else
        {
            MessageBox.Show("请打开位置查找窗口！");
        }
    }

    private static void TypeEditBox(Rectangle rect, uint[] content)
    {
        Point tb1 = new Point(rect.Right - (int)((double)rect.Width * 0.63), rect.Top + (int)((double)rect.Height * 0.06));
        Point tb2 = new Point(rect.Right - (int)((double)rect.Width * 0.42), rect.Top + (int)((double)rect.Height * 0.06));

        // 第一个编辑框：清空并输入
        Mouse.DoubleClick(tb1);
        Thread.Sleep(100);
        // 清空内容：全选然后删除
        Keyboard.Press(VirtualKeyShort.CONTROL);
        Keyboard.Press(VirtualKeyShort.KEY_A);
        Keyboard.Release(VirtualKeyShort.KEY_A);
        Keyboard.Release(VirtualKeyShort.CONTROL);
        Thread.Sleep(50);
        Keyboard.Press(VirtualKeyShort.DELETE);
        Thread.Sleep(50);
        // 输入新内容
        Keyboard.Type(content[0].ToString());

        // 切换到第二个编辑框
        Keyboard.Press(VirtualKeyShort.TAB);
        Thread.Sleep(100);

        // 第二个编辑框：清空并输入（如果有第二个值）
        if (content.Length == 2)
        {
            Mouse.DoubleClick(tb2);
            Thread.Sleep(100);
            // 清空内容：全选然后删除
            Keyboard.Press(VirtualKeyShort.CONTROL);
            Keyboard.Press(VirtualKeyShort.KEY_A);
            Keyboard.Release(VirtualKeyShort.KEY_A);
            Keyboard.Release(VirtualKeyShort.CONTROL);
            Thread.Sleep(50);
            Keyboard.Press(VirtualKeyShort.DELETE);
            Thread.Sleep(50);
            // 输入新内容
            Keyboard.Type(content[1].ToString());
        }

        Thread.Sleep(100);
        Keyboard.Press(VirtualKeyShort.RETURN);
    }


}
