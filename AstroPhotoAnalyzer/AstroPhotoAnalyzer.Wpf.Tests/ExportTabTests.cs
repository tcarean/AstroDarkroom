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

using AstroPhotoAnalyzer.Tests;

namespace AstroPhotoAnalyzer.Wpf.Tests;

public class ExportTabTests
{
    static AnalysisResult Result(Raster r) => AnalysisRun.Execute(r, "memorie", Scenes.OnBuiltForPaper(null));

    [Fact]
    public void R114_TheExportTabCarriesTheQ100FloorAndThe16BitMasterAsFacts()
    {
        // The two facts the report no longer warns about (ADR 0061): the stress test's 8-bit row
        // marked when the master should be kept, and the "what is recommended" paragraph naming
        // the highest quality under 100 that fails, with its figure, when there is one.
        AnalysisResult result = Result(Scenes.DarkSky());
        var export = new ExportTabViewModel();
        export.Populate(result);

        MetricRow from8 = Assert.Single(export.Rows, x => x.Label == "... din 8 biti");
        Assert.True(result.Quantization.Keep16BitMaster);
        Assert.Equal("pastreaza master-ul de 16 biti", from8.Note);

        GuideParagraph recommended = Assert.Single(export.JpegGuide, p => p.Lead == "Ce se recomanda.");
        JpegRow floor = result.Jpeg.HighestFailingBelow100;
        if (floor == null)
            Assert.DoesNotContain("nu cobori sub q100", recommended.Text);
        else
            Assert.EndsWith($"Aici q{floor.Quality} ajunge la o eroare maxima de {Fmt.F1(floor.MaxError)} niveluri (prag 10): nu cobori sub q100.", recommended.Text);
    }
}
