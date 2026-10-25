# 0048. The interface speaks English or Romanian, chosen by `--lang`, each text a pair where it is used

Date: 2026-10-25
Status: accepted 2026-10-25
Supersedes: 0005 in part (the user-visible texts are no longer Romanian only)

## Context

Until 2026-10-25 every text the user sees was Romanian: the report and its recommendations, the
curves' texts, the errors, `--help`, the window's XAML and view-models, about a thousand texts
in some forty files, many of them built deep in the analysis (a class's name, a method's note, a
cloud list). The owner asked for an English interface beside the Romanian one, chosen by a
command-line parameter `lang` with the values `en` and `ro`, written `--lang=ro` or `--lang ro`,
**English when it is missing**, in the console and in the window alike; the translation made in
context, not text by text; the tests that assert Romanian kept asserting Romanian, with as few
changes to the existing tests as possible, texts allowed to change in them and figures not.

ImageVisualCompare had made the same change the same day (its ADR 0038) with one central table
of its sixty texts. Asked on 2026-10-25, the owner chose for this project the English **beside
each Romanian text, where it is used**; an unknown `--lang` value as an **error** in the console
and a **message, then English** in the window; and `README.md` **Romanian only**, documenting
`--lang`.

## Decision

- **Each text is a pair at its use: `Tr.Pick(english, romanian)`** (`Language.cs`), and in XAML
  the markup extension `{local:T En='...', Ro='...'}`, which picks once when the XAML loads. The
  Romanian wording stays byte for byte what it was; the English is written beside it, read with
  the sentences around it and the threshold it names. A text that was a `static readonly` field
  or a constant becomes a property or a method, so that it is picked when it is shown and not
  when the type loads; a text code compares or keys on keeps an identifier apart from its label.
- **The language is ambient, `Tr.Current`**: an `AsyncLocal` scope (`Tr.Use`) over a process
  default (`Tr.SetDefault`), English unless set. The console opens a scope for its run; the
  window sets the default before its first window. Passing the language as a parameter would
  reach every analyzer that names something and most of the existing tests, for a value set once
  per run; the scope, unlike a plain static, keeps two console runs in two languages apart when
  the tests run them in parallel.
- **One parser, `UiLanguageArgs`, in the core**, for both applications: `--lang=x` or
  `--lang x`, anywhere, case-insensitive, the last one counting (R-150). The console reads it
  before anything else, so the help and every error are already in the chosen language; the
  options and the window's file argument are parsed without it (`UiLanguageArgs.Strip`).
- **A wrong value is said, in English** (R-152): the console prints `ERROR: ...` and exits 1,
  like any wrong argument; the window opens in English and shows the sentence in a message.
- **The tests run in Romanian.** Each test assembly sets the process default to Romanian in a
  module initializer, so the Romanian assertions written before this ADR stand unchanged. A
  console run sets its own language, English without `--lang`, so the console tests that assert
  Romanian pass `--lang ro` (their run helpers and four direct calls), and nothing else of them
  changes. The eight Romanian goldens stay byte for byte, except the `--lang` line `help.txt`
  gains; each gets an English twin (`Golden/*.en.txt`), and a test looks for Romanian words in
  the English output (R-153).
- **Numbers do not change with the language** (R-154): `CultureInfo` is not touched and `Fmt`
  stays as it is, so a figure reads the same in both.
- **`README.md` stays the user documentation, Romanian only**, and documents `--lang`; an
  English README would be a second copy of the usage, the drift the rule "User documentation
  lives in `README.md`, and only there" exists against.
- **The rule "Languages" in `CLAUDE.md` is varied**: the user-visible texts are in both
  languages, the English beside the Romanian; everything else is as ADR 0047 says.

## Alternatives rejected

- **One central table of every text, as ImageVisualCompare's ADR 0038.** Consistent with the
  sibling, but a thousand texts would move away from the code that builds them, and the context
  the owner asked the translation to keep would be one jump away in both directions; the diff
  would rewrite every text's call site anyway. The owner chose the pair at the use.
- **`.resx` files and satellite assemblies.** The same distance from the context as the table,
  a missing key falls back silently, the placeholders of an interpolated text are checked by
  nothing, and the language would follow `CurrentUICulture`.
- **A plain static for the language.** The console tests run in parallel; one run in English
  would turn another's Romanian report English half-way.
- **Starting silently in English on an unknown value.** Not chosen: the owner wants the mistake
  said.
- **Changing the culture with the language.** A decimal comma in Romanian would change every
  figure's text; the owner asked that figures do not change.

## Consequences

- A new text is a pair from its first line, in both languages; `docs/design/extending.md` says
  so, and the English goldens with the Romanian-word test catch a text left Romanian.
- A change of a Romanian text changes its English in the same edit, and the reverse.
- Guards: the `R150_` to `R154_` tests (`LanguageTests`, the English goldens, the window's
  English pass); the Romanian texts stay guarded by every test that asserted them before.
