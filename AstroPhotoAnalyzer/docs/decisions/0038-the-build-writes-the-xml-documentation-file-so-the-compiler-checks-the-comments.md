# 0038. The build writes the XML documentation file, so the compiler checks the comments; CS1591 stays muted

Date: 2026-10-22
Status: accepted 2026-10-22

## Context

The kit's rule "Documentation comments" (the kit's ADR 0035) asks for a documentation comment on
every type and method in the code, and counts on the compiler's checks "where the documentation
file is generated". AstroPhotoAnalyzer did not generate it: `Directory.Build.props` never set
`GenerateDocumentationFile`, so the compiler parsed no `///` comment and checked none. The
backfill planned on 2026-10-22
(`docs/plans/2026-10-22-document-every-type-and-method-and-translate-the-comments.md`) writes or
completes the comments of 126 types and 392 methods and translates 1,906 Romanian comment lines,
in five areas. A scan of the 670 doc blocks in the five projects found three that are malformed
XML (a bare `<`: two in `Report/`, one in a test), no tag naming a missing parameter, no member
tagging only some of its parameters, and nine `cref`s that resolve. The SDK publishes each
project's XML file, and its references', unless `PublishDocumentationFiles` is false: the
release's `console/` folder would then hold two `.xml` files beside the single-file exe. Put to
the owner with four options, the owner chose the file on at once, for all five projects.

## Decision

`Directory.Build.props` sets `GenerateDocumentationFile` for all five projects, mutes CS1591 in
`NoWarn` and sets `PublishDocumentationFiles` false; its comment, Romanian like the rest of that
file (ADR 0005), says why. Under the gate's `-warnaserror` the build now fails a malformed
comment (CS1570), a `<param>` or `<paramref>` naming no parameter (CS1572, CS1734), a parameter
left untagged beside tagged ones (CS1573), a `cref` that does not resolve or is ambiguous
(CS1574, CS0419), and a `///` on something that is not a member, such as a local function
(CS1587). The three malformed comments were escaped in the same commit, and nothing else
fired.

## Alternatives rejected

- **After the last area of the backfill.** The five areas would be written unchecked, and their
  slips found at the end, in a sixth pass over files already reviewed.
- **The code projects only.** A condition the kit's props does not have, and the test comments
  left unchecked, to spare one escaped `<` in one test comment.
- **Leave it off.** The backfill's scratch counter sees a missing tag, never a wrong one: a
  `<param>` naming the wrong parameter, or a bare `<` from a translation, would pass the gate.
- **CS1591 on.** It asks for public members only, properties and fields included, and the core is
  internal throughout; the kit's ADR 0035 keeps it muted everywhere.

## Consequences

- A wrong documentation comment fails the gate in all five projects, the tests included; a
  missing one still fails nothing (the rule is judgment, the kit's ADR 0035).
- Each project writes its `.xml` beside its `.dll`; `dotnet publish` with both `kit.json` entries
  copies none (2026-10-22: `console/` holds `AstroPhotoAnalyzer.exe` and the two `.pdb` files,
  `wpf/` 247 files and no `.xml`), and the lock files stay as they are.
- No cost in the gate's time could be told from its noise: the commit before the change took
  43 s, the change's own commit 53 s (a full rebuild), and the five commits after it 41 to 47 s.
  A full rebuild of the solution took 2.54 s with the file and 2.23 s without, inside a spread
  of 2 to 13 s.
