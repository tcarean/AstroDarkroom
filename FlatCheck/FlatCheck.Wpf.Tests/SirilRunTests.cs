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
/// Cu siril-cli adevarat, cand e instalat. Fara Siril testele astea trec fara sa
/// verifice nimic - xunit 2 nu are sarire dinamica - deci un esec aici inseamna o
/// masina cu Siril pe care rularea chiar nu merge, nu una fara Siril.
/// </summary>
public class SirilCliIntegrationTests
{
    static string? Cli => SirilInstall.Locate().CliPath;

    [Fact]
    public async Task AProbeScriptRunsToTheEnd_WithTheLogStreamedIntoTheViewModel()
    {
        string? cli = Cli;
        if (cli == null) return;

        using var f = new SessionFixture().WithFlatMaster();
        using var ws = new TempWorkspace();
        string script = Path.Combine(ws.Dir, "proba.ssf");
        File.WriteAllText(script, "requires 1.2.0\nload process/pp_flat_stacked.fit\nclose\n");

        var vm = new SirilRunViewModel(f.Root, script);
        await vm.RunAsync(SirilScriptRun.For(cli, f.Root, script));

        Assert.True(vm.IsSuccess, vm.Outcome + "\n" + vm.Log);
        Assert.Contains("Script execution finished successfully", vm.Log);
        Assert.Contains("pp_flat_stacked.fit", vm.Log);
        Assert.Equal("comanda 3 din 3:  close", vm.Step);
    }

    [Fact]
    public async Task AScriptThatStopsGivesExitOneAndNamesTheCommand()
    {
        string? cli = Cli;
        if (cli == null) return;

        using var f = new SessionFixture();
        using var ws = new TempWorkspace();
        string script = Path.Combine(ws.Dir, "cade.ssf");
        File.WriteAllText(script, "requires 1.2.0\ncd nu-exista\nclose\n");

        var vm = new SirilRunViewModel(f.Root, script);
        await vm.RunAsync(SirilScriptRun.For(cli, f.Root, script));

        Assert.True(vm.IsFailure);
        Assert.Contains("codul 1", vm.Outcome);
        Assert.Contains("cd nu-exista", vm.Outcome);
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
