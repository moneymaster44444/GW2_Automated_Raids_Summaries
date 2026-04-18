# GW2_Automated_Raids_Summaries

A batch process that automates [Elite Insights](https://github.com/baaron4/GW2-Elite-Insights-Parser) and [EI Combiner](https://github.com/Drevarr/GW2_EI_log_combiner) log processing for Guild Wars 2 arcdps raid logs.

---
## Before you begin, install the following required tools.

- [Python 3](https://www.python.org/downloads/) (Required to run EI Combiner's python script in `process_logs.bat`)
  - After installing Python, install xlsxwriter: `pip install requests glicko2 xlsxwriter`
- [.NET SDK 8](https://dotnet.microsoft.com/en-us/download) (Required by `build_elite_insights.bat` to build `GuildWars2EliteInsights-CLI.exe`)
- [Node.js](https://nodejs.org/en/download)
  - After installing Node.js, install tiddlywiki: `npm install -g tiddlywiki`
    - To automatically load and generate the summary HTML file without having to manually drag and drop the generated JSON file from EI Combiner.

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

## (Optional) Discord webhook

   To automatically post the summary HTML to a Discord channel at the end of each run,
   paste the channel's webhook URL into:
   ```
   \Resources\Config\Secrets\discord_webhook.txt
   ```
   This file is committed empty. On your first run, `process_logs.bat` marks it
   `--skip-worktree` in git so your local edit never appears in `git status` or gets
   committed accidentally. If the file is empty or whitespace-only, the Discord step
   is silently skipped.

   To undo the skip-worktree flag (for example, if you actually want to commit a change
   to the tracked copy):
   ```
   git update-index --no-skip-worktree "Resources/Config/Secrets/discord_webhook.txt"
   ```
   
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
