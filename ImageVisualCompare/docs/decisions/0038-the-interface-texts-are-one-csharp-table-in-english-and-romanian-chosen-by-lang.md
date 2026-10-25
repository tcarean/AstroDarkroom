# 0038. The interface texts are one C# table, English and Romanian side by side, chosen once by `--lang`

Date: 2026-10-25
Status: accepted 2026-10-25
Supersedes: 0006 in part (no rule in `CLAUDE.md` varies the kit's rule "Languages")

## Context

Until 2026-10-25 every text the application shows was English, written where it is used: in
XAML, in `MainWindow`, `MessageDialog`, `FileDialogService`, `VersionInfo`, `MemorySnapshot`,
`Bitmaps` and `ImagePairLoader`, and in Core in `ImagePairValidator`, `ImageInfo` and the FITS
reader, about sixty texts in twelve files. The owner asked for a Romanian interface beside the
English one, chosen by a command-line parameter `lang` with the values `en` and `ro`, English
when it is missing, translated in context rather than text by text, with the tests that assert
English texts left as they are. The texts could be kept in `.resx` files with satellite
assemblies, the usual .NET route, or in C# code. Romanian in a code file is what the kit's rule
"Languages" (the kit's ADR 0012) forbids, and ADR 0006 said no rule of this project varies it.

## Decision

- **One class, `UiText` in Core, holds every text in both languages.** Each text is a member
  that returns its English or its Romanian wording, written next to each other in one call, so a
  text cannot exist in one language only and the two are read and reviewed together. A text with
  values in it is a method with named parameters. `UiText.Current` is the table in use, English
  unless the start-up sets another; every place that writes a text reads it from there at the
  moment it writes, so the existing tests, which run in English, are unchanged.
- **The command line: `--lang=<code>` or `--lang <code>`**, anywhere, case-insensitive, the
  last one counting, never taken for a path (`StartupArguments.Parse`, R-100). `App` sets
  `UiText.Current` from it before the first window, once for the whole run.
- **An unknown value, or none, shows a message and goes on in English** (R-102). The message is
  English only, since the interface it announces is; it uses `MessageDialog`, not the system
  message box (ADR 0018).
- **The kit's rule "Languages" is varied for the Romanian texts only.** Romanian may appear in
  the Romanian wordings of `UiText.cs`, in the tests that assert them, and where a comment or a
  document quotes them; every comment, name and English text is otherwise English. `CLAUDE.md` states the variation as the rule "Two interface
  languages".
- **Numbers are written the same in both languages.** Only the user-interface language changes;
  `CultureInfo` is never set, so `5938 × 3888`, `152%` and `560 MB` read the same (and ADR 0017
  keeps the culture machinery whole).

## Alternatives rejected

- **`.resx` files with a satellite assembly for Romanian.** A key missing from the Romanian
  file falls back to English without a word, a format string's placeholders are checked by
  nothing until the message is shown (often a rare FITS error), the language would follow
  `CurrentUICulture`, so the English tests would need the culture forced on a Romanian Windows,
  and the strongly typed class is generated only by Visual Studio, not by `dotnet build`. Each
  of these can be made up for with a test or a setting; for two languages and sixty texts
  translated here, the compiler doing it was preferred. A third language, or a translator
  outside the project, would be the reason to revisit this.
- **Passing the table as a parameter instead of `UiText.Current`.** It would reach the FITS
  reader, the validator and `ImageInfo` through every signature and change most existing tests,
  for a value set once per run.
- **Changing the culture with the language.** Romanian formatting writes a decimal comma and
  groups thousands with dots wherever a number is formatted with a culture, now or in a later
  change; the owner asked that numbers do not change, and the language of the words is all the
  request is about.
- **Starting silently in English on an unknown value.** Recommended, and not chosen: the owner
  wants the mistake said.

## Consequences

- A new text is a new member of `UiText` in both languages, never a literal at the place that
  shows it; the XAML carries no text of its own, and the code behind assigns it where it
  shows it (the main window in its constructor, `MessageDialog` its buttons in `Show` and
  `Confirm`).
- The tests that switch to Romanian run in one collection with parallelisation off, since
  `UiText.Current` is one value for the process.
- Guards: the `R100_` to `R102_` tests (`StartupArgumentsTests`, `UiTextTests`, and the
  Romanian cases of the window and the loaders); the English texts stay guarded by the tests
  that assert them since R-001.
