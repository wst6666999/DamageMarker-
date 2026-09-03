using HandyControl.Controls;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Text.RegularExpressions;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;
using BitmapDecoder = Windows.Graphics.Imaging.BitmapDecoder;
using MessageBox = HandyControl.Controls.MessageBox;
using System.Drawing.Drawing2D;

namespace DamageMaker.Common
{
    /// <summary>
    /// 一个静态类，用于处理图像裁剪和OCR（光学字符识别）操作
    /// 功能是从图像中提取里程信息
    /// </summary>
    public static class MileageRecognition
    {
        private static OcrEngine? ocr;
        private static OcrEngine? chineseOcr;
        static MileageRecognition()
        {
            var availableLanguages = OcrEngine.AvailableRecognizerLanguages;
            if (OcrEngine.IsLanguageSupported(new Language("en-US")))
            //if (OcrEngine.IsLanguageSupported(new Language("zh-Hans-CN")))
            {
                ocr = OcrEngine.TryCreateFromLanguage(new Language("en-US"  ));
            }
            else
            {
                Growl.InfoGlobal("未找到英文ocr包,当前将使用系统默认语言ocr");
                var lag = OcrEngine.AvailableRecognizerLanguages.FirstOrDefault();
                if (lag != null)
                {
                    ocr = OcrEngine.TryCreateFromLanguage(lag);
                }
                else
                {
                    ocr = null;
                    MessageBox.Error("系统没有安装任何语言包,无法识别");
                }
            }

            // 8C 的 LineType 使用独立中文 OCR，避免影响现有英文里程 OCR。
            var chineseLanguage = availableLanguages.FirstOrDefault(language =>
                language.LanguageTag.StartsWith("zh", StringComparison.OrdinalIgnoreCase));

            if (OcrEngine.IsLanguageSupported(new Language("zh-Hans-CN")))
            {
                chineseOcr = OcrEngine.TryCreateFromLanguage(new Language("zh-Hans-CN"));
            }
            else if (chineseLanguage != null)
            {
                chineseOcr = OcrEngine.TryCreateFromLanguage(chineseLanguage);
            }
            else
            {
                chineseOcr = null;
                Console.WriteLine("[8C-LineType OCR] 未找到中文OCR语言包，LineType将返回空字符串");
            }
        }

        /// <summary>
        /// 从文件路径加载图像并识别
        /// </summary>
        /// <param name="imgPath"></param>
        /// <param name="cropX"></param>
        /// <returns></returns>
        public static async Task<string> CropAndRecognizeMileage(string imgPath, float cropX)
        {
            using IRandomAccessStream stream = await FileRandomAccessStream.OpenAsync(imgPath, Windows.Storage.FileAccessMode.Read);///文件路径imgPath读取图像数据流，以只读模式打开。
            var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(stream);
            var softwareBitmap = await decoder.GetSoftwareBitmapAsync();
            uint cropHeight = (uint)(softwareBitmap.PixelHeight * cropX);
            SoftwareBitmap croppedBitmap = await CropSoftwareBitmapAsync(softwareBitmap, 0, (int)(softwareBitmap.PixelHeight - cropHeight), softwareBitmap.PixelWidth, (int)cropHeight);
            var result = await ocr.RecognizeAsync(croppedBitmap);
            return result.Text.Replace("O", "0").Replace("o", "0").Replace("I", "1").Replace(" ", "").Replace("a","6");
        }
        public static async Task<string> CropAndRecognizeMileage(Bitmap imgBitmap, float cropX)
        {
            SoftwareBitmap softwareBitmap = await ConvertToSoftwareBitmapAsync(imgBitmap);
            uint cropHeight = (uint)(softwareBitmap.PixelHeight * cropX);
            SoftwareBitmap croppedBitmap = await CropSoftwareBitmapAsync(softwareBitmap, 0, (int)(softwareBitmap.PixelHeight - cropHeight), softwareBitmap.PixelWidth, (int)cropHeight);
            //await SaveDebugImg(croppedBitmap);

            var result = await ocr.RecognizeAsync(croppedBitmap);
            return result.Text.Replace("O", "0").Replace("o", "0").Replace("I", "1").Replace(" ", "");
        }

        /// <summary>
        /// // 从Bitmap对象加载图像并按比例裁剪
        /// </summary>
        /// <param name="imgBitmap"></param>
        /// <param name="height"></param>
        /// <returns></returns>
        public static async Task<string> CropAndRecognizeMileage(Bitmap imgBitmap, uint height)
        {
            // 1. 将 System.Drawing.Bitmap 转换为 Windows.Graphics.Imaging.SoftwareBitmap（异步）
            SoftwareBitmap softwareBitmap = await ConvertToSoftwareBitmapAsync(imgBitmap);
            // 2. 计算裁剪区域高度
            uint cropHeight = height;
            // 3. 裁剪 SoftwareBitmap 图像，裁剪区域从图像底部开始，高度为 cropHeight
            SoftwareBitmap croppedBitmap = await CropSoftwareBitmapAsync(softwareBitmap, 0, (int)(softwareBitmap.PixelHeight - cropHeight), softwareBitmap.PixelWidth, (int)cropHeight);

            //  await SaveDebugImg(croppedBitmap);
            // 4. 使用 OCR 引擎识别裁剪后的图像文字（异步）
            var result = await ocr.RecognizeAsync(croppedBitmap);

            //规范格式
            return result.Text.Replace("O", "0").Replace("o", "0").Replace("I", "1").Replace(" ", "").Replace("a","6");
        }
        /// <summary>
        /// 从 Bitmap 对象中按指定矩形裁剪并进行 OCR。
        /// x/y/width/height 均为相对于当前红框截图 temporaryBitmap1 的像素坐标。
        /// </summary>
        public static async Task<string> CropAndRecognizeMileage(Bitmap imgBitmap, int x, int y, int width, int height, bool useDouble502CenterOcr = false)
        {
            // 只针对“双轨502 / 回放软件V3.0A”走中部增强 OCR。
            // 其他软件仍然走原来的矩形裁剪 + Windows OCR 逻辑，不做放大、反色、二值化。
            if (useDouble502CenterOcr)
            {
                return await CropAndRecognizeMileageForDouble502Center(imgBitmap, x, y, width, height);
            }

            SoftwareBitmap softwareBitmap = await ConvertToSoftwareBitmapAsync(imgBitmap);

            // 防止裁剪区域越界，避免 OCR 前直接抛异常。
            int safeX = Math.Max(0, x);
            int safeY = Math.Max(0, y);
            int safeWidth = Math.Min(width, softwareBitmap.PixelWidth - safeX);
            int safeHeight = Math.Min(height, softwareBitmap.PixelHeight - safeY);

            if (safeWidth <= 0 || safeHeight <= 0)
            {
                Console.WriteLine($"[OCR裁剪] 裁剪区域无效: x={x}, y={y}, w={width}, h={height}, imgW={softwareBitmap.PixelWidth}, imgH={softwareBitmap.PixelHeight}");
                return string.Empty;
            }

            Console.WriteLine($"[OCR裁剪] x={safeX}, y={safeY}, w={safeWidth}, h={safeHeight}, imgW={softwareBitmap.PixelWidth}, imgH={softwareBitmap.PixelHeight}");

            try
            {
                SoftwareBitmap croppedBitmap = await CropSoftwareBitmapAsync(softwareBitmap, safeX, safeY, safeWidth, safeHeight);
                var result = await ocr.RecognizeAsync(croppedBitmap);

                return NormalizeOcrText(result.Text);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OCR异常] 普通矩形OCR失败: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// 8C 专用：从 1874×900 临时完整截图中裁剪 LineType 中文区域并识别。
        /// x/y/width/height 均为相对于临时完整截图的像素坐标。
        /// </summary>
        public static async Task<string> CropAndRecognizeLineType(
            Bitmap imgBitmap,
            int x,
            int y,
            int width,
            int height)
        {
            if (chineseOcr == null)
            {
                Console.WriteLine("[8C-LineType OCR] 中文OCR引擎不可用");
                return string.Empty;
            }

            int safeX = Math.Max(0, x);
            int safeY = Math.Max(0, y);
            int safeWidth = Math.Min(width, imgBitmap.Width - safeX);
            int safeHeight = Math.Min(height, imgBitmap.Height - safeY);

            if (safeWidth <= 0 || safeHeight <= 0)
            {
                Console.WriteLine(
                    $"[8C-LineType OCR] 裁剪区域无效: x={x}, y={y}, w={width}, h={height}, " +
                    $"imgW={imgBitmap.Width}, imgH={imgBitmap.Height}");
                return string.Empty;
            }

            Console.WriteLine(
                $"[8C-LineType OCR] 裁剪: x={safeX}, y={safeY}, " +
                $"w={safeWidth}, h={safeHeight}");

            try
            {
                using Bitmap preparedBitmap = Prepare8CLineTypeBitmap(
                    imgBitmap,
                    safeX,
                    safeY,
                    safeWidth,
                    safeHeight);

                Save8CLineTypeDebugBitmap(preparedBitmap);

                SoftwareBitmap softwareBitmap =
                    await ConvertToSoftwareBitmapAsync(preparedBitmap);
                var result = await chineseOcr.RecognizeAsync(softwareBitmap);
                string normalized = NormalizeLineTypeText(result.Text);

                Console.WriteLine($"[8C-LineType OCR原始结果] {result.Text}");
                Console.WriteLine($"[8C-LineType OCR规范结果] {normalized}");

                return normalized;
            }
            catch (Exception ex)
            {
                // LineType OCR 失败不能中断向左/向右截图。
                Console.WriteLine($"[8C-LineType OCR异常] {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// 8C LineType 小区域预处理：裁剪后放大4倍，保持原始颜色。
        /// </summary>
        private static Bitmap Prepare8CLineTypeBitmap(
            Bitmap source,
            int x,
            int y,
            int width,
            int height)
        {
            Rectangle rect = new Rectangle(x, y, width, height);
            using Bitmap cropped = source.Clone(rect, source.PixelFormat);

            const int scale = 4;
            Bitmap enlarged = new Bitmap(cropped.Width * scale, cropped.Height * scale);

            using (Graphics g = Graphics.FromImage(enlarged))
            {
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(cropped, 0, 0, enlarged.Width, enlarged.Height);
            }

            return enlarged;
        }

        private static void Save8CLineTypeDebugBitmap(Bitmap bitmap)
        {
            try
            {
                string debugDir = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "ocr_debug_8c_line_type");
                Directory.CreateDirectory(debugDir);

                string fileName = $"{DateTime.Now:HHmmssfff}_LineType.png";
                bitmap.Save(Path.Combine(debugDir, fileName), ImageFormat.Png);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[8C-LineType OCR调试图保存失败] {ex.Message}");
            }
        }

        private static string NormalizeLineTypeText(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            // LineType 只保留汉字。
            // 空格、换行、中文/英文括号、标点、数字、字母及其他非汉字都会被剔除。
            // 例如：站线，（00），右股 -> 站线右股
            return Regex.Replace(text, @"[^\u4E00-\u9FFF]", string.Empty);
        }

        /// <summary>
        /// 双轨502专用：不再识别整条 1775 宽区域，只截取中部里程文本附近的小区域。
        /// 这样可以避免 Windows OCR 的最大图片尺寸限制，也能减少左右里程、横线、网格线干扰。
        /// 输入 x/y/width/height 仍然是蓝框在红框截图 temporaryBitmap1 内部的坐标。
        /// </summary>
        private static async Task<string> CropAndRecognizeMileageForDouble502Center(Bitmap imgBitmap, int x, int y, int width, int height)
        {
            // 只取蓝框中部附近。按当前 V3.0A 图，中间里程在蓝框中心附近。
            int centerWidth = Math.Min(300, Math.Max(1, width));
            int centerX = x + Math.Max(0, (width - centerWidth) / 2);

            // 原蓝框高 36，文字在偏下位置。这里略去掉上方横线，只保留文字区域。
            int centerY = y + Math.Min(12, Math.Max(0, height / 3));
            int centerHeight = Math.Min(26, Math.Max(1, height - (centerY - y)));

            int safeX = Math.Max(0, centerX);
            int safeY = Math.Max(0, centerY);
            int safeWidth = Math.Min(centerWidth, imgBitmap.Width - safeX);
            int safeHeight = Math.Min(centerHeight, imgBitmap.Height - safeY);

            if (safeWidth <= 0 || safeHeight <= 0)
            {
                Console.WriteLine($"[双轨502-OCR] 中部裁剪区域无效: x={centerX}, y={centerY}, w={centerWidth}, h={centerHeight}, imgW={imgBitmap.Width}, imgH={imgBitmap.Height}");
                return string.Empty;
            }

            Console.WriteLine($"[双轨502-OCR] 中部裁剪: x={safeX}, y={safeY}, w={safeWidth}, h={safeHeight}, imgW={imgBitmap.Width}, imgH={imgBitmap.Height}");

            try
            {
                using Bitmap preparedBitmap = PrepareDouble502CenterOcrBitmap(imgBitmap, safeX, safeY, safeWidth, safeHeight);
                SaveDouble502DebugBitmap(preparedBitmap, "center");

                SoftwareBitmap softwareBitmap = await ConvertToSoftwareBitmapAsync(preparedBitmap);
                var result = await ocr.RecognizeAsync(softwareBitmap);
                string normalized = NormalizeOcrText(result.Text);

                Console.WriteLine($"[双轨502-OCR原始结果] {result.Text}");
                Console.WriteLine($"[双轨502-OCR规范结果] {normalized}");

                return normalized;
            }
            catch (Exception ex)
            {
                // OCR 异常不能中断向左/向右截图流程，只返回空里程。
                Console.WriteLine($"[双轨502-OCR异常] {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// 双轨502专用预处理：中心小块放大、白字黑底反色为黑字白底。
        /// 不对其他软件使用。
        /// </summary>
        private static Bitmap PrepareDouble502CenterOcrBitmap(Bitmap source, int x, int y, int width, int height)
        {
            Rectangle rect = new Rectangle(x, y, width, height);
            using Bitmap cropped = source.Clone(rect, source.PixelFormat);

            // 中部小块宽约 300，放大 6 倍后约 1800 像素宽，不会触发 Windows OCR 最大尺寸限制。
            const int scale = 6;
            Bitmap enlarged = new Bitmap(cropped.Width * scale, cropped.Height * scale);

            using (Graphics g = Graphics.FromImage(enlarged))
            {
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(cropped, 0, 0, enlarged.Width, enlarged.Height);
            }

            // 反色灰度：白字黑底 -> 黑字白底，保留抗锯齿，比硬二值化更适合小字 OCR。
            for (int yy = 0; yy < enlarged.Height; yy++)
            {
                for (int xx = 0; xx < enlarged.Width; xx++)
                {
                    Color c = enlarged.GetPixel(xx, yy);
                    int gray = (int)(0.299 * c.R + 0.587 * c.G + 0.114 * c.B);
                    int inverted = 255 - gray;
                    enlarged.SetPixel(xx, yy, Color.FromArgb(inverted, inverted, inverted));
                }
            }

            return enlarged;
        }

        private static void SaveDouble502DebugBitmap(Bitmap bitmap, string tag)
        {
            try
            {
                string debugDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ocr_debug_502");
                Directory.CreateDirectory(debugDir);
                string fileName = $"{DateTime.Now:HHmmssfff}_{tag}.png";
                bitmap.Save(Path.Combine(debugDir, fileName), ImageFormat.Png);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[双轨502-OCR调试图保存失败] {ex.Message}");
            }
        }

        private static string NormalizeOcrText(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            return text
                .Replace("O", "0")
                .Replace("o", "0")
                .Replace("I", "1")
                .Replace("l", "1")
                .Replace(" ", "")
                .Replace("\r", "")
                .Replace("\n", "")
                .Replace("\t", "")
                .Replace("a", "6")
                .Replace("，", ".")
                .Replace(",", ".");
        }
        private static async Task SaveDebugImg(SoftwareBitmap img)
        {
            using (var fileStream = new FileStream(@"C:\Users\19233\Pictures\" + Path.GetRandomFileName() + ".png", FileMode.Create))
            {
                Windows.Graphics.Imaging.BitmapEncoder encoder = await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId, fileStream.AsRandomAccessStream());
                encoder.SetSoftwareBitmap(img);
                await encoder.FlushAsync();
            }
        }
        public static string? ExtractDistance(this string input, string AppName)
        {
            if (string.IsNullOrEmpty(input) || input.Length < 8) // 最小长度检查
                return null;

            if (AppName.Contains("CTKJ"))
            {
                // 安全查找加号位置
                int plusPos = input.IndexOf('+', Math.Min(4, input.Length - 1));
                if (plusPos < 4 || plusPos > input.Length - 4) // 确保前后有足够字符
                    return null;

                // 安全截取子串
                string kmPart = input.SafeSubstring(plusPos - 4, 4); // km前4位
                string mPart = input.SafeSubstring(plusPos + 1, 3);  // m前三位

                // 验证是否为纯数字
                if (!IsAllDigits(kmPart) || !IsAllDigits(mPart))
                    return null;

                return $"{kmPart}KM{mPart}M";
            }
            else if (AppName.Contains("大仪器"))
            {
                // 匹配 XXX.XXXKm 格式
                Match match = Regex.Match(input, @"(\d+)\.(\d{3})Km");
                if (match.Success && match.Groups.Count >= 3)
                {
                    string kmPart = match.Groups[1].Value;
                    string mPart = match.Groups[2].Value;
                    return $"{kmPart}KM{mPart}M";
                }
                return null;
            }
            else if (AppName.Contains("回放软件V3.0A") || AppName.Contains("V3.0A"))
            {
                // 双轨502 / V3.0A：界面显示为 913.533km 这种格式。
                Match match = Regex.Match(input, @"(\d+)\.(\d{3})[Kk][Mm]", RegexOptions.IgnoreCase);
                if (match.Success && match.Groups.Count >= 3)
                {
                    string kmPart = match.Groups[1].Value;
                    string mPart = match.Groups[2].Value;
                    return $"{kmPart}KM{mPart}M";
                }

                // 兜底：OCR 有时会漏掉小数点，把 913.533km 识别成 913533km。
                match = Regex.Match(input, @"(\d{3,4})(\d{3})[Kk][Mm]", RegexOptions.IgnoreCase);
                if (match.Success && match.Groups.Count >= 3)
                {
                    string kmPart = match.Groups[1].Value;
                    string mPart = match.Groups[2].Value;
                    return $"{kmPart}KM{mPart}M";
                }

                return null;
            }
            else if (AppName.Contains("DL"))
            {
                // 使用正则表达式查找第一个匹配的 XXX.XXXKm 格式
                Match match = Regex.Match(input, @"(\d+)\.(\d{3})Km", RegexOptions.IgnoreCase);

                if (match.Success)
                {
                    string kmPart = match.Groups[1].Value;  // 公里部分
                    string mPart = match.Groups[2].Value;   // 米部分（小数点后三位）
                    return $"{kmPart}KM{mPart}M";
                }
                return null;
            }
            else if (AppName.Contains("GT2PlusReplayer") || AppName.Contains("replayer"))
            {
                // 模式1：标准时间格式 2025-10-1702:53:036km31m
                string pattern1 = @"\d{4}-\d{2}-\d{2}\d{2}:\d{2}:\d{2}(\d+)[Kk][Mm]\s*(\d+)[Mm]";
                Match match = Regex.Match(input, pattern1);
                if (match.Success && match.Groups.Count == 3)
                {
                    return $"{match.Groups[1].Value}KM{match.Groups[2].Value}M";
                }

                // 模式2：时间无冒号 2025-10-170253036km31m
                string pattern2 = @"\d{4}-\d{2}-\d{2}\d{6}(\d+)[Kk][Mm]\s*(\d+)[Mm]";
                match = Regex.Match(input, pattern2);
                if (match.Success && match.Groups.Count == 3)
                {
                    return $"{match.Groups[1].Value}KM{match.Groups[2].Value}M";
                }

                // 模式3：小时和分钟之间有冒号 2025-10-1702:53036km31m
                string pattern3 = @"\d{4}-\d{2}-\d{2}\d{2}:\d{4}(\d+)[Kk][Mm]\s*(\d+)[Mm]";
                match = Regex.Match(input, pattern3);
                if (match.Success && match.Groups.Count == 3)
                {
                    return $"{match.Groups[1].Value}KM{match.Groups[2].Value}M";
                }

                // 模式4：分钟和秒之间有冒号 2025-10-170253:036km31m
                string pattern4 = @"\d{4}-\d{2}-\d{2}\d{4}:\d{2}(\d+)[Kk][Mm]\s*(\d+)[Mm]";
                match = Regex.Match(input, pattern4);
                if (match.Success && match.Groups.Count == 3)
                {
                    return $"{match.Groups[1].Value}KM{match.Groups[2].Value}M";
                }

                return null;
            }
            else if(AppName.Contains("EGT-60"))
            {
                string pattern = @"\d{2}-\d{2}-\d{4}:\d{2}(\d+)[Kk][Mm][\+\-](\d+)[Mm]";
                var matches = Regex.Matches(input, pattern);
                foreach (Match match in matches)
                {
                    if (match.Success && match.Groups.Count == 3)
                    {
                        string kilometers = match.Groups[1].Value;
                        string meters = match.Groups[2].Value;
                        return $"{kilometers}KM{meters}M";
                    }
                }
                return null;
            }
            else
            {
                // 原有正则逻辑保持不变
                string pattern = @"(?i)(\d+)km\+?(\d+\.?\d+)m";
                Match match = Regex.Match(input, pattern);
                if (match.Success && match.Groups.Count >= 3)
                {
                    string beforeKm = match.Groups[1].Value;
                    string afterKm = match.Groups[2].Value;
                    return $"{beforeKm}KM{afterKm}M";
                }
                return null;
            }
        }
        public static string? ExtractVelocity(this string input, string AppName)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            var matches = Regex.Matches(input, @"(\d\.\d)(?=kmph)", RegexOptions.IgnoreCase);
            var results = new List<string>();

            foreach (Match match in matches)
            {
                if (match.Success)
                    results.Add(match.Groups[1].Value);
            }

            // 用逗号连接所有速度，返回字符串
            return string.Join(",", results);
        }






        // 辅助方法：安全子串截取
        private static string SafeSubstring(this string str, int start, int length)
        {
            start = Math.Max(0, start);
            length = Math.Min(length, str.Length - start);
            return str.Substring(start, length);
        }

        // 辅助方法：检查纯数字
        private static bool IsAllDigits(string s)
        {
            return !string.IsNullOrEmpty(s) && s.All(char.IsDigit);
        }



        public static async Task<SoftwareBitmap> ConvertToSoftwareBitmapAsync(Bitmap bitmap)
        {
            // 创建一个内存流来保存 Bitmap 数据
            using (var memoryStream = new System.IO.MemoryStream())
            {
                // 将 Bitmap 保存为 PNG 格式到内存流中
                bitmap.Save(memoryStream, ImageFormat.Png);
                memoryStream.Seek(0, System.IO.SeekOrigin.Begin);

                // 创建一个 InMemoryRandomAccessStream
                using (var randomAccessStream = new InMemoryRandomAccessStream())
                {
                    // 将内存流中的数据写入 InMemoryRandomAccessStream
                    using (var outputStream = randomAccessStream.GetOutputStreamAt(0))
                    {
                        var dataWriter = new DataWriter(outputStream);
                        var bytes = new byte[memoryStream.Length];
                        memoryStream.Read(bytes, 0, bytes.Length);
                        dataWriter.WriteBytes(bytes);
                        await dataWriter.StoreAsync();
                        await dataWriter.FlushAsync();
                    }

                    // 使用 BitmapDecoder 解码流中的图像
                    var decoder = await BitmapDecoder.CreateAsync(randomAccessStream);
                    var softwareBitmap = await decoder.GetSoftwareBitmapAsync();

                    return softwareBitmap;
                }
            }
        }


        private static async Task<SoftwareBitmap> CropSoftwareBitmapAsync(SoftwareBitmap sourceBitmap, int x, int y, int width, int height)
        {
            // 确保裁剪区域在源图像范围内
            if (x < 0 || y < 0 || width <= 0 || height <= 0 ||
                x + width > sourceBitmap.PixelWidth || y + height > sourceBitmap.PixelHeight)
            {
                throw new ArgumentException("裁剪区域超出图像范围");
            }
            // 创建一个内存流
            using (InMemoryRandomAccessStream stream = new InMemoryRandomAccessStream())
            {
                // 创建一个 BitmapEncoder 并将 SoftwareBitmap 写入流
                Windows.Graphics.Imaging.BitmapEncoder encoder = await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(Windows.Graphics.Imaging.BitmapEncoder.BmpEncoderId, stream);
                encoder.SetSoftwareBitmap(sourceBitmap);
                await encoder.FlushAsync();
                // 创建一个 BitmapDecoder 从流中读取图像
                Windows.Graphics.Imaging.BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
                // 创建一个新的 SoftwareBitmap 对象，用于存储裁剪后的图像
                SoftwareBitmap croppedBitmap = new SoftwareBitmap(
                    BitmapPixelFormat.Bgra8,
                    width,
                    height,
                    BitmapAlphaMode.Premultiplied);
                // 裁剪图像
                BitmapTransform transform = new BitmapTransform
                {
                    Bounds = new BitmapBounds
                    {
                        X = (uint)x,
                        Y = (uint)y,
                        Width = (uint)width,
                        Height = (uint)height
                    },
                    InterpolationMode = BitmapInterpolationMode.Fant
                };
                // 获取裁剪后的像素数据
                PixelDataProvider pixelData = await decoder.GetPixelDataAsync(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Premultiplied,
                    transform,
                    ExifOrientationMode.IgnoreExifOrientation,
                    ColorManagementMode.DoNotColorManage);
                // 将裁剪后的像素数据写入新的 SoftwareBitmap 对象
                croppedBitmap.CopyFromBuffer(pixelData.DetachPixelData().AsBuffer());
                return croppedBitmap;
            }
        }

    }
}
