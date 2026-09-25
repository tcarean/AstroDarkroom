# 0014. LF in the repository and on disk, on every platform; fixtures are never converted

Date: 2026-09-25
Status: accepted 2026-09-25

## Context

The kit normalised line endings in the repository (`* text=auto`) and checked them out with the
platform's ending, CRLF on Windows, with `.editorconfig` asking for CRLF. On 2026-09-25 its gate
failed at step 1 in every new worktree on the owner's machine: `core.autocrlf=true` plus
`text=auto` wrote the two fixtures with CRLF, while `manifest.json` holds the SHA-256 of the
committed LF bytes. The shared checkout passed only because its fixtures were still the LF files
written there, which git had never checked out again. Measured the same day: all 798 text files
in `photo` are stored LF; a new checkout of the kit gave 75 CRLF files and 2 LF; the shared
checkout of the kit was mixed, 19 CRLF and 58 LF, because tools that write LF (the assistant's
edits among them) leave LF behind; `dotnet format --verify-no-changes` reports `ENDOFLINE` on
every line whose ending differs from `end_of_line` (measured with CRLF files under `lf`), so a
Linux CI checkout, which is LF, would fail the old `end_of_line = crlf` by the same rule.
FlatCheck, in the same repository, already used `* text=auto eol=lf` with `end_of_line = lf`, and
its gate passed in every worktree. The repository root had no `.gitattributes`, so a project
without its own depended on each machine's `core.autocrlf`. The owner asked for the recommended
practice and accepted it.

## Decision

- LF in the repository and on disk, on every platform: `* text=auto eol=lf` in `.gitattributes`
  and `end_of_line = lf` in `.editorconfig`; the two always agree.
- `.cmd` and `.bat` are checked out CRLF: `cmd.exe` misreads labels in batch files with LF.
- `tests/testdata/fixtures/**` is `-text`: git stores and checks out the exact bytes, so the hash
  in the manifest holds in every clone, and a fixture that needs CRLF keeps it.
- Binary files are `binary`.
- In a monorepo, a root `.gitattributes` carries the same baseline for every project, and a
  project's own file refines it. `photo` has one since this change.
- A change of rules is applied in this order: stage the new `.gitattributes`, check the affected
  files out again (delete and restore: git does not rewrite a file it considers unchanged), then
  `git add --renormalize .`. Renormalising first re-adds a `-text` file with whatever bytes are on
  disk; it happened once while making this change and was undone before the commit.

## Alternatives rejected

- **CRLF on disk on Windows (`core.autocrlf=true`, `end_of_line = crlf`).** The classic Windows
  setup. It holds only while everything runs on Windows and nothing compares bytes; here the
  hashed fixtures and a Linux CI format check both break it.
- **Leave line endings to each machine's `core.autocrlf`.** Each clone gets different bytes, which
  is the failure itself.
- **Normalise line endings before hashing in `check-testdata.ps1`.** Hides a real difference in
  bytes, and misses a fixture whose CRLF is the point of its test.
- **`-text` on all of `tests/testdata/`.** `manifest.json` and the README there are ordinary text,
  normalised like everything else; only the fixtures are compared byte for byte.

## Consequences

A new clone or worktree passes the kit's gate unchanged (measured: verify green in a new
worktree, 20 s). An existing working copy keeps its CRLF files until they are checked out again,
and until then `dotnet format` fails on them with `ENDOFLINE`; so the change is followed by one
re-checkout in each working copy that runs the gate. A file that a tool saves with CRLF is
normalised on commit and shows no diff. `git ls-files --eol` shows, per file, the ending in the
index (`i/`), on disk (`w/`) and the attributes that decided it.
