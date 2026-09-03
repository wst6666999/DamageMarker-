using FlaUI.Core.Input;
using FlaUI.UIA3;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Windows.Controls;
using WinRT;

namespace DamageMaker.Automation
{
   public static class AppInfo
    {
        
        public static  string GetFocusedApplicationName()
        {
            // 保存当前鼠标位置
            var originalPosition = System.Windows.Forms.Cursor.Position;

            Mouse.MovePixelsPerMillisecond = 200;
            Mouse.MoveTo(new Point((int)(1800), (int)(1000)));
            Mouse.DoubleClick(FlaUI.Core.Input.MouseButton.Left);
            System.Threading.Thread.Sleep(300);
            // 操作完成后恢复鼠标位置
            //System.Windows.Forms.Cursor.Position = originalPosition;
            using (var automation = new UIA3Automation())
            {             
                var focusedElement = automation.FocusedElement();
                if (focusedElement != null)
                {
                    var app = focusedElement.Properties.ProcessId;
                    var process = System.Diagnostics.Process.GetProcessById(app);
                    Console.WriteLine($"聚焦到应用:{process.ProcessName}");
                    return process.ProcessName;
                }
                return string.Empty;
            }
        }
    }
}
