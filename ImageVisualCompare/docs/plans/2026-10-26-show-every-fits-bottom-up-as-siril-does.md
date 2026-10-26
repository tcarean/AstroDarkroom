# Show every FITS bottom-up, whatever `ROWORDER` says, as Siril does

Branch `claude/Show-every-FITS-bottom-up-as-Siril-does`, worktree
`.claude/worktrees/Show-every-FITS-bottom-up-as-Siril-does`.

## Context

The owner reported on 2026-10-26 that `starless_gradremoved_syqon_studio_in_cc_stretch0.15.fit`
shows upside down against Siril. Its header says `ROWORDER = 'TOP-DOWN'` while its data is
stored bottom-up like its siblings', and Siril 1.4.4 ignores the keyword on display (measured
through `siril-cli`; the figures are in ADR 0039). ADR 0029's row order was built on the
premise that Siril obeys the keyword; the owner, shown the new fact and three options, chose
to ignore `ROWORDER` as Siril does.

## Steps

1. Rewrite the R017 tests in `FitsHeaderTests` and `FitsImageLoaderTests` so that `TOP-DOWN`
   is bottom-up too, and the two tests that wrote `TOP-DOWN` fixtures to put the first row at
   the top (R015, R016's hot pixel) so that they write bottom-up data; see them fail.
2. `FitsImageLayout.DisplayRow` always counts from the bottom; `BottomUp` and the `ROWORDER`
   read go.
3. R-017 rewritten, README's orientation line, `docs/design/imaging.md`, the CHANGELOG line of
   the unreleased FITS feature, ADR 0039, ADR 0029's status line.
4. Gate green; the application run on the reported file against a sibling.
