using DamageMaker.Models;
using Microsoft.Data.Sqlite;
using SQLitePCL;
using System;
using System.Collections.Generic;
using System.Data;
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
                        // 执行删除（完全删除记录）
                        string sql = "DELETE FROM Images WHERE FolderId = @folderId";
                        using (var cmd = new SqliteCommand(sql, _connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@folderId", folderId);
                            int affectedRows = cmd.ExecuteNonQuery();
                            string deleteFolder = "DELETE FROM DamageFolders WHERE FolderId = @folderId";
                            using (var cmdFolder = new SqliteCommand(deleteFolder, _connection, transaction))
                            {
                                cmdFolder.Parameters.AddWithValue("@folderId", folderId);
                                int rows = cmdFolder.ExecuteNonQuery();
                            }
                            if (affectedRows != count)
                            {
                                _lastErrorMessage = $"应删除 {count} 条，实际删除 {affectedRows} 条";
                                transaction.Rollback();
                                return false;
                            }
                        }

                        transaction.Commit();
                        return true;
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        _lastErrorMessage = ex.Message;
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
        /// 根据 FolderPath 查询对应的 FolderId
        /// </summary>
        /// <param name="folderPath">要查询的 FolderPath</param>
        /// <returns>返回对应的 FolderId，如果未找到则返回 -1</returns>
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

        public List<SqlImgInfo> GetImagesByFolderId(long folderId)
        {
            EnsureConnectionOpen();

            string query = "SELECT ImageId, ImgPath, FolderId, IsConfirmDamage, Remark,ImageData FROM Images WHERE FolderId = @FolderId";
            var command = Connection.CreateCommand();
            command.CommandText = query;
            command.Parameters.AddWithValue("@FolderId", folderId);

            var imgInfos = new List<SqlImgInfo>();

            try
            {
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        imgInfos.Add(new SqlImgInfo
                        {
                            ImgId = reader.GetInt64(0), // ImgId
                            ImgPath = reader.GetString(1), // ImgPath
                            FolderId = reader.GetInt64(2), // FolderId
                            IsConfirmDamage = reader.IsDBNull(3) ? null : reader.GetBoolean(3), // IsConfirmDamage
                            Remark = reader.IsDBNull(4) ? null : reader.GetString(4), // Remark
                            ImageData = reader.IsDBNull(5) ? null : (byte[])reader["ImageData"]//ImageData
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _errorInfo = ex.Message;
                Console.WriteLine($"Error fetching images: {_errorInfo}");
            }

            return imgInfos;
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

            string query = "SELECT ImageId, ImgPath, FolderId, IsConfirmDamage, Remark, ImageData FROM Images";
            var imgInfos = new List<SqlImgInfo>();

            try
            {
                using (var command = Connection.CreateCommand())
                {
                    command.CommandText = query;
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            imgInfos.Add(new SqlImgInfo
                            {
                                ImgId = reader.GetInt64(0), // ImageId
                                ImgPath = reader.GetString(1), // ImgPath
                                FolderId = reader.GetInt64(2), // FolderId
                                IsConfirmDamage = reader.IsDBNull(3) ? null : reader.GetBoolean(3), // IsConfirmDamage
                                Remark = reader.IsDBNull(4) ? null : reader.GetString(4), // Remark
                                ImageData = reader.IsDBNull(5) ? null : (byte[])reader["ImageData"] // ImageData
                            });
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

        public string? GetFolderRemarkById(int folderId)
        {
            try
            {
                string sql = "SELECT Remark FROM DamageFolders WHERE FolderId = @FolderId LIMIT 1";
                using var cmd = new SqliteCommand(sql, _connection);
                cmd.Parameters.AddWithValue("@FolderId", folderId);
                object result = cmd.ExecuteScalar();
                return result != null && result != DBNull.Value ? result.ToString() : string.Empty;
            }
            catch (Exception ex)
            {
                _errorInfo = ex.Message;
                return string.Empty;
            }
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

    }



}


