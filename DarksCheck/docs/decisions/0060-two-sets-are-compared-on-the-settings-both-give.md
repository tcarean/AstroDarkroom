# 0060. Two sets are compared on the settings both give; one a set does not give is taken from the other

Date: 2026-11-14
Status: accepted 2026-11-14
Supersedes: 0053 in part (a set needing both settings to be compared on any); 0055 in part (other settings needing both settings on both sides)

## Context

A frame's factor to ISO 400 and 120 s needs its ISO and its exposure, both (ADR 0004). ADR
0053 decided what happens when a set does not say its settings: it is compared with the other
set as made at that set's settings. "Does not say its settings" was all or nothing: a set
that gave its exposure and no ISO had no factor either, and its exposure was not used. Frames
of that kind come from capture programs that write the gain and no ISO.

The two reviews of the fixes of CR-006 to CR-010, on 2026-11-14, followed what that gives
(CR-076): darks of 300 s against lights of 120 s, no ISO in the files, are judged on the
files' ratio as it stands, so check 2 can read `OK: the dark can be subtracted as it is`, or
a temperature difference that is the exposure's, under check 1's red WARNING on the two
exposures; and the session's darks of 120 s against a partner of 300 s read a verdict with no
word that the exposures differ. ADR 0055 had to carve the case out of "other settings",
because nothing was brought from one set to the other there.

The owner chose on 2026-11-14, in chat, the rule below over two others (one more sentence in
the verdict; leaving the entry, since the owner has no such frames).

## Decision

1. **Setting by setting.** Two sets are compared on the settings both give. A setting one of
   them does not give is taken from the other; one neither gives is taken as the same. The
   factor of one frame over another is `Normalisation.Relative`: the gain per ISO stop raised
   to the stops between the two ISOs, when both give an ISO, times the ratio of the two
   exposures, when both give one.
2. **Nothing moves for frames that give both.** A light against darks, and a partner against
   the session's darks, keep their own factors when both sides give both settings, by the
   arithmetic of before. Otherwise the factor is the reference set's times the relative one.
3. **Other settings** (ADR 0055) is again the rule of a stack alone, `FrameStack.SameSettings`:
   each setting compared when both give it. It is the same settings the factor follows, so a
   set at other settings is always one whose figures were brought, and the report never says
   "worked out" of something that was not.
4. **The report says what was taken.** Three WARNING rows, for the lights against the darks
   and for the darks against a partner: a set that lacks a setting the other gives took the
   other's; the reverse; and a setting neither gives was taken as the same, named (the ISO,
   the exposure, or both). The partner's row shows the settings the partner gives.
5. **The chained row** keeps the second review's rule, setting by setting: a setting that was
   not used between two sets stands, for the third, where the set it was taken from stands.
6. **Each set's own index** stays as ADR 0053 left it: in the file's ADU, said so, unless the
   set gives both settings.

## Alternatives rejected

- **One more sentence in the verdict.** The verdict would still be taken from a ratio that
  mixes temperature with exposure.
- **Leaving the entry open.** The owner's call; the frames are those of dedicated astronomy
  cameras, which the owner does not use.
- **Reading the gain.** A FITS `GAIN` is not an ISO: its unit and scale differ by camera and
  by capture program, and nothing here could turn two gains into a factor.
- **Unmeasured when a setting is missing.** It would undo ADR 0053 for the sets it was written
  for, FITS darks without an ISO beside raw lights.

## Consequences

Taking an ISO that neither set gives as the same is an assumption: with different gains the
ratio is wrong by their factor, and the program cannot know. The WARNING row says the
assumption, and check 1 still says that the frames do not state the ISO. The case was shown on
generated frames only; no session of `docs/validation.md` has such frames, and `/validate`
was run to show that their figures did not move (the plan
`docs/plans/2026-11-14-fix-cr-076.md` holds the result). Files that give neither setting on
either side get a WARNING row where the report said nothing. The lights' own line, "N lights
do not give their ISO or exposure; the darks' were taken", no longer appears for a setting the
darks lack too. `R052_SetsThatDoNotSayBothSettingsAreNotAtOtherSettings`, which pinned ADR
0055's carve-out, was replaced. Guarded by
`R052_ASettingBothSetsGiveIsUsedAndOneASetDoesNotGiveIsTakenFromTheOther` and
`R063_APartnerIsComparedOnTheSettingBothGiveAndTheReportSaysWhatWasTaken`.
