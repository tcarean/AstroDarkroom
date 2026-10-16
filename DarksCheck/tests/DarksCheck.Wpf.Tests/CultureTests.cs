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
using DarksCheck.Tests;

namespace DarksCheck.Wpf.Tests;

/// <summary>
/// The console gets its invariance from InvariantGlobalization; a WPF application cannot use
/// it (the window dies loading XAML), so App.ForceInvariantNumbers does it by hand at start.
/// These tests are what stops someone from removing that call as useless: on a Romanian
/// Windows the saved report would write "0,24" where the console writes "0.24", and the
/// requirement is that the file is identical (R-072). The tests set a Romanian culture and
/// give it back; the collection keeps them apart from the rest.
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

    [Fact]
    public async Task ForcingInvariantNumbersAlsoCoversPoolThreads()
    {
        using RomanianCulture _ = new();

        App.ForceInvariantNumbers();
        string onPool = await Task.Run(() => 0.24.ToString("F2", CultureInfo.CurrentCulture));

        Assert.Equal("0.24", onPool);
    }

    /// <summary>The report the window shows and exports is the console's byte for byte, whatever the Windows culture (R-072).</summary>
    [Fact]
    public async Task R072_TheReportIsTheSameUnderARomanianCulture()
    {
        using TestSettings.Own own = new();
        using SessionFixture f = WindowFixture.Session(lights: 2);
        string invariant;
        {
            App.ForceInvariantNumbers();
            MainViewModel vm = new() { ThreadsText = "2" };
            await vm.OpenAsync(f.Root);
            invariant = vm.Report;
        }

        using RomanianCulture _ = new();
        App.ForceInvariantNumbers();
        MainViewModel ro = new() { ThreadsText = "2" };
        await ro.OpenAsync(f.Root);

        Assert.Equal(invariant, ro.Report);
        Assert.Contains("x1.00 fata de ISO 400", invariant, StringComparison.Ordinal);
        Assert.DoesNotContain("x1,00", ro.Report, StringComparison.Ordinal);
    }
}
