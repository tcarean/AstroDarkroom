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

namespace FlatCheck.Wpf.Tests;

/// <summary>
/// Tabul end-to-end. Testele care conteaza aici nu sunt despre cifre, ci despre cele
/// trei feluri in care se poate termina - verdict, NEDETERMINAT, INAPLICABIL - si mai
/// ales despre faptul ca ultimul nu e o eroare.
/// </summary>
public class LightTabTests
{
    static async Task<LightTabViewModel> Run(SessionFixture f)
    {
        var vm = new LightTabViewModel();
        await vm.RunAsync(f.Session, CancellationToken.None);
        return vm;
    }

    [Fact]
    public async Task ItRunsOverTheWholeSeriesInProcess()
    {
        using var f = new SessionFixture().WithFlatMaster().WithLightSeries(6);

        var vm = await Run(f);

        Assert.True(vm.HasResult);
        Assert.False(vm.HasError);
        Assert.Equal(6, vm.Frames.Count);
        Assert.NotEmpty(vm.Verdict);
        Assert.NotEmpty(vm.MeanRadial);
    }

    /// <summary>
    /// Tabelul se umple pe masura ce cadrele sunt gata, in ordine. Pe 32 de cadre rularea
    /// ia doua minute, iar un tabel care apare tot deodata la sfarsit ar parea blocat.
    /// </summary>
    [Fact]
    public async Task TheTableFillsUpInFileOrder()
    {
        using var f = new SessionFixture().WithFlatMaster().WithLightSeries(5);

        var vm = await Run(f);

        Assert.Equal(
            new[] { "pp_light_00000.fit", "pp_light_00001.fit", "pp_light_00002.fit",
                    "pp_light_00003.fit", "pp_light_00004.fit" },
            vm.Frames.Select(r => r.Name).ToArray());
    }

    /// <summary>Fiecare rand poarta semnul explicit: semnul E diagnosticul.</summary>
    [Fact]
    public async Task EveryRowShowsTheRadialComponentWithAnExplicitSign()
    {
        using var f = new SessionFixture().WithFlatMaster().WithLightSeries(5);

        var vm = await Run(f);

        Assert.All(vm.Frames, r =>
            Assert.True(r.Radial.StartsWith('+') || r.Radial.StartsWith('-'),
                        "randul nu poarta semn: " + r.Radial));
    }

    /// <summary>Bara de progres e determinata pe serie: se stie cate cadre sunt.</summary>
    [Fact]
    public async Task ProgressIsKnownAndReachesTheEnd()
    {
        using var f = new SessionFixture().WithFlatMaster().WithLightSeries(5);

        var vm = await Run(f);

        Assert.True(vm.ProgressKnown);
        Assert.Equal(1.0, vm.Progress, 6);
    }

    // ------------------------------------------------------------------ refuzul

    /// <summary>
    /// CERINTA implicita a gardei din 1.2.0: pe un camp prea larg testul refuza. Refuzul
    /// NU e o eroare a aplicatiei - se afiseaza ca rezultat, cu explicatie, si tabul
    /// ramane utilizabil. Daca ar aparea ca eroare, omul ar crede ca unealta e stricata
    /// exact cand ea tocmai l-a scutit de o concluzie falsa.
    /// </summary>
    [Fact]
    public async Task ATooWideFieldIsRefusedAsAResultNotAsAnError()
    {
        using var f = new SessionFixture().WithFlatMaster()
                                          .WithLightSeries(6, SessionFixture.WideFieldCards);

        var vm = await Run(f);

        Assert.True(vm.HasResult);          // a raspuns
        Assert.False(vm.HasError);          // dar nu a esuat
        Assert.True(vm.Refused);
        Assert.Equal(Stats.NotApplicable, vm.Verdict);
    }

    /// <summary>Cand se refuza, nu se produce nicio cifra si nicio harta.</summary>
    [Fact]
    public async Task ARefusalProducesNoNumbersAtAll()
    {
        using var f = new SessionFixture().WithFlatMaster()
                                          .WithLightSeries(6, SessionFixture.WideFieldCards);

        var vm = await Run(f);

        Assert.False(vm.HasNumbers);
        Assert.Empty(vm.MeanRadial);
        Assert.Empty(vm.Spread);
        Assert.Empty(vm.Sem);
        Assert.Null(vm.Map);
    }

    /// <summary>Refuzul explica degenerarea geometrica si trimite la testul care raspunde.</summary>
    [Fact]
    public async Task ARefusalExplainsWhyAndPointsAtTheRotationTest()
    {
        using var f = new SessionFixture().WithFlatMaster()
                                          .WithLightSeries(6, SessionFixture.WideFieldCards);

        var vm = await Run(f);

        Assert.Contains("degenerare geometrica", vm.Reasons);
        Assert.Contains("Ratio", vm.Reasons);
        Assert.NotEmpty(vm.Field);
    }

    /// <summary>Verdictul de refuz se coloreaza neutru, nu rosu.</summary>
    [Fact]
    public async Task ARefusalIsColouredNeutrally()
    {
        using var f = new SessionFixture().WithFlatMaster()
                                          .WithLightSeries(6, SessionFixture.WideFieldCards);

        var vm = await Run(f);

        Assert.Equal(VerdictPalette.Neutral, VerdictPalette.For(vm.Verdict));
    }

    // ------------------------------------------------------------------ neconcludent

    /// <summary>
    /// O serie prea scurta nu poate concluziona - sub cinci cadre media nu e o
    /// masuratoare. Verdictul e NEDETERMINAT si se spune de ce, dar cifrele exista.
    /// </summary>
    [Fact]
    public async Task AShortSeriesIsUndeterminedButStillShowsItsNumbers()
    {
        using var f = new SessionFixture().WithFlatMaster().WithLightSeries(3);

        var vm = await Run(f);

        Assert.Equal(LightSeriesAnalyzer.Undetermined, vm.Verdict);
        Assert.True(vm.HasNumbers);
        Assert.NotEmpty(vm.MeanRadial);
        Assert.NotEmpty(vm.Reasons);
        Assert.Empty(vm.Diagnosis);        // fara verdict nu se pune diagnostic
    }

    // ------------------------------------------------------------------ fara cadre

    /// <summary>
    /// Fara cadre calibrate, tabul explica ce cere si de ce cele registrate nu se
    /// potrivesc - in loc sa arunce o exceptie de la mijlocul unui calcul.
    /// </summary>
    [Fact]
    public async Task WithoutCalibratedFramesItExplainsWhatItNeeds()
    {
        using var f = new SessionFixture().WithFlatMaster();

        var vm = await Run(f);

        Assert.False(vm.HasResult);
        Assert.True(vm.HasError);
        Assert.Contains("pp_light_*.fit", vm.ErrorText);
        Assert.Contains("r_pp_light_", vm.ErrorText);
    }

    [Fact]
    public async Task RegisteredFramesAloneStillCountAsNoSeries()
    {
        using var f = new SessionFixture().WithFlatMaster();
        File.Copy(Path.Combine(f.ProcessDir, SirilSession.FlatMasterName),
                  Path.Combine(f.ProcessDir, "r_pp_light_00001.fit"));

        var vm = await Run(f);

        Assert.True(vm.HasError);
    }

    // ------------------------------------------------------------------ anulare

    /// <summary>
    /// Anularea nu e o eroare: inseamna ca omul a schimbat folderul sau a inchis
    /// fereastra. Tabul ramane gol, fara panou rosu.
    /// </summary>
    [Fact]
    public async Task CancellingLeavesTheTabEmptyWithoutAnError()
    {
        using var f = new SessionFixture().WithFlatMaster().WithLightSeries(6);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var vm = new LightTabViewModel();
        await vm.RunAsync(f.Session, cts.Token);

        Assert.False(vm.HasResult);
        Assert.False(vm.HasError);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task ResetClearsEverything()
    {
        using var f = new SessionFixture().WithFlatMaster().WithLightSeries(5);
        var vm = await Run(f);

        vm.Reset();

        Assert.False(vm.HasResult);
        Assert.False(vm.Refused);
        Assert.Empty(vm.Frames);
        Assert.Empty(vm.Verdict);
        Assert.Null(vm.Map);
    }
}
