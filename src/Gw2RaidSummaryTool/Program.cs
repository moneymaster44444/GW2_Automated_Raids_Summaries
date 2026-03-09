using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text;

var app = new RaidSummaryApp(args);
return await app.RunAsync();

internal sealed class RaidSummaryApp
{
    private readonly string[] _args;

    private readonly string _root;
    private readonly string _logsDir;
    private readonly string _summariesDir;
    private readonly string _eiJsonDir;
    private readonly string _eliteInsightsConfig;
    private readonly string _combinerConfig;
    private readonly string _webhookFile;
    private readonly string _sampleEliteInsightsConfig;
    private readonly string _sampleCombinerConfig;
    private readonly string _eiCsproj;
    private readonly string _eiPublishDir;
    private readonly string _combinerScript;
    private readonly string _twShellHtml;
    private readonly string _autoImportTid;

    public RaidSummaryApp(string[] args)
    {
        _args = args;
        _root = AppContext.BaseDirectory;

        // When running from bin/Debug/... move up until we hit repo marker.
        while (!File.Exists(Path.Combine(_root, "README.md")) && Directory.GetParent(_root) is { } parent)
        {
            _root = parent.FullName;
        }

        _logsDir = Path.Combine(_root, "Raid_Logs");
        _summariesDir = Path.Combine(_root, "Raids_Summaries");
        _eiJsonDir = Path.Combine(_summariesDir, "EI_json_output");
        _eliteInsightsConfig = Path.Combine(_root, "Resources", "Config", "EliteInsights.conf");
        _combinerConfig = Path.Combine(_root, "Resources", "Config", "top_stats_config.ini");
        _sampleEliteInsightsConfig = Path.Combine(_root, "Resources", "Config", "sample.eliteinsights.conf");
        _sampleCombinerConfig = Path.Combine(_root, "Resources", "Config", "sample.top_stats_config.ini");
        _webhookFile = Path.Combine(_root, "Resources", "Config", "Secrets", "discord_webhook.txt");
        _eiCsproj = Path.Combine(_root, "Resources", "Elite Insights", "GW2EIParserCLI", "GW2EIParserCLI.csproj");
        _eiPublishDir = Path.Combine(_root, "Resources", "Elite Insights", "GW2EI.bin", "Release", "CLI");
        _combinerScript = Path.Combine(_root, "Resources", "EI Combiner", "tw5_top_stats.py");
        _twShellHtml = Path.Combine(_root, "Resources", "EI Combiner", "Example_Output", "Top_Stats_Index.html");
        _autoImportTid = Path.Combine(_root, "auto-import.tid");
    }

    public async Task<int> RunAsync()
    {
        try
        {
            var command = _args.FirstOrDefault()?.ToLowerInvariant() ?? "run";

            return command switch
            {
                "help" or "-h" or "--help" => PrintHelp(),
                "init" => EnsureConfigsAndSecrets(),
                "build" => BuildEliteInsights(),
                "run" => await RunPipelineAsync(),
                _ => Fail($"Unknown command '{command}'. Use 'help' to see commands.")
            };
        }
        catch (Exception ex)
        {
            return Fail(ex.Message);
        }
    }

    private int PrintHelp()
    {
        Console.WriteLine("GW2 Raid Summary Tool (.NET)");
        Console.WriteLine("Commands:");
        Console.WriteLine("  init   - Create config files and discord webhook template");
        Console.WriteLine("  build  - Build/publish Elite Insights CLI");
        Console.WriteLine("  run    - Setup if needed, process logs, generate html, notify Discord");
        Console.WriteLine("  help   - Show this help");
        return 0;
    }

    private async Task<int> RunPipelineAsync()
    {
        if (EnsureConfigsAndSecrets() != 0)
        {
            return 1;
        }

        if (BuildEliteInsightsIfMissing() != 0)
        {
            return 1;
        }

        Directory.CreateDirectory(_logsDir);
        Directory.CreateDirectory(_summariesDir);
        Directory.CreateDirectory(_eiJsonDir);

        CleanupIntermediateFiles();

        var eiCliExe = FindEliteInsightsCli();
        if (eiCliExe is null)
        {
            return Fail("Could not find Elite Insights CLI executable.");
        }

        var logs = Directory
            .EnumerateFiles(_logsDir, "*.zevtc")
            .Concat(Directory.EnumerateFiles(_logsDir, "*.evtc"))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (logs.Count == 0)
        {
            Console.WriteLine($"[INFO] No logs were found in {_logsDir}");
            return 0;
        }

        Console.WriteLine($"[1/4] Parsing {logs.Count} log(s) with Elite Insights...");
        foreach (var log in logs)
        {
            var name = Path.GetFileName(log);
            Console.WriteLine($"  -> {name}");
            await RunProcessOrThrowAsync(eiCliExe, $"-c \"{_eliteInsightsConfig}\" \"{log}\"");
        }

        Console.WriteLine("[2/4] Running EI Combiner...");
        if (!File.Exists(_combinerScript))
        {
            return Fail($"Missing combiner script: {_combinerScript}");
        }

        await RunProcessOrThrowAsync("python", $"\"{_combinerScript}\" -i \"{_eiJsonDir}\" -c \"{_combinerConfig}\"");

        Console.WriteLine("[3/4] Finalizing Drag_and_Drop JSON...");
        var latestDropJson = GetLatestDragAndDropJson();
        if (latestDropJson is null)
        {
            Console.WriteLine("[WARN] EI combiner did not produce Drag_and_Drop json.");
            return 0;
        }

        var copiedJson = Path.Combine(_summariesDir, Path.GetFileName(latestDropJson));
        File.Copy(latestDropJson, copiedJson, overwrite: true);
        foreach (var old in Directory.EnumerateFiles(_eiJsonDir, "Drag_and_Drop_Log_Summary_*.json"))
        {
            File.Delete(old);
        }

        Console.WriteLine("[4/4] Building TiddlyWiki summary html...");
        var outputHtml = await BuildSummaryHtmlAsync(copiedJson);
        if (outputHtml is null)
        {
            return 1;
        }

        await TryNotifyDiscordAsync(outputHtml);

        Console.WriteLine("Done.");
        Console.WriteLine($"Summary html: {outputHtml}");
        return 0;
    }

    private int EnsureConfigsAndSecrets()
    {
        Directory.CreateDirectory(_eiJsonDir);
        Directory.CreateDirectory(Path.Combine(_root, "Resources", "Config"));
        Directory.CreateDirectory(Path.GetDirectoryName(_webhookFile)!);

        if (!File.Exists(_sampleEliteInsightsConfig))
        {
            return Fail($"Missing sample config: {_sampleEliteInsightsConfig}");
        }

        if (!File.Exists(_sampleCombinerConfig))
        {
            return Fail($"Missing sample config: {_sampleCombinerConfig}");
        }

        var eiConfig = File.ReadAllText(_sampleEliteInsightsConfig)
            .Replace("__OUTLOCATION__", _eiJsonDir, StringComparison.Ordinal);
        File.WriteAllText(_eliteInsightsConfig, eiConfig, new UTF8Encoding(false));

        var combinerConfig = File.ReadAllText(_sampleCombinerConfig)
            .Replace("__INPUT_JSON_DIR__", _eiJsonDir, StringComparison.Ordinal);
        File.WriteAllText(_combinerConfig, combinerConfig, new UTF8Encoding(false));

        if (!File.Exists(_webhookFile))
        {
            File.WriteAllText(_webhookFile, string.Empty, new UTF8Encoding(false));
            Console.WriteLine($"Created webhook template: {_webhookFile}");
        }

        Console.WriteLine("Config setup complete.");
        return 0;
    }

    private int BuildEliteInsightsIfMissing()
    {
        return FindEliteInsightsCli() is null ? BuildEliteInsights() : 0;
    }

    private int BuildEliteInsights()
    {
        if (!File.Exists(_eiCsproj))
        {
            return Fail($"Missing csproj: {_eiCsproj}");
        }

        return RunProcess("dotnet", $"publish \"{_eiCsproj}\" -c Release -o \"{_eiPublishDir}\"");
    }

    private string? FindEliteInsightsCli()
    {
        var preferred = new[]
        {
            Path.Combine(_eiPublishDir, "GW2EIParserCLI.exe"),
            Path.Combine(_eiPublishDir, "GuildWars2EliteInsights-CLI.exe")
        };

        foreach (var path in preferred)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        var fallback = Directory
            .EnumerateFiles(Path.Combine(_root, "Resources", "Elite Insights"), "*CLI*.exe", SearchOption.AllDirectories)
            .FirstOrDefault();

        return fallback;
    }

    private void CleanupIntermediateFiles()
    {
        DeleteByPattern(_summariesDir, "*.json");
        DeleteByPattern(_eiJsonDir, "*.json");
        DeleteByPattern(_eiJsonDir, "*.json.gz");
        DeleteByPattern(_eiJsonDir, "*.log");
    }

    private static void DeleteByPattern(string directory, string pattern)
    {
        if (!Directory.Exists(directory)) return;

        foreach (var file in Directory.EnumerateFiles(directory, pattern))
        {
            File.Delete(file);
        }
    }

    private string? GetLatestDragAndDropJson()
    {
        return Directory
            .EnumerateFiles(_eiJsonDir, "Drag_and_Drop_Log_Summary_*.json")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    private async Task<string?> BuildSummaryHtmlAsync(string dragDropJson)
    {
        var twBuildDir = Path.Combine(_root, "Top_Stats_Html");
        if (Directory.Exists(twBuildDir))
        {
            Directory.Delete(twBuildDir, recursive: true);
        }

        if (!File.Exists(_autoImportTid) || !File.Exists(_twShellHtml))
        {
            return null;
        }

        if (RunProcess("tiddlywiki", $"\"{twBuildDir}\" --init server") != 0) return null;
        if (RunProcess("tiddlywiki", $"\"{twBuildDir}\" --load \"{_twShellHtml}\"") != 0) return null;
        if (RunProcess("tiddlywiki", $"\"{twBuildDir}\" --import \"{_autoImportTid}\" text/plain") != 0) return null;
        if (RunProcess("tiddlywiki", $"\"{twBuildDir}\" --import \"{dragDropJson}\" application/json \"$:/data/dragdrop\"") != 0) return null;
        if (RunProcess("tiddlywiki", $"\"{twBuildDir}\" --build index") != 0) return null;

        var output = Path.Combine(twBuildDir, "output", "index.html");
        if (!File.Exists(output))
        {
            return null;
        }

        var finalHtml = Path.Combine(_summariesDir, $"INC_{DateTime.Now:MM-dd-yy}.html");
        File.Copy(output, finalHtml, overwrite: true);

        Directory.Delete(twBuildDir, recursive: true);
        return finalHtml;
    }

    private async Task TryNotifyDiscordAsync(string htmlPath)
    {
        if (!File.Exists(_webhookFile)) return;

        var webhook = File.ReadLines(_webhookFile)
            .Select(line => line.Trim())
            .FirstOrDefault(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith('#'));

        if (string.IsNullOrWhiteSpace(webhook))
        {
            Console.WriteLine("[INFO] No Discord webhook configured.");
            return;
        }

        var message = BuildDiscordMessage(htmlPath);
        var posted = await TryPostDiscordFileAsync(webhook, htmlPath, "text/html", message);

        if (!posted)
        {
            var zipPath = Path.ChangeExtension(htmlPath, ".zip");
            if (File.Exists(zipPath)) File.Delete(zipPath);
            using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                zip.CreateEntryFromFile(htmlPath, Path.GetFileName(htmlPath));
            }

            posted = await TryPostDiscordFileAsync(webhook, zipPath, "application/zip", message);
        }

        Console.WriteLine(posted ? "[OK] Discord notification sent." : "[WARN] Discord notification failed.");
    }

    private static string BuildDiscordMessage(string htmlPath)
    {
        var fileName = Path.GetFileNameWithoutExtension(htmlPath);
        var parts = fileName.Split('_').LastOrDefault()?.Split('-');

        if (parts is { Length: 3 }
            && int.TryParse(parts[0], out var month)
            && int.TryParse(parts[1], out var day)
            && int.TryParse(parts[2], out var year))
        {
            var fullYear = year + 2000;
            if (DateTime.TryParseExact($"{month:D2}-{day:D2}-{fullYear}", "MM-dd-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            {
                return $"Summary for {dt:dddd} {dt:MM/dd} raid";
            }
        }

        return $"Summary for {DateTime.Now:dddd} {DateTime.Now:MM/dd} raid";
    }

    private static async Task<bool> TryPostDiscordFileAsync(string webhook, string filePath, string contentType, string message)
    {
        try
        {
            using var client = new HttpClient();
            using var form = new MultipartFormDataContent();

            var payload = new StringContent($"{{\"content\":\"{message}\"}}", Encoding.UTF8, "application/json");
            form.Add(payload, "payload_json");

            await using var stream = File.OpenRead(filePath);
            var fileContent = new StreamContent(stream);
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
            form.Add(fileContent, "files[0]", Path.GetFileName(filePath));

            var response = await client.PostAsync(webhook, form);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private int RunProcess(string fileName, string arguments)
    {
        var result = RunProcessOrThrowAsync(fileName, arguments).GetAwaiter().GetResult();
        return result;
    }

    private async Task<int> RunProcessOrThrowAsync(string fileName, string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = _root,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = Process.Start(psi);
        if (process is null)
        {
            throw new InvalidOperationException($"Could not start process {fileName}");
        }

        process.OutputDataReceived += (_, e) => { if (e.Data is not null) Console.WriteLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) Console.Error.WriteLine(e.Data); };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Command failed ({process.ExitCode}): {fileName} {arguments}");
        }

        return process.ExitCode;
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine($"[ERROR] {message}");
        return 1;
    }
}
