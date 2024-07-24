using System;
using Microsoft.Data.Sqlite;
using SqlSugar;

namespace shmtu.terminal.desktop.Database.Common;

/// <summary>
/// Common class for database operations
/// 数据库操作的公共类
/// </summary>
public abstract class BaseDbSource
{
    public static readonly string DataDirectoryPath = "data";
    private static readonly string DbExtension = "sqlite";

    /// <summary>
    /// Database connection string
    /// 数据库连接字符串
    /// </summary>
    public readonly static string Connection = $"datasource={DataDirectoryPath}/shmtu.terminal.{DbExtension}";

    public static string Password = "";

    public string DatabaseFileBaseName = "shmtu.terminal";

    /// <summary>
    /// Get a new SqlSugarClient instance with specific configurations
    /// 获取具有特定配置的新 SqlSugarClient 实例
    /// </summary>
    /// <returns>SqlSugarClient instance</returns>
    public static SqlSugarClient GetNewDb(string connectionString = "")
    {
        if (connectionString == "")
        {
            connectionString = Connection;
        }

        if (Password != "")
        {
            connectionString = new SqliteConnectionStringBuilder(connectionString)
            {
                Mode = SqliteOpenMode.ReadWrite,
                Password = Password
            }.ToString();
        }

        var db = new SqlSugarClient(new ConnectionConfig()
            {
                IsAutoCloseConnection = true,
                DbType = DbType.Sqlite,
                ConnectionString = connectionString,
                LanguageType = LanguageType.Default //Set language
            },
            it =>
            {
                // Logging SQL statements and parameters before execution
                // 在执行前记录 SQL 语句和参数
                it.Aop.OnLogExecuting =
                    (sql, para)
                        =>
                    {
                        Console.WriteLine(UtilMethods.GetNativeSql(sql, para));
                    };
            });
        return db;
    }

    public static string GetAbsolutePath()
    {
        var connectionCfgList = Connection.Split(";");
        var path = "";

        foreach (var connectionCfg in connectionCfgList)
        {
            if (connectionCfg.Contains("datasource="))
            {
                path = connectionCfg.Replace("datasource=", "");
            }
        }

        return System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);
    }

    public SqlSugarClient GetNewDbObj()
    {
        var newConnection = $"datasource={DataDirectoryPath}/{DatabaseFileBaseName}.{DbExtension}";
        return GetNewDb(newConnection);
    }
}