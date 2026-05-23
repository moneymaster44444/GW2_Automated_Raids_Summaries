# Resolves the active raid window from RAID_HOURS_<DAY> entries in config.txt.
#
# Emits NAME=VALUE lines on stdout for the caller (process_logs.bat) to parse:
#   STATUS    = ok | none
#   START     = window start in local time, ISO format (yyyy-MM-ddTHH:mm:ss)
#   END       = upper bound for log filter, ISO format
#   RAID_DATE = raid date for the HTML filename (MM-dd-yy)
#   DAY_NAME  = day-of-week the raid is attributed to (e.g. Tuesday)
#   REASON    = human-readable explanation
#
# STATUS=ok means a window was found within the grace period; the caller should
# copy logs whose LastWriteTime falls in [START, END]. STATUS=none means no
# eligible window (all RAID_HOURS_* blank, last raid older than grace, or a
# parse error) and no logs should be copied.
#
# Exit code is 0 in normal operation (including STATUS=none). Non-zero is
# reserved for unexpected failures (e.g. missing config file).

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ConfigFile,
    [datetime]$Now = (Get-Date)
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $ConfigFile)) {
    Write-Host "STATUS=none"
    Write-Host "REASON=Config file not found: $ConfigFile"
    exit 0
}

# ---------------------------------------------------------------------------
# Parse config.txt into a hashtable of KEY -> raw VALUE.
# ---------------------------------------------------------------------------
$cfg = @{}
foreach ($line in Get-Content -LiteralPath $ConfigFile) {
    if ($line -match '^\s*#') { continue }
    if ($line -match '^\s*([A-Za-z_][A-Za-z0-9_]*)\s*=(.*)$') {
        $cfg[$Matches[1]] = $Matches[2]
    }
}

# ---------------------------------------------------------------------------
# Time parsing helpers
# ---------------------------------------------------------------------------

# Accepts "7:30PM", "7:30 PM", "07:30PM", "7PM", "19:30", "19", etc. Returns
# @{ Hour; Minute } or $null on failure. InvariantCulture keeps AM/PM parsing
# stable on non-English Windows installs.
function ConvertFrom-TimeSpec {
    param([string]$Text)
    if ([string]::IsNullOrWhiteSpace($Text)) { return $null }
    $t = $Text.Trim().ToUpperInvariant()
    $formats = @(
        'h:mmtt','hh:mmtt','h:mm tt','hh:mm tt',
        'htt','hhtt','h tt','hh tt',
        'H:mm','HH:mm','H','HH'
    )
    $invariant = [System.Globalization.CultureInfo]::InvariantCulture
    $dt = [DateTime]::MinValue
    foreach ($fmt in $formats) {
        if ([DateTime]::TryParseExact($t, $fmt, $invariant, [System.Globalization.DateTimeStyles]::None, [ref]$dt)) {
            return @{ Hour = $dt.Hour; Minute = $dt.Minute }
        }
    }
    return $null
}

# Friday "RESET" -> local time corresponding to the Saturday 02:00 UTC that
# immediately follows the target Friday. WvW reset is fixed at that UTC moment
# regardless of DST, so converting through ToLocalTime() yields 9PM or 10PM
# (Eastern) automatically. Window length matches the other default raid days
# (start through start + 2.5 hours).
function Get-FridayResetWindow {
    param([datetime]$FridayDate)
    $sat = $FridayDate.Date.AddDays(1)
    $resetUtc = [DateTime]::new($sat.Year, $sat.Month, $sat.Day, 2, 0, 0, [DateTimeKind]::Utc)
    $start = [DateTime]::SpecifyKind($resetUtc.ToLocalTime(), [DateTimeKind]::Unspecified)
    return @{ Start = $start; End = $start.AddMinutes(150) }
}

# Returns @{ Start; End } in local time, or $null when the day is disabled or
# the value can't be parsed. EndOnly < StartOnly is treated as crossing midnight.
# Parse-failure remarks are accumulated into $Remarks keyed by day so the same
# bad value isn't reported once per candidate window in the +/- 7-day sweep.
function Resolve-RaidWindowValue {
    param(
        [string]$Raw,
        [string]$Day,
        [datetime]$RaidDate,
        [hashtable]$Remarks
    )
    if ([string]::IsNullOrWhiteSpace($Raw)) { return $null }
    $val = $Raw.Trim()
    if ($Day -eq 'Friday' -and $val -match '^(?i)RESET$') {
        return Get-FridayResetWindow -FridayDate $RaidDate
    }
    $parts = $val -split '-', 2
    if ($parts.Count -ne 2) {
        $Remarks[$Day] = ("RAID_HOURS_{0}: expected 'START-END', got '{1}'" -f $Day.ToUpper(), $Raw)
        return $null
    }
    $startSpec = ConvertFrom-TimeSpec $parts[0]
    $endSpec   = ConvertFrom-TimeSpec $parts[1]
    if (-not $startSpec -or -not $endSpec) {
        $Remarks[$Day] = ("RAID_HOURS_{0}: could not parse times from '{1}'" -f $Day.ToUpper(), $Raw)
        return $null
    }
    $d = $RaidDate.Date
    $start = [DateTime]::new($d.Year, $d.Month, $d.Day, $startSpec.Hour, $startSpec.Minute, 0, [DateTimeKind]::Unspecified)
    $end   = [DateTime]::new($d.Year, $d.Month, $d.Day, $endSpec.Hour,   $endSpec.Minute,   0, [DateTimeKind]::Unspecified)
    if ($end -le $start) { $end = $end.AddDays(1) }
    return @{ Start = $start; End = $end }
}

# ---------------------------------------------------------------------------
# Grace period
# ---------------------------------------------------------------------------
$grace = 24
if ($cfg.ContainsKey('RAID_LOGS_GRACE_HOURS')) {
    $parsed = 0
    if ([int]::TryParse(($cfg['RAID_LOGS_GRACE_HOURS']).Trim(), [ref]$parsed) -and $parsed -gt 0) {
        $grace = $parsed
    }
}

# ---------------------------------------------------------------------------
# Build candidate windows for a +/- 7-day band around "now". Looking ahead
# matters because the "next non-empty raid start" caps the upper bound of the
# log filter - if the user runs the pipeline Wednesday afternoon, we don't
# want to vacuum up logs that bleed into Wednesday's raid.
# ---------------------------------------------------------------------------
$dayNames = @('Sunday','Monday','Tuesday','Wednesday','Thursday','Friday','Saturday')
$windows = New-Object System.Collections.Generic.List[object]
$remarks = @{}
for ($i = -7; $i -le 7; $i++) {
    $date = $Now.Date.AddDays($i)
    $dayName = $dayNames[[int]$date.DayOfWeek]
    $key = "RAID_HOURS_$($dayName.ToUpper())"
    $raw = if ($cfg.ContainsKey($key)) { $cfg[$key] } else { '' }
    $win = Resolve-RaidWindowValue -Raw $raw -Day $dayName -RaidDate $date -Remarks $remarks
    if ($win) {
        $windows.Add([pscustomobject]@{
            Start    = $win.Start
            End      = $win.End
            DayName  = $dayName
            RaidDate = $date
        })
    }
}

foreach ($msg in $remarks.Values) { Write-Host "REMARK=$msg" }

if ($windows.Count -eq 0) {
    Write-Host "STATUS=none"
    Write-Host "REASON=All RAID_HOURS_* are blank or unparseable"
    exit 0
}

# Most recent window whose start is in the past (could be in-progress or done).
$active = $windows | Where-Object { $_.Start -le $Now } | Sort-Object Start -Descending | Select-Object -First 1

if (-not $active) {
    Write-Host "STATUS=none"
    Write-Host "REASON=No raid window started yet (earliest configured start is in the future)"
    exit 0
}

# When the window is over, enforce the grace period.
$ageHours = ($Now - $active.End).TotalHours
if ($Now -gt $active.End -and $ageHours -gt $grace) {
    Write-Host "STATUS=none"
    Write-Host ("REASON=Most recent raid ({0} {1:MM-dd}) ended {2:0.0}h ago, beyond grace={3}h" -f $active.DayName, $active.RaidDate, $ageHours, $grace)
    exit 0
}

# Upper bound for log filtering: keep everything from window start through "now",
# but stop at the start of the next non-empty raid day so a late run doesn't
# bleed into the next raid's logs.
$nextWindow = $windows | Where-Object { $_.Start -gt $active.Start } | Sort-Object Start | Select-Object -First 1
$upper = $Now
if ($nextWindow -and $nextWindow.Start -lt $upper) { $upper = $nextWindow.Start }

$raidDateTag = $active.RaidDate.ToString('MM-dd-yy')

Write-Host "STATUS=ok"
Write-Host ("START={0:yyyy-MM-ddTHH:mm:ss}" -f $active.Start)
Write-Host ("END={0:yyyy-MM-ddTHH:mm:ss}" -f $upper)
Write-Host "RAID_DATE=$raidDateTag"
Write-Host "DAY_NAME=$($active.DayName)"
if ($Now -le $active.End) {
    Write-Host ("REASON={0} raid in progress (started {1:0.0}h ago)" -f $active.DayName, ($Now - $active.Start).TotalHours)
} else {
    Write-Host ("REASON={0} raid ended {1:0.0}h ago (within grace={2}h)" -f $active.DayName, $ageHours, $grace)
}
exit 0
