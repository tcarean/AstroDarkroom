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

// CultureTests.cs - the report is the same on a Romanian Windows.
//
// Two nets, both tested: Fmt writes the numbers with the invariant culture EXPLICITLY, so
// the report does not depend on the thread's culture; and App.ForceInvariantNumbers fixes
// the culture at startup anyway, for any text that would not go through Fmt.

using AstroPhotoAnalyzer.Tests;
using System.Globalization;

namespace AstroPhotoAnalyzer.Wpf.Tests;

[Collection("cultura")]
public class CultureTests
{
    sealed class RomanianCulture : IDisposable
    {
        readonly CultureInfo _before = CultureInfo.CurrentCulture;
        readonly CultureInfo? _beforeDefault = CultureInfo.DefaultThreadCurrentCulture;

        internal RomanianCulture()
        {
            var ro = new CultureInfo("ro-RO");
            CultureInfo.DefaultThreadCurrentCulture = ro;
            CultureInfo.CurrentCulture = ro;
        }

        public void Dispose()
        {
            CultureInfo.DefaultThreadCurrentCulture = _beforeDefault;
            CultureInfo.CurrentCulture = _before;
        }
    }

    [Fact]
    public void R118_RomanianCultureReallyWouldWriteACommaDecimalSeparator()
    {
        using var _ = new RomanianCulture();
        Assert.Equal("0,24", 0.24.ToString("F2"));
    }

    [Fact]
    public void R118_ForcingInvariantNumbersFixesTheCurrentThread()
    {
        using var _ = new RomanianCulture();
        App.ForceInvariantNumbers();
        Assert.Equal("0.24", 0.24.ToString("F2"));
    }

    [Fact]
    public async Task ForcingInvariantNumbersAlsoCoversPoolThreads()
    {
        using var _ = new RomanianCulture();
        App.ForceInvariantNumbers();
        string onPool = await Task.Run(() => 0.24.ToString("F2"));
        Assert.Equal("0.24", onPool);
    }

    /// <summary>
    /// The real proof: the report produced under the Romanian culture, WITHOUT forcing
    /// anything, is identical to the one produced under the invariant culture - Fmt keeps
    /// it invariant on its own.
    /// </summary>
    [Fact]
    public void R118_TheReportIsIdenticalUnderARomanianCultureWithoutForcing()
    {
        Raster r = Scenes.CleanSky();
        var p = new Parameters();

        string invariant;
        using (new RomanianCulture())
        {
            App.ForceInvariantNumbers();
            invariant = AnalysisRun.Execute(r, "memorie", p).ReportBody;
        }

        string romanian;
        using (new RomanianCulture())
            romanian = AnalysisRun.Execute(r, "memorie", p).ReportBody;

        Assert.Equal(invariant, romanian);
        Assert.Contains("0.25", romanian);
        Assert.DoesNotContain("0,25", romanian);
    }

    [Fact]
    public void R024_ParameterFieldsParseWithThePointRegardlessOfCulture()
    {
        using var _ = new RomanianCulture();
        var f = new ParameterFields { Distance = "1.5", TargetBg = "0.19" };

        Parameters? p = f.TryBuild(out string? error);

        Assert.Null(error);
        Assert.Equal(1.5, p!.ViewingDistanceM);
        Assert.Equal(0.19, p.TargetBackground);
    }
}

[CollectionDefinition("cultura", DisableParallelization = true)]
public class CultureCollection { }
