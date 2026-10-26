# 0006. The interface speaks English or Romanian, chosen in the window and remembered, and a tool starts in the launcher's language

Date: 2026-10-26
Status: accepted 2026-10-26
Supersedes: 0003 in part (Romanian as the only language of what the user sees)

## Context

Until 2026-10-26 every text the launcher shows was Romanian without diacritics (ADR 0003): the
catalogue's five descriptions, the reason of a missing tool, the main window's labels and the
"Despre" window, some twenty texts in six files. On 2026-10-25 the five components gained an
English interface beside the Romanian one, chosen by `--lang` and English when it is missing;
the launcher, which sits beside them and starts them, stayed Romanian, and a tool it started
opened in English. The owner asked for the two languages in the launcher too, chosen from the
window, applied at once, remembered for the next start; translations made in context; the
existing tests, which assert Romanian texts, kept on the Romanian wording with as few changes
as possible, texts allowed to change and figures not.

Asked in this session, the owner chose: **English at a first start** without a saved setting,
like the five components; **the launcher passes its language to the tool it starts**; the
setting in **`%LOCALAPPDATA%\AstroDarkroom\settings.json`**, where LightsCheck and DarksCheck
keep theirs.

## Decision

- **Both wordings sit in place, `T("english", "romana")`** (`Ui.T`, `using static`), where the
  text is composed: FlatCheck's shape (its ADR 0062), the one four of the five components use.
  `Ui` is a copy of FlatCheck's: `UiLanguage`, `Ui.Default`, the `Ui.Use` scope on an
  `AsyncLocal`, `Ui.Current`, `Ui.T`; public here, since the window reads it. No culture is set,
  so a version number reads the same in both languages.
- **The language is switched live, not at start-up.** The main window has a selector in its
  bottom bar, two RadioButtons drawn as buttons, each language named in itself (`English`,
  `Romana`). A check sets `Ui.Default`, writes the setting and calls `Relabel()`, which assigns
  every label and rebuilds the rows; the XAML carries no user text. The "Despre" window is
  modal and built at every open, so it reads the language once, in its constructor.
- **The setting is one JSON file, `{"language":"ro"}`**, read by `LanguageSetting.TryRead`
  with the same no-model-binding parsing as `release.json`: anything it cannot read is no
  language, and the launcher starts in English (`Ui.StartupDefault`); a write that fails is
  ignored, the choice holds for the run.
- **A tool starts in the launcher's language**: `LaunchPlan` carries the arguments, and
  `Launcher.Plan` gives `--lang=ro` or `--lang=en`, the one-token form every component's window
  accepts.
- **The Romanian wordings are the ones the user read until now, byte for byte**; English is
  ASCII. The test assembly sets `Ui.Default` to Romanian in a module initializer, so the tests
  written before this ADR assert Romanian unchanged; a test of the English wording opens a
  `Ui.Use` scope.

## Alternatives rejected

- **One table of every text (ImageVisualCompare's ADR 0038).** Fine for its sixty labels; here
  the suite's majority shape was preferred, and twenty texts beside their code are read in
  context, which is what the owner asked of the translation.
- **A ComboBox for the selector.** The planned control; WPF's default ComboBox brings the
  system's light chrome into the dark window and a dark one needs a full template with a
  popup. Two RadioButtons reuse the buttons' look and show both languages at a glance.
- **`{l:T En= Ro=}` in the XAML (FlatCheck's `TExtension`).** It picks a wording once, when the
  element is made, and cannot re-pick on a live switch; assigning the labels in one method can.
- **Passing the language to `Launcher.Plan` as a parameter.** The ambient `Ui.Current` is
  already the window's language; a parameter would change the signature R-003's tests call.
- **Romanian at a first start.** The launcher was Romanian until now, but the five tools it
  starts are English by default; one language across the suite at a first start was chosen.
- **The setting beside the executable.** It would travel with the unzipped folder, but could
  not be written from a read-only folder; the components' convention was kept.
- **`.resx` files and `CurrentUICulture`.** A missing key falls back silently and the language
  would follow the culture, which the owner asked to leave alone.

## Consequences

- A new text is a `T(english, romanian)` pair where it is shown, assigned from `Relabel()` in
  the main window; the catalogue's descriptions are pairs in `AppCatalogue.Build`.
- `CLAUDE.md`'s rule on languages is reworded: what the user sees is in both languages,
  Romanian without diacritics, and everything else stays English.
- Guards: `LanguageTests` (R-006 to R-008); the Romanian texts stay guarded by the tests that
  asserted them before (R-001 to R-003).
