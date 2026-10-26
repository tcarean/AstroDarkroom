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
`docs/plans/2026-10-26-adopt-the-kit-and-build-the-window.md`.

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
| R-030 | accepted | The window takes a plate-solved source, a destination, an output file or the choice to write in place, the two options (optical keys, force), and offers `Preview` and `Copy`; both are disabled until both paths name existing files. |
| R-031 | accepted | `Preview` shows exactly the cards the console's `--dry-run` lists for the same files, in the same order. |
| R-032 | accepted | `Copy` writes bytes identical to the console's for the same request. |
| R-033 | accepted | A refusal or a warning of the core is shown with the core's words; nothing is written on a refusal. |
| R-034 | accepted | `--lang` is read before the first view loads; a value not understood is said in an English message once the window is up and the window goes on in English; the first two existing files among the other arguments fill the source and the destination. |
| R-035 | accepted | The About window shows `VersionInfo.Banner`, `LicenseInfo.Copyright`, the console's warranty words and the link to the licence text; Close, Enter and Esc close it. |
| R-036 | accepted | Every label and heading is a `T("english", "romana")` pair; the window renders in both languages, with no Romanian word in the English pass. |
| R-037 | accepted | The window and the About window load with no binding error, the dark palette as background, a dark title bar, and the icon of `Icon/copywcs.ico`. |

## Dropped

None yet. Move rows here with status `dropped` and one line saying why.
