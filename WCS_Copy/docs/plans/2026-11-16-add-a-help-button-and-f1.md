# 2026-11-16 Add a Help button and F1

## Context

The owner asked (2026-11-16, in Romanian) for a Help button with a nice icon in CopyWcs's
window, also on F1, opening the wiki manual at the section of the current tab, "as FlatCheck
did" (FlatCheck R-141, ADR 0112). Today F1 opens About (`ApplicationCommands.Help`, R-035,
R-038) and the window has no way to reach its manual,
`https://github.com/tcarean/AstroDarkroom/wiki/CopyWcs`. CopyWcs has no tabs, so the owner
chose in chat which section opens, by what the outcome card shows:

- no Preview or Copy yet: `#the-window`;
- the card shows a result: `#the-result`;
- the card shows **Refused** or a **WARNING**: `#when-something-goes-wrong` (one row per message).

The card keeps the last outcome during a run, so the rule reads the card, not the run.

## What is decided (stated, not asked)

- As FlatCheck: Help sits left of About in the band's right column, icon then the word
  `Help` (`Ajutor`); About keeps its button and loses F1, its tooltip without `(F1)`.
  The icon is FlatCheck's drawing (a question mark on a disc of `AccentBr`), copied from
  AstroDarkroom's `App.xaml`, since `Theme/Dark.xaml` is already AstroDarkroom's palette copy.
- F1 is read in the window's `PreviewKeyDown` and a held key's repeats do nothing, as in
  ImageVisualCompare (its ADR 0056), not through `ApplicationCommands.Help`: FlatCheck's command
  opens a browser tab for every repeat of a held F1. The Help button is a plain `Click`.
- Tooltip: `The manual, at the section about what is on screen (F1)` /
  `Manualul, la sectiunea despre ce e pe ecran (F1)` (ImageVisualCompare's words, without
  diacritics); About: `Version, licence, components` / `Versiune, licenta, componente`.
- The addresses and the section rule live in the window project (`Presentation/Manual.cs`),
  as FlatCheck's: the console has no manual link, and an address is no sentence about the copy.
- About keeps its own link handler (a copied DarksCheck piece, as DarksCheck's Help task kept
  it); the manual opens through a new `Presentation/ExternalLink.cs` that hands the shell only
  an absolute http/https address (ImageVisualCompare's guard) and logs a refused start at Debug.
- Each press logs `Help opened: <address>` at Info (as FlatCheck).

## Steps

1. Worktree: `git worktree add .claude/worktrees/Add-a-help-button-to-CopyWcs -b claude/Add-a-help-button-to-CopyWcs main`
   from `WCS_Copy/`; save this plan as `docs/plans/2026-11-16-add-a-help-button-and-f1.md`.
2. **Requirements** (`docs/requirements.md`): new R-047 (the button, its place, icon, word,
   tooltip, F1 anywhere in the window, a held F1 once, the three sections with an example, the
   log line); R-035 (About from its button only), R-038 (the band's right column: Help, then
   About), R-043 (the Help line), R-036 (`Ajutor` in the window's Romanian words).
3. **ADR 0016** `docs/decisions/0016-help-and-f1-open-the-wiki-manual-at-the-section-of-the-card.md`:
   the owner's answer, the decisions above, rejected: the top of the page, `ApplicationCommands.Help`
   (repeats), an icon font, the icon alone, a help window; consequence: a renamed wiki heading
   must be followed in `Manual.cs`, which no gate test can see.
4. **Tests first** (`tests/CopyWcs.Wpf.Tests/HelpTests.cs`, new, `window` collection on
   `StaFixture`):
   - `R047_` the three addresses, and `SectionFor` over the four card states;
   - F1 pressed through the window's key handler (IVC's `PressF1` with its `SetRepeat` and
     `KeySource`) and the Help button's click, with `OpenAddress` replaced: empty window
     `#the-window`; after a Preview of `WindowFixture`'s pair `#the-result`; after a refusal
     (200-px destination) and after a forced copy with its warning `#when-something-goes-wrong`;
   - a held F1 opens once; the button holds the `HelpIcon` image and the word, in both languages,
     between the explanation and About, with its tooltip; About opens by `Click`, no F1;
   - `ExternalLink` never hands a relative or `file:` address to the shell (a Debug line).
   - `WindowLoggingTests`: the `Help opened: <address>` Info line.
   - `WindowSmokeTests.R038_...`: rewritten for the changed R-038 (two buttons, About without
     the Help command and without `(F1)`), the Romanian words list gains `Ajutor`. These follow
     the requirement's change; no test is edited to pass.
5. **Code**: `Presentation/Manual.cs` (enum `ManualSection`, `Page`, `Url`, `SectionFor(hasOutcome,
   hasError, hasWarnings)`), `Presentation/ExternalLink.cs`, `Theme/Dark.xaml` (`HelpIcon`,
   header comment), `MainWindow.xaml` (no `CommandBinding`, `PreviewKeyDown`, the right column a
   horizontal `StackPanel`: `HelpButton` with `AutomationProperties.Name`, then About at
   `MinWidth` 88), `MainWindow.xaml.cs` (`ShowAbout` on `Click`, `OpenAddress`, `OnPreviewKeyDown`,
   `OnHelpKey(isRepeat)`, `ShowHelp`). XML docs on every type and method added or edited.
6. **Measure the band**: render at 720 and 800 px in both languages (`COPYWCS_DUMP_SCREENS`) and
   read the band's height, today 57 px with the explanation in two lines. If it grows at either
   width, stop and put the heights and the options (accept, a wider `MinWidth`, ...) to the owner.
7. **Docs**: `docs/design/window.md` (the band, the key handler, Help, the tests), `README.md`
   (line 104: Help or F1, About), `CHANGELOG.md` one `Added` line (the window is unreleased, so
   F1's move needs no `Changed` line).
8. **Gate** green (`scripts/verify.ps1`, plus `check-design.ps1 -Since main`), read the whole
   diff, commit on the branch, then a subagent code review offered before the merge.
9. **Wiki** (`E:\Photografie\AstroDarkroomWiki\CopyWcs.md`, "The top band"): Help or F1, About
   by its button; on a wiki branch `claude/Describe-the-Help-button-in-the-CopyWcs-manual`,
   committing `CopyWcs.md` alone (the clone holds other sessions' uncommitted pages). Merge and
   push asked. `img/CopyWCS/main.PNG` shows the old band until retaken.

## Verification

- `dotnet test CopyWcs.sln --filter "FullyQualifiedName~R047|FullyQualifiedName~R038|FullyQualifiedName~R043"`
  red before the code, green after; then the whole gate.
- The PNG renders of the band in both languages, read by eye.
- A live run of `CopyWcs.Wpf.exe` pressing F1 and Help is not done unasked (live input only to a
  foreground app when the owner is away); the key goes through the window's own handler in tests.

## Open for the owner afterwards

Merge; wiki push; a minor version bump (a new feature) is the owner's call.

## What deviated

- The requirement is R-039, not R-047: the window's block R-030 to R-039 had it free, and
  `docs/skills.md` puts a window behaviour there.
- Step 6 found the band grown: with Help, the explanation needs 787 px inside the window (803
  with the frame) for two lines, in both languages, and the window opened 800 px wide, so the
  band was 70 px instead of 57 at the default size and at the minimum. Asked with renders, the
  owner chose to open the window 820 px wide (minimum width unchanged). R-038 says so, ADR 0016
  records it, and R-038's test lays the band out at the opening width less the 16 px frame.
- The button's icon, word, place and tooltips are checked in R-038's rendered theory in
  `WindowSmokeTests`, where the rendering tests live, not in `HelpTests`.
- ADR 0016 supersedes ADR 0008 in part (About alone on the band's right, opened by F1); 0008's
  Status line says so.
