# Explain the Tonuri tab in plain words

Plan for AstroPhotoAnalyzer, written on 2026-11-01, approved by the owner in chat as a list of
25 texts, original and proposal side by side ("Da, toate"), the second tab under the rule "Plain
words" (ADR 0071).

## Steps

1. Branch `claude/Explain-the-Tonuri-tab-plainly` in its worktree.
2. The texts, in both languages, numbers still interpolated:
   - `ResultTabs.cs`, `TonesTabViewModel`: the no-WCS sentence, the histogram's heading and
     note, the preview tooltip, the eight rows (three renamed), the six guide paragraphs.
   - `TonesView.xaml`: the simulation heading, the map button's tooltip, the ladder's note.
   - `TonalAnalyzer.LstarLadder`: "separare slaba" becomes "ochiul abia deosebeste".
   - `ReportWriter`: the recommended-row legend, `ExactLine`, `DustDmaxText`, `DustDefinition`.
   - `PrintStretchSimulator`: `DustClassDefinition`, `DustDescription`, `SkyDescription`, every
     variant; these reach the console report, the `Because` of R-092 and the map legend.
3. `R113_TheTonesTextsAreInPlainWords` in `ViewModelTests`, both languages, without a WCS and
   on the band scene; the assertions that quoted the old sentences updated.
4. The goldens recaptured with `APA_UPDATE_GOLDEN=1`, their diffs read: sentence lines only.
5. R-043, R-044, R-092, R-113 updated; ADR 0072; CHANGELOG.
6. Gate, commit, merge proposal. Next: the other five tabs, then the map and preview windows and
   the parameter card.
