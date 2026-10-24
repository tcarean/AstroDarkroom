# 0059. The build writes the XML documentation file so the compiler checks the comments; CS1591 stays muted and the files are not published

Date: 2026-10-24
Status: accepted 2026-10-24

## Context

The kit's rule "Documentation comments" (the kit's ADR 0035) asks for an XML comment on every
type and method, and the backfill of
`docs/plans/2026-10-24-document-every-type-and-method-and-translate-the-comments.md` writes
about a thousand of them. A scratch counter sees a tag missing, never a tag wrong. Without the
documentation file the compiler parses `///` but reports nothing. With it on, the first build
under the gate's `-warnaserror` reported 34 errors on comments already on `main`, in 15 files of
all five projects: 20 parameters left untagged in 9 members that tag others (CS1573), 3 blocks
that are not well-formed, a bare `<dir>`, `<versiune>` or `<nume simplu>` in the text (7 errors,
CS1570), 3 `<paramref>` naming a local or a tuple element (CS1734), 2 `cref`s naming a renamed
or another type's member (CS1574) and 2 an overloaded one (CS0419).
AstroPhotoAnalyzer settled the same question the same way (its ADR 0038); each project decides
for itself.

## Decision

`Directory.Build.props` sets, for all five projects, `GenerateDocumentationFile` true, `NoWarn`
CS1591 and `PublishDocumentationFiles` false. The 34 errors are fixed in the same commit, with
the smallest change each and no code token changed: the missing `<param>` tags added in
English beside the Romanian ones (a file may hold both languages until its backfill area
translates it, ADR 0060), the bare `<` and `>` escaped, the `<paramref>` to a local turned into
`<c>`, and each `cref` pointed at the member it meant. The owner chose this on 2026-10-24 over
switching the file on only after the last area.

## Alternatives rejected

- **Switch it on after the last area.** Four areas would be written with the compiler blind to
  a malformed or misnamed tag, the likeliest slips of a translation.
- **The code projects only.** The tests' doc comments, five of the 34, would stay unchecked, and
  the setting would need a second place beside `Directory.Build.props`.
- **Leave CS1591 on.** It asks for public members only, properties included; the core is
  `internal` throughout, so it would miss most of the code and demand comments the kit's rule
  does not.
- **Publish the XML files.** Nothing reads them at run time; the console's single-file folder
  would gain `.xml` files beside the exe.

## Consequences

- A comment that is not well-formed XML, a `<param>` or `<paramref>` naming no parameter, a
  parameter left untagged beside tagged ones, a comment on something that is not a member, and
  a `cref` that does not resolve or is ambiguous now fail `scripts/verify.ps1`. A `cref` to an
  overloaded member carries its parameter list.
- The build writes `FlatCheck.Core.xml`, `flatcheck.xml`, `FlatCheck.Wpf.xml` and the two test
  projects' beside their assemblies; a publish of both `kit.json` entries ships none (checked on
  2026-10-24: `console/` holds the exe, two `.pdb` and `lensfun/`).
- A missing comment is still not a compiler error: the kit's rule is judgment, the counter the
  backfill's own check.
