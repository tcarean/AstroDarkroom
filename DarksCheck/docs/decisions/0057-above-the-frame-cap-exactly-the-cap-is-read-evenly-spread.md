# 0057. Above the frame cap exactly the cap is read, evenly spread from the first file to the last

Date: 2026-11-14
Status: accepted 2026-11-14
Supersedes: 0007 in part (every k-th file above the cap)

## Context

The darks and the biases are held in memory, so a group is capped at `Th.MaxFramesInMemory`
(60) frames or at `--max-frames` (ADR 0007). Above the cap one file in k was decoded, k =
ceil(count / cap). The code review of 2026-11-13 (CR-008) counted what that gives: 61 darks
read 31, 90 read 45 and 119 read 60, then 121 read 41: just above the cap, and just above each
of its multiples, the count read fell to about half the cap or under it. The master,
its noise, the index and check 9 were those of the frames read, so a folder of 61 darks whose
lights need 39 read `WARNING: 31 frames are few` while its own master of 61 is enough. The
memory the cap protects was never at stake: 60 frames are 60 frames however they are chosen.

The owner chose on 2026-11-14, in chat, between keeping one file in k and reading exactly the
cap.

## Decision

Above the cap N exactly N files are decoded: file `round(j x (count - 1) / (N - 1))`, counted
from 0, for j = 0 .. N - 1 (`FrameStack.Chosen`), so the first and the last file are always
read and the others lie at even distances between them; for a cap of 1, the first file. The
distance is over 1, so the N files are distinct. A skipped frame says `skipped (over 60 frames
only 60 are read, evenly spread)`. The rule is the same for the darks, the biases and a
partner's darks; the lights are never capped (ADR 0041).

## Alternatives rejected

- **One file in k** (the owner's call). Simple to say, and it wastes up to half the cap.
- **The first N files.** The series warms as it goes (check 4): the first N are the coldest,
  and the end of the series, which check 4 reads, would be cut off.
- **A higher cap.** It moves the same step to another count; the memory is the reason for a
  cap at all.
- **`floor(j x count / N)`.** Evenly spread too, but it never reads the last file when the
  count is not a multiple, and the last frames are the end of the series.

## Consequences

Above the cap the distances between the frames read are no longer all equal (with 61 files
one gap is 2, the others 1). Nothing in the analysis counts on equal distances: the cadence
is read from every file's metadata, skipped or not, and check 4 and the leak fit read each
frame's own figures. The example of R-027 changed with the rule (five frames under a cap of
2 read files 1 and 5, not 1 and 4), and its test was rewritten for it. No real session of
`docs/validation.md` has more than 35 darks or 30 biases, so none is read differently. Guarded
by `R027_AboveTheCapExactlyTheCapIsDecodedEvenlySpreadFromTheFirstToTheLast`.
