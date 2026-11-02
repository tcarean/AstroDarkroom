# Explain the Soft proof tab in plain words

Plan for AstroPhotoAnalyzer, written on 2026-11-02. The owner approved in chat a list of 18
texts, original and proposal side by side ("Da, toate"): the seventh and last tab under the rule
"Plain words" (ADR 0071), with its full-screen window.

## Steps

1. Branch `claude/Explain-the-Soft-proof-tab-plainly` in its worktree.
2. `ProofTexts.cs`: the missing profile, the profile's line, the paper's white and black, the
   legend, the three intents' help. `SoftProof.cs` and `GamutAnalyzer.cs`: the reasons a profile
   is refused.
3. `SoftProofControls.xaml` (the label before the intents, the box and its help, the help of
   Originalul, of the margin and of Ecran intreg), `SoftProofView.xaml` (the note under the
   proof), `SoftProofWindow.xaml` (the key hint).
4. Test: `R163_TheSoftProofTextsAreInPlainWords` (both languages, the core's sentences and the
   views' XAML texts).
5. R-161, R-162, R-163; the README's soft proofing section; ADR 0077; CHANGELOG.
6. The validation run (`Analysis/` and `Report/` changed), its result in `docs/validation.md`.
7. Gate, commit; the merge is the owner's call.
