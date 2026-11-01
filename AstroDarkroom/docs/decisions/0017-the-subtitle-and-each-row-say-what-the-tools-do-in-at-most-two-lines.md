# 0017. The subtitle and each row say what the tools do, in at most two lines

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
middle column of the row's card, which the window's fixed width leaves 416 px wide. The owner
then asked for the subtitle under the banner too, which said only "The astrophotography tools,
in the order they are used. Open one from its row."; it is a `Muted` `TextBlock` of the top bar,
568 px wide beside the logo.

## Decision

Each description says what its tool checks or does, and what for, in at most two lines of that
column, in English and in Romanian: the six pairs the owner approved on 2026-11-01, which R-001
quotes in Romanian and `R006_TheCatalogue_DescribesEveryAppInEnglishToo` in English. Like the
old ones, each starts with a verb; they use the tools' own words (`scurgeri de lumina`,
`aliniere polara`, `negrul hartiei`) and end with a full stop.

The subtitle says what the suite is, in two sentences with a line break between them, one per
line: "From the night's frames to the print: six tools for DSLR astrophotography, in the order
they are used." / "Each checks or prepares one step; open it from its row." ("De la cadrele
noptii la print: sase unelte pentru astrofoto cu DSLR, in ordinea in care se folosesc." /
"Fiecare verifica sau pregateste un pas; deschide-o din randul ei."), the variant the owner
chose on 2026-11-01.

## Alternatives rejected

- **One line, as before.** About 78 characters fit in the column, too few to say what a tool
  is for beyond the kind of frame it reads.
- **The long text in a tooltip.** A reader who does not know the tools has no reason to hover
  over a row, and the button's tooltip already carries the exe or the reason it is missing.
- **More than two lines.** The owner set two; each line makes every row 15 px taller.
- **A subtitle that lists the steps** (check the darks, flats and lights, compare two
  processings, put the plate solve back, prepare the print). It repeats the six rows right
  under it, and its two lines break in the middle of the list; the owner chose the overview.

## Consequences

The window, sized to its content, grows by one line per row: from the 743 px of its log line
of 2026-10-31 to 833 px, measured on 2026-11-01 at 96 DPI in the window started from a mock
suite folder (the new build, empty tool executables, the `release.json` of 0.9.0), where UI
Automation gives each description 416 × 30 px, two lines of 15 px, in both languages; the
work area of the owner's primary screen is 1040 px high. The column is the window's client
area, 664 px (the 680 px less the frame of a window that cannot be resized, measured on the
screenshot of 2026-10-25, whose card spans 624 px), less the list's margins (40), the card's
border and padding (34), the icon's column (54), the text's right margin (16) and the button's
minimum width (104). WPF breaks a line after a hyphen too, so a hyphenated Romanian word must
not end a line: in that window LightsCheck's first text broke as `dithering-` / `ul`, and its
list was reordered, `deriva monturii, dithering-ul` (`mount drift, dithering` in English, so
that the two lists stay alike). No test guards the two lines or the breaks, since the test
project has no WPF: a changed description, or a change of any of those widths, is measured
again, and so is where it breaks.

The subtitle takes one line more than before. Each of its sentences is narrower than its column
(the top bar's 624 px less the logo's 40 px and its 16 px margin), so the line break gives
exactly two lines, each sentence whole; the window grows by another 15 px, to 848 px, computed.
Its text is composed in `MainWindow`, which has no test project (ADR 0001), so it too is
measured again when it changes. One line of each sentence, measured the same way:

```
 526 px  From the night's frames to the print: six tools for DSLR astrophotography, in the order they are used.
 287 px  Each checks or prepares one step; open it from its row.
 500 px  De la cadrele noptii la print: sase unelte pentru astrofoto cu DSLR, in ordinea in care se folosesc.
 332 px  Fiecare verifica sau pregateste un pas; deschide-o din randul ei.
```

Measured on 2026-11-01 with WPF's own text layout (`FormattedText` in Windows PowerShell,
Segoe UI 12, `Display` formatting as the window sets it, 96 DPI): each text's line count at
416 px and the narrowest width that keeps it on two lines (the texts cut short here); the
widest needs 397 px. Laid out at 416 px, the reordered texts included, every first line ends
at a space, none at a hyphen.

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

The window, through UI Automation, before LightsCheck's list was reordered (the same words, on
two lines either way; the folder line's rows left out):

```
shot main-en.png: window 680x833 at (2540,104)
  416 x  30 px  Checks the darks of a session or a library: ligh...
  416 x  30 px  Checks the flats of a lens: exposure, symmetry, ...
  416 x  30 px  Checks the lights of a night: which frames to le...
  416 x  30 px  Compares two versions of one image with a before...
  416 x  30 px  Copies the plate solve of a FITS back into its p...
  416 x  30 px  Prepares an image for print: simulates the paper...
shot main-ro.png: window 680x833 at (2540,104)
  416 x  30 px  Verifica dark-urile unei sesiuni sau biblioteci:...
  416 x  30 px  Verifica flat-urile unui obiectiv: expunerea, si...
  416 x  30 px  Verifica light-urile unei nopti: ce cadre sa las...
  416 x  30 px  Compara doua variante ale unei imagini cu un sli...
  416 x  30 px  Copiaza plate solve-ul unui FITS inapoi in varia...
  416 x  30 px  Pregateste o imagine pentru print: simuleaza har...
```
