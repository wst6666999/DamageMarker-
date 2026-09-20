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
    public class ExportWord
    {
        DocX document;//操作Word文档对象
        MainData mainData;//处理核心数据
        List<KeyValuePair<int, float>> damageCountInfo;//损伤统计信息
        List<DamageData> damageDatas;//原始损伤数据集合
        ScreenshotInfo? NeedSavedInfo;//获取检测需要的信息
        long FolderId;//文件夹ID，用于关联数据库中的图片路径

        // 记录当前导出来源，用来校验数据库图片是否属于当前这条数据。
        private readonly string _dataPath;
        private readonly string _expectedImgFolderName;
        // 报告标题强制使用外部导出文件名同一来源，避免 Word 内标题和外部文件名不一致。
        private readonly string? _reportTitleOverride;

        public ExportWord(string DataPath, string? reportTitleOverride = null)
        {
            _dataPath = DataPath;
            _reportTitleOverride = reportTitleOverride;

            mainData = new MainData(DataPath);
            damageDatas = mainData.ProcessData();//数据处理得到损伤数据
            damageCountInfo = NormalizeDamageCountForDisplay(ObtainInfo.GetCategoryAndCount(damageDatas, true));//统计损伤类型和数量，并按前台显示规则合并 id=6/id=30/id=35 到 id=5
            NeedSavedInfo = mainData.NeedSavedInfo;//获取损伤数据中所需要的信息
            FolderId = mainData.FolderId;//获取关联文件夹的ID
            _expectedImgFolderName = GetExpectedImgFolderName();

            Console.WriteLine($"[ExportWord] DataPath={_dataPath}");
            Console.WriteLine($"[ExportWord] RailWayName={NeedSavedInfo?.RailWayInfo?.RailWayName}");
            Console.WriteLine($"[ExportWord] WorkSection={NeedSavedInfo?.RailWayInfo?.WorkSection}");
            Console.WriteLine($"[ExportWord] SerialNumber={NeedSavedInfo?.RailWayInfo?.SerialNumber}");
            Console.WriteLine($"[ExportWord] WorkDate={NeedSavedInfo?.RailWayInfo?.WorkDate}");
            Console.WriteLine($"[ExportWord] FolderId={FolderId}");
            Console.WriteLine($"[ExportWord] ExpectedImgFolderName={_expectedImgFolderName}");
            Console.WriteLine($"[ExportWord] ReportTitle={GetReportTitle()}");
        }

        private string GetReportTitle()
        {
            if (!string.IsNullOrWhiteSpace(_reportTitleOverride))
            {
                return _reportTitleOverride.Trim();
            }

            if (!string.IsNullOrWhiteSpace(NeedSavedInfo?.RailWayInfo?.RailWayName))
            {
                return NeedSavedInfo.RailWayInfo.RailWayName.Trim();
            }

            if (!string.IsNullOrWhiteSpace(_expectedImgFolderName))
            {
                return _expectedImgFolderName.Trim();
            }

            return " 未输入 ";
        }

        /// <summary>
        /// 与前台 DisplayDamageId 保持一致：导出统计时把 id=6 / id=30 / id=35 统一并入 id=5（其他核伤）。
        /// </summary>
        private static int DisplayDamageIdForReport(float id)
        {
            int damageId = (int)id;
            return damageId == 6 || damageId == 30 || damageId == 35 ? 5 : damageId;
        }

        /// <summary>
        /// GetCategoryAndCount 返回格式为 Key=数量、Value=伤损id。
        /// 这里先把需要显示合并的 id 归并后再统计，保证 Word 表格里的“其他核伤(id=5)”和前台数量一致。
        /// </summary>
        private static List<KeyValuePair<int, float>> NormalizeDamageCountForDisplay(List<KeyValuePair<int, float>> rawDamageInfo)
        {
            if (rawDamageInfo == null)
            {
                return new List<KeyValuePair<int, float>>();
            }

            return rawDamageInfo
                .GroupBy(x => DisplayDamageIdForReport(x.Value))
                .Select(g => new KeyValuePair<int, float>(g.Sum(x => x.Key), g.Key))
                .ToList();
        }

        /// <summary>
        /// 加载数据和模版生成最终的Word
        /// </summary>
        /// <param name="WordPath">生成的Word报告文件的目标保存文件</param>
        public void GenerateWord(string WordPath)
        {
            try
            {
                // 用程序运行目录拼绝对路径，不依赖当前工作目录。
                string templatePath = Path.Combine(AppContext.BaseDirectory, "Resources", "上海报告.docx");
                if (!File.Exists(templatePath))
                {
                    throw new FileNotFoundException("找不到报告模板文件：" + templatePath);
                }

                document = DocX.Load(templatePath);

                var statisticalTable = document.Tables.FirstOrDefault();//获取模版的第一个表格
                FillInTable(statisticalTable);//填充统计表格
                RepleaceInfo(document);//替换
                //InsertColorChartImage(document);
                InsertImges(document);
                Console.WriteLine(document.Paragraphs);
                document.SaveAs(WordPath);
            }
            catch (IOException ex)
            {
                Console.WriteLine(ex.Message);
                throw; // 重新抛出，让上层感知失败，弹真实错误
            }
            finally
            {
                document?.Dispose();
            }
        }

        /// <summary>
        /// 插入通道颜色统计表图片
        /// </summary>
        /// <param name="document"></param>
        private void InsertColorChartImage(DocX document)
        {
            var RailwayName = NeedSavedInfo.RailWayInfo.WorkSection + "+" + NeedSavedInfo.RailWayInfo.SerialNumber + "+" + NeedSavedInfo.RailWayInfo.WorkDate;
            string currentDirectory = Path.GetFullPath(Path.Combine(Settings.Default.InPath, RailwayName));
            string imgDirectory = Path.Combine(currentDirectory, "color");
            Console.WriteLine($"图片目录:{imgDirectory}");
            string imagePath = Path.Combine(imgDirectory, "ChannelColor.png");

            if (File.Exists(imagePath))
            {
                var target = document.Paragraphs.FirstOrDefault(p => p.Text.Contains("图 1通道颜色统计表"));
                if (target != null)
                {
                    // 创建新段落并插入图片
                    var newParagraph = target.InsertParagraphAfterSelf("");
                    var img = document.AddImage(imagePath);

                    // 将厘米转换为像素（1cm ≈ 37.8像素）
                    float heightInPixels = 1.56f * 37.8f;  // 约59像素
                    float widthInPixels = 10.58f * 37.8f;  // 约400像素

                    var picture = img.CreatePicture(heightInPixels, widthInPixels);
                    newParagraph.InsertPicture(picture);
                    newParagraph.Alignment = Alignment.center;
                }
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
                NewValue = GetReportTitle(),
                SearchValue = "250420_沪蓉上下：546.588-549.538K计5.9K_002698"
            };
            document.ReplaceText(DataName);

            var instrumentModel = new StringReplaceTextOptions()
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
                // 仅当系统里配置了回放人员时才替换模板中的报告人；
                // 未配置时保留模板原样（张三），避免被无关的默认值覆盖。
                string newOperatorName = NeedSavedInfo?.RailWayInfo.OperatorName;
                if (!string.IsNullOrWhiteSpace(newOperatorName))
                {
                    para.ReplaceText("张三", newOperatorName);
                    // 设置段落右对齐
                    para.Alignment = Alignment.right;
                }
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
        /// 生成当前导出数据对应的文件夹名称，用于校验数据库里取出的图片是否属于当前线路。
        /// </summary>
        private string GetExpectedImgFolderName()
        {
            if (!string.IsNullOrWhiteSpace(mainData?.ImgFolderName))
            {
                return mainData.ImgFolderName.Trim();
            }

            try
            {
                if (File.Exists(_dataPath))
                {
                    var dir = Path.GetDirectoryName(_dataPath);
                    if (!string.IsNullOrWhiteSpace(dir))
                    {
                        return new DirectoryInfo(dir).Name;
                    }
                }

                if (Directory.Exists(_dataPath))
                {
                    return new DirectoryInfo(_dataPath).Name;
                }
            }
            catch
            {
                // 忽略路径解析异常，走下面的兜底逻辑。
            }

            var info = NeedSavedInfo?.RailWayInfo;
            string folderName = $"{info?.WorkSection}+{info?.SerialNumber}+{info?.WorkDate}";
            return folderName.Trim('+', ' ');
        }

        private static string NormalizePathLike(string value)
        {
            return (value ?? string.Empty)
                .Replace('\\', '/')
                .Trim()
                .ToLowerInvariant();
        }

        private string ResolveImgPath(SqlImgInfo info)
        {
            if (info == null || string.IsNullOrWhiteSpace(info.ImgPath))
            {
                return string.Empty;
            }

            string replacedPath = info.ImgPath.InReplaceOutString();
            if (File.Exists(replacedPath))
            {
                return replacedPath;
            }

            return info.ImgPath;
        }

        private bool IsCurrentDataImage(SqlImgInfo info)
        {
            if (string.IsNullOrWhiteSpace(_expectedImgFolderName))
            {
                // 没有可用于校验的文件夹名时，不强行过滤。
                return true;
            }

            string imgPath = ResolveImgPath(info);
            string normalizedPath = NormalizePathLike(imgPath);
            string normalizedFolderName = NormalizePathLike(_expectedImgFolderName);

            return normalizedPath.Contains(normalizedFolderName);
        }

        /// <summary>
        /// 插入当前 FolderId 下的伤损图片。
        /// 这里新增了路径校验：如果数据库查出来的图片路径不属于当前导出的文件夹，就跳过，避免标题和内容串到另一条线路。
        /// </summary>
        /// <param name="document"></param>
        private void InsertImges(DocX document)
        {
            List<SqlImgInfo> imgs;
            using (var sqlHelper = new SQLHelper(Settings.Default.SqlPath))
            {
                imgs = sqlHelper.GetDamageImgPaths(FolderId);
            }

            Console.WriteLine($"[ExportWord] 查询图片 FolderId={FolderId}, 原始图片数量={imgs?.Count ?? 0}");

            if (imgs == null)
            {
                imgs = new List<SqlImgInfo>();
            }

            var filteredImgs = imgs.Where(IsCurrentDataImage).ToList();

            if (imgs.Count > 0 && filteredImgs.Count != imgs.Count)
            {
                Console.WriteLine($"[ExportWord] 警告：数据库图片与当前导出文件夹不完全匹配。当前文件夹={_expectedImgFolderName}, 原始={imgs.Count}, 匹配={filteredImgs.Count}");

                foreach (var bad in imgs.Where(x => !IsCurrentDataImage(x)).Take(10))
                {
                    Console.WriteLine($"[ExportWord] 已跳过疑似串线图片: {ResolveImgPath(bad)}");
                }
            }

            // 只使用匹配当前文件夹的图片，避免“标题是A，图片内容是B”。
            imgs = filteredImgs;

            int count = 0;

            foreach (var Info in imgs)
            {
                string imgPath = ResolveImgPath(Info);

                if (string.IsNullOrWhiteSpace(imgPath) || !File.Exists(imgPath))
                {
                    Console.WriteLine($"[ExportWord] 图片不存在，跳过: {imgPath}");
                    continue;
                }

                using (var imgF = new Bitmap(imgPath))
                {
                    int width = imgF.Width;
                    int height = imgF.Height;

                    var img = document.AddImage(imgPath);
                    var picture = img.CreatePicture(height * 0.2f, width * 0.2f);

                    // ✅ 获取图片文件名
                    string fileName = Path.GetFileName(imgPath);

                    // ✅ 输出 “第N张图 —— 文件名”，并居中
                    document.InsertParagraph($"第 {++count} 张图 —— {fileName}")
                            .FontSize(10)
                            .Bold()
                            .Alignment = Alignment.center;

                    // ✅ 添加图片，居中
                    var p = document.InsertParagraph().AppendPicture(picture);
                    p.Alignment = Alignment.center;

                    // ✅ 添加备注
                    if (!string.IsNullOrEmpty(Info.Remark))
                    {
                        document.InsertParagraph(Info.Remark)
                                .FontSize(10)
                                .Alignment = Alignment.center;
                    }

                    // ✅ 空行
                    document.InsertParagraph();
                }
            }

            //替换文本
            var ImgCount = new StringReplaceTextOptions()
            {
                NewValue = $"共计{count}处疑似伤损点位",
                SearchValue = "共计4处疑似伤损点位"
            };
            document.ReplaceText(ImgCount);
        }
    }
}
