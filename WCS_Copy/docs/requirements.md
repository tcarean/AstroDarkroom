# Requirements

The source of truth for behaviour. Code follows this file, not the reverse. A behaviour change is
first an edit here (status `draft`), then a test, then code (status `implemented`).

Rules for entries:

- IDs are `R-NNN`, assigned once, never reused. A dropped requirement keeps its ID with status
  `dropped`. Gaps in the numbering are deliberate: each block of ten is one area; a full block
  continues in the next free block of ten, which the area's heading then names.
- Status is one of `draft`, `accepted`, `implemented`, `dropped`. `implemented` means a test named
  `R###_...` exists in a test project under `tests/` (`RepoInvariantTests` checks it both ways).
- Describe observable behaviour, with one concrete example that includes numbers. No adjectives.
- Error behaviour and the exit code are requirements too.

The explanations behind each row (why this way, what was measured) are in `docs/design/` and
`docs/decisions/`; the user documentation of the same behaviour is `README.md`. The rows of
R-001 to R-015 describe the tool as it was built in August 2026 and were written on 2026-10-26
from its tests (ADR 0001); the window rows (R-030 on) are the second task of
`docs/plans/2026-10-26-adopt-the-kit-and-build-the-window.md`, R-038 the plan
`docs/plans/2026-10-28-add-the-top-band-to-the-window.md`.

## The console contract (R-001 to R-009)

| ID | Status | Requirement |
| --- | --- | --- |
| R-001 | implemented | Line 1 of every run is `copywcs <version>` and line 2 the copyright line, in every mode, at `--help` and on a failed run; each exactly once. Example: `copywcs 1.0.0` then `Copyright (C) 2026 Tudor Carean`. There is no `--version` flag; it is an unknown option. |
| R-002 | implemented | A usage error (not exactly two paths, an unknown option, `-o` without a value) prints one line on stderr saying what is wrong, then the usage on stdout, and exits 2. Example: one path alone gives `Exactly two files are needed: the source and the destination.` |
| R-003 | implemented | `-h` or `--help`, once the parser reaches it (an unknown option before it is still a usage error), prints the usage and the warranty paragraph of GPLv3 section 5(d), which names the LICENSE file and the licence's URL, and exits 0 with nothing on stderr. |
| R-004 | implemented | A source or a destination that cannot be read (missing, unreadable, not FITS) gives one line on stderr naming which file and why, exit 1, and nothing is written. Example: `Cannot read the source 'x.fit': Could not find file ...`. |
| R-005 | implemented | When `NAXIS1` or `NAXIS2` differ between the two files, the copy is refused with exit 1 and both sizes named, unless `--force`, which copies and prints a warning line with both sizes first. Example: `Different dimensions: source (100, 80) vs destination (200, 80).` |
| R-006 | implemented | A source without `CTYPE1` gets a warning line on stdout (`WARNING: the source does not look plate-solved (CTYPE1 is missing).`) and the run continues. |
| R-007 | implemented | The keys copied are, in this order and each once: the 30 WCS keys of `WcsMetadataKeys.WcsKeys` (`WCSAXES`, `CTYPE1`, ... `PLTSOLVD`), every `A_i_j`, `B_i_j`, `AP_i_j`, `BP_i_j` card the source has, and, unless `--no-optical`, the 17 optical keys (`FOCALLEN`, `XPIXSZ`, `OBJECT`, `DATE-OBS`, ...); only the keys present in the source. `ABC_1_2` is not a SIP key. |
| R-008 | implemented | When no key of R-007 is present in the source, one line on stderr (`No relevant key found in the source. Nothing to copy.`), exit 1, nothing written. |
| R-009 | implemented | `--dry-run` prints `Keys that would be copied (N):` and then each card, trimmed and indented by two spaces, one per line, exits 0 and leaves the destination byte for byte as it was. |

## The copy (R-010 to R-019)

| ID | Status | Requirement |
| --- | --- | --- |
| R-010 | implemented | A copied card is the source's 80 bytes, verbatim, formatting and inline comment included; never a reparsed or reformatted value. |
| R-011 | implemented | One `HISTORY WCS copied from <source file name>` card is appended, its text cut at 72 characters; the card is English in both interface languages (ADR 0004). |
| R-012 | implemented | Without `-o` the destination is rewritten in place through a temporary file beside it, which is gone at the end; with `-o <file>` the new file is written and the destination is left byte for byte as it was. On success the last two lines are `Copied N keys into <target>.` and the reminder to check the annotation by eye, and the exit code is 0. |
| R-013 | implemented | The written file holds the new header, `END`, spaces up to the next 2880-byte boundary, then the destination's data area unchanged; a header that grows past its original number of blocks (for example 41 cards, two blocks) is padded the same way and the data still follows it. |
| R-014 | implemented | A write that fails (for example a target in a folder that does not exist) gives one line on stderr starting with `Write error:`, exit 1, and no target file. |
| R-015 | implemented | A file whose first card is not `SIMPLE`, or that ends before an `END` card (a short file, or exactly 36 cards with no `END`), is refused by the reader with one of its two messages, which R-004 then reports. |

## The interface language (R-020 to R-029)

| ID | Status | Requirement |
| --- | --- | --- |
| R-020 | implemented | `--lang <code>` and `--lang=<code>`, anywhere on the command line, in any letter case, name the language of every text after the banner; `en` is English, `ro` Romanian; the last `--lang` counts; the spaced form's value is the next argument whatever it is. |
| R-021 | implemented | Without `--lang` the texts are English; `--lang ro` gives the Romanian texts, the same wordings the tool had before 2026-10-26, with the `--lang` line added to the usage. |
| R-022 | implemented | A `--lang` value that is not `en` or `ro`, or a `--lang` with nothing after it, prints the banner and the copyright, then `ERROR: --lang expects en or ro, got 'xx'` (or `... and got nothing`) on stderr, in English, and exits 1. |
| R-023 | implemented | Every scenario of R-002 to R-015 run in English has the exit code and the figures of the same run in Romanian and contains no Romanian word. |
| R-024 | implemented | On the header of the real Siril plate solve kept as a fixture, the keys selected are exactly the lists `docs/validation.json` records for V-001, with and without the optical keys, in that order. |

## The window (R-030 to R-039)

| ID | Status | Requirement |
| --- | --- | --- |
| R-030 | implemented | The window `CopyWcs.Wpf.exe` takes a plate-solved source, a destination, the choice `Write into the destination` (the default) or `Write a new file` with that file's path, the two options `Copy the optical metadata too` (on) and `Continue even when the dimensions differ` (off), and offers `Preview` and `Copy` under the options (`About` is in the band at the top, R-038). A path is taken without the whitespace around it. `Preview` and `Copy` are disabled until both paths name existing files, and while a run is in flight (the run goes off the window's thread, so the window stays live during a copy); `Copy` also until the new file's path is not blank when `Write a new file` is chosen. Under each file box the window shows the two facts it reads from the header: `Source: 100 x 80, plate-solved` (or `not plate-solved`), `Destination: 100 x 80` (or `, already plate-solved` when the destination has `CTYPE1`), and nothing when the file is missing, the reader refuses it, or the header has no `NAXIS1` or `NAXIS2` (`NAXIS = 0`). |
| R-031 | implemented | `Preview` runs the core's dry run and shows, under the core's heading, the cards it selected: trimmed, one per line, in the source's order, exactly the lines `copywcs --dry-run` prints for the same files without their two-space indent, and writes nothing. Example: the test pair of `FitsTestData` gives `Keys that would be copied (15):` and 15 cards, 11 with `Copy the optical metadata too` off. |
| R-032 | implemented | `Copy` runs the same request without the dry run, the three paths without their surrounding whitespace (` out.fit ` writes `out.fit`), and the file the core writes, in place or the new file, is byte for byte the one `copywcs <source> <dest>` or `copywcs ... -o <file>` writes for the same request (the destination untouched in the second case); the core's two closing lines (`Copied 15 keys into <target>.` and the reminder) are shown under the cards, and the destination's facts are read again, so an in-place copy now reads `Destination: 100 x 80, already plate-solved`. |
| R-033 | implemented | A refusal of the core (an unreadable file, different dimensions without force, no relevant key, a write error) is shown under the heading `Refused` with the core's one line, the same line the console writes on stderr, the warnings raised before it above it, no cards and no closing lines, and nothing is written: the destination is byte for byte as it was. A warning of the core (the forced mismatch, the missing `CTYPE1`) is shown with the core's line, in the warning colour, and the run goes on. Example: a 100 x 80 source and a 200 x 80 destination without force show `Different dimensions: source (100, 80) vs destination (200, 80). The WCS would be wrong. Use --force if you know what you are doing.` and write nothing; with the option on, the copy is done and `WARNING: Different dimensions: ... Continuing anyway (--force).` is shown. |
| R-034 | implemented | `--lang` is read as the console reads it (R-020), in the application's constructor, before the first view loads; a value not understood opens the window in English and says `--lang expects en or ro, got 'xx'` in a message box once the window is up. The first two arguments that name existing files, in their order, fill the source and the destination, a `--lang` and its value taken out (so `--lang solved.fit` takes the file as the value); a blank, a folder, a missing file and a third file are ignored. Example: `CopyWcs.Wpf.exe --lang ro solved.fit stack.fit` opens in Romanian with both boxes filled; `CopyWcs.Wpf.exe stack.fit` fills the source only. |
| R-035 | implemented | `About` (the band's button, R-038) or F1 opens a modal window with the icon's 256 px frame, `CopyWcs`, a tagline, `VersionInfo.Banner` (`copywcs 1.0.0`), `LicenseInfo.Copyright`, a licence card with the warranty paragraph of GPLv3 section 5(d), the console's five `--help` lines joined by one space (`LicenseInfo.Warranty`, in the run's language), the link `https://www.gnu.org/licenses/gpl-3.0.html`, and `Close`; `Close`, Enter and Esc close it. |
| R-036 | implemented | Every label, heading, dialog title and fact line is a `T("english", "romana")` pair (`{l:T En='...', Ro='...'}` in XAML), the Romanian without diacritics; the window and the About window render in both languages, and the English pass, with a refusal, a warning, a preview heading and the cards on screen, holds none of the Romanian words `LanguageTests` lists nor of the window's own (`Alege`, `Scrie`, `Copiaza`, `Despre`, `Refuzat`, `Inchide`, `Sursa`, `Destinatia`, `Previzualizare`, `Versiune`, `rezolvat`, `aceeasi`, `pixelii`, ...). Example: with `--lang ro` the buttons read `Previzualizare`, `Copiaza`, `Despre`; with `--lang en`, `Preview`, `Copy`, `About`. |
| R-037 | implemented | The window and the About window load with no binding error in WPF's data-binding trace, the dark palette (`#1B1B1D`) as background and a dark title bar (DWM attribute 20, or 19 before Windows 10 20H1), and carry the icon `Icon/copywcs.ico`, drawn by `Icon/make_icon.py` in the 8 sizes 16, 20, 24, 32, 40, 48, 64 and 256, which the script's `SIZES` line and the file agree on; both executables take the same file through `ApplicationIcon`. |
| R-038 | implemented | At the top of the window, across its whole width, a band on `PanelBg` (`#232326`) with a 1 px `BorderBr` line (`#3A3A40`) under it, as FlatCheck's (ADR 0008): on the left the console's lines 1 and 2 (R-001), `copywcs 1.0.0` in semi-bold over `Copyright (C) 2026 Tudor Carean`; in the middle, wrapped, the explanation `Copies the plate solve (WCS) from a FITS solved in Siril into the same image processed elsewhere, card by card, without touching its pixels.` (in Romanian `Copiaza plate solve-ul (WCS) dintr-un FITS rezolvat in Siril in aceeasi imagine procesata in alta parte, card cu card, fara sa-i atinga pixelii.`); on the right `About` (`Despre`), the window's only About button, the command F1 also runs, with the tooltip `Version and licence (F1)` (`Versiune si licenta (F1)`). |

## Dropped

None yet. Move rows here with status `dropped` and one line saying why.
