using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Tesseract;

namespace DamageMaker.ImageProcessing
{
    public static class TesseractOCR
    {

        public static string PerformOCR(BitmapImage bitmapImage)
        {
            // 将 BitmapImage 转换为 Tesseract 可识别的格式https://github.com/charlesw/tesseract/wiki/Error-1 
            // 将 BitmapImage 转换为 Tesseract 可识别的格式
            using (MemoryStream memoryStream = new MemoryStream())
            {
                BitmapEncoder encoder = new BmpBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmapImage));
                encoder.Save(memoryStream);
                memoryStream.Position = 0;

                // 将调整后的 BitmapImage 转换为 MemoryStream
                memoryStream.SetLength(0);
                encoder = new BmpBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmapImage));
                encoder.Save(memoryStream);
                memoryStream.Position = 0;

                using (var engine = new TesseractEngine(@"D:\Code\Csharp\OcrTest\tessdata", "eng", EngineMode.TesseractAndLstm))
                {
                    engine.SetVariable("tessedit_char_whitelist", "0123456789kmKM/HF+:.");
                    engine.SetVariable("tessedit_pageseg_mode", "6"); 
                    using (var img = Pix.LoadFromMemory(memoryStream.ToArray()))
                    {
                        using (var page = engine.Process(img))
                        {
                          
                            return page.GetText();
                        }
                    }
                }
            }
        }

    }
}
