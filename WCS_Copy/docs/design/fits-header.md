# The FITS header engine and the key lists

**Covers:** `src/CopyWcs.Core/FitsHeader.cs` and `src/CopyWcs.Core/WcsMetadataKeys.cs`.

How the header is read, changed and written back by hand, and which keys a copy takes. What
the tool does with them is in `run-and-console.md`; why there is no FITS library is ADR 0001.

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
- `KeywordOf` is the first eight characters trimmed; `Keywords` lists the non-blank ones, so
  `COMMENT` and `HISTORY` count as keywords and blank cards do not.
- `GetInt` takes the text after `=` up to a `/`, trimmed, and returns -1 when the card is
  missing, has no `=` or is not an integer. Two files that both lack `NAXIS1` therefore
  compare as equal sizes (-1 and -1); the run does not special-case it, since such a file is
  not an image.
- `SetCard(key, raw)` replaces the first card of that keyword in place, keeping its position,
  or appends when there is none; the raw text is padded to 80 or cut at 80 by `Pad`. This is
  the verbatim copy of R-010: the caller passes the source's card, and nothing is reparsed.
- `AddHistory(text)` appends `HISTORY ` plus at most 72 characters of text (R-011).

## Writing back

`WriteWithDataFrom(dataSource, target)` writes the cards, `END`, spaces to the next 2880-byte
boundary, then copies `dataSource` from `_dataOffset` to its end. The header may have grown
past its original number of blocks (a copy adds up to 47 fixed keys, the SIP coefficients and a HISTORY card; 72 on V-001), so the
file cannot be patched in place: when `target` is `dataSource` itself, the output goes to
`target + ".tmp"` and `File.Move(temp, target, overwrite: true)` replaces the original at the
end (R-012, R-013). On a failure before the move, the original is untouched and the `.tmp`
is left behind; on success it is gone. The data area is copied as bytes: `BITPIX`, `NAXIS3`,
extensions and padding travel unchanged.

`ReadPrimary`, `AddHistory` and `WriteWithDataFrom` also write their lines to the diagnostic log
(the read line, the card appended, the write with its temporary file, the file written, and at
Trace every card; `logging.md` has the lines) and decide nothing from them: a failure still
throws, and its caller logs it.

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
