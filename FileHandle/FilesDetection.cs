using DamageMaker.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace DamageMaker.FileHandle
{
    /// <summary>
    /// 用于检测指定文件夹中的新文件，并在检测到新文件时启动一个外部进程
    /// </summary>
    public static class FilesDetection
    {
        /// <summary>
        /// <para name="_timer">用于定时检查新文件</para>
        /// <para name="_lastFileName">存储上一次检测到的新文件名</para>
        /// </summary> 
        private static System.Timers.Timer _timer;
        private static string _lastFileName = string.Empty;

        /// <summary>
        /// 用于启动或停止文件检测
        /// </summary>
        /// <param name="IsDetect">决定是否启动检测</param>
        /// <param name="folderPath">指定要检测的文件夹路径 </param>
        public static void DetectNewFiles(bool IsDetect, string folderPath)
        {
            if (IsDetect)
            {
                if (_timer == null)
                {
                    _timer = new System.Timers.Timer(1000); // 每秒触发一次
                    _timer.Elapsed += (sender, e) => CheckForNewFiles(folderPath);
                }
                _timer.Start();
            }
            else
            {
                _timer?.Stop();
            }
        }

        /// <summary>
        /// 用于检查指定文件夹中的新文件
        /// </summary>
        /// <param name="folderPath"></param>
        private static void CheckForNewFiles(string folderPath)
        {
            //使用 DirectoryInfo 获取文件夹信息，并按文件创建时间降序排列，获取最新的文件
            var directoryInfo = new DirectoryInfo(folderPath);
            var latestFile = directoryInfo.GetFiles().OrderByDescending(f => f.CreationTime).FirstOrDefault();

            string lastFilePrex = Path.GetFileNameWithoutExtension(latestFile.FullName);
            string _lastFilePrex = Path.GetFileNameWithoutExtension(_lastFileName);
            //如果检测到的新文件与上次检测到的文件不同，则更新 _lastFileName 并输出新文件名。
            if (latestFile != null && lastFilePrex != _lastFilePrex)
            {
                _lastFileName = latestFile.FullName;
                Console.WriteLine($"New file detected: {latestFile.Name}");
                var latestFilePath = Path.Combine(folderPath, latestFile.FullName);
                Utilities.StartProcess(@"D:\回放软件\GCT_OL_V0.2.5-8D\RailTest8D.exe", latestFilePath);//启动外部进程
            }
        }
    }
}
