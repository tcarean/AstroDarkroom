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

// SplitTabProgressTests.cs - the Split tab's progress, from a split run on a dispatcher as the
// window runs it (R-092, ADR 0087).
//
// A class of its own, in the "window" collection for its dispatcher, so that only this test runs
// beside the collection's Application: the other split tests, in SplitTabTests, run without it,
// the Siril runner's lines handled inline as before. The fixtures are SplitTabTests'.

using FlatCheck.Tests;

namespace FlatCheck.Wpf.Tests;

[Collection("window")]
public class SplitTabProgressTests
{
    readonly StaFixture _sta;

    public SplitTabProgressTests(StaFixture sta) => _sta = sta;

    [Fact]
    public async Task R092_TheProgressCoversEveryPartAndReachesTheEnd()
    {
        using var f = SplitTabTests.Session(flats: 4);
        using var ws = new TempWorkspace();
        var phases = new List<string>();
        var vm = SplitTabTests.Vm(f, ws, SplitTabTests.FakeSiril(new List<SplitTabTests.Seen>()));
        vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(vm.Phase) && vm.Phase.Length > 0) phases.Add(vm.Phase); };

        // On a dispatcher, as the window runs it: there the copy's progress reports arrive in
        // order, before the split's end. On the thread pool a test has otherwise, they arrive in
        // no fixed order, and a late one could overwrite the final 1 (one failure under load on
        // 2026-11-01, not reproduced in 140 runs; ADR 0087).
        await _sta.InvokeAsync(() => vm.StartAsync());

        Assert.True(vm.HasResult, vm.ErrorText);
        Assert.Contains(phases, p => p.Contains("1 din 2"));
        Assert.Contains(phases, p => p.Contains("2 din 2"));
        Assert.Equal(1.0, vm.Progress, 6);
        Assert.True(vm.ProgressKnown);
    }
}
