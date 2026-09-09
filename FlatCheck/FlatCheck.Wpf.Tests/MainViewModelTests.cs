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
/// Deschiderea unui folder, cap-coada. Aici se verifica cerintele 3 si 4: ce porneste
/// singur, si ce se intampla cand folderul nu e bun.
/// </summary>
public class MainViewModelTests
{
    /// <summary>CERINTA 3: structura si testul end-to-end pornesc singure.</summary>
    [Fact]
    public async Task OpeningAFolderRunsStructureAndEndToEndByItself()
    {
        using var f = new SessionFixture().WithFlatMaster().WithLightSeries(6);
        var vm = new MainViewModel();

        await vm.OpenAsync(f.Root);

        Assert.True(vm.Structure.HasResult);
        Assert.True(vm.Light.HasResult);
        Assert.False(vm.IsRunning);
    }

    /// <summary>
    /// Tabul Ratio nu calculeaza la deschidere, oricate mastere ar fi in folder, si asta
    /// e o decizie: modul cere doua stive din DOUA orientari, adica din doua sedinte,
    /// deci din doua foldere. Ce poate propune un singur folder sunt cadre individuale,
    /// din care iese un rezultat sigur si fals.
    /// </summary>
    [Fact]
    public async Task RatioWaitsForTwoExplicitChoices()
    {
        using var f = new SessionFixture()
            .WithFlatMaster().WithFlatMasters("pp_flat_stacked_A.fit", "pp_flat_stacked_B.fit");
        var vm = new MainViewModel();

        await vm.OpenAsync(f.Root);

        Assert.False(vm.Ratio.HasResult);
        Assert.Null(vm.Ratio.FileA);
        Assert.Null(vm.Ratio.FileB);
    }

    /// <summary>Tabul de expunere isi ia lista, dar nu calculeaza pana nu se alege ceva.</summary>
    [Fact]
    public async Task ExposureGetsItsListButWaitsForAChoice()
    {
        using var f = new SessionFixture().WithFlatMaster().WithRawFlats("a.NEF", "b.NEF");
        var vm = new MainViewModel();

        await vm.OpenAsync(f.Root);

        Assert.Equal(2, vm.Exposure.Files.Count);
        Assert.False(vm.Exposure.HasResult);
    }

    // ------------------------------------------------------------------ folder invalid

    /// <summary>
    /// CERINTA 4: fara masterul de flat, aplicatia nu porneste pe folderul asta si spune
    /// exact ce sa faca - sa ruleze scriptul OSC_Preprocessing.ssf in Siril. Un mesaj care
    /// ar descrie doar fisierul lipsa l-ar lasa pe om sa caute singur legatura.
    /// </summary>
    [Fact]
    public async Task WithoutTheFlatMasterItNamesTheSirilScriptToRun()
    {
        using var f = new SessionFixture().WithProcess();
        var vm = new MainViewModel();

        await vm.OpenAsync(f.Root);

        Assert.True(vm.HasProblem);
        Assert.Contains("OSC_Preprocessing.ssf", vm.ProblemText);
        Assert.Contains("pp_flat_stacked.fit", vm.ProblemText);
        Assert.Contains("Siril", vm.ProblemText);
    }

    [Fact]
    public async Task WithoutProcessAtAllItAlsoNamesTheScript()
    {
        using var f = new SessionFixture();
        var vm = new MainViewModel();

        await vm.OpenAsync(f.Root);

        Assert.True(vm.HasProblem);
        Assert.Contains("OSC_Preprocessing.ssf", vm.ProblemText);
        Assert.Contains("process/", vm.ProblemText);
    }

    [Fact]
    public async Task AMissingFolderIsReportedAsSuch()
    {
        var vm = new MainViewModel();

        await vm.OpenAsync(Path.Combine(Path.GetTempPath(), "nu-exista-" + Guid.NewGuid()));

        Assert.True(vm.HasProblem);
        Assert.Equal(SessionMessages.Title(SessionProblem.NoFolder), vm.ProblemTitle);
    }

    /// <summary>Pe un folder invalid nu se calculeaza nimic: taburile raman goale.</summary>
    [Fact]
    public async Task NothingRunsOnAnInvalidFolder()
    {
        using var f = new SessionFixture().WithProcess();
        var vm = new MainViewModel();

        await vm.OpenAsync(f.Root);

        Assert.False(vm.Structure.HasResult);
        Assert.False(vm.Light.HasResult);
        Assert.False(vm.Ratio.HasResult);
        Assert.Empty(vm.Exposure.Files);
    }

    // ------------------------------------------------------------------ schimbarea folderului

    /// <summary>
    /// Alt folder inseamna alt inceput: rezultatele vechi dispar, in loc sa stea langa
    /// cele noi si sa se poata confunda.
    /// </summary>
    [Fact]
    public async Task OpeningAnotherFolderClearsTheOldResults()
    {
        using var good = new SessionFixture().WithFlatMaster().WithLightSeries(6);
        using var bad = new SessionFixture().WithProcess();
        var vm = new MainViewModel();

        await vm.OpenAsync(good.Root);
        Assert.True(vm.Structure.HasResult);

        await vm.OpenAsync(bad.Root);

        Assert.False(vm.Structure.HasResult);
        Assert.False(vm.Light.HasResult);
        Assert.Empty(vm.Light.Frames);
    }

    [Fact]
    public async Task TheFolderIsRememberedAndReported()
    {
        using var f = new SessionFixture().WithFlatMaster();
        var vm = new MainViewModel();

        Assert.False(vm.HasFolder);

        await vm.OpenAsync(f.Root);

        Assert.True(vm.HasFolder);
        Assert.Equal(f.Root, vm.Folder);
    }

    [Fact]
    public async Task AnEmptyFolderNameLeavesTheAppInItsStartingState()
    {
        var vm = new MainViewModel();

        await vm.OpenAsync("");

        Assert.False(vm.HasFolder);
        Assert.False(vm.HasProblem);
    }

    /// <summary>Anularea de la inchidere e idempotenta - se cheama si cand n-a rulat nimic.</summary>
    [Fact]
    public void CancellingWithNothingRunningIsHarmless()
    {
        var vm = new MainViewModel();

        vm.CancelPending();
        vm.CancelPending();
    }

    // ------------------------------------------------------------------ antet

    /// <summary>
    /// Fereastra arata acelasi banner ca prima linie a consolei. Vine din acelasi loc,
    /// deci un bump de versiune il muta in amandoua deodata.
    /// </summary>
    [Fact]
    public void TheHeaderIsTheSameVersionAndCopyrightTheConsolePrints()
    {
        var vm = new MainViewModel();

        Assert.Equal(VersionInfo.Banner, vm.Banner);
        Assert.Equal(VersionInfo.Copyright, vm.Copyright);
    }
}

/// <summary>Argumentul de linie de comanda: un folder, sau nimic.</summary>
public class AppStartupTests
{
    [Fact]
    public void AnExistingFolderInTheArgumentsIsUsed()
    {
        using var f = new SessionFixture();

        Assert.Equal(f.Root, App.FolderFromArgs(new[] { f.Root }));
    }

    /// <summary>
    /// Orice altceva se ignora. O fereastra care refuza sa porneasca din cauza unui
    /// argument gresit e mai enervanta decat una care intreaba ce folder vrei.
    /// </summary>
    [Fact]
    public void AnythingElseIsIgnoredRatherThanFatal()
    {
        Assert.Null(App.FolderFromArgs(null));
        Assert.Null(App.FolderFromArgs(Array.Empty<string>()));
        Assert.Null(App.FolderFromArgs(new[] { "", "  ", "--ceva", @"C:\nu\exista\aici" }));
    }

    [Fact]
    public void TheFirstExistingFolderWins()
    {
        using var f = new SessionFixture();

        Assert.Equal(f.Root, App.FolderFromArgs(new[] { "--flag", f.Root }));
    }
}
