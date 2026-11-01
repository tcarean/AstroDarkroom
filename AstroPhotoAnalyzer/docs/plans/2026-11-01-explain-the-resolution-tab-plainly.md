# Explain the Rezolutie tab in plain words

Plan for AstroPhotoAnalyzer, written on 2026-11-01, approved by the owner in chat as a list of
fourteen texts, original and proposal side by side, with one change: the stars' width is written
"latimea stelelor (FWHM)" wherever it is named.

## Context

The owner found the Rezolutie tab's explanatory texts too hard for someone who does not know the
method, asked for plain words on every tab, one tab at a time, and for the same register in every
future text and in chat. This is the first tab; it also records the rule and the decision.

## Steps

1. Branch `claude/Explain-the-Resolution-tab-plainly` in its worktree.
2. The texts, in both languages, numbers still interpolated from `Thresholds`:
   - `ResultTabs.cs`, `ResolutionTabViewModel`: the five descriptions, the row names
     "Latimea stelelor (FWHM)" and "Latimea stelelor (FWHM) in <zone>", the too-few-stars note,
     the dpi scale's header.
   - `ResolutionView.xaml`: the three notes under the tables.
   - `PrintSizeAnalyzer.DpiLadder`: three meanings (the 300 dpi myth, the printers' own
     resolution, arm's length).
   - `ReportWriter.SizeTableFooter`: the legend without stars and the two lines at the natural
     distance, shared with the console; the column header stays.
3. `R115_TheResolutionTextsAreInPlainWords` in `ViewModelTests`, both languages, with and
   without stars: no method word, "(FWHM)" present; R-115 to `implemented` with the clause.
4. The goldens `clean-sky-canvas`, `dark-sky-wcs`, `mono-png8` (both languages) recaptured with
   `APA_UPDATE_GOLDEN=1`, their diffs read: legend lines only.
5. The rule "Plain words" in `CLAUDE.md`, ADR 0071, `docs/design/window.md`'s row, CHANGELOG.
6. Gate, commit, merge proposal. The other six tabs follow, one task each, same approval step.
