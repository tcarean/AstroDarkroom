# 0051. The window lists the catalogued clouds in the map window only

Date: 2026-10-27
Status: accepted 2026-10-27

## Context

With a WCS, the Lynds catalogue gives every cloud in the frame with at least
`DustCloudMinPatches` patches a row: its name, its patch count, its L\* and the target at which
it reaches black (ADR 0025). The report prints the darkest `DustCloudReportRows` of them in
Appendix B. The window showed the rows twice: the Tonuri tab, under the simulation and the
`Dust / sky map` button, repeated the report's mono table with every row, and the map window
listed the same clouds beside its check boxes, each caption carrying the same name, L\*, target
and patch count (R-130). On the reference FITS the tab's table ran to 114 rows (V-004)
between the simulation and the level-L\* ladder, and said nothing the map's list did not. On
2026-10-27 the owner asked for the tab's table to go and the map's list to stay ("Vreau sa scoti
de pe pagina de tonuri tabelul cu norii. O sa ramana doar in fereastra cu dust/sky map").

## Decision

The map window's list is the one place the window lists the catalogued clouds; the Tonuri tab
keeps the `Dust / sky map` button and sends the reader there. The tab's `CloudRows` is gone from
the view-model, and its guide paragraph says the button shows the clouds by name with their L\*
and target. The console report is unchanged: it has no map, so its table stays.

## Alternatives rejected

- **Keeping both.** Two copies of the same figures on screen, the tab's one long on a wide field,
  with nothing to tell the reader which to read.
- **Dropping the map's list instead.** The list is the map's set of per-cloud check boxes; the
  circles and labels cannot be switched one by one without it.
- **Keeping the darkest `DustCloudReportRows` on the tab.** Still a second copy, and a
  truncated one, of what the map shows in full.

## Consequences

- R-113 says the tab has no cloud table; R-130 says the caption of each cloud in the map's list
  carries its name, L\*, target and patch count. `R113_TheTabStatesTheDefinitionWarnsWithoutAWcsAndOffersTheMap`
  asserts the view-model has no `CloudRows`; `R130_TheMapCarriesTheFiveLayersTheCloudsAndTheirCaptions`
  asserts the captions.
- `ReportWriter.CloudHeader` and `CloudLine` serve the report and its tests alone.
- The README's Tones row and map paragraph say where the clouds are listed.
