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
    /// Dialogul pentru setul A porneste din folderul masterului sesiunii deschise - acolo
    /// sunt stivele, oriunde le-ar fi pus scriptul - si din process/ cand nu e master.
    /// </summary>
    [Fact]
    public void TheDialogStartsWhereTheSessionKeepsItsMaster()
    {
        using var inMasters = new SessionFixture().WithFlatMasterInMasters().WithProcess();
        using var inProcess = new SessionFixture().WithFlatMaster();
        using var none = new SessionFixture().WithProcess();

        Assert.Equal(inMasters.MastersDir, Attached(inMasters).InitialDirectory);
        Assert.Equal(inProcess.ProcessDir, Attached(inProcess).InitialDirectory);
        Assert.Equal(none.ProcessDir, Attached(none).InitialDirectory);
    }

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
        Assert.Equal(2, vm.WorldRows.Count);    // componenta fixa in lume, cu bara de eroare
        Assert.Equal(5, vm.Tests.Count);    // bateria de structura pe (A+B)/2
        Assert.All(vm.Tests, t => Assert.NotEmpty(t.Value));
        Assert.All(vm.Tests, t => Assert.True(t.HasVerdict));
        Assert.NotEmpty(vm.ProvenanceA);    // cate cadre si de cand
        Assert.NotNull(vm.Map);
    }

    /// <summary>
    /// Fiecare rand isi spune ce masoara, ca in tabul 1. Aici lipseau toate cinci, fiindca
    /// randurile erau blocuri de Grid scrise de mana in XAML, fara loc pentru o descriere.
    /// </summary>
    [Fact]
    public async Task EveryTestRowCarriesItsOwnDescription()
    {
        using var f = new SessionFixture().WithFlatMasters("pp_flat_stacked_A.fit", "pp_flat_stacked_B.fit");
        var vm = Attached(f);
        vm.Choose(isA: true, path: f.MasterPath("pp_flat_stacked_A.fit"));
        vm.Choose(isA: false, path: f.MasterPath("pp_flat_stacked_B.fit"));

        await vm.RecomputeAsync();

        Assert.All(vm.Tests, t => Assert.True(t.HasNote, "rand fara descriere: " + t.Label));
    }

    /// <summary>
    /// Cele doua randuri ale componentei fixe in lume. Primul are cifra cu bara de eroare
    /// si eticheta informativa drept pastila; al doilea, doar cifra. Amandoua cu descrierea
    /// din nucleu - al doilea nu avea niciun cuvant, primul nu spunea ce e pastila.
    /// </summary>
    [Fact]
    public async Task TheWorldRowsCarryTheirDescriptionsAndOnlyTheFirstHasAChip()
    {
        using var f = new SessionFixture().WithFlatMasters("pp_flat_stacked_A.fit", "pp_flat_stacked_B.fit");
        var vm = Attached(f);
        vm.Choose(isA: true, path: f.MasterPath("pp_flat_stacked_A.fit"));
        vm.Choose(isA: false, path: f.MasterPath("pp_flat_stacked_B.fit"));

        await vm.RecomputeAsync();

        Assert.Contains("gradient extern", vm.WorldRows[0].Label);
        Assert.Contains("+/-", vm.WorldRows[0].Value);
        Assert.True(vm.WorldRows[0].HasVerdict);
        Assert.Equal(RatioReport.WorldNote(), vm.WorldRows[0].Note);

        Assert.Contains("colturi", vm.WorldRows[1].Label);
        Assert.False(vm.WorldRows[1].HasVerdict);
        Assert.Equal(RatioReport.WorldCornerNote(), vm.WorldRows[1].Note);
    }

    /// <summary>
    /// Regula pentru tot ecranul, nu pentru un card: orice rand cu o cifra pe el are si
    /// un rand de text sub ea. Asta e testul care ar fi prins de la inceput ca doua
    /// randuri din Ratio n-aveau nimic.
    /// </summary>
    [Fact]
    public async Task EveryRowOnTheScreenCarriesADescription()
    {
        using var f = new SessionFixture().WithFlatMasters("pp_flat_stacked_A.fit", "pp_flat_stacked_B.fit");
        var vm = Attached(f);
        vm.Choose(isA: true, path: f.MasterPath("pp_flat_stacked_A.fit"));
        vm.Choose(isA: false, path: f.MasterPath("pp_flat_stacked_B.fit"));

        await vm.RecomputeAsync();

        var all = vm.WorldRows.Concat(vm.Tests).Concat(vm.Informative).ToList();
        Assert.Equal(2 + 5 + 3, all.Count);
        Assert.All(all, r => Assert.True(r.HasNote, "rand fara descriere: " + r.Label));
        Assert.All(all, r => Assert.True(r.Note!.Length > 30, "descriere prea scurta: " + r.Label));
    }

    /// <summary>
    /// Si marimile informative, ca in consola: raportul --ratio tipareste tot WriteBody,
    /// inclusiv vignetarea, centrul de iluminare si petele locale ale masterului combinat.
    /// Fara verdict, fiindca nu sunt teste; si fara linia de niveluri ADU, pe care nici
    /// consola n-o tipareste in --ratio, fiindca masterul combinat e normalizat la 1.
    /// </summary>
    [Fact]
    public async Task TheInformativeRowsAreShownWithoutVerdict()
    {
        using var f = new SessionFixture().WithFlatMasters("pp_flat_stacked_A.fit", "pp_flat_stacked_B.fit");
        var vm = Attached(f);
        vm.Choose(isA: true, path: f.MasterPath("pp_flat_stacked_A.fit"));
        vm.Choose(isA: false, path: f.MasterPath("pp_flat_stacked_B.fit"));

        await vm.RecomputeAsync();

        Assert.Equal(3, vm.Informative.Count);
        Assert.All(vm.Informative, r => Assert.False(r.HasVerdict));
        Assert.All(vm.Informative, r => Assert.True(r.HasNote, "rand fara descriere: " + r.Label));
        Assert.Contains(vm.Informative, r => r.Label.Contains("vignetare"));
        Assert.Contains(vm.Informative, r => r.Label.Contains("offset"));
        Assert.Contains(vm.Informative, r => r.Label.Contains("pete locale"));
        Assert.DoesNotContain(vm.Informative, r => r.Label.Contains("ADU"));
        Assert.Empty(vm.LocalSpotWarning);       // camp radial curat: nimic de atentionat
    }

    /// <summary>
    /// Cele doua taburi arata ACEEASI baterie - Ratio o ruleaza pe masterul combinat, dar
    /// analizorul e acelasi - deci etichetele, ordinea si descrierile trebuie sa fie
    /// identice. Erau doua compuneri separate, si au divergat: al cincilea test a trebuit
    /// adaugat de doua ori, iar descrierile au ajuns doar intr-un tab.
    /// </summary>
    [Fact]
    public async Task TheRowsAreTheSameOnesTheStructureTabShows()
    {
        using var rf = new SessionFixture().WithFlatMasters("pp_flat_stacked_A.fit", "pp_flat_stacked_B.fit");
        var ratio = Attached(rf);
        ratio.Choose(isA: true, path: rf.MasterPath("pp_flat_stacked_A.fit"));
        ratio.Choose(isA: false, path: rf.MasterPath("pp_flat_stacked_B.fit"));
        await ratio.RecomputeAsync();

        using var sf = new SessionFixture().WithFlatMaster();
        var structure = new StructureTabViewModel();
        await structure.RunAsync(sf.Session, CancellationToken.None);

        Assert.Equal(structure.Tests.Select(t => t.Label), ratio.Tests.Select(t => t.Label));
        Assert.Equal(structure.Tests.Select(t => t.Note), ratio.Tests.Select(t => t.Note));
        Assert.Equal(structure.Tests.Select(t => t.IsKey), ratio.Tests.Select(t => t.IsKey));
        Assert.Equal(structure.Informative.Select(t => t.Label), ratio.Informative.Select(t => t.Label));
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

    /// <summary>
    /// O realegere in timpul calculului nu asteapta si nu concureaza: o depaseste. Pe
    /// ecran ramane perechea aleasa ultima, oricare din cele doua rulari termina prima.
    /// Inainte, cele doua continuari scriau in acelasi cache de pe doua fire si testul
    /// de mai sus pica din cand in cand.
    /// </summary>
    [Fact]
    public async Task ASecondPickSupersedesTheFirst()
    {
        using var f = new SessionFixture()
            .WithFlatMasters("pp_flat_stacked_A.fit", "pp_flat_stacked_B.fit", "pp_flat_stacked_C.fit");
        var vm = Attached(f);

        vm.Choose(isA: true, path: f.MasterPath("pp_flat_stacked_A.fit"));
        vm.Choose(isA: false, path: f.MasterPath("pp_flat_stacked_B.fit"));   // porneste A+B
        Task first = vm.Pending;
        vm.Choose(isA: true, path: f.MasterPath("pp_flat_stacked_C.fit"));    // peste ea: C+B
        Task second = vm.Pending;
        await Task.WhenAll(first, second);

        Assert.True(vm.HasResult);
        Assert.False(vm.IsBusy);
        Assert.False(vm.HasError);
        Assert.Contains("pp_flat_stacked_C.fit", vm.Report);
        Assert.DoesNotContain("pp_flat_stacked_A.fit", vm.Report);
        Assert.NotNull(vm.Map);
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
