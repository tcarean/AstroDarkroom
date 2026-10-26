# The interface language

> **Covers:** `src/ImageVisualCompare.Core/UiText.cs`, `src/ImageVisualCompare.Core/UiLanguage.cs`,
> `src/ImageVisualCompare.Core/StartupArguments.cs`, `src/ImageVisualCompare.Wpf/App.xaml.cs`,
> `tests/ImageVisualCompare.Tests/InRomanian.cs`.
> A change to the code this describes means this note is updated in the same task, or
> deleted with the decision it explains.

## How the language is chosen

`App.OnStartup` hands the command line to `App.ApplyCommandLine`, a static method a test can
run without an application. `StartupArguments.Parse` takes `--lang` and its value out (`--lang=ro`, or `--lang ro` with the next argument as the value, the last one
counting) and leaves the other arguments, in their order, as the candidates for the pair. It
sets `UiText.Current` to the table of the language before `StartupUri` builds the main window,
keeps the pair as before (R-002), and keeps a value it did not understand for the main window.
`MainWindow`'s `Loaded` handler takes that value (`App.TakeUnknownLanguage`) and shows the
message of R-102 before it opens the pair or asks for one; the message and its title are static
members of `UiText`, English only, because the interface they announce is English.

## How a text reaches the screen

`UiText` has one instance per language, `English` and `Romanian`, and every text is a member
that calls `Pick(english, romanian)`: the two wordings are written next to each other, the
compiler refuses a text with one of them missing, and the instance decides which one comes
back. A text with values in it is a method (`Decoding(fileName)`, `PadQuestion(...)`,
`FitsPlanes(planes)`); the values are put in as they are, so a number reads the same in both
languages, and no `CultureInfo` is set anywhere (ADR 0038, ADR 0017).

Every place that writes a text reads `UiText.Current` when it writes it, not before:

- Core: `ImagePairValidator.DescribeMismatch`, `ImageInfo.FormatDescription` (through `Format`,
  then the range of R-016 as it is), and the exceptions of `FitsHeader` and `FitsRowDecoder`,
  whose messages the user reads in the dialog of R-005.
- The imaging layer: `ImagePairLoader`'s first line of a failure, `Bitmaps.TooLarge` (the FITS
  wording or the general one, chosen by `fits`), `MemorySnapshot.Describe`.
- The shell: `MainWindow`'s constructor assigns the toolbar's words and the hint, which the XAML
  no longer carries; its titles, overlay lines and the padded canvas; `MessageDialog`'s buttons;
  `FileDialogService`'s titles and filter; `VersionInfo.About`, a computed property for that
  reason.

The Romanian keeps the words the user documentation used while it was in Romanian (ADR 0041):
the divider is "linia", the canvas "pânza", the user "tu". The format puts the channels first
and the depth after "pe", as that README wrote "PNG pe 16 biți"; `Bits` adds the "de" Romanian
puts after a number whose last two digits are 0 or 20 to 99 ("32 de biți"). Left as they are in
both: numbers, file names, `A` and `B`, `100%`, `×`, `—`, FITS keywords, `vs`, the name
`Image Visual Compare`, and the licence itself, which stays the English `LICENSE` file; the
About notice that points to it is translated. The messages of Windows and .NET (a codec's
failure, a missing file) come in their own language.

A new text is a new member of `UiText` in both languages, never a literal where it is shown.

## Tests

The tests of each area pin the English texts and run in parallel, with `UiText.Current` at its
default. The Romanian tests (`UiTextTests`) switch it with `InRomanian`, which restores the
previous table when disposed; because `Current` is one value for the process, they belong to the
collection `UiLanguageCollection`, whose parallelisation is off, so xUnit runs them alone after
the others. `R101_EveryText_IsWordedInBothLanguages` reads every string property of the two
tables and fails on a Romanian wording equal to the English one, `OK` aside; the methods are
checked by the tests that call them. `StartupArgumentsTests` pins the parsing, and the
`R100_` and `R102_` tests of `UiTextTests` the start-up (`App.ApplyCommandLine`); the message
the main window shows for an unknown value is a modal dialog, checked by launching.
