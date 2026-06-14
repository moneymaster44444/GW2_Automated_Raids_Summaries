using GW2RaidsGui.Config;
using GW2RaidsGui.Logs;
using GW2RaidsGui.Pipeline;

namespace GW2RaidsGui;

public partial class MainForm : Form
{
    private static readonly string[] DayNames =
        { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };
    private const int FridayIndex = 4;

    private readonly AppPaths _paths;
    private readonly RaidLogsManager _logs;
    private readonly PipelineRunner _runner;
    private readonly UpdateService _updateService;
    private ConfigFile _config;

    // Per-day state for round-tripping unmodified raid-hours values.
    private readonly RaidHoursValue[] _loadedDays = new RaidHoursValue[7];
    private readonly bool[] _dayDirty = new bool[7];

    private CancellationTokenSource? _cts;
    private bool _cancelRequested;
    private bool _setupChecked;
    private bool _logsRefreshedDuringRun;

    public MainForm(AppPaths paths)
    {
        _paths = paths;
        _logs = new RaidLogsManager(paths);
        _runner = new PipelineRunner(paths);
        _updateService = new UpdateService(paths);
        _config = ConfigFile.Load(paths);

        BuildUi();
        LoadConfigToUi();
        RefreshLogList();
        UpdateSummaryButton();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (_setupChecked) return;
        _setupChecked = true;

        if (!_paths.IsSetupNeeded()) return;

        using var dlg = new SetupForm(_paths);
        dlg.ShowDialog(this);
        // Setup may have created config files; refresh anything that depends on it.
        RefreshLogList();
        UpdateSummaryButton();
    }

    // ---------------------------------------------------------------- Logs ---

    private void RefreshLogList()
    {
        var files = _logs.List();
        lvLogs.BeginUpdate();
        lvLogs.Items.Clear();
        foreach (var f in files)
        {
            var item = new ListViewItem(f.Name) { Tag = f.FullName };
            item.SubItems.Add(FormatSize(f.Length));
            item.SubItems.Add(f.LastWriteTime.ToString("yyyy-MM-dd HH:mm"));
            lvLogs.Items.Add(item);
        }
        lvLogs.EndUpdate();

        lblLogCount.Text = files.Count switch
        {
            0 => "No logs in Raid_Logs.",
            1 => "1 log in Raid_Logs.",
            _ => $"{files.Count} logs in Raid_Logs.",
        };
        lblDropHint.Visible = files.Count == 0;
    }

    private static string FormatSize(long bytes)
    {
        if (bytes >= 1024L * 1024) return $"{bytes / (1024.0 * 1024):0.0} MB";
        if (bytes >= 1024) return $"{bytes / 1024.0:0.0} KB";
        return $"{bytes} B";
    }

    private void OnDragEnterLogs(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
            e.Effect = DragDropEffects.Copy;
    }

    private void OnDragDropLogs(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is not string[] dropped) return;
        var result = _logs.CopyIn(dropped);
        RefreshLogList();

        var parts = new List<string>();
        if (result.Copied > 0) parts.Add($"added {result.Copied}");
        if (result.Skipped > 0) parts.Add($"skipped {result.Skipped} duplicate");
        lblStatus.Text = parts.Count > 0
            ? "Logs " + string.Join(", ", parts) + "."
            : "No .zevtc/.evtc files found in the drop.";

        if (result.Errors.Count > 0)
            MessageBox.Show(string.Join(Environment.NewLine, result.Errors),
                "Some files could not be copied", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private void OnAddFiles(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Title = "Add arcDPS logs",
            Filter = "arcDPS logs (*.zevtc;*.evtc)|*.zevtc;*.evtc|All files (*.*)|*.*",
            Multiselect = true,
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        var result = _logs.CopyIn(dlg.FileNames);
        RefreshLogList();
        lblStatus.Text = $"Added {result.Copied} log(s).";
    }

    private void OnRemoveSelected(object? sender, EventArgs e)
    {
        var selected = lvLogs.SelectedItems.Cast<ListViewItem>()
            .Select(i => (string)i.Tag!).ToList();
        if (selected.Count == 0)
        {
            lblStatus.Text = "Select one or more logs to remove.";
            return;
        }
        var result = _logs.Remove(selected);
        RefreshLogList();
        lblStatus.Text = $"Removed {result.Deleted} log(s).";
        if (result.Errors.Count > 0)
            MessageBox.Show(string.Join(Environment.NewLine, result.Errors),
                "Some files could not be removed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private void OnClearFolder(object? sender, EventArgs e)
    {
        var count = _logs.List().Count;
        if (count == 0)
        {
            lblStatus.Text = "Raid_Logs is already empty.";
            return;
        }
        var confirm = MessageBox.Show(
            $"Delete all {count} log file(s) from Raid_Logs?",
            "Clear folder", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (confirm != DialogResult.Yes) return;

        var result = _logs.Clear();
        RefreshLogList();
        lblStatus.Text = $"Cleared {result.Deleted} log(s).";
    }

    // ------------------------------------------------------------- Running ---

    private async void OnRunManual(object? sender, EventArgs e)
        => await RunPipelineAsync(RunMode.Manual);

    private async void OnRunScheduled(object? sender, EventArgs e)
        => await RunPipelineAsync(RunMode.Scheduled);

    private async Task RunPipelineAsync(RunMode mode)
    {
        // Persist current settings first; the pipeline reads config.txt.
        if (!TrySaveConfig(out var saveError))
        {
            MessageBox.Show($"Could not save config.txt before running:\n{saveError}",
                "Save failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        if (mode == RunMode.Scheduled && string.IsNullOrWhiteSpace(_config.Get("LOG_SOURCE_DIR")))
        {
            MessageBox.Show(
                "Scheduled runs auto-copy logs from LOG_SOURCE_DIR, but it is not set.\n\n" +
                "Set LOG_SOURCE_DIR on the Settings tab, or use \"Run these logs\" to process " +
                "the logs currently in Raid_Logs.",
                "LOG_SOURCE_DIR not set", MessageBoxButtons.OK, MessageBoxIcon.Information);
            tabs.SelectedTab = tabSettings;
            return;
        }

        if (mode == RunMode.Manual && _logs.List().Count == 0)
        {
            var go = MessageBox.Show(
                "Raid_Logs is empty, so there is nothing to process.\n\nRun anyway?",
                "No logs", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (go != DialogResult.Yes) return;
        }

        SetRunning(true);
        _cancelRequested = false;
        _logsRefreshedDuringRun = false;
        txtConsole.Clear();
        _cts = new CancellationTokenSource();
        lblStatus.Text = mode == RunMode.Manual ? "Running these logs…" : "Running scheduled logs…";

        try
        {
            var code = await _runner.RunAsync(mode, AppendConsole, _cts.Token);
            AppendConsole($"[GUI] Pipeline exited with code {code}.");
            lblStatus.Text = _cancelRequested ? "Cancelled."
                : code == 0 ? "Done."
                : $"Finished with errors (exit {code}).";
        }
        catch (Exception ex)
        {
            AppendConsole("[GUI] Error launching pipeline: " + ex.Message);
            lblStatus.Text = "Failed to launch.";
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            SetRunning(false);
            RefreshLogList();
            UpdateSummaryButton();
        }
    }

    private void OnCancel(object? sender, EventArgs e)
    {
        _cancelRequested = true;
        lblStatus.Text = "Cancelling…";
        _runner.Cancel();
    }

    private void AppendConsole(string line)
    {
        if (txtConsole.IsHandleCreated && txtConsole.InvokeRequired)
        {
            txtConsole.BeginInvoke((Action)(() => AppendConsole(line)));
            return;
        }
        txtConsole.AppendText(line + Environment.NewLine);

        // Once the logs to process are finalized in Raid_Logs (auto-copied for a
        // scheduled run, or already present for a manual run), refresh the list
        // so the user can see exactly what's being processed - mid-run.
        if (!_logsRefreshedDuringRun && IsLogsReadyMarker(line))
        {
            _logsRefreshedDuringRun = true;
            RefreshLogList();
        }
    }

    private static bool IsLogsReadyMarker(string line)
        => line.Contains("Auto-copied", StringComparison.OrdinalIgnoreCase)
           || line.Contains("No raid logs to copy", StringComparison.OrdinalIgnoreCase)
           || line.Contains("[1/3]", StringComparison.OrdinalIgnoreCase);

    private void SetRunning(bool running)
    {
        btnRunManual.Enabled = !running;
        btnRunScheduled.Enabled = !running;
        btnCancel.Visible = running;
        btnCancel.Enabled = running;

        // Keep the log list readable during a run so the user can see what's
        // being processed; only block edits to it (and the settings tab).
        btnAddFiles.Enabled = !running;
        btnRemove.Enabled = !running;
        btnClear.Enabled = !running;
        btnRefresh.Enabled = !running;
        lvLogs.AllowDrop = !running;
        tabLogs.AllowDrop = !running;
        tabLogsTools.AllowDrop = !running;
        tabSettings.Enabled = !running;
        UseWaitCursor = running;

        if (running) runSpinner.Start();
        else runSpinner.Stop();
    }

    private void UpdateSummaryButton()
    {
        btnOpenSummary.Tag = FindLatestSummary();
        btnOpenSummary.Enabled = btnOpenSummary.Tag != null;
    }

    private string? FindLatestSummary()
    {
        if (!Directory.Exists(_paths.RaidsSummariesDir)) return null;
        return new DirectoryInfo(_paths.RaidsSummariesDir)
            .EnumerateFiles("*.html")
            .OrderByDescending(f => f.LastWriteTime)
            .FirstOrDefault()?.FullName;
    }

    private void OnOpenSummary(object? sender, EventArgs e)
    {
        if (btnOpenSummary.Tag is string path && File.Exists(path))
            OpenInShell(path);
    }

    private void OnOpenFolder(object? sender, EventArgs e)
    {
        Directory.CreateDirectory(_paths.RaidsSummariesDir);
        OpenInShell(_paths.RaidsSummariesDir);
    }

    private void OpenInShell(string path)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path)
            {
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open:\n{path}\n\n{ex.Message}",
                "Open failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    // ------------------------------------------------------------- Config ----

    private void LoadConfigToUi()
    {
        _loading = true;
        try
        {
            LoadConfigToUiCore();
        }
        finally
        {
            _loading = false;
        }
    }

    private void LoadConfigToUiCore()
    {
        txtGuildTag.Text = _config.Get("GUILD_TAG");
        txtWebhook.Text = _config.Get("DISCORD_WEBHOOK_URL");
        txtLogSource.Text = _config.Get("LOG_SOURCE_DIR");

        var parallel = _config.Get("MAX_PARALLEL_EI").Trim();
        if (!int.TryParse(parallel, out var pVal) || pVal <= 0)
        {
            chkAutoParallel.Checked = true;
            numParallel.Value = 1;
        }
        else
        {
            chkAutoParallel.Checked = false;
            numParallel.Value = Math.Clamp(pVal, (int)numParallel.Minimum, (int)numParallel.Maximum);
        }
        numParallel.Enabled = !chkAutoParallel.Checked;

        numMinSize.Value = ClampToControl(GetInt("MIN_LOG_SIZE_KB", 900), numMinSize);
        numGrace.Value = ClampToControl(GetInt("RAID_LOGS_GRACE_HOURS", 24), numGrace);

        for (var i = 0; i < 7; i++)
        {
            var value = _config.GetRaidHours(ConfigFile.DayKeys[i]);
            _loadedDays[i] = value;
            _dayDirty[i] = false;
            ApplyRaidHoursToRow(i, value);
        }
    }

    private void ApplyRaidHoursToRow(int i, RaidHoursValue value)
    {
        chkDay[i].Checked = value.Enabled;
        if (i == FridayIndex)
            chkFridayReset.Checked = value.IsReset;

        if (value.ParseOk && !value.IsReset)
        {
            dtStart[i].Value = DateTime.Today.Add(value.Start == TimeSpan.Zero ? DefaultStart : value.Start);
            dtEnd[i].Value = DateTime.Today.Add(value.End == TimeSpan.Zero ? DefaultEnd : value.End);
        }
        else
        {
            dtStart[i].Value = DateTime.Today.Add(DefaultStart);
            dtEnd[i].Value = DateTime.Today.Add(DefaultEnd);
        }
        UpdateRowEnabled(i);
    }

    // Mirrors Scripts/Resolve-RaidWindow.ps1's Get-FridayResetWindow: the WvW
    // reset is fixed at Saturday 02:00 UTC, so the local clock time it lands on
    // shifts with daylight saving (e.g. 10:00 PM vs 9:00 PM US Eastern). The
    // window runs for 2.5 hours from that start.
    private static (DateTime Start, DateTime End) NextFridayResetLocal(DateTime now)
    {
        var nowUtc = now.ToUniversalTime();
        var todayResetUtc = new DateTime(nowUtc.Year, nowUtc.Month, nowUtc.Day, 2, 0, 0, DateTimeKind.Utc);
        var daysUntilSat = ((int)DayOfWeek.Saturday - (int)nowUtc.DayOfWeek + 7) % 7;
        var resetUtc = todayResetUtc.AddDays(daysUntilSat);
        if (resetUtc <= nowUtc) resetUtc = resetUtc.AddDays(7);
        var startLocal = resetUtc.ToLocalTime();
        return (startLocal, startLocal.AddMinutes(150));
    }

    private string ComputeFridayResetTooltip()
    {
        var (start, end) = NextFridayResetLocal(DateTime.Now);
        var startStr = start.ToString("ddd MMM d, h:mm tt");
        var endStr = end.Date == start.Date
            ? end.ToString("h:mm tt")
            : end.ToString("ddd MMM d, h:mm tt");
        return
            "When checked, Friday uses the WvW reset instead of a fixed window." + Environment.NewLine +
            "Reset is Saturday 02:00 UTC; the window runs 2.5 hours and shifts with daylight saving." + Environment.NewLine +
            "Next capture window (your local time):" + Environment.NewLine +
            $"    {startStr}  –  {endStr}";
    }

    private void UpdateRowEnabled(int i)
    {
        var dayOn = chkDay[i].Checked;
        var resetOn = i == FridayIndex && chkFridayReset.Checked;
        dtStart[i].Enabled = dayOn && !resetOn;
        dtEnd[i].Enabled = dayOn && !resetOn;
        if (i == FridayIndex) chkFridayReset.Enabled = dayOn;
    }

    private bool TrySaveConfig(out string error)
    {
        error = string.Empty;
        try
        {
            SaveUiToConfig();
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private void SaveUiToConfig()
    {
        _config.Set("GUILD_TAG", txtGuildTag.Text.Trim());
        _config.Set("DISCORD_WEBHOOK_URL", txtWebhook.Text.Trim());
        _config.Set("MAX_PARALLEL_EI", chkAutoParallel.Checked ? "0" : ((int)numParallel.Value).ToString());
        _config.Set("LOG_SOURCE_DIR", txtLogSource.Text.Trim());
        _config.Set("MIN_LOG_SIZE_KB", ((int)numMinSize.Value).ToString());
        _config.Set("RAID_LOGS_GRACE_HOURS", ((int)numGrace.Value).ToString());

        for (var i = 0; i < 7; i++)
        {
            // Untouched rows round-trip exactly (including unparsed text / RESET).
            if (!_dayDirty[i])
            {
                _config.SetRaidHours(ConfigFile.DayKeys[i], _loadedDays[i]);
                continue;
            }
            _config.SetRaidHours(ConfigFile.DayKeys[i], BuildRaidHoursFromRow(i));
        }

        _config.Save();
    }

    private RaidHoursValue BuildRaidHoursFromRow(int i)
    {
        var v = new RaidHoursValue { Enabled = chkDay[i].Checked, ParseOk = true };
        if (i == FridayIndex && chkFridayReset.Checked)
        {
            v.IsReset = true;
        }
        else
        {
            v.Start = dtStart[i].Value.TimeOfDay;
            v.End = dtEnd[i].Value.TimeOfDay;
        }
        return v;
    }

    private void OnSaveConfig(object? sender, EventArgs e)
    {
        if (TrySaveConfig(out var error))
        {
            // Reloaded state becomes the new round-trip baseline.
            for (var i = 0; i < 7; i++)
            {
                _loadedDays[i] = _config.GetRaidHours(ConfigFile.DayKeys[i]);
                _dayDirty[i] = false;
            }
            lblStatus.Text = "Settings saved to config.txt.";
        }
        else
        {
            MessageBox.Show($"Could not save config.txt:\n{error}",
                "Save failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnRevertConfig(object? sender, EventArgs e)
    {
        _config = ConfigFile.Load(_paths);
        LoadConfigToUi();
        lblStatus.Text = "Settings reloaded from config.txt.";
    }

    private void OnBrowseLogSource(object? sender, EventArgs e)
    {
        using var dlg = new FolderBrowserDialog { Description = "Select your arcDPS log folder (LOG_SOURCE_DIR)" };
        if (Directory.Exists(txtLogSource.Text)) dlg.SelectedPath = txtLogSource.Text;
        if (dlg.ShowDialog(this) == DialogResult.OK)
            txtLogSource.Text = dlg.SelectedPath;
    }

    // ------------------------------------------------------------- Updates ---

    private async void OnCheckForUpdates(object? sender, EventArgs e)
    {
        if (_runner.IsRunning) return;

        btnCheckUpdates.Enabled = false;
        btnRunManual.Enabled = false;
        btnRunScheduled.Enabled = false;
        lblStatus.Text = "Checking for updates…";
        runSpinner.Start();

        UpdateService.CheckResult result;
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            result = await _updateService.CheckAsync(cts.Token);
        }
        finally
        {
            runSpinner.Stop();
            btnCheckUpdates.Enabled = true;
            btnRunManual.Enabled = true;
            btnRunScheduled.Enabled = true;
        }

        if (result.Error != null)
        {
            lblStatus.Text = "Update check failed.";
            MessageBox.Show(this, "Couldn't check for updates:\n\n" + result.Error,
                "Check for updates", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!result.UpdateAvailable)
        {
            lblStatus.Text = $"Up to date ({result.Current}).";
            MessageBox.Show(this, $"You're on the latest version ({result.Current}).",
                "Check for updates", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        lblStatus.Text = $"Update available: {result.Latest}.";
        var go = MessageBox.Show(this,
            $"An update is available.\n\n    Current:  {result.Current}\n    Latest:   {result.Latest}\n\n" +
            "Download and apply it now? Your Raid_Logs, Raids_Summaries, and config.txt are preserved.\n\n" +
            "After updating, close the app and run setup.bat to finish (it rebuilds Elite Insights and the GUI).",
            "Update available", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (go == DialogResult.Yes)
            await RunUpdateAsync(result.Latest!);
    }

    private async Task RunUpdateAsync(string latest)
    {
        SetUpdating(true);
        txtConsole.Clear();
        lblStatus.Text = "Updating…";
        AppendConsole($"[GUI] Updating to {latest}…");

        int code;
        try
        {
            using var cts = new CancellationTokenSource();
            code = await _updateService.RunUpdateAsync(AppendConsole, cts.Token);
        }
        catch (Exception ex)
        {
            SetUpdating(false);
            lblStatus.Text = "Update failed.";
            AppendConsole("[GUI] Update error: " + ex.Message);
            MessageBox.Show(this, "Update failed:\n\n" + ex.Message,
                "Update failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        SetUpdating(false);

        if (code != 0)
        {
            lblStatus.Text = $"Update failed (exit {code}).";
            MessageBox.Show(this, $"The update did not complete (exit {code}).\nSee the Output pane for details.",
                "Update failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        lblStatus.Text = $"Updated to {latest}.";
        var restart = MessageBox.Show(this,
            $"Updated to {latest}.\n\nThe app needs to restart to finish applying the update — this rebuilds " +
            "Elite Insights and the GUI. Restart now?",
            "Update complete", MessageBoxButtons.YesNo, MessageBoxIcon.Information);

        if (restart == DialogResult.Yes)
        {
            RelaunchViaSetup();
        }
        else
        {
            lblStatus.Text = "Update downloaded — run setup.bat to finish.";
            MessageBox.Show(this,
                "When you're ready, close GW2 Raid Summaries and run setup.bat to finish applying the update " +
                "(it rebuilds Elite Insights and the GUI).",
                "Finish the update", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    // Launches a detached helper that waits for THIS process to exit (so the
    // rebuild isn't blocked by the locked exe), then runs setup.bat - which
    // rebuilds and relaunches the GUI. Then closes the app.
    private void RelaunchViaSetup()
    {
        try
        {
            var pid = Environment.ProcessId;
            var setupBat = Path.Combine(_paths.RepoRoot, "setup.bat").Replace("'", "''");
            var command =
                $"Wait-Process -Id {pid} -Timeout 120 -ErrorAction SilentlyContinue; & '{setupBat}'";

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("powershell.exe")
            {
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{command}\"",
                UseShellExecute = true,
                WorkingDirectory = _paths.RepoRoot,
            });
            Application.Exit();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                "Couldn't restart automatically:\n\n" + ex.Message +
                "\n\nPlease close the app and run setup.bat to finish the update.",
                "Restart needed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    // Like a run, but no Cancel: a release update should not be interrupted
    // mid-replace, so once confirmed it runs to completion.
    private void SetUpdating(bool updating)
    {
        btnRunManual.Enabled = !updating;
        btnRunScheduled.Enabled = !updating;
        tabSettings.Enabled = !updating;
        UseWaitCursor = updating;
        if (updating) runSpinner.Start();
        else runSpinner.Stop();
    }

    private int GetInt(string key, int fallback)
        => int.TryParse(_config.Get(key).Trim(), out var v) ? v : fallback;

    private static decimal ClampToControl(int value, NumericUpDown control)
        => Math.Clamp(value, (int)control.Minimum, (int)control.Maximum);
}
