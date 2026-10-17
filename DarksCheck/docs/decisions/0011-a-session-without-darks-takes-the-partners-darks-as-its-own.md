# 0011. A session without darks takes the partner's darks as its own

Date: 2026-10-16
Status: accepted 2026-10-16

## Context

The tool's purpose is to match a library of darks to a session's lights (ADR 0004). ADR 0002
made `darks\` required and refused a folder with `lights\` and no `darks\`, since such a folder
is a session whose root holds stacked results; ADR 0008 measured the lights only at the
session's own master and reached a partner through the darks' ratio. The owner's Veil session
of 2026-10-06 (87 lights of 120 s at ISO 1600, 30 biases) has no darks at all, and the
2026-10-10 library at the same settings is the master it would be calibrated with; it could
not be opened (ADR 0008's open question, the plan's follow-up 6, the owner's word of
2026-10-16: "vreau dupa sa implementam si cazul in care ai o sesiune cu lights fara darks").

## Decision

- **The partner's darks stand in for the session's**, in the one run the tool has. A folder
  with a `lights\` that holds frames and no `darks\` opens as a session with empty darks
  (`SessionFolder.Open(path, allowLightsOnly: true)`, used by the inventory and the run); the
  run then requires a partner (`--against`, the window's partner), opens it as a session or
  library of darks, decodes its darks into the stack and runs checks 2 to 8 on them, check 9
  with the session's lights against their median master, and INDEX from them. The session's
  biases, when it has any, still give a FITS stack its black and the read noise. Check 1's
  `potrivire` compares the lights with the partner darks' settings, so a library at another
  ISO or exposure is flagged where a session's own darks would have been.
- **The report says whose darks they are and drops what would compare the partner with
  itself**: the `=== DARK-URI (ale partenerului <folder>)` line, the `darks` row `(lipsa; se
  folosesc dark-urile partenerului <folder>)`, `expunere partener`, `interval darks: (lipsa)`,
  INDEX's `dark-uri (partener)` with the partner's label, and no `partener`,
  `dark-uri / partener`, `pixeli comuni` or `lights / partener` rows (`PartnerAnalysis` stays
  null; `DarkSeriesResult.DarksFromPartner` names the folder).
- **Without a partner the folder stays an error**, now saying what to give in both
  applications' words (`RunReport.LightsOnlyNeedsPartner`); the window's problem card carries
  `Alege partener...`, which chooses the partner and runs the same folder again. A folder with
  `biases\` or `flats\` but no `lights\` with frames, or an empty `lights\`, is refused as
  before. `--inventory` lists a lights-only session with its darks `(lipsa)`.

## Alternatives rejected

- **A second, shorter run** (check 9 and INDEX only, the lights measured at the partner's
  master): a second report shape and a second view-model path, and the library's own
  quality, which checks 2 to 8 give, unreported where the owner most wants it.
- **Loosening ADR 0002 to accept any folder without `darks\`**: the folder with `biases\` or
  `flats\` alone has nothing to measure, and a root holding `result.fit` must never become
  the darks.
- **Keeping `--against`'s partner rows** (darks over partner, the overlap) when the darks are
  the partner's: a ratio of 1.00 on every pixel says nothing and would read as a match.

## Consequences

R-002 refined, R-054 and R-077 added, R-063 amended; the golden `lights-only.txt`; V-007 runs
the Veil lights against the library and records what they show. The figures of check 9 are
the same the lights would give in a session holding those darks (R054's test proves it on
synthetic frames). The console's `--against` and the window's partner now serve two cases: a
session with darks compares them with the partner; one without takes the partner's.
