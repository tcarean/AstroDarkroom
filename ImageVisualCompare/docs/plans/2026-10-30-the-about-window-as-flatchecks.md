# The About window as FlatCheck's, in ImageVisualCompare's colours

On `claude/Log-ImageVisualCompare-with-NLog`, before its merge; once approved, this file is saved as
`docs/plans/2026-10-30-the-about-window-as-flatchecks.md` and committed first.

## Context

The owner asked (2026-10-30) why ImageVisualCompare's About looks different from FlatCheck's, and
to make it look the same. FlatCheck, LightsCheck, DarksCheck, CopyWcs and AstroPhotoAnalyzer all
have FlatCheck's About window (`FlatCheck.Wpf/Views/AboutWindow.xaml`): the icon's largest frame
beside the name, a tagline, the version and the copyright, a `LICENCE` card with the warranty
paragraph and a link, a `THIRD-PARTY COMPONENTS` card with one row per component, and `Close`.
ImageVisualCompare's About has been plain text since 1.0.0, in its own `MessageDialog` since 1.1.1
(ADR 0024), now 14 lines with NLog's three at the end. The owner chose this tool's own palette for the window
(answered 2026-10-30): FlatCheck's layout, cards and type sizes; ImageVisualCompare's colours.

## The window

`src/ImageVisualCompare.Wpf/AboutWindow.xaml(.cs)`, beside `MessageDialog`, a port of FlatCheck's
markup: `Width="600"`, `SizeToContent="Height"`, `NoResize`, `ShowInTaskbar="False"`,
`CenterOwner`, `Background` `ChromeBrush`, `Foreground` `TextBrush`, Segoe UI,
`TextFormattingMode="Display"`, `Hyperlink.RequestNavigate` caught on the window. No `Icon`
(every window here takes the executable's). Top to bottom:

- header: `Logo` (72 × 72, the .ico's 256-pixel frame), `Image Visual Compare` at 22 SemiBold,
  the tagline (Muted), the banner in Consolas, the copyright (Faint);
- card `LICENCE`: the warranty paragraph (Muted), then the link `The full text of the GNU General
  Public License v3` to `https://www.gnu.org/licenses/gpl-3.0.html`;
- card `THIRD-PARTY COMPONENTS`: the lead (Muted), an `ItemsControl` of `ComponentRowTemplate`
  rows over `ThirdParty.Components`: name SemiBold + " - " + role, licence, copyright, link;
- `Close` (`DialogButton`, `IsDefault` and `IsCancel`, so Enter and Esc close), right-aligned.

The constructor: `InitializeComponent`, `DataContext = new AboutViewModel()`, the logo, and
`DarkTitleBar.Apply(this)` (the rule "No MessageBox"). `OpenLink` opens the address with
`Process.Start(... UseShellExecute = true)`; a `Win32Exception` or `InvalidOperationException`
writes a Debug line (`the link <url> could not be opened`) through a `Loggers.Window` logger,
as FlatCheck's `ExternalLink.Open` does. `MainWindow`'s About button keeps its `About opened`
line and calls `new AboutWindow { Owner = this }.ShowDialog()`.

`AboutViewModel.cs` (public, as FlatCheck's, since XAML binds it): `Title`, `Name`, `Tagline`,
`Banner`, `Copyright`, `LicenceHeading`, `Warranty`, `LicenceUrl`, `LicenceLinkLabel`,
`ThirdPartyHeading`, `ThirdPartyLead`, `Close`, `Components` (public `ComponentRow`s copied from
`ThirdParty.Components`). It words nothing: every text is `UiText.Current`'s, read when the window
is built; the name and the banner are `VersionInfo`'s (a new `VersionInfo.Name`,
`Image Visual Compare`, which `Banner` then uses). The logo: the .ico becomes a WPF `Resource` of
the csproj too (beside `ApplicationIcon`), decoded from its pack URI, the widest frame frozen, as
FlatCheck's `AppIcon.Largest()`.

## Colours (the owner's choice: this tool's palette)

`Theme.xaml` gains FlatCheck's keyed styles under FlatCheck's names, in this palette: `Card`
(`PanelBrush`, new, `#23272E`, `ChromeBrush` + 8 per channel as FlatCheck's panel is over its
window; border `ChromeBorderBrush`; radius 6; padding 14), `Heading` (12 SemiBold, opacity 0.75),
`Muted` (`MutedTextBrush`, 12, wrapping), `Faint` (`FaintTextBrush`, new, `#636B78`, the muted
grey at FlatCheck's faint-to-muted ratio, 11), an implicit `Hyperlink` style (`AccentBrush`,
underlined on hover; no other hyperlink exists), and `ComponentRowTemplate`.

## Texts (`UiText`, both languages; proposed wording)

`AboutNotice` goes and `AboutTitle(banner)` becomes a property; new members:

| Member | English | Romanian |
| --- | --- | --- |
| `AboutTitle` | About Image Visual Compare | Despre Image Visual Compare |
| `AboutTagline` | Before/after comparison of two versions of the same astrophotography image. | Comparație înainte/după a două versiuni ale aceleiași imagini de astrofotografie. |
| `AboutLicenceHeading` | LICENCE | LICENȚĂ |
| `AboutWarranty` | the three GPL sentences of today + "The full licence text is in the LICENSE file next to this program." | the Romanian of the same |
| `AboutLicenceLink` | The full text of the GNU General Public License v3 | Textul complet al GNU General Public License v3 |
| `AboutThirdPartyHeading` | THIRD-PARTY COMPONENTS | COMPONENTE TERȚE |
| `AboutThirdParty` | The program links components written by others, each compatible with GPLv3; the FITS reader, the display modes and every computation are written by hand. Each licence's full text, and why it is compatible, are in THIRD-PARTY-NOTICES.md, beside the source code. | the Romanian of the same |
| `DiagnosticLogRole` | writing the diagnostic log beside the program (the log folder) | scrierea jurnalului de diagnostic lângă program (folderul log) |
| `Close` | Close | Închide |

`VersionInfo.About` and `ThirdParty.About` (the text forms) go; nothing else reads them.

## Steps

1. **Requirements** (`docs/requirements.md`): R-072 becomes the window (its header, the licence
   card with the paragraph and the link, the components card, `Close`, Enter and Esc, the title,
   a link opening in the browser, FlatCheck's layout in this tool's colours); R-075's first half
   becomes the card's rows (the rest unchanged); R-063 says `About` opens that window; R-101
   names the About window.
2. **Decision**: `docs/decisions/0045-the-about-window-is-flatchecks-in-this-tools-colours.md`
   (Supersedes: 0024 in part, About as text in `MessageDialog`): the owner's question and choice,
   the window, the palette; rejected: FlatCheck's colours, a longer `MessageDialog` text, a
   shared About library. ADR 0043's "notice" bullet points at the card (ADR 0043 is unmerged).
   Check `main`'s last ADR number before the merge.
3. **Tests first** (red): new `AboutWindowTests.cs`: the view model's header is `VersionInfo`'s,
   its warranty, link and URL; its rows equal `ThirdParty.Components` in order and fields; and,
   on `WpfThread`, the window builds (never shown) with a 256-pixel logo and one row per component
   (a missing resource or style key fails there, ADR 0032). `UiTextTests`: the Romanian About test
   reads the view model's texts in Romanian; `AboutTitle` in the dialogs test. Gone: the two
   `R072_` text tests of `VersionTests` and `R075_AboutEndsWithTheComponents`.
4. **Code**: `UiText`, `VersionInfo`, `ThirdParty`, `AboutViewModel`, `AboutWindow`,
   `Theme.xaml`, the csproj's `Resource`, `MainWindow`'s handler.
5. **Documents**: `shell.md` (an "About window" section; Covers the three new files),
   `versioning-and-licence.md` (the About bullet and the third-party paragraph), `language.md`,
   `code-map.md`, `testing.md` (`AboutWindowTests`), `logging.md` (the link's Debug line),
   `CLAUDE.md` (Layout), `README.md` (About in the toolbar section, the status-bar paragraph and
   the licence section), `CHANGELOG.md` (Changed: About is a window like the suite's).
6. **Gate**: `dotnet build ImageVisualCompare.sln -c Release --no-incremental -warnaserror
   --no-restore`, `dotnet format --verify-no-changes`, `scripts/check-design.ps1 -Since main`,
   `scripts/verify.ps1`; commit on the branch.

## Verification

- The gate green; the new tests fail before the code and pass after.
- Off screen, no window shown: the scratch renderer draws the About window in English and in
  Romanian, and FlatCheck's About from AstroDarkroom's bundle (`artifacts/1.0.0/FlatCheck.Wpf.dll`)
  the same way, side by side for the owner.
- The rule "Run the application": launch the Release build and open About (Enter closes it, a
  link opens the browser), only when the owner is not at the PC or at their word; screenshot
  through `PrintWindow`.
- At wrap-up: the golden-like Romanian About test's changed lines, each tied to this change; the
  merge proposed with ADR numbers checked.

## Outcome (2026-10-30)

Implemented on the same branch as planned, the gate green: the plan (d5020e0), then the window, its
tests and documents in one commit (e3836cc9). The branch's commits were squashed into one before
the merge, at the owner's word (2026-10-30), so these hashes are the branch's, not `main`'s. What
differs from the plan, and why:

- **`FontSize="13"` on the window.** FlatCheck's theme gives every `TextBlock` without a style 13
  through an implicit style; this theme has none, and a global one would change the main window,
  so the About window sets it. Seen on the first render beside FlatCheck's: the link, the
  component's name and the banner were a size smaller.
- **The window test reads the bindings from later bodies.** On the WPF test thread a binding takes
  its value in the dispatcher's data-binding pass, after the body that built the window returns;
  the first version of the test read an empty title. It now builds the window in one body and
  reads it from the next ones, as `MainWindowTests` waits for a pair.
- **`VersionInfo.LicenceUrl`** holds the GNU page, which `UiText.AboutNotice` held as a literal;
  a `UiText` property worded the same in both languages fails `R101_EveryText_IsWordedInBothLanguages`.
- **The gate asked for ADR 0024's status** to name the part ADR 0045 supersedes.
- **Checked off screen, no window shown:** a scratch program builds each About window, moves its
  content into a frame of the client width (584 DIPs) and draws it; this tool's, in English and
  Romanian, 584 × 539 DIPs, and FlatCheck's from AstroDarkroom's bundle, 584 × 865 DIPs, its five
  components making it taller.
- **Launched at 18:18, the owner away for 80 minutes** (the rule "Run the application"): the
  Release build of e3836cc9 on the PNG and TIFF pair from the command line, About opened through UI
  Automation, the window 600 × 578 pixels with its dark caption, as the render; Enter closed it,
  and Esc after a second opening, each key sent only while the window owned the foreground; the
  log held three `About opened` lines and `exit 0 after 123.3 s`. The links were not clicked,
  not to leave a browser open on the owner's desktop. A modal window sits under its owner in the
  automation tree, not under the desktop, which the first search missed.
- **The review of c494e48..aa5e037** (a subagent, 2026-10-30, at the owner's word): eleven findings,
  all verified and all taken at the owner's word. The window test checked the bound source, not
  what the window draws: it now lays the content out, which builds the card's row from its
  template, and reads the row's text (the two spaces either side of the dash are FlatCheck's), Close
  as default and cancel, and both links; a key misspelled in the row template on purpose made both
  window tests fail with `Cannot find resource named`, where the old test passed. `ThirdPartyTests`
  claims every package of the lock file, direct and transitive, checks each component's section of
  the notices file, NLog's three conditions and its disclaimer, each link absolute https, and
  looks for the notices file in any item of the csproj and of the `Directory.Build` files, in any
  case. ADR 0045 supersedes ADR 0023 in part too (the banner heading About). The link handler logs
  the address as written and disposes the process. `shell.md` covers `AboutViewModel.cs` and the
  csproj and says why one button is both Enter's and Esc's; the index and `CLAUDE.md` point to the
  About window; seven stale mentions of the About text or dialog are reworded; R-075 describes the
  row instead of quoting its spacing; R-101 and `language.md` name the GPL's English name in the
  link among what stays English (4c6b3e4). Launched again at 19:24, the owner idle for six
  minutes: About opened, Enter and Esc closed it, `exit 0 after 13.2 s`; the links were not
  clicked.
