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

// SirilRunTests.cs - fereastra de rulare, cu un siril-cli fals care livreaza linii
// inregistrate, si - doar cand Siril e instalat - cu cel adevarat pe un script de proba.

using FlatCheck.Tests;

namespace FlatCheck.Wpf.Tests;

public class SirilRunViewModelTests
{
    const string Script = "requires 1.2.0\ncd biases\nconvert bias -out=../process\ncd ..\nclose\n";

    static SirilRunViewModel Vm() => new(@"C:\sesiune", @"C:\scripturi\proba.ssf", Script);

    /// <summary>Un runner care livreaza liniile date si iese cu codul dat.</summary>
    static SirilRunner Fake(int exitCode, params string[] lines) => (onLine, ct) =>
    {
        foreach (string l in lines) onLine(l);
        return Task.FromResult(exitCode);
    };

    /// <summary>
    /// Log-ul primeste doar liniile "log:", fara prefix; progresul devine bara, nu text;
    /// comanda curenta e cea din script, intreaga, numarata din total.
    /// </summary>
    [Fact]
    public async Task LogLinesAreShown_ProgressBecomesABar_AndTheStepIsReadFromTheScript()
    {
        var vm = Vm();

        await vm.RunAsync(Fake(0,
            "log: Welcome to siril 1.4.4",
            "log: Running command: requires",
            "1789481943: running command requires",
            "log: Running command: cd",
            "log: Running command: convert",
            "progress: Converting files, 0.00%",
            "\x1b[A\x1b[2KTprogress: 50.00%",
            "Block 0: channel 0",
            "log: Conversion succeeded",
            "log: Script execution finished successfully."));

        Assert.Equal("Welcome to siril 1.4.4\nRunning command: requires\nRunning command: cd\nRunning command: convert\n"
                   + "Conversion succeeded\nScript execution finished successfully.\n", vm.Log);
        Assert.Equal("comanda 3 din 5:  convert bias -out=../process", vm.Step);
        Assert.Equal(5, vm.CommandCount);
        Assert.Equal("Converting files", vm.ProgressLabel);
        Assert.Equal(0.5, vm.Progress, 9);

        Assert.True(vm.IsFinished);
        Assert.False(vm.IsRunning);
        Assert.True(vm.IsSuccess);
        Assert.Equal(SessionMessages.RunSucceeded(), vm.Outcome);
    }

    /// <summary>Codul 1 e un esec, si verdictul spune la ce comanda s-a oprit.</summary>
    [Fact]
    public async Task ANonZeroExitCodeIsAFailureNamingTheStep()
    {
        var vm = Vm();

        await vm.RunAsync(Fake(1, "log: Running command: requires", "log: Running command: cd"));

        Assert.True(vm.IsFailure);
        Assert.Contains("codul 1", vm.Outcome);
        Assert.Contains("cd biases", vm.Outcome);
        Assert.False(vm.ProgressKnown);
    }

    /// <summary>Siril tipareste doar verbul; dincolo de lista scriptului se arata verbul, nu se cade.</summary>
    [Fact]
    public async Task MoreCommandsThanTheScriptHasFallBackToTheVerb()
    {
        var vm = new SirilRunViewModel(@"C:\sesiune", @"C:\scripturi\gol.ssf", "");

        await vm.RunAsync(Fake(0, "log: Running command: load"));

        Assert.Equal("comanda 1:  load", vm.Step);
        Assert.Equal(0, vm.CommandCount);
    }

    [Fact]
    public async Task CancellingKillsTheRunAndReportsItAsCancelled_NotAsAFailureCode()
    {
        var vm = Vm();
        var started = new TaskCompletionSource();

        SirilRunner runner = async (onLine, ct) =>
        {
            onLine("log: Running command: convert");
            started.SetResult();
            await Task.Delay(Timeout.Infinite, ct);
            return 0;
        };

        Task run = vm.RunAsync(runner);
        await started.Task;
        Assert.True(vm.IsRunning);

        vm.Cancel();
        await run;

        Assert.False(vm.IsRunning);
        Assert.True(vm.IsFailure);
        Assert.Equal(SessionMessages.RunCancelled(), vm.Outcome);
    }

    [Fact]
    public async Task AFailureToStartIsAVerdictOnScreen_NotAnException()
    {
        var vm = Vm();

        await vm.RunAsync((_, _) => throw new System.ComponentModel.Win32Exception("nu exista fisierul"));

        Assert.True(vm.IsFailure);
        Assert.Contains("nu exista fisierul", vm.Outcome);
    }

    [Fact]
    public async Task ARunCannotBeStartedTwice()
    {
        var vm = Vm();
        await vm.RunAsync(Fake(0));

        await Assert.ThrowsAsync<InvalidOperationException>(() => vm.RunAsync(Fake(0)));
    }

    [Fact]
    public void CancelBeforeStartIsHarmless()
    {
        var vm = Vm();
        vm.Cancel();
        Assert.False(vm.IsFinished);
    }
}

/// <summary>
/// Lansarea insasi, cu un siril-cli fals: un .cmd care spune ce argumente a primit si
/// copiaza fisierul de setari primit cu -i, ca testul sa vada ce ar fi citit Siril. Merge pe
/// orice Windows, fara Siril, si nu scrie nimic in config-ul omului.
/// </summary>
public class SirilScriptRunTests
{
    static string PrivateRoot => Path.Combine(Path.GetTempPath(), "FlatCheck");

    /// <summary>
    /// siril-cli fals. Primeste -d folder -s script -i setari: al cincilea argument spune daca
    /// a venit un fisier de setari, al saselea care; copia lui ajunge in folderul -d, seen.ini.
    /// Liniile din <paramref name="then"/> ruleaza dupa aceea (fisierul e scris fara BOM, cu
    /// CRLF - altfel cmd citeste gresit prima linie).
    /// </summary>
    static string FakeCli(TempWorkspace ws, string then = "")
    {
        string path = ws.PathTo("siril-cli.cmd");
        File.WriteAllText(path, "@echo off\r\n"
                              + "echo ARG5=%~5\r\n"
                              + "echo ARG6=%~6\r\n"
                              + "if \"%~5\"==\"-i\" copy /b /y \"%~6\" \"%~2\\seen.ini\" >nul\r\n"
                              + then
                              + "exit /b 0\r\n");
        return path;
    }

    static string[] Copies(TempWorkspace ws) => Directory.GetDirectories(ws.Dir, "siril-*");

    /// <summary>Ruleaza runner-ul si strange liniile: vin de pe doua fire, stdout si stderr.</summary>
    static async Task<(int Code, List<string> Lines)> Run(SirilRunner runner, CancellationToken ct = default)
    {
        var lines = new List<string>();
        int code = await runner(l => { lock (lines) lines.Add(l); }, ct);
        return (code, lines);
    }

    static string Value(List<string> lines, string key)
        => lines.Where(l => l.StartsWith(key + "=", StringComparison.Ordinal))
                .Select(l => l.Substring(key.Length + 1).Trim())
                .FirstOrDefault() ?? "";

    static byte[] Bytes(string? path) => path != null && File.Exists(path) ? File.ReadAllBytes(path) : Array.Empty<byte>();

    /// <summary>
    /// Rularea din fereastra (cardul de problema, fiecare parte a Split-ului): siril-cli primeste
    /// cu -i o copie a config-ului lui Siril intr-un folder al ei, iar folderul dispare dupa
    /// rulare. Fara -i, siril-cli rescrie config-ul omului la sfarsitul fiecarui script.
    /// </summary>
    [Fact]
    public async Task R094_EveryRunGetsAPrivateCopyOfSirilsSettings_PassedWithI_AndGoneAfterTheRun()
    {
        using var ws = new TempWorkspace();
        string cli = FakeCli(ws);
        string? user = SirilInstall.FindConfig();
        byte[] before = Bytes(user);

        var (code, lines) = await Run(SirilScriptRun.For(cli, ws.Dir, ws.PathTo("proba.ssf")));

        Assert.Equal(0, code);
        Assert.Equal("-i", Value(lines, "ARG5"));
        string config = Value(lines, "ARG6");
        Assert.StartsWith(Path.Combine(PrivateRoot, "siril-"), config, StringComparison.OrdinalIgnoreCase);
        Assert.False(user != null && string.Equals(Path.GetFullPath(user), Path.GetFullPath(config), StringComparison.OrdinalIgnoreCase),
                     "siril-cli a primit chiar config-ul omului");

        // octetii config-ului, cititi inainte sau dupa: o salvare a lui Siril intre timp nu e o greseala a copiei
        byte[] seen = File.ReadAllBytes(ws.PathTo("seen.ini"));
        Assert.True(seen.SequenceEqual(before) || seen.SequenceEqual(Bytes(user)), "copia nu are octetii config-ului lui Siril");
        Assert.False(Directory.Exists(Path.GetDirectoryName(config)), "folderul copiei a ramas: " + config);
    }

    /// <summary>
    /// Copia are exact octetii config-ului dat; fara config e goala - valorile implicite ale lui
    /// Siril, ca atunci cand Siril nu are niciun config. Siril-ul fals face apoi ce face cel
    /// adevarat la sfarsit: isi rescrie copia (un wd nou) si lasa un .XXXXXX langa ea. Nimic
    /// din asta nu se intoarce in config, si folderul pleaca cu tot cu ce a lasat Siril.
    /// </summary>
    [Fact]
    public async Task R094_TheCopyCarriesTheSettingsBytes_AndIsEmptyWhenThereAreNone()
    {
        using var ws = new TempWorkspace();
        string cli = FakeCli(ws, ">>\"%~6\" echo wd=%~2\r\n>\"%~6.AB12CD\" type \"%~6\"\r\n");
        string settings = ws.PathTo("config.1.4.ini");
        File.WriteAllText(settings, "[core]\nwd=C:\\\\nicaieri\nextension=.fit\n");
        byte[] before = File.ReadAllBytes(settings);

        var (code, _) = await Run(SirilScriptRun.For(cli, ws.Dir, ws.PathTo("proba.ssf"), settings, ws.Dir));

        Assert.Equal(0, code);
        Assert.Equal(before, File.ReadAllBytes(ws.PathTo("seen.ini")));
        Assert.Equal(before, File.ReadAllBytes(settings));
        Assert.Empty(Copies(ws));

        (code, _) = await Run(SirilScriptRun.For(cli, ws.Dir, ws.PathTo("proba.ssf"), null, ws.Dir));

        Assert.Equal(0, code);
        Assert.Empty(File.ReadAllBytes(ws.PathTo("seen.ini")));
        Assert.Empty(Copies(ws));
    }

    /// <summary>
    /// Opreste in mijlocul rularii: siril-cli e omorat, anularea se raporteaza ca anulare, si
    /// copia pleaca si ea - doar dupa ce procesul a iesit, cat timp ruleaza e la locul ei.
    /// </summary>
    [Fact]
    public async Task R094_ARunStoppedMidwayLeavesNoCopyBehind()
    {
        using var ws = new TempWorkspace();
        string cli = FakeCli(ws, "echo PORNIT\r\nping -n 30 127.0.0.1 >nul\r\n");
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cts = new CancellationTokenSource();

        Task<int> run = SirilScriptRun.For(cli, ws.Dir, ws.PathTo("proba.ssf"), null, ws.Dir)(
            l => { if (l.StartsWith("PORNIT", StringComparison.Ordinal)) started.TrySetResult(); }, cts.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Single(Copies(ws));

        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => run.WaitAsync(TimeSpan.FromSeconds(20)));
        Assert.Empty(Copies(ws));
    }

    /// <summary>
    /// Un config care exista dar nu se poate citi (aici tinut deschis exclusiv) opreste
    /// rularea inainte de pornire, cu fisierul numit pe ecran: Siril nu lucreaza pe ascuns cu
    /// valorile implicite, si nu ramane nicio copie.
    /// </summary>
    [Fact]
    public async Task R094_UnreadableSettingsStopTheRunBeforeSirilStarts_AndTheMessageNamesTheFile()
    {
        using var ws = new TempWorkspace();
        string cli = FakeCli(ws);
        string settings = ws.PathTo("config.1.4.ini");
        File.WriteAllText(settings, "[core]\n");
        var vm = new SirilRunViewModel(ws.Dir, ws.PathTo("proba.ssf"), "");

        using (new FileStream(settings, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            await vm.RunAsync(SirilScriptRun.For(cli, ws.Dir, ws.PathTo("proba.ssf"), settings, ws.Dir));

        Assert.True(vm.IsFailure);
        Assert.Contains(settings, vm.Outcome);
        Assert.False(File.Exists(ws.PathTo("seen.ini")), "siril-cli a pornit fara copie");
        Assert.Empty(Copies(ws));
    }
}

/// <summary>
/// Cu siril-cli adevarat, cand e instalat. Fara Siril testele astea trec fara sa
/// verifice nimic - xunit 2 nu are sarire dinamica - deci un esec aici inseamna o
/// masina cu Siril pe care rularea chiar nu merge, nu una fara Siril.
/// Fiecare rulare primeste o copie goala a setarilor (valorile implicite) in workspace-ul
/// testului, nu una a config-ului omului, ca testele sa nu depinda de setarile lui. Inainte
/// de copie, siril-cli rescria config-ul omului la fiecare rulare, si asa si-a pierdut omul
/// odata toate setarile lui Siril (ADR 0055).
/// </summary>
public class SirilCliIntegrationTests
{
    // FindCli, nu Locate: Locate citeste si config-ul omului, fara niciun rost aici
    static string? Cli => SirilInstall.FindCli();

    static SirilRunner Run(string cli, string folder, string script, TempWorkspace ws)
        => SirilScriptRun.For(cli, folder, script, null, ws.Dir);

    /// <summary>
    /// siril-cli a lucrat pe copie: o spune in log, cu calea ei, si copia nu mai e acolo. Pe
    /// config-ul omului ar fi scris folderul sesiunii ca wd - un guid, deci nu apare altfel -
    /// iar fara config l-ar fi creat.
    /// </summary>
    static void AssertItRanOnTheCopy(SirilRunViewModel vm, TempWorkspace ws, string sessionRoot, string? userConfig)
    {
        Assert.Contains("Reading configuration file " + Path.Combine(ws.Dir, "siril-"), vm.Log);
        Assert.Empty(Directory.GetDirectories(ws.Dir, "siril-*"));

        if (userConfig == null) Assert.Null(SirilInstall.FindConfig());
        else Assert.DoesNotContain(Path.GetFileName(sessionRoot), File.ReadAllText(userConfig), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AProbeScriptRunsToTheEnd_WithTheLogStreamedIntoTheViewModel()
    {
        string? cli = Cli;
        if (cli == null) return;

        using var f = new SessionFixture().WithFlatMaster();
        using var ws = new TempWorkspace();
        string script = Path.Combine(ws.Dir, "proba.ssf");
        File.WriteAllText(script, "requires 1.2.0\nload process/pp_flat_stacked.fit\nclose\n");
        string? user = SirilInstall.FindConfig();

        var vm = new SirilRunViewModel(f.Root, script);
        await vm.RunAsync(Run(cli, f.Root, script, ws));

        Assert.True(vm.IsSuccess, vm.Outcome + "\n" + vm.Log);
        Assert.Contains("Script execution finished successfully", vm.Log);
        Assert.Contains("pp_flat_stacked.fit", vm.Log);
        Assert.Equal("comanda 3 din 3:  close", vm.Step);
        AssertItRanOnTheCopy(vm, ws, f.Root, user);
    }

    /// <summary>Siril isi salveaza setarile si cand scriptul cade, deci si aici trebuie sa fie pe copie.</summary>
    [Fact]
    public async Task AScriptThatStopsGivesExitOneAndNamesTheCommand()
    {
        string? cli = Cli;
        if (cli == null) return;

        using var f = new SessionFixture();
        using var ws = new TempWorkspace();
        string script = Path.Combine(ws.Dir, "cade.ssf");
        File.WriteAllText(script, "requires 1.2.0\ncd nu-exista\nclose\n");
        string? user = SirilInstall.FindConfig();

        var vm = new SirilRunViewModel(f.Root, script);
        await vm.RunAsync(Run(cli, f.Root, script, ws));

        Assert.True(vm.IsFailure);
        Assert.Contains("codul 1", vm.Outcome);
        Assert.Contains("cd nu-exista", vm.Outcome);
        AssertItRanOnTheCopy(vm, ws, f.Root, user);
    }

    /// <summary>Instalarea de pe masina asta, daca exista, arata a instalare: bin/siril-cli.exe si scripturi .ssf.</summary>
    [Fact]
    public void WhatLocateFindsIsConsistent()
    {
        var install = SirilInstall.Locate();
        if (install.CliPath == null) return;

        Assert.True(File.Exists(install.CliPath));
        Assert.EndsWith("siril-cli.exe", install.CliPath, StringComparison.OrdinalIgnoreCase);
        Assert.All(install.Scripts, p => Assert.EndsWith(".ssf", p, StringComparison.OrdinalIgnoreCase));
        Assert.All(install.Scripts, p => Assert.True(File.Exists(p), p));
    }
}
