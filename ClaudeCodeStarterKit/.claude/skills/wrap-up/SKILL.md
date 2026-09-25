---
name: wrap-up
description: Close a task - run the gate, update CHANGELOG, requirements, decisions and plans, then propose the commit. Use at the end of every session or when the user says /wrap-up.
---

# Wrap up

Run through every step; report what each one found. Do not skip a step because it "probably"
does not apply.

1. Run `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1`. Paste the last
   ten lines. If it is red, fix it or stop and report; never continue with a red gate.
2. `git diff` the whole change. Read it back. Write a three-sentence plain-language summary:
   what changed for the user, what changed inside, what did not change that someone might expect.
3. `CHANGELOG.md`: every user-visible change has a line under `[Unreleased]`, in the right
   subsection (Added, Changed, Fixed, Removed).
4. `docs/requirements.md`: statuses match reality. A requirement touched by this task that is
   still `draft` is a question for the user, not something to leave.
5. `docs/decisions/`: any choice made in this session that someone could later ask "why?" about
   has an ADR. If unsure, list the candidates and ask. An ADR still `proposed` while code built
   on it is about to be committed is a question for the owner: accept it, or change the code.
6. `docs/plans/`: if a plan was followed, note at its end what deviated and why.
7. `docs/validation.md`: if figures were re-measured, the date and the output are recorded.
8. Guide and README: if the change touched `scripts/`, `.claude/`, `CLAUDE.md` or `monorepo/`,
   quote what `README.md` and, in the kit, `GHID.md` say about the touched piece, and fix what
   no longer holds, in this task. `RepoInvariantTests` only catches paths that no longer exist,
   not explanations that no longer hold (ADR 0011). In a project made from the kit, a change to
   the process itself (a hook, a skill, a rule) is flagged: it probably belongs in the kit and its
   guide too.
9. If `git branch --show-current` is still a generated name (`claude/` plus two random words and
   a hex suffix, like `claude/wonderful-yalow-f0ea0e`) rather than `claude/` plus the task in
   English, rename the branch now with `git branch -m claude/<Task-name>` (ADR 0013). Propose
   the commit message: one line, English, under 72 characters, saying why. No body, no
   trailers, no `Co-Authored-By` (the summary from step 2 stays in the chat, and the "what" is
   in CHANGELOG). Then ask: does the owner want you to run the commit, or will they do it by
   hand? Do nothing until they answer. If the branch is ready to merge into `main`, say so,
   suggest `/code-review main...<branch>` first if it has not run on this work, and ask the same
   question for the merge. Once the merge is done, delete the branch with `git branch -d`; if a
   worktree holds it, release it first (`git switch --detach` from the session inside that
   worktree, `git worktree remove <path>` from outside). `-D` only when the owner asks. Never
   `--no-verify`.
10. If anything was left unfinished, say so in one list, with what is needed to finish it.
