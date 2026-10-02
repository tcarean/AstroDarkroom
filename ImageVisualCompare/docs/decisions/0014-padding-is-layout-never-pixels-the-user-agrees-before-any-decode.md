# 0014. Padding is layout, never pixels: both images top-left on the larger extent, and the user agrees before any decode

Date: 2026-10-02
Status: accepted 2026-10-02

## Context

Retroactive: recorded on 2026-10-02 from `CLAUDE.md` ("Invariants", "Padding is layout, never
pixels"), the comments of `PadPlan`, `ImagePairValidator.Inspect` and `ImagePairLoadResult`, the
comments in `MainWindow`'s `OpenPairAsync`, `ConfirmPaddingAsync` and `ShowPair`, and
`README.md` ("Dacă dimensiunile în pixeli diferă, ești întrebat înainte să se încarce ceva", the
paragraph after it, and "Când perechea a avut nevoie de padding"); the decision itself dates
from 861f5c1 (image padding to allow comparison of unequal images, 2026-09-09). Before it,
`ImagePairValidator.Validate` refused any pair whose pixel dimensions differed, with both
resolutions in the message; the `Inspect` comment still says that differing dimensions are "not
a refusal any more". A decoded image is a Bgra32 bitmap, 84 MB for a 21 MP frame (README
"Memorie"), and the pixel readout reads both images at the same content coordinate.

## Decision

- The canvas is `PadPlan`, from `ImagePairValidator.Inspect`: the larger of the two widths and
  the larger of the two heights, with `RequiresPadding` true when either differs. Only the pixel
  dimensions count.
- Each image keeps its own pixel size (ADR 0012) and sits at the origin of the canvas, top-left.
  The two backdrops (`ImageBackdropBrush`, `#000000`) cover the whole canvas and are what shows
  where the smaller image ends (ADR 0013). No bitmap is copied or enlarged, `PadPlan` carries no
  per-image offset, and the files are not touched.
- A mismatched pair is put to the user after both probes and before either decode (ADR 0015),
  through the `confirmPadding` delegate of `ImagePairLoader.LoadAsync`. `ConfirmPaddingAsync`
  shows `MessageDialog.Confirm` (ADR 0018), titled `Images have different sizes`, with the text
  of `ImagePairValidator.DescribeMismatch`: both file names, both resolutions and the canvas.
  Yes goes on to the decode. No returns `ImagePairLoadResult.Declined` (`WasDeclined` true, no
  `Error`), and `OpenPairAsync` reopens the file dialogs without a message.
- A caller with no user to ask passes no delegate; a mismatched pair is then refused, and that
  same text is the error.
- A padded pair's status bar gives the canvas and both originals, as in the README's
  `600 × 400 padded · A 600 × 300 · B 400 × 400`: the canvas size alone would be a half-truth,
  since neither image is that size (the `ShowPair` comment).

## Alternatives rejected

- **Copy the smaller image into a buffer the size of the canvas.** At 84 MB an image, that copy
  would be the most expensive thing the application does, and the readout could no longer tell
  the padding from a black area of the picture.
- **Centre the smaller image.** A content coordinate would stop being a pixel coordinate in
  both images: the readout and the minimap would need offsets, and `PadPlan` would have to carry
  them.
- **Pad without asking.** It is not something to do behind the user's back: half the point of a
  before/after slider is that the two halves line up (the `Inspect` comment).
- **Report a declined question as a failure.** The user answered; a second dialog saying so
  "would only make the user dismiss their own decision" (the `OpenPairAsync` comment).

## Consequences

- A content coordinate is a pixel coordinate in both images: the readout needs no offset and
  prints `—` past the end of the smaller image on its own bounds check, so the padding never
  reads as black pixels of the picture.
- Until the user answers, a mismatched pair has cost two header reads; declining costs nothing
  more.
- The minimap's thumbnail is drawn of the canvas, image A over black, so its viewport rectangle
  lands where it should on a padded pair (`CompareView.CreateThumbnail`).
- Guards: `ImagePairValidatorTests` (the larger extent on each axis, in either order; the
  question names both files, both resolutions and the canvas), `ImagePairLoaderTests` (a
  mismatch reaches no decode, whether refused or declined; declining is not an error; an
  accepted pair keeps each image its own size; a matching pair is never asked); validation case
  V-002 of `ValidationTests` (ADR 0005).

Source: CLAUDE.md ("Invariants"); README.md ("Ce fișiere îi dai", "Ce îți arată bara de stare");
src/ImageVisualCompare.Core/ImagePairValidator.cs; src/ImageVisualCompare.Wpf/MainWindow.xaml.cs;
src/ImageVisualCompare.Wpf/Imaging/ImagePairLoader.cs;
src/ImageVisualCompare.Wpf/Controls/CompareView.xaml.cs; src/ImageVisualCompare.Wpf/Theme.xaml.
