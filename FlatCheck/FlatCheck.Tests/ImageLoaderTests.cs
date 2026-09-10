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
/// `ImageLoader` is the only place in the tool that knows FITS from raw, and these tests
/// exist to keep it that way. Everything below it - the analyzers, the reports, the
/// verdict ladder - must be unable to tell where the pixels came from, so what is pinned
/// here is the routing and the shape of what comes back, not any arithmetic.
/// </summary>
public class ImageLoaderTests
{
    const int W = 64, H = 48;

    static byte[] SampleFits(params string[] extraCards)
        => FitsTestData.BuildFits(W, H, FitsTestData.Radial(W, H), extraCards: extraCards);

    // ---------------------------------------------------------------- the FITS side

    [Fact]
    public void AFitsLoadsWithItsHeaderAndNoScale()
    {
        using var ws = new TempWorkspace();
        string path = ws.WriteFile("flat.fit", SampleFits(FitsTestData.OpticsCards(135, 3.9)));

        LoadedFrame f = ImageLoader.Load(path);

        Assert.Equal(H, f.Image.H);
        Assert.Equal(W, f.Image.W);
        Assert.Equal("135", f.Header["FOCALLEN"]);

        // Null, not an invented scale: a FITS genuinely does not carry the ADU levels, and
        // ExposureAnalyzer must fall back to AutoWhite rather than believe a made-up one.
        Assert.Null(f.Scale);
    }

    /// <summary>
    /// The header has to survive the trip intact, because --light's applicability guard
    /// reads it. If the loader dropped it, every FITS would be graded as "unknown field"
    /// and would quietly run when it should refuse.
    /// </summary>
    [Fact]
    public void TheFitsHeaderReachesTheApplicabilityGuard()
    {
        using var ws = new TempWorkspace();
        // Focal length only, no pixel size - the DSLR case, graded against a full-frame
        // sensor. 20 mm is far past the limit, so the guard must refuse.
        string wide = ws.WriteFile("wide.fit", SampleFits(FitsTestData.OpticsCards(20)));

        var s = LightSuitability.Check(ImageLoader.Load(wide).Header, null);

        Assert.False(s.CanRun);
    }

    // ---------------------------------------------------------------- the routing itself

    /// <summary>
    /// The sharp version of "dispatch is by extension": the *same bytes* must be read by
    /// two different readers. A valid FITS is not a valid NEF, so naming it .NEF has to
    /// fail - if it loaded, the loader would be sniffing content, not routing on the name,
    /// and a real NEF could then silently fall through to the FITS reader.
    /// </summary>
    [Fact]
    public void TheSameBytesAreRoutedByExtensionNotByContent()
    {
        using var ws = new TempWorkspace();
        byte[] bytes = SampleFits();
        string asFits = ws.WriteFile("frame.fit", bytes);
        string asNef = ws.WriteFile("frame.NEF", bytes);

        Assert.Equal(H, ImageLoader.Load(asFits).Image.H);
        Assert.ThrowsAny<Exception>(() => ImageLoader.Load(asNef));
    }

    /// <summary>
    /// Anything that is not one of the six raw extensions goes to the FITS reader - which
    /// is why a .jpg surfaces as a FITS parse error rather than "unrecognised format".
    /// Documented as a known issue rather than fixed; pinned so it cannot change silently.
    /// </summary>
    [Fact]
    public void AnUnknownExtensionGoesToTheFitsReader()
    {
        using var ws = new TempWorkspace();
        string path = ws.WriteFile("frame.jpg", SampleFits());

        Assert.Equal(H, ImageLoader.Load(path).Image.H);
    }

    // ---------------------------------------------------------------- the two entry points

    /// <summary>
    /// LoadImage is a convenience over Load, not a second code path. If it ever grew its
    /// own dispatch, the two could drift and only one of them would get a fix.
    /// </summary>
    [Fact]
    public void LoadImageReturnsExactlyWhatLoadReturns()
    {
        using var ws = new TempWorkspace();
        string path = ws.WriteFile("flat.fit", SampleFits());

        Img viaLoad = ImageLoader.Load(path).Image;
        Img viaLoadImage = ImageLoader.LoadImage(path);

        Assert.Equal(viaLoad.H, viaLoadImage.H);
        Assert.Equal(viaLoad.W, viaLoadImage.W);
        Assert.Equal(viaLoad.D, viaLoadImage.D);
    }

    [Fact]
    public void AMissingFileFails()
    {
        using var ws = new TempWorkspace();

        Assert.ThrowsAny<Exception>(() => ImageLoader.Load(ws.PathTo("nu-exista.fit")));
        Assert.ThrowsAny<Exception>(() => ImageLoader.Load(ws.PathTo("nu-exista.NEF")));
    }

    // ---------------------------------------------------------------- the contract itself

    /// <summary>
    /// Each reader states what it owns. ImageLoader picks between them through the
    /// contract alone - it knows no reader's API - so these declarations are the whole
    /// dispatch, and a reader claiming too much would silently swallow the other's files.
    /// </summary>
    [Theory]
    [InlineData("x.fit", true)]
    [InlineData("x.FITS", true)]
    [InlineData("x.fts", true)]
    [InlineData("x.NEF", false)]
    [InlineData("x.cr2", false)]
    [InlineData("x.jpg", false)]
    public void TheFitsReaderClaimsOnlyFitsExtensions(string path, bool claimed)
        => Assert.Equal(claimed, new FitsReader().Handles(path));

    /// <summary>
    /// The listing rule the window uses for the histogram tab is the same set of
    /// extensions the reader claims - one list, exposed, like RawReader's.
    /// </summary>
    [Theory]
    [InlineData("x.fit", true)]
    [InlineData("x.FITS", true)]
    [InlineData("x.fts", true)]
    [InlineData("x.NEF", false)]
    [InlineData("x.txt", false)]
    [InlineData("", false)]
    public void IsFitsPath_IsTheExtensionListOnly(string path, bool expected)
    {
        Assert.Equal(expected, FitsReader.IsFitsPath(path));
        Assert.Equal(new[] { ".fit", ".fits", ".fts" }, FitsReader.FitsExtensions);
    }

    [Theory]
    [InlineData("x.NEF", true)]
    [InlineData("x.fit", false)]
    [InlineData("x.jpg", false)]
    public void TheRawReaderClaimsOnlyRawExtensions(string path, bool claimed)
        => Assert.Equal(claimed, new RawReader().Handles(path));
}
