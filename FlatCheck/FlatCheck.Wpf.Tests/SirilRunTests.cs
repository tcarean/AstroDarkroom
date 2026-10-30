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

// SirilRunTests.cs - the run window, with a fake siril-cli that delivers recorded lines,
// and - only when Siril is installed - with the real one on a sample script.

using FlatCheck.Tests;

namespace FlatCheck.Wpf.Tests;

public class SirilRunViewModelTests
{
    const string Script = "requires 1.2.0\ncd biases\nconvert bias -out=../process\ncd ..\nclose\n";

    static SirilRunViewModel Vm() => new(@"C:\sesiune", @"C:\scripturi\proba.ssf", Script);

    /// <summary>A runner that delivers the given lines and exits with the given code.</summary>
    static SirilRunner Fake(int exitCode, params string[] lines) => (onLine, ct) =>
    {
        foreach (string l in lines) onLine(l);
        return Task.FromResult(exitCode);
    };

    /// <summary>
    /// The log gets only the "log:" lines, without the prefix; the progress becomes the bar, not
    /// text; the current command is the one from the script, whole, counted out of the total.
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

    /// <summary>
    /// When the script could not be read, the window shows the verb Siril printed; the log
    /// says the same, not "command 3 of 0" (the review of 2026-10-27, A12).
    /// </summary>
    [Fact]
    public async Task WithoutTheScriptsTextTheLogNamesTheVerbSirilPrinted()
    {
        var vm = new SirilRunViewModel(@"C:\sesiune", @"C:\scripturi\proba.ssf", "");
        using var capture = LogCapture.Start();

        await vm.RunAsync(Fake(0,
            "log: Running command: requires",
            "log: Running command: cd",
            "log: Running command: convert",
            "log: Script execution finished successfully."));

        Assert.Equal(0, vm.CommandCount);
        Assert.Equal(3, vm.CommandIndex);
        var ended = Assert.Single(capture.Events, e => e.Message.StartsWith("run ended", StringComparison.Ordinal));
        Assert.EndsWith(" at command 3: convert", ended.Message);
        Assert.DoesNotContain("of 0", ended.Message);
    }

    /// <summary>Code 1 is a failure, and the verdict says at which command it stopped.</summary>
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

    /// <summary>Siril prints only the verb; beyond the script's list the verb is shown, nothing
    /// fails.</summary>
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
/// The launch itself, with a fake siril-cli: a .cmd that says which arguments it got and
/// copies the settings file it got with -i, so the test sees what Siril would have read. It
/// works on any Windows, without Siril, and writes nothing in the user's config.
/// </summary>
public class SirilScriptRunTests
{
    static string PrivateRoot => Path.Combine(Path.GetTempPath(), "FlatCheck");

    /// <summary>
    /// Fake siril-cli. It gets -d folder -s script -i settings: the fifth argument says whether a
    /// settings file came, the sixth which one; its copy lands in the -d folder, seen.ini. The
    /// lines in <paramref name="then"/> run after that (the file is written without a BOM, with
    /// CRLF - otherwise cmd misreads the first line).
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

    /// <summary>Runs the runner and gathers the lines: they come from two threads, stdout and
    /// stderr.</summary>
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
    /// The run from the window (the problem card, each part of the Split): siril-cli gets with
    /// -i a copy of Siril's config in a folder of its own, and the folder disappears after the
    /// run. Without -i, siril-cli rewrites the user's config at the end of every script.
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

        // the config's bytes, read before or after: a save by Siril meanwhile is not a fault of the
        // copy
        byte[] seen = File.ReadAllBytes(ws.PathTo("seen.ini"));
        Assert.True(seen.SequenceEqual(before) || seen.SequenceEqual(Bytes(user)), "copia nu are octetii config-ului lui Siril");
        Assert.False(Directory.Exists(Path.GetDirectoryName(config)), "folderul copiei a ramas: " + config);
    }

    /// <summary>
    /// The copy has exactly the bytes of the given config; without a config it is empty - Siril's
    /// defaults, as when Siril has no config at all. The fake Siril then does what the real one
    /// does at the end: rewrites its copy (a new wd) and leaves a .XXXXXX beside it. None of
    /// that goes back into the config, and the folder leaves with whatever Siril left.
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
    /// Stops mid-run: siril-cli is killed, the cancellation is reported as a cancellation, and
    /// the copy leaves too - only after the process has exited; while it runs it is in place.
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
    /// A config that exists but cannot be read (here held open exclusively) stops the run
    /// before it starts, with the file named on screen: Siril does not quietly work with the
    /// defaults, and no copy remains.
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
/// With the real siril-cli, when it is installed. Without Siril these tests pass without
/// checking anything - xunit 2 has no dynamic skip - so a failure here means a machine
/// with Siril on which the run really does not work, not one without Siril.
/// Every run gets an empty copy of the settings (the defaults) in the test's workspace,
/// not one of the user's config, so the tests do not depend on the user's settings. Before
/// the copy, siril-cli rewrote the user's config at every run, and that is how the user
/// once lost all of Siril's settings (ADR 0055).
/// </summary>
public class SirilCliIntegrationTests
{
    // FindCli, not Locate: Locate also reads the user's config, to no purpose here
    static string? Cli => SirilInstall.FindCli();

    static SirilRunner Run(string cli, string folder, string script, TempWorkspace ws)
        => SirilScriptRun.For(cli, folder, script, null, ws.Dir);

    /// <summary>
    /// siril-cli worked on the copy: it says so in the log, with its path, and the copy is no
    /// longer there. On the user's config it would have written the session folder as wd - a
    /// guid, so it does not appear otherwise - and without a config it would have created it.
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

    /// <summary>Siril saves its settings also when the script fails, so here too it must be on the
    /// copy.</summary>
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

    /// <summary>The install on this machine, if there is one, looks like an install:
    /// bin/siril-cli.exe and .ssf scripts.</summary>
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
