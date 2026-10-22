# 0031. Each loader turns its decoder's own failures into the exception types the pair loader expects

Date: 2026-10-22
Status: accepted 2026-10-22
Supersedes: 0015 in part (a new exception type becomes a message by being added to IsExpectedLoadFailure)

## Context

R-005 says a file that cannot be read gives a message and never crashes the application, and
ADR 0015 makes `ImagePairLoader.IsExpectedLoadFailure` the list of exception types that become
one; its consequences say that any other type is not caught, and that turning one into a message
means adding it to the list. The code review of the documentation backfill (2026-10-21) found
two failures of WIC off the list, each closing the application with no message. A PNG whose
header says 40 000 × 30 000 probes, then WIC throws `OverflowException` (0x80070216) when the
`CachedBitmap` sets the pixels aside. A damaged WebP, HEIC or AVIF makes Windows' codec throw
`COMException` 0xC00D36BE, "The file does not conform to the relevant file format
specification.", at the probe (a lossless WebP with a garbage bitstream) or at the decode (a
lossy one). `FitsImageLoader.CreateBitmap` already turns WPF's `OverflowException` for a FITS too
large to show into a `NotSupportedException` with a message of its own (ADR 0029). On 2026-10-22
the owner chose, of the two ways below, the loader.

## Decision

- A loader turns its decoder's own failures into the types `IsExpectedLoadFailure` accepts, with
  a message the user can read. The list, and R-005's, stay as they are.
- `WicImageLoader`: an `OverflowException` from the `CachedBitmap` becomes a
  `NotSupportedException`, `An image of <W> × <H> is too large to show.`, the FITS reader's
  wording without its prefix; a `COMException`, from the decoder, the frame or the
  `CachedBitmap`, in `Probe` and in `Load`, becomes a `FileFormatException` carrying the codec's
  message.
- Any other type still reaches the caller: a failure no loader expects is a defect, and a crash
  shows it.

## Alternatives rejected

- **Adding `OverflowException` and `COMException` to `IsExpectedLoadFailure` and to R-005.** An
  arithmetic overflow in the program's own code would then become a message about the user's
  file, and so would a COM failure that has nothing to do with a decoder.

## Consequences

- The messages: `Could not read huge.png.` with `An image of 40000 × 30000 is too large to
  show.`; `Could not read broken.webp.` with the codec's message, which ends in its HRESULT.
- A codec failure of another type, found later, is turned into an accepted type in the loader
  of that codec, not added to the list.
- Guards: `ImagePairLoaderTests.R005_AnImageTooLargeToDecode_IsAMessageNamingIt` and
  `R005_ADamagedWebP_IsAMessage_AtTheProbeOrAtTheDecode`, on files `TempImages` writes. Without
  Windows' WebP codec WIC refuses the WebP as an unknown format, a `NotSupportedException`, and
  the second test passes without reaching the `COMException`.
