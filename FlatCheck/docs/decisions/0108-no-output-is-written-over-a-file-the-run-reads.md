# 0108. No output is written over a file the run reads

Date: 2026-11-13
Status: accepted 2026-11-13

## Context

The console writes two kinds of file: the picture at `-o` (default `flatcheck.png`) and the
synthetic flat at `--synthetic`. The only guard was `--synthetic` against the input file. The
review of 2026-11-13 (CR-011) followed the rest: `flatcheck process/pp_flat_stacked.fit -o
process/pp_flat_stacked.fit` read the master, printed the report, wrote the PNG's bytes into
the master and printed "map saved"; the same for the second flat of `--against`, for the
`--raw` file, and for `--synthetic` naming the `--raw` file. A frame of a `--light` folder
named at `-o` was lost the same way. The master of a session is made by a Siril run of
minutes over raw frames that may be gone; a raw flat cannot be made again at all.

## Decision

The owner's choice of 2026-11-13, between protecting the files the run reads and also
refusing any `-o` that ends in a FITS or raw extension:

1. An output that names a file the run reads is refused before anything runs, exit 1: `-o`
   against the input file, the `--against` file and the `--raw` file; `--synthetic` against the
   input file and the `--raw` file. The check does not look at the mode, so `flat.NEF --expo -o
   flat.NEF` is refused though `--expo` writes no picture: such a line is a slip either way.
2. With `--light` on a folder, `-o` naming a file the pattern matches there is refused by the
   run, before a frame is read: those inputs are known only from the disk, so the parse cannot
   see them.
3. Two paths are the same file when their full paths are equal, without case on Windows (the
   existing `SamePath`). The message names the output as typed.
4. Any other name is an output like before, whatever its extension and whether the file exists.

## Alternatives rejected

- **Also refuse an `-o` ending in a FITS or raw extension.** It would catch one more slip (an
  `-o` naming another session's master), but it is a rule about names, not about what is read:
  it refuses a harmless line and still lets `-o notes.txt` through. The rule chosen has no
  false refusal.
- **Ask before overwriting an existing file**, as the window does for the synthetic flat. The
  console runs in scripts, where nobody answers, and overwriting `flatcheck.png` at every run
  is what it is for.
- **Compare the files, not the paths** (a hard link, a second name of the same file through a
  junction or an 8.3 name). No portable call says two paths are one file, and the slip this
  guards is typing the same name twice.

## Consequences

- A line that names its input as its output gets an error where it lost the file.
- A file reached under two different full paths is not recognised; the limit is in point 3.
- The window writes only names it builds itself beside the session's files, and asks before it
  replaces a synthetic flat; it is not changed.
- R-137 holds the behaviour; `R137_...` in `OutputOverInputTests` pins every pair, the series
  and the bytes left intact.
