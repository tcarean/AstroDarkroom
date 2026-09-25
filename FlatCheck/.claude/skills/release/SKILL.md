---
name: release
description: Cut a release - pick the version from the CHANGELOG, run scripts/release.ps1 (verify, version bump in Directory.Build.props and flatcheck.py, CHANGELOG section, publish console and window), then propose the commit and the tag and ask who runs them. Use when the user says /release or asks to release, tag or publish a version.
---

# Release

The script does the mechanical part; you do the judgement and the asking. Nothing here commits,
tags or pushes without the owner's answer (rule 13). The owner chooses the version number
(CLAUDE.md: never bump on your own initiative); you propose one.

1. **Where we are.** `git branch --show-current` and `git status --porcelain -- .`. A release
   is cut from `main` (or the branch the owner names) with a clean tree. If the tree is dirty,
   stop: that work is a task of its own, `/wrap-up` first.
2. **What is being released.** Read `[Unreleased]` in `CHANGELOG.md` and the current
   `<Version>` in `Directory.Build.props`. Propose the next version by SemVer: a `Removed` or a
   behaviour-breaking `Changed` entry means major; anything under `Added` means minor; only
   `Fixed` and `Internal` means patch. State the reasoning in one line and ask the owner to
   confirm or override the number.
3. **Figures.** Ask whether to run `/validate` first; a release is the moment a stale figure on
   the real masters gets noticed. Ask whether `scripts/crosscheck.ps1` ran since the last
   formula change.
4. **Dry run.** `scripts/release.ps1 -Version X.Y.Z -WhatIf` and paste its output: tag name,
   Unreleased content, what would change.
5. **Run it.** `scripts/release.ps1 -Version X.Y.Z`. It runs verify, edits
   `Directory.Build.props`, `flatcheck.py` and `CHANGELOG.md`, publishes the console (single
   file) and the window app to `artifacts/X.Y.Z/`. Paste the last lines. If it fails, report
   and stop; it changes nothing before verify passes.
6. **Propose, then ask.** Show `git diff --stat`, then propose, on separate lines:
   the commit message `Release X.Y.Z`, and the tag command
   `git tag -a flatcheck-vX.Y.Z -m "Release X.Y.Z"`. Ask: does the owner run these, or should
   you? Wait. Never push; if the owner wants the tag pushed, say the command and let them decide.
7. **After.** Add nothing else to the CHANGELOG; the fresh `[Unreleased]` is already there.
   Note the artifact path and size in the chat. The `README.md` build section names the
   publish commands; `artifacts/` is what they produce.
