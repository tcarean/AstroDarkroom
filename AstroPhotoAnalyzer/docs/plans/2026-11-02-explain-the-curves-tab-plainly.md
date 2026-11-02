# Explain the Editare Curbe tab in plain words

Plan for AstroPhotoAnalyzer, written on 2026-11-02. The owner approved in chat a list of 20
texts, original and proposal side by side ("Da, toate"): the sixth tab under the rule "Plain
words" (ADR 0071), with the curve window and the `HISTORY` lines of the saved FITS. One of the
texts is the sentence V-012 records; approving it approved the recorded figure's change.

## Steps

1. Branch `claude/Explain-the-Curves-tab-plainly` in its worktree.
2. `CurveTexts.cs`: where the curve's points come from, the evidence lines, the two paper checks,
   the gamut lines and note, the reasons no curve is suggested, the curve window's caption and
   note, the save's messages, the `HISTORY` text (the kind of curve and the Siril tool on two
   cards, so that each fits its 72 characters).
3. `CurvesView.xaml` (the Print tooltip, the note under the image) and `CurveWindow.xaml` (the
   chart's heading).
4. Tests: the changed assertions in `CurveTests` and `CurvesTabTests`;
   `R146_TheCurvesTextsAreInPlainWords` (both languages, five scenes, every message, each
   `HISTORY` line within 72 characters).
5. R-142, R-145, R-146; V-012's `curves.problem`; ADR 0076; CHANGELOG.
6. The validation run on the twelve files of the manifest, its result in `docs/validation.md`.
7. Gate, commit; the merge is the owner's call.
