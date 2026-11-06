# 0028. Every text for the user is in plain words, the ADU scale tab first

Date: 2026-11-02
Status: accepted 2026-11-02; the word obisnuit of the dark current superseded by 0035
Supersedes: 0015 in part (the words of the provenance and the row name gasit); 0016 in part (the words of the ratio to the stack and the row name gasit); 0023 in part (the words for frames that name no camera)

## Context

On 2026-11-02 the owner found the explanations of the window's tabs too complicated and asked
for two things. First, every tab rewritten in words anyone can follow, starting with
`Explicatii scara ADU`, with the original and the proposal of each text side by side, approved
before any edit. Second, every text written for users from now on, and every explanation given
to the owner in chat, in the same plain register, written into the rules and into a decision.
The owner asked the same of AstroPhotoAnalyzer and FlatCheck on 2026-11-01 (their ADRs 0071 and
0082).

The tab's texts used the method's own words: sigma, median, square root, quadrature,
quantisation, normalisation, deviation, offset, gain, raw ADU and `e-` for electrons. A reader
who does not know the method could not follow them. The owner approved the eighteen texts of
`docs/plans/2026-11-02-explain-the-adu-scale-tab-plainly.md` as proposed ("Da, toate").

## Decision

1. **Plain words for every text the user reads**, in the window and in the console. A text says
   what a figure means for the frames and the image, in short sentences and everyday words, and
   names no part of the method. A term a figure is known by stays only with its meaning in the
   same sentence: `intr-un pixel incap 34446 electroni (full well)`, `curentul de intuneric (ce
   aduna un pixel obisnuit din caldura senzorului)`. The figures stay those of the result and of
   `Thresholds.cs`. A rewrite is shown to the owner first, the original and the proposal side by
   side, and made only on the owner's yes. The same register holds in chat with the owner. The
   rule "Plain words" in `CLAUDE.md` carries this.
2. **One tab at a time, the ADU scale first.** This task rewrites `ScaleReport`'s rows and
   paragraphs, `CameraFacts.SourceWords` and the tab's lead in `ScaleView.xaml`, in both
   languages; the console prints the same rows and paragraphs. The other tabs follow, one task
   each, with the same approval.
3. **The words chosen.**
   - `ADU din fisier` (`ADU in the file`) replaces `ADU brut` (`raw ADU`).
   - The rows `cuantizare`, `zgomot pe cadru` and `gasit` become `pasul valorilor`,
     `zgomot pe un dark` and `ce s-a gasit` (`value step`, `noise on one dark`, `what was found`).
   - `media` and `mediana` stay only as the two ways Siril makes a stack.
   - The provenance reads `Cifre pentru <camera> de pe photonstophotos.net, scoase din
     masuratorile DxOMark, citite pe 2026-10-20; la senzorii mici metoda da prea multi electroni
     pe ADU.`
   - The ratio to the stack reads `si 0.24 din zgomotul stack-ului de lights (3.3 ADU)`, on
     check 2's rows too, so the two tabs say it the same way.
   - Frames that name no camera read `Fisierele nu spun ce aparat le-a facut`.
   - The reference position reads `pixelii verzi G1` when it is a green, its name alone otherwise.
   - The master-noise row of frames whose noise could not be measured reads `NEMASURAT: zgomotul
     cadrelor nu s-a putut masura, deci nici al masterului`. The first approved words, `cu un singur
     cadru nu exista master`, were wrong, as the old ones were: a single frame gets a master noise
     predicted from its own noise (R-024). The owner chose the new words in chat.
4. **R-081 guards it.** `R081_TheScaleSectionIsInPlainWords` reads every row and paragraph, in
   both languages, over scenes that reach the variants, and fails on a word of the method.

## Alternatives rejected

- **The terms with a glossary beside them.** The owner asked for texts anyone can read where
  they stand; a glossary sends the reader elsewhere.
- **The window only.** The console prints the same rows and paragraphs, and the report the
  window writes is the console's text; two wordings of one figure would drift apart.
- **One approval for every tab.** The owner asked to see each text; one tab per task keeps each
  list short enough to read.
- **`putin prea multi` (slightly too many), as approved.** The source says only that its method
  overestimates the gain on small sensors, not by how little, so the text says `prea multi`; the
  owner was told.

## Consequences

- R-080 quotes the new words and R-081 is new. The six goldens that carry the section were
  recaptured: every changed line lies in the section and every figure is the same. The wording
  drops two repeated figures (the reference settings in `1 ADU-eq`, the count in `sqrt(N)`),
  writes the step twice (`din 1 in 1`, in the row and in the first paragraph) and adds one fixed
  figure (`de 4 ori mai multe cadre il injumatatesc`).
- ADRs 0015, 0016 and 0023 quote the old words; their Status lines name the part this ADR
  supersedes. The decisions they record stand.
- The README's prose and the other tabs keep the method's words until their own tasks.
