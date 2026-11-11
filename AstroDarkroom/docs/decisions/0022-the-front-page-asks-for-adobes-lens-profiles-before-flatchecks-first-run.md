# 0022. The front page and the README ask for Adobe's lens profiles before FlatCheck's first run

Date: 2026-11-11
Status: accepted 2026-11-11
Supersedes: 0008 in part (Adobe's lens profiles called optional)

## Context

ADR 0008 had the front page list, among what the tools need, "optional for FlatCheck, Adobe's
lens profiles", and this project's README said the same. On 2026-11-11 the owner found no
instructions for installing the profiles in FlatCheck's manual in the wiki and said that without
them an important part of FlatCheck does not work; the manual gained a section, "Installing
Adobe's lens profiles", near its top, and the owner asked for the READMEs to say the same.
FlatCheck's ADR 0098 records what is lost without them and what was measured that day (the
converter downloads with no sign-in, about 1.8 GB).

## Decision

The front page's section is "For FlatCheck: Adobe's lens profiles": it asks for them before
FlatCheck's first run, says what FlatCheck's vignetting check loses without them, gives the
download's size and that it needs no Adobe account, and links the steps in FlatCheck's manual;
its "Download" section names them beside Siril. This project's README says the same in its
list of what the suite needs.

## Alternatives rejected

- **Keeping "optional" (ADR 0008).** It hid that one of FlatCheck's main checks rests on less
  reliable profiles without them.
- **The steps written out on the front page.** The manual has them, with how to check that
  FlatCheck found the profiles; the front page links them.

## Consequences

- The front page's anchor for that section changed from `#optional-for-flatcheck-adobes-lens-profiles`
  to `#for-flatcheck-adobes-lens-profiles`; nothing in the repository or the wiki linked it.
- The launcher's own prerequisites text in "Despre" (R-009, ADR 0007) still says "Optional, for
  FlatCheck"; changing it is a change of R-009, left to a decision of its own.
