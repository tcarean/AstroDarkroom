---
name: wrap-up
description: Close a task - run the gate, update CHANGELOG, requirements, decisions and plans, then propose the commit. Use at the end of every session or when the user says /wrap-up.
---

# Wrap up

Run through every step; report what each one found. Do not skip a step because it "probably"
does not apply. Before step 1, read `docs/skills.md` if it exists: its `## /wrap-up` section
adds project-specific steps and says where they go.

1. Run `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1`. Paste the last
   ten lines. If it is red, fix it or stop and report; never continue with a red gate.
2. If the session touched numeric code (CLAUDE.md says which files) and `docs/validation.json`
   has cases, run `/validate` and paste its drift table. A figure that moved is a finding, not
   something to re-measure into place; so is a golden file that changed, where the project has
   them.
3. `git diff` the whole change. Read it back. Write a three-sentence plain-language summary:
   what changed for the user, what changed inside, what did not change that someone might expect.
4. `CHANGELOG.md`: every user-visible change has a line under `[Unreleased]`, in the right
   subsection (Added, Changed, Fixed, Removed; Internal for a pure refactor).
5. `docs/requirements.md`: statuses match reality. A requirement touched by this task that is
   still `draft` is a question for the user, not something to leave. A new `R###_` test moves
   its requirement to `implemented`.
6. `docs/decisions/`: any choice made in this session that someone could later ask "why?" about
   has an ADR, including a choice settled in chat that happened to match convention. If unsure,
   list the candidates and ask. An ADR still `proposed` while code built on it is about to be
   committed is a question for the owner: accept it, or change the code.
7. `docs/plans/`: if a plan was followed, note at its end what deviated and why.
8. `docs/validation.md`: if figures were re-measured, the date and the output are recorded.
9. `README.md` is the user documentation: if the change is user-visible (a mode, an option, a
   verdict, a tab), the text there describes it. If the change touched `CLAUDE.md`, `kit.json`,
   `docs/skills.md`, `.claude/settings.json` or `monorepo/`, quote what `README.md` and, in the
   kit, `GHID.md` say about the touched piece and fix what no longer holds, in this task;
   `RepoInvariantTests` only catches paths that no longer exist, not explanations that no longer
   hold (ADR 0011). `scripts/`, `.claude/skills/` and `.claude/agents/` are the kit's (rule 15):
   in the kit, a change there ends with `scripts/sync.ps1` and one commit per project; in a
   project, such a change is made in the kit instead, and an edit here is refused by verify and
   by the Stop hook.
10. If `git branch --show-current` is still a generated name (`claude/` plus two random words and
    a hex suffix, like `claude/wonderful-yalow-f0ea0e`) rather than `claude/` plus the task in
    English, rename the branch now with `git branch -m claude/<Task-name>` (ADR 0013). Propose
    the commit message: one line, English, under 72 characters, saying why. No body, no
    trailers, no `Co-Authored-By` (the summary from step 3 stays in the chat, and the "what" is
    in CHANGELOG). Then ask: does the owner want you to run the commit, or will they do it by
    hand? Do nothing until they answer. If the branch is ready to merge into `main`, say so,
    suggest `/code-review main...<branch>` first if it has not run on this work, and ask the same
    question for the merge. Once the merge is done, delete the branch with `git branch -d`; if a
    worktree holds it, release it first (`git switch --detach` from the session inside that
    worktree, `git worktree remove <path>` from outside). `-D` only when the owner asks. Never
    `--no-verify`.
11. If anything was left unfinished, say so in one list, with what is needed to finish it.
