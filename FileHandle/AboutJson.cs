using DamageMaker.Models;
using HandyControl.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

/// <summary>
/// 序列化：将程序中的数据结构或对象转换为符合JSON格式的字符串（文本）的过程
/// 反序列化：将JSON格式的字符串解析并还原为程序中的数据结构或对象的过程
/// </summary>
namespace DamageMaker.FileHandle
{
    /// <summary>
    /// AboutJson：一个静态类，包含处理 JSON 文件的静态方法。
    /// </summary>
    public static class AboutJson
    {
        /// <summary>
        /// 检查文件是否存在，如果存在则读取文件内容并反序列化为对象。
        /// 如果文件不存在，返回类型的默认值（default(T)
        /// </summary>
        /// <typeparam name="T">泛型方法，用于从指定路径的 JSON 文件中反序列化对象</typeparam>
        /// <param name="jsonFilePath">JSON 文件的路径</param>
        /// <returns>反序列化对象</returns>
        public static T? DeserializeJson<T>(string jsonFilePath)
        {
            if (File.Exists(jsonFilePath))//File属于System.IO命名空间中的类，Exists方法用于检查文件是否存在
            {
                var jsonString = File.ReadAllText(jsonFilePath);//ReadAllText方法读取文件(jsonFilePath)内容
                return JsonSerializer.Deserialize<T>(jsonString);//.Deserialize<T>方法将读取到的文件内容(jsonString)反序列化为对象并返回
            }
            else
            {
                return default(T);
            }
        }

        public static(List<DamageData>, ScreenshotInfo?) JsonPathToData(string JsonPath)
        {
            try
            {
                //读取指定路径的 JSON 文件内容

                var SString = Path.Combine(JsonPath, "info.json");
                //调用DesserializeJson方法将 JSON 字符串反序列化为对象,并返回;(此方法已经在AboutJSon类中封装好了)
              
                
                var DamgeShots = AboutJson.DeserializeJson<ScreenshotInfo>(SString);
                //var hasThermiteWeld47 = DamgeDatas.Any(d => d.DamagePoint?.Any(p => p.Length > 4 && p[4] == 47) ?? false);
                //Console.WriteLine($"原始数据中是否存在铝热焊47: {hasThermiteWeld47}");

                var DString = Path.Combine(JsonPath, "result1.json");
                if (!File.Exists(DString))
                {
                    DString = Path.Combine(JsonPath, "result.json");
                }
                var DamgeDatas = AboutJson.DeserializeJson<List<DamageData>>(DString);
                return (DamgeDatas, DamgeShots);
            }
            catch (Exception ex)
            {
                //若读取或反序列化过程中出现异常，打印错误信息
                Console.WriteLine($"读取JSON文件错误：{ex.Message}");
                //返回一个空的DamageData列表和null的ScreenshotInfo对象
                return (new List<DamageData>(), null);
            }
        }

        public static List<Records.OcrData> LoadOcrDatas(string JsonPath)
        {
            try
            {
                var ocrPath = Path.Combine(JsonPath, "OcrResult.json");

                if (!File.Exists(ocrPath))
                {
                    Console.WriteLine("OCR 文件不存在：" + ocrPath);
                    return new List<Records.OcrData>();
                }

                var ocrDatas = DeserializeJson<List<Records.OcrData>>(ocrPath);

                if (ocrDatas == null)
                {
                    Console.WriteLine("OCR 数据反序列化失败");
                    return new List<Records.OcrData>();
                }

                return ocrDatas;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"读取 OCR 数据时出错：{ex.Message}");
                return new List<Records.OcrData>();
            }
        }




        /// <summary>
        /// 检查目录是否存在，如果存在则将对象序列化为 JSON 字符串并写入文件
        /// 如果目录不存在，使用 MessageBox 显示错误消息
        /// </summary>
        /// <typeparam name="T">泛型方法，用于将对象序列化为 JSON 并保存到指定路径</typeparam>
        /// <param name="jsonData">要序列化的对象</param>
        /// <param name="jsonPath">保存 JSON 文件的目录路径</param>
        /// <param name="jsonName">保存的 JSON 文件名</param>
        public static void SaveJson<T>(T jsonData, string jsonPath, string jsonName)
        {
            if (Directory.Exists(jsonPath))//Directory属于System.IO命名空间中的类，Exists方法用于检查目录是否存在(即已经反序列化的文件内容)
            {
                if (jsonData != null)
                {                  
                    string jsonString = JsonSerializer.Serialize(jsonData, new JsonSerializerOptions { WriteIndented = true });               
                    File.WriteAllText(Path.Combine(jsonPath, jsonName), jsonString);
                }
            }
            else
            {
                Directory.CreateDirectory(jsonPath);
                if (jsonData != null)
                {
                    string jsonString = JsonSerializer.Serialize(jsonData, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(Path.Combine(jsonPath, jsonName), jsonString);
                }
            }
        }
     }
}
