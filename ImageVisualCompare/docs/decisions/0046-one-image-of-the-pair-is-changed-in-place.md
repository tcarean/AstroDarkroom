# 0046. One image of the pair is changed in place, the other kept as decoded

Date: 2026-10-30
Status: accepted 2026-10-30

## Context

The owner asked (2026-10-30) to change one of the two images and leave the other as it is: a
reference against three or four variants of one processing, where `Open…` makes the user pick the
same reference again each time. Until now the only way in was a pair, both files chosen and both
decoded (R-001, R-008): on the real pair each decode is about 2 s and a buffer of 88 MiB, and the
view goes back to fit with the divider in the middle, so the place being looked at is lost. The
owner chose the same day where the buttons go: in the toolbar beside each file name, not over the
images.

## Decision

- One image is changed alone, through `ImagePairLoader.ReplaceAsync`: the new file is probed and
  decoded, the kept image is the `LoadedImage` already on screen, never read again. The padding
  question of R-003 is asked when the new file's size differs from the kept image's; the canvas is
  settled from the two as for a pair (R-004).
- Both images of a pair are always in one display mode: the mode asked for holds when the kept
  image and the new file are both shown as stored, and is `Linear` otherwise; when that leaves the
  kept image in another mode than the pair's, the kept file is decoded again in the pair's mode.
- The zoom, the pan and the divider stay when the new pair's canvas has the same size as the old
  one's, and the pair is shown at fit otherwise, as a switch of mode keeps the view (R-113).
- The two buttons, `Change A…` and `Change B…`, sit in the toolbar's left part beside the file
  names, where the pair is named; each opens one dialog, titled as that image's dialog of R-001 is.
  `Open…` and `Ctrl+O` keep opening a whole pair: the two are different actions.

## Alternatives rejected

- **Load the pair again through `LoadAsync` with one path changed.** Two probes and two decodes
  for one new file: 2 s and 88 MiB more on the real pair, for a bitmap already in memory.
- **Keep the kept image in its own mode when the new file cannot show it.** A pair half equalised
  and half linear would compare nothing; the mode is the pair's (R-110), so the one kept file is
  decoded again, the cost of a mode switch on one file.
- **Always show the new pair at fit.** A change is made to look at the same place in another
  variant; the fit is kept for a canvas that changed size, where the old offsets mean nothing.
- **Buttons floating over the surface, one on each side of the divider.** The surface is the
  thing being judged, a dark field where a button would sit over the pixels; the toolbar already
  names each image, and the button beside the name says which it changes.
- **A key for each side.** None asked for; the two buttons are enough, and the keyboard's letters
  are kept for what the surface does.

## Consequences

- R-009 (the loader) and R-065 (the buttons and what follows) say it; R-008, R-060 and R-063 name
  the buttons. `ImagePairLoaderTests.R009_*` check the one probe and the one decode, the question
  with the kept image on its side, and the second decode of the kept file when the mode falls to
  `Linear`; `MainWindowTests.R065_*` check that the other image's bitmap is the same object after
  the change, the divider kept, and the dialog asked once, for the side clicked, in the folder of
  the file replaced.
- `IFileDialogService` gains `PickImage`, one dialog for one side; every fake in the tests
  implements it.
- The outgoing image is released only if nothing holds the outgoing pair when `ShowPair` collects
  (ADR 0040): a local of `ChangeImageAsync` holding the pair is hoisted into the method's state
  machine and lives through `ShowPair`. Measured live on 2026-10-30 on the real pair: with such a
  local, 552.9 MiB before the release and 552.6 after, a 92 MiB bitmap outgoing; with the pair read
  in `ReplaceShownAsync` and only the canvas's size kept, 508.2 MiB before and 418.7 after, and
  504.3 before and 419.1 after, the outgoing buffers (88 and 82 MiB) given back.
- The same local existed in `ToggleModeAsync` since ADR 0040, so a switch of display mode freed
  nothing at its release either, the process carrying two pairs until the next load, which is
  the very defect ADR 0040 set out to fix; its measurement of 2026-10-26 was of the open, which
  has no such local. Measured live on 2026-10-30 on the real FITS pair, `H` twice: 667.6 MiB
  before the release and 665.9 after, then 676.2 and 675.4, with 176 MiB of buffers outgoing;
  with the pair no longer held, 670.6 before and 403.8 after, then 672.8 and 407.9. Fixed in this
  task, the same way; R-081 and R-113 described the intended behaviour all along.
- The toolbar's left part is no longer one text block but the names with a button after each;
  the README's controls table and its "What the window shows" section say so. The two buttons
  carry the open-file glyph of Segoe MDL2 Assets before their text (`ChangeButton` in
  `Theme.xaml`), asked for by the owner on 2026-10-30 so they stand out from the names; the glyph
  is a font character, not an image, so it draws crisp at every display scale and needs no asset.
- The review of 2026-10-30 found that a horizontal `StackPanel`, which measures its children
  without a limit, let two long names push `Change B…` past the column's clip (two names of
  `de_printat_PRINT_preview_16bit.tif`'s length need about 1370 DIPs of toolbar). The label is
  now a `PairLabelPanel`, and the names' widths are `PairLabelLayout`'s, a Core record with its
  tests: the names give way, the longer first, never the buttons.
- The first live run showed every toolbar button's border hugging its text: `ToolButton`'s and
  `ToolToggleButton`'s templates never handed the style's `Padding` to their content. The
  `ContentPresenter` now takes it as its margin, so every toolbar and dialog button is 20 DIPs
  wider than before.
