---
name: release
description: Cut a release - pick the version from the CHANGELOG, run scripts/release.ps1 (verify, version bump in Directory.Build.props, CHANGELOG section, publish what kit.json lists), then propose the commit and the tag, which the owner runs on main. Use when the user says /release or asks to release, tag or publish a version.
---

# Release

The script does the mechanical part; you do the judgement and the asking. A release is cut on
`main`, in the main checkout, in a session the owner opened in the project folder there; commits
and tags on `main` are the owner's (the kit's ADRs 0021 and 0029): nothing here commits, tags or
pushes. The owner chooses the version number (the kit's rule "Version and year are the
owner's"); you propose one. Before step 1, read
`docs/skills.md` if it exists: its `## /release` section adds project-specific steps and says
where they go.

1. **Where we are.** `git branch --show-current`, `git status --porcelain -- .` and
   `git rev-parse HEAD`; note the hash. A release is cut from `main`, with a clean tree;
   `release.ps1` refuses any other branch. If the tree is dirty, stop: that work is a task of
   its own, `/wrap-up` first.
2. **What is being released.** Read `[Unreleased]` in `CHANGELOG.md`, the newest released
   heading (`## [X.Y.Z] - date`) and the current `<Version>` in `Directory.Build.props`. If
   `<Version>` is above the newest released heading, the owner has already bumped it: propose
   that number. Otherwise propose the next version by SemVer: a `Removed` or a
   behaviour-breaking `Changed` entry means major; anything under `Added` means minor; only
   `Fixed` and `Internal` means patch. State the reasoning in one line and ask the owner to
   confirm or override the number. The script refuses a version that is not above the newest
   released heading, is below `<Version>`, or is already tagged.
3. **Figures.** If `docs/validation.json` has cases, ask whether to run `/validate` first; a
   release is the moment a stale figure gets noticed.
4. **Dry run.** `scripts/release.ps1 -Version X.Y.Z -WhatIf` and paste its output: tag name,
   Unreleased content, what would change, and the components it would bundle. A component is
   another kit project an entry of `release.publish` publishes from (`../X/...`); each must be
   clean, and is taken from its folder as it is. Name to the owner every component listed with
   unreleased changes: the owner may release it first.
5. **Run it.** `scripts/release.ps1 -Version X.Y.Z`. It runs verify, edits
   `Directory.Build.props` and `CHANGELOG.md`, publishes each entry of `release.publish` to a
   staging folder and merges it into `artifacts/X.Y.Z/<output>/` with a hash check, writes
   `artifacts/X.Y.Z/release.json` (the version, the commit, each component's version, commit and
   unreleased flag) and, when `kit.json` sets `release.zip`, the zip in `artifacts/` with its
   `.sha256` beside it (the kit's ADR 0036). Paste the last lines. If it fails, report and stop;
   it changes nothing before verify passes. A collision (two entries publishing different bytes
   at one path) stops it after the version files changed: paste the collision lines and stop;
   the owner decides, usually an `output` subfolder for that entry in `kit.json`, then
   `scripts/release.ps1 -Version X.Y.Z -PublishOnly` redoes the publish alone.
6. **Check, propose, then ask.** `git rev-parse HEAD` must still be the hash of step 1, and
   `git status --porcelain -- .` must list only `CHANGELOG.md` and, unless the owner had already
   set the version, `Directory.Build.props`; each component's folder must still be clean (a
   publish that rewrote its lock files is the kit's ADR 0034). If HEAD moved or anything else
   changed, stop and report: another session or hand worked on `main` meanwhile. Otherwise show
   `git diff --stat`, then propose, on separate lines, the commit limited to the two files,
   `git commit -m "Release X.Y.Z" -- Directory.Build.props CHANGELOG.md`, and the tag command
   `git tag -a <tag> -m "Release X.Y.Z"` with the tag name the script printed. The owner runs
   them by hand; the commit hook refuses a commit of yours on `main`, and `git tag` asks the
   owner first. Wait. Never push; if the owner wants the tag pushed, say the command and let
   them decide.
7. **After.** Add nothing else to the CHANGELOG; the fresh `[Unreleased]` is already there.
   Note the artifact path and size in the chat, and the zip and its `.sha256` when there is
   one: uploading them anywhere is the owner's, by hand.
