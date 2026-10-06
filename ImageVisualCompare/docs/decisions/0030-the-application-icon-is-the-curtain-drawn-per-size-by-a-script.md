# 0030. The application icon is the curtain, drawn at each size by a committed script

Date: 2026-10-06
Status: accepted 2026-10-06

## Context

The application had no icon: the executable, the caption and the taskbar showed the generic
Windows one, which says nothing among a dozen astro tools in a taskbar. On 2026-10-06 six
drawings were put to the owner in the dark palette of `Theme.xaml`: the curtain (a star field
split by the divider and its handle), a split star, the letters A | B, a magnifier over pixels,
grey steps one level apart, and a galaxy half mono and half colour. The owner chose the
curtain. It is the application's own picture: the turquoise divider with the handle of the real
one, a dimmer processing left of it and a brighter one right of it. Nothing in the project could
produce an `.ico`, and the rule "No imaging dependency" excludes a package for it.

## Decision

The icon is the curtain. `src/ImageVisualCompare.Wpf/Assets/make-icon.ps1` draws it with WPF's
drawing classes from Windows PowerShell and writes `Assets/ImageVisualCompare.ico`, 16 to 256 px
in nine sizes, each drawn at its own pixel size; the file is committed, and the csproj's
`ApplicationIcon` embeds it. No window sets `Icon` of its own. R-064 states the behaviour.

## Alternatives rejected

- **One of the other five drawings.** The owner's choice; the split star was the other
  recommendation, the A | B letters say less about astro images, and the grey steps vanish
  at 16 px by design.
- **One 256 px drawing scaled down by Windows.** The 2-pixel divider lands between pixels
  and blurs at 16 and 24 px, and the stars disappear; the per-size drawing keeps both.
- **An SVG source and an external converter (Inkscape, ImageMagick).** A tool installed by
  hand on whoever regenerates it, for nine bitmaps that WPF, already on every machine that
  builds the project, can draw.
- **`Icon` set on each window in XAML.** A WPF window without one already takes the
  executable's; a second reference would be one more place to keep in step.

## Consequences

- The caption, the taskbar, Explorer and the dialogs the main window owns show the icon; seen
  on the running application on 2026-10-06.
- Changing the drawing means editing and running the script, then committing the new `.ico`;
  the build never runs the script.
- Guards: `AppIconTests` (the nine sizes, 32 bits, in the file; one icon group with the nine in
  the built application, which fails without `ApplicationIcon`).

Source: src/ImageVisualCompare.Wpf/Assets/make-icon.ps1;
src/ImageVisualCompare.Wpf/ImageVisualCompare.Wpf.csproj; docs/requirements.md (R-064);
docs/design/shell.md ("The icon").
