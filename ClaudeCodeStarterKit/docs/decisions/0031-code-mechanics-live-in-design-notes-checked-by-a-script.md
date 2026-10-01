# 0031. How code works lives in the design note of its area; a synced script checks the notes

Date: 2026-10-01
Status: accepted 2026-10-01

## Context

Since 2026-09-24 the kit's rule "No code structure in docs" said not to describe code structure in
`CLAUDE.md` or in docs, and to describe behaviour and decisions instead. Read literally, it was
broken from its first day: the ADR template asks for the tests that guard a decision, and the
kit's own `CLAUDE.md` names `Program.Run`. The first adopter worked another way. Its design notes
hold the mechanics of the code they cover, each says at the top which files it covers, and since
2026-09-27 a note repeats neither its `CLAUDE.md` nor a decision record; nothing checked any of it.
Measured on 2026-10-01 on its 21 notes: 23 "Covers:" passages, most inside the opening quote
block, with 190 code spans, 111 of which look like paths. 109 of those name files or folders that
exist, from the project root, from a code or test folder, from the notes' folder, or as a file name
found in the project; the other 2 both name `Report/AgainstReport.cs`, which does not exist. On
2026-09-30 the owner decided, in chat, decision Q7 of the programme: any file may name code as an
address; what code does and how its parts fit goes only in the design note of its area, which names
the code it covers and is updated or deleted with it; one whole-project map is allowed; a synced
script and a `/wrap-up` step keep the notes true. On 2026-10-01 the owner answered what this left
open: the rule covers the project's code and tests as `kit.json` names them; any file may also say
what a test or a check enforces; the Covers form the first adopter already writes is accepted; the
script lists the notes a branch touched; a project retires an index test of its own.

## Decision

- A rule of the kit, "Design notes": "Any file may name the project's code (the folders and files
  `kit.json` lists as code and tests) and say what a test or a check enforces. How that code
  works and how its parts fit go only in the design note of its area: a note in `docs/design/`
  whose `Covers:` sentence, above its first section, names the files it describes, and which is
  updated or deleted in the same task as that code. A note repeats neither `CLAUDE.md` nor an
  ADR; one note may map the whole project." The kit's `CLAUDE.md` drops its own rule "No code
  structure in docs". The guide and the READMEs keep explaining what the kit's own scripts do:
  they are not the project's code (ADR 0011).
- A Covers sentence is a "Covers:" marker, bold or plain, in a quote block or not, above the
  note's first heading of level 2 or more, and runs to the first full stop followed by a space or
  a line end. A code span in it that looks like a path (a "/" or a known file extension) must name
  a file or folder of the project, with its letter case: from the project root, from a code or test
  folder, from `docs/design/`, or as a file name found in the project. Other spans are not checked.
- `scripts/check-design.ps1`, synced like every script, is the step "design notes" of
  `scripts/verify.ps1`, after "decisions". It checks that the index `docs/design/README.md` names
  every note and only notes, that every note has its Covers sentence, and that every path in it
  exists. With `-Since <ref>` it checks nothing and lists the notes whose Covers sentence names a
  file changed since `<ref>`; a new `/wrap-up` step runs it with `main`, and each listed note is
  read against the change.
- A project that takes the check retires any test of its own that checks the same index.

## Alternatives rejected

- **Keep "No code structure in docs".** It forbade what every ADR and `CLAUDE.md` do, naming code
  and tests, and gave the mechanics no home.
- **The rule for every file, the kit's scripts included.** The guide and the READMEs exist to
  explain the scripts (ADR 0011), and each script's header already says what it does.
- **A strict Covers form: the first line, paths from the project root only.** 78 of the first
  adopter's 111 path names would be rewritten, and nothing more would be checked.
- **A test in each project.** Tests are not synced; the first adopter's test checked the index
  only, and each project would write and keep its own.
- **List the touched notes by hand at `/wrap-up`.** Paths that start at a code folder, and bare
  file names, make a diff hard to match by eye; the script uses the same rules as the check.
- **Check the text of a note.** No script can tell whether a sentence still describes the code;
  `/wrap-up` and `/docs-health` judge that.

## Consequences

The gate has eight checks. A file renamed or deleted while a Covers sentence still names it turns
the gate red until the note is fixed, and so does a note added without a Covers sentence or an
index row. The first adopter's two stale spans are fixed in its sync commit, and its index test is
removed there. Both projects allow `scripts/check-design.ps1` in their own `allow` list of
`.claude/settings.json`, so the `/wrap-up` step does not ask each time; the owner chose that on
2026-10-01, and it is not a process key. `DesignCheckTests` covers each rule of the check, the
forms of note it reads, and the listing on a git repository. The check never judges whether a note still says what its code
does; the `/wrap-up` step points at the notes to read.
