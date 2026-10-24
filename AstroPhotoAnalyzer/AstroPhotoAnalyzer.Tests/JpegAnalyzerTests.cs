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

public class JpegAnalyzerTests
{
    /// <summary>
    /// A sky with a gradient and noise, plus 16 stars: enough structure for the JPEG to have
    /// something to spoil.
    /// </summary>
    static Raster Scene(int planes = 3)
    {
        Raster r = SyntheticRasters.Ramp(256, 256, 0.08f, 0.14f, planes);
        for (int i = 0; i < 4; i++)
            for (int j = 0; j < 4; j++)
                r = SyntheticRasters.AddStar(r, 40 + i * 55, 40 + j * 55, 3.0, 0.4f);
        return SyntheticRasters.AddNoise(r, 0.5 / 255, 6);
    }

    [Fact]
    public void LowerQualityMeansSmallerFilesAndLargerErrors()
    {
        var j = JpegAnalyzer.Analyze(Scene());

        string dump = string.Join(" | ", j.Rows.Select(r => $"q{r.Quality}: {r.Bytes} B, mean {r.MeanAbsError:0.000}, max {r.MaxError:0.0}, >2: {r.PctOverThreshold:0.00}%"));

        Assert.Equal(new[] { 100, 95, 90 }, j.Rows.Select(r => r.Quality));
        Assert.True(j.Rows[0].Bytes > j.Rows[1].Bytes && j.Rows[1].Bytes > j.Rows[2].Bytes, "bytes: " + dump);
        // The MAXIMUM error rises monotonically as the quality drops; the MEAN does not
        // necessarily. Measured: at q100 the integer RGB<->YCbCr conversion costs +/-1-2 levels
        // per pixel, and q95 smooths the noise under one level and, on an almost lossless scene,
        // can land closer to the reference on average. That is why the report judges the JPEG
        // by the maximum and by the affected pixels, not by the mean.
        Assert.True(j.Rows[0].MaxError <= j.Rows[1].MaxError && j.Rows[1].MaxError <= j.Rows[2].MaxError, "maximum error: " + dump);
        Assert.True(j.Rows[0].MeanAbsError < 1.0, "mean error q100: " + dump);
        Assert.True(j.Tiff8LzwBytes > 0);
    }

    [Fact]
    public void QualityOneHundredIsCloseToTheSource()
    {
        var row = JpegAnalyzer.Analyze(Scene()).Rows[0];

        // Includes the rounding to 8 bits (0.25); on the reference image it was 0.63.
        Assert.InRange(row.MeanAbsError, 0.2, 1.5);
        Assert.True(row.Acceptable, "max err: " + row.MaxError);
        Assert.True(row.P99 >= row.MeanAbsError);
        Assert.True(row.P999 >= row.P99);
        Assert.True(row.MaxError >= row.P999);
    }

    [Fact]
    public void AFlatImageCompressesAlmostLosslessly()
    {
        var row = JpegAnalyzer.Analyze(SyntheticRasters.Flat(64, 64, 0.3f)).Rows[0];

        Assert.True(row.MeanAbsError < 0.6, "mean: " + row.MeanAbsError);
        Assert.Equal(0, row.PctOverThreshold);
        Assert.Equal(0, row.MeanLevelOverThreshold);
    }

    [Fact]
    public void TheAffectedPixelsReportTheirMeanLevel()
    {
        var row = JpegAnalyzer.Analyze(Scene()).Rows[2];

        if (row.PctOverThreshold > 0) Assert.InRange(row.MeanLevelOverThreshold, 1, 254);
    }

    [Fact]
    public void MonoIsEncodedAsLuminance()
    {
        var j = JpegAnalyzer.Analyze(Scene(planes: 1));

        Assert.Equal(3, j.Rows.Count);
        Assert.All(j.Rows, r => Assert.True(r.Bytes > 0));
        Assert.True(j.Tiff8LzwBytes > 0);
    }

    [Fact]
    public void ACustomQualityListIsHonoured()
    {
        var j = JpegAnalyzer.Analyze(Scene(), new[] { 80 });

        var row = Assert.Single(j.Rows);
        Assert.Equal(80, row.Quality);
    }
}
