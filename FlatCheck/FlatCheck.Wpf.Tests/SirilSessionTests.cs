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

using FlatCheck.Tests;

namespace FlatCheck.Wpf.Tests;

/// <summary>
/// Deducerea structurii de foldere. E singura parte a aplicatiei WPF care are reguli
/// adevarate - restul e asezare - deci e si partea care merita cele mai multe teste.
/// </summary>
public class SirilSessionTests
{
    /// <summary>Un folder de sesiune ca cel produs de scripturile Siril.</summary>
    static SirilSession Session(TempWorkspace ws, bool process = true, bool master = true,
                                bool flats = true)
    {
        if (process) Directory.CreateDirectory(Path.Combine(ws.Dir, "process"));
        if (flats) Directory.CreateDirectory(Path.Combine(ws.Dir, "flats"));
        if (master)
            File.WriteAllBytes(Path.Combine(ws.Dir, "process", SirilSession.FlatMasterName),
                               FitsTestData.BuildFits(32, 32, FitsTestData.Radial(32, 32)));
        return new SirilSession(ws.Dir);
    }

    // ------------------------------------------------------------------ validare

    [Fact]
    public void ACompleteSessionFolderPasses()
    {
        using var ws = new TempWorkspace();
        Assert.Equal(SessionProblem.None, Session(ws).Check());
    }

    [Fact]
    public void AMissingFolderIsItsOwnProblem()
    {
        var s = new SirilSession(Path.Combine(Path.GetTempPath(), "nu-exista-" + Guid.NewGuid()));
        Assert.Equal(SessionProblem.NoFolder, s.Check());
    }

    /// <summary>
    /// Fara process/ nu s-a rulat niciun script. E alta greseala decat "am rulat, dar
    /// masterul nu s-a produs", si de asta e alt cod.
    /// </summary>
    [Fact]
    public void AFolderWithoutProcessIsDistinctFromOneWithoutTheMaster()
    {
        using var a = new TempWorkspace();
        using var b = new TempWorkspace();

        Assert.Equal(SessionProblem.NoProcessDir, Session(a, process: false, master: false).Check());
        Assert.Equal(SessionProblem.NoFlatMaster, Session(b, master: false).Check());
    }

    /// <summary>Cerinta 4: intrarea modului 1 se ia prin conventie, nu se cauta.</summary>
    [Fact]
    public void TheFlatMasterIsTakenByConventionFromProcess()
    {
        using var ws = new TempWorkspace();
        var s = Session(ws);

        Assert.Equal(Path.Combine(ws.Dir, "process", "pp_flat_stacked.fit"), s.FlatMasterPath);
    }

    /// <summary>Cerinta 5: raportul se salveaza in folderul dat, nu in process/.</summary>
    [Fact]
    public void TheReportGoesInTheChosenFolderNotInProcess()
    {
        using var ws = new TempWorkspace();
        var s = Session(ws);

        Assert.Equal(Path.Combine(ws.Dir, "Rezultat testare.txt"), s.ResultTextPath);
        Assert.Equal(Path.Combine(ws.Dir, "flatcheck.png"), s.ResidualPngPath);
    }

    // ------------------------------------------------------------------ seria de lights

    [Fact]
    public void TheLightSeriesUsesTheSameDefaultPatternAsTheConsole()
    {
        using var ws = new TempWorkspace();
        var s = Session(ws);
        string proc = s.ProcessDir;

        File.WriteAllText(Path.Combine(proc, "pp_light_00001.fit"), "x");
        File.WriteAllText(Path.Combine(proc, "pp_light_00002.fit"), "x");

        Assert.Equal(2, s.LightSeries().Count);
    }

    /// <summary>
    /// Cadrele registrate sunt excluse fiindca tiparul e ancorat la inceput. Nu e un
    /// detaliu: registrarea transforma geometric cadrul, deci muta eroarea de flat fata
    /// de centrul in jurul caruia se fiteaza modelul radial.
    /// </summary>
    [Fact]
    public void RegisteredFramesAreExcludedFromTheSeries()
    {
        using var ws = new TempWorkspace();
        var s = Session(ws);

        File.WriteAllText(Path.Combine(s.ProcessDir, "pp_light_00001.fit"), "x");
        File.WriteAllText(Path.Combine(s.ProcessDir, "r_pp_light_00001.fit"), "x");

        Assert.Equal(new[] { "pp_light_00001.fit" },
                     s.LightSeries().Select(Path.GetFileName).ToArray());
    }

    /// <summary>Potrivirea e in proces, deci ".fit" nu inghite ".fits" ca in Win32.</summary>
    [Fact]
    public void TheSeriesDoesNotSwallowLongerExtensions()
    {
        using var ws = new TempWorkspace();
        var s = Session(ws);

        File.WriteAllText(Path.Combine(s.ProcessDir, "pp_light_00001.fit"), "x");
        File.WriteAllText(Path.Combine(s.ProcessDir, "pp_light_00002.fits"), "x");

        Assert.Single(s.LightSeries());
    }

    [Fact]
    public void AMissingProcessFolderGivesAnEmptySeriesRatherThanThrowing()
    {
        using var ws = new TempWorkspace();
        var s = Session(ws, process: false, master: false);

        Assert.Empty(s.LightSeries());
        Assert.Empty(s.RatioCandidates());
    }

    // ------------------------------------------------------------------ candidatii ratio

    /// <summary>Cerinta 6: lista se filtreaza pe "pp_flat*" cu extensia .fit.</summary>
    [Fact]
    public void RatioCandidatesAreTheFlatMastersInProcess()
    {
        using var ws = new TempWorkspace();
        var s = Session(ws);

        File.WriteAllText(Path.Combine(s.ProcessDir, "pp_flat_A.fit"), "x");
        File.WriteAllText(Path.Combine(s.ProcessDir, "pp_flat_B.fit"), "x");
        File.WriteAllText(Path.Combine(s.ProcessDir, "pp_light_00001.fit"), "x");
        File.WriteAllText(Path.Combine(s.ProcessDir, "flat_necalibrat.fit"), "x");

        Assert.Equal(new[] { "pp_flat_A.fit", "pp_flat_B.fit" },
                     s.RatioCandidates().Select(Path.GetFileName).ToArray());
    }

    /// <summary>
    /// Masterul general NU e candidat pentru testul de rotatie: e stiva tuturor
    /// flat-urilor, iar testul compara doua stive facute separat. Comparat cu unul dintre
    /// seturi ar da o cifra fara sens - jumatate din el chiar e setul acela.
    ///
    /// Conteaza si practic: sortarea fiind ordinala, ar fi cazut ultimul in lista, adica
    /// exact pe preselectia setului B.
    /// </summary>
    [Fact]
    public void TheOverallFlatMasterIsNotARatioCandidate()
    {
        using var ws = new TempWorkspace();
        var s = Session(ws);

        File.WriteAllText(Path.Combine(s.ProcessDir, "pp_flat_A.fit"), "x");

        Assert.DoesNotContain(SirilSession.FlatMasterName,
                              s.RatioCandidates().Select(Path.GetFileName));
        Assert.True(File.Exists(s.FlatMasterPath), "masterul exista, doar nu e propus");
    }

    /// <summary>Potrivirea tiparului e insensibila la litere mari; excluderea la fel.</summary>
    [Fact]
    public void TheExclusionIgnoresCase()
    {
        using var ws = new TempWorkspace();
        var s = Session(ws, master: false);

        File.WriteAllText(Path.Combine(s.ProcessDir, "PP_FLAT_STACKED.FIT"), "x");
        File.WriteAllText(Path.Combine(s.ProcessDir, "pp_flat_A.fit"), "x");

        Assert.Equal(new[] { "pp_flat_A.fit" },
                     s.RatioCandidates().Select(Path.GetFileName).ToArray());
    }

    /// <summary>
    /// Scos din lista, dar NU respins: lista propune, iar "Alt fisier..." ramane o
    /// portita pentru fluxurile in care masterul chiar e unul dintre cele doua seturi.
    /// </summary>
    [Fact]
    public void TheOverallMasterIsStillAValidManualChoice()
    {
        using var ws = new TempWorkspace();
        var s = Session(ws);

        Assert.True(SirilSession.IsFlatMasterName(s.FlatMasterPath));
    }

    [Theory]
    [InlineData("pp_flat_stacked.fit", true)]
    [InlineData("pp_flat_A.fit", true)]
    [InlineData(@"C:\altundeva\pp_flat_B.fit", true)]
    [InlineData("flat_stacked.fit", false)]      // necalibrat: fara prefixul pp_
    [InlineData("pp_light_00001.fit", false)]
    [InlineData("pp_flat_stacked.fits", false)]  // extensie diferita
    [InlineData("pp_flat_stacked.tif", false)]
    public void AFileIsAFlatMasterOnlyIfItsNameSaysSo(string path, bool expected)
        => Assert.Equal(expected, SirilSession.IsFlatMasterName(path));

    [Fact]
    public void AnEmptyPathIsNotAFlatMaster()
    {
        Assert.False(SirilSession.IsFlatMasterName(null));
        Assert.False(SirilSession.IsFlatMasterName(""));
    }

    // ------------------------------------------------------------------ flat-uri brute

    /// <summary>
    /// Doar raw de aparat. Un FITS din flats/ poate fi orice - inclusiv unul deja
    /// debayerizat, pe care modul l-ar respinge oricum - deci nu se propune.
    /// </summary>
    [Fact]
    public void OnlyCameraRawFilesAreOfferedForTheExposureCheck()
    {
        using var ws = new TempWorkspace();
        var s = Session(ws);

        foreach (string n in new[] { "a.NEF", "b.cr2", "c.dng", "d.fit", "e.jpg", "f.txt" })
            File.WriteAllText(Path.Combine(s.FlatsDir, n), "x");

        Assert.Equal(new[] { "a.NEF", "b.cr2", "c.dng" },
                     s.RawFlats().Select(Path.GetFileName).ToArray());
    }

    /// <summary>
    /// Extensiile vin din RawReader, nu dintr-o lista scrisa in aplicatia WPF. Testul
    /// pinuieste exact asta: daca cititorul invata un aparat nou, lista il arata.
    /// </summary>
    [Fact]
    public void TheOfferedExtensionsAreTheOnesTheReaderAccepts()
    {
        using var ws = new TempWorkspace();
        var s = Session(ws);

        foreach (string ext in RawReader.RawExtensions)
            File.WriteAllText(Path.Combine(s.FlatsDir, "flat" + ext), "x");

        Assert.Equal(RawReader.RawExtensions.Count, s.RawFlats().Count);
    }

    [Fact]
    public void AMissingFlatsFolderGivesAnEmptyListRatherThanThrowing()
    {
        using var ws = new TempWorkspace();
        Assert.Empty(Session(ws, flats: false).RawFlats());
    }

    [Fact]
    public void RawFlatsAreSortedOrdinallyByName()
    {
        using var ws = new TempWorkspace();
        var s = Session(ws);

        foreach (string n in new[] { "DSC_0003.NEF", "DSC_0001.NEF", "DSC_0002.NEF" })
            File.WriteAllText(Path.Combine(s.FlatsDir, n), "x");

        Assert.Equal(new[] { "DSC_0001.NEF", "DSC_0002.NEF", "DSC_0003.NEF" },
                     s.RawFlats().Select(Path.GetFileName).ToArray());
    }
}
