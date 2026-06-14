using System.Text.RegularExpressions;

namespace GW2RaidsGui.Config;

/// <summary>
/// Reads and writes the repo's <c>config.txt</c> (NAME=VALUE pairs, with
/// <c>#</c> comment lines) while preserving every comment and the original
/// ordering. Saving updates existing key lines in place and only appends keys
/// that are genuinely absent, so the heavily-commented template stays intact.
/// </summary>
public sealed class ConfigFile
{
    public static readonly string[] DayKeys =
    {
        "RAID_HOURS_MONDAY", "RAID_HOURS_TUESDAY", "RAID_HOURS_WEDNESDAY",
        "RAID_HOURS_THURSDAY", "RAID_HOURS_FRIDAY", "RAID_HOURS_SATURDAY",
        "RAID_HOURS_SUNDAY",
    };

    private static readonly Regex KeyLine =
        new(@"^\s*(?<key>[A-Za-z_][A-Za-z0-9_]*)\s*=", RegexOptions.Compiled);

    private readonly string _path;
    private readonly List<string> _lines = new();
    private readonly Dictionary<string, int> _keyToLine =
        new(StringComparer.OrdinalIgnoreCase);

    private ConfigFile(string path) => _path = path;

    public string Path => _path;

    /// <summary>
    /// Loads config.txt. If it does not exist, it is first created from
    /// sample.config.txt (matching what process_logs.bat does on first run).
    /// </summary>
    public static ConfigFile Load(AppPaths paths)
    {
        if (!File.Exists(paths.ConfigFile) && File.Exists(paths.SampleConfigFile))
            File.Copy(paths.SampleConfigFile, paths.ConfigFile);

        var cfg = new ConfigFile(paths.ConfigFile);
        if (File.Exists(paths.ConfigFile))
        {
            foreach (var line in File.ReadAllLines(paths.ConfigFile))
                cfg.AddLine(line);
        }
        return cfg;
    }

    private void AddLine(string line)
    {
        _lines.Add(line);
        // A leading '#' (after optional whitespace) marks a comment, never a key.
        if (line.TrimStart().StartsWith('#')) return;
        var m = KeyLine.Match(line);
        if (m.Success)
        {
            var key = m.Groups["key"].Value;
            // First occurrence wins, mirroring the batch loader.
            if (!_keyToLine.ContainsKey(key))
                _keyToLine[key] = _lines.Count - 1;
        }
    }

    /// <summary>Returns the raw value after the first '=', or empty if the key is absent.</summary>
    public string Get(string key)
    {
        if (!_keyToLine.TryGetValue(key, out var idx)) return string.Empty;
        var line = _lines[idx];
        var eq = line.IndexOf('=');
        return eq < 0 ? string.Empty : line[(eq + 1)..];
    }

    public void Set(string key, string value)
    {
        value ??= string.Empty;
        if (_keyToLine.TryGetValue(key, out var idx))
        {
            var line = _lines[idx];
            var eq = line.IndexOf('=');
            var lhs = eq < 0 ? key : line[..eq]; // keep original key text/spacing
            _lines[idx] = lhs + "=" + value;
        }
        else
        {
            _lines.Add(key + "=" + value);
            _keyToLine[key] = _lines.Count - 1;
        }
    }

    public RaidHoursValue GetRaidHours(string dayKey)
    {
        var allowReset = dayKey.Equals("RAID_HOURS_FRIDAY", StringComparison.OrdinalIgnoreCase);
        return RaidHoursValue.Parse(Get(dayKey), allowReset);
    }

    public void SetRaidHours(string dayKey, RaidHoursValue value)
        => Set(dayKey, value.ToConfigString());

    public void Save() => File.WriteAllLines(_path, _lines);
}
