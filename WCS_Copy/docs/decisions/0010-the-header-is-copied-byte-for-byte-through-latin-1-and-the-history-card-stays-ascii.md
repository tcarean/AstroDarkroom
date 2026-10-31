# 0010. The header is read and written byte for byte through Latin-1, the HISTORY card stays ASCII through a frozen decomposition table, and the log writes a byte above 0x7F as `\xNN`

Date: 2026-10-31
Status: accepted 2026-10-31

## Context

`FitsHeader` decoded each card with `Encoding.ASCII` and encoded every card back with it. A
byte above 0x7F therefore came out as `?`: in a copied card, and in a card of the destination
the copy never touched, since the whole header is rewritten; the HISTORY card, built from the
source's file name, got a `?` for every letter outside ASCII. The third review of the log task
(ADR 0009, 2026-10-30) found this, and the owner chose then to keep ASCII and pin it (R-010,
`fits-header.md`, the rule "A card is copied verbatim" in `CLAUDE.md`, the test
`R010_Run_AByteAboveAsciiInACopiedCard_IsWrittenAsAQuestionMark`), since the FITS standard
allows only printable ASCII in a header. Measured by the owner on 2026-10-30: of 3873 FITS files
under `E:\Photografie`, no header holds a byte above 0x7F and no file name holds a letter outside
ASCII. The change is one of principle, decided by the owner on 2026-10-31: a tool that promises
a verbatim copy and a destination left as it was should not alter a byte it did not mean to
touch, standard or not; and it should not itself write a byte the standard forbids.

One fact surfaced while implementing. The owner named Unicode's canonical decomposition (FormD)
with the combining marks dropped as the rule for the HISTORY card. The console and the core
tests run with `InvariantGlobalization` (ADR 0002's build; FlatCheck's ADR 0037 for the window,
which cannot), and there `string.Normalize(NormalizationForm.FormD)` returns a text unchanged,
without an exception, measured on .NET 8.0.33 on 2026-10-31 with a probe run twice:

```
invariant=unset  input=0103 0219 021B 00EE 00E2 00E9 00C5 FormD=0061 0306 0073 0326 0074 0326 0069 0302 0061 0302 0065 0301 0041 030A
DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1  FormD=0103 0219 021B 00EE 00E2 00E9 00C5
```

Through the runtime's `Normalize`, the console would have written `Nebul?.fit` where the window
wrote `Nebula.fit` for the same file.

## Decision

- **The header is read and written with `Encoding.Latin1`**, where each byte 0x00 to 0xFF is
  the character of that code and comes back as the same byte: every card, copied or not, keeps
  its bytes, and a header that is all ASCII reads and writes exactly as it did under ASCII
  (R-010).
- **The HISTORY card stays ASCII** (R-011): a character outside ASCII whose canonical
  decomposition (FormD) begins with an ASCII character followed by combining marks only becomes
  that character (a with breve to `a`, s and t with comma to `s` and `t`, i and a with
  circumflex to `i` and `a`, e with acute to `e`, A with ring to `A`); a non-spacing combining
  mark standing alone is dropped; anything else outside ASCII, an emoji, a letter of another
  script, a spacing or enclosing mark, becomes one `?`. The copy never puts a byte above 0x7F
  in a header itself.
- **The rule is frozen into a table**, `AsciiText`, not asked of the runtime: the 495
  characters of the Basic Multilingual Plane whose FormD decomposition is one ASCII character
  plus combining marks, generated on .NET 8.0.33 by enumerating the plane (53 in Latin-1
  Supplement, 108 in Latin Extended-A, 83 in Latin Extended-B, 244 in Latin Extended Additional,
  and 7 others: the Greek question mark, the Greek varia, the Kelvin and Angstrom signs, and the
  three negated relations, whose negation is lost: `=` for U+2260, `<` for U+226E, `>` for
  U+226F, as the rule says). The console and the window thus write the same card
  for the same name, whatever globalization mode each runs in. The table is checked against the
  runtime's own FormD over the whole plane by `AsciiTextTests` in the window's test project,
  which runs with culture data; a runtime whose Unicode data adds such a character makes that
  test red, and the table is then regenerated.
- **The log writes a byte above 0x7F as `\xNN`** (R-042), the same way as a control character,
  so the log stays ASCII and says which byte the file holds; the `?` it wrote stood for a byte
  the file no longer gets.

## Alternatives rejected

- **ASCII as it was, the `?` pinned.** The choice of 2026-10-30; rejected on 2026-10-31
  because the `?` reaches cards the copy never meant to touch, so "the destination's header is
  left as it was, with the cards copied over it" was not true for such a file, and because the
  tool wrote the `?`, a non-standard byte of its own making, into the HISTORY card.
- **The transliteration in HISTORY alone, ASCII kept for the cards.** It would have fixed the
  one card the tool writes and left the copy altering the bytes of a header it did not write.
- **Latin-1 without the HISTORY rule.** The file name's letters would have gone into the card
  as Latin-1 bytes (one `?` per character above 0xFF): the copy itself would then have written a
  byte the standard forbids, which the first point of the decision set out to end.
- **The runtime's `string.Normalize` instead of a table.** Measured above: a no-op under
  `InvariantGlobalization`, so the two applications would have disagreed on one name; turning
  invariant globalization off for the console is a decision of its own (its number formatting
  and culture data rest on it) and was not the owner's question.

## Consequences

- An all-ASCII header is unchanged: the Release console's copy of the V-001 fixture's solve into
  a copy of the fixture, built before the change and after it, gave the same SHA-256
  (`ea8aa7ce...1a629c`) for the `-o` copy and for the in-place one; the ASCII tests, V-001 among
  them, did not move.
- Guarded by `R010_Run_AByteAboveAsciiInACopiedCard_IsCopiedAsItIs`,
  `R010_Run_AByteAboveAsciiInADestinationCardNotCopied_StaysAsItIs`, the `R011_AddHistory_...`
  tests (the seven letters, an emoji, the cut at 72 after the transliteration),
  `R011_Run_ASourceNamedWithADiacritic_WritesTheBaseLetterInHistory`,
  `R042_ACardIsLoggedAsTheFileHoldsIt_TheHistoryCardWithTheBaseLetter`,
  `R042_AByteAboveAsciiInACardIsLoggedAsItsHexCode`, and the table's pin
  `R011_TheBaseCharacterTable_IsTheRuntimesFormDWithoutTheMarks_OverTheWholePlane`.
- The table is a maintained artefact of 4 KB: its generator is described in `fits-header.md`,
  and the pin test says when to run it again.
- The parser trims spaces only: with Latin-1 the bytes 0x85 and 0xA0 read as characters
  `string.Trim()` counts as white space, and a keyword field of `OBJECT` plus two 0xA0 bytes
  would have matched `OBJECT` and been replaced, which is the untouched card R-010 now
  promises (found by the review of 2026-10-31; `KeywordOf` and `GetInt` use `Trim(' ')`, the
  two listings `TrimEnd(' ')`).
- A byte above 0x7F in a card the dry run lists, or the window shows, appears as the character
  the console's output encoding or WPF gives the Latin-1 code (0xC5 as A with ring), where it
  was `?`; the copy and the log are unaffected, and the file written holds the byte.
- The test files stay ASCII: a letter is written by its code point, a byte above 0x7F is put
  into the file's bytes.
