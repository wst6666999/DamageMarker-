using ClosedXML.Excel;//用于处理Excel文件的读写
using DamageMaker.Models;
/// 该命名空间应该包含DamageData类和ScreenshotInfo类
using System.IO;//用于文件和流的输出操作
using DamageMaker.Common;
using Brushes = System.Windows.Media.Brushes;
using DamageMaker.FileHandle;
using DamageMaker.DamageDataProcessing;
using System.Windows;
using DamageMarker;//用于处理Json数据的序列化和反序列化

namespace DamageMaker.GenerateReport
{
    //定义一个内部类ExportExcellmentation，实现IExportExcel接口
    public  class ExportExcellmentation 
    {
        //WriteToExcelCount用于记录写入 Excel 的次数
        //ExcelTemplate用于存储 Excel模板的范围
        //ExcelTemplatePath用户存储模版的路径
        public static int WriteToExcelCount = 1;
        public static IXLRange ExcelTemplate = null;
        String ExcelTemplatePath=string.Empty;

        public ExportExcellmentation()
        {
            //构造函数，初始化ExcelTemplatePath
                ExcelTemplatePath = @".\Resources\excel模板SH.xlsx";

        }

        /// <summary>
        /// 批量导出Excel
        /// </summary>
        /// <param name="JsonPaths">可能包含json数据的文件路径数组 </param>
        /// <param name="ExcelPath">最终Excel文件的路径</param>
        /// <returns>返回true</returns>
        /// 
        public bool BulkExportExcel(string[] JsonPaths, string ExcelPath)
        {
            try
            {
                using (var workbook = XLWorkbook.OpenFromTemplate(ExcelTemplatePath))
                {
                    var worksheet = workbook.Worksheets.First();//获取工作簿的第一个工作表
                    int rowIndex = 4;
                    foreach (var jsonPath in JsonPaths)
                    {
                        //初始化MainData并处理数据
                        var mainData =new MainData(jsonPath);
                        var damageDatas = mainData.ProcessData();//数据处理得到损失数据
                        var screenshotInfo = mainData.NeedSavedInfo;//获取轨道信息
                        int AllImgCount = mainData.AllImgCount;//获取图片总数

                        if (screenshotInfo != null && screenshotInfo.RailWayInfo.RailWayName == null)
                        {
                            //如果RailWayName为空，则将jsonPath的文件名作为RailWayName
                            screenshotInfo.RailWayInfo.RailWayName = mainData.ImgFolderName;
                        }

                       

                        WriteToExcel(damageDatas, screenshotInfo, worksheet, rowIndex);
                        rowIndex++;
                    }
                    WriteToExcelCount = 1;
                    workbook.SaveAs(ExcelPath);
                    return true;
                }
            }
            catch (Exception ex) {
                MessageBox.Show($"导出Excel文件错误：{ex.StackTrace}  {ex.Message}");
                return false;             
            }
           
                      
        }

        /// <summary>
        /// 将数据写入Excel对象
        /// <typeparam name="T"></typeparam>
        /// <param name="damageDatas">包含需要写入的损伤数据列表</param>
        /// <param name="screenshotInfo">包含可选的截图信息</param>
        /// <para name="rowIndex">起始写入的行索引</para>
        /// <para name="IXLWorksheet">起始写入的行索引</para>
        /// <returns>IXLWorksheet，写入数据后的工作表对象</returns>
        /// </summary>
        public IXLWorksheet WriteToExcel(List<DamageData> damageDatas, ScreenshotInfo? screenshotInfo, IXLWorksheet worksheet, int rowIndex)
        {
            if (WriteToExcelCount == 1)
            {
                ExcelTemplate = worksheet.Range(4, 1, 7, 32);//表示这是第一次写入数据，将第 4 行到第 7 行、第 1 列到第 31 列的范围保存为模板
            }

            if (WriteToExcelCount != 1)
            {
                //表示不是第一次写入数据，将模板复制到目标位置。目标位置是从 `WriteToExcelCount * 4` 行开始的 4 行范围
                var TargetTemplate = worksheet.Range(WriteToExcelCount * 4, 1, WriteToExcelCount * 4 + 3, 32);
                ExcelTemplate.CopyTo(TargetTemplate);
            }

            for (int i = 1; i < 12; i++)
            {
                //使用 `switch` 表达式将 `screenshotInfo` 中的信息写入工作表的相应单元格。每个 `case` 对应一个列索引，写入相应的值
                //调用screenShotInfo.RailWayInfo方法获得录入的信息
                worksheet.Cell(WriteToExcelCount * 4, i).Value = i switch
                {
                    1 => WriteToExcelCount,//序号
                    2 => screenshotInfo?.RailWayInfo.RailWayName,//铁路线路名称
                    3 => screenshotInfo?.RailWayInfo.Instruments,//检测仪器型号
                    4 => screenshotInfo?.RailWayInfo.SerialNumber,//仪器序列号
                    5 =>screenshotInfo?.RailWayInfo.WorkDate,//检测日期
                    6 => screenshotInfo?.RailWayInfo.WorkSection,//检测区段
                    7 => screenshotInfo?.RailWayInfo.WorkLength,//检测长度
                    8 => screenshotInfo?.RailWayInfo.SelectedLineType,//线路类型
                    9 => screenshotInfo?.RailWayInfo.WorkGroup,//工作组名称
                    10 => screenshotInfo?.RailWayInfo.OperatorName,//操作员姓名
                    11 =>screenshotInfo?.RailWayInfo.AnalyzeTime,//分析时间
                    _ => " "//其他列显式空值
                };
            }

            //调用 `ObtainInfo.GetCategoryAndCount` 方法获取损伤信息。
            //调用 `WriteDamageCount` 方法将损伤信息写入工作表的不同范围
            List<KeyValuePair<int, float>> damageInfo = ObtainInfo.GetCategoryAndCount(damageDatas, true);
            
            
                WriteDamageCount(damageInfo, worksheet, 12, 20, lineSpace: 2);
            
            WriteToExcelCount++;
            return worksheet;//返回写入数据后的工作表对象
        }

        /// <summary>
        /// 将损伤信息写入工作表的不同范围
        /// </summary>
        /// <param name="damageInfo">包含损伤信息的键值对列表</param>
        /// <param name="worksheet">目标 Excel 工作表对象</param>
        /// <param name="startColumn">起始列索引</param>
        /// <param name="endColumn">结束列索引</param>
        /// <param name="isTake10">是否取前 10 个损伤信息</param>
        /// <param name="lineSpace">行间距</param>
        /// <param name="columnOffset">列偏移量</param>
        private static void WriteDamageCount(List<KeyValuePair<int, float>> damageInfo, IXLWorksheet worksheet,int startColumn,int endColumn, bool isTake10=false, int lineSpace=1,int columnOffset=0)
        {
            ///<summary>
            ///逻辑：
            ///获取指定范围的单元格
            ///遍历单元格并获取单元格的列号和行号
            ///将损伤数据写入对应的单元格
            /// </summary>
            var RuleBaseMetalsDamage = worksheet.Range(WriteToExcelCount * 4+ columnOffset, startColumn, WriteToExcelCount * 4+ columnOffset, endColumn);
            foreach (var item in RuleBaseMetalsDamage.Cells())
            {
                var itemColumn = item.Address.ColumnNumber;
                var itemRow = item.Address.RowNumber;

                var id = DataConversion.DamageNameToId(item.Value.ToString());//将单元格值转换转换为ID

                damageInfo.ForEach(x =>
                {
                    if (x.Value == id)
                    {
                        var shouldWrite = x.Key >= 10 ? 10:x.Key;//损伤数据超过10时截断
                        worksheet.Cell(itemRow + lineSpace, itemColumn).Value = isTake10 ? shouldWrite : x.Key;//根据isTake10决定写入值
                    }
                });
            }
        }
    }
}
