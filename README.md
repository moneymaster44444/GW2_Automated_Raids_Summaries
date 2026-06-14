# GW2_Automated_Raids_Summaries

A batch process that automates [Elite Insights](https://github.com/baaron4/GW2-Elite-Insights-Parser) and [EI Combiner](https://github.com/Drevarr/GW2_EI_log_combiner) log processing for Guild Wars 2 arcdps raid logs.

---
## Before you begin, install the following required tools.

- [Python 3](https://www.python.org/downloads/) (Required to run EI Combiner's python script in `process_logs.bat`)
  - The required Python packages (`requests`, `glicko2`, `xlsxwriter`) install automatically on the first run of `process_logs.bat`. To install them manually instead: `pip install -r requirements.txt`
- [.NET SDK 8](https://dotnet.microsoft.com/en-us/download) (Required by `build_elite_insights.bat` to build `GuildWars2EliteInsights-CLI.exe`)
- [Node.js](https://nodejs.org/en/download)
  - `tiddlywiki` installs automatically on the first run of `process_logs.bat` (used to load and generate the summary HTML without having to manually drag and drop the generated JSON file from EI Combiner). To install it manually instead: `npm install -g tiddlywiki`

---
## How to Use

### 1. Place your arcDPS logs into:
   ```
   Raid_Logs
   ```
### 2. Run:
   ```bat
   process_logs.bat
   ```
   You will find the resulting HTML file in `Raids_Summaries`.

## GUI

The GUI is the main way to use this project. To set it up and launch it, run:
```bat
setup.bat
```
This builds the GUI app and starts it. On the first launch the GUI also runs the
one-time setup (Elite Insights, config files, dependencies) for you. It is a
small Windows app that wraps `process_logs.bat`:

- **Logs tab** — drag and drop `.zevtc` / `.evtc` files into `Raid_Logs`, remove a
  selection, or clear the folder.
- **Settings tab** — edit `config.txt` (guild tag, Discord webhook, log source
  folder, parallelism, and the weekly raid windows) with checkboxes, number
  fields, and time pickers. Comments in `config.txt` are preserved on save. It
  also shows the current version and has a **Check for updates** button that
  applies the latest release (the same as `update.bat`, with progress shown in
  the Output pane); your logs, summaries, and `config.txt` are preserved.
- **Run these logs** processes whatever is currently in `Raid_Logs`.
  **Run scheduled logs** first auto-copies from `LOG_SOURCE_DIR` for the active
  raid window (the same behavior a scheduled run uses), then processes. The live
  pipeline output streams in the Output pane.

On the first run, `setup.bat` also creates an app-like **GW2 Raid Summaries**
shortcut (with the guild icon, no console window) at the repo root that you can
double-click like any other program. To recreate it later, or to also place a
copy on your Desktop, run:
```bat
powershell -ExecutionPolicy Bypass -File Scripts\Create-Launcher-Shortcut.ps1 -Desktop
```

The GUI requires the **.NET 8 SDK**, which you already need to build Elite
Insights. The first launch compiles the app, so it may take a moment. On a
brand-new install, the first time you open the GUI it runs one-time setup
(building Elite Insights and installing tools) in a progress window before the
main app is ready — this only happens once.

## Updating

To pull the latest release:
```bat
update.bat
```
`process_logs.bat` prints a notice at the end of each run when a newer release
is available. Your logs, summaries, and `config.txt` are preserved. The
Elite Insights and EI Combiner folders are replaced wholesale from the release
zip, and the built EI CLI rebuilds automatically on the next `process_logs.bat`
run. Config files (`EliteInsights.conf`, `top_stats_config.ini`) regenerate from
the updated sample templates on the next run.

## Configuration

On your first run, `process_logs.bat` creates `config.txt` at the repo root
from `sample.config.txt`. Edit `config.txt` to set:

- `GUILD_TAG=` — prefix for the generated summary HTML. Output files are
  named `<GUILD_TAG>_<date>.html`. Defaults to `OnLY`.
- `DISCORD_WEBHOOK_URL=` — optional. Paste a Discord channel webhook URL
  to auto-post the summary HTML at the end of each run. Leave blank to
  skip the Discord step.

`config.txt` is gitignored, so your local edits never appear in
`git status` or risk accidental commits. If a future release adds new
fields to `sample.config.txt`, copy them into your `config.txt` manually.

---
## Details: Batch Files Overview

- `establish_config_files.bat`
  
  Creates the required config files for Elite Insights and EI Combiner.  
  This runs automatically in `process_logs.bat` as necessary.
  
- `build_elite_insights.bat`
  
  Builds the Elite Insights CLI executable at:  
  ```
  Resources\Elite Insights\GW2EI.bin\Release\CLI\GuildWars2EliteInsights-CLI.exe
  ```  
  This runs automatically in `process_logs.bat` as necessary.

- `process_logs.bat`
  
  Processes your arcDPS logs by running them through Elite Insights and EI Combiner.  
  Accepts an optional run mode as its first argument:
  - *(no argument)* — auto-copy from `LOG_SOURCE_DIR` when it is set, otherwise
    process whatever is already in `Raid_Logs` (unchanged legacy behavior).
  - `--manual` — skip the auto-copy and process the current `Raid_Logs` contents.
  - `--scheduled` — force the raid-window auto-copy from `LOG_SOURCE_DIR`
    (requires it to be set), then process. Used by the GUI's two run buttons.
  
  Produces a combined JSON file (`Drag_and_Drop_Log_Summary_for_############.json`) in (`Raids_Summaries`) that can be dragged into:  
  ```
  Resources\EI Combiner\Example_Output\Top_Stats_Index.html
  ```

- `get_latest_ei_and_ei_combiner.bat`
  
  This is a development tool to pull the latest releases of Elite Insights and EI Combiner into this repo.  
  Updates may introduce breaking changes that require adjustments in this repo.  
  Always test after running it.  

---

## Advanced Use / Development

If you are maintaining this repository and want to update either Elite Insights or EI Combiner:

- First, update the `3rd_party_repo_version.lock`
  - Find the latest release version tag names for both EI and EI Combiner projects.
  - Update the "ref" field with the version tag names.
  - Commit to local so that your change list is clean before moving to the next step.
- Run:
  ```bat
  get_latest_ei_and_ei_combiner.bat
  ```
  - This will pull the release versions of Elite Insights and EI Combiner into `\Resources\Elite Insights` and `\Resources\EI Combiner` respectively.
  - Make sure your git repo does not have any changes when running this. If you get any error stating that your repo has changes even though it's clean (phantom changes), try running `git restore` in cmd at the root:
    - ```
      git restore -s@ -SW -- Resources/"EI Combiner"

      git restore -s@ -SW -- Resources/"Elite Insights"
      ```
- After updating, thoroughly test `process_logs.bat` to ensure no breaking changes were introduced before committing and pushing.
