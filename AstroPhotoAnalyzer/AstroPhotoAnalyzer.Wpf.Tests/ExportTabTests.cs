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
        // The two facts the report no longer warns about (ADR 0061). The dark scene loses levels
        // from 8 bits, so the stress test's 8-bit row is marked; its JPEG qualities under 100 all
        // stay within the threshold (q90 at 7.3 levels), so no failing quality is named.
        AnalysisResult result = Result(Scenes.DarkSky());
        Assert.True(result.Quantization.Keep16BitMaster);
        Assert.Null(result.Jpeg.HighestFailingBelow100);
        var export = new ExportTabViewModel();
        export.Populate(result);

        MetricRow from8 = Assert.Single(export.Rows, x => x.Label == "... din 8 biti");
        Assert.Equal("pastreaza master-ul de 16 biti", from8.Note);
        GuideParagraph recommended = Assert.Single(export.JpegGuide, p => p.Lead == "Ce se recomanda.");
        Assert.DoesNotContain("nu cobori sub q100", recommended.Text);

        // The same analysis with the JPEG figures of the owner's 2026-08-12 core denoised by Syqon
        // at 0.75 (ADR 0061: q100 at 4.5 levels, q95 at 25.3, q90 at 32.4): the paragraph names
        // q95, the highest that fails, in both languages.
        result.Jpeg = new JpegResult
        {
            Rows = new[]
            {
                new JpegRow { Quality = 100, MaxError = 4.5 },
                new JpegRow { Quality = 95, MaxError = 25.3 },
                new JpegRow { Quality = 90, MaxError = 32.4 },
            },
            Tiff8LzwBytes = result.Jpeg.Tiff8LzwBytes,
        };
        export.Populate(result);

        recommended = Assert.Single(export.JpegGuide, p => p.Lead == "Ce se recomanda.");
        Assert.EndsWith($" Aici q95 ajunge la o eroare maxima de 25.3 niveluri (prag {Fmt.F0(Thresholds.JpegMaxErrorReject)}): nu cobori sub q100.", recommended.Text);

        using (Tr.Use(UiLanguage.English))
        {
            export.Populate(result);
            recommended = Assert.Single(export.JpegGuide, p => p.Lead == "What is recommended.");
            Assert.EndsWith($" Here q95 reaches a maximum error of 25.3 levels (threshold {Fmt.F0(Thresholds.JpegMaxErrorReject)}): do not go below q100.", recommended.Text);
        }
    }
}
