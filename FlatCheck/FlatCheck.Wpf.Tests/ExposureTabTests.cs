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
/// Tabul de expunere. Comportamentul cerut e simplu - alegi un fisier, se calculeaza;
/// comuti "Sky", se recalculeaza - dar al doilea e si cel usor de uitat, si atunci un
/// sky flat corect expus la 22 % ar fi raportat ca subexpus.
/// </summary>
public class ExposureTabTests
{
    static ExposureTabViewModel Attached(SessionFixture f)
    {
        var vm = new ExposureTabViewModel();
        vm.Attach(f.Session, CancellationToken.None);
        return vm;
    }

    // ------------------------------------------------------------------ lista

    /// <summary>Cerinta 8: lista vine din flats/, si contine doar raw de aparat.</summary>
    [Fact]
    public void TheListComesFromFlatsAndHoldsOnlyCameraRaws()
    {
        using var f = new SessionFixture().WithFlatMaster()
            .WithRawFlats("DSC_0001.NEF", "DSC_0002.NEF", "note.txt", "convertit.fit");

        var vm = Attached(f);

        Assert.Equal(2, vm.Files.Count);
        Assert.All(vm.Files, p => Assert.True(RawReader.IsRawPath(p)));
    }

    /// <summary>
    /// Fara raw-uri, tabul explica de ce modul cere un cadru brut - stivuirea
    /// normalizeaza exact nivelul care se masoara aici.
    /// </summary>
    [Fact]
    public void WithoutRawFlatsItExplainsWhyAMasterWouldNotDo()
    {
        using var f = new SessionFixture().WithFlatMaster().WithFlats();

        var vm = Attached(f);

        Assert.True(vm.HasError);
        Assert.Contains("BRUT", vm.ErrorText);
        Assert.Contains("normalizeaza", vm.ErrorText);
        Assert.Contains(".nef", vm.ErrorText);
    }

    [Fact]
    public void NothingIsSelectedUntilTheUserPicks()
    {
        using var f = new SessionFixture().WithFlatMaster().WithRawFlats("a.NEF");

        var vm = Attached(f);

        Assert.Null(vm.Selected);
        Assert.False(vm.HasResult);
    }

    // ------------------------------------------------------------------ calcul

    [Fact]
    public async Task PickingAFileMeasuresIt()
    {
        using var f = new SessionFixture().WithFlatMaster();
        string path = f.WriteAnalysableRawFlat("flat.fit");

        var vm = Attached(f);
        vm.Selected = path;
        await vm.RecomputeAsync();

        Assert.True(vm.HasResult);
        Assert.NotEmpty(vm.Centre);
        Assert.NotEmpty(vm.Verdict);
        Assert.True(vm.IsRawFlat);
    }

    /// <summary>
    /// CERINTA 8: comutatorul "Sky" schimba pragurile, deci si verdictul, pe acelasi
    /// fisier. Nivelul masurat ramane identic - se schimba doar cum se citeste.
    /// </summary>
    [Fact]
    public async Task TheSkySwitchChangesTheVerdictWithoutChangingTheMeasurement()
    {
        using var f = new SessionFixture().WithFlatMaster();
        string path = f.WriteAnalysableRawFlat("flat.fit");

        var vm = Attached(f);
        vm.Selected = path;
        await vm.RecomputeAsync();
        string panelVerdict = vm.Verdict, level = vm.Centre;

        vm.Sky = true;
        await vm.RecomputeAsync();

        Assert.Equal(level, vm.Centre);              // aceeasi masuratoare
        Assert.NotEqual(panelVerdict, vm.Verdict);   // alta citire
    }

    /// <summary>Sfatul pentru sky flat spune explicit sa NU se forteze mai sus.</summary>
    [Fact]
    public async Task TheSkyAdviceWarnsAgainstPushingHigher()
    {
        using var f = new SessionFixture().WithFlatMaster();
        string path = f.WriteAnalysableRawFlat("flat.fit", centre: 20000);

        var vm = Attached(f);
        vm.Sky = true;
        vm.Selected = path;
        await vm.RecomputeAsync();

        Assert.Contains("sky flat", vm.Advice, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PickingAnotherFileRecomputes()
    {
        using var f = new SessionFixture().WithFlatMaster();
        string bright = f.WriteAnalysableRawFlat("bright.fit", centre: 34000);
        string dim = f.WriteAnalysableRawFlat("dim.fit", centre: 12000);

        var vm = Attached(f);
        vm.Selected = bright;
        await vm.RecomputeAsync();
        string first = vm.Centre;

        vm.Selected = dim;
        await vm.RecomputeAsync();

        Assert.NotEqual(first, vm.Centre);
    }

    /// <summary>
    /// Gate-ul de sanitate: un fisier deja calibrat sau debayerizat nu primeste verdict,
    /// ci explicatia - inclusiv cauza tipica, "am deschis NEF-ul in Siril si am dat Save".
    /// </summary>
    [Fact]
    public async Task ADebayeredFileGetsAnExplanationInsteadOfAVerdict()
    {
        using var f = new SessionFixture().WithFlatMaster().WithFlats();
        string path = Path.Combine(f.FlatsDir, "debayerizat.fit");
        File.WriteAllBytes(path, FlatCheck.Tests.FitsTestData.BuildFits(
            128, 128, FlatCheck.Tests.FitsTestData.Radial(128, 128, center: 34000)));

        var vm = Attached(f);
        vm.Selected = path;
        await vm.RecomputeAsync();

        Assert.True(vm.HasResult);       // a rulat
        Assert.False(vm.IsRawFlat);      // dar fisierul nu e ce cere modul
        Assert.Equal(Stats.NotApplicable, vm.Verdict);
        Assert.Contains("DEBAYERIZAT", vm.Advice);
        Assert.Contains("Conversion", vm.Advice);
    }

    [Fact]
    public async Task ACorruptFileBecomesAnExplanationNotACrash()
    {
        using var f = new SessionFixture().WithFlatMaster().WithFlats();
        string path = Path.Combine(f.FlatsDir, "stricat.NEF");
        File.WriteAllText(path, "nu e un raw");

        var vm = Attached(f);
        vm.Selected = path;
        await vm.RecomputeAsync();

        Assert.False(vm.HasResult);
        Assert.True(vm.HasError);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task TheReportIsTheConsoleText()
    {
        using var f = new SessionFixture().WithFlatMaster();
        string path = f.WriteAnalysableRawFlat("flat.fit");

        var vm = Attached(f);
        vm.Selected = path;
        await vm.RecomputeAsync();

        var expected = new StringWriter();
        RunReport.WriteBanner(expected);
        ExposureRun.Execute(path, null, null, false, expected);

        Assert.Equal(expected.ToString(), vm.Report);
    }

    [Fact]
    public async Task ResetClearsEverything()
    {
        using var f = new SessionFixture().WithFlatMaster();
        string path = f.WriteAnalysableRawFlat("flat.fit");
        var vm = Attached(f);
        vm.Selected = path;
        await vm.RecomputeAsync();

        vm.Reset();

        Assert.Empty(vm.Files);
        Assert.Null(vm.Selected);
        Assert.False(vm.HasResult);
        Assert.Empty(vm.Verdict);
    }
}
