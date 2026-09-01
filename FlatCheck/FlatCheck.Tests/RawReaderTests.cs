// SPDX-License-Identifier: GPL-3.0-or-later
//
// Copyright (C) 2026 Tudor Carean
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

using System.Reflection;

namespace FlatCheck.Tests;

/// <summary>
/// Reading camera raws is the one place the tool leans on someone else's code, so what is
/// pinned here is the *seam*, not libraw: which paths get routed to it, and that a FITS
/// never does. A raw file cannot be committed to the repo - the smallest real NEF is 18 MB
/// - so the decode itself is checked against a real file only when one is pointed at,
/// via FLATCHECK_NEF (see <see cref="DecodesARealRawWhenOneIsPointedAt"/>).
/// </summary>
public class RawReaderTests
{
    static readonly IFrameReader Reader = new RawReader();

    [Theory]
    [InlineData("flat.NEF")]
    [InlineData("flat.nef")]
    [InlineData("a/b/DSC_0060.Nef")]
    [InlineData("x.cr2")]
    [InlineData("x.cr3")]
    [InlineData("x.arw")]
    [InlineData("x.dng")]
    [InlineData("x.raf")]
    public void RecognisesTheRawExtensionsFlatcheckPyAccepts(string path)
        => Assert.True(Reader.Handles(path));

    /// <summary>
    /// The dispatch is by extension, so a FITS must not fall into the raw reader - that
    /// would replace the hand-rolled reader the whole tool is built on, silently.
    /// </summary>
    [Theory]
    [InlineData("pp_flat_stacked.fit")]
    [InlineData("x.fits")]
    [InlineData("x.fts")]
    [InlineData("noextension")]
    [InlineData("")]
    [InlineData(null)]
    public void DoesNotClaimAnythingThatIsNotARaw(string path)
        => Assert.False(Reader.Handles(path));

    /// <summary>
    /// A missing file must say so plainly. Without this the failure surfaces from inside
    /// libraw as a numeric error code, which tells the user nothing.
    /// </summary>
    [Fact]
    public void AMissingRawFailsWithTheFileNameNotALibrawErrorCode()
    {
        string path = Path.Combine(Path.GetTempPath(), "flatcheck-nu-exista.NEF");

        var e = Assert.Throws<FileNotFoundException>(() => Reader.Load(path));

        Assert.Contains("flatcheck-nu-exista.NEF", e.Message);
    }

    /// <summary>
    /// A file whose name says raw but whose bytes are not gets refused, rather than
    /// decoded into noise. This is the only failure mode that could produce a confident
    /// wrong number instead of an error.
    /// </summary>
    [Fact]
    public void AFileThatIsNotActuallyARawIsRefused()
    {
        using var ws = new TempWorkspace();
        string path = Path.Combine(ws.Dir, "minte.NEF");
        File.WriteAllBytes(path, new byte[4096]);

        Assert.ThrowsAny<Exception>(() => Reader.Load(path));
    }

    /// <summary>
    /// The end-to-end proof, and the one that closes the feature gap against flatcheck.py.
    /// Point FLATCHECK_NEF at any camera raw:
    ///
    ///     FLATCHECK_NEF=E:/.../DSC_0060.NEF dotnet test --filter ARealRaw
    ///
    /// It cannot run in CI - the smallest real NEF is 18 MB, far too big to commit - so it
    /// is gated on the variable and does nothing without it. Numeric equality with rawpy is
    /// verified separately by running flatcheck.py over the same file; that comparison needs
    /// both toolchains and so lives outside the suite.
    /// </summary>
    [Fact]
    public void ARealRawDecodesToAnUndebayeredMosaicWithAScaleFromTheFile()
    {
        string path = Environment.GetEnvironmentVariable("FLATCHECK_NEF");
        if (string.IsNullOrEmpty(path)) return;

        LoadedFrame f = ImageLoader.Load(path);

        Assert.True(f.Image.H > 1000 && f.Image.W > 1000, $"{f.Image.W}x{f.Image.H}");
        Assert.Equal("libraw (exact)", f.Scale.Source);
        Assert.True(f.Scale.White > f.Scale.Black, $"white {f.Scale.White} black {f.Scale.Black}");

        // Empty, not null: LightSuitability reads it and must land in the "unknown field"
        // layer, which runs with a note. A null would throw instead.
        Assert.NotNull(f.Header);
        Assert.Empty(f.Header);

        // Undebayered: the four Bayer positions must NOT agree. If they do, something in
        // the chain demosaiced the frame and the exposure mode has nothing left to measure.
        var r = ExposureAnalyzer.Analyze(f.Image, null, null, false, f.Scale);
        Assert.True(r.IsCfa, "spread " + r.CfaSpread);
        Assert.False(r.LooksDebayered);
        Assert.False(r.LevelTooLow);
    }

    /// <summary>
    /// The container must not matter. Takes real pixels off a real raw, writes them into a
    /// FITS, and runs both through the loader and the analyzer - the raw with the scale it
    /// carries, the FITS with those same levels typed in as --sat / --black. Every number
    /// has to match.
    ///
    /// <see cref="ExposureAnalyzerTests.ReadingTheScaleFromARawComputesExactlyWhatSatAndBlackDo"/>
    /// makes the same claim on synthetic data; this one makes it on a file that came out of
    /// a camera, through libraw, through the FITS reader and back.
    ///
    /// A crop rather than the whole frame: 24 MP twice over is hundreds of MB, and the claim
    /// - same pixels in, same result out, whatever the container - does not need the corners
    /// to be tested. Both offsets are even, so the Bayer phase survives the crop.
    /// </summary>
    [Fact]
    public void ARealRawAndAFitsHoldingItsPixelsGiveTheSameNumbers()
    {
        string path = Environment.GetEnvironmentVariable("FLATCHECK_NEF");
        if (string.IsNullOrEmpty(path)) return;

        LoadedFrame raw = ImageLoader.Load(path);
        Img crop = EvenCrop(raw.Image, 1200, 800);

        using var ws = new TempWorkspace();
        string fitsPath = ws.WriteFile("din_nef.fit",
            FitsTestData.BuildFits(crop.W, crop.H, (x, y) => crop.At(y, x), bitpix: 16));

        LoadedFrame fits = ImageLoader.Load(fitsPath);

        Assert.Null(fits.Scale);                  // a FITS carries no ADU scale, ever
        Assert.Equal(crop.D, fits.Image.D);       // and the pixels survived the round trip

        var fromRaw = ExposureAnalyzer.Analyze(crop, null, null, false, raw.Scale);
        var fromFits = ExposureAnalyzer.Analyze(fits.Image, raw.Scale.White, raw.Scale.Black, false);

        Assert.Equal(fromFits.ChannelMedians, fromRaw.ChannelMedians);
        Assert.Equal(fromFits.CentreMedianPct, fromRaw.CentreMedianPct, 12);
        Assert.Equal(fromFits.GlobalMedianPct, fromRaw.GlobalMedianPct, 12);
        Assert.Equal(fromFits.CentreClipPct, fromRaw.CentreClipPct, 12);
        Assert.Equal(fromFits.BrightestChannel, fromRaw.BrightestChannel);
        Assert.Equal(fromFits.Verdict, fromRaw.Verdict);
    }

    /// <summary>A centred crop whose origin is even, so the Bayer phase is unchanged.</summary>
    static Img EvenCrop(Img a, int w, int h)
    {
        int x0 = ((a.W - w) / 2) & ~1, y0 = ((a.H - h) / 2) & ~1;
        var o = new Img(h, w);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                o.Set(y, x, a.At(y0 + y, x0 + x));
        return o;
    }

    /// <summary>
    /// The FITS modes have to keep working when Sdcb.LibRaw is absent entirely - someone
    /// copied just flatcheck.exe out of a plain publish. That holds only while no *field*
    /// of this reader is a libraw type: fields are resolved when the type is loaded, and
    /// the type is loaded on every run because ImageLoader asks it Handles(), whereas
    /// method bodies are resolved lazily at JIT time.
    ///
    /// Measured, by deleting Sdcb.LibRaw.dll and running on a FITS: a static field of a
    /// libraw *reference* type changes nothing, a field of a libraw *value* type makes the
    /// run fail with "Could not load file or assembly". This rejects both - that reference
    /// types happen to survive is a CLR implementation detail, not something to lean on.
    ///
    /// Nothing else guards this. Without it, the first field added here would break
    /// FITS-only installs and no test would notice.
    /// </summary>
    [Fact]
    public void HoldsNoLibrawTypedFields_SoTheAssemblyStaysLazilyLoaded()
    {
        var fields = typeof(RawReader).GetFields(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);

        foreach (var f in fields)
        {
            string asm = f.FieldType.Assembly.GetName().Name ?? "";
            Assert.False(asm.StartsWith("Sdcb", StringComparison.Ordinal),
                $"campul {f.Name} e de tip {f.FieldType.FullName} din {asm} - "
              + "asta forteaza incarcarea libraw si pe rularile pe FITS");
        }
    }
}
