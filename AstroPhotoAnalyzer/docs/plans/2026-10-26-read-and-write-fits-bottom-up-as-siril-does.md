# Read and write every FITS bottom-up, whatever `ROWORDER` says, as Siril does

Branch `claude/Show-every-FITS-bottom-up-as-Siril-does`, shared with ImageVisualCompare's fix of
the same day (its ADR 0039); the worktree is ImageVisualCompare's
`.claude/worktrees/Show-every-FITS-bottom-up-as-Siril-does`, which checks out the whole
monorepo, and this project's commit is its own, made from this folder with this project's gate.

## Context

Siril 1.4.4 shows every FITS with the file's first row at the bottom and uses `ROWORDER` only
for the Bayer pattern (measured 2026-10-26, ImageVisualCompare ADR 0039). This tool flipped
only on `BOTTOM-UP`, wrote a fresh FITS `TOP-DOWN`, and took the lent WCS's flip from the
keyword: three places where a file without the keyword, or a lying `TOP-DOWN` one, was the
wrong way up against Siril. The owner asked for the fix here, in the same branch as
ImageVisualCompare's.

## Steps

1. R-011 rewritten (and made `implemented`), R-014, R-018 and R-145 reworded; the failing
   tests: `R011_*` in `FitsReaderTests`, the fresh-FITS row order and the `ROWORDER` card in
   `CurveTests`, the lent WCS without the keyword in `WcsBorrowTests`.
2. `FitsReader` always flips and notes an ignored `TOP-DOWN`; `ReadImageHeader` drops the flip;
   `Wcs.TryFrom` is given `true` by the reader and by `BorrowWcs`; `FitsWriter` always writes
   the raster from its last row, the fresh file under `ROWORDER = 'BOTTOM-UP'`.
3. ADR 0049, the CHANGELOG's Fixed line, the README's save bullet, `docs/design/input.md`.
4. The gate; `/validate` on the reference images (the rule "Formula changes re-validate": the
   reader is in `Input/`); a FITS saved from a PNG opened in `siril-cli` and exported, its
   first PNG row compared with the PNG's.
