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
/// Tabul de structura. Cel mai important test din fisier e cel care compara raportul
/// salvat cu iesirea consolei, caracter cu caracter: exact aia e cerinta, iar daca
/// fereastra ar incepe sa-si compuna singura textul, s-ar rupe tacut.
/// </summary>
public class StructureTabTests
{
    static async Task<StructureTabViewModel> Run(SessionFixture f)
    {
        var vm = new StructureTabViewModel();
        await vm.RunAsync(f.Session, CancellationToken.None);
        return vm;
    }

    [Fact]
    public async Task ItAnalysesTheConventionalFlatMaster()
    {
        using var f = new SessionFixture().WithFlatMaster();

        var vm = await Run(f);

        Assert.True(vm.HasResult);
        Assert.False(vm.HasError);
        Assert.Equal(f.Session.FlatMasterPath, vm.Source);
        Assert.NotEmpty(vm.Verdict);
    }

    /// <summary>
    /// CERINTA 5, literal: fisierul salvat trebuie sa contina EXACT iesirea consolei.
    /// Se compara cu textul produs de acelasi strat pe care il foloseste flatcheck.exe -
    /// deci daca cineva ar reformata ceva doar pentru fereastra, testul cade.
    /// </summary>
    [Fact]
    public async Task TheSavedReportIsCharacterForCharacterTheConsoleOutput()
    {
        using var f = new SessionFixture().WithFlatMaster();
        var session = f.Session;

        var vm = await Run(f);

        var expected = new StringWriter();
        RunReport.WriteBanner(expected);
        StructureReport.Write(
            StructureAnalyzer.Analyze(ImageLoader.LoadImage(session.FlatMasterPath)),
            session.FlatMasterPath, expected);
        RunReport.WriteMapSaved(session.ResidualPngPath, expected);

        Assert.Equal(expected.ToString(), File.ReadAllText(session.ResultTextPath));
        Assert.Equal(expected.ToString(), vm.Report);
    }

    /// <summary>Antetul e primul lucru din fisier: un raport lipit spune singur ce l-a produs.</summary>
    [Fact]
    public async Task TheSavedReportStartsWithTheVersionAndCopyright()
    {
        using var f = new SessionFixture().WithFlatMaster();

        await Run(f);

        var lines = File.ReadAllLines(f.Session.ResultTextPath);
        Assert.Equal(VersionInfo.Banner, lines[0]);
        Assert.Equal(VersionInfo.Copyright, lines[1]);
    }

    [Fact]
    public async Task TheReportAndTheMapLandInTheChosenFolder()
    {
        using var f = new SessionFixture().WithFlatMaster();
        var session = f.Session;

        var vm = await Run(f);

        Assert.True(File.Exists(session.ResultTextPath));
        Assert.True(File.Exists(session.ResidualPngPath));
        Assert.Equal(session.ResultTextPath, vm.SavedTo);
        Assert.Empty(vm.SaveError);
    }

    /// <summary>
    /// Ce se vede pe ecran si ce se salveaza pe disc sunt aceiasi octeti. Altfel harta
    /// din fereastra ar putea arata altceva decat fisierul trimis mai departe.
    /// </summary>
    [Fact]
    public async Task TheDisplayedMapIsTheSameFileThatWasSaved()
    {
        using var f = new SessionFixture().WithFlatMaster();

        var vm = await Run(f);

        Assert.Equal(File.ReadAllBytes(f.Session.ResidualPngPath), vm.Map);
    }

    // ------------------------------------------------------------------ randurile

    /// <summary>
    /// Cele patru teste apar toate, in ordinea din consola, si fiecare cu verdictul lui.
    /// </summary>
    [Fact]
    public async Task TheFourDeviationTestsAreShownWithTheirOwnVerdicts()
    {
        using var f = new SessionFixture().WithFlatMaster();

        var vm = await Run(f);

        Assert.Equal(4, vm.Tests.Count);
        Assert.All(vm.Tests, t => Assert.True(t.HasVerdict));
        Assert.Contains("colturi", vm.Tests[0].Label);
        Assert.Contains("azimutala", vm.Tests[1].Label);
        Assert.Contains("reziduu", vm.Tests[2].Label);
        Assert.Contains("nclinare", vm.Tests[3].Label);
    }

    /// <summary>Reziduul e cel care conteaza cel mai mult, si se vede ca atare.</summary>
    [Fact]
    public async Task OnlyTheResidualRowIsMarkedAsTheKeyOne()
    {
        using var f = new SessionFixture().WithFlatMaster();

        var vm = await Run(f);

        var key = Assert.Single(vm.Tests.Where(t => t.IsKey));
        Assert.Contains("reziduu", key.Label);
    }

    /// <summary>
    /// Randurile informative NU poarta verdict. Offsetul centrului de iluminare a fost
    /// un test pana in 1.2.0 si nu mai e - nu e scale-invariant, iar peste un prag nici
    /// macar monoton - deci o pastila colorata langa el ar reintroduce exact greseala.
    /// </summary>
    [Fact]
    public async Task TheInformativeRowsCarryNoVerdict()
    {
        using var f = new SessionFixture().WithFlatMaster();

        var vm = await Run(f);

        Assert.Equal(2, vm.Informative.Count);
        Assert.All(vm.Informative, r => Assert.False(r.HasVerdict));
        Assert.Contains(vm.Informative, r => r.Label.Contains("vignetare"));
        Assert.Contains(vm.Informative, r => r.Label.Contains("offset"));
    }

    /// <summary>
    /// Scara vine din StructureReport.Scale(), deci din aceleasi praguri ca raportul, si
    /// banda in care a cazut verdictul e marcata.
    /// </summary>
    [Fact]
    public async Task TheScaleHasFourBandsAndMarksTheCurrentOne()
    {
        using var f = new SessionFixture().WithFlatMaster();

        var vm = await Run(f);

        Assert.Equal(4, vm.Scale.Count);
        Assert.Equal(new[] { "PERFECT", "BUN", "ACCEPTABIL", "REFA" },
                     vm.Scale.Select(s => s.Band).ToArray());

        var current = Assert.Single(vm.Scale.Where(s => s.IsCurrent));
        Assert.Equal(vm.Verdict, current.Band);
    }

    /// <summary>Sfatul vine din nucleu, nu e rescris in fereastra.</summary>
    [Fact]
    public async Task TheAdviceIsTheOneTheReportWouldPrint()
    {
        using var f = new SessionFixture().WithFlatMaster();

        var vm = await Run(f);

        Assert.Equal(StructureReport.Advice(vm.Verdict), vm.Advice);
    }

    // ------------------------------------------------------------------ esecuri

    /// <summary>
    /// Un fisier care nu e FITS: analiza pica, dar tabul ramane intreg si spune de ce.
    /// </summary>
    [Fact]
    public async Task ACorruptMasterBecomesAnExplanationNotACrash()
    {
        using var f = new SessionFixture().WithProcess();
        File.WriteAllText(Path.Combine(f.ProcessDir, SirilSession.FlatMasterName), "nu e FITS");

        var vm = await Run(f);

        Assert.False(vm.HasResult);
        Assert.True(vm.HasError);
        Assert.NotEmpty(vm.ErrorText);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task TheBusyFlagIsClearedWhetherItSucceedsOrFails()
    {
        using var ok = new SessionFixture().WithFlatMaster();
        using var bad = new SessionFixture().WithProcess();
        File.WriteAllText(Path.Combine(bad.ProcessDir, SirilSession.FlatMasterName), "x");

        Assert.False((await Run(ok)).IsBusy);
        Assert.False((await Run(bad)).IsBusy);
    }

    /// <summary>Golirea sterge tot, ca sa nu ramana cifre din folderul precedent.</summary>
    [Fact]
    public async Task ResetClearsEverything()
    {
        using var f = new SessionFixture().WithFlatMaster();
        var vm = await Run(f);

        vm.Reset();

        Assert.False(vm.HasResult);
        Assert.Empty(vm.Tests);
        Assert.Empty(vm.Informative);
        Assert.Empty(vm.Scale);
        Assert.Empty(vm.Verdict);
        Assert.Empty(vm.Report);
        Assert.Null(vm.Map);
        Assert.Empty(vm.SavedTo);
    }
}
