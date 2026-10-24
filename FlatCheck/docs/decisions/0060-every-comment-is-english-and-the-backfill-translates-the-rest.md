# 0060. Every comment is English; the backfill translates the rest, area by area

Date: 2026-10-24
Status: accepted 2026-10-24
Supersedes: 0006 in part (the Romanian comments of the tests and of the XAML and project files, and a comment added to a file not yet translated being Romanian)

## Context

ADR 0006 (2026-09-25) kept the code comments Romanian, the csproj headers included. The kit's
ADR 0035 then made the comments of the code's C# files English, translated when each file gets
its documentation comments, and until then a comment added to such a file stayed Romanian so
that no file held two languages. On 2026-10-24, `main` at 941deff, 3,845 comment lines of the
121 code files were still Romanian, with 899 in 43 of the 78 test files, 162 XAML comments and
the comments of five csproj files. The owner asked for every comment in the code to be English,
and for comments to be written only in English from now on; the backfill
(`docs/plans/2026-10-24-document-every-type-and-method-and-translate-the-comments.md`) runs in
five areas, one branch each, over several sessions. ADR 0006 rejected files in two languages
because the model would then pick a language per edit; with every new comment English there is
nothing left to pick, and the mix is bounded by the backfill.

## Decision

Every comment is English, in every file of the five projects: C#, XAML, csproj and props, tests
included. A comment written from now on is English, whatever the language of the file around
it. A file not yet translated may hold both languages until its backfill area translates it;
that exception ends with the fifth area, which removes it from `CLAUDE.md`. What the user sees,
string literals and Romanian output quoted in a comment, stays Romanian (ADR 0006). The owner
chose this on 2026-10-24 over having any task that comments a Romanian file translate that whole
file first.

## Alternatives rejected

- **The task that comments a Romanian file translates it first.** No file would ever hold two
  languages, but a one-line fix in `SyntheticTabViewModel.cs` would carry a translation of its
  hundreds of comment lines, for the days until the backfill reaches it anyway.
- **Keep new comments Romanian until the file's area.** The owner's instruction is English from
  now on; and the documentation-file commit (ADR 0059) already had to add English tags to files
  of later areas.
- **Leave the tests, XAML and project files Romanian.** The kit's rule "Documentation comments"
  exempts the tests from tags, not from the rule "Languages"; the owner asked for every comment.

## Consequences

- During the backfill some files hold both languages; `doccount romanian`, the backfill's scratch
  check, lists what is left per area, and the last area shows none.
- `CLAUDE.md` "Languages" says English for every comment and names the temporary exception.
- LightsCheck's copies of FlatCheck's readers are recopied after the backfill (its own task).
