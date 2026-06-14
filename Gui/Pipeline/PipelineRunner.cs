using System.Diagnostics;

namespace GW2RaidsGui.Pipeline;

public enum RunMode
{
    /// <summary>Process the logs currently in Raid_Logs (--manual).</summary>
    Manual,
    /// <summary>Force the raid-window auto-copy from LOG_SOURCE_DIR (--scheduled).</summary>
    Scheduled,
    /// <summary>Run first-time setup only, then exit without processing (--setup).</summary>
    Setup,
}

/// <summary>
/// Launches <c>process_logs.bat</c> with the requested run mode and streams its
/// console output line by line. Output callbacks fire on a background thread;
/// the caller is responsible for marshalling to the UI thread.
/// </summary>
public sealed class PipelineRunner
{
    private readonly AppPaths _paths;
    private Process? _process;

    public PipelineRunner(AppPaths paths) => _paths = paths;

    public bool IsRunning => _process is { HasExited: false };

    public async Task<int> RunAsync(RunMode mode, Action<string> onOutput, CancellationToken ct)
    {
        if (IsRunning)
            throw new InvalidOperationException("A pipeline run is already in progress.");

        var flag = mode switch
        {
            RunMode.Manual => "--manual",
            RunMode.Scheduled => "--scheduled",
            RunMode.Setup => "--setup",
            _ => "--manual",
        };
        // cmd /c "<"bat path"> <flag>" - the doubled quotes are the documented
        // way to pass a quoted program plus arguments through cmd.exe.
        var inner = $"\"{_paths.ProcessLogsBat}\" {flag}";

        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c \"{inner}\"",
            WorkingDirectory = _paths.RepoRoot,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        process.OutputDataReceived += (_, e) => { if (e.Data != null) onOutput(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) onOutput(e.Data); };
        process.Exited += (_, _) => tcs.TrySetResult(process.ExitCode);

        _process = process;
        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await using (ct.Register(TryKill))
            {
                return await tcs.Task.ConfigureAwait(false);
            }
        }
        finally
        {
            process.Dispose();
            _process = null;
        }
    }

    /// <summary>Terminates the running pipeline and its child processes.</summary>
    public void Cancel() => TryKill();

    private void TryKill()
    {
        try
        {
            var p = _process;
            if (p is { HasExited: false })
                p.Kill(entireProcessTree: true);
        }
        catch
        {
            // Best effort: the process may have exited between the check and kill.
        }
    }
}
