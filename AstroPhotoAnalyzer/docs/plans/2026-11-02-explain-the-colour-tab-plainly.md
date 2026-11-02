# Explain the Culoare tab in plain words

Plan for AstroPhotoAnalyzer, written on 2026-11-02. The owner approved in chat a list of 13
texts, original and proposal side by side ("Da, toate"): the fifth tab under the rule "Plain
words" (ADR 0071), with Appendix E of the text report, which prints the same figures.

## Steps

1. Branch `claude/Explain-the-Colour-tab-plainly` in its worktree.
2. `BackgroundView.xaml`: the table's heading. `BackgroundTabViewModel`: the four rows' names and
   descriptions, the zones' rows ("fata de cer"), the guide's five paragraphs; the Tonuri tab
   named where a Fundal tab was.
3. `EdgeRegion.PixelsNote` and `ReportWriter.AppendixBackground`: the note and the appendix's
   lines.
4. Tests: the note's assertion; `R116_TheColourTextsAreInPlainWords` (both languages, the dark
   scene and the mono sky, the appendix included); R-116 to implemented.
5. Goldens recaptured with `APA_UPDATE_GOLDEN=1`, their diffs read and their numbers counted.
6. R-061, R-112, R-116, `window.md`, ADR 0075, CHANGELOG.
7. Gate, commit; the merge is the owner's call.
