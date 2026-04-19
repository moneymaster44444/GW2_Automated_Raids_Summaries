# Refactor notes

## What changed

The batch layer still orchestrates the pipeline. The inline `>>` echo blocks that
generated `.ps1` files at runtime were replaced with committed scripts under
`Scripts/`.

## Before / after line counts

| File | Before | After | Delta |
|---|---:|---:|---:|
| `process_logs.bat` | 565 | 435 | -130 (-23%) |
| `establish_config_files.bat` | 78 | 50 | -28 (-36%) |
| `build_elite_insights.bat` | 67 | 67 | 0 (unchanged — already clean) |
| `Scripts/Get-DateTag.ps1` | — | 12 | new |
| `Scripts/Establish-Configs.ps1` | — | 26 | new |
| `Scripts/Post-DiscordSummary.ps1` | — | 131 | new |
| **Total batch lines** | **710** | **552** | **-158 (-22%)** |
| **Total (batch + scripts)** | **710** | **721** | **+11** |

The `process_logs.bat` target in the brief was under 300 lines. The actual result is
435 lines. The 300 estimate assumed a thinner `:notify_discord` subroutine; after
extraction, `:notify_discord` is still 62 lines because it must (a) read the first
non-comment line of the webhook file, (b) capture PowerShell stdout to a temp file,
(c) parse `RESULT=` / `NAME=` markers, and (d) stitch results back through
`endlocal & set` to the caller. Each of those is a few irreducible lines of batch.
The main pipeline flow (lines 1–337) is ~23% shorter than before.

## Behavior differences worth reviewing

1. **Discord HTML → ZIP fallback is now a single PowerShell invocation**, not three
   (upload HTML, Compress-Archive, upload ZIP). One-shot script decides internally
   whether to retry with a ZIP. Observable behavior is unchanged: the console still
   shows `uploading HTML attachment`, then on failure `HTML upload failed - trying
   ZIP fallback`, `Building ZIP file`, `ZIP file written to:`, `uploading ZIP
   fallback`, `Posted Discord notification (ZIP).`

2. **`:compute_date_tag` no longer has fallbacks.** Previously: PowerShell → WMIC →
   `%DATE%` parsing → literal `unknown`. Now: PowerShell only, falling back to
   `unknown` if the call fails. We already required PowerShell for the Discord post,
   so the WMIC / `%DATE%` tiers were dead.

3. **PowerShell binary discovery was removed.** Previously the batch enumerated
   `%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe`, a `Sysnative`
   variant, and bare `powershell.exe`, using the first that existed. Now it always
   calls `powershell.exe` (on PATH on every supported Windows). If this breaks on
   some environment we haven't considered, a single `where powershell` check at the
   top can be added.

4. **`discord_webhook.txt` is now committed empty**, not generated on first run.
   On the first run after pulling this change, `process_logs.bat` calls
   `git update-index --skip-worktree` on it so local edits don't show as pending
   changes. A marker file at `Resources/Config/.webhook_skipworktree_applied`
   prevents re-running the git call. Both are silently skipped if git isn't
   installed or the working directory isn't a git checkout.

   **Upgrade note for existing installs:** users who already have a populated
   `discord_webhook.txt` locally (as an untracked file, since it was previously in
   `.gitignore`) may see a pull conflict when this change lands. Workaround: move
   the file aside, pull, then run `process_logs.bat` and paste the URL back in.

5. **`establish_config_files.bat` no longer creates `discord_webhook.txt`.** The
   file is committed to the repo so the create-if-missing block was removed. The
   secrets-directory `mkdir` is still there defensively.

6. **Label and comment style standardized.** All labels are now `:snake_case` with
   no leading underscore (`:run_ei`, `:fail`, `:notify_discord`, etc). All inline
   comments are lowercase `rem`.

## File layout after refactor

```
Scripts/
  Get-DateTag.ps1           # prints MM-dd-yy
  Establish-Configs.ps1     # sample.* -> real config files with token replacement
  Post-DiscordSummary.ps1   # uploads HTML attachment; falls back to ZIP internally
Resources/Config/Secrets/
  discord_webhook.txt       # committed empty; local edits are skip-worktree'd
Resources/Config/
  .webhook_skipworktree_applied   # (gitignored) marker; presence = don't re-apply
```

## Verifying

- End-to-end: drop a sample log in `Raid_Logs/`, run `process_logs.bat`, confirm
  `Raids_Summaries/INC_<date>.html` appears.
- skip-worktree: after a first successful run, paste a URL into
  `discord_webhook.txt` and confirm `git status` shows no pending change.
- Discord skip: leave `discord_webhook.txt` empty and confirm the pipeline finishes
  with `Discord notification skipped (reason: No webhook URL set)` and exit 0.

## Self-updater

End users install from tagged release zips (no git clone). The app version lives
in a single-line `VERSION` file at the repo root (e.g. `v0.1.11`), bumped per
release tag. Split of concerns:

- **Check** — `process_logs.bat` appends a silent-on-failure block at the very
  end that calls `Scripts/Check-Latest-Release.ps1` and, if a newer tag exists,
  prints one `[UPDATE] New release … available. Run update.bat to upgrade.`
  line. Network/API failures exit 0 with no output so the pipeline summary is
  unchanged for offline users.
- **Apply** — `update.bat` is a terse wrapper around
  `Scripts/Update-FromRelease.ps1`. The PowerShell script downloads the latest
  release zipball to `%TEMP%`, validates the extracted shape (single top-level
  folder, contains `process_logs.bat` and `VERSION`), then mutates the install.
  Batch files can't cleanly self-update mid-run; keeping the wrapper to three
  lines means cmd.exe's buffered read won't care if `update.bat` is overwritten.

Update semantics:

- **Preserve** (never touched): `Raid_Logs/`, `Raids_Summaries/`,
  `Resources/Config/Secrets/discord_webhook.txt`,
  `Resources/Config/.webhook_skipworktree_applied`.
- **Wholesale replace** (deleted before copy): `Resources/Elite Insights/`,
  `Resources/EI Combiner/`. This drops the built EI CLI at
  `Resources/Elite Insights/GW2EI.bin/`; the next `process_logs.bat` run detects
  the missing exe and invokes `build_elite_insights.bat` to rebuild it. This is
  intentional — the EI binary always matches the subtree source committed at
  that tag.
- **Regenerate** (deleted after copy): `Resources/Config/EliteInsights.conf`
  and `Resources/Config/top_stats_config.ini` are removed so the next run's
  `establish_config_files.bat` step rebuilds them from the (possibly updated)
  sample templates.
- **Overwrite** everything else (the app code, `Scripts/`, `VERSION`, etc.).

Version comparison uses strict `v\d+\.\d+\.\d+` numeric compare per part;
anything not matching falls back to string inequality so unrecognised tags
still trigger an update notice.

**Known limitation.** Only the two subtree folders are wholesale-replaced.
App-code files (anything under the repo root outside `Resources/Elite Insights/`
and `Resources/EI Combiner/`) are copy-over-only — files removed upstream
remain in the local install until a user deletes them manually. If this becomes
a problem, a manifest-based delete pass driven by the release tarball's file
listing is the natural follow-up.
