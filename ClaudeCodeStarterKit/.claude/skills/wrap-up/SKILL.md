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
   has an ADR. If unsure, list the candidates and ask.
6. `docs/plans/`: if a plan was followed, note at its end what deviated and why.
7. `docs/validation.md`: if figures were re-measured, the date and the output are recorded.
8. Propose the commit message: first line under 70 characters saying why; body with the summary
   from step 2. Do not commit unless the user asked for it in this session; never `--no-verify`.
9. If anything was left unfinished, say so in one list, with what is needed to finish it.
