using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows;
using OpenCvSharp.Extensions;
using OpenCvSharp;
using System.Drawing;
using Rect = OpenCvSharp.Rect;


namespace DamageMaker.ImageProcessing
{
    /// <summary>
    /// 一个静态类，包含图像处理的静态方法
    /// </summary>
    public static class ImgProcessing
    {
        public static BitmapImage ResizeBitmapImage(BitmapImage bitmapImage, double scale)
        {
            TransformedBitmap transformedBitmap = new TransformedBitmap();
            transformedBitmap.BeginInit();
            transformedBitmap.Source = bitmapImage;
            transformedBitmap.Transform = new ScaleTransform(scale, 1.0);
            transformedBitmap.EndInit();

            // Convert TransformedBitmap to BitmapImage
            BitmapImage resizedBitmapImage = new BitmapImage();
            using (MemoryStream memoryStream = new MemoryStream())
            {
                BmpBitmapEncoder encoder = new BmpBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(transformedBitmap));
                encoder.Save(memoryStream);
                memoryStream.Position = 0;
                resizedBitmapImage.BeginInit();
                resizedBitmapImage.StreamSource = memoryStream;
                resizedBitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                resizedBitmapImage.EndInit();
            }
            return resizedBitmapImage;
        }

        public static BitmapImage CropBitmapImage(BitmapImage bitmapImage, int cropHeight = 30)
        {
            int originalWidth = bitmapImage.PixelWidth;
            int originalHeight = bitmapImage.PixelHeight;
            // Create a CroppedBitmap to crop the bottom part of the image
            CroppedBitmap croppedBitmap = new CroppedBitmap(bitmapImage, new Int32Rect(0, originalHeight - cropHeight, originalWidth, cropHeight));
            // Convert CroppedBitmap to BitmapImage
            BitmapImage croppedBitmapImage = new BitmapImage();
            using (MemoryStream memoryStream = new MemoryStream())
        {
                BmpBitmapEncoder encoder = new BmpBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(croppedBitmap));
                encoder.Save(memoryStream);
                memoryStream.Position = 0;
                croppedBitmapImage.BeginInit();
                croppedBitmapImage.StreamSource = memoryStream;
                croppedBitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                croppedBitmapImage.EndInit();
            }
            return croppedBitmapImage;
        }

        internal struct MatchInfo
        {
         internal  float similarity;//相似度
          internal  int Xpos;//x坐标
        }
        /// <summary>
        /// 用于计算两张图像之间的偏移量
        /// </summary>
        /// <param name="bitmap1">第一张图像</param>
        /// <param name="bitmap2">第二张图像</param>
        /// <param name="LeftContrast">布尔值，决定从第一张图像的左侧还是右侧截取区域进行匹配</param>
        /// <returns>根据匹配结果计算并返回偏移量</returns>
        public static int GetImgOffset(Bitmap bitmap1,Bitmap bitmap2,bool LeftContrast, bool showImg=false)
        {

            Mat img1 = BitmapConverter.ToMat(bitmap1);
            Mat img2 = BitmapConverter.ToMat(bitmap2);
            //获取第一张图片的宽度和第二张图片的高度
            int width = 300;
            int height = img2.Height;
            //从第一张图片中截取指定区域
            Rect roi;
            if (LeftContrast == true)
            {
                roi = new Rect(0, 0, width, height);
            }
            else
            {
                roi = new Rect(img1.Width - width, 0, width, height);
            }

            Mat img1Cropped = new Mat(img1, roi);
            // 转换为灰度图像
            Mat grayImg1Cropped = new Mat();
            Mat grayImg2 = new Mat();
            Cv2.CvtColor(img1Cropped, grayImg1Cropped, ColorConversionCodes.BGR2GRAY);
            Cv2.CvtColor(img2, grayImg2, ColorConversionCodes.BGR2GRAY);

            // 膨胀操作
            Mat kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(3, 3));
            Cv2.Dilate(grayImg1Cropped, grayImg1Cropped, kernel);
            Cv2.Dilate(grayImg2, grayImg2, kernel);


            // 模板匹配
            //使用 TemplateMatchModes.SqDiffNormed 模式，值越小越匹配。
            Mat result = new Mat();
            Cv2.MatchTemplate(grayImg2, grayImg1Cropped, result, TemplateMatchModes.SqDiffNormed);

            // 输出匹配值的统计信息
            double minVal, maxVal;
            OpenCvSharp.Point minLoc, maxLoc;
            Cv2.MinMaxLoc(result, out minVal, out maxVal, out minLoc, out maxLoc);
            Console.WriteLine($"最小匹配值: {minVal}");
            Console.WriteLine($"最大匹配值: {maxVal}");
            // 设置相似度阈值
            double threshold = minVal + (maxVal - minVal) * 0.05; // 选择一个合适的阈值

            var m = new MatchInfo();
            m.similarity = 1;
            m.Xpos = 1;
            List<int> MatchX = new ();

            // 遍历结果矩阵，找到所有匹配区域
            for (int y = 0; y < result.Rows; y++)
            {
                for (int x = 0; x < result.Cols; x++)
                {
                    if (result.At<float>(y, x) <= threshold) // SqDiff 模式下，值越小越匹配
                    {
                        // 输出匹配位置和相似度
                        Console.WriteLine($"匹配位置: ({x}, {y})");
                        Console.WriteLine($"相似度: {result.At<float>(y, x)}");
                        if (result.At<float>(y, x) < m.similarity && x != 0)
                        {
                            m.similarity = result.At<float>(y, x);
                            m.Xpos = x;
                        }
                        MatchX.Add(x);
                        //在原图上绘制匹配区域
                        if (showImg)
                        {
                            Rect matchRect = new Rect(x, y, grayImg1Cropped.Width, grayImg1Cropped.Height);
                            Cv2.Rectangle(img2, matchRect, Scalar.Red, 2);
                        }                       
                    }
                }
            }
         
          
            // 显示结果
            if (showImg)
            {
                Cv2.ImShow("Matched Image", img2);
                Cv2.WaitKey(0);
                Cv2.DestroyAllWindows();
            }
            
           
            if (MatchX.Max()-MatchX.Min()>100||m.Xpos==0)
            {
                Console.WriteLine("本次图像识别偏移量可能有误,将偏移量设置为固定值1100");
                return LeftContrast?1100: img1.Width - width - 1100;
            }
            else
            {
                if (LeftContrast)
                {
                    return m.Xpos;
                }
                else
                {
                    return img1.Width - width - m.Xpos;
                }
            }
        }
    }
}
