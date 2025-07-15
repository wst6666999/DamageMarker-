using DamageMarker.ViewModels;
using HandyControl.Controls;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace DamageMaker.Common
{
    public static class Utilities
    {
        /// <summary>
        /// 比较两个数组是否相等
        /// </summary>
        /// <typeparam name="T">数组的类型</typeparam>
        /// <param name="array1">比较的第一个数组</param>
        /// <param name="array2">比较的第二个数组</param>
        /// <returns>true表示相等</returns>
        public static bool AreArraysEqual<T>(T[][] array1, T[][] array2) where T : IEquatable<T>
        {
            if (array1 == null && array2 == null)
            {
                return true;
            }
            else if (array1.GetLength(0) == 0 && array2.GetLength(0) == 0)
            {
                return true;
            }
            else if ((array1.GetLength(0) != array2.GetLength(0) || array1[0].Length != array2[0].Length))
            {
                return false;
            }

            return array1.Rank == 2 && array2.Rank == 2 &&
                   Enumerable.Range(0, array1.GetLength(0))
                   .All(i => Enumerable.Range(0, array1.GetLength(1))
                   .All(j => array1[i][j].Equals(array2[i][j])));
        }

     public static  void StartProcess(string exePath, string arguments)
        {
            try
            {
                var startInfo = new ProcessStartInfo(exePath);
                //startInfo.WindowStyle = ProcessWindowStyle.Maximized;
                startInfo.WorkingDirectory = Path.GetDirectoryName(exePath);
                if (arguments != null)
                {
                    startInfo.Arguments = $"\"{arguments}\"";
                }

                var p = Process.Start(startInfo);
                p.EnableRaisingEvents = true;            
            }
            catch (Exception ex)
            {
                Growl.InfoGlobal($"无法启动应用程序: {ex.Message}");
            }
        }

     public static void StartProcess(string exePath)
        {
            StartProcess(exePath, null);
        }

        public static bool IsWindows11()
        {
            const string key = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";
            using (RegistryKey registryKey = Registry.LocalMachine.OpenSubKey(key))
            {
                if (registryKey != null)
                {
                    string currentBuild = registryKey.GetValue("CurrentBuild") as string;
                    if (int.TryParse(currentBuild, out int buildNumber))
                    {
                        // Windows 11 的内部版本号大于等于 22000
                        return buildNumber >= 22000;
                    }
                }
            }
            return false;
        }
/// <summary>
/// 判断是否是Windows 10
/// </summary>
/// <returns></returns>
        public static bool IsWindows10()
        {
            const string key = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";
            using (RegistryKey registryKey = Registry.LocalMachine.OpenSubKey(key))
            {
                if (registryKey != null)
                {
                    string currentBuild = registryKey.GetValue("CurrentBuild") as string;
                    if (int.TryParse(currentBuild, out int buildNumber))
                    {
                        // Windows 10 的内部版本号小于 22000
                        return buildNumber < 22000;
                    }
                }
            }
            return false;
        }





    }
}
