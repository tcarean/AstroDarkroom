# Name only the files a Covers sentence describes

Approved in chat on 2026-10-01: the owner asked whether describing code in design notes is
right, asked for a measurement, then for one of the variant that would move the code map into
the area notes, and answered yes to the recommended first step.

## Facts measured (2026-10-01, main at b0bdf78)

- Replaying FlatCheck's history with its notes of that day: of 30 merged branches, 12 list a
  note and `code-map.md` is among them every time; in 5, all process tasks touching only tests,
  it is the only one. Of 58 earlier commits, 51 list it.
- The map's eight lines about the test projects are about their project files. Its "Structure"
  section names every code file of `FlatCheck.Core/`; of the window it describes `Interop/`,
  `Presentation/`, `Session/`, `ViewModels/` and `Views/`, not `Mvvm/` or `App`.
- Against the area notes: 1% of the map's five-word sequences appear in another note, 45% of
  its code names do, and about 55% of its sentences give a reason. Moving it would be a rewrite
  of some 420 lines over 15 notes.

## Changes

- FlatCheck `docs/design/code-map.md`: the Covers sentence names what the map describes; the
  files it only points at follow the sentence.
- Kit `docs/design/README.md`: one bullet, a Covers sentence names what its note describes.
- Kit ADR 0032 with the measurement; kit CHANGELOG, Changed.

## What deviated

- No plan mode: a small task whose design the owner settled in chat after the measurements;
  it ran in the session of task 6, which "One task per session" would have ended.
- Replayed with the narrowed map: listed in 7 of the 30 branches instead of 12, none of the 5
  test-only branches lists a note; on the 58 earlier commits, 43 instead of 51.
