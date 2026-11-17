# 0112. Only the first page of a file is decoded, and a TIFF's directory without an image is not a page

Date: 2026-11-17
Status: accepted 2026-11-17

## Context

On 2026-11-17 the window refused a TIFF the owner had exported from Lightroom Classic 16.0, with
ImageSharp's own sentence on the error card: `The number of samples in the TIFF BitsPerSample
entry is not supported.` Nothing is wrong with the image's bits. The file (135 564 803
bytes, SHA-256 `ada526e955f79e56c39b6e87fe1a1978d905331bc17224aac933c446bedf6353`; not kept,
see Consequences) is a little-endian classic TIFF of 5837 x 3867, 16-bit RGB, uncompressed, one strip
at offset 58, with an ICC profile of 3144 bytes. After that directory comes a second one with a
single entry and no image: the tag 52545, 18 773 bytes that begin with the boxes `jumb`, `jumd`
and `c2pa`. Those are the Content Credentials Lightroom adds on export.

`ImageSharpReader` asked ImageSharp 3.1.11 for the whole file: `Image.Identify`, then
`Image.Load`. Measured on that file with a probe:

```
Identify(): 5837x3867 frames=2
  frame 0: bits=TiffBitsPerSample(16, 16, 16) compression=None photometric=Rgb exifValues=21 icc=3144
    ImageWidth in the frame's tags: 5837
  frame 1: bits= compression= photometric= exifValues=1 icc=none
    ImageWidth in the frame's tags: absent
    tag 52545 (52545) type Undefined
Identify(MaxFrames=1): 5837x3867 frames=2
Load() threw NotSupportedException: The number of samples in the TIFF BitsPerSample entry is not supported.
Load(MaxFrames=1): 5837x3867 frames=1 icc(image)= icc(frame0)=3144
pixels compared with the file's strip bytes: 22571679, different: 0
```

So `Identify` lists the second directory as a frame like any other, and takes no limit;
`Load` decodes every frame and throws on the one without an image; with
`DecoderOptions.MaxFrames = 1` it decodes the image alone, every sample equal to the file's
bytes, the profile on the first frame as before.

The tool measures one image. Of a multi-page TIFF whose pages have one size it read the first
page and said so in the report's file line (R-012), after decoding all of them. One whose pages
differ in size (an image and its preview) it refused: on a hand-built file of two pages, 6 and
4 rows high, `Load()` throws `Images with different sizes are not supported`, and with
`MaxFrames = 1` it gives the first page, 8 x 6.

## Decision

The reader decodes with `MaxFrames = 1`, colour and mono alike, whatever the format. The pages
the note counts (`a TIFF with 2 pages; the first was read`) are the directories that state an
image width, a tag every image has and that has no default; a directory without one is not a
page and is not mentioned. A file with Lightroom's Content Credentials is therefore read as the
same file without them, with no note.

## Alternatives rejected

- **Decode everything, and on a failure try again with the first page.** Two decodes of a
  135 MB file for the case that needed the fix, and nothing gained for the others: the later
  pages were never measured.
- **Refuse the file with a sentence that says to export without Content Credentials.** The
  image is whole and every other program opens it; the owner would be sent back to Lightroom for
  something the tool can step over.
- **Say in the report that the file carries Content Credentials.** The file line's note is for
  what changes the reading (clipped values, a palette, other pages); the credentials change
  nothing the tool measures or recommends.
- **Count the pages by `BitsPerSample` or by the photometric tag.** `BitsPerSample` has a
  default, so a real one-bit page may leave it out and would not be counted.
- **Move to ImageSharp 4.** The version is pinned for its licence task (ADR 0009), and whether
  4.x reads such a file was not measured.

## Consequences

- The export is not kept, a deviation from ADR 0110 for this one file. A copy was made in
  code-testdata for the measurements above and taken out the same day at the owner's word: no
  test reads it, it is 135 MB, and any TIFF exported from Lightroom Classic with Content
  Credentials on shows the case, so the measurement can be made again on a fresh export, though
  not on the same bytes. The catalogue held a second export under the same name a quarter of an
  hour later (the same size and the same two directories, other bytes), and the window built
  from this branch opened and analysed that one too.
- On the measured export the console gave
  `TIFF 16-bit RGB, 5837 x 3867 (22.6 MP), necomprimat, 129.3 MB, profil de culoare (ICC): sRGB IEC61966-2.1`
  and a full report, exit `2` (the verdict is `ATENTIE`), where it gave the error.
- A TIFF with several real pages no longer has its later pages decoded. One whose pages differ
  in size, or whose later page ImageSharp cannot decode, was refused and is now read, with the
  note. When the first directory of such a file is the small preview, the preview is what the
  tool measures; the size on the file line and the note are the only signs.
- What Identify refuses is refused as before, since the decode is never reached: an animated
  PNG (`imagine stricata: Invalid PNG data`) and a TIFF whose first directory holds no image
  (`imagine stricata: The TIFF image frame is missing the ImageWidth`).
- The credentials are not read, and no file the tool writes carries them: the curve's, the
  paper fit's and the crop's writers take the pixels and the ICC profile only
  (`known-limitations.md`).
- R-012 says so and is `implemented`: three tests named `R012_...` in `ImageSharpReaderTests`
  read hand-built files, `TiffTestData.WithADirectoryWithoutAnImage` adding the directory
  Lightroom writes and `WithASecondPage` a real second page.
- Any other file ImageSharp cannot decode still shows ImageSharp's own sentence, as the
  reader's comment says; only this cause is removed.
