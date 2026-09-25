# 0042. A machine-readable output carries the version and the copyright as its first two fields, not as banner lines

Date: 2026-09-25
Status: accepted 2026-09-25

## Context

Gap 3 of the docs-health audit of 2026-09-25. `docs/requirements.md` R-001 says every run prints
`flatcheck <version>` as its first line and `Copyright (C) 2026 Tudor Carean` as its second, in
every mode, for `--help` and on a failed run; there is no `--version` flag. The two reasons are in
`docs/design/process.md`: under "Versioning", "a pasted report always says which version produced
it", and under "Licensing", the copyright is on line 2 for the "same reason as the banner: a pasted
report says on its own whose tool produced it". The GPLv3 §5(d) obligation (a work with
interactive user interfaces displays Appropriate Legal Notices: copyright, no warranty, how to view
the licence) is met by `--help`, which carries `RunReport.WarrantyLines` and the pointer to
`LICENSE` (R-005), and by the window's "Despre" (ADR 0040); the banner and the copyright line are
the project's own choice, not the licence's demand. ADR 0040 also cites "line 2 of every report"
as the runtime display of copyright notices that brings LGPL-2.1 §6 into play for LibRaw.

Four things pin R-001 today. `RunReport.WriteBanner` writes the two lines to the `TextWriter` it is
given, which for the console is stdout, and the window's saved `Rezultat testare.txt` opens on the
same two lines. `VersionTests` (`R001_EveryModePrintsTheBannerOnTheFirstLine`,
`R001_EveryModePrintsTheCopyrightOnTheSecondLine`) read stdout only and assert lines 1 and 2 for
every mode, `--help` and two failures. Every golden file in `FlatCheck.Tests/Golden` starts with
`flatcheck <VERSIUNE>` and the copyright verbatim; `GoldenOutputTests.RunCli` captures stdout, then
appends `[stderr]` plus stderr, then scrubs the version with a regex anchored to a whole line
(`^flatcheck \d+\.\d+\.\d+\r?$`). `crosscheck.py` concatenates stdout and stderr, scrubs the same
line with the same anchored pattern in `norm()`, finds the version with `version_of()` on any line,
and checks version agreement once through `--help`.

Any machine-readable output (a `--json` option was the audit's example; nothing is planned or
requested) cannot start with two prose lines and stay parseable, so R-001 as written forbids it,
and no document says which rule wins. This ADR decides the rule now, so that the day someone asks
for such an output the requirement is already settled and no test or golden has to be argued
about under time pressure.

## Decision

If a machine-readable output is ever added, it carries the same two strings as its first two
fields, in this order, before any result: the version as `flatcheck <version>` and the copyright
line, both from `VersionInfo`; it prints no banner lines to stdout and nothing to stderr on
success. R-001 keeps its rule for every human-readable mode, `--help` and failed runs, and gains
one sentence stating this. Nothing is implemented by this ADR: no mode, no test, no golden.

## Alternatives rejected

- **(a) Banner and copyright to stderr when the output is machine-readable; stdout stays pure.**
  R-001 would say "on stdout, or on stderr in a machine-readable mode"; the two `R001_` tests read
  stdout only, so a sibling reading stderr would be needed. A golden of such a mode would show
  the two lines at its end under `[stderr]`, since `RunCli` appends stderr after stdout, and the
  anchored scrub would still catch them. `crosscheck.py` would be unaffected: it concatenates the
  two streams and matches the banner on any line. The deciding fact is the GPL and versioning
  reasoning: the document a user saves or pastes (the redirected stdout) would carry neither
  version nor copyright, which is exactly the "pasted report says on its own" reason R-001 exists
  for; the notices would be shown only on a terminal nobody keeps.
- **(b) Version and copyright embedded as fields inside the machine-readable document.** Chosen.
  R-001 gains one sentence; the `R001_` tests stay as they are and a new mode gets a test on its
  first two fields. Existing goldens are untouched; a golden of the new mode would carry the
  version inside a field, so `ScrubVersion` and `crosscheck.py`'s `norm()` each need one more
  pattern (the anchored one would not match), or the golden moves at every bump, which ADR 0039
  rejected. `crosscheck.py` gets a scenario only if `flatcheck.py` grows the same mode (rule 16
  already exempts three modes). The reasoning of `process.md` holds unchanged: the artefact
  identifies its producer and its version by itself, and ADR 0040's "displays copyright notices
  at runtime" stays true for every mode.
- **(c) R-001 gets an explicit exception for machine-readable modes; the notice obligation is met
  by `--help` alone.** R-001 would read "except in a machine-readable mode, which prints neither
  line". Existing tests and goldens untouched; a golden of the new mode would simply lack the
  placeholder line, and `crosscheck.py` would not notice, since `check_versions()` uses `--help`.
  Legally sufficient: §5(d) speaks of interactive interfaces, and `--help` and "Despre" already
  carry the full Appropriate Legal Notices. Rejected because it abandons the project's own reason
  for R-001 for precisely the output most likely to be saved and forwarded, and because ADR
  0040's LGPL-2.1 §6 sentence ("line 2 of every report") would need a qualifier.
- **(d) No machine-readable output is ever added; R-001 stands as written.** Nothing changes:
  tests, goldens, `crosscheck.py` and the notice reasoning stay exactly as they are. Rejected
  because it does not close the gap: a later ADR could still supersede it, so the question of
  which rule wins would come back the day the feature is asked for, with the same four documents
  to reconcile; deciding the shape now costs one sentence in R-001 and no code.

## Consequences

- R-001 states the rule for a machine-readable output in one sentence; the status of the rest of
  the row does not change, and nothing implements the new sentence until a mode exists.
- The two `R001_` tests, every golden file, `ScrubVersion`, `crosscheck.py`'s `norm()` and
  `check_versions()` are untouched by this ADR.
- The day a machine-readable mode is added: its requirement row gets a concrete example with the
  two fields; a test named after that row asserts the two fields first; `ScrubVersion` and
  `norm()` each gain a pattern for the version inside a field, so the goldens and the cross-check
  keep the placeholder discipline of ADR 0039; `flatcheck.py` grows the same mode or rule 16's
  exemption list names it; `README.md` documents it in Romanian; `docs/design/process.md`
  ("Versioning", "Licensing") gets one sentence each.
- `--help` and the window's "Despre" remain the only places that carry the full §5(d) notice
  (R-005, ADR 0040); this ADR changes nothing there.
- Guards today: `R001_EveryModePrintsTheBannerOnTheFirstLine`,
  `R001_EveryModePrintsTheCopyrightOnTheSecondLine`, `GoldenOutputTests`, `crosscheck.py`.
- Related: ADR 0039 (version source and scrubbing), ADR 0040 (GPL notice and "Despre").
