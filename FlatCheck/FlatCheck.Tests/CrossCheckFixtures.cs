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
/// Dumps the parity fixtures (<see cref="ParityFixtures"/>) to a folder, so the frozen
/// Python prototype can be run over the *same bytes* by hand and compared with the console.
///
/// The cross-check was retired on 2026-09-26 (ADR 0047): flatcheck.py and crosscheck.py stay
/// in the repository frozen at 2.2.0 and are not maintained; the numbers they confirmed are
/// pinned by <see cref="ParityGoldenTests"/>. This dump is what remains for a comparison by
/// hand, for as long as the frozen prototype's output still means something:
///
///     FLATCHECK_DUMP_FIXTURES=/some/dir dotnet test --filter DumpsFixtures
///     python crosscheck.py /some/dir
///
/// Does nothing unless the variable is set, so a normal test run is unaffected.
/// </summary>
public class CrossCheckFixtures
{
    [Fact]
    public void DumpsFixturesWhenAsked()
    {
        string? dir = Environment.GetEnvironmentVariable("FLATCHECK_DUMP_FIXTURES");
        if (string.IsNullOrEmpty(dir)) return;

        ParityFixtures.WriteAll(dir);

        Assert.True(File.Exists(Path.Combine(dir, "clean.fit")));
    }
}
