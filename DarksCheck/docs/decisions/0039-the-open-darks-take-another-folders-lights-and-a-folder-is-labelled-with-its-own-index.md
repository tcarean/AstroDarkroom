# 0039. The open darks take another folder's lights, and a folder is labelled with its own index

Date: 2026-11-07
Status: accepted 2026-11-07; the partner staying remembered superseded by 0041
Supersedes: 0011 in part (the label of a session whose darks are the partner's)

## Context

The owner's question of 2026-11-07: with a library of darks open, do these darks fit the
lights of some other session, one shot without darks or with bad ones? The measurement that
answers it has existed since ADR 0004 and ADR 0011: check 9 reads the dark's hot pixels in
the lights, and the ratio gives the verdict, the degrees and the lights' equivalent index
(R-064). But a run only takes the lights of the folder it opens, so the question could be asked
one way round only: open the lights-only session and give the darks as the partner (R-054).
The owner thinks darks first. Two more things came up on the Index tab in that lights-only
case: the big label was the partner's (`<library>-idx67`), which a session with darks of its
own never shows, and the same index and label stood again on the `dark-uri (partener)` row.

## Decision

A run can take its lights from another folder: `--lights DIR` in the console, a card on the
window's Lights tab (R-055, R-122). DIR is a session, whose `lights\` is taken, or a bare
folder of lights. Those lights replace the open folder's own in the inventory, check 1, check 9,
the lights table, the lights CSV and the INDEX row `lights`; the figures are the ones the same
lights give from the folder's own `lights\`, since `SessionFolder` only swaps the list. The
darks' index and label stay the open folder's. The chosen lights folder is not stored in
`settings.json` and is dropped when another folder is opened; the partner stays remembered as
before (R-075). A lights folder that does not open or holds no frame fails with the prefix
`lights-urile alese: ` / `the chosen lights: `, as the partner fails with `partenerul: `.

The label of a folder with lights and no darks of its own is the folder's own name plus the
lights' equivalent index (R-066): what dark those lights are like, read through the partner's
darks. The partner's label moves to the end of the `dark-uri (partener)` row, so the partner's
index and label appear once, on the partner's row, and the big label on the Index tab is always
the open folder's. The text under the label says so for such a folder.

## Alternatives rejected

- **An index read on the lights alone, without any dark.** The index is the amplitude of the
  hot pixels on the ranks 122 .. 483 of a dark master; in a light those pixels sit under a sky
  with 15 to 20 ADU of noise per pixel on the Veil session, against an index of 25 (Smida) to
  67 (the library). A median of the local excess over dithered lights might recover it, but
  nothing was measured, and a verdict on a given dark still needs that dark's pixels. Left for
  a task of its own, after a measurement on Smida (truth 25 through its darks) and the Veil
  (29 through the library).
- **A second kind of partner on the Index tab.** The Index tab's partner is darks against darks;
  mixing a lights folder into the same chooser would make `dark-uri / partener` and
  `lights / partener` depend on which kind was chosen. The lights folder lives on the Lights
  tab, where check 9 is read.
- **The lights folder remembered like the partner.** A library of darks remembered as the
  partner is what every session is compared with; a foreign lights folder silently attached to
  the next folder opened would give a verdict about lights the owner did not mean.
- **`-idx-` on a session without darks.** It says nothing; the owner wants the number the
  session's lights ask for in the folder's name.

## Consequences

The owner opens the library, chooses the session's lights on the Lights tab and reads check 9;
or runs `darkscheck <library> --lights <session>`. `DarkOptions.LightsFrom` reaches
`SessionFolder.Open`, which lists DIR's `lights\` or DIR itself; `R055_*`, `R066_*`, `R122_*`
and the goldens `lights-from.txt`, `en_lights-from.txt` guard it. R-054's text and test lose
the partner's label on the lights-only session; `lights-only.txt` and `en_lights-only.txt`
change on their `eticheta` and `dark-uri (partener)` rows. ADR 0011 stands for the partner's
darks standing in for the session's; only its label clause is replaced here.
