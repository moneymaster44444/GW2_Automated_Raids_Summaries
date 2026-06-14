using GW2RaidsGui.Pipeline;

namespace GW2RaidsGui;

/// <summary>
/// Modal first-run dialog. It explains that one-time setup is happening, then
/// drives <c>process_logs.bat --setup</c> (build Elite Insights, generate
/// configs, install Python/TiddlyWiki) while streaming a live status and an
/// animated progress bar.
/// </summary>
public sealed class SetupForm : Form
{
    // Ordered substring -> friendly step text. The first match found on a line
    // updates the current-step label. Mirrors the [SETUP]/build markers that
    // process_logs.bat prints.
    private static readonly (string Marker, string Text)[] StepMarkers =
    {
        ("Creating config.txt", "Creating configuration files…"),
        ("establish_config_files", "Generating Elite Insights / Combiner configs…"),
        ("build_elite_insights", "Building Elite Insights (this can take a few minutes)…"),
        ("dotnet publish", "Building Elite Insights (this can take a few minutes)…"),
        ("Attempting to publish", "Building Elite Insights (this can take a few minutes)…"),
        ("Checking Python dependencies", "Checking Python dependencies…"),
        ("packages missing; installing", "Installing Python dependencies…"),
        ("Checking TiddlyWiki", "Checking TiddlyWiki…"),
        ("installing globally via npm", "Installing TiddlyWiki…"),
        ("First-time setup complete", "Finishing up…"),
    };

    private readonly AppPaths _paths;
    private readonly PipelineRunner _runner;
    private CancellationTokenSource? _cts;
    private bool _cancelRequested;

    private Label lblStep = null!;
    private ProgressBar bar = null!;
    private TextBox txtDetails = null!;
    private Button btnCancel = null!;
    private Button btnFinish = null!;

    public SetupForm(AppPaths paths)
    {
        _paths = paths;
        _runner = new PipelineRunner(paths);
        BuildUi();
    }

    private void BuildUi()
    {
        Text = "GW2 Raid Summaries — First-time setup";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(580, 440);
        Font = SystemFonts.MessageBoxFont ?? new Font("Segoe UI", 9f);
        TrySetIcon();

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(16),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // header
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // step
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // bar
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // details
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // buttons

        var header = new Label
        {
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 12),
            MaximumSize = new Size(548, 0),
            Text = "Running one-time setup. This only happens once and may take a few minutes.",
        };

        lblStep = new Label
        {
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 6),
            Font = new Font(Font, FontStyle.Bold),
            Text = "Starting setup…",
        };

        bar = new ProgressBar
        {
            Dock = DockStyle.Top,
            Height = 22,
            Style = ProgressBarStyle.Marquee,
            MarqueeAnimationSpeed = 30,
            Margin = new Padding(0, 0, 0, 10),
        };

        txtDetails = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.Gainsboro,
            Font = new Font("Consolas", 8.5f),
        };

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            Margin = new Padding(0, 10, 0, 0),
        };
        btnFinish = new Button { Text = "Finish", AutoSize = true, Enabled = false, Padding = new Padding(10, 4, 10, 4) };
        btnFinish.Click += (_, _) => { DialogResult = DialogResult.OK; Close(); };
        btnCancel = new Button { Text = "Cancel", AutoSize = true, Padding = new Padding(10, 4, 10, 4) };
        btnCancel.Click += OnCancel;
        buttons.Controls.Add(btnFinish);
        buttons.Controls.Add(btnCancel);

        root.Controls.Add(header, 0, 0);
        root.Controls.Add(lblStep, 0, 1);
        root.Controls.Add(bar, 0, 2);
        root.Controls.Add(txtDetails, 0, 3);
        root.Controls.Add(buttons, 0, 4);
        Controls.Add(root);

        AcceptButton = btnFinish;
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        await RunSetupAsync();
    }

    private async Task RunSetupAsync()
    {
        _cancelRequested = false;
        _cts = new CancellationTokenSource();
        try
        {
            var code = await _runner.RunAsync(RunMode.Setup, OnOutput, _cts.Token);
            bar.Style = ProgressBarStyle.Continuous;
            bar.Value = bar.Maximum;
            if (_cancelRequested)
            {
                lblStep.Text = "Setup cancelled.";
            }
            else if (code == 0)
            {
                lblStep.Text = "Initial setup complete.";
            }
            else
            {
                lblStep.Text = $"Setup finished with problems (exit {code}). See details below.";
            }
        }
        catch (Exception ex)
        {
            bar.Style = ProgressBarStyle.Continuous;
            OnOutput("[GUI] Error during setup: " + ex.Message);
            lblStep.Text = "Setup could not run. See the details below.";
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            btnCancel.Enabled = false;
            btnFinish.Enabled = true;
            btnFinish.Focus();
        }
    }

    private void OnOutput(string line)
    {
        if (txtDetails.IsHandleCreated && txtDetails.InvokeRequired)
        {
            txtDetails.BeginInvoke((Action)(() => OnOutput(line)));
            return;
        }

        txtDetails.AppendText(line + Environment.NewLine);

        foreach (var (marker, text) in StepMarkers)
        {
            if (line.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                lblStep.Text = text;
                break;
            }
        }
    }

    private void OnCancel(object? sender, EventArgs e)
    {
        if (_cts == null) return;
        _cancelRequested = true;
        lblStep.Text = "Cancelling…";
        btnCancel.Enabled = false;
        _runner.Cancel();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // Closing the window mid-setup is treated as a cancel.
        if (_runner.IsRunning)
        {
            _cancelRequested = true;
            _runner.Cancel();
        }
        base.OnFormClosing(e);
    }

    private void TrySetIcon()
    {
        try
        {
            using var stream = GetType().Assembly.GetManifestResourceStream("GW2RaidsGui.app.ico");
            if (stream != null) Icon = new Icon(stream);
        }
        catch
        {
            // Default icon is fine.
        }
    }
}
