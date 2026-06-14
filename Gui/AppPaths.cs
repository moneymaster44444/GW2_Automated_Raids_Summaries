namespace GW2RaidsGui;

/// <summary>
/// Resolves the well-known paths in the repository the GUI operates on.
/// The repo root is the folder that contains <c>process_logs.bat</c>.
/// </summary>
public sealed class AppPaths
{
    public string RepoRoot { get; }

    public string ProcessLogsBat => Path.Combine(RepoRoot, "process_logs.bat");
    public string RaidLogsDir => Path.Combine(RepoRoot, "Raid_Logs");
    public string RaidsSummariesDir => Path.Combine(RepoRoot, "Raids_Summaries");
    public string ConfigFile => Path.Combine(RepoRoot, "config.txt");
    public string SampleConfigFile => Path.Combine(RepoRoot, "sample.config.txt");

    public string VersionFile => Path.Combine(RepoRoot, "VERSION");
    public string CheckReleaseScript => Path.Combine(RepoRoot, "Scripts", "Check-Latest-Release.ps1");
    public string UpdateScript => Path.Combine(RepoRoot, "Scripts", "Update-FromRelease.ps1");

    /// <summary>Reads the first non-blank line of the VERSION file, or "(unknown)".</summary>
    public string ReadVersion()
    {
        try
        {
            if (File.Exists(VersionFile))
            {
                foreach (var line in File.ReadAllLines(VersionFile))
                    if (!string.IsNullOrWhiteSpace(line)) return line.Trim();
            }
        }
        catch
        {
            // Fall through to the unknown sentinel.
        }
        return "(unknown)";
    }

    private string EiRootDir => Path.Combine(RepoRoot, "Resources", "Elite Insights");
    private string EiCliDir => Path.Combine(EiRootDir, "GW2EI.bin", "Release", "CLI");
    public string EliteInsightsConf => Path.Combine(RepoRoot, "Resources", "Config", "EliteInsights.conf");
    public string TopStatsIni => Path.Combine(RepoRoot, "Resources", "Config", "top_stats_config.ini");

    /// <summary>True once the Elite Insights CLI executable has been built.</summary>
    public bool IsEiCliBuilt()
    {
        string[] names = { "GW2EIParserCLI.exe", "GuildWars2EliteInsights-CLI.exe" };
        if (Directory.Exists(EiCliDir) && names.Any(n => File.Exists(Path.Combine(EiCliDir, n))))
            return true;
        if (Directory.Exists(EiRootDir))
        {
            try
            {
                return Directory.EnumerateFiles(EiRootDir, "*CLI*.exe", SearchOption.AllDirectories).Any();
            }
            catch
            {
                // Fall through to "not built" on any enumeration error.
            }
        }
        return false;
    }

    /// <summary>
    /// True when the one-time setup (EI CLI build + generated config files) has
    /// not completed yet - i.e. this looks like a first run.
    /// </summary>
    public bool IsSetupNeeded()
        => !IsEiCliBuilt() || !File.Exists(EliteInsightsConf) || !File.Exists(TopStatsIni);

    private AppPaths(string repoRoot) => RepoRoot = repoRoot;

    /// <summary>
    /// Locates the repo root. Preference order:
    /// 1. An explicit path passed on the command line (setup.bat passes the repo root).
    /// 2. Walking up from the executable's directory until process_logs.bat is found.
    /// 3. Walking up from the current working directory.
    /// Throws if no candidate contains process_logs.bat.
    /// </summary>
    public static AppPaths Resolve(string[] args)
    {
        var candidates = new List<string?>();
        if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
            candidates.Add(args[0]);

        var found = candidates
            .Select(NormalizeOrNull)
            .FirstOrDefault(p => p != null && File.Exists(Path.Combine(p, "process_logs.bat")));

        found ??= WalkUpForMarker(AppContext.BaseDirectory);
        found ??= WalkUpForMarker(Directory.GetCurrentDirectory());

        if (found == null)
        {
            throw new DirectoryNotFoundException(
                "Could not locate the repository root (the folder containing process_logs.bat). " +
                "Launch the GUI via setup.bat from the repository root.");
        }

        return new AppPaths(found);
    }

    private static string? NormalizeOrNull(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        try { return Path.GetFullPath(path.Trim()); }
        catch { return null; }
    }

    private static string? WalkUpForMarker(string start)
    {
        var dir = new DirectoryInfo(start);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "process_logs.bat")))
                return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }
}
