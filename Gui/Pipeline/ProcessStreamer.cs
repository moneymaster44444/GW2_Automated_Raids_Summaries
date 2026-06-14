using System.Diagnostics;

namespace GW2RaidsGui.Pipeline;

/// <summary>
/// Runs a process and streams its stdout/stderr line by line to a callback,
/// returning the exit code. Cancelling the token kills the process tree.
/// Output callbacks fire on background threads - the caller marshals to the UI.
/// </summary>
internal static class ProcessStreamer
{
    public static async Task<int> RunAsync(ProcessStartInfo psi, Action<string> onOutput, CancellationToken ct)
    {
        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;

        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        process.OutputDataReceived += (_, e) => { if (e.Data != null) onOutput(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) onOutput(e.Data); };
        process.Exited += (_, _) => tcs.TrySetResult(process.ExitCode);

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await using (ct.Register(() =>
            {
                try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
                catch { /* already exited */ }
            }))
            {
                return await tcs.Task.ConfigureAwait(false);
            }
        }
        finally
        {
            process.Dispose();
        }
    }
}
