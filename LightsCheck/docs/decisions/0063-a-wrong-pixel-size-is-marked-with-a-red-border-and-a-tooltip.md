# 0063. A wrong pixel size is marked with a red border and a tooltip

Date: 2026-11-16
Status: accepted 2026-11-16

## Context

R-029 says of the window's pixel field that "a blank field is not marked invalid, a wrong value
is", and ADR 0026 that a wrong value is "marked". The view-model computed and raised
`PixelIsValid` and nothing bound it, so the field looked the same whatever was typed. The
review of 2026-11-13 (CR-016, `confirmed`) gave the case: `3,76` typed with a comma, the
decimal sign of a Romanian keyboard, is not a number for the field, the run goes on as if the
field were blank, and the only trace is the source named on the `scale` row. How the field is
marked was never decided. DarksCheck marks its settings fields with a red border and a tooltip
that says what is wrong and what happens (its `SettingField` style).

## Decision

While the pixel field holds a wrong value a border in the theme's bad colour (`BadBr`) is drawn
over its own and the field gets a tooltip of its own, `Not a positive number (with a dot as the separator): the
pixel size is taken as if the field were empty` (`Nu e un numar pozitiv (cu punct): marimea
pixelului se ia ca si cum campul ar fi gol`), the owner's approval of the plan of 2026-11-16.
A blank field and a right value look as before. The style, `SettingField`, takes DarksCheck's
idea, a trigger on the field's `Tag`, which the window binds to `PixelIsValid`, and draws the
mark another way: the red border is an element of its own in the field's template, shown by
that trigger and touched by no other. Measured on 2026-11-16, after the review of the branch:
WPF's stock template of a text box (its Aero2 theme's) has three triggers, `IsEnabled=False -> border.Opacity
0.56`, `IsMouseOver=True -> border.BorderBrush #FF7EB4EA` and `IsKeyboardFocused=True ->
border.BorderBrush #FF569DE5`, which set the drawn border directly, over the field's
`BorderBrush`. A red `BorderBrush` on the field, the first form of this fix and what DarksCheck
does, therefore shows only while the field has neither the keyboard nor the pointer: not while
the wrong value is being typed, and never together with the tooltip. The template is the stock
one, its three triggers and two blues copied so the field looks like the thread field beside
it, plus the mark. The field's tooltip shows while a run disables the field as well
(`ToolTipService.ShowOnDisabled`).

## Alternatives rejected

- **The border alone.** It says that something is wrong and not what: the comma is the likely
  mistake, and only words can name the dot.
- **A line under the top bar**, where the export's result goes. That line is about what the
  window did; a field being edited would write over it at every key.
- **Refusing to run with a wrong value.** R-029 lets the run go on with the files' own pixel
  size, which is nearly always there.

## Consequences

Over the marked field the pointer shows the field's own tooltip in place of the longer one of
the `PIXEL (um)` input, which stays on the label above it. The stock template's two blues are
now written in the theme; the stock template comes with .NET's WPF, so a .NET that changed
them, or a Windows theme for which WPF takes another stock template (high contrast), would
leave this one field unlike the thread field beside it. That the mark stays in sight under
the keyboard and the pointer is read from the template, where no other trigger touches it: a
test cannot give an unshown window either. DarksCheck's settings fields have the gap this one
had. The thread count field is not
marked: R-029 gives it no invalid state, a wrong count meaning the default. Guarded by
`R029_AWrongPixelSizeIsMarkedAndABlankOrRightOneIsNot`.
