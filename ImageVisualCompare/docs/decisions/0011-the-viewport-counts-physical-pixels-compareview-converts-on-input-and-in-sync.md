# 0011. The viewport counts physical pixels, so 100 % is one image pixel per screen pixel; CompareView converts on input and in Sync

Date: 2026-10-02
Status: accepted 2026-10-02

## Context

Retroactive: recorded on 2026-10-02 from `CLAUDE.md` ("Invariants", "Physical pixels below,
device-independent pixels above"), the summary of
`src/ImageVisualCompare.Wpf/Controls/CompareView.xaml.cs`, the comment on `RenderPlanner.For` in
`src/ImageVisualCompare.Core/RenderPlan.cs` and the paragraph of `README.md` on what 100 % means;
the decision itself dates from a6e5f10 (the base version, 2026-09-08), and the render-side
conversion was extracted into `RenderPlanner` in 801bda5 (refactoring and new tests,
2026-09-08). WPF lays out in device-independent pixels (DIPs), and with Windows display scaling
at 125 % or 150 % a DIP is more than one physical pixel. For pixel peeping the README wants the
honest 100 %: one image pixel over one physical screen pixel, not over a logical one. The comment
on `RenderPlanner.For` names the risk: getting the conversion backwards "is invisible on an
unscaled display and wrong everywhere else".

## Decision

- `Viewport` works entirely in physical screen pixels; its content space is image pixels.
  `Viewport.Scale` 1.0, which `ZoomToActualSize` sets, is one image pixel per physical screen
  pixel.
- The conversion is confined to `CompareView` and runs one way at each end, with `_deviceScale`
  (`VisualTreeHelper.GetDpi(this).DpiScaleX`, read on `Loaded` and in `OnDpiChanged`). On the
  way in, every WPF position or size is multiplied by it before it reaches the viewport: in the
  mouse handlers (the wheel anchor through `ToDevice`, the pan delta, the cursor point of the
  pixel readout), in `UpdateViewportSize` and in the centre anchor of `ZoomAtCentre`;
  `WheelPanPixels` (140) is in physical pixels already. On the way out, `Sync` passes it to
  `RenderPlanner.For`, whose `RenderScale` is the viewport's scale divided by `deviceScale`; the
  translation is the negated offset in content units, applied before the scale, and needs no
  conversion.
- The divider is the exception and stays in DIPs throughout: its position is a fraction of the
  window and never touches image coordinates, so `DividerModel` receives `ActualWidth`,
  `ActualHeight` and the mouse position unconverted.

## Alternatives rejected

- **The viewport in DIPs, WPF's own unit.** 100 % would then put an image pixel over a logical
  pixel, which at 125 % or 150 % scaling is 1.25 or 1.5 physical pixels: not the honest 100 %
  the README promises for pixel peeping.
- **Converting wherever a coordinate is used.** A conversion missed or applied twice is
  invisible on an unscaled display, so it would pass every look at 100 % scaling; confined to
  one control, with the outbound half in a pure function, it has one place to be read and half
  of it is tested.

## Consequences

- A conversion error in `CompareView` shows only on a scaled display: at 100 % scaling the two
  spaces are the same.
- The arrow keys move the divider by 1 or 10 DIPs (`OnKeyDown`); the README calls them px, which
  they are at 100 % scaling only.
- The summary of `src/ImageVisualCompare.Core/Viewport.cs` says "Screen space is measured in
  device-independent pixels", against the code and `CLAUDE.md`, and has said so since a6e5f10.
  The code's meaning is the decision; the comment is stale, left for a task of its own.
  (Corrected 2026-10-02: the summary now says physical screen pixels and that the view
  converts before it calls in; `docs/plans/2026-10-02-copy-the-licence-next-to-the-program-and-fix-stale-texts.md`.)
- Guards: `RenderPlanTests` for the render side (the render scale divided by the device scale,
  and 100 % on a scaled display landing on one physical pixel); the input side none by test, and
  the application has to be launched and looked at on a scaled display (ADR 0026).

Source: CLAUDE.md ("Invariants", "Physical pixels below, device-independent pixels above");
src/ImageVisualCompare.Wpf/Controls/CompareView.xaml.cs (summary, input handlers,
`UpdateViewportSize`, `ZoomAtCentre`, `Sync`); src/ImageVisualCompare.Core/RenderPlan.cs;
src/ImageVisualCompare.Core/Viewport.cs (summary); README.md (the paragraph on what 100 % means).
