# Explain the Export tab in plain words

Plan for AstroPhotoAnalyzer, written on 2026-11-02. The owner approved in chat a list of 16
texts, original and proposal side by side ("Aprob toate"): the fourth tab under the rule "Plain
words" (ADR 0071), with Appendix C of the text report, which prints the same figures.

## Steps

1. Branch `claude/Explain-the-Export-tab-plainly` in its worktree.
2. `ExportView.xaml`: the two headings. `ExportTabViewModel`: the TIFF, rounding, shadow and
   plateau rows (names, notes, descriptions) and the JPEG guide's texts; the JPEG table's column
   titles and the guide's leads stay.
3. `ReportWriter.AppendixExport`: the TIFF, rounding, shadow and plateau lines.
4. Tests: the Export tab's assertions on the new words, `R114_TheExportTextsAreInPlainWords`
   (both languages, the dark scene and the mono sky, the appendix included).
5. Goldens recaptured with `APA_UPDATE_GOLDEN=1`, their diffs read and their numbers counted.
6. R-112, R-114, README, ADR 0074, CHANGELOG.
7. Gate, commit; the merge is the owner's call.
