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
        => Assert.True(RawReader.IsRawPath(path));

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
        => Assert.False(RawReader.IsRawPath(path));

    /// <summary>
    /// A missing file must say so plainly. Without this the failure surfaces from inside
    /// libraw as a numeric error code, which tells the user nothing.
    /// </summary>
    [Fact]
    public void AMissingRawFailsWithTheFileNameNotALibrawErrorCode()
    {
        string path = Path.Combine(Path.GetTempPath(), "flatcheck-nu-exista.NEF");

        var e = Assert.Throws<FileNotFoundException>(() => RawReader.Load(path));

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

        Assert.ThrowsAny<Exception>(() => RawReader.Load(path));
    }

    /// <summary>
    /// The real check, and the one that closes the feature gap against flatcheck.py:
    /// point FLATCHECK_NEF at any camera raw and the decode must produce a plausible
    /// CFA mosaic with a scale read from the file, not guessed.
    ///
    ///     FLATCHECK_NEF=E:/.../DSC_0060.NEF dotnet test --filter DecodesARealRaw
    ///
    /// Numeric equality with rawpy is verified by running flatcheck.py over the same
    /// file; that comparison lives outside the suite because it needs both toolchains.
    /// Does nothing unless the variable is set, so a normal run is unaffected.
    /// </summary>
    [Fact]
    public void DecodesARealRawWhenOneIsPointedAt()
    {
        string path = Environment.GetEnvironmentVariable("FLATCHECK_NEF");
        if (string.IsNullOrEmpty(path)) return;

        RawFrame f = RawReader.Load(path);

        Assert.True(f.Data.H > 1000 && f.Data.W > 1000, $"{f.Data.W}x{f.Data.H}");
        Assert.Equal("libraw (exact)", f.Scale.Source);
        Assert.True(f.Scale.White > f.Scale.Black, $"white {f.Scale.White} black {f.Scale.Black}");

        // Undebayered: the four Bayer positions must NOT agree. If they do, something in
        // the chain demosaiced the frame and the exposure mode has nothing left to measure.
        var r = ExposureAnalyzer.Analyze(f.Data, null, null, false, f.Scale);
        Assert.True(r.IsCfa, "spread " + r.CfaSpread);
        Assert.False(r.LooksDebayered);
        Assert.False(r.LevelTooLow);
    }
}
