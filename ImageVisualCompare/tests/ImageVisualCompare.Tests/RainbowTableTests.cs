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

using ImageVisualCompare.Core;

namespace ImageVisualCompare.Tests;

/// <summary>Siril 1.4.4's rainbow colour map, as R-112 pins it.</summary>
public class RainbowTableTests
{
    [Theory]
    [InlineData(0, 255, 0, 255)]
    [InlineData(1, 250, 0, 255)]
    [InlineData(51, 0, 0, 255)]
    [InlineData(64, 0, 65, 255)]
    [InlineData(102, 0, 255, 255)]
    [InlineData(128, 0, 255, 125)]
    [InlineData(153, 0, 255, 0)]
    [InlineData(204, 255, 255, 0)]
    [InlineData(254, 255, 5, 0)]
    [InlineData(255, 255, 0, 0)]
    public void R112_TheTableIsSirils(byte level, byte r, byte g, byte b)
    {
        Assert.Equal((r, g, b), RainbowTable.Colour(level));
    }

    [Fact]
    public void R112_WriteBgra_PutsBlueFirstAndAlpha255()
    {
        byte[] bgra = new byte[4];

        RainbowTable.WriteBgra(64, bgra);

        Assert.Equal([255, 65, 0, 255], bgra);
    }

    [Fact]
    public void R114_EveryColourOfTheTable_GivesItsLevelBack()
    {
        for (int level = 0; level < 256; level++)
        {
            (byte r, byte g, byte b) = RainbowTable.Colour((byte)level);
            Assert.Equal((byte)level, RainbowTable.LevelOf(r, g, b));
        }
    }

    [Fact]
    public void R114_AColourNotInTheTable_HasNoLevel()
    {
        Assert.Null(RainbowTable.LevelOf(0, 0, 0));
        Assert.Null(RainbowTable.LevelOf(128, 128, 128));
    }
}
