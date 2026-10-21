# Add the documentation comments rule

## Context

On 2026-10-21 the owner asked, in Romanian; translated: "I want you to add a new rule, valid in
the kit and in every project that implements the kit: all methods must have an XML comment,
including one that explains the method's parameters and the return. Classes must have one too.
The comments are in English, as usual." The owner wrote "xaml comment"; it is read as an XML
documentation comment (`///`), since XAML has no methods.

The kit says the opposite today. `Directory.Build.props:28-31` mutes CS1591 because "comments
are for the 'why', not for every member", and three adopters copied that sentence. Two adopters
recorded the same in their own ADR 0003, and one of them also mutes CS1573 and rejected "a param
tag for every parameter".

## The owner's answers in plan mode (2026-10-21)

1. **Check:** the rule in prose only, with no automated check. Put to the owner:
   - a Roslyn test in `RepoInvariantTests`, which I recommended;
   - StyleCop.Analyzers;
   - CS1591 turned back on.
2. **Scope:** the folders and files `kit.json` lists as code. Tests are exempt.
3. **Rollout:** the kit now, and the rule synced to every project now. Each project documents
   its existing code in a task of its own.

## Facts checked (2026-10-21, `main` at b8669f8)

- **One repository.** The `photo` monorepo holds the kit and six adopters whose `kit.json`
  points at it: AstroPhotoAnalyzer, DarksCheck, FlatCheck, ImageVisualCompare, LightsCheck and
  MindfulnessReminder. `sync.ps1 -Check` exits 0 in all six.
- **Branches.** `git worktree list` shows no branch for this task. The two open branches,
  `claude/Recommend-the-lift-that-puts-the-dust-on-paper-black` (APA) and
  `claude/Record-the-plate-solve-check-of-the-move-senses` (LightsCheck), touch no shared folder
  and nothing in the kit.
- **ADR numbers.** The newest kit ADR is 0034, so the new one is 0035. It supersedes no kit ADR:
  0001's "Comments in code" is about where decisions live, not about documentation.
- **How the gaps were measured.** A throwaway Roslyn syntax walk in my scratchpad. It parses
  every `.cs` under the folders each `kit.json` lists, using the SDK 9.0.319
  `Microsoft.CodeAnalysis.CSharp.dll`, and skips `bin/`, `obj/`, `*.g.cs` and `*.Designer.cs`.
  - A **type** is any class, record, struct, interface, enum or delegate declaration.
  - A **method** is a method, constructor, operator, conversion operator or finalizer.
  - A member counts as documented when its comment has a `<summary>` or `<inheritdoc/>`.
  - A documented member is **incomplete** when it lacks a `<param>`, a `<typeparam>`, or (when
    it returns a value) a `<returns>`.
- **What the code folders need**, in types and methods to touch (missing or incomplete):

  | Project | Code lines | Types | Methods | Properties without one (not required) |
  |---|---|---|---|---|
  | kit | 101 | 1 | 5 | 0 |
  | AstroPhotoAnalyzer | 11,109 | 126 | 393 | 328 |
  | DarksCheck | 9,454 | 89 | 363 | 138 |
  | FlatCheck | 21,103 | 164 | 838 | 336 |
  | ImageVisualCompare | 3,296 | 30 | 143 | 30 |
  | LightsCheck | 8,206 | 70 | 290 | 134 |
  | MindfulnessReminder | 3,875 | 8 | 150 | 33 |
  | **Total** | | **488** | **2,182** | 999 |

  - Of the 2,222 methods, 1,255 have no comment and 927 have an incomplete one.
  - Of the 1,604 undocumented types and methods, 318 are publicly visible, which is what CS1591
    would reach.
  - The tests (exempt) hold 300 types and 3,027 methods, 2,234 of which are test methods.
- **Texts that state the opposite of the new rule:**
  - `Directory.Build.props`: in the kit, DarksCheck, LightsCheck, MindfulnessReminder and
    ImageVisualCompare.
  - MindfulnessReminder ADR 0003, Decision: "comments stay for the 'why'".
  - ImageVisualCompare ADR 0003: the same, plus "A param tag for every parameter" under
    Alternatives rejected.
  - FlatCheck and APA generate no documentation file and say nothing about comments.
- **Compiler checks already on** where the documentation file is generated (the kit and the
  four projects above):
  - CS1572, a `<param>` naming no parameter;
  - CS1573, a parameter without a tag while another has one (muted in ImageVisualCompare);
  - CS1570, malformed XML;
  - CS1574, an unresolved `cref`.
- **Stale memory.** ImageVisualCompare pins no ImageSharp (its packages are the four test ones),
  so the memory "IVC will hit the advisories next" is wrong. I fix that memory at wrap-up.

## Assumptions (the rule "Two readings": decided, stated)

1. **Methods** are methods, constructors, operators, conversion operators and finalizers, of
   any accessibility, private ones included. **Classes** are every type declaration.
   - Properties, fields, events and enum members are not required; they may have a comment.
   - Local functions and lambdas are not members.
2. **Tags:**
   - `<summary>` always.
   - A `<param>` for each parameter; a primary constructor's parameters (records, classes) go on
     the type.
   - A `<typeparam>` for each type parameter.
   - A `<returns>` when a value comes back. Not for `void`, a constructor, or a plain `Task` or
     `ValueTask`, which have no value to describe (27 such methods in the code folders).
   - `<inheritdoc/>` for an override or an interface implementation.
3. **What a comment says:** the member's contract, which is what a caller needs. That is what
   it does, what each parameter means (unit, range, whether null is allowed) and what comes back.
   It does not repeat the name. How an area's parts fit stays in its design note (the rule
   "Design notes"); a line's "why" stays an ordinary comment.
4. **One clause in `/wrap-up` step 3.** When the diff is read back, every type and method it
   adds or edits in the code has its comment. This is the model's own read at a fixed point,
   not an automated check, so it stays within answer 1. Say no at approval and I drop it.
5. **Settings do not change anywhere.**
   - CS1591 stays muted, now because the rule is judgment and CS1591 would reach only public
     members (properties and fields included).
   - ImageVisualCompare keeps its CS1573 mute until its own backfill task documents every
     parameter there.
   - Only the comment texts and the two ADR Status lines change.
6. **Adopters' texts are fixed in their sync commits,** as the golden-diff task did and as
   `/wrap-up` step 9 says ("; <part> superseded by the kit's ADR NNNN" on a project ADR's
   Status line).
7. **CHANGELOG:** one Added line in the kit. None in the adopters now: their sync changes
   process text and comments only. Each backfill task adds its own Internal line.
8. **The generator prompt** (`AI Prompt to Generate Starter Project.md`) gains rule 15, in
   stack-neutral words, so a starter regenerated from it carries the rule.
9. **The kit ADR is anonymous** (the kit's ADR 0025). It gives totals, not adopters' names.
   `GHID.md` keeps the owner's words in the original Romanian.

## Setup

- Branch `claude/Add-the-documentation-comments-rule`. The worktree is made from
  `E:\Photografie\Code\ClaudeCodeStarterKit`:
  `git worktree add .claude/worktrees/Add-the-documentation-comments-rule -b claude/Add-the-documentation-comments-rule main`
- Work and commit with `git -C <wt>/<project>`, the path written out, never in a variable.
- This plan is saved as
  `ClaudeCodeStarterKit/docs/plans/2026-10-21-add-the-documentation-comments-rule.md`.
- **If a gate goes red for a reason that predates this task** (a fresh restore, a new NuGet
  advisory, an antivirus block on a freshly built dll), I stop and report it. I do not touch
  settings or the kit's scripts to get past it.

## Changes

### The kit (`<wt>/ClaudeCodeStarterKit`)

**`.claude/rules/kit/process.md`:** a new rule, placed before "Design notes":

> - **Documentation comments.** Every type and every method in the code (the folders and files
>   `kit.json` lists as code; tests are exempt), constructors and operators included, private
>   ones too, has an XML documentation comment in English: a `<summary>`, a `<param>` for each
>   parameter (a primary constructor's go on its type), a `<typeparam>` for each type parameter,
>   and a `<returns>` when it returns a value (not for `void` or a plain `Task`). An override or
>   an interface implementation may use `<inheritdoc/>`. The comment says what a caller needs:
>   what the member does, what each parameter means (unit, range, whether null is allowed) and
>   what comes back, not its name again. How an area's parts fit stays in its design note, and
>   a line's "why" in an ordinary comment. A property, field or event may have one but need not.
>   Code written before this rule gets its comments in a task of its own; until then a change
>   documents every type and method it adds or edits. Judgment, not a gate: nothing checks it
>   (the kit's ADR 0035).

**`.claude/skills/wrap-up/SKILL.md`, step 3,** gains one sentence:

> Every type and method the diff adds or edits in the code has its documentation comment, with
> a tag for each parameter and for the return; add what is missing (the kit's rule
> "Documentation comments").

**New ADR** `docs/decisions/0035-every-type-and-method-has-an-xml-doc-comment.md`:
- Title: "Every type and method in the code has an XML documentation comment; judgment, not a
  gate". `Date: 2026-10-21`, `Status: accepted 2026-10-21`, no `Supersedes:`.
- **Context:**
  - the old CS1591 reason, quoted;
  - the owner's request in English, and how "xaml" is read;
  - the measurement (method, definitions and totals above);
  - the owner's three answers.
- **Decision:**
  - the rule's text and the readings in Assumptions 1-3;
  - CS1591 stays muted, for the new reason;
  - in a project, the rule is synced at once, the texts that say the opposite are corrected in
    the sync commit, and the existing code is documented in a task of its own, by area where
    the project is large.
- **Alternatives rejected,** each with its deciding fact:
  - the Roslyn test: put to the owner and declined; the owner chose the rule alone;
  - StyleCop.Analyzers: only betas for current C# (the stable 1.1.118 is from 2019), and SA1600
    cannot leave out properties (999 more comments) or the ~150 other rules;
  - CS1591: 318 of 1,604, and never a missing `<param>` or `<returns>`;
  - tests included: 2,438 more methods, about 2,200 of them tests whose names already carry
    the requirement;
  - every project in one task: about 2,670 comments, bulk-written and unreadable as a diff;
  - keeping "comments for the 'why'": the owner asked for the opposite.
- **Consequences:**
  - a missing comment fails nothing; `/wrap-up` step 3 and `/code-review` are where it is seen;
  - where the documentation file is generated, CS1572/CS1573/CS1570/CS1574 still fail a wrong
    comment;
  - synced to every project;
  - six backfill tasks follow (sizes above).

**Sample code** gets complete comments, per Assumptions 2-3. The requirement IDs in the
existing summaries stay.
- `src/Starter.Core/Stats.cs`: `<param>` and `<returns>` for `Median` and `RobustSigma`.
- `src/Starter.Core/NumberFile.cs`: `Parse` gets a summary (which mentions the
  `FormatException` naming the line), a `<param>` and a `<returns>`.
- `src/Starter.Cli/Program.cs`:
  - `Program` gets a summary;
  - `Main` gets a summary, a `<param>` and a `<returns>`;
  - `Run` gets its three `<param>`s and a `<returns>`.

**`Directory.Build.props:28-29`,** the comment becomes:

> IDE0005 (unused usings) only runs at build when the doc file is generated. CS1591 (missing XML
> comment) stays muted: the rule "Documentation comments" asks for a comment on every type and
> method, private ones included, and nothing checks it; CS1591 would ask only for public
> members, properties and fields included (the kit's ADR 0035).

**`GHID.md`** (Romanian, no diacritics, as the guide is):
- **The rules-file paragraph (line 89):** "Documentation comments" joins the list of names,
  before "Design notes".
- **A new paragraph in "Fluxul unei schimbari",** after **Bug-uri**: "**Comentariile XML**
  (regula "Documentation comments" a kit-ului, ADR 0035)". It covers:
  - what the rule asks;
  - the owner's words in the original;
  - the old "for the why" reason, and that it is reversed;
  - contract versus design note versus ordinary comment;
  - why tests are exempt;
  - why there is no mechanism: the three measured options and the owner's choice;
  - what still holds it: `/wrap-up` step 3, `/code-review`, and the CS157x checks;
  - the backfill per project, by area.
- **"Ce e garantat si ce nu" (line 126), "Ce ramane pe judecata":** adds "daca fiecare tip si
  metoda din cod are comentariul XML".
- **The `/wrap-up` list, item 3,** gains the step-3 sentence.
- **"Migrarea unui proiect existent":** one bolded sentence after Stratul 3. The existing code's
  comments come gradually, in a task of their own, by area; meanwhile every change documents
  what it adds or edits.

**`README.md`, "Ce ramane pe judecata" (line 136):** the same addition as in the guide.

**`AI Prompt to Generate Starter Project.md`, "Rules to encode",** gains:

> 15. Every type and every function or method in the code, private ones included, has a
>     documentation comment in the stack's standard form: what it does, each parameter, the
>     return value. Tests are exempt.

**`CHANGELOG.md`,** a line at the top of `### Added`:

> The kit's rule "Documentation comments" (ADR 0035): every type and every method in the code,
> private ones included, has an XML documentation comment in English, with a `<param>` for each
> parameter, a `<typeparam>` for each type parameter and a `<returns>` for a value. Tests are
> exempt, and nothing checks it; `/wrap-up` step 3 looks for it in the diff. The sample code is
> documented, and `Directory.Build.props` keeps CS1591 muted for that reason.

### Each adopter (`<wt>/<project>`, after `scripts/sync.ps1` in the kit)

- **The sync** copies `.claude/rules/kit/process.md` and `.claude/skills/wrap-up/SKILL.md`. I
  check that it copies nothing else.
- **DarksCheck, LightsCheck:** the `Directory.Build.props` comment becomes the kit's new text,
  word for word.
- **MindfulnessReminder:**
  - `Directory.Build.props`: the same new reason, keeping its "394 of them on 2026-10-09" figure.
  - ADR 0003 Status: `accepted 2026-10-09; comments for the "why" only superseded by the kit's
    ADR 0035`.
- **ImageVisualCompare:**
  - `Directory.Build.props`: the same new reason for CS1591, plus: CS1573 stays muted until the
    comments written before the rule tag every parameter (ADR 0003).
  - ADR 0003 Status: `accepted 2026-10-02; comments for the "why" only superseded by the kit's
    ADR 0035`.
- **AstroPhotoAnalyzer, FlatCheck:** the sync only.

## Commits on `claude/Add-the-documentation-comments-rule`

1. **K1:** this plan.
2. **K2:** the kit: the rule, the wrap-up step, ADR 0035, the sample code, the props comment,
   GHID, README, the generator prompt and the CHANGELOG. From here the worktree's adopters are
   red at step 0 until their own commit.
3. **A1, D1, F1, I1, L1, M1:** one commit per adopter, each with the sync plus its edits.

Each message is one line, under 100 characters, and says why. The commit hook runs the gate of
each project a commit touches.

## Then

- `/wrap-up` runs its steps in each of the seven project folders. In the kit's run, step 3
  applies the new sentence to the sample code.
- Memory:
  - a new pointer, `documentation-comments-rule.md`, to the kit's ADR 0035 and the backfill
    sizes;
  - the ImageSharp memory corrected for ImageVisualCompare.
- I suggest `/code-review main...claude/Add-the-documentation-comments-rule`. Then I propose
  `git merge --no-ff claude/Add-the-documentation-comments-rule` in `E:\Photografie\Code` and
  ask who runs it. After the merge: `git worktree remove`, then `git branch -d`.
- **Follow-ups, not in this task:** six backfill tasks, one per adopter, with the sizes from the
  table. FlatCheck and AstroPhotoAnalyzer go by area, one area per session, with verify after
  each. ImageVisualCompare's task also drops its CS1573 mute, and FlatCheck's and APA's may
  turn on the documentation file so the CS157x checks run there too. A chip opens at a checkout
  root in `photo`, so I hand you one prompt per project instead, to paste into a session opened
  in that project's folder.

## Verification

- `scripts/verify.ps1` is green in each of the seven project folders of the worktree. I paste
  the last lines of each.
- `check-decisions.ps1`: the kit has 35 ADRs, and every adopter keeps its count. Both new
  "superseded by the kit's ADR 0035" clauses resolve against the worktree's kit.
- `sync.ps1 -Check` is identical in all six adopters.
- Re-running the scratch counter on the kit's code folders finds 0 undocumented and 0 incomplete
  types and methods. I paste the output.
- A grep for "comments are for the", "for the \"why\", not for every" and "comments stay for"
  over the seven folders finds only history: plans, and the two ADR 0003s whose Status now says
  that part is superseded.
- `git diff main...` is read in full before each commit.

## What deviated

(filled at wrap-up)
