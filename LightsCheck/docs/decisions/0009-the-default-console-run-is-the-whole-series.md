# 0009. The default console run is the whole series; the inventory is behind `--inventory`

Date: 2026-10-09
Status: accepted 2026-10-09

## Context

Step 1 of the build (`docs/plans/2026-10-09-build-lightscheck.md`) shipped the inventory as
the only run, so `lightscheck <folder>` printed the metadata table. Step 3 added the series:
the measured frames, the registration, the dithering, the drift and the list of frames to
drop, which is what the owner opens the tool for. Two runs now answered the bare command, and
one of them had to be the default. The inventory takes a second (no pixel is decoded); the
series takes minutes on a night of raws and needs the thread and pixel options.

## Decision

`lightscheck <folder>` runs the whole series and prints the report. The inventory, the
metadata-only run, is `--inventory`; one frame is `--frame N`. The window opens the series
run alone.

## Alternatives rejected

- **Keep the inventory as the default and put the series behind `--series`.** The report is
  the tool's purpose; the user would type the option every time, and the window would differ
  from the console's bare command.
- **Print the inventory first and ask whether to continue.** The console is also run from
  scripts and the tests compare its text; a prompt breaks both.

## Consequences

The bare command takes minutes; the progress line on stderr says where it is. The inventory's
golden stays (`inventory.txt`, run with `--inventory`) beside the series' (`series.txt`).
`README.md` documents the bare command as the series. `Options` refuses `--inventory` with
`--frame` (R-004).
