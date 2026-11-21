# 0118. Culori pentru hartie and Soft proof work on the whole image

Date: 2026-11-21
Status: accepted 2026-11-21
Supersedes: 0084 in part (for the Culori pentru hartie tab alone: the preview's step chosen by the gallery's area, the cap of `CurvePreviewMaxPixels`, the recompute for a larger area and the one case where the view enlarges a preview; Editare Curbe keeps all four); 0066 in part (the proof made on a reduction to `ProofMaxSide`, 2400 on the long side)

## Context

Both tabs showed a reduced copy: the paper fit's gallery one made for its area (on a Full HD
screen every fourth pixel of a 23 MP image, 1460 x 967) and never over 8 MP (ADR 0084), the soft
proof one of at most 2400 pixels on its long side (every third pixel, 1946 x 1289). Their zoom,
up to 8:1, enlarged the copy's pixels, so at a high zoom a square on screen was 3 or 4 pixels of
the image and the stars could not be judged. On 2026-11-21 the owner noticed it and asked what
the whole image would cost. ADR 0084 had measured the fit at 8.0 s and 1.6 GB and drawn the cap
from it; the proof had never been measured. Both were measured again that day (the table under
Consequences), and the owner chose the whole image, with the two choices below.

## Decision

1. Both tabs work on the image itself, a step of 1, while it has at most
   `WholeImageMaxPixels` (64 MP); a larger image is reduced by the smallest whole step that
   brings it within (`RasterOps.WholeImageStride`). The set of each run says its step, and the
   texts under the image call it a reduced copy only when it is one.
2. The paper fit's gallery no longer depends on its area: one computation per image, about
   12 s on the reference FITS, with "computing" shown meanwhile, and no recompute for a larger
   area. Its bitmaps and red layers are made on the computation's thread, since on the window's
   they would hold it for 0.9 s.
3. In Culori pentru hartie nothing is enlarged to fill the area: an image smaller than it
   stays at its own size, and one reduced past the cap still has at least 16 MP. Soft proof
   keeps its own rule, which this decision does not touch: its image is fitted to the area,
   enlarged too when it is smaller (R-163).
4. Editare Curbe and Crop with aspect ratio keep their reduced copies and ADR 0084's rules:
   the owner asked about the two tabs where colour is judged pixel by pixel.
5. The fit's computation starts when the soft proof's has ended, not beside it as ADR 0051 had
   every run start: `MainViewModel` hands the fit the proof's task, and the fit shows
   "computing" from the start. Chosen by the owner the same day, on the measurement under
   Consequences: started together, each takes the cores from the other.
6. The cap stays at 64 MP with the peak known (about 115 MB for each megapixel while the runs
   last, 7 GB at the cap): the owner's answer the same day, when the figure that the cap had
   been proposed on, the 40 MB held afterwards, turned out to be the smaller of the two.

## Alternatives rejected

- **The whole resolution only for the part on screen.** The overview stays on the small copy
  and a zoom past it computes the visible piece from the image, about 2 MP, at today's cost in
  time and memory (both the fit and the proof go pixel by pixel through a table, so a piece can
  be computed alone). The assistant recommended it; the owner chose the whole image: the cost
  measured is one this PC carries, and the viewers stay as they are.
- **Larger caps without the whole image** (5.6 MP, a step of 2). Cheap, and at a high zoom the
  pixels shown are still not the image's.
- **The small gallery first, the whole image when it is ready.** An image after 2 s instead of
  12, for a second computation, a swap under the user's eyes and a zoom to carry from one size
  to the other. Left out until the wait proves to bother.
- **A cap of 32 MP.** The peak would stay under 4 GB, and the cameras of 45 to 61 MP would be
  shown at every second pixel, which is what this decision is meant to end.
- **No cap.** The memory grows with the image, about 40 MB held for each megapixel by the two
  tabs together as first measured (some 30 MB as built, about 115 MB while the runs last), and the runs start with every image opened, whether the tabs are looked at or
  not: a 150 MP mosaic would hold about 4.5 GB and need about 17 GB while the runs last, where
  today it works. 64 MP covers the cameras
  of up to 61 MP; it is a presumption about the images and the PC, not a measurement.

## Consequences

- R-188 says so; R-160, R-163, R-165, R-178 and R-179 point to it. `SoftProofTests` checks
  that a 2401 pixel wide image is proofed whole and the step of five sizes around the cap;
  `GamutFitTests`, `GamutFitTabTests` and `SoftProofTabTests` that the images shown have the
  image's size and that the sentences call it a copy only when it is reduced; `GamutFitTabTests`
  also that the fit waits for the task it is handed and is abandoned while it waits, and, through
  `MainViewModel`, that the fit's run starts after the proof was rendered (decision 5), and, on
  the window's own thread, that the gallery's bitmaps are made on another (the thread of the
  tab's "gallery prepared" log line). No test builds an image over 64 MP: the runs and the two
  tabs take the cap as an argument (`maxPixels`, `MaxPixels`), `WholeImageMaxPixels` by
  default, and the tests lower it, so a small frame is proofed and fitted as a reduced copy,
  with its mask, its bitmaps and its sentences; a fit test on a frame 3200 pixels wide tells the
  whole image from the step the 1600 x 900 area gave it before.
- Measured 2026-11-21 on the plate-solved FITS (5837 x 3867, 22.6 MP) through the lab's baryta
  profile (`Hahnemuhle PRBaryta.icm`), a scratchpad program over the branch's core and window
  assemblies, 8 logical cores, one run each; the memory is the process's private bytes after a
  full collection, less the 414 MB held after the analysis:

  | | the copy until now | the whole image |
  | --- | --- | --- |
  | soft proof: reduction and mask | 0.17 s | 0.59 s |
  | soft proof: first render of an intent, with its table | 0.85 to 1.08 s | 1.56 to 1.74 s |
  | soft proof: a render with the table built (a change of intent) | 0.09 to 0.17 s | 0.82 to 0.83 s |
  | soft proof: the three bitmaps | 0.08 s | 0.43 s |
  | soft proof: held by the tab | +101 MB | +517 MB |
  | paper fit: the three variants | 0.79 s | 9.92 s |
  | paper fit: the whole run with the bitmaps | 1.92 s | 12.00 s |
  | paper fit: the bitmaps and red layers | 0.11 s | 0.92 s |
  | paper fit: held while the run lasts | +82 MB | +943 MB |
  | paper fit: held by the tab afterwards | +48 MB | +397 MB |

  The copies until now were the proof's 1946 x 1289 and the fit's 1460 x 967 (the presumed Full
  HD area). With both tabs on the whole image the process holds about 1.3 GB on this image,
  where it held about 0.6 GB.
- The same day, the two tabs driven through their view-models on the same image, both
  computations started together as `MainViewModel` starts them (two runs): the proof is shown
  after 9.5 and 14.5 s and the fit after 17.8 and 18.3 s, where alone they take 3.2 s and
  12 s, since each loop of the two wants every core; started one after the other, the proof
  is shown after 3.2 s and the fit after 15.0 s. Either way the process holds 1 330 MB
  afterwards (916 MB over the analysis) and its working set peaks at 3.4 to 4.0 GB on this
  PC of 32 GB. With the managed heap limited to 2 GB (`DOTNET_GCHeapHardLimit`) both orders
  still complete, in the same times, peaking at 2.6 to 2.7 GB: about 115 MB for each megapixel
  are needed while the runs last, the rest of the peak being memory not yet collected. An
  image at the cap, 64 MP, would therefore need about 7 GB while the runs last and hold about
  2.6 GB afterwards (about 1.9 GB as built, the point on the code as built below).
- The fit is shown about 14 s later than before (15.5 to 16.3 s after the analysis, where
  it took 1.9 s), in the background; a tab opened sooner shows "computing".
- How smoothly the window zooms and pans bitmaps of 22.6 MP was not measured: it shows only in
  the running window, and the owner tries it on the branch's build before the merge.
- `/validate`'s paper fit case (V-016) now computes whole-image previews it does not read,
  about 10 s more; its figures are measured on the tonal subsample and do not move. The proof's
  case, V-015, is measured on the proof itself, so it moved: its width 1946 -> 5837, its share
  outside the paper's gamut 1.8983 -> 1.9155 % (every pixel counted, not every third), its six
  means by at most 0.001; recorded again at the owner's word (`validation.md`, 2026-11-21).
- The soft proof tab no longer keeps the proofed raster once its bitmap is made, only its
  size and intent: 271 MB less held on this image than the 517 MB of the table above, which
  was measured before that.
- The code as built, measured the same day in the same way (the fit after the proof, the
  proofed raster dropped; two runs): the proof is shown 3.3 s after the analysis and the fit
  15.5 and 16.3 s after it; the process holds 1 069 MB afterwards, 655 MB over the analysis,
  some 30 MB for each megapixel; its working set peaks at 3.5 GB, and at 2.7 GB with the
  managed heap limited to 2 GB.
- A fit's computation that a new analysis overtakes still runs to its end beside the new one,
  now for about 12 s and 0.9 GB (`known-limitations.md`); cancelling it stays undone, as in
  ADR 0084.
- `Thresholds.ProofMaxSide` is gone; `CurvePreviewMaxPixels` stays for Editare Curbe and the
  crop. `soft-proof.md`, `paper-fit.md`, `window.md`, `pipeline.md`, `thresholds.md`,
  `known-limitations.md` and the README say the new rule.
