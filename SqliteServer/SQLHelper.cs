using DamageMaker.Models;
using DocumentFormat.OpenXml.Math;
using Microsoft.Data.Sqlite;
using SQLitePCL;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using System.IO;
using System.Linq;

namespace DamageMaker.SqliteServer
{
    public class SQLHelper : IDisposable
    {
        private readonly string _mDbConnectionString;
        private SqliteConnection _connection;
        private string _connectionString;
        private string _lastErrorMessage = string.Empty;
        private string _lastError = "";
        public SqliteConnection Connection => _connection;



        private string _errorInfo; // 最后一次错误信息

        static SQLHelper()
        {
            if (Environment.OSVersion.Version.Build >= 10586)
            {
                raw.SetProvider(new SQLite3Provider_winsqlite3());
            }
            else
            {
                raw.SetProvider(new SQLite3Provider_e_sqlite3());
            }
        }

        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="dataSource">数据库文件路径</param>
        public SQLHelper(string dataSource)
        {
            _mDbConnectionString = "Filename=" + dataSource;
            _connection = new SqliteConnection(_mDbConnectionString);
            EnsureDamageFoldersNewColumns();
        }

        /// <summary>
        /// 确保连接已打开
        /// </summary>
        public void EnsureConnectionOpen()
        {
            if (_connection.State != ConnectionState.Open)
            {
                _connection.Open();
            }
        }

        /// <summary>
        /// 执行一条非查询语句
        /// </summary>
        /// <param name="sql">SQL语句</param>
        /// <returns>返回影响的结果数，失败返回 -1</returns>
        /// public SqlCommand CreateCommand(string sql)

        public int ExecuteSql(string sql)
        {
            try
            {
                EnsureConnectionOpen();
                using (var command = new SqliteCommand(sql, _connection))
                {
                    return command.ExecuteNonQuery();
                }
            }
            catch (SqliteException ex)
            {
                _errorInfo = ex.Message;
                return -1;
            }
        }

        /// <summary>
        /// 执行查询语句，返回单行结果
        /// </summary>
        /// <param name="sql">SQL语句</param>
        /// <param name="columns">结果应包含的列数</param>
        /// <returns>返回结果列表，失败返回 null</returns>
        public List<object> ExecuteReader_OneLine(string sql, int columns)
        {
            try
            {
                EnsureConnectionOpen();
                using (var cmd = new SqliteCommand(sql, _connection))
                {
                    using (var myReader = cmd.ExecuteReader())
                    {
                        var ret = new List<object>();
                        while (myReader.Read())
                        {
                            for (var i = 0; i < columns; i++)
                            {
                                ret.Add(myReader.GetValue(i));
                            }
                        }
                        return ret;
                    }
                }
            }
            catch (SqliteException e)
            {
                _errorInfo = e.Message;
                return null;
            }
        }

        /// <summary>
        /// 执行查询语句，返回多行结果
        /// </summary>
        /// <param name="sql">SQL语句</param>
        /// <param name="columns">结果应包含的列数</param>
        /// <returns>返回结果列表，失败返回 null</returns>
        public List<List<string>> ExecuteReader(string sql, int columns)
        {
            try
            {
                EnsureConnectionOpen();
                using (var cmd = new SqliteCommand(sql, _connection))
                {
                    using (var myReader = cmd.ExecuteReader())
                    {
                        var ret = new List<List<string>>();
                        while (myReader.Read())
                        {
                            var lst = new List<string>();
                            for (var i = 0; i < columns; i++)
                            {
                                lst.Add(myReader[i].ToString());
                            }
                            ret.Add(lst);
                        }
                        return ret;
                    }
                }
            }
            catch (SqliteException e)
            {
                _errorInfo = e.Message;
                return null;
            }
        }

        public string GetLastErrorMessage() => _lastErrorMessage;

        public bool CompareStringWithDatabase(string value, string tableName, string columnName)
        {
            try
            {
                string query = $"SELECT COUNT(*) FROM [{tableName}] WHERE LOWER(TRIM([{columnName}])) LIKE LOWER(@value)";
                using var command = new SqliteCommand(query, _connection);

                // 不再 Trim 参数本身，保留通配符
                string likePattern = $"%{value?.Trim()}%";

                var parameter = command.CreateParameter();
                parameter.ParameterName = "@value";
                parameter.DbType = DbType.String;
                parameter.Value = likePattern;

                command.Parameters.Add(parameter);

                long count = (long)command.ExecuteScalar();
                Console.WriteLine($"Query executed: {query} with value = '{parameter.Value}', count = {count}");

                return count > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CompareStringWithDatabase] Error: {ex.Message}");
                return false;
            }
        }


        // 获取DamageFolder的ID
        public int GetDamageFolderId(string workDate, string serialNumber, string workSection)
        {
            try
            {
                string sql = @"
            SELECT FolderId 
            FROM DamageFolders 
            WHERE WorkDate = @workDate 
              AND SerialNumber = @serialNumber 
              AND WorkSection = @workSection
            LIMIT 1";

                using (var cmd = new SqliteCommand(sql, _connection))
                {
                    cmd.Parameters.AddWithValue("@workDate", workDate);
                    cmd.Parameters.AddWithValue("@serialNumber", serialNumber);
                    cmd.Parameters.AddWithValue("@workSection", workSection);

                    object result = cmd.ExecuteScalar();
                    return (result != null) ? Convert.ToInt32(result) : -1;
                }
            }
            catch (Exception ex)
            {
                _lastErrorMessage = ex.Message;
                return -1;
            }
        }

        // 删除指定FolderId的所有图片记录
        public bool DeleteImagesByFolderId(int folderId)
        {
            try
            {
                using (var transaction = _connection.BeginTransaction())
                {
                    try
                    {
                        // 先获取记录数用于验证
                        int count = GetImageCountByFolderId(folderId, transaction);

                        if (count == 0)
                        {
                            _lastErrorMessage = "没有找到对应的图片记录";
                            return true;
                        }

                        Console.WriteLine("开始删除数据库记录...");
                        Console.WriteLine(folderId);

                        // **第一步：先删除 DamageAnnotations 表中相关的记录**
                        string deleteAnnotations = "DELETE FROM DamageAnnotations WHERE ImageId IN (SELECT ImageId FROM Images WHERE FolderId = @folderId)";
                        using (var cmdAnnotations = new SqliteCommand(deleteAnnotations, _connection, transaction))
                        {
                            cmdAnnotations.Parameters.AddWithValue("@folderId", folderId);
                            int deletedAnnotations = cmdAnnotations.ExecuteNonQuery();
                            Console.WriteLine($"已删除 {deletedAnnotations} 条 DamageAnnotations 记录");
                        }

                        // **第二步：删除 Images 表中的记录**
                        string sql = "DELETE FROM Images WHERE FolderId = @folderId";
                        using (var cmd = new SqliteCommand(sql, _connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@folderId", folderId);
                            int affectedRows = cmd.ExecuteNonQuery();

                            // **第三步：删除文件夹记录**
                            string deleteFolder = "DELETE FROM DamageFolders WHERE FolderId = @folderId";
                            using (var cmdFolder = new SqliteCommand(deleteFolder, _connection, transaction))
                            {
                                cmdFolder.Parameters.AddWithValue("@folderId", folderId);
                                int rows = cmdFolder.ExecuteNonQuery();
                                Console.WriteLine($"已删除文件夹记录: {rows} 条");
                            }

                            if (affectedRows != count)
                            {
                                _lastErrorMessage = $"应删除 {count} 条，实际删除 {affectedRows} 条";
                                transaction.Rollback();
                                return false;
                            }
                        }

                        transaction.Commit();
                        Console.WriteLine("删除操作完成");
                        return true;
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        _lastErrorMessage = ex.Message;
                        Console.WriteLine($"删除失败: {ex.Message}");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                _lastErrorMessage = ex.Message;
                return false;
            }
        }

        private int GetImageCountByFolderId(int folderId, SqliteTransaction transaction = null)
        {
            try
            {
                string sql = "SELECT COUNT(*) FROM Images WHERE FolderId = @folderId";
                using var cmd = new SqliteCommand(sql, _connection);
                if (transaction != null)
                    cmd.Transaction = transaction;

                cmd.Parameters.AddWithValue("@folderId", folderId);
                object result = cmd.ExecuteScalar();
                return (result != null) ? Convert.ToInt32(result) : 0;
            }
            catch
            {
                return 0;
            }
        }

        public int InsertTableWithTransaction(Dictionary<string, object> parameters, string tableName, SqliteTransaction transaction)
        {
            if (parameters == null || parameters.Count == 0)
            {
                _errorInfo = "参数不能为空";
                return -1;
            }

            var columnNames = string.Join(", ", parameters.Keys.Select(k => k.TrimStart('@')));
            var parameterPlaceholders = string.Join(", ", parameters.Keys);

            string insertSql = $@"
            INSERT INTO {tableName} (
                {columnNames}
            ) VALUES (
                {parameterPlaceholders}
            );";

            try
            {
                using (var command = new SqliteCommand(insertSql, _connection, transaction))
                {
                    foreach (var param in parameters)
                    {
                        command.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
                    }
                    return command.ExecuteNonQuery();
                }
            }
            catch (SqliteException ex)
            {
                _errorInfo = ex.Message;
                return -1;
            }
        }


        /// <summary>
        /// 插入数据到 DamageFolders 表
        /// </summary>
        /// <param name="parameters">包含列名和对应值的字典</param>
        /// <returns>返回影响的行数，失败返回 -1</returns>
        public int InsertTable(Dictionary<string, object> parameters, string TableName)
        {
            if (parameters == null || parameters.Count == 0)
            {
                _errorInfo = "参数不能为空";
                return -1;
            }

            var columnNames = string.Join(", ", parameters.Keys.Select(k => k.TrimStart('@')));
            var parameterPlaceholders = string.Join(", ", parameters.Keys);

            string insertSql = $@"
            INSERT INTO {TableName} (
                {columnNames}
            ) VALUES (
                {parameterPlaceholders}
            );";

            try
            {
                EnsureConnectionOpen();
                using (var command = new SqliteCommand(insertSql, _connection))
                {
                    foreach (var param in parameters)
                    {
                        command.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
                    }
                    return command.ExecuteNonQuery();
                }
            }
            catch (SqliteException ex)
            {
                _errorInfo = ex.Message;
                return -1;
            }
        }



        /// <summary>
        /// 确保 DamageFolders 表存在新版本列：StartMileage / EndMileage / SelectedRailType。
        /// 旧库升级用，幂等，每次构造 SQLHelper 时自动执行。
        /// </summary>
        public void EnsureDamageFoldersNewColumns()
        {
            try
            {
                EnsureConnectionOpen();

                var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (var columnCommand = new SqliteCommand("PRAGMA table_info(DamageFolders);", _connection))
                using (var reader = columnCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string columnName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                        if (!string.IsNullOrEmpty(columnName))
                        {
                            existingColumns.Add(columnName);
                        }
                    }
                }

                string[] newColumns =
                {
                    "StartMileage TEXT",
                    "EndMileage TEXT",
                    "SelectedRailType TEXT"
                };

                foreach (string columnDef in newColumns)
                {
                    string columnName = columnDef.Split(' ')[0];
                    if (!existingColumns.Contains(columnName))
                    {
                        using var alterCommand = new SqliteCommand(
                            $"ALTER TABLE DamageFolders ADD COLUMN {columnDef};",
                            _connection);
                        alterCommand.ExecuteNonQuery();
                        Console.WriteLine($"[数据库迁移] DamageFolders 表已增加字段 {columnName}");
                    }
                }
            }
            catch (SqliteException ex)
            {
                // 数据库不存在或表不存在时不阻断程序启动，相关操作后续会自行报错。
                Console.WriteLine($"[数据库迁移] 检查 DamageFolders 新字段失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 确保 Images 表存在 LineType 字段。
        /// 不再创建 ImageLineTypePending，也不再创建任何自动关联触发器。
        /// </summary>
        public void EnsureImagesLineTypeColumn()
        {
            EnsureConnectionOpen();

            bool imagesTableExists;
            using (var tableCommand = new SqliteCommand(
                "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'Images';",
                _connection))
            {
                imagesTableExists = Convert.ToInt32(tableCommand.ExecuteScalar()) > 0;
            }

            if (!imagesTableExists)
            {
                throw new InvalidOperationException("数据库中不存在 Images 表，无法保存 LineType");
            }

            bool hasLineTypeColumn = false;
            using (var columnCommand = new SqliteCommand("PRAGMA table_info(Images);", _connection))
            using (var reader = columnCommand.ExecuteReader())
            {
                while (reader.Read())
                {
                    string columnName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                    if (string.Equals(columnName, "LineType", StringComparison.OrdinalIgnoreCase))
                    {
                        hasLineTypeColumn = true;
                        break;
                    }
                }
            }

            if (!hasLineTypeColumn)
            {
                using var alterCommand = new SqliteCommand(
                    "ALTER TABLE Images ADD COLUMN LineType TEXT;",
                    _connection);
                alterCommand.ExecuteNonQuery();
                Console.WriteLine("[LineType数据库] 已自动给 Images 表增加 LineType 字段");
            }
        }

        /// <summary>
        /// 清理旧版本创建的 LineType 待关联表和触发器。
        /// 该操作是幂等的，多次调用不会报错。
        /// </summary>
        public void RemoveLegacyLineTypePendingArtifacts()
        {
            EnsureConnectionOpen();

            const string cleanupSql = @"
DROP TRIGGER IF EXISTS trg_Images_ApplyPendingLineType_Insert;
DROP TRIGGER IF EXISTS trg_Images_ApplyPendingLineType_UpdatePath;
DROP TABLE IF EXISTS ImageLineTypePending;";

            using var command = new SqliteCommand(cleanupSql, _connection);
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// LineType 入库前统一规范化：只保留汉字。
        /// 空格、中文/英文括号、标点、数字、字母和其他非汉字全部剔除。
        /// 例如：站线，（00），右股 -> 站线右股。
        /// </summary>
        public static string NormalizeLineTypeForStorage(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            return Regex.Replace(text, @"[^\u4E00-\u9FFF]", string.Empty);
        }


        /// <summary>
        /// 在 Images 记录已经由项目原流程写入后，只更新 LineType 字段。
        /// 不修改 ImageData、Mileage、ImgPath、FolderId 或任何其他字段。
        /// lookup 的 Key 支持：PATH|完整路径、FILE|文件名。
        /// </summary>
        public int UpdateImageLineTypesOnly(
            int folderId,
            IReadOnlyDictionary<string, string>? lineTypeLookup)
        {
            if (folderId < 0 || lineTypeLookup == null || lineTypeLookup.Count == 0)
            {
                return 0;
            }

            try
            {
                EnsureConnectionOpen();
                EnsureImagesLineTypeColumn();
                RemoveLegacyLineTypePendingArtifacts();

                var images = new List<(long ImageId, string ImgPath, string CurrentLineType)>();

                using (var selectCommand = _connection.CreateCommand())
                {
                    selectCommand.CommandText = @"
SELECT ImageId, ImgPath, LineType
FROM Images
WHERE FolderId = @FolderId;";
                    selectCommand.Parameters.AddWithValue("@FolderId", folderId);

                    using var reader = selectCommand.ExecuteReader();
                    while (reader.Read())
                    {
                        images.Add((
                            reader.GetInt64(0),
                            reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                            reader.IsDBNull(2) ? string.Empty : reader.GetString(2)));
                    }
                }

                if (images.Count == 0)
                {
                    return 0;
                }

                using var transaction = _connection.BeginTransaction();
                using var updateCommand = _connection.CreateCommand();
                updateCommand.Transaction = transaction;
                updateCommand.CommandText = @"
UPDATE Images
SET LineType = @LineType
WHERE ImageId = @ImageId;";

                var lineTypeParameter = updateCommand.Parameters.Add("@LineType", SqliteType.Text);
                var imageIdParameter = updateCommand.Parameters.Add("@ImageId", SqliteType.Integer);

                int updatedCount = 0;

                foreach (var image in images)
                {
                    string normalizedPath;
                    try
                    {
                        normalizedPath = Path.GetFullPath(image.ImgPath)
                            .Trim()
                            .Replace('/', '\\');
                    }
                    catch
                    {
                        normalizedPath = (image.ImgPath ?? string.Empty)
                            .Trim()
                            .Replace('/', '\\');
                    }

                    string normalizedLineType = string.Empty;

                    if (!string.IsNullOrWhiteSpace(normalizedPath) &&
                        lineTypeLookup.TryGetValue(
                            $"PATH|{normalizedPath}",
                            out string? lineTypeByPath))
                    {
                        normalizedLineType = NormalizeLineTypeForStorage(lineTypeByPath);
                    }

                    if (string.IsNullOrWhiteSpace(normalizedLineType))
                    {
                        string fileName = Path.GetFileName(image.ImgPath);
                        if (!string.IsNullOrWhiteSpace(fileName) &&
                            lineTypeLookup.TryGetValue(
                                $"FILE|{fileName}",
                                out string? lineTypeByFileName))
                        {
                            normalizedLineType = NormalizeLineTypeForStorage(lineTypeByFileName);
                        }
                    }

                    // 没识别到有效汉字时不覆盖数据库中已有值。
                    if (string.IsNullOrWhiteSpace(normalizedLineType))
                    {
                        continue;
                    }

                    string normalizedExisting =
                        NormalizeLineTypeForStorage(image.CurrentLineType);

                    if (string.Equals(
                        normalizedExisting,
                        normalizedLineType,
                        StringComparison.Ordinal))
                    {
                        continue;
                    }

                    lineTypeParameter.Value = normalizedLineType;
                    imageIdParameter.Value = image.ImageId;
                    updatedCount += updateCommand.ExecuteNonQuery();
                }

                transaction.Commit();
                Console.WriteLine(
                    $"[LineType入库] FolderId={folderId}，仅更新LineType，共更新={updatedCount}");
                return updatedCount;
            }
            catch (Exception ex)
            {
                _lastErrorMessage = ex.Message;
                Console.WriteLine(
                    $"[LineType入库] 仅更新LineType失败：FolderId={folderId}，error={ex.Message}");
                return -1;
            }
        }

        /// <summary>
        /// 根据当前图片路径和文件夹ID读取 Images.LineType。
        /// 优先使用完整路径匹配；完整路径不一致时，回退到同一文件夹内的文件名匹配。
        /// 返回值会再次执行“只保留汉字”规范化，兼容历史未清理数据。
        /// </summary>
        public string GetNormalizedLineTypeByImagePath(string? imagePath, long folderId)
        {
            if (string.IsNullOrWhiteSpace(imagePath) || folderId <= 0)
            {
                return string.Empty;
            }

            try
            {
                EnsureImagesLineTypeColumn();

                string targetPath = NormalizeImagePathForComparison(imagePath);
                string targetFileName = Path.GetFileName(imagePath);
                string fileNameFallback = string.Empty;

                const string query = @"
SELECT ImgPath, LineType
FROM Images
WHERE FolderId = @FolderId;";

                using var command = new SqliteCommand(query, _connection);
                command.Parameters.AddWithValue("@FolderId", folderId);

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    string dbPath = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
                    string dbLineType = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);

                    if (string.Equals(
                        NormalizeImagePathForComparison(dbPath),
                        targetPath,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        return NormalizeLineTypeForStorage(dbLineType);
                    }

                    if (string.IsNullOrEmpty(fileNameFallback) &&
                        string.Equals(
                            Path.GetFileName(dbPath),
                            targetFileName,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        fileNameFallback = NormalizeLineTypeForStorage(dbLineType);
                    }
                }

                return fileNameFallback;
            }
            catch (Exception ex)
            {
                _errorInfo = ex.Message;
                Console.WriteLine($"[LineType查询] 当前图片LineType读取失败: path={imagePath}, FolderId={folderId}, error={ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// 批量读取上一周期候选图片的 Images.LineType。
        /// Key为ImageId，Value为只保留汉字后的LineType。
        /// </summary>
        public Dictionary<long, string> GetNormalizedLineTypesByImageIds(IEnumerable<long> imageIds)
        {
            var result = new Dictionary<long, string>();
            var ids = (imageIds ?? Enumerable.Empty<long>())
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            if (ids.Count == 0)
            {
                return result;
            }

            try
            {
                EnsureImagesLineTypeColumn();

                var parameterNames = new List<string>();
                using var command = _connection.CreateCommand();

                for (int i = 0; i < ids.Count; i++)
                {
                    string parameterName = $"@ImageId{i}";
                    parameterNames.Add(parameterName);
                    command.Parameters.AddWithValue(parameterName, ids[i]);
                }

                command.CommandText = $@"
SELECT ImageId, LineType
FROM Images
WHERE ImageId IN ({string.Join(",", parameterNames)});";

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    long imageId = reader.GetInt64(0);
                    string lineType = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                    result[imageId] = NormalizeLineTypeForStorage(lineType);
                }
            }
            catch (Exception ex)
            {
                _errorInfo = ex.Message;
                Console.WriteLine($"[LineType查询] 上一周期LineType批量读取失败: {ex.Message}");
            }

            return result;
        }

        private static string NormalizeImagePathForComparison(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            return path.Trim().Replace('/', '\\');
        }

        /// <summary>
        /// 根据 FolderPath 查询对应的 FolderId
        /// </summary>
        /// <param name="folderPath">要查询的 FolderPath</param>
        /// <returns>返回对应的 FolderId，如果未找到则返回 -1</returns>
        // 保留原来的同步方法
        public int GetFolderIdByPath(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath))
            {
                _errorInfo = "FolderPath 不能为空";
                return -1;
            }

            string query = "SELECT FolderId FROM DamageFolders WHERE FolderPath = @FolderPath";

            try
            {
                EnsureConnectionOpen();
                using (var cmd = new SqliteCommand(query, _connection))
                {
                    cmd.Parameters.AddWithValue("@FolderPath", folderPath);
                    var result = cmd.ExecuteScalar();
                    return result != null ? Convert.ToInt32(result) : -1;
                }
            }
            catch (SqliteException ex)
            {
                _errorInfo = ex.Message;
                return -1;
            }
        }

        // 新增异步版本（包装器）
        public async Task<int> GetFolderIdByPathAsync(string folderPath)
        {
            return await Task.Run(() => GetFolderIdByPath(folderPath));
        }

        /// <summary>
        /// 根据 FolderId 查询图像数量
        /// </summary>
        /// <param name="folderId">FolderId</param>
        /// <returns>返回图像数量，失败返回 -1</returns>
        /// <summary>
        /// 根据条件查询表中记录的数量
        /// </summary>
        /// <param name="tableName">表名</param>
        /// <param name="conditions">条件参数（列名和对应值的字典）</param>
        /// <returns>返回记录数量，失败返回 -1</returns>
        public int GetCount(string tableName, Dictionary<string, object> conditions)
        {
            if (string.IsNullOrEmpty(tableName))
            {
                _errorInfo = "表名不能为空";
                return -1;
            }

            if (conditions == null || conditions.Count == 0)
            {
                _errorInfo = "条件不能为空";
                return -1;
            }

            // 动态生成 WHERE 子句
            var whereClause = string.Join(" AND ", conditions.Keys.Select(k => $"{k.TrimStart('@')} = {k}"));

            string query = $@"
        SELECT COUNT(*)
        FROM {tableName}
        WHERE {whereClause}";

            try
            {
                EnsureConnectionOpen();
                using (var cmd = new SqliteCommand(query, _connection))
                {
                    // 绑定参数
                    foreach (var param in conditions)
                    {
                        cmd.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
                    }

                    var result = cmd.ExecuteScalar();
                    return result != null ? Convert.ToInt32(result) : -1;
                }
            }
            catch (SqliteException ex)
            {
                _errorInfo = ex.Message;
                return -1;
            }
        }

        /// <summary>
        /// 根据 FolderId 获取对应的图片信息列表一个是从 Images 表中获取图片信息一个是从 DamageAnnotations
        /// /// </summary>
        /// <param name="folderId"></param>
        /// <returns></returns>
        public List<SqlImgInfo> GetImagesByFolderId(long folderId)
        {
            EnsureConnectionOpen();
            var images = new List<SqlImgInfo>();

            // 创建Dictionary用于快速查找伤损类型名称
            var damageTypeDict = Records.DamageCategoryData?
                .ToDictionary(r => r.Id, r => r.CategoryName) ?? new Dictionary<int, string>();

            var command = Connection.CreateCommand();
            command.CommandText = @"
SELECT 
    i.ImageId,
    i.ImgPath, 
    i.FolderId, 
    i.IsConfirmDamage, 
    i.Remark,
    i.ImageData,
    i.Mileage,
    i.SaveTime,
    GROUP_CONCAT(DISTINCT d.DamageType) as DamageTypes
FROM Images i
LEFT JOIN DamageAnnotations d ON i.ImageId = d.ImageId
WHERE i.FolderId = @FolderId
GROUP BY i.ImageId, i.ImgPath, i.FolderId, i.IsConfirmDamage, i.Remark,
         i.ImageData, i.Mileage, i.SaveTime
ORDER BY i.SaveTime DESC";

            command.Parameters.AddWithValue("@FolderId", folderId);

            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    var imgInfo = new SqlImgInfo
                    {
                        ImgId = reader.GetInt64(reader.GetOrdinal("ImageId")),
                        ImgPath = reader.GetString(reader.GetOrdinal("ImgPath")),
                        FolderId = reader.GetInt64(reader.GetOrdinal("FolderId")),
                        Mileage = reader.IsDBNull(reader.GetOrdinal("Mileage")) ? null : reader.GetString(reader.GetOrdinal("Mileage")),
                        SaveTime = reader.GetDateTime(reader.GetOrdinal("SaveTime")),
                        ImageData = reader.IsDBNull(reader.GetOrdinal("ImageData")) ? null : (byte[])reader["ImageData"],
                        IsConfirmDamage = reader.IsDBNull(reader.GetOrdinal("IsConfirmDamage")) ? (bool?)null : reader.GetBoolean(reader.GetOrdinal("IsConfirmDamage")),
                        Remark = reader.IsDBNull(reader.GetOrdinal("Remark")) ? null : reader.GetString(reader.GetOrdinal("Remark")),
                        DamageType = GetDamageTypeNamesFromIds(reader["DamageTypes"], damageTypeDict)
                    };

                    images.Add(imgInfo);
                }
            }

            return images;
        }
        private string GetDamageTypeNamesFromIds(object damageTypesObj, Dictionary<int, string> damageTypeDict)
        {
            if (damageTypesObj is DBNull || damageTypesObj == null)
                return "无伤损";

            var damageTypeIds = damageTypesObj.ToString()
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s =>
                {
                    if (float.TryParse(s, out float floatValue))
                        return (int)floatValue;
                    return -1;
                })
                .Where(id => id != -1)
                .Distinct()
                .ToArray();

            if (damageTypeIds.Length == 0)
                return "无伤损";

            var typeNames = new List<string>();
            foreach (var id in damageTypeIds)
            {
                if (damageTypeDict.TryGetValue(id, out var name))
                {
                    typeNames.Add(name);
                }
                else
                {
                    typeNames.Add($"未知({id})");
                }
            }

            return typeNames.Count == 0 ? "无伤损" : string.Join(",", typeNames);
        }
        /// <summary>
        /// 执行带参数的非查询 SQL 语句
        /// </summary>
        /// <param name="sql">SQL 语句</param>
        /// <param name="parameters">参数字典</param>
        /// <returns>返回受影响的行数，失败返回 -1</returns>
        public int ExecuteNonQuery(string sql, Dictionary<string, object?> parameters)
        {
            try
            {
                EnsureConnectionOpen(); // 确保数据库连接已打开

                using (var command = new SqliteCommand(sql, _connection))
                {
                    // 添加参数到命令
                    foreach (var param in parameters)
                    {
                        command.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
                    }

                    // 执行 SQL 语句并返回受影响的行数
                    return command.ExecuteNonQuery();
                }
            }
            catch (SqliteException ex)
            {
                _errorInfo = ex.Message; // 记录错误信息
                Console.WriteLine($"SQL 执行失败: {_errorInfo}");
                return -1; // 返回 -1 表示失败
            }
        }

        public List<SqlImgInfo> GetDamageImgPaths(long folderId)
        {
            const string query = @"
        SELECT ImgPath, Remark, ImageData
        FROM Images
        WHERE FolderId = @FolderId AND IsConfirmDamage = 1;";

            var parameters = new Dictionary<string, object?>
    {
        { "@FolderId", folderId }
    };

            var imgPaths = new List<SqlImgInfo>();

            try
            {
                EnsureConnectionOpen();
                using (var command = new SqliteCommand(query, _connection))
                {
                    foreach (var param in parameters)
                    {
                        command.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
                    }
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            imgPaths.Add(new SqlImgInfo
                            {
                                ImgPath = reader.GetString(0),
                                Remark = reader.GetString(1)
                            }); // 读取 ImgPath 列
                        }
                    }
                }
            }
            catch (SqliteException ex)
            {
                _errorInfo = ex.Message;
                Console.WriteLine($"SQL 执行失败: {_errorInfo}");
            }

            return imgPaths;
        }

        /// <summary>
        /// 根据文件夹名称删除数据库中的记录
        /// </summary>
        /// <param name="folderPath">文件夹路径</param>
        /// <returns>返回受影响的行数，失败返回 -1</returns>
        public int DeleteFolderInfo(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                _errorInfo = "FolderPath 不能为空";
                return -1;
            }

            try
            {
                EnsureConnectionOpen();

                string sql = "DELETE FROM DamageFolders WHERE FolderPath = @FolderPath";
                var parameters = new Dictionary<string, object?>
        {
            { "@FolderPath", folderPath }
        };

                return ExecuteNonQuery(sql, parameters);
            }
            catch (Exception ex)
            {
                _errorInfo = $"删除文件夹信息失败: {ex.Message}";
                return -1;
            }
        }



        public int UpdateImageIsConfirmDamage(long imageId, bool? isConfirmDamage)
        {
            const string query = @"
        UPDATE Images
        SET IsConfirmDamage = @IsConfirmDamage
        WHERE ImageId = @ImageId
         AND 
        (IsConfirmDamage != @IsConfirmDamage OR IsConfirmDamage IS NULL OR @IsConfirmDamage IS NULL);
    ";

            var parameters = new Dictionary<string, object?>
    {
        { "@ImageId", imageId },
        { "@IsConfirmDamage", isConfirmDamage }
    };

            return ExecuteNonQuery(query, parameters);
        }
        /// <summary>
        /// 获取所有文件夹下的所有图片信息
        /// </summary>
        public List<SqlImgInfo> GetAllImages()
        {
            EnsureConnectionOpen();

            string query = @"SELECT ImageId, ImgPath, FolderId, IsConfirmDamage, Remark, ImageData, Mileage, SaveTime 
               FROM Images";
            var imgInfos = new List<SqlImgInfo>();

            try
            {
                using (var command = Connection.CreateCommand())
                {
                    command.CommandText = query;
                    using (var reader = command.ExecuteReader())
                    {
                        int imageDataOrdinal = reader.GetOrdinal("ImageData");

                        while (reader.Read())
                        {
                            byte[] imageData = null;

                            // 正确读取 BLOB 数据
                            if (!reader.IsDBNull(imageDataOrdinal))
                            {

                                // 或者方法2: 使用 GetStream (推荐)
                                using (var stream = reader.GetStream(imageDataOrdinal))
                                using (var memoryStream = new System.IO.MemoryStream())
                                {
                                    stream.CopyTo(memoryStream);
                                    imageData = memoryStream.ToArray();
                                }
                            }

                            var sqlImgInfo = new SqlImgInfo
                            {
                                ImgId = reader.GetInt64(reader.GetOrdinal("ImageId")),
                                ImgPath = reader.GetString(reader.GetOrdinal("ImgPath")),
                                FolderId = reader.GetInt64(reader.GetOrdinal("FolderId")),
                                IsConfirmDamage = reader.IsDBNull(reader.GetOrdinal("IsConfirmDamage"))
                                    ? null
                                    : reader.GetBoolean(reader.GetOrdinal("IsConfirmDamage")),
                                Remark = reader.IsDBNull(reader.GetOrdinal("Remark"))
                                    ? null
                                    : reader.GetString(reader.GetOrdinal("Remark")),
                                ImageData = imageData,  // 使用正确读取的数据

                                Mileage = reader.IsDBNull(reader.GetOrdinal("Mileage"))
                                    ? null
                                    : reader.GetString(reader.GetOrdinal("Mileage")),
                                SaveTime = reader.IsDBNull(reader.GetOrdinal("SaveTime"))
                                    ? DateTime.MinValue
                                    : reader.GetDateTime(reader.GetOrdinal("SaveTime"))
                            };
                            imgInfos.Add(sqlImgInfo);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _errorInfo = ex.Message;
                Console.WriteLine($"Error fetching all images: {_errorInfo}");
            }

            return imgInfos;
        }

        public int UpdateImageData(long imageId, byte[]? imageData)
        {
            const string query = @"
            UPDATE Images
            SET ImageData = @ImageData
            WHERE ImageId = @ImageId;
        ";
            var parameters = new Dictionary<string, object?>
        {
            { "@ImageId", imageId },
            { "@ImageData", imageData ?? (object)DBNull.Value }
        };
            return ExecuteNonQuery(query, parameters);
        }


        public int UpdateImageRemark(long imageId, string? remark)
        {
            const string query = @"
        UPDATE Images
        SET Remark = @Remark
        WHERE ImageId = @ImageId";

            var parameters = new Dictionary<string, object?>
    {
        { "@ImageId", imageId },
        { "@Remark", remark }
    };

            return ExecuteNonQuery(query, parameters);
        }

        public int AppendImageRemark(long imageId, string appendText)
        {
            // 1. 查询当前 Remark
            string selectSql = "SELECT Remark FROM Images WHERE ImageId = @ImageId";
            string? currentRemark = null;
            try
            {
                EnsureConnectionOpen();
                using (var cmd = new SqliteCommand(selectSql, _connection))
                {
                    cmd.Parameters.AddWithValue("@ImageId", imageId);
                    var result = cmd.ExecuteScalar();
                    currentRemark = result == DBNull.Value ? null : result?.ToString();
                }
            }
            catch (SqliteException ex)
            {
                _errorInfo = ex.Message;
                return -1;
            }

            // 2. 追加内容（用换行分隔）
            string newRemark;
            if (string.IsNullOrEmpty(currentRemark))
                newRemark = appendText;
            else
                newRemark = currentRemark + Environment.NewLine + appendText;

            // 3. 更新 Remark 字段
            const string updateSql = @"
                UPDATE Images
                SET Remark = @Remark
                WHERE ImageId = @ImageId;
            ";
            var parameters = new Dictionary<string, object?>
            {
                { "@ImageId", imageId },
                { "@Remark", newRemark }
            };

            return ExecuteNonQuery(updateSql, parameters);
        }

        /// <summary>
        /// 获取最后一次失败原因
        /// </summary>
        /// <returns>返回错误信息</returns>
        public string GetLastError()
        {
            return _errorInfo;
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (_connection != null)
            {
                _connection.Dispose();
                _connection = null;
            }
        }
        public int UpdateFolderRemark(string folderPath, string? remark)
        {
            const string query = @"
            UPDATE DamageFolders
            SET Remark = @Remark
            WHERE RailWayName = @RailWayName;
                ";

            var parameters = new Dictionary<string, object?>
            {
                { "@RailWayName", folderPath },
                { "@Remark", remark ?? (object)DBNull.Value }
            };
            Console.WriteLine($"更新备注:FolderPath = {folderPath}, Remark = {remark}");
            return ExecuteNonQuery(query, parameters);
        }

        public int? GetIsConfirmDamage(long folderId)
        {
            string sql = "SELECT IsConfirmDamage FROM Images WHERE ImageId = @FolderId LIMIT 1";
            EnsureConnectionOpen();
            using var cmd = new SqliteCommand(sql, _connection);
            cmd.Parameters.AddWithValue("@FolderId", folderId);

            var result = cmd.ExecuteScalar();

            if (result == null || result == DBNull.Value)
                return null;

            return Convert.ToInt32(result);
        }

        public void UpdateFolderLastOpenTime(int folderId, DateTime lastOpenTime)
        {
            EnsureConnectionOpen();
            var command = new SqliteCommand("UPDATE DamageFolders SET LastOpenTime = @time WHERE FolderId = @id", _connection);
            command.Parameters.AddWithValue("@time", lastOpenTime);
            command.Parameters.AddWithValue("@id", folderId);
            command.ExecuteNonQuery();

        }

        /// <summary>
        /// 删除指定图片记录
        /// </summary>
        /// <param name="imgId"></param>
        /// <returns></returns>
        public async Task DeleteImageAsync(long imgId)
        {
            try
            {
                EnsureConnectionOpen();
                const string query = @"
                DELETE FROM Images
                WHERE ImageId = @ImageId;";
                var parameters = new Dictionary<string, object?>
                    {
                { "@ImageId", imgId }
                    };
                await Task.Run(() => ExecuteNonQuery(query, parameters));
            }
            catch (Exception ex)
            {
                _errorInfo = ex.Message;
            }
        }

        /// <summary>
        /// 删除指定图片的所有标注记录
        /// </summary>
        /// <param name="imgId"></param>
        /// <returns></returns>
        public async Task DeleteDamageAnnotationAsync(long imgId)
        {
            //根据图片ID删除所有标注记录
            try
            {
                EnsureConnectionOpen();
                const string query = @"
                DELETE FROM DamageAnnotations
                WHERE ImageId = @ImageId;";
                var parameters = new Dictionary<string, object?>
                    {
                { "@ImageId", imgId }
                    };
                await Task.Run(() => ExecuteNonQuery(query, parameters));
            }
            catch (Exception ex)
            {
                _errorInfo = ex.Message;
            }
        }

        /// <summary>
        /// 根据图片ID获取所有标注的损伤类型
        /// </summary>
        /// <param name="imgId"></param>
        /// <returns></returns>
        public async Task<float[]> GetDamageTypesByImageIdAsync(long imgId)
        {
            EnsureConnectionOpen();
            const string query = @"
            SELECT DISTINCT DamageType
            FROM DamageAnnotations
            WHERE ImageId = @ImageId;";

            var parameters = new Dictionary<string, object?>
            {
                { "@ImageId", imgId }
            };

            return await Task.Run(() =>
            {
                var damageTypes = new List<float>();
                using var command = new SqliteCommand(query, _connection);
                foreach (var param in parameters)
                {
                    command.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
                }
                using var reader = command.ExecuteReader();

                while (reader.Read())
                {
                    if (!reader.IsDBNull(0))
                    {
                        damageTypes.Add(reader.GetFloat(0));
                    }
                }

                return damageTypes.ToArray();
            });
        }

        /// <summary>
        /// 根据选中的线路类型和方向获取对应的文件夹ID列表
        /// </summary>
        /// <param name="selectedLineType"></param>
        /// <param name="selectedLineDirection"></param>
        /// <returns></returns>
        public async Task<List<long>> GetFolderIdsAsync(string? selectedLineType,string? selectedLineDirection,string? selectedRailType)
        {
            EnsureConnectionOpen();

            const string query = @"
                SELECT FolderId
                FROM DamageFolders
                WHERE (@SelectedRouteLine IS NULL OR SelectedRouteLine = @SelectedRouteLine)
                AND (@SelectedUpOrDown IS NULL OR SelectedUpOrDown = @SelectedUpOrDown)
                AND (@SelectedRailType IS NULL OR SelectedRailType = @SelectedRailType);";

            var parameters = new Dictionary<string, object?>
            {
                { "@SelectedRouteLine", selectedLineType },
                { "@SelectedUpOrDown", selectedLineDirection },
                { "@SelectedRailType", selectedRailType }
            };

            var folderIds = new List<long>();

            await Task.Run(() =>
            {
                using var command = new SqliteCommand(query, _connection);

                foreach (var param in parameters)
                {
                    command.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
                }

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    if (!reader.IsDBNull(0))
                    {
                        folderIds.Add(reader.GetInt64(0));
                    }
                }
            });

            return folderIds;
        }

        /// <summary>
        /// 获取所有文件夹的备注信息
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public Dictionary<string, string> GetAllFolderRemarks()
        {
            EnsureConnectionOpen();
            var folderRemarks = new Dictionary<string, string>();
            try
            {
                const string query = @"
            SELECT FolderPath, Remark
            FROM DamageFolders;";
                using var command = new SqliteCommand(query, _connection);
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    string folderPath = reader.GetString(0);
                    string remark = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                    folderRemarks[folderPath] = remark;
                }
            }
            catch (SqliteException ex)
            {
                _errorInfo = ex.Message;
            }
            return folderRemarks;

        }

        /// <summary>
        /// 获取多个文件夹的疑似损伤图片数量
        /// </summary>
        /// <param name="folderNames"></param>
        /// <returns></returns>
        internal async Task<Dictionary<string, int>>? GetSuspectedDamageCounts(List<string> folderNames)
        {
            EnsureConnectionOpen();
            try
            {
                var folderCounts = new Dictionary<string, int>();
                const string query = @"
            SELECT DF.RailWayName, COUNT(*) AS SuspectedCount
            FROM Images I
            JOIN DamageFolders DF ON I.FolderId = DF.FolderId
            WHERE I.IsConfirmDamage = 1 AND DF.RailWayName IN ({0})
            GROUP BY DF.RailWayName;";
                // 动态生成参数占位符
                var parameters = new List<string>();
                for (int i = 0; i < folderNames.Count; i++)
                {
                    parameters.Add($"@FolderName{i}");
                }
                string inClause = string.Join(", ", parameters);
                string finalQuery = string.Format(query, inClause);
                using var command = new SqliteCommand(finalQuery, _connection);
                for (int i = 0; i < folderNames.Count; i++)
                {
                    command.Parameters.AddWithValue($"@FolderName{i}", folderNames[i]);
                }
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    string folderName = reader.GetString(0);
                    int count = reader.GetInt32(1);
                    folderCounts[folderName] = count;
                }
                return folderCounts;
            }
            catch (SqliteException ex)
            {
                _errorInfo = ex.Message;
                return null;
            }
        }

        /// <summary>
        /// 根据线路名称获取对应的文件夹ID
        /// </summary>
        /// <param name="railWayName"></param>
        /// <returns></returns>
        public long GetFolderIdByRailWayName(string? railWayName)
        {
            try
            {
                EnsureConnectionOpen();
                const string query = @"
            SELECT FolderId
            FROM DamageFolders
            WHERE RailWayName = @RailWayName
            LIMIT 1;";
                using var command = new SqliteCommand(query, _connection);
                command.Parameters.AddWithValue("@RailWayName", railWayName ?? string.Empty);
                var result = command.ExecuteScalar();
                return result != null ? Convert.ToInt64(result) : -1;
            }
            catch (SqliteException ex)
            {
                _errorInfo = ex.Message;
                return -1;
            }
        }

        /// <summary>
        /// 根据图片路径获取对应的损伤标注信息
        /// </summary>
        /// <param name="imagePath">图片路径</param>
        /// <returns>损伤标注列表</returns>
        public List<DamageAnnotation> GetDamageAnnotationsByImagePath(string imagePath)
        {
            // 参数验证
            if (string.IsNullOrEmpty(imagePath))
            {
                return new List<DamageAnnotation>();
            }

            try
            {
                EnsureConnectionOpen();

                // SQL查询语句
                const string query = @"
            SELECT 
                AnnotationId,
                ImageId,
                X,
                Y,
                Width,
                Height,
                DamageType,
                Confidence,
                CreatedTime,
                UpdatedTime,
                ImagePath
            FROM DamageAnnotations 
            WHERE ImagePath = @ImagePath
            ORDER BY AnnotationId";

                using var command = new SqliteCommand(query, _connection);
                command.Parameters.AddWithValue("@ImagePath", imagePath);

                using var reader = command.ExecuteReader();
                var annotations = new List<DamageAnnotation>();

                while (reader.Read())
                {
                    var annotation = new DamageAnnotation
                    {
                        AnnotationId = reader.GetInt32(0),
                        ImageId = reader.GetInt32(1),
                        X = reader.GetFloat(2),
                        Y = reader.GetFloat(3),
                        Width = reader.GetFloat(4),
                        Height = reader.GetFloat(5),
                        DamageType = reader.GetFloat(6),
                        Confidence = reader.GetFloat(7),
                        CreatedTime = reader.GetDateTime(8),
                        UpdatedTime = reader.GetDateTime(9),
                        ImagePath = reader.IsDBNull(10) ? string.Empty : reader.GetString(10)
                    };

                    annotations.Add(annotation);
                }

                return annotations;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetDamageAnnotationsByImagePath: {ex.Message}");
                return new List<DamageAnnotation>();
            }
        }

        /// <summary>
        /// 根据图片路径来删除伤损标注记录
        /// </summary>
        /// <param name="imgPath"></param>
        public void DeleteDamageAnnotationsByImagePath(string imgPath)
        {
            EnsureConnectionOpen();
            const string query = @"
            DELETE FROM DamageAnnotations
            WHERE ImagePath = @ImagePath;";
            var parameters = new Dictionary<string, object?>
            {
                { "@ImagePath", imgPath }
            };
            ExecuteNonQuery(query, parameters);
        }

        /// <summary>
        /// 根据文件夹ID和周期号获取对应的日期列表
        /// </summary>
        /// <param name="id"></param>
        /// <param name="periodNumber"></param>
        /// <returns></returns>
        public async Task<List<DateTime>> GetDatesByFolderIdAndPeriodAsync(long id, int periodNumber)
        {
            try
            {
                EnsureConnectionOpen();
                const string query = @"
            SELECT DISTINCT WorkDate
            FROM DamageFolders
            WHERE FolderId = @FolderId AND CycleNumber = @CycleNumber;";
                var dates = new List<DateTime>();
                using var command = new SqliteCommand(query, _connection);
                command.Parameters.AddWithValue("@FolderId", id);
                command.Parameters.AddWithValue("@CycleNumber", periodNumber);
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    if (!reader.IsDBNull(0))
                    {
                        DateTime workDate;
                        if (DateTime.TryParse(reader.GetString(0), out workDate))
                        {
                            dates.Add(workDate);
                        }
                    }
                }
                return dates;
            }
            catch (SqliteException ex)
            {
                _errorInfo = ex.Message;
                return new List<DateTime>();
            }

        }
        /// <summary>
        /// 根据文件夹ID获取最大周期号
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<int> GetPeriodByFolderIdAsync(long id)
        {
            try
            {
                EnsureConnectionOpen();
                const string query = @"
            SELECT MAX(CycleNumber)
            FROM DamageFolders
            WHERE FolderId = @FolderId;";
                using var command = new SqliteCommand(query, _connection);
                command.Parameters.AddWithValue("@FolderId", id);
                var result = await command.ExecuteScalarAsync();
                return result != null && result != DBNull.Value ? Convert.ToInt32(result) : 0;
            }
            catch (SqliteException ex)
            {
                _errorInfo = ex.Message;
                return 0;
            }
        }


        /// <summary>
        /// 自动周期对比专用：按线别、周期号和起止里程查询上一周期焊缝。
        /// 不使用 SelectedUpOrDown（上下行）和 SelectedRailType（股别）作为筛选条件。
        /// 候选伤损类型仍保持原规则：id=52 / id=6 / id=2。
        /// </summary>
        public List<WeldPositionInfo> GetWeldPositionsByRouteAndCycle(
            string? selectedRouteLine,
            int weldInterval,
            string? startMileage,
            string? endMileage)
        {
            if (string.IsNullOrWhiteSpace(selectedRouteLine) ||
                weldInterval <= 0 ||
                string.IsNullOrWhiteSpace(startMileage) ||
                string.IsNullOrWhiteSpace(endMileage))
            {
                return new List<WeldPositionInfo>();
            }

            if (!TryGetMileageRange(
                startMileage,
                endMileage,
                out var queryStartMileage,
                out var queryEndMileage))
            {
                return new List<WeldPositionInfo>();
            }

            try
            {
                EnsureConnectionOpen();

                const string getImageIdsQuery = @"
SELECT DISTINCT
    da.ImageId,
    df.StartMileage,
    df.EndMileage
FROM DamageAnnotations da
INNER JOIN Images i ON da.ImageId = i.ImageId
INNER JOIN DamageFolders df ON i.FolderId = df.FolderId
WHERE df.SelectedRouteLine = @SelectedRouteLine
  AND df.CycleNumber = @WeldInterval
  AND da.DamageType IN (52, 2, 6);";

                var imageIdSet = new HashSet<long>();

                using (var command = new SqliteCommand(getImageIdsQuery, _connection))
                {
                    command.Parameters.AddWithValue("@SelectedRouteLine", selectedRouteLine);
                    command.Parameters.AddWithValue("@WeldInterval", weldInterval);

                    using var reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        long imageId = reader.GetInt64(0);
                        string dbStartMileageText = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                        string dbEndMileageText = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);

                        if (!TryGetMileageRange(
                            dbStartMileageText,
                            dbEndMileageText,
                            out var dbStartMileage,
                            out var dbEndMileage))
                        {
                            continue;
                        }

                        bool isOverlap = queryStartMileage <= dbEndMileage &&
                                         dbStartMileage <= queryEndMileage;

                        if (isOverlap)
                        {
                            imageIdSet.Add(imageId);
                        }
                    }
                }

                var imageIds = imageIdSet.ToList();
                if (imageIds.Count == 0)
                {
                    return new List<WeldPositionInfo>();
                }

                var parameters = new List<SqliteParameter>();
                var inClauses = new List<string>();

                for (int i = 0; i < imageIds.Count; i++)
                {
                    string parameterName = $"@ImageId{i}";
                    inClauses.Add(parameterName);
                    parameters.Add(new SqliteParameter(parameterName, imageIds[i]));
                }

                string inClause = string.Join(",", inClauses);

                const string getAllAnnotationsQueryTemplate = @"
SELECT
    da.ImageId,
    da.X,
    da.Y,
    da.Width,
    da.Height,
    da.DamageType,
    da.Confidence,
    i.ImgPath,
    i.FolderId,
    i.Mileage,
    i.ImageData
FROM DamageAnnotations da
INNER JOIN Images i ON da.ImageId = i.ImageId
WHERE da.ImageId IN ({0})
ORDER BY da.ImageId;";

                string getAllAnnotationsQuery = string.Format(
                    getAllAnnotationsQueryTemplate,
                    inClause);

                using var allCommand = new SqliteCommand(
                    getAllAnnotationsQuery,
                    _connection);
                allCommand.Parameters.AddRange(parameters.ToArray());

                using var readerAll = allCommand.ExecuteReader();
                var weldDict = new Dictionary<long, WeldPositionInfo>();

                while (readerAll.Read())
                {
                    long imgId = readerAll.GetInt64(0);

                    if (!weldDict.TryGetValue(imgId, out var weldInfo))
                    {
                        weldInfo = new WeldPositionInfo
                        {
                            ImgId = imgId,
                            ImagePath = readerAll.IsDBNull(7) ? string.Empty : readerAll.GetString(7),
                            FolderId = readerAll.GetInt64(8),
                            Mileage = readerAll.IsDBNull(9) ? string.Empty : readerAll.GetString(9),
                            ImageData = readerAll.IsDBNull(10) ? null : (byte[])readerAll.GetValue(10),
                            Annotations = new List<float[]>()
                        };

                        weldDict[imgId] = weldInfo;
                    }

                    var annotation = new float[6];
                    annotation[0] = readerAll.IsDBNull(1) ? 0 : readerAll.GetFloat(1);
                    annotation[1] = readerAll.IsDBNull(2) ? 0 : readerAll.GetFloat(2);
                    annotation[2] = readerAll.IsDBNull(3) ? 0 : readerAll.GetFloat(3);
                    annotation[3] = readerAll.IsDBNull(4) ? 0 : readerAll.GetFloat(4);
                    annotation[4] = readerAll.IsDBNull(5) ? 0 : readerAll.GetInt32(5);
                    annotation[5] = readerAll.IsDBNull(6) ? 0 : (float)readerAll.GetDouble(6);

                    weldInfo.Annotations.Add(annotation);
                }

                return weldDict.Values.ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetWeldPositionsByRouteAndCycle: {ex.Message}");
                return new List<WeldPositionInfo>();
            }
        }

        /// <summary>
        /// 根据线路、方向和周期号获取焊缝位置信息
        /// </summary>
        /// <param name="routeLine"></param>
        /// <param name="direction"></param>
        /// <param name="cycleNumber"></param>
        /// <returns></returns>
        public List<WeldPositionInfo> GetWeldPositionsByRouteAndDirectionAndCycle(
    string? selectedRouteLine,
    string? selectedUpOrDown,
    string? selectedRailType,
    int weldInterval,
    string? StartMileage,
    string? EndMileage
    )
        {
            // 强制版：线别、上下行、股别、周期、起止里程都必须有效
            if (string.IsNullOrWhiteSpace(selectedRouteLine) ||
                string.IsNullOrWhiteSpace(selectedUpOrDown) ||
                string.IsNullOrWhiteSpace(selectedRailType) ||
                weldInterval <= 0 ||
                string.IsNullOrWhiteSpace(StartMileage) ||
                string.IsNullOrWhiteSpace(EndMileage))
            {
                return new List<WeldPositionInfo>();
            }

            // 先把传入的起止里程转成数字，方便比较区间
            if (!TryGetMileageRange(StartMileage, EndMileage, out var queryStartMileage, out var queryEndMileage))
            {
                return new List<WeldPositionInfo>();
            }

            try
            {
                EnsureConnectionOpen();

                const string getImageIdsQuery = @"
SELECT DISTINCT 
    da.ImageId,
    df.StartMileage,
    df.EndMileage
FROM DamageAnnotations da
INNER JOIN Images i ON da.ImageId = i.ImageId
INNER JOIN DamageFolders df ON i.FolderId = df.FolderId
WHERE df.SelectedRouteLine = @SelectedRouteLine
  AND df.SelectedUpOrDown = @SelectedUpOrDown
  AND df.SelectedRailType = @SelectedRailType
  AND df.CycleNumber = @WeldInterval
  AND da.DamageType IN (52, 2, 6);";

                var imageIdSet = new HashSet<long>();

                using (var command = new SqliteCommand(getImageIdsQuery, _connection))
                {
                    command.Parameters.AddWithValue("@SelectedRouteLine", selectedRouteLine);
                    command.Parameters.AddWithValue("@SelectedUpOrDown", selectedUpOrDown);
                    command.Parameters.AddWithValue("@SelectedRailType", selectedRailType);
                    command.Parameters.AddWithValue("@WeldInterval", weldInterval);

                    using var reader = command.ExecuteReader();

                    while (reader.Read())
                    {
                        var imageId = reader.GetInt64(0);

                        var dbStartMileageText = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                        var dbEndMileageText = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);

                        // 把数据库里的起止里程也转成数字
                        if (!TryGetMileageRange(dbStartMileageText, dbEndMileageText, out var dbStartMileage, out var dbEndMileage))
                        {
                            continue;
                        }

                        // 判断传入区间和数据库区间是否重合
                        var isOverlap = queryStartMileage <= dbEndMileage &&
                                        dbStartMileage <= queryEndMileage;

                        if (isOverlap)
                        {
                            imageIdSet.Add(imageId);
                        }
                    }
                }

                var imageIds = imageIdSet.ToList();

                if (imageIds.Count == 0)
                {
                    return new List<WeldPositionInfo>();
                }

                var parameters = new List<SqliteParameter>();
                var inClauses = new List<string>();

                for (int i = 0; i < imageIds.Count; i++)
                {
                    var paramName = $"@ImageId{i}";
                    inClauses.Add(paramName);
                    parameters.Add(new SqliteParameter(paramName, imageIds[i]));
                }

                var inClause = string.Join(",", inClauses);

                const string getAllAnnotationsQueryTemplate = @"
SELECT 
    da.ImageId,
    da.X,
    da.Y,
    da.Width,
    da.Height,
    da.DamageType,
    da.Confidence,
    i.ImgPath,
    i.FolderId,
    i.Mileage,
    i.ImageData
FROM DamageAnnotations da
INNER JOIN Images i ON da.ImageId = i.ImageId
WHERE da.ImageId IN ({0})
ORDER BY da.ImageId;";

                var getAllAnnotationsQuery = string.Format(getAllAnnotationsQueryTemplate, inClause);

                using var allCommand = new SqliteCommand(getAllAnnotationsQuery, _connection);
                allCommand.Parameters.AddRange(parameters.ToArray());

                using var readerAll = allCommand.ExecuteReader();

                var weldDict = new Dictionary<long, WeldPositionInfo>();

                while (readerAll.Read())
                {
                    var imgId = readerAll.GetInt64(0);

                    if (!weldDict.TryGetValue(imgId, out var weldInfo))
                    {
                        weldInfo = new WeldPositionInfo
                        {
                            ImgId = imgId,
                            ImagePath = readerAll.IsDBNull(7) ? string.Empty : readerAll.GetString(7),
                            FolderId = readerAll.GetInt64(8),
                            Mileage = readerAll.IsDBNull(9) ? string.Empty : readerAll.GetString(9),
                            ImageData = readerAll.IsDBNull(10) ? null : (byte[])readerAll.GetValue(10),
                            Annotations = new List<float[]>()
                        };

                        weldDict[imgId] = weldInfo;
                    }

                    var annotation = new float[6];
                    annotation[0] = readerAll.IsDBNull(1) ? 0 : readerAll.GetFloat(1);
                    annotation[1] = readerAll.IsDBNull(2) ? 0 : readerAll.GetFloat(2);
                    annotation[2] = readerAll.IsDBNull(3) ? 0 : readerAll.GetFloat(3);
                    annotation[3] = readerAll.IsDBNull(4) ? 0 : readerAll.GetFloat(4);
                    annotation[4] = readerAll.IsDBNull(5) ? 0 : readerAll.GetInt32(5);
                    annotation[5] = readerAll.IsDBNull(6) ? 0 : (float)readerAll.GetDouble(6);

                    weldInfo.Annotations.Add(annotation);
                }

                return weldDict.Values.ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetWeldPositionsByRouteAndDirectionAndCycle: {ex.Message}");
                return new List<WeldPositionInfo>();
            }
        }
        /// <summary>
        /// 根据图片路径获取图片信息
        /// </summary>
        /// <param name="imagePath"></param>
        /// <returns></returns>
        public async Task<SqlImgInfo> GetImagesByImagePathAsync(string imagePath)
        {
            try
            {
                EnsureConnectionOpen();
                const string query = @"
            SELECT ImageId, ImgPath, FolderId, IsConfirmDamage, Remark, ImageData, Mileage, SaveTime
            FROM Images
            WHERE ImgPath = @ImgPath
            LIMIT 1;";
                using var command = new SqliteCommand(query, _connection);
                command.Parameters.AddWithValue("@ImgPath", imagePath);
                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return new SqlImgInfo
                    {
                        ImgId = reader.GetInt64(0), // ImgId
                        ImgPath = reader.GetString(1), // ImgPath
                        FolderId = reader.GetInt64(2), // FolderId
                        IsConfirmDamage = reader.IsDBNull(3) ? null : reader.GetBoolean(3), // IsConfirmDamage
                        Remark = reader.IsDBNull(4) ? null : reader.GetString(4), // Remark
                        ImageData = reader.IsDBNull(5) ? null : (byte[])reader["ImageData"],//ImageData
                        Mileage = reader.IsDBNull(6) ? null : reader.GetString(6),
                        SaveTime = reader.IsDBNull(7) ? DateTime.MinValue : reader.GetDateTime(7) // SaveTime
                    };
                }
                else
                {
                    return null;
                }
            }
            catch (SqliteException ex)
            {
                _errorInfo = ex.Message;
                return null;
            }
        }

        /// <summary>
        /// 更新指定图片标注的损伤类型
        /// </summary>
        /// <param name="imgId"></param>
        /// <param name="rectX"></param>
        /// <param name="rectY"></param>
        /// <param name="result"></param>
        /// <returns></returns>
        public async Task UpdateDamageAnnotationAsync(long imgId, int rectX, int rectY, float result)
        {
            EnsureConnectionOpen();
            const string query = @"
            UPDATE DamageAnnotations
            SET DamageType = @DamageType,
                UpdatedTime = @UpdatedTime
            WHERE ImageId = @ImageId AND X = @X AND Y = @Y;";  // 修正列名：RectX → X, RectY → Y

            var parameters = new Dictionary<string, object?>
            {
                { "@ImageId", imgId },
                { "@X", rectX },        // 修正参数名：@RectX → @X
                { "@Y", rectY },        // 修正参数名：@RectY → @Y
                { "@DamageType", result },
                { "@UpdatedTime", DateTime.Now }
            };
            await Task.Run(() => ExecuteNonQuery(query, parameters));
        }


        /// <summary>
        /// 更新图片信息
        /// </summary>
        /// <param name="sqlImgInfo"></param>
        /// <returns></returns>
        public async Task UpdateSqlImgInfoAsync(SqlImgInfo sqlImgInfo)
        {
            EnsureConnectionOpen();
            const string query = @"
    UPDATE Images
    SET ImgPath = @ImgPath,
        FolderId = @FolderId,
        IsConfirmDamage = @IsConfirmDamage,
        Remark = @Remark,
        ImageData = @ImageData,
        Mileage = @Mileage,
        SaveTime = @SaveTime
    WHERE ImageId = @ImageId;";

            var parameters = new Dictionary<string, object?>
    {
        { "@ImageId", sqlImgInfo.ImgId },
        { "@ImgPath", sqlImgInfo.ImgPath },
        { "@FolderId", sqlImgInfo.FolderId },
        { "@IsConfirmDamage", sqlImgInfo.IsConfirmDamage },
        { "@Remark", sqlImgInfo.Remark ?? (object)DBNull.Value },
        { "@ImageData", sqlImgInfo.ImageData ?? (object)DBNull.Value },
        { "@Mileage", sqlImgInfo.Mileage ?? (object)DBNull.Value },
        { "@SaveTime", sqlImgInfo.SaveTime }
    };
            await Task.Run(() => ExecuteNonQuery(query, parameters));
        }

        /// <summary>
        /// 根据文件夹名称删除该文件夹下的所有图片及其标注记录
        /// </summary>
        /// <param name="folderName"></param>
        public void DeleteImagesAndAnnotationsByFolder(string folderName)
        {
            try
            {
                EnsureConnectionOpen();
                // 1. 获取文件夹ID
                long folderId = GetFolderIdByRailWayName(folderName);
                if (folderId == -1)
                {
                    Console.WriteLine($"未找到文件夹: {folderName}");
                    return;
                }
                // 2. 获取该文件夹下的所有图片ID
                var imageIds = new List<long>();
                const string selectImagesQuery = @"
                SELECT ImageId
                FROM Images
                WHERE FolderId = @FolderId;";
                using (var selectCommand = new SqliteCommand(selectImagesQuery, _connection))
                {
                    selectCommand.Parameters.AddWithValue("@FolderId", folderId);
                    using (var reader = selectCommand.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            imageIds.Add(reader.GetInt64(0));
                        }
                    }
                }
                // 3. 删除所有标注记录
                const string deleteAnnotationsQuery = @"
                DELETE FROM DamageAnnotations
                WHERE ImageId = @ImageId;";
                using (var deleteAnnotationCommand = new SqliteCommand(deleteAnnotationsQuery, _connection))
                {
                    deleteAnnotationCommand.Parameters.Add("@ImageId", SqliteType.Integer);
                    foreach (var imageId in imageIds)
                    {
                        deleteAnnotationCommand.Parameters["@ImageId"].Value = imageId;
                        deleteAnnotationCommand.ExecuteNonQuery();
                    }
                }
                // 4. 删除所有图片记录
                const string deleteImagesQuery = @"
                DELETE FROM Images
                WHERE FolderId = @FolderId;";
                using (var deleteImageCommand = new SqliteCommand(deleteImagesQuery, _connection))
                {
                    deleteImageCommand.Parameters.AddWithValue("@FolderId", folderId);
                    deleteImageCommand.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                _errorInfo = $"删除文件夹下的图片及标注记录失败: {ex.Message}";
            }
        }

        /// <summary>
        /// 根据图片ID和坐标删除指定的损伤标注记录
        /// </summary>
        /// <param name="imgId"></param>
        /// <param name="deleteDamagePoint"></param>
        /// <returns></returns>
        public async Task DeleteDamageAnnotationByCoordinatesAsync(long imgId, float[]? deleteDamagePoint)
        {
            try
            {
                EnsureConnectionOpen();
                const string query = @"
            DELETE FROM DamageAnnotations
            WHERE ImageId = @ImageId AND X = @X AND Y = @Y;";
                var parameters = new Dictionary<string, object?>
                {
                    { "@ImageId", imgId },
                    { "@X", deleteDamagePoint != null && deleteDamagePoint.Length > 0 ? deleteDamagePoint[0] : 0 },
                    { "@Y", deleteDamagePoint != null && deleteDamagePoint.Length > 1 ? deleteDamagePoint[1] : 0 }
                };
                await Task.Run(() => ExecuteNonQuery(query, parameters));
            }
            catch (Exception ex)
            {
                _errorInfo = ex.Message;
            }
        }

        /// <summary>
        /// 根据图片ID删除该图片的所有损伤标注记录
        /// </summary>
        /// <param name="imgId"></param>
        /// <returns></returns>
        public async Task DeleteAllDamageAnnotationsByImgIdAsync(long imgId)
        {
            try
            {
                EnsureConnectionOpen();
                const string query = @"
                DELETE FROM DamageAnnotations
                WHERE ImageId = @ImageId;";
                var parameters = new Dictionary<string, object?>
                {
                    { "@ImageId", imgId }
                };
                await Task.Run(() => ExecuteNonQuery(query, parameters));
            }
            catch (Exception ex)
            {
                _errorInfo = ex.Message;
            }
        }

        /// <summary>
        /// 根据文件夹路径获取SelectedRouteLine（线别）
        /// </summary>
        /// <param name="folderPath">文件夹路径</param>
        /// <returns>返回线别，找不到返回null</returns>
        public string GetSelectedRouteLineByFolderPath(string folderPath)
        {
            try
            {
                EnsureConnectionOpen();

                string query = @"
                    SELECT SelectedRouteLine
                    FROM DamageFolders
                    WHERE FolderPath = @FolderPath
                    LIMIT 1";

                using var command = new SqliteCommand(query, _connection);
                command.Parameters.AddWithValue("@FolderPath", folderPath);

                var result = command.ExecuteScalar();

                if (result != null && result != DBNull.Value)
                {
                    return result.ToString();
                }

                return null;
            }
            catch (SqliteException ex)
            {
                _errorInfo = ex.Message;
                Console.WriteLine($"GetSelectedRouteLineByFolderPath 错误: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 根据文件夹路径获取SelectedUpOrDown（上下行）
        /// </summary>
        /// <param name="folderPath">文件夹路径</param>
        /// <returns>返回上下行，找不到返回null</returns>
        public string GetSelectedUpOrDownByFolderPath(string folderPath)
        {
            try
            {
                EnsureConnectionOpen();

                string query = @"
            SELECT SelectedUpOrDown
            FROM DamageFolders
            WHERE FolderPath = @FolderPath
            LIMIT 1";

                using var command = new SqliteCommand(query, _connection);
                command.Parameters.AddWithValue("@FolderPath", folderPath);

                var result = command.ExecuteScalar();

                if (result != null && result != DBNull.Value)
                {
                    return result.ToString();
                }

                return null;
            }
            catch (SqliteException ex)
            {
                _errorInfo = ex.Message;
                Console.WriteLine($"GetSelectedUpOrDownByFolderPath 错误: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 根据文件夹路径获取CycleNumber（周期号）
        /// </summary>
        /// <param name="folderPath">文件夹路径</param>
        /// <returns>返回周期号，找不到返回null</returns>
        public int GetCycleNumberByFolderPath(string folderPath)
        {
            try
            {
                EnsureConnectionOpen();

                string query = @"
                    SELECT CycleNumber
                    FROM DamageFolders
                    WHERE FolderPath = @FolderPath
                    LIMIT 1";

                using var command = new SqliteCommand(query, _connection);
                command.Parameters.AddWithValue("@FolderPath", folderPath);

                var result = command.ExecuteScalar();

                if (result != null && result != DBNull.Value)
                {
                    if (int.TryParse(result.ToString(), out int cycleNumber))
                    {
                        return cycleNumber;
                    }
                }

                return 0;
            }
            catch (SqliteException ex)
            {
                _errorInfo = ex.Message;
                Console.WriteLine($"GetCycleNumberByFolderPath 错误: {ex.Message}");
                return 0;
            }
        }

        public string GetSelectedRailTypeByFolderPath(string folderPath)
        {
            try
            {
                EnsureConnectionOpen();

                string query = @"
            SELECT SelectedRailType
            FROM DamageFolders
            WHERE FolderPath = @FolderPath
            LIMIT 1";

                using var command = new SqliteCommand(query, _connection);
                command.Parameters.AddWithValue("@FolderPath", folderPath);

                var result = command.ExecuteScalar();

                if (result != null && result != DBNull.Value)
                {
                    return result.ToString();
                }

                return null;
            }
            catch (SqliteException ex)
            {
                _errorInfo = ex.Message;
                Console.WriteLine($"GetSelectedUpOrDownByFolderPath 错误: {ex.Message}");
                return null;
            }
        }


        public string GetSerialNumberByFolderPath(string folderPath)
        {
            try
            {
                EnsureConnectionOpen();

                string query = @"
            SELECT SerialNumber
            FROM DamageFolders
            WHERE FolderPath = @FolderPath
            LIMIT 1";

                using var command = new SqliteCommand(query, _connection);
                command.Parameters.AddWithValue("@FolderPath", folderPath);

                var result = command.ExecuteScalar();

                if (result != null && result != DBNull.Value)
                {
                    return result.ToString();
                }

                return null;
            }
            catch (SqliteException ex)
            {
                _errorInfo = ex.Message;
                Console.WriteLine($"GetSelectedUpOrDownByFolderPath 错误: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 更新图片的损伤等级
        /// </summary>
        /// <param name="imgId"></param>
        /// <param name="damageLevel"></param>
        /// <returns></returns>
        internal int UpdateDamageLevel(long imgId, int? damageLevel)
        {
            try
            {
                EnsureConnectionOpen();
                const string query = @"
            UPDATE Images
            SET DamageLevel = @DamageLevel
            WHERE ImageId = @ImageId;";
                var parameters = new Dictionary<string, object?>
                    {
                    { "@ImageId", imgId },
                    { "@DamageLevel", damageLevel ?? (object)DBNull.Value }
                };
                return ExecuteNonQuery(query, parameters);
            }
            catch (SqliteException ex)
            {
                _errorInfo = ex.Message;
                return -1;
            }
        }

        private static bool TryGetMileageRange(
    string? startMileageText,
    string? endMileageText,
    out double startMileage,
    out double endMileage)
        {
            startMileage = 0;
            endMileage = 0;

            if (!TryParseMileageToMeter(startMileageText, out startMileage))
            {
                return false;
            }

            if (!TryParseMileageToMeter(endMileageText, out endMileage))
            {
                return false;
            }

            // 防止用户传反，比如 start 比 end 大
            if (startMileage > endMileage)
            {
                var temp = startMileage;
                startMileage = endMileage;
                endMileage = temp;
            }

            return true;
        }

        private static bool TryParseMileageToMeter(string? mileageText, out double meter)
        {
            meter = 0;

            if (string.IsNullOrWhiteSpace(mileageText))
            {
                return false;
            }

            var text = mileageText
                .Trim()
                .ToUpper()
                .Replace(" ", "");

            // 格式1：K123+456 或 DK123+456
            var match = System.Text.RegularExpressions.Regex.Match(
                text,
                @"[A-Z]*K(?<km>\d+(\.\d+)?)\+(?<m>\d+(\.\d+)?)");

            if (match.Success)
            {
                var km = double.Parse(match.Groups["km"].Value);
                var m = double.Parse(match.Groups["m"].Value);
                meter = km * 1000 + m;
                return true;
            }

            // 格式2：123+456
            match = System.Text.RegularExpressions.Regex.Match(
                text,
                @"^(?<km>\d+(\.\d+)?)\+(?<m>\d+(\.\d+)?)$");

            if (match.Success)
            {
                var km = double.Parse(match.Groups["km"].Value);
                var m = double.Parse(match.Groups["m"].Value);
                meter = km * 1000 + m;
                return true;
            }

            // 格式3：123KM456M 或 0000KM280M
            match = System.Text.RegularExpressions.Regex.Match(
                text,
                @"(?<km>\d+(\.\d+)?)KM(?<m>\d+(\.\d+)?)M?");

            if (match.Success)
            {
                var km = double.Parse(match.Groups["km"].Value);
                var m = double.Parse(match.Groups["m"].Value);
                meter = km * 1000 + m;
                return true;
            }

            // 格式4：纯数字，默认认为已经是米
            if (double.TryParse(text, out var value))
            {
                meter = value;
                return true;
            }

            return false;
        }

    }
}


