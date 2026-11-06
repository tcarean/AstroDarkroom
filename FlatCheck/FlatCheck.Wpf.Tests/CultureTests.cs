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

namespace FlatCheck.Wpf.Tests;

/// <summary>
/// Culture. A small file for a problem that would have shown only on someone else's
/// computer.
///
/// The console gets its invariance from &lt;InvariantGlobalization&gt;, but that cannot be
/// used in a WPF application - the window crashes when loading the XAML. Here it is done
/// by hand, at start-up, and these tests are all that keeps someone from removing that
/// call as "useless": on a Romanian Windows the saved report would suddenly start writing
/// "0,24" where the console writes "0.24", and the requirement is that the file be
/// IDENTICAL, not equivalent.
///
/// The tests set a Romanian culture themselves and give it back at the end; xunit can run
/// classes in parallel, so the collection keeps them apart from the rest.
/// </summary>
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

    /// <summary>Confirms that the fixture really produces the problem we are testing.</summary>
    [Fact]
    public void RomanianCultureReallyWouldWriteACommaDecimalSeparator()
    {
        using var _ = new RomanianCulture();

        Assert.Equal("0,24", 0.24.ToString("F2"));
    }

    [Fact]
    public void ForcingInvariantNumbersFixesTheCurrentThread()
    {
        using var _ = new RomanianCulture();

        App.ForceInvariantNumbers();

        Assert.Equal("0.24", 0.24.ToString("F2"));
    }

    /// <summary>
    /// The pool threads too, where the analyzers run through Task.Run. CurrentCulture alone
    /// would not be enough: new threads would take the system's culture, and the report is
    /// composed exactly there.
    /// </summary>
    [Fact]
    public async Task ForcingInvariantNumbersAlsoCoversPoolThreads()
    {
        using var _ = new RomanianCulture();

        App.ForceInvariantNumbers();
        string onPool = await Task.Run(() => 0.24.ToString("F2"));

        Assert.Equal("0.24", onPool);
    }

    /// <summary>
    /// The real proof: the report saved by the window, under the Romanian culture, is identical
    /// to the one the console would write. Not only the separator - the whole text.
    /// </summary>
    [Fact]
    public async Task R090_TheSavedReportIsTheSameUnderARomanianCulture()
    {
        using var f = new SessionFixture().WithFlatMaster();
        var session = f.Session;

        var expected = new StringWriter();
        RunReport.WriteBanner(expected);
        StructureReport.Write(
            StructureAnalyzer.Analyze(ImageLoader.LoadImage(session.FlatMasterPath)),
            session.FlatMasterPath, expected);
        RunReport.WriteMapSaved(session.ResidualPngPath, expected);

        using (new RomanianCulture())
        {
            App.ForceInvariantNumbers();
            await new StructureTabViewModel().RunAsync(session, CancellationToken.None);
        }

        Assert.Equal(expected.ToString(), File.ReadAllText(session.ResultTextPath));
    }

    /// <summary>
    /// The figures in the interface too: a "51,6 %" beside a report that writes "51.6 %" would
    /// only be confusing.
    /// </summary>
    [Fact]
    public async Task TheOnScreenNumbersUseTheSameSeparatorAsTheReport()
    {
        using var f = new SessionFixture().WithFlatMaster();

        StructureTabViewModel vm;
        using (new RomanianCulture())
        {
            App.ForceInvariantNumbers();
            vm = new StructureTabViewModel();
            await vm.RunAsync(f.Session, CancellationToken.None);
        }

        Assert.All(vm.Tests, t => Assert.DoesNotContain(",", t.Value));
        Assert.Contains(".", vm.Tests[2].Value);
    }

    /// <summary>
    /// The map legend too. Its number goes through Numbers.Signed, not the report's F2, so it
    /// is not covered by any test above: on a Romanian Windows it would have written "-10,0 %"
    /// beside a report that writes "10.96 %".
    /// </summary>
    [Fact]
    public async Task TheMapLegendUsesTheSameSeparatorAsTheReport()
    {
        using var f = new SessionFixture().WithFlatMaster();

        StructureTabViewModel vm;
        using (new RomanianCulture())
        {
            App.ForceInvariantNumbers();
            vm = new StructureTabViewModel();
            await vm.RunAsync(f.Session, CancellationToken.None);
        }

        Assert.DoesNotContain(",", vm.MapLegend);
        Assert.Contains("-10.0 %", vm.MapLegend);
    }
}

/// <summary>
/// The culture tests change a global state, so they cannot run in parallel with anything.
/// </summary>
[CollectionDefinition("cultura", DisableParallelization = true)]
public class CultureCollection { }
