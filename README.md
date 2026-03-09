# GW2 Automated Raids Summaries (.NET Console App)

This project now uses a **single C# console application** to run the same workflow that was previously spread across batch files:

1. Create real config files from sample templates.
2. Build the Elite Insights CLI.
3. Ensure `discord_webhook.txt` exists.
4. On normal runs, clean intermediate files, parse logs, combine JSON, generate summary HTML with TiddlyWiki, and optionally post to Discord.

---

## What it does

The app reproduces the business flow from the original scripts:

- Reads logs from `Raid_Logs` (`.zevtc` and `.evtc`).
- Runs Elite Insights CLI to generate JSON in `Raids_Summaries/EI_json_output`.
- Runs EI Combiner (`tw5_top_stats.py`) to produce `Drag_and_Drop_Log_Summary_*.json`.
- Loads that JSON into TiddlyWiki in headless mode and builds `index.html`.
- Writes final summary to `Raids_Summaries/INC_MM-dd-yy.html`.
- If a webhook URL exists in `Resources/Config/Secrets/discord_webhook.txt`, uploads the summary to Discord (HTML first, ZIP fallback).

---

## Prerequisites

Install these tools before using the app:

- **.NET SDK 8+**
- **Python 3**
  - Install required packages:
    - `pip install requests glicko2 xlsxwriter`
- **Node.js**
  - Install TiddlyWiki globally:
    - `npm install -g tiddlywiki`

---

## Console App Location

- Project file: `src/Gw2RaidSummaryTool/Gw2RaidSummaryTool.csproj`
- Entry point: `src/Gw2RaidSummaryTool/Program.cs`

---

## Usage

From repository root:

```bash
dotnet run --project src/Gw2RaidSummaryTool -- help
```

### Commands

```bash
dotnet run --project src/Gw2RaidSummaryTool -- init
```
Creates/refreshes:
- `Resources/Config/EliteInsights.conf` from `sample.eliteinsights.conf`
- `Resources/Config/top_stats_config.ini` from `sample.top_stats_config.ini`
- `Resources/Config/Secrets/discord_webhook.txt` (if missing)

```bash
dotnet run --project src/Gw2RaidSummaryTool -- build
```
Builds/publishes Elite Insights CLI to:
- `Resources/Elite Insights/GW2EI.bin/Release/CLI`

```bash
dotnet run --project src/Gw2RaidSummaryTool -- run
```
Full pipeline (default command if omitted):
- setup if needed
- build EI CLI if missing
- process logs
- build summary HTML
- optional Discord upload

---

## Discord setup (optional)

Put your webhook URL as the first non-empty, non-comment line in:

`Resources/Config/Secrets/discord_webhook.txt`

Example:

```text
# Guild raid summary webhook
https://discord.com/api/webhooks/...
```

---

## Notes for maintainers

- The old batch scripts are still in the repository as references to prior behavior.
- The C# app keeps the flow intentionally simple and readable so entry-level developers can maintain it.
- If you need to update vendored EI/EI Combiner source trees, continue using `3rd_party_repo_version.lock` + `get_latest_ei_and_ei_combiner.bat`.
