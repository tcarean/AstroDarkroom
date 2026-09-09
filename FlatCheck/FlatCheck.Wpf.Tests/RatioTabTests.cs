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
/// Tabul de rotatie 180. Ce se verifica: lista se filtreaza corect, alegerea de mana e
/// validata cu explicatie, si orice schimbare recalculeaza.
/// </summary>
public class RatioTabTests
{
    static RatioTabViewModel Attached(SessionFixture f)
    {
        var vm = new RatioTabViewModel();
        vm.Attach(f.Session, CancellationToken.None);
        return vm;
    }

    // ------------------------------------------------------------------ lista

    /// <summary>Cerinta 6: lista propune doar masterele de flat din process/.</summary>
    [Fact]
    public void OnlyFlatMastersAreOffered()
    {
        using var f = new SessionFixture()
            .WithFlatMasters("pp_flat_A.fit", "pp_flat_B.fit")
            .WithLightSeries(2);

        var vm = Attached(f);

        Assert.All(vm.Candidates, c => Assert.True(SirilSession.IsFlatMasterName(c)));
        Assert.DoesNotContain(vm.Candidates, c => c.Contains("pp_light"));
    }

    /// <summary>
    /// Cu exact doua mastere - cazul obisnuit, setul A si setul B - tabul e gata sa
    /// calculeze fara nicio alegere.
    /// </summary>
    [Fact]
    public void TwoMastersArePreselectedAsAAndB()
    {
        using var f = new SessionFixture().WithFlatMasters("pp_flat_A.fit", "pp_flat_B.fit");

        var vm = Attached(f);

        Assert.EndsWith("pp_flat_A.fit", vm.FileA);
        Assert.EndsWith("pp_flat_B.fit", vm.FileB);
    }

    /// <summary>
    /// Cu mai multe mastere se aleg CAPETELE listei, nu primele doua. Cerut de utilizator:
    /// pe un folder unde apare si masterul combinat, sau un al treilea set, primele doua
    /// n-ar mai fi perechea de comparat.
    /// </summary>
    [Fact]
    public void WithMoreThanTwoMastersTheLastOneIsPreselectedAsB()
    {
        using var f = new SessionFixture()
            .WithFlatMasters("pp_flat_A.fit", "pp_flat_B.fit", "pp_flat_C.fit", "pp_flat_D.fit");

        var vm = Attached(f);

        Assert.EndsWith("pp_flat_A.fit", vm.FileA);
        Assert.EndsWith("pp_flat_D.fit", vm.FileB);
    }

    /// <summary>Sortarea e ordinala, deci "ultimul" e ultimul din lista afisata.</summary>
    [Fact]
    public void TheDefaultForBIsTheLastEntryInTheList()
    {
        using var f = new SessionFixture()
            .WithFlatMasters("pp_flat_A.fit", "pp_flat_B.fit", "pp_flat_C.fit");

        var vm = Attached(f);

        Assert.Equal(vm.Candidates[0], vm.FileA);
        Assert.Equal(vm.Candidates[^1], vm.FileB);
    }

    [Fact]
    public void WithASingleMasterOnlyTheFirstSlotIsFilled()
    {
        using var f = new SessionFixture().WithFlatMasters("pp_flat_A.fit");

        var vm = Attached(f);

        Assert.NotNull(vm.FileA);
        Assert.Null(vm.FileB);
    }

    // ------------------------------------------------------------------ validare

    /// <summary>
    /// CERINTA 6, partea a doua: un fisier care nu incepe cu "pp_flat" nu se refuza
    /// scurt - se explica ce sunt fisierele astea in iesirea Siril si de ce testul cere
    /// unul. Mesajul trebuie sa spuna si de ce un flat NECALIBRAT nu merge.
    /// </summary>
    [Fact]
    public void ChoosingSomethingElseExplainsWhatAFlatMasterIs()
    {
        using var f = new SessionFixture().WithFlatMasters("pp_flat_A.fit", "pp_flat_B.fit");
        var vm = Attached(f);

        vm.Choose(isA: true, path: @"C:\undeva\poza.jpg");

        Assert.True(vm.HasError);
        Assert.Contains("pp_flat", vm.ErrorText);
        Assert.Contains("process/", vm.ErrorText);
        Assert.Contains("preprocesat", vm.ErrorText);
        Assert.Contains("180", vm.ErrorText);
    }

    [Fact]
    public void ARejectedChoiceDoesNotReplaceTheCurrentSelection()
    {
        using var f = new SessionFixture().WithFlatMasters("pp_flat_A.fit", "pp_flat_B.fit");
        var vm = Attached(f);
        string before = vm.FileA;

        vm.Choose(isA: true, path: @"C:\undeva\flat_necalibrat.fit");

        Assert.Equal(before, vm.FileA);
    }

    /// <summary>Un master dintr-un alt folder e acceptat si intra in lista.</summary>
    [Fact]
    public void AFlatMasterFromAnotherFolderIsAccepted()
    {
        using var f = new SessionFixture().WithFlatMasters("pp_flat_A.fit", "pp_flat_B.fit");
        var vm = Attached(f);

        vm.Choose(isA: false, path: @"C:\alta-sesiune\pp_flat_vechi.fit");

        Assert.False(vm.HasError);
        Assert.Equal(@"C:\alta-sesiune\pp_flat_vechi.fit", vm.FileB);
        Assert.Contains(@"C:\alta-sesiune\pp_flat_vechi.fit", vm.Candidates);
    }

    // ------------------------------------------------------------------ calcul

    [Fact]
    public async Task ItComparesTheTwoChosenMasters()
    {
        using var f = new SessionFixture().WithFlatMasters("pp_flat_A.fit", "pp_flat_B.fit");
        var vm = Attached(f);

        await vm.RecomputeAsync();

        Assert.True(vm.HasResult);
        Assert.NotEmpty(vm.Verdict);
        Assert.NotEmpty(vm.Structure);
        Assert.NotEmpty(vm.Corners);
        Assert.NotNull(vm.Map);
    }

    /// <summary>Aplicatia trebuie sa permita schimbarea fisierelor si recalcularea.</summary>
    [Fact]
    public async Task ChangingAFileRecomputes()
    {
        using var f = new SessionFixture()
            .WithFlatMasters("pp_flat_A.fit", "pp_flat_B.fit", "pp_flat_C.fit");
        var vm = Attached(f);

        await vm.RecomputeAsync();
        string first = vm.Structure;

        // Cel din MIJLOC: ultimul e deja preselectat pentru B, deci a-l pune din nou
        // n-ar schimba nimic si testul ar trece fara sa verifice ceva.
        vm.FileB = vm.Candidates.Single(c => c.EndsWith("pp_flat_B.fit"));
        await vm.RecomputeAsync();

        Assert.NotEqual(first, vm.Structure);
    }

    /// <summary>
    /// Acelasi fisier de doua ori da 1 peste tot, deci un "OK" care nu inseamna nimic.
    /// Se opreste inainte, cu explicatie.
    /// </summary>
    [Fact]
    public async Task ComparingAFileWithItselfIsRefused()
    {
        using var f = new SessionFixture().WithFlatMasters("pp_flat_A.fit", "pp_flat_B.fit");
        var vm = Attached(f);
        vm.FileB = vm.FileA;

        await vm.RecomputeAsync();

        Assert.False(vm.HasResult);
        Assert.True(vm.HasError);
        Assert.Contains("DIFERITE", vm.ErrorText);
    }

    [Fact]
    public async Task WithoutTwoFilesNothingIsComputed()
    {
        using var f = new SessionFixture().WithFlatMasters("pp_flat_A.fit");
        var vm = Attached(f);

        await vm.RecomputeAsync();

        Assert.False(vm.HasResult);
        Assert.False(vm.HasError);
    }

    /// <summary>Doua mastere cu dimensiuni diferite: eroare explicata, nu crash.</summary>
    [Fact]
    public async Task MismatchedSizesBecomeAnExplanation()
    {
        using var f = new SessionFixture().WithFlatMasters("pp_flat_A.fit");
        File.WriteAllBytes(Path.Combine(f.ProcessDir, "pp_flat_mic.fit"),
            FlatCheck.Tests.FitsTestData.BuildFits(
                64, 64, FlatCheck.Tests.FitsTestData.Radial(64, 64)));

        var vm = Attached(f);
        await vm.RecomputeAsync();

        Assert.True(vm.HasError);
        Assert.Contains("dimensiuni diferite", vm.ErrorText);
    }

    [Fact]
    public async Task TheReportIsTheConsoleText()
    {
        using var f = new SessionFixture().WithFlatMasters("pp_flat_A.fit", "pp_flat_B.fit");
        var vm = Attached(f);

        await vm.RecomputeAsync();

        var expected = new StringWriter();
        RunReport.WriteBanner(expected);
        RatioRun.Execute(vm.FileA, vm.FileB, expected);

        Assert.Equal(expected.ToString(), vm.Report);
    }
}
