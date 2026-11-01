# Explain the Raport tab in plain words

Plan for AstroPhotoAnalyzer, written on 2026-11-01. The owner approved in chat a list of 31
texts, original and proposal side by side ("Da, toate"): the third tab under the rule "Plain
words" (ADR 0071), in two parts.

## Part 1: what the tab shows directly

1. Branch `claude/Explain-the-Report-tab-plainly` in its worktree.
2. The texts, in both languages, every figure and threshold kept:
   - `ReportView.xaml`: the caption beside the overall verdict.
   - `VerdictText.Title`: section 3, "Culoarea fundalului si marginile cadrului".
   - `Recommendations.cs`: every text and "because" of the six sections.
   - `Fmt.RoCount`: the Romanian "de" before a count's noun.
3. Tests: the assertions that quoted the old sentences, `R112_TheReportTextsAreInPlainWords`
   (nine scenes, both languages), a test for `Fmt.RoCount`.
4. Goldens recaptured with `APA_UPDATE_GOLDEN=1`, their diffs read and their numbers counted.
5. README: the section's name and the report excerpt, taken again from a run on
   `de_printat.png`.
6. R-090 to R-096, R-112, the design note `recommendations-and-report.md`, ADR 0073, a dated
   correction in ADR 0072 for its example figures, CHANGELOG.
7. Gate, commit.

## Part 2: the text report's header and appendices A, B, D

Approved by the owner the same day (12 texts, "Sunt ok propunerile"), with the merge asked for
in the same message. The appendices C, E and F go with the Export tab, the Culoare tab and the
parameter card, whose words they repeat.

1. `ReportWriter`: the header's profile and distance, Appendix A (with a mono variant), the
   title of Appendix B, its class label and cloud lines, Appendix D's stars and zones; `Clouds`
   and `Stars` for a count with its noun.
2. `PrintStretchSimulator.ClassFractions`: the sky path's thresholds in plain words.
3. The Tonuri tab's two labels that named the green channel, on a mono image: the failing
   assertion first, in `R113_TheTonesTextsAreInPlainWords`.
4. Goldens, R-044, R-100, R-112, R-113, the design note, ADR 0073, CHANGELOG; gate; commit; the
   merge into main.
