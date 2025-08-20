using DamageMaker.Common;
using DamageMaker.DamageDataProcessing;
using DamageMaker.Models;
using DamageMaker.Properties;
using DamageMaker.SqliteServer;
using DamageMaker.ViewModels;
using DamageMaker.Views;
using DamageMarker;
using DamageMarker.ViewModels;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Math;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Search;
using Xceed.Document.NET;
using Xceed.Words.NET;
using Alignment = Xceed.Document.NET.Alignment;
using Table = Xceed.Document.NET.Table;

namespace DamageMaker.GenerateReport
{
    public  class ExportWord
    {
        DocX document;//操作Word文档对象
        MainData mainData;//处理核心数据
        List<KeyValuePair<int, float>> damageCountInfo;//损伤统计信息
        List<DamageData> damageDatas;//原始损伤数据集合
        ScreenshotInfo ?NeedSavedInfo;//获取检测需要的信息
        long FolderId;//文件夹ID，用于关联数据库中的图片路径

        public ExportWord(string DataPath) {

          mainData = new MainData(DataPath);
          damageDatas = mainData.ProcessData();//数据处理得到损伤数据
          damageCountInfo = ObtainInfo.GetCategoryAndCount(damageDatas, true);//统计损伤类型和数量
          NeedSavedInfo = mainData.NeedSavedInfo;//获取损伤数据中所需要的信息
          FolderId = mainData.FolderId;//获取关联文件夹的ID
        }

        /// <summary>
        /// 加载数据和模版生成最终的Word
        /// </summary>
        /// <param name="WordPath">生成的Word报告文件的目标保存文件</param>
        public  void GenerateWord(string WordPath)
        {
            try
            {
                document = DocX.Load(".\\Resources\\上海报告.docx");

                var statisticalTable = document.Tables.FirstOrDefault();//获取模版的第一个表格
                FillInTable(statisticalTable);//填充统计表格
                RepleaceInfo(document);//替换
                InsertImges(document);
                Console.WriteLine(document.Paragraphs);
                document.SaveAs(WordPath);
            }
            catch (IOException ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                document.Dispose();
            }
        }

        /// <summary>
        /// 将文档中的占位文本内容替换成数据
        /// </summary>
        /// <param name="document"></param>
        private void RepleaceInfo(DocX document)
        {
            var DataName = new StringReplaceTextOptions()
            {
                NewValue = NeedSavedInfo?.RailWayInfo.RailWayName??" 未输入 ",
                SearchValue = "250420_沪蓉上下：546.588-549.538K计5.9K_002698"
            };
            document.ReplaceText(DataName);

            var  instrumentModel = new StringReplaceTextOptions()
            {
                NewValue = NeedSavedInfo?.RailWayInfo.Instruments ?? " 未输入 ",
                SearchValue = "8C"
            };
            document.ReplaceText(instrumentModel);

            var WorkDate = new StringReplaceTextOptions()
            {
                NewValue = NeedSavedInfo?.RailWayInfo.WorkDate ?? " 未输入 ",
                SearchValue = "2025年4月20日",
            };
            document.ReplaceText(WorkDate);

            var WorkLength = new StringReplaceTextOptions()
            {
                NewValue = NeedSavedInfo?.RailWayInfo.WorkLength ?? " 未输入 ",
                SearchValue = "6011"
            };
            document.ReplaceText(WorkLength);

            var ElapsedTimeForScrrnshot = new StringReplaceTextOptions()
            {
                NewValue = NeedSavedInfo?.RailWayInfo.ElapsedTimeForScrrnshot ?? " 未输入 ",
                SearchValue = "3分53秒"
            };
            document.ReplaceText(ElapsedTimeForScrrnshot);

            var ElapsedTimeForAnalyze = new StringReplaceTextOptions()
            {
                NewValue = NeedSavedInfo?.RailWayInfo.ElapsedTimeForAnalyze ?? " 未输入 ",
                SearchValue = "1分15秒"
            };
            document.ReplaceText(ElapsedTimeForAnalyze);

            var para = document.Paragraphs.FirstOrDefault(p => p.Text.Contains("张三"));
            if (para != null)
            {
                // 替换“张三”为“合肥平行线机器人”（或RailWayInfo.OperatorName）
                string newOperatorName = NeedSavedInfo?.RailWayInfo.OperatorName ?? "合肥平行线机器人";
                para.ReplaceText("张三", newOperatorName);

                // 设置段落右对齐
                para.Alignment = Alignment.right;
            }

            //替换报告时间
            string searchDate = "2025/5/28";
            string currentDate = DateTime.Now.ToString("yyyy/M/d");

            document.ReplaceText(searchDate, currentDate);
        }

        /// <summary>
        /// 将损伤信息填入表格中
        /// </summary>
        /// <param name="statisticalTable">表示填充数据的表格</param>
        private void FillInTable(Table statisticalTable)
        {
            if (statisticalTable == null)
            {
                Console.WriteLine("没有找到统计表格");
                return;
            }
            for (int j = 1; j < statisticalTable.ColumnCount; j++)
            {
                var DamageName = statisticalTable.Rows[0].Cells[j].Paragraphs.FirstOrDefault()?.Text;//获取表格第0行第j列的文本内容为损坏名称
                var DamageId = DataConversion.DamageNameToId(DamageName);//将损坏名称转换为对应的损坏ID
                var DamageCount = damageCountInfo.Where(x => x.Value == DamageId).Select(x => x.Key).FirstOrDefault();//统计损伤数量
                int value = SetupContentViewModel.Instance.DisplayedRulesCount;
                int FinalCount;
                
                FinalCount = DamageCount;
                
                statisticalTable.Rows[1].Cells[j].Paragraphs.FirstOrDefault().Append(FinalCount.ToString());//将统计数量转换成字符串添加到表格中
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="document"></param>
        private void InsertImges(DocX document)
        {
            List<SqlImgInfo> imgs;
            using (var sqlHelper = new SQLHelper(Settings.Default.SqlPath))
            {
                imgs= sqlHelper.GetDamageImgPaths(FolderId);              
            }
            int count = 0;

            foreach (var Info in imgs)
            {
                string imgPath ;
                if (File.Exists(Info.ImgPath.InReplaceOutString()))
                {
                     imgPath = Info.ImgPath.InReplaceOutString();
                }
                else
                {
                    imgPath = Info.ImgPath;
                }
                var imgF = new Bitmap(imgPath);
                int width = imgF.Width;
                int height = imgF.Height;
                var img = document.AddImage(imgPath);
                var picture = img.CreatePicture(height * 0.2f, width * 0.2f);
                document.Paragraphs.LastOrDefault().AppendLine("第" + ++count + "张图").AppendLine().FontSize(10).Alignment = Alignment.left;
                var p=document.Paragraphs.LastOrDefault().AppendPicture(picture).AppendLine("");
                p.Append(Info.Remark).FontSize(10).AppendLine().Alignment=Alignment.center;
                p.AppendLine();
                imgF.Dispose();
            }

            var ImgCount = new StringReplaceTextOptions()
            {
                NewValue = $"共计{count}处疑似伤损点位",
                SearchValue = "共计4处疑似伤损点位"
            };
            document.ReplaceText(ImgCount);
        }


    }
}
