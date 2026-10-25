# 0004. The windows darken their title bar through DWM

Date: 2026-10-25
Status: accepted 2026-10-25

## Context

The launcher's two windows use LightsCheck's dark palette, but their title bars stayed the
system's light colour: the first version left the dark title bar out because it needs an
unsafe interop call the launcher had no other use for. The owner asked on 2026-10-25 for the
title bar to follow the dark theme too, as it does in the five tools the launcher starts
(DarksCheck, FlatCheck, LightsCheck, ImageVisualCompare and AstroPhotoAnalyzer each carry a
`DarkTitleBar`).

## Decision

Both windows ask the window manager for a dark title bar through a copy of LightsCheck's
`DarkTitleBar`, and the window project allows unsafe blocks for its one interop call.

## Alternatives rejected

- **Keep the light title bar.** The owner asked for the dark one; the launcher is the first
  window the suite shows, and it would be the only one with a light bar.
- **A custom chrome (`WindowStyle="None"` and a drawn bar).** It would have to redraw the
  buttons, the drag, the snap and the resize border the system gives for free, for two small
  windows.
- **A shared library for the suite's WPF pieces.** The components keep their own copies by
  their ADRs (LightsCheck 0008, DarksCheck 0009), and the launcher never changes a component.

## Consequences

`AllowUnsafeBlocks` is on in `AstroDarkroom.Wpf` only; `AstroDarkroom.Core` stays safe. The
copy is maintained apart from the tools' copies: a fix in one reaches the others only by hand.
Nothing tests it: the window has no test project (ADR 0001); it is checked by eye.
