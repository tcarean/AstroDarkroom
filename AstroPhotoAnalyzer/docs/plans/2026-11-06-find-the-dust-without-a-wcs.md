# Find the Great Rift without a WCS

Date: 2026-11-06. Branch `claude/Find-the-dust-without-a-wcs`, worktree
`.claude/worktrees/Find-the-dust-without-a-wcs` (from `main`, the kit's rule "Worktrees").

## Context

The owner asked, on the Smida Milky Way frame of 2026-08-12 (a plate-solved FITS and a TIFF of
the same frame), that without a WCS the Great Rift and the Pipe count as dust, with figures
close to the WCS path's, and whether thresholds alone could do it. The measurements are in ADR
0085: without a WCS the sky model's 25th percentile sinks into the Rift, so the dust class is
0.9 % of the frame and the darkest 1 % of the sky decides (L\* 3.48 against 4.85 with the WCS);
with the median of each cell and 3 sigmas the class is 9.8 % at L\* 4.90, and on the deep-sky
frames the verdict's dust does not move. The owner chose both thresholds on 2026-11-06, after
the figures at two stretch levels, with added pixel noise and on the starless pair.

## The change

1. `Thresholds.cs`: `SkyModelLowPercentile` 25 -> 50, `SkyNoiseK` 4 -> 3, with their comments.
2. `docs/requirements.md` R-044: the two values and the re-measured examples.
3. ADR 0085, superseding ADR 0033's two values.
4. Goldens: regenerated with `APA_UPDATE_GOLDEN=1`, every changed line read and tied to the
   change (the synthetic sky-path scenes' classes and thresholds).
5. `/validate` with the reference images copied into the worktree: the sky-path cases (V-001,
   V-003, V-005, V-009 to V-014) re-measured, the drift table pasted at wrap-up,
   `validation.json` and `validation.md` updated with a dated History line.
6. `docs/design/thresholds.md`, `known-limitations.md`, the README's sky-path paragraph,
   CHANGELOG.

## Out of scope

- The band path's absolute `DustDepthLstar` (5 L\*), which finds less of the Rift on a darker
  stretch (ADR 0085's measurement at 0.10): a task of its own, if the owner wants it.
