# 0048. A choice of lights or partner analyses the folder again at once

Date: 2026-11-10
Status: accepted 2026-11-10
Supersedes: 0009 in part (a partner chosen after a run applied on the next run, offered by the Index tab); 0036 in part (the hint of `Analizeaza din nou`)

## Context

On the Potrivire cu lights tab (R-122) and the Index tab (R-073), a choice of lights or of
partner after a run only said so under the top bar (`... s-a schimbat: apasa "Analizeaza din
nou" ...`); the analysis waited for the `Analizeaza din nou` button on the card. The owner,
2026-11-10: when a choice is made, analyse again at once, with no button. Asked about the two
buttons, the owner chose to remove them over keeping them for a changed setting of the top bar.

## Decision

`MainViewModel.ChoosePartnerAsync` and `ChooseLightsFromAsync` set the choice and run the open
folder again, the settings of the top bar read anew. Every choice the dialog returns runs, the
same folder again included (as the problem card's `Alege partener...` did, R-077), and so does a
clear (`Fara partener`, `Lights-urile folderului`); without a folder open the choice is only
set. The two `Analizeaza din nou` buttons, their hint and the two status lines are gone. The
problem card's button is the Index tab's handler. A changed setting of the top bar takes effect
at the next run: a choice, or the folder opened again.

## Alternatives considered

- **Run only when the choice differs from the one in force.** Choosing the same folder again is
  then a dead click, and with the button gone it is the one way left to run again from the card.
  Rejected.
- **Keep `Analizeaza din nou` for the settings.** The owner chose to remove it; the top bar's
  four settings are rarely changed, and `Alege folder...` runs them.

## Consequences

R-073, R-077, R-087 and R-122 say so; the window logs `partner chosen:` and `lights chosen:` with
whether the folder runs again, and the clears. A run takes about a minute on a real session
(63 to 90 s on Smida), now started by the choice itself; the stop button stays.
