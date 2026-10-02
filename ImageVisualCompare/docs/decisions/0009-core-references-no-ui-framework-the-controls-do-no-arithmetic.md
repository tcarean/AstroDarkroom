# 0009. Core references no UI framework, with its own PointD, SizeD and RectD; the controls do no arithmetic of their own

Date: 2026-10-02
Status: accepted 2026-10-02

## Context

Retroactive: recorded on 2026-10-02 from `CLAUDE.md` ("Structure", the list under
`src/ImageVisualCompare.Core/` and "The last four exist so that the controls do no arithmetic of
their own"; "Testing"), the summaries of `src/ImageVisualCompare.Core/PointD.cs` and
`src/ImageVisualCompare.Core/Geometry.cs`, and
`src/ImageVisualCompare.Core/ImageVisualCompare.Core.csproj`; the decision itself dates from
a6e5f10 (the base version, 2026-09-08) for a core without UI types, and from 801bda5
(refactoring and new tests, 2026-09-08) for the four layout records. The base version already
kept zoom and pan in a `net9.0` library with a `PointD` of its own, but the WPF layer had no
tests at all and still computed some numbers itself, among them the minimap rectangle and the
divider's clip geometry. `CLAUDE.md` as 591f433 wrote it named moving those calculations into
Core, "where they can be tested", as the next commit; 801bda5 was that commit, and took the suite
from 79 test cases to 126. What stays in the WPF layer can only be checked by launching the
application and looking (`CLAUDE.md`, "Testing").

## Decision

- `ImageVisualCompare.Core` targets `net9.0`, without `UseWPF` and without a package, so no UI
  type compiles in it. The geometry it needs is its own: `PointD`, `SizeD` and `RectD`, record
  structs, and `AspectFit`, declared there instead of `System.Windows.Point` and
  `System.Windows.Rect`, so Core needs no `WindowsBase`.
- The controls do no arithmetic of their own. A number a control puts on screen is computed in
  Core and handed over in a record the control only assigns: `RenderPlan` (from
  `RenderPlanner.For`), `DividerLayout` (from `DividerModel.Layout`), `MinimapLayout` (from
  `MinimapGeometry.Layout`) and `ScrollBarState` (from `ScrollBarStates.Horizontal` and
  `Vertical`), each with its test class.
- The kit's ADR 0002 has made a library without UI the rule for every project made from the kit
  since 2026-09-24; this project had its own form of it from its first commit, and this ADR
  records that form.

## Alternatives rejected

- **`System.Windows.Point` and `System.Windows.Rect` in Core.** They live in `WindowsBase`; the
  zoom, pan and layout maths would carry a UI framework reference and could not be tested
  without a WPF host (the `PointD` and `RectD` summaries).
- **The arithmetic left in the controls**, as the base version left the minimap rectangle and
  the divider's clip. A number computed in a control can only be checked by launching the
  application and looking at it; in a Core record it has a unit test.

## Consequences

- If a number looks wrong on screen, its test is in Core: `CompareView.Sync`, `UpdateOverlay`
  and `UpdateScrollBars`, and `MinimapView.Refresh`, read a record and assign properties.
- What the controls keep is what a unit test cannot reach without UI automation: event wiring,
  mouse capture, property assignment. A few one-line computations remain there too: the line's
  left edge (`layout.LineX - DividerLine.Width / 2`), the zoom badge's percentage, the centre
  anchor in `ZoomAtCentre`, the thumbnail's scale in `CreateThumbnail`, and the conversions of
  ADR 0011.
- Guards: the compile boundary itself (Core targets `net9.0` without WPF, so a WPF type does not
  compile there), and `ViewportTests`, `RenderPlanTests`, `DividerLayoutTests`,
  `MinimapGeometryTests`, `ScrollBarStateTests`, `AspectFitTests` and `RectDTests` (the last two
  in `GeometryTests.cs`).

Source: CLAUDE.md ("Structure", "Testing"), and as 591f433 and 801bda5 wrote it ("Testing");
src/ImageVisualCompare.Core/PointD.cs; src/ImageVisualCompare.Core/Geometry.cs;
src/ImageVisualCompare.Core/ImageVisualCompare.Core.csproj;
src/ImageVisualCompare.Wpf/Controls/CompareView.xaml.cs; the kit's ADR 0002.
