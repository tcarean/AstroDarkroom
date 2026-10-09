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

using LightsCheck.Core;

namespace LightsCheck.Tests;

/// <summary>Connected components, 4-connected like scipy's default, with their measurements (R-014).</summary>
public class ComponentsTests
{
    private static Raster Of(string[] rows)
    {
        Raster r = new(rows.Length, rows[0].Length);
        for (int y = 0; y < rows.Length; y++)
        {
            for (int x = 0; x < rows[0].Length; x++)
            {
                r.D[y * r.W + x] = rows[y][x] == '.' ? 0 : rows[y][x] - '0';
            }
        }

        return r;
    }

    [Fact]
    public void R014_DiagonalNeighboursAreSeparateComponents()
    {
        Raster a = Of(
        [
            "1.....",
            ".1....",
            "...22.",
            "...22.",
        ]);
        List<Component> c = Components.Find(a, 0.5, a, a);
        Assert.Equal(3, c.Count);
        Assert.Equal([1, 1, 4], c.Select(x => x.Area).OrderBy(x => x).ToArray());
    }

    [Fact]
    public void R014_AUShapeIsOneComponentWhateverTheScanOrder()
    {
        Raster a = Of(
        [
            "1...1",
            "1...1",
            "11111",
        ]);
        Component c = Assert.Single(Components.Find(a, 0.5, a, a));
        Assert.Equal(9, c.Area);
        Assert.Equal(0, c.Y0);
        Assert.Equal(2, c.Y1);
        Assert.Equal(0, c.X0);
        Assert.Equal(4, c.X1);
        Assert.Equal(3, c.Height);
        Assert.Equal(5, c.Width);
    }

    [Fact]
    public void R014_PeakFluxAndCentroidAreMeasuredOnTheValuesRaster()
    {
        Raster mask = Of(["...", ".1.", "..."]);
        mask.D[1 * 3 + 1] = 1;
        mask.D[1 * 3 + 2] = 1;
        Raster values = Of(["...", ".13", "..."]);
        Raster raw = Of(["...", ".99", "..."]);
        Component c = Assert.Single(Components.Find(mask, 0.5, values, raw));
        Assert.Equal(2, c.Area);
        Assert.Equal(3, c.Peak);
        Assert.Equal(9, c.PeakRaw);
        Assert.Equal(4, c.Flux);
        Assert.Equal(1, c.CentroidY, 1e-9);
        Assert.Equal((1 * 1 + 2 * 3) / 4.0, c.CentroidX, 1e-9);
    }

    [Fact]
    public void R014_ThresholdIsStrict()
    {
        Raster a = Of(["5"]);
        Assert.Empty(Components.Find(a, 5, a, a));
        Assert.Single(Components.Find(a, 4.9, a, a));
    }
}
