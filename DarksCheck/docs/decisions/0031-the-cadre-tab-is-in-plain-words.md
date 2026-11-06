# 0031. The Cadre tab is in plain words

Date: 2026-11-02
Status: accepted 2026-11-02; the word obisnuit of the noise guide line superseded by 0035
Supersedes: 0030 in part (the words of the no-frame messages)

## Context

ADR 0028 put every text the user reads in plain words, one tab at a time; the ADU scale tab and
the Rezumat (ADR 0030) are done. The owner picked the Cadre tab next. It shows the frames table,
the same the console prints as `TABEL PE CADRE`, with a hover hint on every column header, and
three charts beside it, each with a title, a sentence and its guide lines. Its words named the
method: `sigma`, `rezid.` and `amplit.` as column names, `zgomot alb`, `mediana seriei`, `outlier`
and `culoarea de atentie` in the hints and the charts. The owner approved the plan's list of
seventeen texts, shown with the texts of the Smida session, with "Aprobat", together with the
question left from the Rezumat: the analysis's messages when no frame of a kind can be read said
`nu s-a putut decoda`.

## Decision

1. **The rule of ADR 0028 holds**: what a figure means for the frames, in short sentences and
   everyday words; the figures as the result gives them.
2. **Three columns are renamed** in the console and the window alike: `sigma`, `rezid.` and
   `amplit.` become `zgomot`, `dif.` and `fierb.` (`noise`, `diff.` and `hot` in English). Each
   name ends in the column where its right-aligned figures end (the old names ended one column
   short, `>20` two, and the review moved them); the note beside the header says what every
   column means, `dif. = cat difera cadrul de master, ADU`.
3. **A frame's reason to discard says what was measured**: `de aruncat: pixelii fierbinti sunt la
   0.826 din master, sub 0.87, adica la peste 2.0 grade sub platou (1.10)`; a skipped frame reads
   `sarit (peste 60 de cadre se citeste doar unul din 2)`, in the lights table too.
4. **The charts say what their lines mean**: `CAT DIFERA FIECARE CADRU DE MASTER (ADU)` with the
   line `de 1.5 ori zgomotul obisnuit`, `BENZI ALEATOARE (1 = FARA BENZI)` with `fara benzi`, the
   warming chart's line at 1 `masterul`; the frames to discard show "in galben", the colour they
   have, not "in the attention colour".
5. **Read, not decoded**: the messages when no dark, bias, light or partner dark can be read say
   `nu s-a putut citi` (`could not be read`), the words the Rezumat's warnings took in ADR 0030.
6. **After the review**, as the owner approved: the plateau is "the steady temperature of the
   series", with no direction, in the note, the chart and the hint, since a series may climb,
   come down or stay; a left or right side is `latura din stanga` / `latura din dreapta` in every
   side text, the Rezumat's and the ADU tab's too; the `ora` hint says a FITS from Siril carries
   the UTC time; the hints of `zgomot` and `>20` name the series' pixels (`pe pixelii verzi G1`),
   the core's words, since the note that names them is often out of view; a frame whose hot
   pixels do not show at all gets its reason, `de aruncat: pixelii fierbinti ai masterului nu se
   vad in acest cadru`.
7. **R-083 guards it**: `R083_...` reads the core's texts over every variant the fixtures reach
   and renders the Cadre tab in both languages, with one list of the method's words that the
   two test projects share (`tests/DarksCheck.Tests/PlainWords.cs`, linked into the window's).

## Alternatives rejected

- **Keeping the short method names and explaining them in the hints only.** The name is what
  the eye reads first, and the console has no hint.
- **Wider columns for longer names** (`zgomot fata de master`). Every row of the table would
  move, in the console and in the window's grid, for words the note already gives.

## Consequences

- R-025, R-027, R-028, R-053, R-071 and R-110 quote the new words; R-083 is new.
- The console's `--help`, the window's `THREADS` hint, the start screen (`sunt decodate si
  masurate`) and the progress lines (`decodare dark-uri: 3 / 20`) still say it: they are the
  console's, the top bar's and the start screen's texts, not the Cadre tab's.
