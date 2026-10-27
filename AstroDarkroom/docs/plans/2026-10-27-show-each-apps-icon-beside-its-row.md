# 2026-10-27 Show each app's icon beside its row

## Goal

R-010: the launcher's row of each app shows the app's own icon, the one its window exe
carries, dimmed when the app is missing from the suite folder. The owner asked for it on
2026-10-27 ("the icon of each application beside it"); the session ran without the owner
present, so the source of the icon was decided and recorded in ADR 0011 as proposed, with the
alternatives, for the owner's yes at the merge.

## Steps

1. Copy the six components' `.ico` files into `Icon/` under their own names.
2. `SuiteApp` gets `Icon`, the file name; the six catalogue lines name theirs; R-001's test
   expects it.
3. R-010 in `docs/requirements.md`; `R010_EveryAppsIcon_IsACopyOfTheComponentsOwn` in
   `SuiteTests`: the copy exists, and beside the siblings has the bytes of the component's
   `ApplicationIcon` under the same file name.
4. The window: every `Icon/*.ico` a resource; `Icons.LargestFrame(fileName)` replaces
   `App.LargestIconFrame()` and caches each decoded frame; `AppRow.Icon`; a first column
   in the row's card with the image at 40 px and an opacity trigger on `IsAvailable`.
5. ADR 0011, the design note, CHANGELOG, README, `CLAUDE.md` (`Icon/`, the new-app rule),
   `docs/skills.md`.

## Verification

`scripts/verify.ps1` green; the window started from a suite folder shows the six icons, and
from the build folder shows them dimmed.

## What deviated

Nothing in the steps. The R-010 test was written after the copies were in place, so it never
failed on its own; its teeth were checked by appending a byte to `Icon/copywcs.ico`, which
made it fail with `Icon/copywcs.ico differs from ...\WCS_Copy\Icon\copywcs.ico`, then
restoring the copy. The window was checked from a mock suite folder (the build output, empty
files named like the six exes, the 1.0.0 `release.json`) and from the build folder, captured
with PrintWindow: six icons at 40 px, dimmed in the second.
