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

using System.Globalization;
using System.Text.Json;

namespace FlatCheck.Tests;

/// <summary>
/// The console gets its dot from <c>&lt;InvariantGlobalization&gt;</c>, a runtime switch read
/// from the executable's runtimeconfig.json. A test project is its own executable: the switch
/// of the console it references does not reach the test host. Without the same switch here,
/// the golden files and the report tests format under the test machine's culture, pass on an
/// en-GB machine and fail by the dozen on a Romanian one (measured 2026-09-25, ADR 0043).
/// The window's half of the same rule is <c>CultureTests</c> in FlatCheck.Wpf.Tests (R-090).
/// </summary>
public class InvariantGlobalizationTests
{
    const string Switch = "System.Globalization.Invariant";

    [Fact]
    public void R007_TheConsoleRunsUnderInvariantGlobalization()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "flatcheck.runtimeconfig.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));

        var props = doc.RootElement.GetProperty("runtimeOptions").GetProperty("configProperties");

        Assert.True(props.TryGetProperty(Switch, out var on) && on.GetBoolean(),
            $"flatcheck.runtimeconfig.json does not set {Switch}");
    }

    [Fact]
    public void R007_TheTestHostRunsUnderTheSameSwitchAsTheConsole()
    {
        Assert.True(AppContext.TryGetSwitch(Switch, out bool on) && on,
            $"the test host does not set {Switch}; add <InvariantGlobalization> to FlatCheck.Tests.csproj");
        Assert.Equal("", CultureInfo.CurrentCulture.Name);
        Assert.Equal("0.24", 0.24.ToString("F2"));
    }
}
