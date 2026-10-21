# 0035. Every type and method in the code has an XML documentation comment; judgment, not a gate

Date: 2026-10-21
Status: accepted 2026-10-21

## Context

Since 2026-09-24 the kit's `Directory.Build.props` generates the documentation file, so that
IDE0005 runs at build, and mutes CS1591, the compiler's warning for a public member without an
XML comment, because "comments are for the 'why', not for every member". Four of the six projects
made from the kit give the same reason in theirs, two of them also in an ADR of their own; one of
those mutes CS1573 as well, a parameter without a tag while another has one, and rejected "a param
tag for every parameter". On 2026-10-21 the owner asked for the opposite, in chat, in Romanian;
translated: "I want you to add a new rule, valid in the kit and in every project that implements
the kit: all methods must have an XML comment, including one that explains the method's
parameters and the return. Classes must have one too. The comments are in English, as usual."
The owner wrote "xaml comment"; XAML has no methods, so it is read as an XML documentation comment
(`///`).

Measured the same day on `main` (b8669f8), with a throwaway Roslyn syntax walk over the folders
each `kit.json` lists, in the kit and the six projects, build output and generated files left
out. In the code folders there are 713 types: 349 have no comment, and 139 have one without every
`<param>` or `<typeparam>`. There are 2,222 methods: 1,255 have no comment, and 927 have one
without every `<param>` or `<returns>`. 999 properties, indexers and events have none. Of the
1,604 types and methods without a comment, 318 are publicly visible, the ones CS1591 reports. The
tests hold 300 types and 3,027 methods, 2,234 of them test methods. StyleCop.Analyzers, the usual
analyzer for this, has shipped only betas since its stable 1.1.118 of 2019-04-29.

In plan mode the owner chose the rule in prose, with no check; the code folders only; and the kit
now, with each project's existing code documented in a task of its own.

## Decision

- A rule of the kit, "Documentation comments", in `.claude/rules/kit/process.md`: every type and
  every method in the code (the folders and files `kit.json` lists as code; tests are exempt),
  constructors and operators included, private ones too, has an XML documentation comment in
  English. It has a `<summary>`, a `<param>` for each parameter (a primary constructor's go on its
  type), a `<typeparam>` for each type parameter, and a `<returns>` when it returns a value.
- "Method" is read as a method, constructor, operator, conversion operator or finalizer; "class"
  as every type declaration: class, record, struct, interface, enum and delegate. Properties,
  fields, events and enum members may have a comment and need not. Local functions and lambdas
  are not members.
- No `<returns>` for `void`, a constructor, or a plain `Task` or `ValueTask`, which carry no
  value. `<inheritdoc/>` is enough for an override or an interface implementation.
- A comment states the member's contract, what a caller needs: what it does, what each
  parameter means (unit, range, whether null is allowed) and what comes back. How the parts of
  an area fit stays in its design note (ADR 0031), and the "why" of a line in an ordinary
  comment.
- Nothing checks it. `Directory.Build.props` keeps CS1591 muted, now because the rule is
  judgment and CS1591 would ask for public members only, properties and fields included.
  `/wrap-up` step 3 reads the diff for the types and methods it adds or edits.
- In a project the rule arrives with the sync of the rules file, and from then on binds every
  type and method a change adds or edits. A project text that gives the old reason is corrected
  in that sync commit. A project ADR that recorded it gets `; <part> superseded by the kit's ADR
  0035` on its Status (ADR 0025).
- The code written before the rule gets its comments in a task of its own per project, by area
  where the project is large. The kit's sample code is documented with this ADR.

## Alternatives rejected

- **A Roslyn check in `RepoInvariantTests`.** It would have listed every gap by file and line,
  at the price of a new test package and a test kept in every project. It was put to the owner
  with that cost, and the owner chose the rule alone.
- **StyleCop.Analyzers, its documentation rules only.** Only its betas read current C#, and its
  SA1600 cannot leave out properties, events and fields. The rule would have grown to every
  member, 999 more comments in the code today, with about 150 other rules to switch off.
- **CS1591 back on.** It sees publicly visible members only: 318 of the 1,604 types and methods
  without a comment. It never sees a missing `<param>` or `<returns>`, and it asks for public
  properties and fields as well.
- **Tests included.** 2,438 more methods to touch, most of them test methods whose names already
  carry the requirement ID and the behaviour.
- **Every project in this task.** About 2,670 comments in one change: written in bulk they come
  out thin, and nobody reads a diff of that size.
- **Keep "comments are for the 'why'".** The owner asked for the opposite.

## Consequences

- The rules file is synced to every project (ADR 0028), so the rule binds everywhere at once.
- A missing comment fails no gate. `/wrap-up` step 3 and `/code-review` are where one is seen.
- Where the documentation file is generated, the compiler still fails a wrong comment:
  - a `<param>` naming no parameter (CS1572);
  - a parameter without a tag while another has one (CS1573);
  - malformed XML (CS1570);
  - a `cref` that does not resolve (CS1574).
- Each project has a backfill task ahead. Its size is in the plan of this ADR's task,
  `docs/plans/2026-10-21-add-the-documentation-comments-rule.md`.
- Guards: none.
