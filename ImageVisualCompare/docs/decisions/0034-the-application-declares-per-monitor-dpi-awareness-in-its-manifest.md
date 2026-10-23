# 0034. The application declares per-monitor DPI awareness, v2, in its manifest

Date: 2026-10-22
Status: accepted 2026-10-22

## Context

ADR 0011 makes 100 % one image pixel per physical screen pixel: the viewport counts physical
pixels, and `CompareView` divides by the device scale WPF reports
(`VisualTreeHelper.GetDpi(this).DpiScaleX`, read on `Loaded` and in `OnDpiChanged`). The code
review of the documentation backfill (2026-10-21) found that the application declared no DPI
awareness: the csproj had no `ApplicationManifest`, the built executable and assembly carried
only the compiler's default manifest, and the process ran system-DPI-aware. WPF then reports the
system's scale on every monitor and raises no `DpiChanged` when the window moves, and Windows
stretches the window on a monitor scaled differently: with the main display at 100 %, "100%" on
a 150 % monitor is enlarged 1.5 times; the other way round, three image pixels go into two
physical ones, which averages away the one-level differences this tool exists to show. The
owner's three monitors are all at 100 %, where nothing differs. On 2026-10-22 the owner chose
per-monitor awareness over writing the limit into R-022, ADR 0011 and `rendering.md`.

## Decision

- `src/ImageVisualCompare.Wpf/app.manifest` declares `dpiAware` `true/pm` and `dpiAwareness`
  `PerMonitorV2, PerMonitor`, beside what the compiler's default manifest said (`asInvoker`);
  the Wpf csproj names it in `ApplicationManifest`, and the build embeds it in the assembly and,
  from there, in the executable Windows starts.
- Nothing else changes: WPF scales each window to its own monitor and raises `DpiChanged` on a
  move, and `CompareView` reads the scale as ADR 0011 says.

## Alternatives rejected

- **Writing the limit into R-022, ADR 0011 and `rendering.md`.** Two settings in a manifest fix
  what the documentation would only warn about, and WPF supports them without code.

## Consequences

- Measured on 2026-10-22 on the running application, `GetProcessDpiAwareness` and the main
  window's DPI awareness context: the build of 3326fe4 is system aware (1), this one per-monitor
  (2) with the per-monitor v2 context. On the owner's monitors, all at 100 %, the launch shows
  the same picture as before.
- A move between monitors scaled differently is checked only when the owner sets one monitor to
  another scaling; until then it is the reading of WPF's documented behaviour, not a run.
  (Restated 2026-10-23: run, with one monitor set to 150 %. The window's DPI went from 96 to
  144 on the move, and at `100%` a 48 × 48 patch of the captured window matched
  `de_printat.png` at scale 1 with a mean difference of 0.14 levels, against 1.41 at scale 1.5,
  which a window stretched by Windows would show;
  `docs/plans/2026-10-22-fix-the-defects-the-documentation-review-found.md`.)
- Guards: `AppManifestTests.R022_TheApplicationDeclaresPerMonitorDpiAwareness` reads the
  manifest from the built assembly and executable, as `AppIconTests` reads the icon.
