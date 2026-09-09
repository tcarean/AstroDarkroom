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
/// Tabul de atribuire prin rotatie 180.
///
/// Tabul NU mai cauta in folder, si de-aia nu mai exista teste de lista aici. Modul cere
/// doua MASTERE, cate unul pentru fiecare orientare, iar cele doua orientari sunt doua
/// sedinte, deci doua foldere de sesiune. Ce putea propune un singur folder erau cadrele
/// individuale, din care iese un rezultat sigur si fals - zgomotul unui cadru e de
/// acelasi ordin cu semnalul cautat. Deci se cer explicit doua fisiere, si ce se
/// verifica aici e validarea alegerii si atribuirea rezultata.
/// </summary>
public class RatioTabTests
{
    static RatioTabViewModel Attached(SessionFixture f)
    {
        var vm = new RatioTabViewModel();
        vm.Attach(f.Session, CancellationToken.None);
        return vm;
    }

    // ------------------------------------------------------------------ pornire

    /// <summary>
    /// Tabul porneste gol: nu propune nimic, fiindca un folder de sesiune nu poate sti
    /// care sunt cele doua orientari. In schimb explica ce ii trebuie, inainte de orice.
    /// </summary>
    [Fact]
    public void TheTabStartsEmptyAndExplainsWhatItNeeds()
    {
        using var f = new SessionFixture()
            .WithFlatMasters("pp_flat_stacked_A.fit", "pp_flat_stacked_B.fit");

        var vm = Attached(f);

        Assert.Null(vm.FileA);
        Assert.Null(vm.FileB);
        Assert.False(vm.HasResult);

        Assert.Contains("DOUA MASTERE", vm.Documentation);
        Assert.Contains("DOUA SEDINTE", vm.Documentation);
        Assert.Contains("180", vm.Documentation);
    }

    /// <summary>
    /// Documentatia trebuie sa spuna si ce NU poate testul: ca partea fixa pe aparat
    /// poate fi optica (inofensiva) sau ceva pe obiectiv (nu). Fara asta, un verdict bun
    /// pe un master contaminat citeste ca o garantie.
    /// </summary>
    [Fact]
    public void TheDocumentationStatesWhatTheTestCannotTell()
    {
        using var f = new SessionFixture().WithFlatMasters("pp_flat_stacked_A.fit");
        var vm = Attached(f);

        Assert.Contains("CE NU ITI SPUNE", vm.Documentation);
        Assert.Contains("obiectiv", vm.Documentation);
    }

    // ------------------------------------------------------------------ validare

    /// <summary>
    /// Un fisier care nu incepe cu "pp_flat" nu se refuza scurt - se explica ce sunt
    /// fisierele astea in iesirea Siril si de ce testul cere unul, inclusiv de ce un flat
    /// NECALIBRAT nu merge.
    /// </summary>
    [Fact]
    public void ChoosingSomethingElseExplainsWhatAFlatMasterIs()
    {
        using var f = new SessionFixture()
            .WithFlatMasters("pp_flat_stacked_A.fit", "pp_flat_stacked_B.fit");
        var vm = Attached(f);

        vm.Choose(isA: true, path: @"C:\undeva\poza.jpg");

        Assert.True(vm.HasError);
        Assert.Contains("pp_flat", vm.ErrorText);
        Assert.Contains("process/", vm.ErrorText);
        Assert.Contains("preprocesat", vm.ErrorText);
        Assert.Contains("180", vm.ErrorText);
    }

    /// <summary>
    /// Cadrul calibrat individual e capcana reala a tabului: sta in acelasi process/, are
    /// acelasi prefix si arata la fel ca masterul. Pana la corectarea tiparului dialogul
    /// il si propunea, iar perechea iesea cu un verdict sigur si fals. Refuzul spune
    /// explicit de ce, altfel arata ca un moft.
    /// </summary>
    [Fact]
    public void ChoosingAnIndividualCalibratedFlatIsRefusedAndExplained()
    {
        using var f = new SessionFixture()
            .WithFlatMasters("pp_flat_stacked_A.fit", "pp_flat_stacked_B.fit");
        var vm = Attached(f);

        vm.Choose(isA: true, path: f.MasterPath("pp_flat_00001.fit"));

        Assert.True(vm.HasError);
        Assert.Null(vm.FileA);
        Assert.Contains("pp_flat_00001.fit", vm.ErrorText);
        Assert.Contains("zgomotul unui singur cadru", vm.ErrorText);
    }

    [Fact]
    public void ARejectedChoiceDoesNotReplaceTheCurrentSelection()
    {
        using var f = new SessionFixture()
            .WithFlatMasters("pp_flat_stacked_A.fit", "pp_flat_stacked_B.fit");
        var vm = Attached(f);
        vm.Choose(isA: true, path: f.MasterPath("pp_flat_stacked_A.fit"));
        string before = vm.FileA;

        vm.Choose(isA: true, path: @"C:\undeva\flat_necalibrat.fit");

        Assert.Equal(before, vm.FileA);
    }

    /// <summary>
    /// Cazul NORMAL, nu exceptia: cele doua mastere vin din doua foldere de sesiune
    /// diferite, cate unul pentru fiecare orientare.
    /// </summary>
    [Fact]
    public void AFlatMasterFromAnotherSessionIsAccepted()
    {
        using var f = new SessionFixture().WithFlatMasters("pp_flat_stacked_A.fit");
        var vm = Attached(f);

        vm.Choose(isA: false, path: @"C:\alta-sesiune\process\pp_flat_stacked.fit");

        Assert.False(vm.HasError);
        Assert.Equal(@"C:\alta-sesiune\process\pp_flat_stacked.fit", vm.FileB);
    }

    /// <summary>
    /// Masterul general al unei sesiuni e exact ce cere modul, nu ceva de refuzat: in
    /// fluxul normal, ambele fisiere sunt "pp_flat_stacked.fit", din doua foldere.
    /// </summary>
    [Fact]
    public void TheOverallMasterOfAnotherSessionIsAccepted()
    {
        using var f = new SessionFixture().WithFlatMasters("pp_flat_stacked_A.fit");
        var vm = Attached(f);

        vm.Choose(isA: true, path: @"C:\sesiune-0deg\process\pp_flat_stacked.fit");
        vm.Choose(isA: false, path: @"C:\sesiune-180deg\process\pp_flat_stacked.fit");

        Assert.False(vm.HasError);
        Assert.EndsWith("pp_flat_stacked.fit", vm.FileA);
        Assert.EndsWith("pp_flat_stacked.fit", vm.FileB);
    }

    /// <summary>
    /// Acelasi fisier in ambele sloturi da o componenta externa de exact zero - care nu
    /// inseamna "setup curat", inseamna "nu s-a masurat nimic". Prins la ALEGERE, ca sa
    /// nu se piarda selectia buna de dinainte.
    /// </summary>
    [Fact]
    public void TheSameFileInBothSlotsIsRefusedAtChoiceTime()
    {
        using var f = new SessionFixture()
            .WithFlatMasters("pp_flat_stacked_A.fit", "pp_flat_stacked_B.fit");
        var vm = Attached(f);
        vm.Choose(isA: true, path: f.MasterPath("pp_flat_stacked_A.fit"));

        vm.Choose(isA: false, path: f.MasterPath("pp_flat_stacked_A.fit"));

        Assert.True(vm.HasError);
        Assert.Contains("DIFERITE", vm.ErrorText);
        Assert.Null(vm.FileB);
    }

    // ------------------------------------------------------------------ atribuire

    [Fact]
    public async Task ItAttributesTheStructureOfTheCombinedMaster()
    {
        using var f = new SessionFixture()
            .WithFlatMasters("pp_flat_stacked_A.fit", "pp_flat_stacked_B.fit");
        var vm = Attached(f);
        vm.Choose(isA: true, path: f.MasterPath("pp_flat_stacked_A.fit"));
        vm.Choose(isA: false, path: f.MasterPath("pp_flat_stacked_B.fit"));

        await vm.RecomputeAsync();

        Assert.True(vm.HasResult);
        Assert.NotEmpty(vm.Verdict);
        Assert.NotEmpty(vm.World);          // componenta fixa in lume, cu bara de eroare
        Assert.NotEmpty(vm.Corners);        // bateria de structura pe (A+B)/2
        Assert.NotEmpty(vm.Residual);
        Assert.NotEmpty(vm.ProvenanceA);    // cate cadre si de cand
        Assert.NotNull(vm.Map);
    }

    /// <summary>
    /// Verdictul tabului e verdictul de STRUCTURA al masterului combinat, pe aceeasi
    /// scara ca tabul 1 - deci si sfatul trebuie sa fie acelasi text, nu unul rescris.
    /// </summary>
    [Fact]
    public async Task TheAdviceComesFromTheSharedResidualScale()
    {
        using var f = new SessionFixture()
            .WithFlatMasters("pp_flat_stacked_A.fit", "pp_flat_stacked_B.fit");
        var vm = Attached(f);
        vm.Choose(isA: true, path: f.MasterPath("pp_flat_stacked_A.fit"));
        vm.Choose(isA: false, path: f.MasterPath("pp_flat_stacked_B.fit"));

        await vm.RecomputeAsync();

        Assert.Equal(StructureReport.Advice(vm.Verdict), vm.Advice);
    }

    /// <summary>
    /// Doua harti, fiindca modul desparte masterul in doua componente. Comutatorul
    /// schimba ce se vede, si cele doua chiar sunt diferite.
    /// </summary>
    [Fact]
    public async Task BothMapsAreAvailableAndTheToggleSwitchesThem()
    {
        using var f = new SessionFixture()
            .WithFlatMasters("pp_flat_stacked_A.fit", "pp_flat_stacked_B.fit");
        var vm = Attached(f);
        vm.Choose(isA: true, path: f.MasterPath("pp_flat_stacked_A.fit"));
        vm.Choose(isA: false, path: f.MasterPath("pp_flat_stacked_B.fit"));

        await vm.RecomputeAsync();
        byte[] sensor = vm.Map;

        vm.ShowWorldMap = true;

        Assert.NotNull(vm.Map);
        Assert.NotEqual(sensor, vm.Map);
    }

    /// <summary>Alegerea celui de-al doilea fisier declanseaza singura calculul.</summary>
    [Fact]
    public async Task ChoosingTheSecondFileComputes()
    {
        using var f = new SessionFixture()
            .WithFlatMasters("pp_flat_stacked_A.fit", "pp_flat_stacked_B.fit");
        var vm = Attached(f);

        vm.Choose(isA: true, path: f.MasterPath("pp_flat_stacked_A.fit"));
        Assert.False(vm.HasResult);

        vm.Choose(isA: false, path: f.MasterPath("pp_flat_stacked_B.fit"));
        await vm.RecomputeAsync();

        Assert.True(vm.HasResult);
    }

    [Fact]
    public async Task WithoutTwoFilesNothingIsComputed()
    {
        using var f = new SessionFixture().WithFlatMasters("pp_flat_stacked_A.fit");
        var vm = Attached(f);
        vm.Choose(isA: true, path: f.MasterPath("pp_flat_stacked_A.fit"));

        await vm.RecomputeAsync();

        Assert.False(vm.HasResult);
    }

    [Fact]
    public void ResetClearsBothChoices()
    {
        using var f = new SessionFixture()
            .WithFlatMasters("pp_flat_stacked_A.fit", "pp_flat_stacked_B.fit");
        var vm = Attached(f);
        vm.Choose(isA: true, path: f.MasterPath("pp_flat_stacked_A.fit"));

        vm.Reset();

        Assert.Null(vm.FileA);
        Assert.Null(vm.FileB);
        Assert.False(vm.ShowWorldMap);
        Assert.Null(vm.Map);
        Assert.Empty(vm.MapLegend);
    }

    // -------------------------------------------------------- scara hartii

    static async Task<RatioTabViewModel> Computed(SessionFixture f)
    {
        var vm = Attached(f);
        vm.Choose(isA: true, path: f.MasterPath("pp_flat_stacked_A.fit"));
        vm.Choose(isA: false, path: f.MasterPath("pp_flat_stacked_B.fit"));
        await vm.RecomputeAsync();
        return vm;
    }

    static MapScaleOption Cap(RatioTabViewModel vm, double? capPct)
        => vm.MapScale.Options.Single(o => o.Label == RunReport.MapScaleLabel(capPct));

    /// <summary>
    /// Modul are doua harti, si scara se aplica celei afisate - oricare din cele doua e.
    /// </summary>
    [Fact]
    public async Task TheScaleAppliesToBothMaps()
    {
        using var f = new SessionFixture()
            .WithFlatMasters("pp_flat_stacked_A.fit", "pp_flat_stacked_B.fit");
        var vm = await Computed(f);

        byte[] sensorAtTen = vm.Map;
        vm.ShowWorldMap = true;
        byte[] worldAtTen = vm.Map;

        Assert.NotEqual(sensorAtTen, worldAtTen);

        Cap(vm, Th.ResidPerfect).IsSelected = true;
        byte[] worldAtTwo = vm.Map;
        vm.ShowWorldMap = false;
        byte[] sensorAtTwo = vm.Map;

        Assert.NotEqual(worldAtTen, worldAtTwo);
        Assert.NotEqual(sensorAtTen, sensorAtTwo);
    }

    /// <summary>
    /// Pe scara automata cele doua componente au intervale diferite, deci legenda trebuie
    /// sa urmeze harta afisata - altfel ar descrie alta imagine decat cea de pe ecran.
    /// </summary>
    [Fact]
    public async Task TheLegendFollowsTheDisplayedMap()
    {
        using var f = new SessionFixture()
            .WithFlatMasters("pp_flat_stacked_A.fit", "pp_flat_stacked_B.fit");
        var vm = await Computed(f);
        Cap(vm, null).IsSelected = true;

        string sensor = vm.MapLegend;
        vm.ShowWorldMap = true;

        Assert.NotEmpty(sensor);
        Assert.NotEqual(sensor, vm.MapLegend);
    }

    /// <summary>Acelasi ghid ca la structura, si tot pliat implicit.</summary>
    [Fact]
    public void TheGuideIsThereBeforeAnythingIsChosen()
    {
        var vm = new RatioTabViewModel();

        Assert.NotEmpty(vm.MapGuide.Intro);
        Assert.NotEmpty(vm.MapGuide.Shapes);
        Assert.False(vm.MapGuide.IsExpanded);
    }
}
