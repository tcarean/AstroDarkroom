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
using LightsCheck.Core;
using LightsCheck.Tests;

namespace LightsCheck.Wpf.Tests;

/// <summary>
/// The console gets its invariance from InvariantGlobalization; a WPF application cannot use
/// it (the window dies loading XAML), so App.ForceInvariantNumbers does it by hand at start.
/// These tests are what stops someone from removing that call as useless: on a Romanian
/// Windows the saved report would write "0,24" where the console writes "0.24", and the
/// requirement is that the file is identical. The tests set a Romanian culture and give it
/// back; the collection keeps them apart from the rest.
/// </summary>
[Collection("culture")]
public class CultureTests
{
    private sealed class RomanianCulture : IDisposable
    {
        private readonly CultureInfo _before = CultureInfo.CurrentCulture;
        private readonly CultureInfo? _beforeDefault = CultureInfo.DefaultThreadCurrentCulture;

        internal RomanianCulture()
        {
            CultureInfo ro = new("ro-RO");
            CultureInfo.DefaultThreadCurrentCulture = ro;
            CultureInfo.CurrentCulture = ro;
        }

        public void Dispose()
        {
            CultureInfo.DefaultThreadCurrentCulture = _beforeDefault;
            CultureInfo.CurrentCulture = _before;
        }
    }

    /// <summary>Confirms the fixture produces the problem under test.</summary>
    [Fact]
    public void RomanianCultureReallyWouldWriteACommaDecimalSeparator()
    {
        using RomanianCulture _ = new();

        Assert.Equal("0,24", 0.24.ToString("F2", CultureInfo.CurrentCulture));
    }

    [Fact]
    public void ForcingInvariantNumbersFixesTheCurrentThread()
    {
        using RomanianCulture _ = new();

        App.ForceInvariantNumbers();

        Assert.Equal("0.24", 0.24.ToString("F2", CultureInfo.CurrentCulture));
    }

    /// <summary>And the pool threads, where the analysis runs through Task.Run and the report is composed.</summary>
    [Fact]
    public async Task ForcingInvariantNumbersAlsoCoversPoolThreads()
    {
        using RomanianCulture _ = new();

        App.ForceInvariantNumbers();
        string onPool = await Task.Run(() => 0.24.ToString("F2", CultureInfo.CurrentCulture));

        Assert.Equal("0.24", onPool);
    }

    /// <summary>The real proof: the window's report under a Romanian culture is the console's, byte for byte.</summary>
    [Fact]
    public async Task R030_TheReportIsTheSameUnderARomanianCulture()
    {
        using SessionFixture f = new SessionFixture().WithStarSeries(SessionFixture.Pattern);

        string expected;
        using (StringWriter sw = new())
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            RunReport.WriteBanner(sw);
            SeriesReport.Write(SeriesRun.Run(f.Root, new SeriesOptions(), null, CancellationToken.None), sw);
            expected = sw.ToString();
        }

        MainViewModel vm;
        using (new RomanianCulture())
        {
            App.ForceInvariantNumbers();
            vm = new MainViewModel();
            await vm.OpenAsync(f.Root);
        }

        Assert.Equal(expected, vm.Report);
        Assert.Contains(".", vm.Frames[0].Elongation, StringComparison.Ordinal);
        Assert.All(vm.Frames, r => Assert.DoesNotContain(",", r.Fwhm, StringComparison.Ordinal));
    }
}

/// <summary>The culture tests change global state, so they run alone.</summary>
[CollectionDefinition("culture", DisableParallelization = true)]
public class CultureGroup
{
}
