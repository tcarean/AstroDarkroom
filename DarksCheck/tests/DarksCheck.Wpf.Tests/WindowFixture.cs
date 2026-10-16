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

using DarksCheck.Tests;

namespace DarksCheck.Wpf.Tests;

/// <summary>The synthetic session every window test opens: six darks with the lattice hot pixels, three biases, four lights 1.3 x warmer, and a partner library 2 x warmer.</summary>
internal static class WindowFixture
{
    internal static SessionFixture Session(int lights = 4) => new SessionFixture()
        .WithDarkSeries(6, i => new SyntheticDark { Seed = 100 + i, Hot = SyntheticLight.Lattice }, biases: 3)
        .WithLights(lights, i => new SyntheticLight { Seed = 300 + i, Hot = SyntheticLight.Lattice, HotFactor = 1.3, Stars = SyntheticLight.ThreeStars });

    internal static SessionFixture Partner() => new SessionFixture()
        .WithDarkSeries(3, i => new SyntheticDark { Seed = 700 + i, Hot = SyntheticLight.Lattice, HotFactor = 2 }, inSubfolder: false);
}
