# Explain the Index tab in plain words

Plan for the task branch `claude/Explain-the-darks-Index-tab-plainly`, worktree
`.claude/worktrees/Explain-the-darks-Index-tab-plainly`, from `main` at 28520c8b. Written on
2026-11-06. The owner picked the Index tab after Lights (ADRs 0034 and 0035) and approved its three
texts in one list ("Da, toate"). The version is not changed.

## Context

The Index tab shows the label with its copy button and a text under it, the INDEX rows (ADR 0030,
with ADR 0035's `valoarea medie`), and the partner with its explanation and three buttons. The
tab's own texts that named the method or read oddly are three: `WindowTexts.IndexLead`, and in
`IndexView.xaml` the partner's explanation and the hint of `Analizeaza din nou`. The rest (the
titles, the copy button and its hint, the empty partner's text, the two other buttons, the status
lines) was already plain and stays.

## The list, approved as proposed ("Da, toate", 2026-11-06)

| # | Text | Before | After |
| --- | --- | --- | --- |
| 1 | under the label | `Indexul e un numar pentru cat de cald a rulat senzorul: mediana amplitudinii pixelilor de pe rangurile 5-20 pe megapixel ai masterului, normalizata la ISO 400 si 120 s. Eticheta e numele folderului plus idx<NN>: o copiezi si redenumesti folderul tu; programul nu scrie si nu redenumeste nimic in folderele cu poze.` | `Indexul e un numar pentru cat de cald a fost senzorul: valoarea medie a pixelilor fierbinti din master de pe locurile 5-20 pe fiecare megapixel, numarand de la cel mai fierbinte, socotita pentru ISO 400 si 120 s. Cu cat senzorul a fost mai cald, cu atat indexul e mai mare. Eticheta e numele folderului plus idx si indexul rotunjit: o copiezi si redenumesti tu folderul; programul nu scrie si nu redenumeste nimic in folderele cu poze.` |
| 2 | the partner's explanation | `Alt folder de dark-uri, o biblioteca sau alta sesiune: dark-urile sesiunii se compara cu masterul lui pe pixelii fierbinti in amandoua, in grade echivalente, iar lights-urile prin dark-uri. Se tine minte intre porniri si se aplica la urmatoarea analiza.` | `Partenerul e alt folder de dark-uri: o biblioteca sau alta sesiune. Programul compara dark-urile sesiunii cu masterul partenerului, pe pixelii fierbinti in amandoua, si spune cu cate grade sunt dark-urile sesiunii mai calde sau mai reci decat el. Lights-urile se compara cu partenerul prin dark-urile sesiunii. Daca sesiunea nu are dark-uri, se folosesc cele ale partenerului. Programul tine minte partenerul ales si dupa ce il inchizi, si il foloseste la urmatoarea analiza.` |
| 3 | `Analizeaza din nou`'s hint | `Ruleaza din nou folderul deschis, cu partenerul si setarile de acum` | `Analizeaza din nou folderul deschis, cu partenerul si setarile de acum` |

The English says the same, with the same figures, and without apostrophes in the XAML pairs.

## Steps

1. Requirements: R-087, new.
2. Tests: `R087_...` renders the tab with a partner in both languages, with the word list of
   R-083 to R-086, which takes `rang` / `rank` and `echivalent` / `equivalent`.
3. Code: `WindowTexts.cs`, `IndexView.xaml`; window texts only, so no golden changes and no
   `/validate`.
4. Gate, ADR 0036, CHANGELOG, the design note, commit, the review offer, the merge proposal.
