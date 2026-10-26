# 0007. The launcher tells the suite's prerequisites, hides the unreleased flag and draws a flag on each language button

Date: 2026-10-26
Status: accepted 2026-10-26

## Context

On 2026-10-26 the owner asked for four changes to the launcher: no more "unreleased changes"
(`modificari nelansate`) in the main window's rows or in "Despre"; a mention somewhere that the
tools work on a Siril session folder and need Siril installed, newer than 1.4.4; for FlatCheck,
a recommendation to install Adobe's lens profiles, optional, naming the Adobe programs that
bring them; and a flag on each language button.

The facts behind the texts come from the components' own documentation. DarksCheck and
LightsCheck open the session folder Siril lays out (`darks/`, `lights/`, optional `biases/`);
FlatCheck opens the master `pp_flat_stacked.fit` Siril's script writes into `process/` or
`masters/` and runs the preprocessing script through the user's `siril-cli` (FlatCheck ADRs
0035 and 0055); every Siril figure in the suite was measured against Siril 1.4.4 (FlatCheck ADR
0025). FlatCheck reads Adobe's `.lcp` profiles from `C:\ProgramData\Adobe\CameraRaw\LensProfiles\1.0`
and `%APPDATA%\Adobe\CameraRaw\LensProfiles` when an Adobe program installed them (FlatCheck
R-070, ADR 0028); its README names Lightroom, Camera Raw (Photoshop) and the free Adobe DNG
Converter as the programs that do, and falls back to the lensfun database it ships with.
(Corrected 2026-10-26: Lightroom Classic and Lightroom keep their profiles inside their own
program folders, which FlatCheck does not read; of the three, only the DNG Converter and Camera
Raw install them where FlatCheck reads them. ADR 0008 records the measurement. The third
paragraph of `Prerequisites.Details` was written from FlatCheck's README and names Lightroom.)

Asked in this session, the owner chose: the **United States flag** for English (over the
Union Jack and the cross of St George); the prerequisites **in "Despre" and in `README.md`,
plus one short line under the main window's subtitle**; the version written **"1.4.4 or
newer"**, since 1.4.4 is the version everything was measured on.

## Decision

- **The `unreleased` flag of `release.json` is read and not shown.** `ReleaseManifest` keeps
  the key, so the file stays what `scripts/release.ps1` writes and R-004's tests stand; the
  rows show the version alone and the component list of "Despre" has two columns. The flag
  says whether a component's CHANGELOG had lines under `[Unreleased]` when the suite was
  bundled: a fact about the owner's workflow, which bundles the components from `main`, not
  about what the user holds, and it read as a warning.
- **The prerequisites are texts in the core, `Prerequisites.Summary` and
  `Prerequisites.Details`**, one line and three paragraphs, each a `T(en, ro)` pair (ADR 0006),
  so the test R009 guards both wordings and the facts they must name; the window only shows
  them. The main window shows the summary under its subtitle and points at "Despre", which
  shows the three paragraphs in a card between the licence and the components. `README.md`
  says the same in its "Requirements" section. The launcher checks none of it: it computes nothing
  of its own and never looks outside its folder.
- **The flags are vector drawings in `App.xaml`**, two `DrawingImage` resources at the flags'
  official proportions, shown 13 px high and aliased so the stripes stay crisp; the US flag's
  stars are a grid of dots, which at that size reads as the lighter field they are. Each
  language button's content is its flag beside its name; the names stay the single literals
  the rule "Two interface languages" allows.

## Alternatives rejected

- **Emoji flags in the button text.** Windows has no flag glyphs: a regional-indicator pair
  renders as two letters (`US`, `RO`).
- **A PNG per flag.** Sharp at one DPI only, and a second kind of resource for two images of
  three colours each.
- **The Union Jack or the cross of St George for English.** Put to the owner with a
  recommendation (the Union Jack, the usual convention of language selectors); the owner
  chose the US flag.
- **The prerequisites in the README only, or only in "Despre".** The owner wanted the main
  window to say it too; the summary line keeps the window short and sends the reader to
  "Despre" for the three paragraphs.
- **Dropping `unreleased` from `release.json` and `ComponentInfo`.** The file is written by
  the kit's release script, which the suite shares with every project (the kit's ADR 0036),
  and the key costs nothing unread.
- **A check of Siril's presence or version in the launcher.** It would make the launcher
  look outside its folder, and FlatCheck already says when `siril-cli` is not found.

## Consequences

- `AppRow.UnreleasedHint` and `ComponentLine.Note` are gone; a reader of `release.json` still
  sees `unreleased` there.
- A change of the suite's needs (a new Siril version, another profile source) is an edit of
  the three pairs in `Prerequisites.cs`, of R-009 and of the README's "Requirements".
- Guard: `LanguageTests.R009_ThePrerequisites_NameSirilAndTheAdobeProfilesInBothLanguages`.
  The flags and the card are checked by eye: the window has no test project (ADR 0001).
