# Requirements

The source of truth for behaviour. Code follows this file, not the reverse. A behaviour change is
first an edit here (status `draft`), then a test, then code (status `implemented`).

Rules for entries:

- IDs are `R-NNN`, assigned once, never reused. A dropped requirement keeps its ID with status `dropped`.
- Status is one of `draft`, `accepted`, `implemented`, `dropped`.
- Describe observable behaviour, with one concrete example. No adjectives.
- Tests carry the ID in their name: `R001_Catalogue_ListsTheFiveAppsInWorkflowOrder`.
- Error behaviour is a requirement too.

| ID | Status | Requirement |
| --- | --- | --- |
| R-005 | implemented | Every source file (`.cs`, `.xaml`, `.py`) under the code, test and `Icon/` folders starts with the GPL notice: the SPDX line, the copyright line and the three standard paragraphs, as `LicenseHeaderTests` spells them; a `.py` keeps its shebang and a `.xaml` its XML declaration on the first line. `LICENSE` is the full GPL v3 text. Example: `src/AstroDarkroom.Core/Version.cs` begins with `// SPDX-License-Identifier: GPL-3.0-or-later`. |

## Dropped

None yet. Move rows here with status `dropped` and one line saying why.
