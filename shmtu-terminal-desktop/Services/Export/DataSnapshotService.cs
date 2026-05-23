using System.IO;
using System.IO.Compression;
using shmtu.terminal.desktop.Database.Common;
using shmtu.terminal.desktop.Services.Config;

namespace shmtu.terminal.desktop.Services.Export;

/// <summary>
/// 数据快照服务 — 创建/恢复/管理数据快照
/// 快照内容：整个 Data/ 目录（排除 models 和 export）
/// 快照格式：ZIP 压缩包
/// </summary>
public static class DataSnapshotService
{
    /// <summary>
    /// 快照目录
    /// </summary>
    private static readonly string SnapshotDirectory = Path.Combine(BaseDbSource.DataDirectoryPath, "snapshot");

    /// <summary>
    /// 创建快照
    /// </summary>
    public static async Task<string> CreateSnapshotAsync(CancellationToken cancellationToken = default)
    {
        EnsureSnapshotDirectoryExists();

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        var snapshotFileName = $"{timestamp}.zip";
        var snapshotPath = Path.Combine(SnapshotDirectory, snapshotFileName);

        var dataDir = BaseDbSource.DataDirectoryPath;
        if (!Directory.Exists(dataDir))
        {
            throw new DirectoryNotFoundException($"数据目录不存在: {dataDir}");
        }

        await Task.Run(() =>
        {
            using var zip = ZipFile.Open(snapshotPath, ZipArchiveMode.Create);

            // 排除的目录
            var excludeDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "models", "export", "snapshot",
            };

            // 排除的文件
            var excludeFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".gitkeep",
            };

            // 遍历所有文件
            foreach (var file in Directory.EnumerateFiles(dataDir, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var relativePath = Path.GetRelativePath(dataDir, file);

                // 检查是否在排除目录中
                var pathParts = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (pathParts.Any(p => excludeDirs.Contains(p))) continue;

                // 检查是否排除文件
                if (excludeFiles.Contains(Path.GetFileName(file))) continue;

                zip.CreateEntryFromFile(file, relativePath, CompressionLevel.Optimal);
            }
        }, cancellationToken);

        // 清理过期快照
        CleanupOldSnapshots();

        return snapshotPath;
    }

    /// <summary>
    /// 恢复快照
    /// </summary>
    public static async Task RestoreSnapshotAsync(string snapshotPath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(snapshotPath))
        {
            throw new FileNotFoundException($"快照文件不存在: {snapshotPath}");
        }

        var dataDir = BaseDbSource.DataDirectoryPath;

        await Task.Run(() =>
        {
            // 先备份当前数据（防止恢复失败导致数据丢失）
            var tempBackupDir = dataDir + "_temp_backup";
            if (Directory.Exists(dataDir))
            {
                if (Directory.Exists(tempBackupDir))
                    Directory.Delete(tempBackupDir, true);
                Directory.Move(dataDir, tempBackupDir);
            }

            try
            {
                // 解压快照
                Directory.CreateDirectory(dataDir);
                ZipFile.ExtractToDirectory(snapshotPath, dataDir, true);

                // 删除临时备份
                if (Directory.Exists(tempBackupDir))
                {
                    Directory.Delete(tempBackupDir, true);
                }
            }
            catch
            {
                // 恢复失败，还原临时备份
                if (Directory.Exists(dataDir))
                    Directory.Delete(dataDir, true);
                if (Directory.Exists(tempBackupDir))
                    Directory.Move(tempBackupDir, dataDir);
                throw;
            }
        }, cancellationToken);
    }

    /// <summary>
    /// 获取所有快照列表
    /// </summary>
    public static List<SnapshotInfo> GetSnapshotList()
    {
        EnsureSnapshotDirectoryExists();

        var snapshots = new List<SnapshotInfo>();

        foreach (var file in Directory.GetFiles(SnapshotDirectory, "*.zip")
                     .OrderByDescending(f => File.GetCreationTime(f)))
        {
            var fileInfo = new FileInfo(file);
            snapshots.Add(new SnapshotInfo
            {
                FileName = fileInfo.Name,
                FilePath = fileInfo.FullName,
                CreatedTime = fileInfo.CreationTime,
                SizeBytes = fileInfo.Length,
                SizeText = FormatFileSize(fileInfo.Length),
            });
        }

        return snapshots;
    }

    /// <summary>
    /// 删除快照
    /// </summary>
    public static bool DeleteSnapshot(string snapshotPath)
    {
        if (!File.Exists(snapshotPath)) return false;
        File.Delete(snapshotPath);
        return true;
    }

    /// <summary>
    /// 清理过期快照（保留最近 N 个）
    /// </summary>
    public static void CleanupOldSnapshots()
    {
        var appConfig = TomlConfigService.LoadAppConfig();
        var keepCount = appConfig.Data.SnapshotKeepCount;

        var snapshots = GetSnapshotList();
        if (snapshots.Count <= keepCount) return;

        var toDelete = snapshots.Skip(keepCount);
        foreach (var snapshot in toDelete)
        {
            try
            {
                File.Delete(snapshot.FilePath);
            }
            catch
            {
                // 忽略删除失败
            }
        }
    }

    private static void EnsureSnapshotDirectoryExists()
    {
        if (!Directory.Exists(SnapshotDirectory))
        {
            Directory.CreateDirectory(SnapshotDirectory);
        }
    }

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB"];
        var order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }
}

/// <summary>
/// 快照信息
/// </summary>
public class SnapshotInfo
{
    public string FileName { get; set; } = "";
    public string FilePath { get; set; } = "";
    public DateTime CreatedTime { get; set; }
    public long SizeBytes { get; set; }
    public string SizeText { get; set; } = "";
}
