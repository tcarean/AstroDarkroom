# Explain the Rejected tab in plain words

## Context

The owner asked on 2026-11-02 for every LightsCheck tab in plain words, one tab at a time (ADR
0032, rule "Plain words"); Dithering, Summary, Frames and Quality are merged (last 959ebbb7). The
Respinse (Rejected) tab is the last: the line under its heading, four explanation paragraphs and
the note on the "Write the files..." button. The heading line is also the console's
`RESPINSE (...)` line, composed once by `SeriesReport.RejectedHeading`; it also reads `1 cadre
slabe, 1 necitite` today, Romanian plural for a count of one. The owner approved the six
proposals in chat on 2026-11-02, "in the console too" ("Da, si in consola"). The title
`RESPINSE` and the frames' reasons (R-079) stay.

## Branch, numbers

- Branch `claude/Explain-the-Rejected-tab-plainly`, worktree
  `LightsCheck/.claude/worktrees/Explain-the-Rejected-tab-plainly`, from `main` (959ebbb7).
- R-081 and ADR 0036 (main holds R-080 and ADR 0035; checked again before the merge).
- This plan saved as `docs/plans/2026-11-02-explain-the-rejected-tab-plainly.md`.

## The texts (Romanian as approved; English to the same meaning)

1. Heading line (`SeriesReport.RejectedHeading`, window and console): `2 cadre slabe; cele la
   limita raman in stiva, la alegerea ta`; one: `1 cadru slab`; with unread frames `1 cadru slab
   si 1 cadru necitit; ...` (`2 cadre necitite`) / `2 bad frames; the marginal ones stay in the
   stack, your choice`, `1 bad frame and 1 unread frame; ...`. Counts through the existing
   `SeriesReport.Count` helper (singular for one).
2. Paragraph 1: `Alungirea spune de cate ori sunt stelele mai lungi decat late, de obicei, intr-un cadru (1 = rotunde). De la 1.60 cadrul e slab si iese din stiva.` / `The elongation says how many times longer than wide the stars are, typically, in a frame (1 = round). From 1.60 the frame is bad and leaves the stack.`
3. Paragraph 2: `Intre 1.35 si 1.60 cadrul e la limita si ramane in stiva, daca nu hotarasti altfel. Cadrele la limita sunt pe randul 'la limita' din CADRE MISCATE, pe tabul Rezumat, iar pe tabul Cadre au clasa galbena.` / `Between 1.35 and 1.60 the frame is marginal and stays in the stack unless you decide otherwise. The marginal frames are on the 'marginal' row of TRAILED FRAMES, on the Summary tab, and on the Frames tab their class is yellow.`
4. Paragraph 3: `Dara e directia in care s-au intins stelele in timpul expunerii, data ca unghi fata de orizontala imaginii. Cand se stie care axa a imaginii e RA si stelele cadrului s-au intins toate cam in aceeasi directie, motivul spune si pe ce axa a monturii cade dara.` / `The trail is the direction the stars were stretched in during the exposure, given as an angle to the image's horizontal. When it is known which image axis is RA and the frame's stars were all stretched about the same way, the reason also says which mount axis the trail falls on.`
5. Paragraph 4 (bold): `O dara pe RA vine de la urmarire: eroarea periodica a monturii, o viteza de urmarire gresita sau jocul din angrenaje (backlash) pe RA. O dara pe DEC vine de la alinierea polara, de la vant sau de la o atingere a monturii. Una oblica vine de la amandoua sau de la o lovitura.` / `A trail on RA comes from the tracking: the mount's periodic error, a wrong tracking rate or the play in the gears (backlash) on RA. A trail on DEC comes from the polar alignment, the wind or a touch on the mount. An oblique one comes from both, or from a knock.`
6. Button note (`RejectedView.xaml`): `Butonul "Scrie fisierele..." din bara de sus salveaza si numele acestor cadre intr-un fisier (lightscheck-rejected.txt), cate unul pe rand, ca sa le scoti din secventa in Siril.` / `The "Write the files..." button in the top bar also saves the names of these frames in a file (lightscheck-rejected.txt), one per line, so you can take them out of the sequence in Siril.`

The thresholds 1.35 and 1.60 stay read from `Th` (`RunReport.F(Th.ElongationMarginal, 2)`,
`Th.ElongationBad`).

## Steps

1. Worktree and branch; this plan under `docs/plans/`.
2. `docs/requirements.md`: R-081 (the clause, the heading's counts, an example); R-036 (c)
   quotes the bold paragraph's new opening `O dara pe RA vine de la ...`.
3. ADR 0036 "The Rejected tab is in plain words", citing ADR 0032; the heading's singular
   for one, the same in the console.
4. Tests first, failing: `R081_TheRejectedHeadingAndExplanationAreInPlainWords` (`SeriesTests`,
   both languages: `PlainWords` on the heading and the paragraphs, the heading's exact words on
   the pattern session (2 bad) and the classes session with an unreadable frame (1 bad, 1
   unread)), and `R081_TheRejectedTabUsesNoneOfTheMethodsWords` (`WindowLanguageTests`, the
   rendered `RejectedView`, both languages). `PlainWords` gains `coherence`.
5. The texts: `SeriesReport.RejectedHeading` (through `Count`), `RejectedExplanation`,
   `RejectedView.xaml`; their doc comments follow.
6. Old tests follow: `SeriesTests` (the bold paragraph `StartsWith("Dara pe RA")` becomes
   `O dara pe RA`, R-036); the English scans gain the new Romanian stems; goldens `series`,
   `series-classes` and their `en_` twins recaptured, the RESPINSE line only, numbers checked by
   the script.
7. Docs: `series-analysis.md` (the heading's words), `language.md` (every tab rewritten; glossary
   rows), README where it quotes them, CHANGELOG, the plan's "What deviated".
8. Build `--no-incremental -warnaserror`, render the Rejected tab in both languages and look,
   gate, `/validate` (`Report/` touched; V-008/V-011 as before), `docs/validation.md` history,
   commit, merge proposal.

## Verification

- Both R081 tests fail first, pass after; the whole gate green.
- Golden diffs: only the RESPINSE line of each of the four files; every number unchanged.
- The Rejected tab rendered in both languages and looked at.
- `/validate`: no figure moves.

## What deviated

- The README needed no change: its REJECTED and Rejected bullets describe the tab without
  quoting the heading line or the paragraphs, and both still hold.
- `docs/design/language.md` now says every tab is rewritten and gained two glossary rows (the
  marginal frames' rule, periodic error and backlash); `window.md` still holds as written.
- The window test failed first on `coerenta`, the core test on the heading's old words; the R-036
  test followed its requirement (`O dara pe RA`), as planned.
- Each of the four goldens changed on its RESPINSE line only, every number the same (script).
