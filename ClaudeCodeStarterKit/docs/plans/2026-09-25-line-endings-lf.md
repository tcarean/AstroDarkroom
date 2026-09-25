# 2026-09-25 LF line endings everywhere, fixtures byte-exact

## Context

The kit's gate failed at step 1 (test data manifest) in every new worktree: `core.autocrlf=true`
plus `* text=auto` checked the fixtures out with CRLF, and the manifest holds the hashes of the
committed LF bytes. It blocked a commit of an unrelated change (branch names, ADR 0013). The
owner asked for the recommended practice for line endings in a .NET repository on Windows, then
asked for it to be applied: "porneste taskul de line endings cu recomandarea ta". That answer,
given in chat and accepted, is the approved plan below; ADR 0014 records the decision.

## Measured before changing anything

- Index: 798 text files, all LF; 3 binary (`.ico`); no file stored with CRLF.
- A new worktree of the kit: 75 files CRLF on disk, 2 LF. The shared checkout: 19 CRLF, 58 LF.
- `.editorconfig`: the kit says `crlf`, FlatCheck says `lf`; no other project has one.
- The root of `photo` has no `.gitattributes`; FlatCheck has `* text=auto eol=lf`.
- One batch file in the repository: `SirilCleanup/SirilCleanup.bat`.

## Steps

1. Kit `.gitattributes`: `* text=auto eol=lf`; `.cmd`/`.bat` CRLF; `tests/testdata/fixtures/**`
   `-text`; `.ico` binary beside the existing binaries. Kit `.editorconfig`: `end_of_line = lf`,
   CRLF for `.cmd`/`.bat` (the `.sh` section became redundant).
2. Root `.gitattributes` for `photo`: the same baseline plus the image and FITS/NEF binaries.
3. Stage the rules, check the kit's files out again, `git add --renormalize .`: no content change.
4. Kit verify green in the new worktree.
5. ADR 0014; `GHID.md` (why, and the order of a change); `monorepo/README.md` (the root file);
   `tests/testdata/README.md` (fixtures are `-text`); the regeneration prompt; CHANGELOG.
6. After the merge, the owner's working copies that run the kit's gate are checked out again.

## Deviations

- Step 3 first ran `git add --renormalize .` before the re-checkout; with `-text` it staged the
  two fixtures with the CRLF bytes on disk. Caught in `git status`, unstaged (`git restore
  --staged`; the index blobs matched `HEAD` again), then done in the right order. The order is now
  part of ADR 0014 and the guide.
- Measured in addition: `dotnet format --verify-no-changes` on CRLF files under `end_of_line = lf`
  fails with `ENDOFLINE` (exit 2). That makes step 6 required, not optional.
