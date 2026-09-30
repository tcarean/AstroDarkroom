# 0035. The Siril preprocessing script is run from the problem card, through the user's own siril-cli, chosen from Siril's list

Date: 2026-09-25
Status: accepted 2026-09-25

## Context

Retroactive: recorded on 2026-09-25 from docs/design/two-applications.md ("Running the Siril
script from the window") (and the memory notes flatcheck-siril-preprocess-button and
siril-install-layout); the decision itself dates from 2026-09-15. A folder with no preprocessing
run, or no flat master in `masters/` or `process/`, could only be sent back to Siril by hand.
The owner asked for the measurement before the code, on a small copied session, Siril 1.4.4:
stdout is live through a pipe (gaps between lines match the work; exit 0 on success, 1 when the
script stops at a command; stderr empty); three quarters of the output is progress (1735 lines
for a 3+3+3-frame session, 1324 of them `progress:` lines or the ANSI-prefixed update form);
re-running overwrites cleanly (a second run over a complete `process/` and a third over one with
the master and `pp_light*` deleted rebuilt every file and exited 0, the only complaints being
`seqfile ... already exists, not overwriting`); Siril prints only the verb (`Running command: cd`).
On the owner's machine `siril-cli.exe` is at `C:\Program Files\Siril\bin\`, not in PATH; the
scripts repo is cloned into `%LOCALAPPDATA%\siril-scripts`, and `config.1.4.ini` lists
`script_path` and `selected_scripts`.

## Decision

The problem card carries a "Ruleaza in Siril" button that runs `siril-cli.exe -d <session>
-s <script>` from the user's own install, shows the output live in `SirilRunWindow`, and on exit
code 0 runs `Check()` again through `MainViewModel.ReopenAsync`, so whether the master appeared
is decided by the same code as always. The owner's five decisions, recorded so nobody re-opens
them: run the whole script, as simple as possible (no trimmed stdin script); locate Siril in the
user's install, never bundle or copy a script; a running Siril GUI is a warning, not a refusal;
the user chooses the script from a list - what Siril's script menu shows, from `config.<ver>.ini`
- with `OSC_Preprocessing_WithoutDark.ssf` preselected because it is the owner's script; measure
before code. The script is chosen on the card, not in the run window. Inputs are checked before
launching: `SirilScript.RequiredFolders` reads the plain-name `cd <name>` commands and
`SirilSession.MissingInputFolders` refuses a folder lacking any of them or with one empty.

## Alternatives rejected

- **The named-pipe mode (`-p`).** Not needed; a plain `Process` with redirected stdout is live.
- **Bundling a script.** The tool runs exactly the file Siril would, from wherever Siril keeps it
  (on this machine the scripts repo, not the install's `scripts/` folder with its six stock ones).
- **Asking for the script in the run window.** The window is the log of a run already started; a
  window that first asks and then runs would be two screens in one.
- **Refusing when the Siril GUI is running.** Two processes writing the same `process/` corrupt
  it, but the tool cannot know which folder the GUI has open; checked on the card and on the click.
- **Showing every stdout line.** `SirilLogLine` turns progress into a bar and shows only the
  `log:` lines, as Siril's own log pane does; debug prints are dropped.

## Consequences

- Nothing here is a number, so nothing is in the core. `SirilRunViewModel` counts the `Running
  command:` lines and reads the full command out of the script, to say `comanda 2 din 20:  cd biases`.
- "Opreste" mid-run leaves a folder the next run fixes, and the cancel message says so. The
  pre-check is a rule of form; a script that enters folders differently is simply not pre-checked.
- `MainViewModel` takes `Func<SirilInstall>` and `Func<bool>` as optional constructor arguments so
  the tests do not depend on the machine. `SirilCliIntegrationTests` use the real `siril-cli` when
  present (a probe script on the fixture master, one that fails on `cd nu-exista`) and pass
  without checking otherwise, since xunit 2 has no dynamic skip. The `SirilLogLine` parser is
  pinned on recorded lines; `WindowSmokeTests` renders the run window in three states.
- Amended 2026-09-30 by ADR 0055: the command line is `siril-cli.exe -d <session> -s <script>
  -i <private copy of Siril's settings>`. Without `-i`, siril-cli rewrote the user's
  `config.<ver>.ini` at the end of every script, and a failed save deleted it. The
  `SirilCliIntegrationTests` now run on an empty private copy in their workspace.
- Source note: docs/design/two-applications.md; memory notes flatcheck-siril-preprocess-button, siril-install-layout.
