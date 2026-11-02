# Explain the parameter card in plain words

Plan for AstroPhotoAnalyzer, written on 2026-11-02. The owner approved in chat a list of 19
texts, original and proposal side by side ("Da, toate"): the parameter card, its help texts and
messages, and Appendix F of the report, after the tabs and windows of ADRs 0071 to 0078.

## Steps

1. Branch `claude/Explain-the-parameter-card-plainly` in its worktree.
2. `MainWindow.xaml`: the card's labels, the note under **Analizeaza**, the tooltip of
   **Scoate**. `ParameterFields.cs`: the profile line, the four rewritten help texts (their
   numbers from `Thresholds`), the out-of-limits and aspect messages.
3. `Parameters.cs`: the target background's message and the aspect's name in its check.
   `ReportWriter.cs`: Appendix F's two lines and the given Dmax's mark. A stale quote in
   `PrintStretchSimulator.DustDescription`'s comment.
4. Tests: the assertions that quote the old texts; `R026_TheCardTextsAreInPlainWords`; the
   goldens recaptured with `APA_UPDATE_GOLDEN=1`, their diffs read and their numbers counted.
5. R-020, R-023, R-024, R-026, R-027, R-155; the README; `docs/design/console.md`; ADR 0079;
   CHANGELOG.
6. The validation run (`Report/` and `Parameters.cs` changed), its result in
   `docs/validation.md`.
7. Gate, commit; the merge is the owner's call.
