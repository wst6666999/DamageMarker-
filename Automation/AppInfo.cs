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
            Mouse.MovePixelsPerMillisecond = 200;
            Mouse.MoveTo(new Point((int)(1800), (int)(1000)));
            Mouse.DoubleClick(FlaUI.Core.Input.MouseButton.Left);
            using (var automation = new UIA3Automation())
            {             
                var focusedElement = automation.FocusedElement();
                if (focusedElement != null)
                {
                    var app = focusedElement.Properties.ProcessId;
                    var process = System.Diagnostics.Process.GetProcessById(app);
                    Console.WriteLine($"聚焦到应用:{process.MainModule.ModuleName}");
                    return process.MainModule.ModuleName;
                }
                return string.Empty;
            }
        }
    }
}
