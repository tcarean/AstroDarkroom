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

        MetricRow from8 = Assert.Single(export.Rows, x => x.Label == "... pornind din 8 biti");
        Assert.Equal("pastreaza originalul pe 16 biti", from8.Note);
        GuideParagraph recommended = Assert.Single(export.JpegGuide, p => p.Lead == "Ce se recomanda.");
        Assert.DoesNotContain("nu cobori sub calitatea 100", recommended.Text);

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
        Assert.EndsWith($" Aici calitatea 95 ajunge la o diferenta de 25.3 niveluri (prag {Fmt.F0(Thresholds.JpegMaxErrorReject)}): nu cobori sub calitatea 100.", recommended.Text);

        using (Tr.Use(UiLanguage.English))
        {
            export.Populate(result);
            recommended = Assert.Single(export.JpegGuide, p => p.Lead == "What is recommended.");
            Assert.EndsWith($" Here quality 95 reaches a difference of 25.3 levels (threshold {Fmt.F0(Thresholds.JpegMaxErrorReject)}): do not go below quality 100.", recommended.Text);
        }
    }

    /// <summary>
    /// R-114 (ADR 0074): the Export tab's texts and Appendix C of the text report are in plain
    /// words, in both languages, on the dark scene (16 bits, flat areas measured, the 16-bit
    /// original to keep) and on the mono sky: the rows' names, notes and descriptions, the guide's
    /// texts and the appendix's lines use none of the method's words. The JPEG table's column
    /// titles stay (the guide explains them), and so do the guide's leads, which name them.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void R114_TheExportTextsAreInPlainWords(bool english)
    {
        using IDisposable scope = Tr.Use(english ? UiLanguage.English : UiLanguage.Romanian);
        var texts = new List<string>();
        foreach (Raster raster in new[] { Scenes.DarkSky(), Scenes.MonoSky() })
        {
            AnalysisResult result = Result(raster);
            var export = new ExportTabViewModel();
            export.Populate(result);
            texts.AddRange(export.Rows.SelectMany(r => new[] { r.Label, r.Note, r.Description }).Where(t => t != null));
            texts.AddRange(export.JpegGuide.Select(g => g.Text));
            var report = new System.IO.StringWriter();
            ReportWriter.WriteFull(result, report);
            string[] lines = report.ToString().Split('\n');
            int c = Array.FindIndex(lines, l => l.StartsWith(Tr.Pick("Appendix C.", "Anexa C.")));
            int d = Array.FindIndex(lines, l => l.StartsWith(Tr.Pick("Appendix D.", "Anexa D.")));
            Assert.True(c > 0 && d > c, "the report has Appendix C before Appendix D");
            texts.AddRange(lines.Skip(c + 1).Take(d - c - 1)
                .Where(l => !l.TrimStart().StartsWith("JPEG 4:4:4") && !l.TrimStart().StartsWith("q")));
        }

        var methodWords = new System.Text.RegularExpressions.Regex(
            @"\b(esantion\w*|samples?|cuantiz\w*|quantiz\w*|palier\w*|plateau\w*|LSB|sigma|encod\w*|decod\w*|subesantion\w*|subsampling|YCbCr|monoton\w*|predictor|stres\w*|shadow-lift|master|auto-correct|tiles?|dal[ae]|dalele|pozitii|positions)\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        Assert.All(texts, t => Assert.False(methodWords.IsMatch(t), t));
        Assert.Contains(texts, t => t.Contains(Tr.Pick("keep the 16-bit original", "pastreaza originalul pe 16 biti")));
        Assert.Contains(texts, t => t.Contains(Tr.Pick("small areas of smooth sky", "zone mici de cer neted")));
    }
}
