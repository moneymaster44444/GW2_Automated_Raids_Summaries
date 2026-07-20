namespace GW2RaidsGui;

public partial class MainForm
{
    private static readonly TimeSpan DefaultStart = new(19, 30, 0);
    private static readonly TimeSpan DefaultEnd = new(22, 0, 0);

    private bool _loading;

    // Tabs
    private TabControl tabs = null!;
    private TabPage tabLogs = null!;
    private TabPage tabSettings = null!;
    private TableLayoutPanel tabLogsTools = null!;

    // Logs tab
    private ListView lvLogs = null!;
    private Label lblLogCount = null!;
    private Label lblDropHint = null!;
    private Button btnAddFiles = null!;
    private Button btnRemove = null!;
    private Button btnClear = null!;
    private Button btnRefresh = null!;

    // Run bar
    private Button btnRunManual = null!;
    private Button btnRunScheduled = null!;
    private ToolTip runBarTooltip = null!;
    private Button btnCancel = null!;
    private Label lblStatus = null!;
    private GifIndicator runSpinner = null!;
    private Button btnOpenSummary = null!;
    private Button btnOpenFolder = null!;

    // Console
    private TextBox txtConsole = null!;

    // Settings tab
    private TextBox txtGuildTag = null!;
    private TextBox txtWebhook = null!;
    private CheckBox chkShowWebhook = null!;
    private CheckBox chkAutoParallel = null!;
    private NumericUpDown numParallel = null!;
    private TextBox txtLogSource = null!;
    private Button btnBrowseSource = null!;
    private NumericUpDown numMinSize = null!;
    private NumericUpDown numGrace = null!;
    private readonly CheckBox[] chkDay = new CheckBox[7];
    private readonly DateTimePicker[] dtStart = new DateTimePicker[7];
    private readonly DateTimePicker[] dtEnd = new DateTimePicker[7];
    private CheckBox chkFridayReset = null!;
    private ToolTip raidHoursTooltip = null!;
    private Button btnSaveConfig = null!;
    private Button btnRevertConfig = null!;
    private Label lblVersion = null!;
    private Button btnCheckUpdates = null!;

    private void BuildUi()
    {
        Text = "GW2 Automated Raids Summaries";
        TrySetWindowIcon();
        Font = SystemFonts.MessageBoxFont ?? new Font("Segoe UI", 9f);
        MinimumSize = new Size(840, 620);
        Size = new Size(960, 740);
        StartPosition = FormStartPosition.CenterScreen;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 200));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        tabs = new TabControl { Dock = DockStyle.Fill };
        tabLogs = new TabPage("Logs");
        tabSettings = new TabPage("Settings");
        tabs.TabPages.Add(tabLogs);
        tabs.TabPages.Add(tabSettings);

        BuildLogsTab();
        BuildSettingsTab();

        root.Controls.Add(tabs, 0, 0);
        root.Controls.Add(BuildConsolePanel(), 0, 1);
        root.Controls.Add(BuildRunBar(), 0, 2);

        Controls.Add(root);
    }

    private void BuildLogsTab()
    {
        tabLogsTools = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(8),
        };
        tabLogsTools.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // toolbar
        tabLogsTools.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // hint
        tabLogsTools.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // list

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
        };
        btnAddFiles = MakeButton("Add files…", OnAddFiles);
        btnRemove = MakeButton("Remove selected", OnRemoveSelected);
        btnClear = MakeButton("Clear folder", OnClearFolder);
        btnRefresh = MakeButton("Refresh", (_, _) => RefreshLogList());
        lblLogCount = new Label
        {
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(12, 9, 0, 0),
            ForeColor = SystemColors.GrayText,
        };
        toolbar.Controls.Add(btnAddFiles);
        toolbar.Controls.Add(btnRemove);
        toolbar.Controls.Add(btnClear);
        toolbar.Controls.Add(btnRefresh);
        toolbar.Controls.Add(lblLogCount);

        lblDropHint = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            Height = 28,
            TextAlign = ContentAlignment.MiddleCenter,
            Text = "Drag and drop .zevtc / .evtc files anywhere here, or click “Add files…”.",
            ForeColor = SystemColors.GrayText,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(0, 4, 0, 4),
        };

        lvLogs = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            MultiSelect = true,
            HideSelection = false,
            AllowDrop = true,
        };
        lvLogs.Columns.Add("Log file", 460);
        lvLogs.Columns.Add("Size", 90);
        lvLogs.Columns.Add("Modified", 150);
        lvLogs.DragEnter += OnDragEnterLogs;
        lvLogs.DragDrop += OnDragDropLogs;
        lvLogs.KeyDown += (_, e) => { if (e.KeyCode == Keys.Delete && btnRemove.Enabled) OnRemoveSelected(this, EventArgs.Empty); };

        tabLogsTools.Controls.Add(toolbar, 0, 0);
        tabLogsTools.Controls.Add(lblDropHint, 0, 1);
        tabLogsTools.Controls.Add(lvLogs, 0, 2);

        // Accept drops on the whole tab, not just the list.
        tabLogs.AllowDrop = true;
        tabLogs.DragEnter += OnDragEnterLogs;
        tabLogs.DragDrop += OnDragDropLogs;
        tabLogsTools.AllowDrop = true;
        tabLogsTools.DragEnter += OnDragEnterLogs;
        tabLogsTools.DragDrop += OnDragDropLogs;

        tabLogs.Controls.Add(tabLogsTools);
    }

    private Control BuildConsolePanel()
    {
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8, 0, 8, 0) };
        var header = new Label
        {
            Dock = DockStyle.Top,
            Text = "Output",
            AutoSize = false,
            Height = 20,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = SystemColors.GrayText,
        };
        txtConsole = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.Gainsboro,
            Font = new Font("Consolas", 9f),
        };
        panel.Controls.Add(txtConsole);
        panel.Controls.Add(header);
        return panel;
    }

    private Control BuildRunBar()
    {
        var bar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 8,
            RowCount = 1,
            Padding = new Padding(8, 6, 8, 6),
        };
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // run manual
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // run scheduled
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // cancel
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // status text
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // working gif
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); // filler
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // open summary
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // open folder

        btnRunManual = MakeButton("▶  Run these logs", OnRunManual);
        btnRunManual.Font = new Font(Font, FontStyle.Bold);
        btnRunManual.MinimumSize = new Size(150, 36);

        btnRunScheduled = MakeButton("▶  Run scheduled logs", OnRunScheduled);
        btnRunScheduled.MinimumSize = new Size(170, 36);

        runBarTooltip = new ToolTip { ShowAlways = true, InitialDelay = 400, AutoPopDelay = 9000, ReshowDelay = 100 };
        runBarTooltip.SetToolTip(btnRunManual,
            "Process the logs currently listed above (the contents of the Raid_Logs folder).");
        runBarTooltip.SetToolTip(btnRunScheduled,
            "Auto-copy logs from your log source folder to Raid_Logs folder, then process them.");

        btnCancel = MakeButton("Cancel", OnCancel);
        btnCancel.MinimumSize = new Size(80, 36);
        btnCancel.Visible = false;

        // Status text + a small "working" gif, vertically centered with the buttons.
        lblStatus = new Label
        {
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(8, 0, 4, 0),
        };
        runSpinner = new GifIndicator { Size = new Size(40, 26), Anchor = AnchorStyles.Left, Margin = new Padding(0, 0, 8, 0) };

        btnOpenSummary = MakeButton("Open summary", OnOpenSummary);
        btnOpenSummary.MinimumSize = new Size(110, 36);
        btnOpenFolder = MakeButton("Open folder", OnOpenFolder);
        btnOpenFolder.MinimumSize = new Size(100, 36);

        bar.Controls.Add(btnRunManual, 0, 0);
        bar.Controls.Add(btnRunScheduled, 1, 0);
        bar.Controls.Add(btnCancel, 2, 0);
        bar.Controls.Add(lblStatus, 3, 0);
        bar.Controls.Add(runSpinner, 4, 0);
        // column 5 is an empty Percent(100) filler that keeps the Open buttons on the right
        bar.Controls.Add(btnOpenSummary, 6, 0);
        bar.Controls.Add(btnOpenFolder, 7, 0);
        return bar;
    }

    private void BuildSettingsTab()
    {
        var body = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(12) };

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var row = 0;
        void AddRow(string label, Control control, string? help = null)
        {
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var lbl = new Label
            {
                Text = label,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 8, 12, 8),
            };
            control.Margin = new Padding(0, 5, 0, 5);
            table.Controls.Add(lbl, 0, row);
            table.Controls.Add(control, 1, row);
            row++;
            if (help != null)
            {
                table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                var h = new Label
                {
                    Text = help,
                    AutoSize = true,
                    ForeColor = SystemColors.GrayText,
                    Margin = new Padding(0, 0, 0, 8),
                    MaximumSize = new Size(620, 0),
                };
                table.Controls.Add(h, 1, row);
                row++;
            }
        }

        txtGuildTag = new TextBox { Width = 220, Anchor = AnchorStyles.Left };
        AddRow("Guild tag", txtGuildTag,
            "Prefix for the generated summary HTML: <GUILD_TAG>_<date>.html.");

        txtWebhook = new TextBox { Dock = DockStyle.Fill, UseSystemPasswordChar = true };
        chkShowWebhook = new CheckBox { Text = "Show", AutoSize = true, Anchor = AnchorStyles.Left };
        chkShowWebhook.CheckedChanged += (_, _) => txtWebhook.UseSystemPasswordChar = !chkShowWebhook.Checked;
        AddRow("Discord webhook URL", Pair(txtWebhook, chkShowWebhook),
            "Optional. Paste a Discord channel webhook to auto-post the summary. Leave blank to skip.");

        chkAutoParallel = new CheckBox { Text = "Auto (about half your CPU cores)", AutoSize = true, Anchor = AnchorStyles.Left };
        numParallel = new NumericUpDown { Minimum = 1, Maximum = 64, Width = 70, Anchor = AnchorStyles.Left };
        chkAutoParallel.CheckedChanged += (_, _) => numParallel.Enabled = !chkAutoParallel.Checked;
        AddRow("Parallel EI instances", Pair(chkAutoParallel, numParallel),
            "How many Elite Insights instances to run at once (MAX_PARALLEL_EI). Auto = 0.");

        txtLogSource = new TextBox { Dock = DockStyle.Fill };
        btnBrowseSource = MakeButton("Browse…", OnBrowseLogSource);
        AddRow("Log source folder", Pair(txtLogSource, btnBrowseSource),
            "Optional arcDPS folder to auto-copy from on a scheduled run (LOG_SOURCE_DIR). " +
            "Leave blank to only process logs you place in Raid_Logs.");

        numMinSize = new NumericUpDown { Minimum = 0, Maximum = 1_000_000, Increment = 50, Width = 100, Anchor = AnchorStyles.Left };
        AddRow("Min log size (KB)", numMinSize,
            "Skip logs smaller than this when auto-copying (MIN_LOG_SIZE_KB). Filters out short fights / wipes.");

        numGrace = new NumericUpDown { Minimum = 1, Maximum = 100_000, Width = 100, Anchor = AnchorStyles.Left };
        AddRow("Grace hours", numGrace,
            "If the last raid window ended more than this many hours ago, a scheduled run copies nothing (RAID_LOGS_GRACE_HOURS).");

        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(BuildRaidHoursGroup(), 0, row);
        table.SetColumnSpan(table.GetControlFromPosition(0, row)!, 2);

        body.Controls.Add(table);

        var saveBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(12, 6, 12, 6),
        };
        btnSaveConfig = MakeButton("Save settings", OnSaveConfig);
        btnSaveConfig.Font = new Font(Font, FontStyle.Bold);
        btnRevertConfig = MakeButton("Reload from config file", OnRevertConfig);
        lblVersion = new Label
        {
            Text = "Version " + _paths.ReadVersion(),
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(28, 9, 8, 0),
        };
        btnCheckUpdates = MakeButton("Check for updates", OnCheckForUpdates);
        saveBar.Controls.Add(btnSaveConfig);
        saveBar.Controls.Add(btnRevertConfig);
        saveBar.Controls.Add(lblVersion);
        saveBar.Controls.Add(btnCheckUpdates);

        tabSettings.Controls.Add(body);
        tabSettings.Controls.Add(saveBar);
    }

    private GroupBox BuildRaidHoursGroup()
    {
        var group = new GroupBox
        {
            Text = "Weekly raid windows",
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 14, 0, 8),
            Padding = new Padding(10),
        };

        var grid = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 5,
            Dock = DockStyle.Fill,
        };
        for (var c = 0; c < 5; c++)
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        // Header row
        grid.Controls.Add(new Label { Text = "Day", AutoSize = true, Font = new Font(Font, FontStyle.Bold), Margin = new Padding(3, 6, 18, 6) }, 0, 0);
        grid.Controls.Add(new Label { Text = "Start", AutoSize = true, Font = new Font(Font, FontStyle.Bold), Margin = new Padding(3, 6, 6, 6) }, 1, 0);
        grid.Controls.Add(new Label { Text = "", AutoSize = true }, 2, 0);
        grid.Controls.Add(new Label { Text = "End", AutoSize = true, Font = new Font(Font, FontStyle.Bold), Margin = new Padding(3, 6, 6, 6) }, 3, 0);
        grid.Controls.Add(new Label { Text = "", AutoSize = true }, 4, 0);

        for (var i = 0; i < 7; i++)
        {
            var rowIndex = i + 1;
            var idx = i;

            chkDay[i] = new CheckBox { Text = DayNames[i], AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 18, 6) };
            chkDay[i].CheckedChanged += (_, _) => { if (!_loading) _dayDirty[idx] = true; UpdateRowEnabled(idx); };

            dtStart[i] = MakeTimePicker();
            dtStart[i].ValueChanged += (_, _) => { if (!_loading) _dayDirty[idx] = true; };

            var toLabel = new Label { Text = "to", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(6, 9, 6, 6) };

            dtEnd[i] = MakeTimePicker();
            dtEnd[i].ValueChanged += (_, _) => { if (!_loading) _dayDirty[idx] = true; };

            grid.Controls.Add(chkDay[i], 0, rowIndex);
            grid.Controls.Add(dtStart[i], 1, rowIndex);
            grid.Controls.Add(toLabel, 2, rowIndex);
            grid.Controls.Add(dtEnd[i], 3, rowIndex);

            if (i == FridayIndex)
            {
                chkFridayReset = new CheckBox { Text = "WvW reset", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(10, 6, 3, 6) };
                chkFridayReset.CheckedChanged += (_, _) => { if (!_loading) _dayDirty[FridayIndex] = true; UpdateRowEnabled(FridayIndex); };

                // Hovering the checkbox (box or label - it's one control) shows the
                // exact local time the RESET value resolves to, recomputed on hover
                // so it stays correct across DST and week boundaries.
                raidHoursTooltip = new ToolTip { ShowAlways = true, InitialDelay = 300, AutoPopDelay = 20000, ReshowDelay = 100 };
                raidHoursTooltip.SetToolTip(chkFridayReset, ComputeFridayResetTooltip());
                chkFridayReset.MouseEnter += (_, _) =>
                    raidHoursTooltip.SetToolTip(chkFridayReset, ComputeFridayResetTooltip());

                grid.Controls.Add(chkFridayReset, 4, rowIndex);
            }
        }

        group.Controls.Add(grid);
        return group;
    }

    private void TrySetWindowIcon()
    {
        try
        {
            using var stream = GetType().Assembly
                .GetManifestResourceStream("GW2RaidsGui.app.ico");
            if (stream != null) Icon = new Icon(stream);
        }
        catch
        {
            // Fall back to the default system icon if the resource is missing.
        }
    }

    private DateTimePicker MakeTimePicker() => new()
    {
        Format = DateTimePickerFormat.Custom,
        CustomFormat = "h:mm tt",
        ShowUpDown = true,
        Width = 90,
        Anchor = AnchorStyles.Left,
    };

    private Button MakeButton(string text, EventHandler onClick)
    {
        var b = new Button { Text = text, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(8, 4, 8, 4) };
        b.Click += onClick;
        return b;
    }

    // Horizontal pair: first control fills, trailing controls hug the right.
    private static Control Pair(Control fill, params Control[] trailing)
    {
        var p = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 1 + trailing.Length,
            RowCount = 1,
            Margin = new Padding(0),
        };
        p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        fill.Margin = new Padding(0, 0, 6, 0);
        p.Controls.Add(fill, 0, 0);
        for (var i = 0; i < trailing.Length; i++)
        {
            p.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            trailing[i].Anchor = AnchorStyles.Left;
            trailing[i].Margin = new Padding(0, 0, 6, 0);
            p.Controls.Add(trailing[i], i + 1, 0);
        }
        return p;
    }
}
