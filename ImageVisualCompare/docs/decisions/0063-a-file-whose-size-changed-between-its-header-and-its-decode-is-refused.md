# 0063. A file whose size changed between its header and its decode is refused

Date: 2026-11-21
Status: accepted 2026-11-21

## Context

A pair is loaded in two passes (ADR 0014, ADR 0015): the two headers are read first, the canvas
is settled from their sizes (`ImagePairValidator.Inspect`), and when the sizes differ the user
is asked whether to pad, before anything is decoded; then the files are decoded, each read from
disk again. Nothing compared what was decoded with what the canvas was settled from. The review
of 2026-11-13 listed it (`docs/review/2026-11-13-code-review.md`, CR-017), confirmed by reading
`ImagePairLoader`:

- In `LoadAsync` the padding question can stay open for as long as the user leaves it; a file
  saved again meanwhile is decoded at its new size and put on the canvas of its old one.
- In `ReplaceAsync` the canvas is settled from the kept image as it is on screen, opened
  perhaps hours before, and the kept file is read from disk again when the pair's mode falls
  back. The review's case: a pair of FITS in `Histogram`; file A is saved again from Siril,
  cropped, under the same name; the user changes B to a PNG. The mode falls to `Linear`, A is
  decoded again at its new size, and the pair has the canvas of the old size, with no padding
  question: an image layer of one size on a backdrop of another, and a status bar naming a
  size that neither is.

Both decoders report the size they decoded (`WicImageLoader` describes the frame it decodes,
`FitsImageLoader` the header it reads at the decode), so the pair holds what is needed for the
check.

The owner chose on 2026-11-21, of two options (refuse with a message; settle the canvas again
and ask again).

## Decision

After each decode, `ImagePairLoader` compares the decoded image's width and height with those
the canvas was settled from: the probed header in `LoadAsync` and for the new file of
`ReplaceAsync`, the image on screen for the kept file decoded again. On a difference the load
fails as a file that cannot be read does (R-005): `Could not read <file name>.` and, on the
next line, a text of its own, `UiText.FileChangedOnDisk`:

- English: `The file has changed on disk: it was 5938 × 3888, it is now 4000 × 3000. Open the pair again.`
- Romanian: `Fișierul s-a schimbat pe disc: era 5938 × 3888, acum este 4000 × 3000. Deschide din nou perechea.`

What follows is what follows any such failure, with nothing new in the window: after an open
the dialogs ask again; after a change of one image or a switch of mode the pair on screen stays
as it was. A changed image A ends the load before B is decoded.

## Alternatives rejected

- **Settle the canvas again from the decoded sizes and ask the padding question again.** The
  load would go on to a pair, which is kinder. But it adds a second question after the decodes,
  a decline after the memory was spent, and in a change of one image a kept file that may be
  decoded twice; and the pair shown would not be the one the user thought was open. The case is
  rare, and `Open…` gives the same pair, planned from what is on disk now, in one step.
- **A check of the file's date or length.** It says the file was written, not that the image
  changed, and a file saved again at the same size is shown as it is on disk now, as any open
  shows it: nothing is wrong with that pair.
- **A message of its own, without `Could not read`.** One more title and flow for the window to
  handle; the first line names the file, which is what the user needs, and the second says why.

## Consequences

- Only the size is compared. A file saved again with other pixels, another depth or another
  format at the same size is decoded and shown as it is now; the status bar says what it is.
- After the review's case the user cannot change B until the pair is opened again: each try
  decodes the kept A again and is refused. The message says to open the pair again.
- The image decoded before the refusal is dropped as after any failed load; when its memory
  comes back is CR-022 of the same list.
- A switch of mode already reads both headers again (`LoadAsync`), so a file changed before the
  switch gives a pair planned from the new sizes; the check there covers only a change during
  the switch itself.
- Guards: `ImagePairLoaderTests.R005_AFileWhoseSizeChangedSinceItsHeaderWasRead_IsRefused`,
  `R009_AKeptFileWhoseSizeChangedOnDisk_IsRefused_NotShownOnTheOldCanvas` and
  `R009_ANewFileWhoseSizeChangedSinceItsHeaderWasRead_IsRefused`, with a loader that decodes
  another size than it probed; `UiTextTests` holds the two wordings.

Source: docs/review/2026-11-13-code-review.md (CR-017);
src/ImageVisualCompare.Wpf/Imaging/ImagePairLoader.cs; src/ImageVisualCompare.Core/UiText.cs.
