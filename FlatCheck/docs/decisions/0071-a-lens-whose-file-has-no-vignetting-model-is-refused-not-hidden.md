# 0071. A lens whose profile file carries no vignetting model is refused, not hidden

Date: 2026-10-27
Status: accepted 2026-10-27

## Context

Adobe's lens profile folder holds files for lenses measured for distortion or colour only:
1275 of the 3531 `.lcp` files on the author's machine on 2026-10-27 have no `VignetteModel`
(the extender combinations among them, "Canon RF 100-400mm F5.6-8 IS USM + EXTENDER RF2x").
`--lenses` and the window's list name every file by its name, without parsing it (ADR 0066:
listing 3500 files must not parse them), so such a lens can be chosen. Chosen, its file parsed
to zero profiles, `LensCatalog.Nearest` returned null and `SyntheticFlatBuilder.Build`
dereferenced it: the console printed `ERROR: Object reference not set to an instance of an
object.` and the window "The analysis failed". The review of 2026-10-27 found it (A3); the
comment beside the window's format note said the write would refuse it with the reason, which
was not so.

## Decision

`SyntheticRun.Execute` parses the chosen lens's files before building and, when they hold no
vignetting profile, refuses with `SyntheticReport.NoProfile`: the lens's title, that its file
describes distortion or colour only, and to choose another lens. Exit 1, an input refusal like a
missing focal length (R-071); the window shows it under the synthetic refusal's title. The
lists stay as they are: a lens without a vignetting profile is listed, since listing reads names
only, and `LensCatalog.Parse` writes a Debug line when a file parses to no profile.

## Alternatives rejected

- **Hide such lenses from the lists.** It needs every file parsed at `Locate`, which ADR 0066
  ruled out for 3500 files, or a parse of the whole catalog on the first `--lenses`; and the user
  who types the lens's name would get "no lens matches", which is false.
- **Refuse only in the builder, with a null check.** The refusal would come out as an exception,
  exit 1 by way of `ERROR:` without the reason, and the window would still say "The analysis
  failed".

## Consequences

- R-071 names the case; `R071_ALensWhoseFileHasNoVignettingModelIsRefusedWithTheReason` runs it
  through the run and through the console.
- The exit-code table in `FlatCheckCli.cs` and `docs/design/thresholds.md` name it beside the
  other input refusals.
