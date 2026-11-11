# 0021. The front page opens with what the suite does and each tool, linked to its manual

Date: 2026-11-11
Status: accepted 2026-11-11
Supersedes: 0008 in part (the repository's other folders); 0020 in part (the front page's order)

## Context

The front page, the repository's root `README.md` (ADR 0008), opened with the download, then
described the suite and each tool in the words of a manual: the folders each one opens, the
files it writes, its tabs. On 2026-11-11 the owner asked for a front page that makes the case
for the suite to someone who has not yet decided to download it: a title, a short "What is Astro
Darkroom" (checking the calibration frames and the lights, comparing two versions of an image,
preparing it for print), a screenshot of the launcher, each tool in a few plain sentences with a
link to its manual in the wiki, then "What the tools need" and the build from a checkout as they
were, and the licence last. Since the same day the wiki has a manual, with pictures, for every
tool and for the launcher. The owner approved the wording proposed in chat and every
recommendation but one: the table of the repository's other folders goes.

## Decision

- The front page's order: the title and a one-line motto; "What is Astro Darkroom", in four
  bullets (calibration frames, light frames, processing, print), the highlights (free, Windows,
  no installer, two languages) and a link to the latest release; "The tools", the launcher's
  screenshot and one section per tool, titled by its name, linked to its wiki page, and a
  question, with a few plain sentences and a link to its manual; "Download"; "What the tools
  need"; "For developers", with the build of the suite from a checkout; "Licence" last.
- The repository's other folders are not listed.
- The screenshot is the wiki's own file, `img/AstroDarkRoom/main.PNG`, embedded by its
  raw.githubusercontent.com address, not a copy in the repository.
- "Download" and "What the tools need" say that FlatCheck alone needs Siril, as the wiki's home
  page does; the rest of those two sections is unchanged.

## Alternatives rejected

- **A table of the six tools with the launcher's two-line texts.** Shorter, but it tells too
  little to someone deciding whether to download.
- **A copy of the screenshot in the repository.** Two copies of one picture, and the front
  page's would go stale when the wiki's is replaced.
- **The first person** ("I built these tools for my own nights"). ADR 0019 stands: the page
  speaks to its reader as "you" and of the tests impersonally.
- **Keeping the table of the other folders.** The owner asked for it to go: those folders are
  not part of the suite.

## Consequences

- The front page shows the wiki's picture of the launcher: a new screenshot under the same name
  in the wiki changes it, and a renamed or moved one breaks it (the wiki's paths are
  case-sensitive).
- Each tool's paragraph restates what its manual says; a task that changes what a tool does
  checks its paragraph here, as ADR 0008 already asks for the rest of the page.
- A visitor sees the other folders only in the file list GitHub shows above the page.
