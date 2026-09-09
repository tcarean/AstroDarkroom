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
/// Cultura. Un fisier mic pentru o problema care s-ar fi vazut abia pe calculatorul
/// altcuiva.
///
/// Consola isi obtine invarianta din &lt;InvariantGlobalization&gt;, dar aia nu se poate
/// folosi intr-o aplicatie WPF - fereastra crapa la incarcarea XAML-ului. Aici se face
/// de mana, la pornire, iar testele astea sunt tot ce impiedica pe cineva sa scoata acel
/// apel ca "inutil": pe un Windows romanesc raportul salvat ar incepe brusc sa scrie
/// "0,24" unde consola scrie "0.24", iar cerinta e ca fisierul sa fie IDENTIC, nu
/// echivalent.
///
/// Testele isi pun singure o cultura romaneasca si o dau inapoi la sfarsit; xunit poate
/// rula clase in paralel, deci colectia le tine separat de restul.
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

    /// <summary>Confirma ca fixtura chiar produce problema pe care o testam.</summary>
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
    /// Si firele din pool, unde ruleaza analizoarele prin Task.Run. Doar CurrentCulture
    /// n-ar ajunge: firele noi si-ar lua cultura sistemului, iar raportul s-ar compune
    /// tocmai acolo.
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
    /// Proba adevarata: raportul salvat de fereastra, sub cultura romaneasca, e identic
    /// cu cel pe care l-ar scrie consola. Nu doar separatorul - tot textul.
    /// </summary>
    [Fact]
    public async Task TheSavedReportIsTheSameUnderARomanianCulture()
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
    /// Si cifrele din interfata: un "51,6 %" langa un raport care scrie "51.6 %" ar fi
    /// doar derutant.
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
    /// Si legenda hartii. Are propriul format ("+0.0;-0.0"), deci nu e acoperita de
    /// niciun test de mai sus: pe un Windows romanesc ar fi scris "-10,0 %" langa un
    /// raport care scrie "10.96 %".
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
/// Testele de cultura schimba o stare globala, deci nu pot rula in paralel cu nimic.
/// </summary>
[CollectionDefinition("cultura", DisableParallelization = true)]
public class CultureCollection { }
