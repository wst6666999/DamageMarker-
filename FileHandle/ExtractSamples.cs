using DamageMaker.Common;
using DamageMaker.DamageDataProcessing;
using DamageMaker.Models;
using System;
using System.IO;
using System.Timers;
using System.Linq;
using System.Collections.Generic;
using DamageMaker.FileHandle;
using Timer = System.Timers.Timer;

namespace DamageMarker
{
    public class ExtractSamples
    {
        private readonly string rootFolderPath;
        private readonly string baseFolderPath;
        private readonly int intervalDays;
        private Timer timer;

        public ExtractSamples(string rootFolderPath, string baseFolderPath, int intervalDays)
        {
            this.rootFolderPath = rootFolderPath;
            this.baseFolderPath = baseFolderPath;
            this.intervalDays = intervalDays;
        }

        public void ProcessingSample()
        {
            if (!Directory.Exists(baseFolderPath))
            {
                Directory.CreateDirectory(baseFolderPath);
            }

            // 获取上次创建子文件夹的日期
            string lastCreatedDateFilePath = Path.Combine(baseFolderPath, "lastCreatedDate.txt");
            DateTime lastCreatedDate = GetLastCreatedDate(lastCreatedDateFilePath);

            // 计算需要创建的子文件夹
            DateTime currentDate = DateTime.Now;
            while (lastCreatedDate.AddDays(intervalDays) <= currentDate)
            {
                lastCreatedDate = lastCreatedDate == DateTime.MinValue ? currentDate : lastCreatedDate.AddDays(intervalDays);
                CreateSubFolders(lastCreatedDate);
            }

            // 保存上次创建子文件夹的日期
            File.WriteAllText(lastCreatedDateFilePath, lastCreatedDate.ToString("yyyy-MM-dd"));

            // 设置定时器，每 intervalDays 天创建一个新的子文件夹
            timer = new Timer(intervalDays * 24 * 60 * 60 * 1000); // intervalDays 天的毫秒数
            timer.Elapsed += (sender, e) => CreateNewSubFolder(lastCreatedDateFilePath);
            timer.Start();
        }

        private DateTime GetLastCreatedDate(string lastCreatedDateFilePath)
        {
            if (File.Exists(lastCreatedDateFilePath))
            {
                string lastCreatedDateString = File.ReadAllText(lastCreatedDateFilePath);
                if (DateTime.TryParse(lastCreatedDateString, out DateTime lastCreatedDate))
                {
                    return lastCreatedDate;
                }
            }
            return DateTime.MinValue;
        }

        private void CreateSubFolders(DateTime lastCreatedDate)
        {
            string subFolderName = lastCreatedDate.ToString("yyyy-MM-dd");
            string subFolderPath = Path.Combine(baseFolderPath, subFolderName);
            if (!Directory.Exists(subFolderPath))
            {
                Directory.CreateDirectory(subFolderPath);

                // 创建正常和伤损文件夹
                string normalFolderPath = Path.Combine(subFolderPath, "正常样本");
                string damageFolderPath = Path.Combine(subFolderPath, "伤损样本");
                Directory.CreateDirectory(normalFolderPath);
                Directory.CreateDirectory(damageFolderPath);

                // 获取过去 intervalDays 天内创建的子文件夹
                var subFolders = Directory.GetDirectories(rootFolderPath)
                    .Where(d => Directory.GetCreationTime(d) >= DateTime.Now.AddDays(-intervalDays))
                    .ToList();

                foreach (var subFolder in subFolders)
                {
                    // 找到 result.json 文件
                    string jsonFilePath = Path.Combine(subFolder, "result.json");
                    if (File.Exists(jsonFilePath))
                    {
                        // 调用 DeserializeJson 方法并获取结果
                        var damageDataList = AboutJson.DeserializeJson<List<DamageData>>(jsonFilePath);

                        // 在 foreach 循环中输出筛选后的内容
                        foreach (var damageData in damageDataList)
                        {
                            foreach (var points in damageData.DamagePoint)
                            {
                                string fileExtension = Path.GetExtension(damageData.Url).ToLower();
                                if (fileExtension == ".png" || fileExtension == ".jpg" || fileExtension == ".jpeg")
                                {
                                    string sourceFilePath = Path.Combine(subFolder, damageData.Url);

                                    if (points[4] == 14 || points[4] == 15 || points[4] == 16)
                                    {
                                        string destinationFilePath = Path.Combine(normalFolderPath, Path.GetFileName(damageData.Url));
                                        CopyFile(sourceFilePath, destinationFilePath);
                                    }

                                    if (points[4] == 17 || points[4] == 18 || points[4] == 19 )
                                    {
                                        string destinationFilePath = Path.Combine(damageFolderPath, Path.GetFileName(damageData.Url));
                                        CopyFile(sourceFilePath, destinationFilePath);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private void CopyFile(string sourceFilePath, string destinationFilePath)
        {
            try
            {
                File.Copy(sourceFilePath, destinationFilePath, true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error copying file {sourceFilePath}: {ex.Message}");
            }
        }

        private void CreateNewSubFolder(string lastCreatedDateFilePath)
        {
            DateTime lastCreatedDate = GetLastCreatedDate(lastCreatedDateFilePath);
            lastCreatedDate = lastCreatedDate.AddDays(intervalDays);
            CreateSubFolders(lastCreatedDate);

            // 保存上次创建子文件夹的日期
            File.WriteAllText(lastCreatedDateFilePath, lastCreatedDate.ToString("yyyy-MM-dd"));
        }
    }
}
