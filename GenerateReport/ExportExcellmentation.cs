using System;
using System.Collections.Generic;
using System.IO; // 用于文件和流的输出操作
using System.Linq;
using System.Windows;
using ClosedXML.Excel; // 用于处理 Excel 文件的读写
using DamageMaker.Models;
using DamageMaker.Common;
using DamageMaker.FileHandle;
using DamageMaker.DamageDataProcessing;
using DamageMaker.Properties;
using DamageMaker.SqliteServer;
using DamageMarker; // 用于处理 Json 数据的序列化和反序列化
using Microsoft.Data.Sqlite;

namespace DamageMaker.GenerateReport
{
    /// <summary>
    /// Excel 批量导出。
    /// 新模板结构：第 1 行为表头，第 2 行开始一行一个文件夹。
    /// </summary>
    public class ExportExcellmentation
    {
        /// <summary>
        /// 当前写入序号，从 1 开始。
        /// </summary>
        public static int WriteToExcelCount = 1;

        /// <summary>
        /// 保留旧字段，避免外部代码引用时报错。
        /// 新模板不再使用 4 行模板块。
        /// </summary>
        public static IXLRange ExcelTemplate = null;

        private string ExcelTemplatePath = string.Empty;

        private sealed class DatabaseExportData
        {
            public ScreenshotInfo ScreenshotInfo { get; set; } = new ScreenshotInfo();
            public List<KeyValuePair<int, float>> DamageInfo { get; set; } = new();
            public int ImageCount { get; set; }
        }

        public ExportExcellmentation()
        {
            // 新 Excel 模板路径。
            // 如果你实际仍然把新模板命名为“上海表格.xlsx”，只需要改这一行。
            ExcelTemplatePath = @".\Resources\上海表格.xlsx";
        }

        /// <summary>
        /// 批量导出 Excel。
        /// </summary>
        /// <param name="JsonPaths">用于定位数据库 DamageFolders 记录的文件夹路径数组。</param>
        /// <param name="ExcelPath">最终 Excel 文件保存路径。</param>
        /// <returns>导出成功返回 true，失败返回 false。</returns>
        public bool BulkExportExcel(string[] JsonPaths, string ExcelPath)
        {
            try
            {
                WriteToExcelCount = 1;

                using (var sqlHelper = new SQLHelper(Settings.Default.SqlPath))
                using (var workbook = XLWorkbook.OpenFromTemplate(ExcelTemplatePath))
                {
                    var worksheet = workbook.Worksheets.First();

                    // 新模板：第 1 行是表头，第 2 行开始写数据。
                    int rowIndex = 2;

                    foreach (var jsonPath in JsonPaths)
                    {
                        var databaseData = LoadExportDataFromDatabase(sqlHelper, jsonPath);
                        WriteToExcel(
                            databaseData.DamageInfo,
                            databaseData.ScreenshotInfo,
                            worksheet,
                            rowIndex,
                            databaseData.ImageCount);

                        rowIndex++;
                        WriteToExcelCount++;
                    }

                    // 统一设置导出区域样式：字号 16，内容水平/垂直居中。
                    ApplyGlobalExportStyle(worksheet, rowIndex - 1);

                    workbook.SaveAs(ExcelPath);
                    return true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出Excel文件错误：{ex.StackTrace}  {ex.Message}");
                return false;
            }
            finally
            {
                WriteToExcelCount = 1;
            }
        }

        /// <summary>
        /// 兼容旧调用方式。
        /// 如果外部代码仍然调用这个方法，因为没有传入路径，所以抓取总数写 0。
        /// </summary>
        public IXLWorksheet WriteToExcel(List<DamageData> damageDatas, ScreenshotInfo? screenshotInfo, IXLWorksheet worksheet, int rowIndex)
        {
            return WriteToExcel(damageDatas, screenshotInfo, worksheet, rowIndex, pngCount: 0);
        }

        /// <summary>
        /// 将一个文件夹的数据写入 Excel 一行。
        /// </summary>
        /// <param name="damageDatas">伤损数据。</param>
        /// <param name="screenshotInfo">线路、仪器、日期等信息。</param>
        /// <param name="worksheet">目标工作表。</param>
        /// <param name="rowIndex">写入行，从第 2 行开始。</param>
        /// <param name="pngCount">抓取总数，当前原图目录下 png 数量。</param>
        /// <returns>写入后的工作表对象。</returns>
        private IXLWorksheet WriteToExcel(List<DamageData> damageDatas, ScreenshotInfo? screenshotInfo, IXLWorksheet worksheet, int rowIndex, int pngCount)
        {
            var damageInfo = ObtainInfo.GetCategoryAndCount(damageDatas, true);
            return WriteToExcel(damageInfo, screenshotInfo, worksheet, rowIndex, pngCount);
        }

        /// <summary>
        /// 将数据库汇总结果写入 Excel 一行。
        /// </summary>
        private IXLWorksheet WriteToExcel(
            List<KeyValuePair<int, float>> damageInfo,
            ScreenshotInfo? screenshotInfo,
            IXLWorksheet worksheet,
            int rowIndex,
            int pngCount)
        {
            CopyDataRowStyle(worksheet, rowIndex);

            var railWayInfo = screenshotInfo?.RailWayInfo;

            string instruments = railWayInfo?.Instruments ?? "";
            string workSection = railWayInfo?.WorkSection ?? "";
            string serialNumber = railWayInfo?.SerialNumber ?? "";
            string workDate = railWayInfo?.WorkDate ?? "";
            string workLength = railWayInfo?.WorkLength ?? "";
            string elapsedTimeForScreenshot = railWayInfo?.ElapsedTimeForScrrnshot ?? "";
            string selectedRouteLine = railWayInfo?.SelectedRouteLine ?? "";
            string selectedUpOrDown = railWayInfo?.SelectedUpOrDown ?? "";
            string selectedRailType = railWayInfo?.SelectedRailType ?? "";

            // 文件名列：作业区间 + 串号 + 作业日期。
            // 按你原来的文件夹命名习惯，中间用 + 号连接。
            string fileName = string.Join("+", new[] { workSection, serialNumber, workDate }.Where(x => !string.IsNullOrWhiteSpace(x)));

            // 新模板伤损列对应关系：
            // 其他核伤 = 5
            // 有焊缝标记的核伤 = 52
            // 螺孔裂纹 = 7
            // 零度异常 = 8
            // 轨腰裂纹 = 9
            // 轨面剥离 = 13
            // 鱼鳞伤 = 36 + 23
            // 水平裂纹 = 29
            // 轨底裂纹 = 37
            int otherNuclearDamage = GetDamageCount(damageInfo, 5);
            int weldMarkedNuclearDamage = GetDamageCount(damageInfo, 52);
            int boltHoleCrack = GetDamageCount(damageInfo, 7);
            int zeroDegreeAbnormal = GetDamageCount(damageInfo, 8);
            int railWaistCrack = GetDamageCount(damageInfo, 9);
            int railSurfaceSpalling = GetDamageCount(damageInfo, 13);
            int fishScaleDamage = GetDamageCount(damageInfo, 36, 23);
            int horizontalCrack = GetDamageCount(damageInfo, 29);
            int railBottomCrack = GetDamageCount(damageInfo, 37);

            // 疑似伤损总数：前面所有伤损列求和。
            int suspectedDamageTotal = otherNuclearDamage
                                     + weldMarkedNuclearDamage
                                     + boltHoleCrack
                                     + zeroDegreeAbnormal
                                     + railWaistCrack
                                     + railSurfaceSpalling
                                     + fishScaleDamage
                                     + horizontalCrack
                                     + railBottomCrack;

            // A:V 共 22 列。
            worksheet.Cell(rowIndex, 1).Value = WriteToExcelCount;              // 序号
            worksheet.Cell(rowIndex, 2).Value = instruments;                    // 仪器类型
            worksheet.Cell(rowIndex, 3).Value = fileName;                       // 文件名（作业区间+串号+作业日期）
            worksheet.Cell(rowIndex, 4).Value = workSection;                    // 作业区间
            worksheet.Cell(rowIndex, 5).Value = serialNumber;                   // 串号
            worksheet.Cell(rowIndex, 6).Value = workDate;                       // 作业日期
            worksheet.Cell(rowIndex, 7).Value = workLength;                     // 作业长度
            worksheet.Cell(rowIndex, 8).Value = elapsedTimeForScreenshot;       // 数据分析时间：ElapsedTimeForScrrnshot
            worksheet.Cell(rowIndex, 9).Value = selectedRouteLine;              // 线别
            worksheet.Cell(rowIndex, 10).Value = selectedUpOrDown;              // 行别
            worksheet.Cell(rowIndex, 11).Value = selectedRailType;              // 股别
            worksheet.Cell(rowIndex, 12).Value = otherNuclearDamage;            // 其他核伤
            worksheet.Cell(rowIndex, 13).Value = weldMarkedNuclearDamage;       // 有焊缝标记的核伤
            worksheet.Cell(rowIndex, 14).Value = boltHoleCrack;                 // 螺孔裂纹
            worksheet.Cell(rowIndex, 15).Value = zeroDegreeAbnormal;            // 零度异常
            worksheet.Cell(rowIndex, 16).Value = railWaistCrack;                // 轨腰裂纹
            worksheet.Cell(rowIndex, 17).Value = railSurfaceSpalling;           // 轨面剥离
            worksheet.Cell(rowIndex, 18).Value = fishScaleDamage;               // 鱼鳞伤 = 36 + 23
            worksheet.Cell(rowIndex, 19).Value = horizontalCrack;               // 水平裂纹
            worksheet.Cell(rowIndex, 20).Value = railBottomCrack;               // 轨底裂纹
            worksheet.Cell(rowIndex, 21).Value = suspectedDamageTotal;          // 疑似伤损总数
            worksheet.Cell(rowIndex, 22).Value = pngCount;                      // 抓取总数

            return worksheet;
        }

        private static DatabaseExportData LoadExportDataFromDatabase(
            SQLHelper sqlHelper,
            string inputPath)
        {
            string folderPath = ResolveFolderPath(inputPath);
            int folderId = FindFolderId(sqlHelper, folderPath);

            if (folderId < 0)
            {
                throw new InvalidOperationException(
                    $"数据库中未找到文件夹记录：{folderPath}");
            }

            sqlHelper.EnsureConnectionOpen();

            return new DatabaseExportData
            {
                ScreenshotInfo = LoadScreenshotInfo(sqlHelper, folderId),
                DamageInfo = LoadDamageInfo(sqlHelper, folderId),
                ImageCount = LoadImageCount(sqlHelper, folderId)
            };
        }

        private static int FindFolderId(SQLHelper sqlHelper, string folderPath)
        {
            var pathCandidates = new List<string> { folderPath };

            try
            {
                pathCandidates.Add(Path.GetFullPath(folderPath));
            }
            catch
            {
                // 路径无法标准化时仍使用调用方传入值查询。
            }

            foreach (string candidate in pathCandidates
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                int folderId = sqlHelper.GetFolderIdByPath(candidate);
                if (folderId >= 0)
                {
                    return folderId;
                }
            }

            return -1;
        }

        private static ScreenshotInfo LoadScreenshotInfo(SQLHelper sqlHelper, int folderId)
        {
            bool hasSelectedRailType = DatabaseColumnExists(
                sqlHelper,
                "DamageFolders",
                "SelectedRailType");

            string selectedRailTypeColumn = hasSelectedRailType
                ? "SelectedRailType"
                : "NULL AS SelectedRailType";

            using var command = sqlHelper.Connection.CreateCommand();
            command.CommandText = $@"
SELECT
    Instruments,
    WorkSection,
    SerialNumber,
    WorkDate,
    WorkLength,
    ElapsedTimeForScrrnshot,
    SelectedRouteLine,
    SelectedUpOrDown,
    {selectedRailTypeColumn},
    RailWayName
FROM DamageFolders
WHERE FolderId = @FolderId
LIMIT 1;";
            command.Parameters.AddWithValue("@FolderId", folderId);

            using var reader = command.ExecuteReader();
            if (!reader.Read())
            {
                throw new InvalidOperationException(
                    $"数据库中未找到 FolderId={folderId} 的作业信息");
            }

            return new ScreenshotInfo
            {
                RailWayInfo = new RailInfo
                {
                    Instruments = ReadDatabaseString(reader, 0),
                    WorkSection = ReadDatabaseString(reader, 1),
                    SerialNumber = ReadDatabaseString(reader, 2),
                    WorkDate = ReadDatabaseString(reader, 3),
                    WorkLength = ReadDatabaseString(reader, 4),
                    ElapsedTimeForScrrnshot = ReadDatabaseString(reader, 5),
                    SelectedRouteLine = ReadDatabaseString(reader, 6),
                    SelectedUpOrDown = ReadDatabaseString(reader, 7),
                    SelectedRailType = ReadDatabaseString(reader, 8),
                    RailWayName = ReadDatabaseString(reader, 9)
                }
            };
        }

        private static List<KeyValuePair<int, float>> LoadDamageInfo(
            SQLHelper sqlHelper,
            int folderId)
        {
            using var command = sqlHelper.Connection.CreateCommand();
            command.CommandText = @"
SELECT
    da.DamageType,
    COUNT(DISTINCT i.ImageId) AS ImageCount
FROM Images i
INNER JOIN DamageAnnotations da ON da.ImageId = i.ImageId
WHERE i.FolderId = @FolderId
GROUP BY da.DamageType
ORDER BY ImageCount DESC, da.DamageType;";
            command.Parameters.AddWithValue("@FolderId", folderId);

            var damageInfo = new List<KeyValuePair<int, float>>();
            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                float damageType = reader.IsDBNull(0)
                    ? 0f
                    : Convert.ToSingle(reader.GetDouble(0));
                int imageCount = reader.IsDBNull(1)
                    ? 0
                    : Convert.ToInt32(reader.GetInt64(1));

                damageInfo.Add(new KeyValuePair<int, float>(imageCount, damageType));
            }

            return damageInfo;
        }

        private static int LoadImageCount(SQLHelper sqlHelper, int folderId)
        {
            using var command = sqlHelper.Connection.CreateCommand();
            command.CommandText = @"
SELECT COUNT(*)
FROM Images
WHERE FolderId = @FolderId;";
            command.Parameters.AddWithValue("@FolderId", folderId);

            object? result = command.ExecuteScalar();
            return result == null || result == DBNull.Value
                ? 0
                : Convert.ToInt32(result);
        }

        private static bool DatabaseColumnExists(
            SQLHelper sqlHelper,
            string tableName,
            string columnName)
        {
            using var command = sqlHelper.Connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info([{tableName}]);";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                if (!reader.IsDBNull(1) && string.Equals(
                    reader.GetString(1),
                    columnName,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string ReadDatabaseString(SqliteDataReader reader, int ordinal)
        {
            return reader.IsDBNull(ordinal)
                ? string.Empty
                : Convert.ToString(reader.GetValue(ordinal)) ?? string.Empty;
        }

        /// <summary>
        /// 复制第 2 行样式到当前写入行。
        /// 新模板第 2 行作为数据行样式模板。
        /// </summary>
        private static void CopyDataRowStyle(IXLWorksheet worksheet, int rowIndex)
        {
            const int templateRowIndex = 2;
            const int maxColumn = 22;

            worksheet.Row(rowIndex).Height = worksheet.Row(templateRowIndex).Height;

            for (int column = 1; column <= maxColumn; column++)
            {
                worksheet.Cell(rowIndex, column).Style = worksheet.Cell(templateRowIndex, column).Style;
            }

            // 数据行固定字号 16，内容水平/垂直居中。
            var dataRowRange = worksheet.Range(rowIndex, 1, rowIndex, maxColumn);
            dataRowRange.Style.Font.FontSize = 16;
            dataRowRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            dataRowRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }

        /// <summary>
        /// 统一设置导出区域样式。
        /// 包括表头和所有数据行：字号 16，内容水平/垂直居中。
        /// </summary>
        private static void ApplyGlobalExportStyle(IXLWorksheet worksheet, int lastRowIndex)
        {
            const int maxColumn = 22;

            if (lastRowIndex < 1)
                return;

            var exportRange = worksheet.Range(1, 1, lastRowIndex, maxColumn);
            exportRange.Style.Font.FontSize = 16;
            exportRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            exportRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }

        /// <summary>
        /// 按伤损 id 汇总数量。
        /// damageInfo 的结构沿用 ObtainInfo.GetCategoryAndCount：Key 为数量，Value 为伤损 id。
        /// </summary>
        private static int GetDamageCount(List<KeyValuePair<int, float>> damageInfo, params int[] ids)
        {
            return damageInfo
                .Where(x => ids.Any(id => Math.Abs(x.Value - id) < 0.001f))
                .Sum(x => x.Key);
        }

        /// <summary>
        /// 兼容传入文件夹路径或文件路径，统一转换成数据库使用的文件夹路径。
        /// </summary>
        private static string ResolveFolderPath(string path)
        {
            if (Directory.Exists(path))
                return path;

            if (File.Exists(path))
                return Path.GetDirectoryName(path) ?? path;

            return path;
        }

        /// <summary>
        /// 旧模板用的写法，新模板已不再使用。
        /// 保留方法只是为了避免外部代码误引用时报错。
        /// </summary>
        private static void WriteDamageCount(List<KeyValuePair<int, float>> damageInfo, IXLWorksheet worksheet, int startColumn, int endColumn, bool isTake10 = false, int lineSpace = 1, int columnOffset = 0)
        {
            // 新模板改为固定列写入，不再根据模板表头反查 id。
        }
    }
}
