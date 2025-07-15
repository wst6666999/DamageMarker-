  using DamageMaker.Properties;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace DamageMaker.Common
{
    public static class StringExtensions
    {
        /// <summary>
        /// 将输入路径中的In替换为Out
        /// </summary>
        /// <param name="inString">输入路径</param>
        /// <returns>将in替换为out的路径</returns>
        public static string InReplaceOutString(this string inString)
        {
            return inString.Replace(Settings.Default.InPath, Settings.Default.OutPath);
        }

        public static string OutReplaceInString(this string OutString)
        {
            return OutString.Replace(Settings.Default.OutPath, Settings.Default.InPath);
        }
        public static string GetLastDirectory(this string inString)
        {
            string directory;
            if (File.Exists(inString))
            {
                directory = Path.GetDirectoryName(inString);
                directory = Path.GetFileName(directory);

            }
            else if (Directory.Exists(inString))
            {
                directory = Path.GetFileName(inString.TrimEnd(Path.DirectorySeparatorChar));
            }
            else
            {
                throw new Exception("输入路径有问题");
            }

            return directory;
        }

    }
}
