# 0058. A setting known to differ is a WARNING of check 1, whatever the other setting says

Date: 2026-11-14
Status: accepted 2026-11-14

## Context

Check 1's match compares the darks' dominant exposure and ISO with the lights' (R-007, ADR
0010). Its verdict was computed only when both settings were known on both sides; otherwise
it read `UNMEASURED: the frames do not state the exposure or the ISO`. The code review of
2026-11-13 (CR-009) gave the case: frames from a capture program that writes `GAIN` and no
`ISOSPEED`, darks of 300 s and lights of 120 s. The two exposures stand in the rows above,
and the match says it could not be measured. The owner chose on 2026-11-14, in chat, a
WARNING on what is known.

## Decision

Each setting is compared on its own when both sides give it (`MatchAnalyzer.Analyze`). A
setting that differs, the exposures beyond `Th.ExposureTolerance` or the ISOs, makes the
verdict WARNING whatever the other setting says. OK needs both settings given on both sides
and equal. UNMEASURED is what is left: a setting not given on a side, and nothing given on
both sides that differs. The WARNING names both settings when all four are given, as before;
when the other setting is not given on both sides it names only the one that differs and says
which the frames do not state.

## Alternatives rejected

- **UNMEASURED whenever a side lacks a setting** (the owner's call). The report then
  withholds a mismatch it prints two rows above.
- **OK when what is known matches.** Equal exposures with unknown ISOs are not a match: the
  ISO may differ, and the report would say the darks have the lights' ISO.
- **The existing sentence with `ISO -` for the missing value.** It reads as a setting, not as
  something the files do not say.

## Consequences

A session whose frames give both settings reads as before; no golden file changed. With CR-006
(ADR 0055) the WARNING is drawn large and red in the window, in the partly known case too.
Check 2 already took a setting known to differ as "other settings"
(`FrameStack.SameSettings`), so the two checks now agree on such files. Guarded by
`R007_ASettingKnownToDifferIsAWarningEvenWhenTheOtherIsNotGiven`;
`R007_WithoutExposuresOrIsoTheMatchIsUnmeasured` still pins equal exposures without an ISO.
