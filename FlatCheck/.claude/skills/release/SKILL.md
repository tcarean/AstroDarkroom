---
name: release
description: Cut a release - pick the version from the CHANGELOG, run scripts/release.ps1 (verify, version bump in Directory.Build.props, CHANGELOG section, publish what kit.json lists), then propose the commit and the tag and ask who runs them. Use when the user says /release or asks to release, tag or publish a version.
---

# Release

The script does the mechanical part; you do the judgement and the asking. Nothing here commits,
tags or pushes without the owner's answer (rule 13). The owner chooses the version number; you
propose one. Before step 1, read `docs/skills.md` if it exists: its `## /release` section adds
project-specific steps and says where they go.

1. **Where we are.** `git branch --show-current` and `git status --porcelain -- .`. A release
   is cut from `main` (or the branch the owner names) with a clean tree. If the tree is dirty,
   stop: that work is a task of its own, `/wrap-up` first.
2. **What is being released.** Read `[Unreleased]` in `CHANGELOG.md` and the current
   `<Version>` in `Directory.Build.props`. Propose the next version by SemVer: a `Removed` or a
   behaviour-breaking `Changed` entry means major; anything under `Added` means minor; only
   `Fixed` and `Internal` means patch. State the reasoning in one line and ask the owner to
   confirm or override the number.
3. **Figures.** If `docs/validation.json` has cases, ask whether to run `/validate` first; a
   release is the moment a stale figure gets noticed.
4. **Dry run.** `scripts/release.ps1 -Version X.Y.Z -WhatIf` and paste its output: tag name,
   Unreleased content, what would change.
5. **Run it.** `scripts/release.ps1 -Version X.Y.Z`. It runs verify, edits
   `Directory.Build.props` and `CHANGELOG.md`, publishes what `kit.json` lists under
   `release.publish` to `artifacts/X.Y.Z/`. Paste the last lines. If it fails, report and stop;
   it changes nothing before verify passes.
6. **Propose, then ask.** Show `git diff --stat`, then propose, on separate lines:
   the commit message `Release X.Y.Z`, and the tag command
   `git tag -a <tag> -m "Release X.Y.Z"` with the tag name the script printed. Ask: does the
   owner run these, or should you? Wait. Never push; if the owner wants the tag pushed, say
   the command and let them decide.
7. **After.** Add nothing else to the CHANGELOG; the fresh `[Unreleased]` is already there.
   Note the artifact path and size in the chat.
