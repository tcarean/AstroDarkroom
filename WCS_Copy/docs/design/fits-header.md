# The FITS header engine and the key lists

**Covers:** `src/CopyWcs.Core/FitsHeader.cs`, `src/CopyWcs.Core/AsciiText.cs` and
`src/CopyWcs.Core/WcsMetadataKeys.cs`.

How the header is read, changed and written back by hand, how the one card the tool composes is
kept ASCII, and which keys a copy takes and which of the destination's it drops. What the tool
does with them is in `run-and-console.md`; why there is no FITS library is ADR 0001; why the
bytes are kept as they are and the HISTORY card is ASCII is ADR 0010; why a copy drops the
destination's old plate-solve cards is ADR 0011.

## The card and block model

A FITS header is ASCII: cards of 80 characters, 36 to a 2880-byte block, the keyword in
columns 1 to 8, `= ` in columns 9 and 10 for a value card, an optional `/ comment`, and an
`END` card followed by spaces to the end of its block. `FitsHeader` keeps the primary header
as a `List<string>` of raw 80-character cards, in order, END excluded, and remembers
`_dataOffset`: the number of header bytes consumed, which is where the data area begins.

- `ReadFrom(Stream)` reads whole blocks with `ReadFully` (a `Stream.Read` may return fewer
  bytes than asked, even mid-stream; the fake slow stream of `FitsHeaderTests` proves the loop
  copes) and walks each block card by card until the card whose keyword is `END`; the cards
  before it are kept, the rest of that block is ignored, and `_dataOffset` is the number of
  blocks read times 2880. A stream that ends before END, including one of exactly 36 cards
  with no END, is "truncated or not FITS"; a first card other than `SIMPLE` is "not FITS"
  (R-015). Only the primary HDU is read; extensions, if any, are part of what is copied as
  data.
- `KeywordOf` is the first eight characters trimmed of spaces only (`Trim(' ')`, as `GetInt`
  trims a value: with Latin-1 the bytes 0x85 and 0xA0 read as white space that `Trim()` would
  strip, as it stripped the control characters 0x09 to 0x0D, while FITS pads with the space
  alone; `OBJECT` plus two 0xA0 bytes must not pass for `OBJECT`, nor `END` plus one for `END`;
  `R010_ReadFrom_TheByte0xA0InAKeywordOrAValue_IsNotWhiteSpace`); `Keywords` lists the
  non-blank ones, so
  `COMMENT` and `HISTORY` count as keywords and blank cards do not.
- `GetInt` takes the text after `=` up to a `/`, trimmed, and returns -1 when the card is
  missing, has no `=` or is not an integer. Two files that both lack `NAXIS1` therefore
  compare as equal sizes (-1 and -1); the run does not special-case it, since such a file is
  not an image. `GetDouble` reads the same text (`ValueText`, shared by both) as a finite
  double, invariant, the FITS exponent letter `D` taken as `E`, and gives null for a missing
  card, a string, a logical or nothing: the numbers of R-017's check. `GetString` reads a quoted
  value from the card itself, not through `ValueText`, so a '/' inside the quotes stays text; a
  doubled quote is one, the trailing spaces go, and a value with no closing quote on the card is
  null.
- `SetCard(key, raw)` replaces the first card of that keyword in place, keeping its position,
  or appends when there is none; the raw text is padded to 80 or cut at 80 by `Pad`. This is
  the verbatim copy of R-010: the caller passes the source's card, and nothing is reparsed.
  A second card of the same keyword, which a header may hold though the standard says it
  should not, is left as it is.
- `CardsOf(key)` lists every card of a keyword in the header's order; `RemoveCards(key)` takes
  them all out, the other cards keeping their order, and returns them (R-016). The run logs a
  dropped key's cards through `CardsOf` before anything changes, and drops them through
  `RemoveCards` before the first `SetCard`.
- `AddHistory(text)` appends `HISTORY ` plus at most 72 characters of `AsciiText.Of(text)`
  (R-011): the text is made ASCII first, then cut, so the cut counts the characters written.

## The ASCII of a card the tool composes

`AsciiText.Of` is the one place a text the tool composes (today the source's file name in the
HISTORY card) becomes ASCII. It walks the text by runes: a rune below 0x80 is itself; one the
table `BaseCharacters` holds becomes its base character; a combining mark standing alone
(`Rune.GetUnicodeCategory` is `NonSpacingMark`, a name already in decomposed form) is dropped;
any other rune becomes one `?`, an emoji's two UTF-16 units included. A text that is already
ASCII is returned as the same instance.

The table is a `const string` of pairs, the character then its base, read once into a
`Dictionary<char, char>`: the 495 characters of the Basic Multilingual Plane whose FormD
decomposition is one ASCII character followed by combining marks only. It was generated on
2026-10-31 on .NET 8.0.33 by a scratch console without `InvariantGlobalization` that, for each
code point 0x80 to 0xFFFF outside the surrogates, took `Normalize(NormalizationForm.FormD)` of
the one-character string (an `ArgumentException`, thrown for a non-character, skipped it),
kept it when it differed by ordinal comparison, its first character was below 0x80 and every
later one was a `NonSpacingMark`, and printed the pairs as `\uXXXX` escapes twelve to a line;
no character had anything but marks after an ASCII base. The table is frozen rather than asked
of the runtime because the console's `string.Normalize` is a no-op under
`InvariantGlobalization` (ADR 0010 has the probe). `AsciiTextTests` in the window's test
project, which runs with culture data, repeats the enumeration against the runtime's FormD and
compares every character's expectation with `AsciiText.Of`, counting 495 with a base: when a
runtime's Unicode data moves, that test says so, and the table is generated again the same way.

## Writing back

`WriteWithDataFrom(dataSource, target)` writes the cards, `END`, spaces to the next 2880-byte
boundary, then copies `dataSource` from `_dataOffset` to its end. The header may have grown
past its original number of blocks (a copy adds up to 47 fixed keys, the SIP coefficients and a HISTORY card; 72 on V-001), so the
file cannot be patched in place: when `target` is `dataSource` itself, the output goes to
`target + ".tmp"` and `File.Move(temp, target, overwrite: true)` replaces the original at the
end (R-012, R-013). On a failure before the move, the original is untouched and the `.tmp`
is left behind; on success it is gone. The data area is copied as bytes: `BITPIX`, `NAXIS3`,
extensions and padding travel unchanged.

The cards are decoded and encoded with `Encoding.Latin1` (`CardEncoding`): each byte 0x00 to
0xFF is the character of that code and is written back as the same byte, so a byte above 0x7F,
which a FITS header may not hold but a file may, goes through the read, the `SetCard` and the
write untouched, in a copied card and in a card of the destination the copy never selected
alike (R-010, ADR 0010; until 2026-10-31 the cards went through `Encoding.ASCII`, which made
such a byte `?`). A header that is all ASCII is unchanged by the switch: the Release console's
copy of the V-001 fixture's solve into a copy of the fixture, with `-o` and in place, built from
`main` before the change and from the branch after it, gave the same SHA-256
`ea8aa7cebf3910de495d95d9fd275f42ea725982df4aa570f2794476371a629c` all four times (2026-10-31).
`R010_Run_AByteAboveAsciiInACopiedCard_IsCopiedAsItIs` and
`R010_Run_AByteAboveAsciiInADestinationCardNotCopied_StaysAsItIs` put a 0xC5 into a file's
bytes and read the file written back as Latin-1.

`ReadPrimary`, `AddHistory` and `WriteWithDataFrom` also write their lines to the diagnostic log
(the read line, the card appended, the write with its temporary file, the file written, and at
Trace every card; `logging.md` has the lines) and decide nothing from them: a failure still
throws, and its caller logs it. The read line's length comes from `LengthOf`, which never throws:
a stream that cannot seek is not asked, and whatever a seekable file's query throws is taken as
no length.

## The key lists

`WcsMetadataKeys` holds two literal lists and a pattern:

- `WcsKeys`, 30 keys: the axis and projection (`WCSAXES`, `CTYPE`, `CUNIT`), the reference
  pixel and value (`CRPIX`, `CRVAL`), the scale and rotation in their three forms (`CDELT`
  with `CROTA`, the `CD` matrix, the `PC` matrix), the frame (`EQUINOX`, `RADESYS`, `LONPOLE`,
  `LATPOLE`), the SIP orders and Siril's `PLTSOLVD`. A header carries one of the three scale
  forms; the list has all three so that any solver's output is copied whole.
- `OpticalKeys`, 17 keys: the focal length and pixel size a later re-solve needs as a starting
  scale, the binning, the pointing (`OBJCTRA`, `OBJCTDEC`, `RA`, `DEC`, `OBJECT`), the site,
  the date and the instrument.
- `SipCoefficient`, `^(A|B|AP|BP)_\d+_\d+$`: the distortion coefficients, whose names vary
  with the polynomial degree; `ABC_1_2` does not match, nor does `A_ORDER`.

`SelectKeysToCopy(source, includeOptical)` builds the wanted list in that order, the SIP
coefficients in the source's own order between the WCS keys and the optical ones, drops
duplicates and keeps only the keys the source has (R-007). V-001 in `docs/validation.md`
shows what this yields on a real Siril header: 61 WCS and SIP keys, 11 optical ones.

`SelectKeysToDrop(source, dest)` walks the destination's `Keywords` in order and keeps each one
that is in `WcsKeys` or matches `SipCoefficient` and that the source does not have, each once
(R-016). It is the same two lists that decide what a plate solve is, read from the other side, so
no card of those keys is left in the target but the source's, save a second card of a keyword the
source has, which `SetCard` does not reach; a plate-solve card outside both lists (`RADECSYS`,
`PVi_m`, `CTYPE1A`) is neither copied nor dropped. `OpticalKeys` never take part, so a
destination's own `OBJECT` or `SITELAT` stays when the source lacks it. The selector itself does
not look at `CTYPE1`: the run asks for it only when the source has one (ADR 0011). V-002 shows it
on a real destination: the 4 `CD` keys the SyQon CLI wrote beside Siril's `PC`.

`Determinant(header)` is the determinant of a plate solve's linear part, read as Siril 1.4.4
reads it (measured, ADR 0011): the CD matrix when the header holds any `CDi_j` card, a missing
one 0 as the WCS standard says; else `CDELT1` times `CDELT2` (each 1 when missing) times the PC
matrix's determinant (the identity where a `PC` card is missing). A `CROTA` is not read, since a
turn has determinant 1 and never mirrors. It is null without `CTYPE1`, without a linear part (no
`CDi_j` and no `CDELTi` card: the defaults alone would give +1 to a header that holds no scale),
or when a card it reads is there but holds no number. `Orientation` is its sign, null for 0 too.
`Mirrored(source, dest)` compares the two orientations only when both `CTYPE1` name the same
first axis, the text before the first '-' (`RA` of `'RA---TAN-SIP'`): the same solve written
declination first has the opposite sign without being a mirror, and a `LINEAR` axis is no sky
solve. It returns true for a mirror, false for the same orientation and null when there is
nothing to compare (R-017, ADR 0012). V-002's pair has -2.9641E-6 and +2.9641E-6.
