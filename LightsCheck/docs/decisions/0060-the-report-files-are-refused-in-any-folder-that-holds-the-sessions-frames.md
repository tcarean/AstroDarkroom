# 0060. The report files are refused in any folder that holds the session's frames

Date: 2026-11-16
Status: accepted 2026-11-16

## Context

ADR 0002 says the tool writes its files "beside the session or where the user asks, never
inside `lights/` or `biases/`", R-028 and R-032 repeat it, and the rule "Read-only input" of
`CLAUDE.md` is the one rule the owner gave with the photos. Nothing enforced it:
`SeriesOutputs.WriteAll` wrote into the folder it was given, and its comment left the choice to
"the caller", which did not check. The review of 2026-11-13 (CR-014, `confirmed`) gave three
ways to the files among the frames: `lightscheck S --out S\lights`; in the window, a double
click on `lights` in the folder dialog, which opens at the session root; and a session whose
frames sit in the folder itself, where the window proposes that very folder. No frame is
touched by any of them, and the three files have no frame's extension, so no later run reads
them as frames; what is broken is the promise. The tests that carry the rule in their names
wrote elsewhere and then checked that `lights\` had not changed, which cannot fail. Which
folders to refuse had two reasonable answers, and the owner chose on 2026-11-16.

## Decision

The three files are refused in any folder that holds the session's frames: the folder of the
lights, of the biases and of the darks (the darks are only counted, R-042, and are the user's
frames all the same), and any folder inside one of them. When the lights sit in the session
folder itself, that folder is refused and a folder inside it is not, since everything beside
the frames is inside it; the refusal then ends with `A new folder inside it is fine.`

The check is the core's, in `SeriesOutputs.WriteAll`, so no caller can write there: it throws
`InvalidOperationException` with the sentence the user reads, before a folder is made or a file
opened. The console also hands its `--out` folder to the run (`SeriesOptions.OutputFolder`),
which checks it as soon as the session is open, so `--out S\lights` is refused at once and not
after the minutes of measuring; the window has no folder until the user asks to write, and its
refusal is the line under the top bar, where an export error already goes. Two folders are the
same when their full paths are, without a trailing separator, whatever the case of the letters
on Windows.

## Alternatives rejected

- **Only `lights\` and `biases\`**, the rule to the letter: a session with its frames in the
  folder itself could then still take the files beside the frames, and `darks\` stayed open.
  The rule is about the frames, not about two folder names.
- **The check in each caller.** That is what the comment of `WriteAll` asked for and nobody
  did; a third caller would forget it again.
- **Refusing every folder inside the session folder.** The session root beside `lights\` is
  where ADR 0002 wants the files, and where the window's dialog opens.

## Consequences

A user who kept the report files among the frames is told to choose another folder; nothing is
moved or deleted for them. A folder reached through a link or a mapped drive under another
name is not recognised as the same: the comparison is of the paths as typed, made full.
`biases\` or `darks\` without a frame in it is not a folder of the session (`SessionFolder`
keeps none for it) and is not refused. Guarded by
`R028_TheFilesAreRefusedInAnyFolderThatHoldsTheSessionsFrames`,
`R028_AFolderBesideTheFramesIsWritten`,
`R028_WhenTheLightsSitInTheSessionFolderThatFolderIsRefusedAndOneInsideItIsNot`,
`R028_OutIntoLightsIsRefusedBeforeAnyFrameIsMeasured`
and, in the window, `R032_AnExportIntoLightsIsRefusedWithALineAndNoFile`.
