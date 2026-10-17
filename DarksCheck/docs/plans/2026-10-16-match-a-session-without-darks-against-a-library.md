# 2026-10-16 Match a session without darks against a library

## Goal

A session that has `lights\` (and maybe `biases\`) but no `darks\`, the owner's Veil session of
2026-10-06 (87 lights of 120 s at ISO 1600, 30 biases, no darks; the plan's follow-up 6, agreed
on 2026-10-16), can be opened with a partner library (`--against DIR` in the console, the
partner folder in the window) and reports how its lights match the library's master: the
hot pixels the lights show, the lights-over-master ratio with its degrees and verdict, the
library's index and label, and the lights' equivalent index. Without a partner such a folder
stays an error, now saying what to do.

## Decisions proposed

1. **The partner's darks take the place of the session's darks** in one run, not a new kind
   of run. `DarkSeriesRun` opens the partner, decodes its darks into the stack the session's
   darks would have filled, and runs checks 2 to 8 on them, check 9 with the session's lights
   against their master, and INDEX from them; the session's biases, when it has any, still
   give the black of a FITS stack and the read noise. One report shape, one result record, one
   window; the checks 2 to 8 then describe the library, which the owner wants to know anyway.
   Rejected: a second, shorter run with only check 9 and INDEX (a second report shape, a
   second view-model path, and the library's own quality unreported).
2. **The report says whose darks they are.** The `=== DARK-URI` line reads
   `=== DARK-URI (ale partenerului <folder>): ...`; the SESIUNE card's `darks` row reads
   `(lipsa; se folosesc dark-urile partenerului)`; INDEX's `dark-uri` row is labelled
   `dark-uri (partener)`, the label is the partner's (`<library>-idx67`), and the `partener`,
   `dark-uri / partener`, `pixeli comuni` and `lights / partener` rows are not printed (the
   darks are the partner's). Check 1's `potrivire` compares the lights' settings with the
   partner darks' settings, so a library at another ISO is still flagged.
3. **R-002 is refined, not loosened.** A folder with `lights\` and no `darks\` opens only when
   a partner is given; the console's message without one becomes `sesiunea <root> are lights\
   dar nu are darks\; da --against <biblioteca> ca sa compari lights-urile cu ea` and exits 1;
   the window's problem card shows the same sentence, which names the Index tab's button.
   A folder with `biases\` or `flats\` but neither `darks\` nor `lights\` stays an error.
   `--inventory` on such a folder lists the lights and biases and says the darks are absent.
4. **Decision records.** ADR 0011 (the partner's darks as the session's; the refinement of
   ADR 0002's rule); R-002 and R-063 amended; new rows R-054 (the lights-only run and its
   report) and R-077 (the window's partner-required card); V-007 on the Veil lights against
   the 2026-10-10 library, with its figures recorded after the first run.

## Steps

1. `docs/requirements.md`: R-002, R-054, R-063, R-077 (`draft`), then the tests.
2. `SessionFolder.Open(path, allowLightsOnly: false)`: with the flag, a folder with `lights\`
   and no `darks\` opens with an empty `Darks` and `DarksDir` null-equivalent (the root);
   `Inventory` and `InventoryReport` print `(lipsa...)` for the darks and `NEMASURAT` for the
   match until the run supplies the partner's settings.
3. `DarkSeriesRun.Run`: when the session has no darks, `options.Against` is required
   (`InvalidOperationException` with the sentence of decision 3 otherwise); the partner's
   stack becomes `darks`, `DarkSeriesResult` gains `DarksFromPartner` (the partner's root, or
   null), `Partner` stays null, the match of check 1 is recomputed with the partner darks'
   `FrameInfo`s, the label is the partner's.
4. `DarkReport`: the header line and the three rows of decision 2; `DarkOutputs` unchanged
   (the CSV of darks describes the partner's darks, the header row says so in the report).
5. Console: `Program` opens the folder before the run as today; the message of decision 3.
   Window: `MainViewModel` passes the partner into the run as today; the problem card gets
   the sentence; `WindowTexts` the partner hint.
6. Tests: `SessionFolderTests` (the flag), `AcceptanceTests` (console messages, exit codes),
   `LightsAndIndexTests` (a lights-only fixture against a partner: the report's lines, the
   figures equal to the same lights against the same darks in a normal session),
   `MainViewModelTests` (the window path), a golden `lights-only.txt`.
7. V-007: `darkscheck "E:\Photografie\Astro\Veil\2026-10-06-3h" --against <library>`; the
   figures measured are recorded in `docs/validation.json` and `.md` (the kit's rule
   "Measured numbers": the run's output pasted at wrap-up).
8. Docs: ADR 0011, `dark-analysis.md` and `session-and-console.md` (the flag, the branch of the
   run), `window.md`, README (a new paragraph under `--against` and under the window), CHANGELOG
   (Added), this plan's deviations; `/code-review high` and a subagent review; commit; merge
   proposed.

## Verification

`scripts/verify.ps1` green; `/validate` with V-007 added; on the Veil session the console
prints the library's checks 2 to 8, the lights' persistent pixels against the library's
master, a lights / dark ratio with its degrees and verdict, `idx67` as the darks' index with
the library's label, and the lights' equivalent index; without `--against` it exits 1 with
the sentence of decision 3; the window shows the same with the partner chosen in the Index tab.

## What deviated

- The refusal sentence names both applications' ways (`--against DIR in consola, "Alege
  partener..." in fereastra`) and lives in `RunReport.LightsOnlyNeedsPartner`, so the window's
  problem card needed no text of its own; `WindowTexts` got nothing new.
- `SessionFolder.DarksDir` of a lights-only session stays the absent `darks\` path, not the
  root: `Label` never reads it on that path (the label is the partner's) and `FolderLine` is
  skipped, so no reader of it changes.
- The problem card's `Alege partener...` reruns only when the dialog returned a folder; a
  cancelled dialog does nothing (the first draft reran whenever the card was showing).
- The step's own review (`/code-review high`) found four points, three fixed (the cancelled
  dialog, the lights folder enumerated twice, the darks CSV of a lights-only run named as the
  partner's); the partner button shows on every problem card, which telling apart would need
  the core to type its refusals, left as is. A Fable subagent review found seven, all taken:
  the export could write into the partner library on a lights-only run (`WriteAll` now checks
  `DarksFromPartner`), a partner that does not open failed the run with a sentence that did
  not say it was the partner's (now `partenerul: ...`), the window note described the first
  draft of the button, the `aparat` row missed the partner darks' camera (now
  `Inventory.PartnerDarks`), V-007's figures lacked the pasted run (now in
  `docs/validation.md`), `--help` did not mention the new use of `--against`, and the rows
  R-054 and R-077 sat before R-053 and R-076.
