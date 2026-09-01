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
  mirror C#-side changes into it. It does, however, carry the GPL notice -
  see "Licensing" below.
- `VersionInfo.cs` - the version and the banner, read out of the assembly. See
  "Versioning" below.
- `LicenseInfo.cs` - the copyright string, read out of the assembly. See
  "Licensing" below.
- `LICENSE` - the verbatim GNU GPL v3 text. See "Licensing" below.

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
- **Never bump the version** - that is the user's call. See "Versioning" below.
- **Every source file starts with the GPL notice.** New files included. See
  "Licensing" below.

## Build & test

    dotnet build CopyWcs.sln
    dotnet test CopyWcs.sln

## Versioning

Every run prints `copywcs <version>` as its **first line** - every mode, `--help`, and
failed runs too, so a pasted output always says which version produced it. The copyright
line sits right under it (see "Licensing"). There is no `--version` flag; it would be
redundant, and `VersionTests` pins that it stays gone.

`<Version>` in `CopyWcs/CopyWcs.csproj` is the **single source of truth**.
`CopyWcs/VersionInfo.cs` does *not* hardcode it: `VersionInfo.Value` reads the assembly's
`AssemblyInformationalVersion`, which MSBuild derives from `<Version>`, and trims any
`+sourcerevision` suffix. Don't reintroduce a literal there.

**The user bumps the version manually**, per feature or fix, choosing the increment
themselves. **Never bump it on your own initiative** - not as part of a change, not
"while you're in there". If a change seems to warrant a bump, say so and let them decide.

Unlike `FlatCheck`, the version lives in exactly one place: `copy_wcs.py` carries no
`__version__`. It is unmaintained prior art, there is no cross-check script pairing the
two, and a second copy would only be a thing to desync. So a bump touches the csproj and
nothing else - no golden files here to scrub the banner out of.

| situation | what fails |
|---|---|
| `<Version>` bumped | nothing |
| `<Version>` removed from csproj | the format and plumbing tests in `VersionTests` |
| the banner stops being printed, or moves off line 1 | `VersionTests`, and the copyright-position tests in `LicenseNoticeTests` |

## Licensing

The tool is **GPLv3-or-later**. The full text is in `LICENSE` at the repo root (the
verbatim FSF `gpl-3.0.txt` - don't reword it, and don't reflow it). This mirrors the
sibling `FlatCheck` project's setup, deliberately.

Four things follow, and all four are pinned by tests:

- **Every source file starts with the notice**, above the file's own header comment:
  the SPDX line, the copyright, and all three paragraphs of the standard GNU notice
  (permission, no-warranty, where-to-get-a-copy). `//` for C#, `#` for Python, below
  the shebang. New files get it too - it is the first thing to write, not an
  afterthought. `LicenseHeaderTests` walks both projects plus `copy_wcs.py` and fails
  on any file missing it, which is the only thing keeping the convention alive as
  files get added. The SPDX line is there so licence scanners can read the file
  without parsing English.
- **Every run prints the copyright on line 2**, right under the version banner, in every
  mode, at `--help`, and on failed runs - so a pasted output says on its own whose tool
  produced it. Same placement as `FlatCheck`.
- **`--help` carries the warranty disclaimer** and points at `LICENSE` - GPLv3 §5(d),
  which wants a program that displays legal notices to display that one too. It lives
  in `Options.PrintUsage` rather than under the banner on purpose: five lines on top of
  every run would drown the actual output.
- **One source of truth for the copyright string**: `<Copyright>` in
  `CopyWcs/CopyWcs.csproj`, read back through `AssemblyCopyrightAttribute` by
  `LicenseInfo.Copyright` (the literal in `LicenseInfo.Read` is only a fallback for an
  assembly built without the attribute). `LicenseNoticeTests` binds the two together.

`copy_wcs.py` gets the notice header - the GPL asks that of every copy of the source -
but *not* the runtime copyright line or the warranty epilog: it is unmaintained prior
art, and nothing tests its output. Same reason it carries no `__version__`; see
"Versioning".
