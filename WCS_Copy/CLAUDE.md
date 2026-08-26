# CopyWcs

Copies FITS plate-solving (WCS) and optical metadata from a Siril-solved
source file into a destination FITS file, by hand-parsing the FITS card/block
format directly (no FITS library, no external dependencies).

## Structure

- `CopyWcs/` - the app: `Program.cs` (entry point) calls `CopyWcsCli.Run(args, stdout, stderr)`
  (testable orchestration), which uses `Options` (CLI parsing), `FitsHeader`
  (FITS read/write engine), and `WcsMetadataKeys` (which keys to copy).
- `CopyWcs.Tests/` - xUnit tests, referencing `CopyWcs` via `InternalsVisibleTo`.
- `copy_wcs.py` - the original Python/astropy prototype the C# tool was ported
  from. Prior art only; not maintained in parallel - don't feel obliged to
  mirror C#-side changes into it.

## Conventions

- User-facing text (CLI messages, `--help`, code comments) is in Romanian -
  keep new text consistent with this.
- `FitsHeader` copies the raw 80-byte card verbatim
  (`dest.SetCard(key, source.GetCard(key)!)`), never a reparsed/reformatted
  value - this preserves the source's exact formatting and inline comment.
  Don't "clean up" this into a reparse.
- `Main` stays a one-line shim to `CopyWcsCli.Run`; keep new CLI behavior in
  `CopyWcsCli`/`Options` so it stays testable in-process, without spawning a
  subprocess.

## Build & test

    dotnet build CopyWcs.sln
    dotnet test CopyWcs.sln
