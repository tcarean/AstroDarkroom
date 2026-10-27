# 0053. The window shows, clears and remembers the ICC profile, and re-runs when it changes

Date: 2026-10-27
Status: accepted 2026-10-27

## Context

Since ADR 0030 the window takes the paper's Dmax from an ICC profile ("Din profil ICC...",
R-132), but the choice was visible nowhere once made: the Dmax field showed a number, and the
profile's name appeared only in Anexa F after the next "Analizeaza". The name was kept lazily,
in four private strings of `ParameterFields`, and reached the report only while the field still
showed the text the profile wrote. Nothing was remembered between runs of the window, so a lab
profile chosen from a file had to be found again at every start. On 2026-10-27 the owner asked
for the profile in use to be shown plainly in the top bar beside the image's name, with a small
"Clear" button; for the analysis to restart by itself when another profile is chosen or the
current one is removed; and for the chosen profile to be saved and still selected at the next
start ("As vrea ca profilul de ICC selectat curent sa fie vizibil clar in bara unde este si
numele imaginii... un mic buton de Clear... sa repornesti analiza... sa se salveze").

## Decision

1. The profile is a state of `ParameterFields`, `Profile`, an `IccProfileRow`, and the bar binds
   it. The drop is eager: typing another Dmax over the profile's, or choosing a medium, sets it
   to null at once, so the bar never names a profile the next run would not use; retyping the
   profile's Dmax does not bring it back.
2. "Foloseste" and "Scoate" re-run the analysis on the open image; a choice that changes
   nothing (the profile in use, its Dmax still in the field) runs nothing; both buttons are
   disabled while a run lasts. A typed Dmax applies at "Analizeaza", as every field does.
3. The profile's path is remembered in `%LOCALAPPDATA%\AstroPhotoAnalyzer\settings.json`, key
   `iccProfile`, written on every change of the bar's profile through a temporary file and one
   move (AstroDarkroom's `LanguageSetting` pattern), by `WindowSettings`, a class of the Wpf
   project: window state, nothing computed, so not the core's. A read or a write that fails
   changes nothing and throws nothing. The parameterless `MainViewModel` and `MainWindow`
   remember nothing, so no test touches the user's file.
4. At the start the profile is read again from its file by the core: readable, it is applied
   as if chosen; missing or unreadable, it is shown in red with the core's reason and not used,
   until "Scoate" or another choice. The medium is not remembered.

## Alternatives rejected

- **Keeping the lazy drop.** The bar would name a profile while the field held another Dmax,
  and the report would say "explicit": a bar that lies.
- **A settings class in the core.** The core computes and formats; what the window remembers
  is window state, and the console has `--icc` for the same purpose. The kit's rule
  "Computed only in the core" is about numbers and sentences, not about a path in a file.
- **The registry, or WPF's `Settings.settings`.** A JSON file beside AstroDarkroom's is
  readable by hand, portable between the suite's tools, and tested on a path of the test's
  own; the registry is neither, and the settings designer ties the schema to the assembly's
  version.
- **Forgetting an unreadable profile silently.** A profile picked from a Downloads folder and
  deleted since would vanish without a word, and the user would wonder which Dmax the run
  took; shown in red with the reason, the bar says what happened and "Scoate" ends it.
- **Remembering the medium too.** Not asked; the medium's Dmax is overwritten by the profile's
  anyway, and the default (baryta) is the common case.

## Consequences

- R-155 and R-156 state the behaviour; `IccPickerTests`, `ProfileBarTests`,
  `WindowSmokeTests` and `WindowEnglishTests` guard it, with settings files of their own.
- A second remembered setting goes into `WindowSettings` as another key; the file stays one
  JSON object.
- The window built by `App` reads the user's file at every start; a profile that moved shows
  in red until it is chosen again from its new place.
