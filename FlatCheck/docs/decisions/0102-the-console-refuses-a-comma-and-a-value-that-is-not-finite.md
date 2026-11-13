# 0102. The console refuses a comma and a value that is not finite

Date: 2026-11-13
Status: accepted 2026-11-13

## Context

The console's six numeric options (`--sat`, `--black`, `--fov`, `--focal`, `--fstop`, `--crop`)
were parsed with `NumberStyles.Any` in the invariant culture. That style allows a thousands
separator, which is the comma there, and .NET drops it wherever it stands; it also parses
`NaN` and `Infinity`. The review of 2026-11-13 (CR-002) followed the cases: `--fstop 2,8` was
f/28 with no error, and the comparison then took the profile nearest f/28; `--crop 1,5` was 15,
a frame 2.4 mm wide; `--focal 13,5` was 135; `--sat 4095,5` was 40955. `--focal NaN` passed the
check `<= 0`, which is false for NaN, and `LensCatalog.Nearest` then threw "Sequence contains no
elements". `docs/design/process.md` said all along that `0,5` does not parse.

The window's own number fields take a comma as the decimal sign
(`SyntheticTabViewModel.Num`), so the same text was read two ways by the two applications.

## Decision

The owner's choice of 2026-11-13, between refusing the comma and reading it as the decimal sign:

1. A number on the command line has a dot for the decimals, an optional sign and an optional
   exponent, and is finite. `TryNum` parses with `NumberStyles.Float` and then asks
   `double.IsFinite`.
2. Anything else, a comma included, is refused with the message the option already had for a
   value that is not a number (`--fstop expects an f-number, got '2,8'`), exit 1, before
   anything runs. No new text.
3. A token that starts with a dash and is not such a number is an unknown option, as before, so
   `-Infinity` and `-1,5` are refused there.

## Alternatives rejected

- **The comma as the decimal sign**, as in the window. The two applications would read alike,
  but `1,234` would silently become 1.234 where another reader means 1234, the design note
  would have to change, and a command line is also written by scripts, where the dot is the
  rule. An error cannot give a wrong figure; a guess can.
- **A hint in the message** ("use a dot"). The message quotes the value, which shows the
  comma; six texts in two languages for one sentence was not worth it.

## Consequences

- A user who types `2,8` out of habit gets an error where a wrong verdict was printed before.
- The window still takes a comma in its fields; that difference is CR-056's to settle.
- `FitsReader` reads header numbers with `NumberStyles.Any` too; a FITS header with a comma in
  a number is outside the standard and is not changed here.
- R-005 holds the behaviour; `R005_...` in `OptionsTests` pins every option with a comma, `NaN`
  and `Infinity`.
