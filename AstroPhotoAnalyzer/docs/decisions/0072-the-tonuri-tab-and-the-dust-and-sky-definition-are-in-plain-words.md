# 0072. The Tonuri tab and the definition of the dust and the sky are in plain words

Date: 2026-11-01
Status: accepted 2026-11-01

## Context

The second tab of the rule "Plain words" (ADR 0071). The Tonuri tab had the most text of the
seven: eight row descriptions, the histogram's heading and note, the simulation's legend and
exact-target line, the definition of the dust and the sky, the level-L* ladder, the preview
tooltip, the no-WCS sentence and a six-paragraph guide. The owner approved 25 rewrites on
2026-11-01, from an original / proposal list.

Three of those texts are not the window's alone. The simulation's legend, the exact-target line
(`ReportWriter.SimulationFooter`, `ExactLine`, `DustDmaxText`) and the definition of the dust
and the sky (`ReportWriter.DustDefinition`, built from `SimulationResult.DustDescription`,
`SkyDescription` and `DustClassDefinition`) are printed by the console report too, the last one
also inside every background recommendation's `Because` (R-092) and in the map window's legend.
R-044 fixed the definition's wording ("percentila 25 % a dalelor de praf", "mediana dalelor de
cer, in afara benzii (|b| peste 10 grade)", "clase relative la cerul modelat"). The owner was
told that and chose to change it as well, so that no sentence of the tab stays in the method's
words.

## Decision

Every text of the Tonuri tab is in plain words, in both languages (R-113, checked by
`R113_TheTonesTextsAreInPlainWords` on a scene without a WCS and on the band scene: no median,
percentile, interpolation, Otsu, modelled, astrometric, visibility threshold, weak separation or
subsample). In particular:

- The definition of the dust and the sky says what was measured, not how: "cele mai intunecate
  zone intinse din norii de praf (pragul sub care stau 25 % din dalele de praf; praful e 9.4 %
  din cadru, dalele cu 5 L* mai intunecate decat banda din jur sau decat cerul)" and "cerul din
  afara benzii Caii Lactee, la peste 10 grade de ea (valoarea din mijloc a dalelor de cer)"; on
  the sky path "valoarea din mijloc a dalelor de cer (dalele impartite prin comparatie cu
  cerul)" and "cel mai intunecat 1 % din dalele de cer (nu s-a gasit praf)". The owner's approved
  text for the band path had dropped the clause saying how a dust patch is told (the former
  `DustClassDefinition`); it is kept, in plain words, since a definition that names no criterion
  would leave the figure without its evidence (the rule "Every recommendation carries its
  evidence"). The same sentence is the console's, the `Because`'s and the map legend's, so the
  three stay one.
- The exact-target line reads as an answer: "fundalul la care praful ajunge exact pe negrul
  hartiei: 0.17 la Dmax 2.4 (0.15 daca Dmax e 2.3, 0.19 daca e 2.5); asa cum e imaginea, praful
  ramane deasupra negrului de la Dmax 2.1 in sus; cerul ajunge la L* 15 cand fundalul e 0.14".
- "pragul de vizibilitate" and "separare slaba" become what they mean: "L* 15, sub care ochiul
  abia mai deosebeste tonurile" and "ochiul abia deosebeste". The row names say which figure
  they hold: "Nivelul fundalului (canalul verde)", "L* median (cerul)", "Repere pe canalul verde
  (0..255)".
- "print-safe" and "dale" stay: the first is the mark in the simulation table's own column, the
  second is the word on the map, and both are explained where they first appear.

The goldens `dark-sky`, `dark-sky-wcs`, `clean-sky-canvas` and `mono-png8`, in both languages,
were recaptured for these sentences alone; every figure in them is unchanged.

## Alternatives rejected

- **Keeping R-044's wording for the definition and rewriting the tab around it.** The
  definition is the one sentence under the simulation table that says what the verdict was
  measured on; left as it was, it would be the hardest sentence on the tab.
- **A plain window text and the old console text for the shared three.** They are composed once
  on purpose (R-113: "as the report writes it"); two wordings of one figure drift.

## Consequences

- R-043, R-044, R-092 and R-113 quote the new sentences; the assertions in
  `PrintStretchSimulatorTests`, `BackgroundRecommendationTests`, `OverlayWindowTests` and
  `ViewModelTests` that quoted the old ones name the new.
- The map window's dust legend changes in passing (it embeds `DustClassDefinition`); the rest of
  the map's and the preview window's texts are a task of their own, as are the parameter card's
  help texts and the Raport tab's recommendation texts.
