namespace GW2RaidsGui.Logs;

/// <summary>
/// Manages the contents of the <c>Raid_Logs</c> folder: listing the arcDPS log
/// files, copying dropped files/folders in, and removing them.
/// </summary>
public sealed class RaidLogsManager
{
    // arcDPS writes .zevtc (compressed) by default; .evtc is the uncompressed variant.
    public static readonly string[] LogExtensions = { ".zevtc", ".evtc" };

    private readonly string _dir;

    public RaidLogsManager(AppPaths paths) => _dir = paths.RaidLogsDir;

    public string Directory => _dir;

    public void EnsureDir()
    {
        if (!System.IO.Directory.Exists(_dir))
            System.IO.Directory.CreateDirectory(_dir);
    }

    private static bool IsLog(string path)
        => LogExtensions.Contains(System.IO.Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    /// <summary>Current log files in Raid_Logs, newest first.</summary>
    public List<FileInfo> List()
    {
        EnsureDir();
        return new DirectoryInfo(_dir)
            .EnumerateFiles()
            .Where(f => IsLog(f.Name))
            .OrderByDescending(f => f.LastWriteTime)
            .ToList();
    }

    public sealed record CopyResult(int Copied, int Skipped, List<string> Errors);

    /// <summary>
    /// Copies dropped paths into Raid_Logs. Directories are searched recursively
    /// for log files. Non-log files are ignored. A file already present with the
    /// same name and size is skipped; otherwise it is overwritten.
    /// </summary>
    public CopyResult CopyIn(IEnumerable<string> droppedPaths)
    {
        EnsureDir();
        int copied = 0, skipped = 0;
        var errors = new List<string>();

        foreach (var src in ExpandToLogFiles(droppedPaths))
        {
            try
            {
                var dest = System.IO.Path.Combine(_dir, System.IO.Path.GetFileName(src));
                if (File.Exists(dest))
                {
                    var s = new FileInfo(src);
                    var d = new FileInfo(dest);
                    if (s.Length == d.Length)
                    {
                        skipped++;
                        continue;
                    }
                }
                File.Copy(src, dest, overwrite: true);
                copied++;
            }
            catch (Exception ex)
            {
                errors.Add($"{System.IO.Path.GetFileName(src)}: {ex.Message}");
            }
        }

        return new CopyResult(copied, skipped, errors);
    }

    private static IEnumerable<string> ExpandToLogFiles(IEnumerable<string> paths)
    {
        foreach (var p in paths)
        {
            if (System.IO.Directory.Exists(p))
            {
                foreach (var f in System.IO.Directory.EnumerateFiles(p, "*", SearchOption.AllDirectories))
                    if (IsLog(f)) yield return f;
            }
            else if (File.Exists(p) && IsLog(p))
            {
                yield return p;
            }
        }
    }

    public sealed record DeleteResult(int Deleted, List<string> Errors);

    public DeleteResult Remove(IEnumerable<string> filePaths)
    {
        int deleted = 0;
        var errors = new List<string>();
        foreach (var path in filePaths)
        {
            try
            {
                File.Delete(path);
                deleted++;
            }
            catch (Exception ex)
            {
                errors.Add($"{System.IO.Path.GetFileName(path)}: {ex.Message}");
            }
        }
        return new DeleteResult(deleted, errors);
    }

    /// <summary>Deletes every .zevtc/.evtc file in Raid_Logs.</summary>
    public DeleteResult Clear() => Remove(List().Select(f => f.FullName).ToList());
}
