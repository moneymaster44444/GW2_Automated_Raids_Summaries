using System.Diagnostics;
using GW2RaidsGui.Pipeline;

namespace GW2RaidsGui;

/// <summary>
/// Wraps the existing release scripts so the GUI can check for and apply updates:
/// Check-Latest-Release.ps1 (quiet version check) and Update-FromRelease.ps1
/// (download + replace, run non-interactively with -Yes).
/// </summary>
public sealed class UpdateService
{
    private readonly AppPaths _paths;

    public UpdateService(AppPaths paths) => _paths = paths;

    public sealed record CheckResult(string Current, string? Latest, bool UpdateAvailable, string? Error);

    public async Task<CheckResult> CheckAsync(CancellationToken ct)
    {
        var current = _paths.ReadVersion();
        var lines = new List<string>();
        try
        {
            var psi = BuildPowerShell(_paths.CheckReleaseScript, "-CurrentVersion", current);
            await ProcessStreamer.RunAsync(psi, l => { lock (lines) lines.Add(l); }, ct);

            string? latest = null;
            var available = false;
            lock (lines)
            {
                foreach (var line in lines)
                {
                    var t = line.Trim();
                    if (t.StartsWith("LATEST=", StringComparison.OrdinalIgnoreCase))
                        latest = t["LATEST=".Length..].Trim();
                    else if (t.StartsWith("UPDATE_AVAILABLE=", StringComparison.OrdinalIgnoreCase))
                        available = t.EndsWith("1", StringComparison.Ordinal);
                }
            }

            if (string.IsNullOrEmpty(latest))
                return new CheckResult(current, null, false, "Could not reach GitHub (no response).");
            return new CheckResult(current, latest, available, null);
        }
        catch (Exception ex)
        {
            return new CheckResult(current, null, false, ex.Message);
        }
    }

    /// <summary>Runs Update-FromRelease.ps1 -Yes, streaming progress. Returns its exit code.</summary>
    public Task<int> RunUpdateAsync(Action<string> onOutput, CancellationToken ct)
    {
        var psi = BuildPowerShell(_paths.UpdateScript, "-InstallRoot", _paths.RepoRoot, "-Yes");
        return ProcessStreamer.RunAsync(psi, onOutput, ct);
    }

    private ProcessStartInfo BuildPowerShell(string scriptPath, params string[] scriptArgs)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            WorkingDirectory = _paths.RepoRoot,
        };
        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-ExecutionPolicy");
        psi.ArgumentList.Add("Bypass");
        psi.ArgumentList.Add("-File");
        psi.ArgumentList.Add(scriptPath);
        foreach (var a in scriptArgs) psi.ArgumentList.Add(a);
        return psi;
    }
}
