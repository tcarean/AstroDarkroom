# 0075. A lensfun folder that cannot be listed is left out whole, with a Warn line, and the list says it could not be read

Date: 2026-10-30
Status: accepted 2026-10-30

## Context

`ProfileLocator.Locate` decided whether lensfun was present with one unguarded enumeration of
the `lensfun/` folder beside the executable. A folder that exists but cannot be listed (a deny
rule, broken permissions after a copy) threw there, and because one `Sources(o)` serves every
console mode that locates the profiles, the structure mode with `--raw` or `--lens`, `--lenses`
and `--synthetic` (`--against` compares two flats and locates none) printed `ERROR: Access to
the path '...\lensfun' is denied.` and exited 1; in the
window the shared `ProfileCatalog` threw the same into every Structure run with a raw in
`flats/` ("The analysis failed", no five tests, no card) and into the Sintetic prefill. The
Adobe roots had been made tolerant of this on 2026-10-27 (ADR 0066's dated correction) and
2026-10-30 (the review of the task that says when Adobe's profiles are missing, R-107's root
test): a root that cannot be listed is lost whole, with one Warn line, and the search goes on. ADR 0028's consequence, "without the folder every mode runs", did not hold for a folder
that is there and unreadable.

Measured 2026-10-30 in the task worktree: two tests with `UnlistableFolder` (a deny rule on
listing) failed on `Locate` and on the Structure tab's run; `flatcheck --lenses --profiles
<dir>` with the deny rule on `<dir>\lensfun` printed the ERROR line and exited 1, and listed 9
lenses with exit 0 once the rule was removed. Three options went to the owner with a
recommendation; the owner chose the third the same day.

## Decision

- `Locate` wraps the check and the read of the lensfun folder in one `try`: on any exception
  the folder is left out whole, a Warn line names it (`the lensfun folder <dir> could not be
  listed; its lenses are left out`), `LensfunPresent` is false and a new flag,
  `ProfileSources.LensfunUnreadable`, is true. The version file is not read either, though a
  file inside such a folder can still be opened by name (a deny on listing does not deny
  traverse, measured 2026-10-30 in the task's review): the folder is left out whole,
  `LensfunVersion` null, so the Sintetic tab's panel shows no version for it. The search goes
  on with the Adobe files and every mode runs as it would without the folder. In the window
  that is a load that succeeded, kept by `ProfileCatalog` for the life of the window: a
  permission fixed afterwards needs a restart, as installing Adobe's profiles does (R-113).
- The one line that speaks of the folder, `SyntheticReport.SourcesSummary`, printed by
  `--lenses` and shown on the Sintetic tab, ends with ` - the folder could not be read` /
  ` - folderul nu a putut fi citit` in that state, where a missing or empty folder keeps
  ` - the folder is missing or empty`. The log's Info line says `lensfun unreadable`.
- Nothing else changes: the Structure card never names lensfun when an Adobe file was found,
  and R-113's first sentence already covers a search with no Adobe file and no lensfun lens.
  The goldens run with a readable lensfun folder, so none moves.

## Alternatives rejected

- **Absent, exactly as an Adobe root (option 1).** The same `try` and Warn line, with no new
  flag: the list would then say "the folder is missing or empty" about a folder that is there,
  and send the user to copy `lensfun/` again instead of to the permission and the log. The
  folder ships with the tool, so an unreadable one is a broken install, worth one true sentence.
- **Present but nothing read (option 2).** `LensfunPresent` left to `Directory.Exists`, as for
  a folder whose files all fail to parse: the line reads "0 lenses from <dir>" with no suffix.
  Not false, but silent, and the field's meaning ("holds any .xml file") would drift.
- **A paragraph on the Structure card.** The card keys on counts (R-113) and says nothing of
  lensfun when Adobe's files are there; a sentence for a permission problem would sit above a
  comparison that worked.

## Consequences

- `R115_AnUnlistableLensfunFolderIsLeftOutWithAWarnLine_NotAFailedSearch`,
  `R115_SourcesSummary_TellsAnUnreadableLensfunFolderFromAMissingOne`,
  `R115_Lenses_AnUnlistableLensfunFolderIsSaidSo_AndTheListStillComes` and the window's
  `R115_AnUnlistableLensfunFolderDoesNotFailTheStructureRun` pin this; the first and the last
  two use `UnlistableFolder` and return on another OS.
- `ProfileSources` gains a constructor parameter; the three test call sites that build one by
  hand pass `false`. The mechanics are in `docs/design/lens-profiles.md` (section 5) and the Warn line
  in `docs/design/logging.md`'s `ProfileLocator` row.
