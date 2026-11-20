# 0067. A session gives its chosen lights only from its lights folder

Date: 2026-11-19
Status: accepted 2026-11-19

## Context

`--lights DIR`, and the lights folder chosen on the window's Lights tab, take the lights of the
run from another folder (R-055, ADR 0039): DIR's `lights\` when it holds frames, otherwise DIR's
own frames, so that a bare folder of lights can be given. The code review of 2026-11-13
(CR-017, `owner`) gave the input on which the fallback goes wrong: `darkscheck <library>
--lights E:\MySession`, where the session's `lights\` is empty (the raws archived) and its root
holds Siril's `result.fit`. The run read `lights: E:\MySession (1 frame .fit; chosen by you)`
and check 2 measured the stack as the one light. For the darks, `SessionFolder.Open` refuses
exactly this: a folder with a session subfolder never falls back to its root, "which holds the
stacked results". The owner chose on 2026-11-19, between refusing such a folder, refusing it
and also leaving out of a bare folder the FITS files whose header says they are stacks (as
LightsCheck's ADR 0048 does), and leaving it as it was.

## Decision

A chosen folder that has a session subfolder (`lights`, `darks`, `biases` or `flats`) gives its
lights from its `lights\` alone. When that folder is missing or holds no frame, the run is
refused before anything is decoded, with the sentence of a chosen folder without frames and the
`lights\` folder in it: `the chosen lights: no light in E:\MySession\lights (raw: ...; FITS:
...)`. A folder with no session subfolder gives its own frames, as before.

## Alternatives rejected

- **Also leaving the stacks out of a bare folder of FITS lights, by their header.** More code
  for a case the owner does not have (the lights are raws, and a folder of lights given on its
  own holds no result); it can be added when such a folder turns up.
- **Leaving the fallback.** One stacked image measured as a light gives a check 2 and an index
  of the lights that describe nothing.

## Consequences

A session without lights of its own cannot be given as the lights' folder; the message names
the folder that was searched. A folder of lights that also holds a `darks`, `biases` or `flats`
subfolder beside the frames is refused, and its lights are then given as that folder's
`lights\` or moved into one. No figure changes and no golden file changed. Guarded by
`R055_ASessionWithoutLightsIsRefusedNotReadFromItsRoot`.
