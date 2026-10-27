# 0011. Each row shows its app's icon from a copy of the component's .ico, kept equal by a test

Date: 2026-10-27
Status: accepted 2026-10-27

## Context

On 2026-10-27 the owner asked for each app's icon beside its row in the launcher. Every
component draws its own icon (`Icon/<name>.ico` in five of them, `Assets/ImageVisualCompare.ico`
in ImageVisualCompare), eight or nine frames from 16 to 256 px, and names it as the
`ApplicationIcon` of its window project, so each window exe in the suite folder carries it.
The launcher's window is built from the catalogue in the core, which knows each app by file
names (R-001), and shows its own icon from an `.ico` resource whose largest frame WPF scales to
40 px (`docs/design/launcher.md`). Where the row's icon comes from was the one choice.

## Decision

Each component's `.ico` is copied into this repository's `Icon/` folder under its own file
name, built into the launcher as a resource like the launcher's own, and named by the
catalogue (`SuiteApp.Icon`, R-010). `SuiteTests` compares every copy's bytes with the file the
component's project names as its `ApplicationIcon`, so a component that redraws its icon turns
the gate red here until the copy is refreshed (the one task is `Copy-Item` of that file). The
row shows the largest frame scaled to 40 px, like the two logos, and dims it to the disabled
button's opacity when the app is missing from the suite folder, so the row keeps its face and
the dimming says the rest.

## Alternatives rejected

- **Read the icon out of the exe in the suite folder at run time.** It is the truest source,
  but a missing app would have no icon at all, the development build (every app missing)
  would show none, and a frame larger than 32 px needs `PrivateExtractIcons` through P/Invoke,
  a second unsafe import beside `DwmSetWindowAttribute` (ADR 0004), or a blurred 32 px icon
  from `System.Drawing`.
- **Link the components' files into the project by relative path.** The build would then
  depend on the sibling checkouts; the release already does (`kit.json`), but a clone of this
  folder alone would not build, where today it builds and only skips the sibling checks.
- **The launcher's own icon on every row.** It says nothing about the row.

## Consequences

`Icon/` holds seven files instead of one; six are copies, never drawn here, and `CLAUDE.md`
says a new app in the suite is also its icon copied there. The copies add about 260 KB to the
repository and to `AstroDarkroom.exe`. R-010 and its test guard the copies; the launcher still
never changes a component. The dimmed icon of a missing app is a second signal beside the
disabled button; the reason stays in the button's tooltip (R-002).
