# Add the Romanian "de" to counts of 20 and up

Plan for the task branch `claude/Add-the-Romanian-de-to-counts`, worktree
`.claude/worktrees/Add-the-Romanian-de-to-counts`, cut from `claude/Explain-the-ADU-scale-tab-plainly`
at 224ad4ad (not merged yet), so the scale section's new words are already there; that branch
merges first. Written on 2026-11-02. The owner asked for the task in this session and approved
the list below as proposed ("Da, aplica-le", 2026-11-02). The version is not changed.

## Context

Romanian puts `de` between a count and the noun it counts from 20 up, unless the last two digits
are 01 to 19: `20 de cadre`, `101 cadre`, `120 de cadre`. DarksCheck's Romanian texts never wrote
it: `darks\ (20 cadre .NEF)`, `443 pixeli peste 20 ADU-eq`, `stack-ul celor 32 lights`.
AstroPhotoAnalyzer has the rule as `Fmt.RoCount`; DarksCheck takes the rule, not the file.

## The approved changes

The English stays as it is; the figures stay what they are.

| # | Counted word | Before | After |
| --- | --- | --- | --- |
| 1 | frames | `darks\ (20 cadre .NEF)`, `20 cadre decodate din 20`, `media a 20 cadre`, `OK: 20 cadre ajung`, `de la 57 cadre`, `peste 60 cadre`, `facut din 20 cadre` | `20 de cadre` in each: `darks\ (20 de cadre .NEF)`, `20 de cadre decodate din 20`, `media a 20 de cadre`, `OK: 20 de cadre ajung`, `de la 57 de cadre`, `peste 60 de cadre`, `facut din 20 de cadre` |
| 2 | pixels | `443 pixeli peste 20 ADU-eq`, `434 pixeli comuni`, `86 pixeli persistenti`, `25 pixeli morti, 30 saturati`, `o zona de 50 x 50 pixeli` | `443 de pixeli ...`, `434 de pixeli comuni`, `86 de pixeli persistenti`, `25 de pixeli morti, 30 de saturati`, `o zona de 50 x 50 de pixeli` |
| 3 | lights | `din 32 lights`, `stack-ul celor 32 lights`, `20 lights nu s-au putut decoda` | `din 32 de lights`, `stack-ul celor 32 de lights`, `20 de lights nu s-au putut decoda` |
| 4 | others | `24 valori distincte`, `20 bias-uri nu s-au putut decoda`, `20 dark-uri ale partenerului`, `20 pauze peste 5 s` | `24 de valori distincte`, `20 de bias-uri ...`, `20 de dark-uri ...`, `20 de pauze peste 5 s` |
| 5 | unchanged | counts under 20 and those ending in 01 to 19 (`4 cadre`, `19 cadre`, `101 cadre`), figures with decimals (`2.0 grade`, `de 1.5 ori`), units (`120 s`, `65535 ADU`, `24 Mpx`) | the same |

The owner kept the word `lights` (`32 de lights`), the word the program uses everywhere.

## Steps

1. Requirements: R-095, new, states the rule with its exceptions; the examples of R-002, R-003,
   R-023, R-027 and R-080 that quote a count of 20 and up take `de`.
2. Test: `R095_...` pins the rule's edges (19, 20, 100, 101, 119, 120, a figure with decimals)
   and a library of 20 synthetic darks read in both languages; the tests that quote such a count
   take `de`.
3. Code: `Ui.RoCount`, for a count or a whole figure as shown, and every Romanian count of the
   console and the window through it (`RunReport.Frames`, `DarkReport.Pixels` and the counts
   written inline in `Report/` and `Analysis/`).
4. Goldens: the Romanian scenarios are recaptured; only lines with a count of 20 and up may
   change, and no figure.
5. ADR 0029 for the rule; CHANGELOG under Fixed.
6. Gate, `/validate` (the change touches `Report/` and `Analysis/`), commit, then the review and
   the merge proposal, after the ADU branch's.

## What deviated

- Counts the list did not show take the same rule, since they go through the same helper: the
  time constant of check 3 when it is counted in frames (`constanta de timp 20 de cadre`; in
  minutes it stays `20 min`), the doubling rule's degrees when the setting is 20 or more, and the
  counts of `Thresholds.cs` written into texts (`sub 6 cadre`, `de peste 3 ori`), which are under
  20 and read as before.
- Three tests quoted a Romanian count of 20 and up and now quote it with `de`: R051 and R053 in
  `LightsAndIndexTests` (`21 de pixeli persistenti`, `20 de pixeli`) and R080's paragraph
  (`50 x 50 de pixeli`); the window's tests that use such counts as plain inputs (the emphasis
  tests) were left as they are.
- The doc comments of `RunReport.Frames` and `DarkReport.Pixels` quoted the old words and now
  quote the new.
