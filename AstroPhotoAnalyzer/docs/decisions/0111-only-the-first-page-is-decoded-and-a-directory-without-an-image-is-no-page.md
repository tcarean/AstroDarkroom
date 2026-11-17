# 0111. Only the first page of a file is decoded, and a TIFF's directory without an image is not a page

Date: 2026-11-17
Status: accepted 2026-11-17

## Context

On 2026-11-17 the window refused a TIFF the owner had exported from Lightroom Classic 16.0, with
ImageSharp's own sentence on the error card: `The number of samples in the TIFF BitsPerSample
entry is not supported.` Nothing is wrong with the image's bits. The file, kept as
`E:\Photografie\Astro\code-testdata\AstroPhotoAnalyzer\lightroom-content-credentials-2026-11-17\denoised_syqon_studio_in_max_0.75_stretch0.15_logd4.16_Gentle_lowdust_signed_Soft.tif`
(ADR 0110), is a little-endian classic TIFF of 5837 x 3867, 16-bit RGB, uncompressed, one strip
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

The tool measures one image. Of a multi-page TIFF it has always read the first page and said so
in the report's file line (R-012), after decoding all of them.

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

- On the kept export the console gives
  `TIFF 16-bit RGB, 5837 x 3867 (22.6 MP), necomprimat, 129.3 MB, profil de culoare (ICC): sRGB IEC61966-2.1`
  and a full report, exit `0`, where it gave the error.
- A TIFF with several real pages is read faster and in less memory, its later pages no longer
  decoded, and one whose later page ImageSharp cannot decode is now read. An animated PNG is
  read as its first frame, as before.
- The credentials are not read, and no file the tool writes carries them: the curve's, the
  paper fit's and the crop's writers take the pixels and the ICC profile only
  (`known-limitations.md`).
- R-012 says so and is `implemented`: three tests named `R012_...` in `ImageSharpReaderTests`
  read hand-built files, `TiffTestData.WithADirectoryWithoutAnImage` adding the directory
  Lightroom writes and `WithASecondPage` a real second page.
- Any other file ImageSharp cannot decode still shows ImageSharp's own sentence, as the
  reader's comment says; only this cause is removed.
