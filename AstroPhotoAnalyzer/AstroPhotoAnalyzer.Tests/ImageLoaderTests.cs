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

namespace AstroPhotoAnalyzer.Tests;

public class ImageLoaderTests
{
    [Fact]
    public void DispatchesFitsAndTiffToTheirReaders()
    {
        using var ws = new TempWorkspace();
        string fits = ws.WriteFile("a.fit", FitsTestData.Build2D(2, 2, (x, y) => 0.5, bitpix: -32));
        string tif = ImageFiles.SaveTiff8Rgb(SyntheticRasters.Flat(2, 2, 0.5f), ws.PathTo("a.tif"));

        Assert.Equal("FITS", ImageLoader.Load(fits).Source.Format);
        Assert.Equal("TIFF", ImageLoader.Load(tif).Source.Format);
    }

    [Fact]
    public void AnUnknownExtensionIsRefusedAndTheMessageListsWhatIsAccepted()
    {
        using var ws = new TempWorkspace();
        string psd = ws.WriteFile("a.psd", new byte[16]);

        var e = Assert.Throws<Exception>(() => ImageLoader.Load(psd));

        Assert.Contains(".psd", e.Message);
        Assert.Contains(".fit", e.Message);
        Assert.Contains(".tif", e.Message);
        Assert.Contains(".png", e.Message);
    }

    [Fact]
    public void AMissingFileIsAFileNotFound()
    {
        using var ws = new TempWorkspace();

        Assert.Throws<FileNotFoundException>(() => ImageLoader.Load(ws.PathTo("nu.tif")));
    }

    [Fact]
    public void TheExtensionListCoversBothReaders()
    {
        var ext = ImageLoader.Extensions;

        Assert.Contains(".fits", ext);
        Assert.Contains(".tiff", ext);
        Assert.Contains(".jpeg", ext);
        Assert.Equal(ext.Count, ext.Distinct().Count());
    }
}
