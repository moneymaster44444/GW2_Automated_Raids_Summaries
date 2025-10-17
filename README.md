# GW2_Automated_Raids_Summaries

A batch process that automates [Elite Insights](https://github.com/baaron4/GW2-Elite-Insights-Parser) and [EI Combiner](https://github.com/Drevarr/GW2_EI_log_combiner) log processing for Guild Wars 2 arcdps raid logs.

---
## Before you begin, install the following runtimes and tools.

- [Python 3](https://www.python.org/downloads/) (to run EI Combiner's python script in `process_logs.bat`)
  - After installing Python, install xlsxwriter: `pip install requests glicko2 xlsxwriter`
- [.NET SDK 8](https://dotnet.microsoft.com/en-us/download) (used by `build_elite_insights.bat` to build `GuildWars2EliteInsights-CLI.exe`)
- [Node.js](https://nodejs.org/en/download)
  - After installing Node.js, install tiddlywiki: `npm install -g tiddlywiki`
    - To automatically load and generate the summary HTML file without having to manually drag and drop the generated JSON file from EI Combiner.
---
## How to Use

1. (First time only)From the repo root, run:
   ```bat
   establish_config_files.bat
   build_elite_insights.bat
   ``` 
2. Place your arcDPS logs into:
   ```
   Raid_Logs
   ```
3. (Optional) Set Discord webhook URL to automatically post the results to Discord
     - Create a webhook on a Discord channel of your choice and paste the webhook URL to:
     ```
     \Resources\Config\Secrets\discord_webhook.txt
     ```
5. Run:
   ```bat
   process_logs.bat
   ```
   If you did step 4, then your summary will post to Discord at the end of the batch and you are done.

   Otherwise, the batch will create a combined JSON file (`Drag_and_Drop_Log_Summary_for_############.json`) in (`Raids_Summaries`) which can be dragged into:
   ```
   Resources\EI Combiner\Example_Output\Top_Stats_Index.html
   ```
   Manually post the resulting HTML file to Discord.
---
## Batch Files Overview

- **`establish_config_files.bat`**  
  Creates the required config files for Elite Insights and EI Combiner.  
  Run this **once** after cloning the project.

- **`build_elite_insights.bat`**  
  Builds the Elite Insights CLI executable at:  
  ```
  Resources\Elite Insights\GW2EI.bin\Release\CLI\GuildWars2EliteInsights-CLI.exe
  ```
  Run this **once** after cloning the project (after `establish_config_files.bat`).

- **`process_logs.bat`**  
  Processes your arcDPS logs by running them through Elite Insights and EI Combiner.  
  Produces a combined JSON file (`Drag_and_Drop_Log_Summary_for_############.json`) in (`Raids_Summaries`) that can be dragged into:  
  ```
  Resources\EI Combiner\Example_Output\Top_Stats_Index.html
  ```

- **`get_latest_ei_and_ei_combiner.bat`**  
  Development utility to pull the latest upstream versions of Elite Insights and EI Combiner subtrees.  
  Only run this when new releases are available upstream.  
  Updates may introduce breaking changes that require adjustments in this repo. Always test after running it.

---
## Advanced Usage / Development

If you are maintaining this repository and want to update either Elite Insights or EI Combiner:

- Run:
  ```bat
  get_latest_ei_and_ei_combiner.bat
  ```
- This will pull the latest upstream changes from Elite Insights and EI Combiner into the repo.
- After updating, thoroughly test `process_logs.bat` to ensure no breaking changes were introduced before committing and pushing.
