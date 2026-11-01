# 0017. Each row says what its tool does, in at most two lines

Date: 2026-11-01
Status: accepted 2026-11-01
Supersedes: 0010 in part (the description of CopyWcs)

## Context

Each row described its tool in a few words, such as "Checks the flats of a lens" or "Checks a
final image before printing", which tell someone who does not know the tools little about what
each one does. On 2026-11-01 the owner asked for descriptions that give such a reader an idea of
each tool, in two lines at most, and said what AstroPhotoAnalyzer's should name: it simulates
the paper, checks the blacks against the paper's limits, helps with the curves and soft-proofs
through an ICC profile. The description is a `Muted` `TextBlock` (Segoe UI 12, wrapping) in the
middle column of the row's card, which the window's fixed width leaves 416 px wide.

## Decision

Each description says what its tool checks or does, and what for, in at most two lines of that
column, in English and in Romanian: the six pairs the owner approved on 2026-11-01, which R-001
quotes in Romanian and `R006_TheCatalogue_DescribesEveryAppInEnglishToo` in English. Like the
old ones, each starts with a verb; they use the tools' own words (`scurgeri de lumina`,
`aliniere polara`, `negrul hartiei`) and end with a full stop.

## Alternatives rejected

- **One line, as before.** About 78 characters fit in the column, too few to say what a tool
  is for beyond the kind of frame it reads.
- **The long text in a tooltip.** A reader who does not know the tools has no reason to hover
  over a row, and the button's tooltip already carries the exe or the reason it is missing.
- **More than two lines.** The owner set two; each line makes every row 15 px taller.

## Consequences

The window, sized to its content, grows by one line per row: six lines of 15 px, from the
743 px of its log line of 2026-10-31 (at 96 DPI) to about 833 px, computed, not measured;
the work area of the owner's primary screen is 1040 px high. The column is the window's client
area, 664 px (the 680 px less the frame of a window that cannot be resized, measured on the
screenshot of 2026-10-25, whose card spans 624 px), less the list's margins (40), the card's
border and padding (34), the icon's column (54), the text's right margin (16) and the button's
minimum width (104). No test guards the two lines, since the test project has no WPF: a changed
description, or a change of any of those widths, is measured again.

Measured on 2026-11-01 with WPF's own text layout (`FormattedText` in Windows PowerShell,
Segoe UI 12, `Display` formatting as the window sets it, 96 DPI): each text's line count at
416 px and the narrowest width that keeps it on two lines (the texts cut short here); the
widest needs 397 px.

```
149 ch  2 lines  2 lines from 380 px  | Checks the darks of a session or a library: light leaks, ...
156 ch  2 lines  2 lines from 397 px  | Verifica dark-urile unei sesiuni sau biblioteci: scurgeri ...
155 ch  2 lines  2 lines from 388 px  | Checks the flats of a lens: exposure, symmetry, vignetting ...
155 ch  2 lines  2 lines from 386 px  | Verifica flat-urile unui obiectiv: expunerea, simetria, ...
144 ch  2 lines  2 lines from 377 px  | Checks the lights of a night: which frames to leave out ...
153 ch  2 lines  2 lines from 384 px  | Verifica light-urile unei nopti: ce cadre sa lasi afara ...
143 ch  2 lines  2 lines from 373 px  | Compares two versions of one image with a before/after ...
142 ch  2 lines  2 lines from 363 px  | Compara doua variante ale unei imagini cu un slider ...
146 ch  2 lines  2 lines from 380 px  | Copies the plate solve of a FITS back into its processed ...
142 ch  2 lines  2 lines from 359 px  | Copiaza plate solve-ul unui FITS inapoi in varianta lui ...
155 ch  2 lines  2 lines from 392 px  | Prepares an image for print: simulates the paper, checks ...
158 ch  2 lines  2 lines from 393 px  | Pregateste o imagine pentru print: simuleaza hartia, ...
```
