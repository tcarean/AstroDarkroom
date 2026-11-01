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

using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using DarksCheck.Core;
using NLog;

namespace DarksCheck.Tests;

/// <summary>
/// When LibRaw cannot be loaded (R-110, ADR 0026): every raw's error names Microsoft's Visual C++
/// runtime, missing or too old, and the link to its latest version, in either language, and the
/// log says once per process which of the runtime's four DLLs Windows loads. Windows gives the same
/// exception, naming <c>libraw.dll</c>, whether the runtime is missing or LibRaw's own DLL is: a
/// copy of the console without <c>raw_r.dll</c> shows the whole path, out of process, since this
/// process has loaded LibRaw already.
/// </summary>
public partial class LibRawLoadTests
{
    private const string English = "cannot load LibRaw: Microsoft's Visual C++ runtime is likely missing or too old, so camera raw files "
        + "cannot be read until its latest version is installed: https://aka.ms/vc14/vc_redist.x64.exe";

    private const string Romanian = "nu pot incarca LibRaw: runtime-ul Microsoft Visual C++ probabil lipseste sau este prea vechi, deci nu pot "
        + "citi fisiere raw de aparat pana nu instalezi ultima lui versiune: https://aka.ms/vc14/vc_redist.x64.exe";

    /// <summary>The loader's exception, as .NET words it when Windows refuses <c>raw_r.dll</c> or a DLL it imports.</summary>
    private static readonly DllNotFoundException Loader =
        new("Unable to load DLL 'libraw.dll' or one of its dependencies: The specified module could not be found. (0x8007007E)");

    /// <summary>RawReader's error for a loader failure, from the method its catch list calls.</summary>
    private static InvalidOperationException Missing(Exception inner)
    {
        MethodInfo m = typeof(RawReader).GetMethod("Missing", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("RawReader has no static Missing(Exception) any more");
        return Assert.IsType<InvalidOperationException>(m.Invoke(null, [inner]));
    }

    [Fact]
    public void R110_TheMessageNamesTheRuntimeAndItsLink_InEnglish()
    {
        using IDisposable _ = Ui.Use(UiLanguage.English);
        InvalidOperationException e = Missing(Loader);

        Assert.Equal(English, e.Message);
        Assert.Same(Loader, e.InnerException);
    }

    /// <summary>The Romanian wording, without diacritics, as every Romanian text (ADR 0021).</summary>
    [Fact]
    public void R110_TheMessageNamesTheRuntimeAndItsLink_InRomanian()
    {
        using IDisposable _ = Ui.Use(UiLanguage.Romanian);
        InvalidOperationException e = Missing(Loader);

        Assert.Equal(Romanian, e.Message);
        Assert.Same(Loader, e.InnerException);
        Assert.All(e.Message, c => Assert.True(c < 128, $"'{c}' is not ASCII"));
    }

    /// <summary>
    /// Only RawReader's error for a loader failure counts: its four causes, as its catch list takes
    /// them, and none of the errors a raw or another file gives when LibRaw itself is there.
    /// </summary>
    [Fact]
    public void R110_OnlyRawReadersLoadFailureCountsAsOne()
    {
        Assert.True(VcRuntime.IsLoadFailure(Missing(Loader)));
        Assert.True(VcRuntime.IsLoadFailure(Missing(new BadImageFormatException("raw_r.dll is not a valid image"))));
        Assert.True(VcRuntime.IsLoadFailure(Missing(new TypeInitializationException("Sdcb.LibRaw.Natives.LibRawNative", null))));
        Assert.True(VcRuntime.IsLoadFailure(Missing(new FileNotFoundException("Could not load file or assembly", "Sdcb.LibRaw"))));

        Assert.False(VcRuntime.IsLoadFailure(new InvalidOperationException("x.NEF: libraw gives an impossible scale (white 0 <= black 0).")));
        Assert.False(VcRuntime.IsLoadFailure(new InvalidOperationException("the partner: no darks", new IOException("gone"))));
        Assert.False(VcRuntime.IsLoadFailure(Loader));
        Assert.False(VcRuntime.IsLoadFailure(new FileNotFoundException("cannot find the file x.NEF", "x.NEF")));
        Assert.False(VcRuntime.IsLoadFailure(new InvalidDataException("FITS: no SIMPLE card")));
    }

    /// <summary>The line names those of the four that do not load, and where each one that loads came from.</summary>
    [Fact]
    public void R110_TheLineNamesTheMissingDllsAndWhereTheOthersCameFrom()
    {
        RuntimeDll[] some =
        [
            new("vcruntime140.dll", true, @"C:\Windows\SYSTEM32\vcruntime140.dll", "14.0.24215.1"),
            new("vcruntime140_1.dll", false, null, null),
            new("msvcp140.dll", true, @"C:\Windows\SYSTEM32\msvcp140.dll", "14.0.24215.1"),
            new("vcomp140.dll", true, @"C:\Windows\SYSTEM32\vcomp140.dll", null),
        ];
        Assert.Equal(
            @"Visual C++ runtime: missing vcruntime140_1.dll; vcruntime140.dll 14.0.24215.1 at C:\Windows\SYSTEM32\vcruntime140.dll, "
            + @"msvcp140.dll 14.0.24215.1 at C:\Windows\SYSTEM32\msvcp140.dll, vcomp140.dll at C:\Windows\SYSTEM32\vcomp140.dll load; "
            + "LibRaw cannot be loaded until the runtime's latest version is installed",
            VcRuntime.Line(some));

        RuntimeDll[] none = VcRuntime.Files.Select(f => new RuntimeDll(f, false, null, null)).ToArray();
        Assert.Equal(
            "Visual C++ runtime: missing vcruntime140.dll, vcruntime140_1.dll, msvcp140.dll, vcomp140.dll; "
            + "LibRaw cannot be loaded until the runtime's latest version is installed",
            VcRuntime.Line(none));

        RuntimeDll[] all = VcRuntime.Files.Select(f => new RuntimeDll(f, true, @"C:\Windows\SYSTEM32\" + f, "14.51.36247.0")).ToArray();
        Assert.Equal(
            @"Visual C++ runtime: vcruntime140.dll 14.51.36247.0 at C:\Windows\SYSTEM32\vcruntime140.dll, "
            + @"vcruntime140_1.dll 14.51.36247.0 at C:\Windows\SYSTEM32\vcruntime140_1.dll, "
            + @"msvcp140.dll 14.51.36247.0 at C:\Windows\SYSTEM32\msvcp140.dll, "
            + @"vcomp140.dll 14.51.36247.0 at C:\Windows\SYSTEM32\vcomp140.dll load; LibRaw failed to load for another reason",
            VcRuntime.Line(all));
    }

    /// <summary>
    /// One line per process, at the first load failure: Warn when a DLL does not load, Info when the
    /// four do; another error writes nothing and asks Windows nothing, and a second failure neither.
    /// </summary>
    [Fact]
    public void R110_TheLineIsWrittenOnceAtTheFirstLoadFailure_AndNeverForAnotherError()
    {
        int logged = 0;
        int asked = 0;
        IReadOnlyList<RuntimeDll> OneMissing()
        {
            asked++;
            return VcRuntime.Files.Select(f => f == "vcomp140.dll" ? new RuntimeDll(f, false, null, null) : new RuntimeDll(f, true, @"C:\Windows\SYSTEM32\" + f, "14.0.24215.1")).ToArray();
        }

        using LogCapture capture = LogCapture.Start();
        Assert.False(VcRuntime.LogOnce(new InvalidDataException("FITS: no SIMPLE card"), OneMissing, ref logged));
        Assert.True(VcRuntime.LogOnce(Missing(Loader), OneMissing, ref logged));
        Assert.False(VcRuntime.LogOnce(Missing(Loader), OneMissing, ref logged));

        Assert.Equal(1, asked);
        LogCapture.Captured line = Assert.Single(capture.Events);
        Assert.Equal(LogLevel.Warn, line.Level);
        Assert.Equal("VcRuntime", line.Logger);
        Assert.StartsWith("Visual C++ runtime: missing vcomp140.dll; vcruntime140.dll 14.0.24215.1 at ", line.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void R110_WhenTheFourLoadTheLineIsInfo()
    {
        int logged = 0;
        IReadOnlyList<RuntimeDll> AllFour() => VcRuntime.Files.Select(f => new RuntimeDll(f, true, @"C:\Windows\SYSTEM32\" + f, "14.51.36247.0")).ToArray();

        using LogCapture capture = LogCapture.Start();
        Assert.True(VcRuntime.LogOnce(Missing(Loader), AllFour, ref logged));

        LogCapture.Captured line = Assert.Single(capture.Events);
        Assert.Equal(LogLevel.Info, line.Level);
        Assert.EndsWith("load; LibRaw failed to load for another reason", line.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The seams that call it sit in a frame's failure path, which must not fail: a probe that
    /// throws becomes the process's one line, at Warn, with its exception.
    /// </summary>
    [Fact]
    public void R110_AProbeThatThrowsIsALineNotAFailure()
    {
        int logged = 0;
        IReadOnlyList<RuntimeDll> Broken() => throw new System.ComponentModel.Win32Exception(5, "Access is denied");

        using LogCapture capture = LogCapture.Start();
        Assert.True(VcRuntime.LogOnce(Missing(Loader), Broken, ref logged));

        LogCapture.Captured line = Assert.Single(capture.Events);
        Assert.Equal(LogLevel.Warn, line.Level);
        Assert.Equal("Visual C++ runtime: Windows could not be asked which of its DLLs load", line.Message);
        Assert.Contains("Access is denied", line.Line, StringComparison.Ordinal);
    }

    /// <summary>
    /// Windows' own answer, on whatever this machine has: the four in their order, each that loads
    /// with a full path ending in its own name.
    /// </summary>
    [Fact]
    public void R110_TheProbeAnswersForTheFourInTheirOrder()
    {
        IReadOnlyList<RuntimeDll> dlls = VcRuntime.Probe();

        Assert.Equal(["vcruntime140.dll", "vcruntime140_1.dll", "msvcp140.dll", "vcomp140.dll"], dlls.Select(d => d.File));
        Assert.All(dlls.Where(d => !d.Loads), d => Assert.Null(d.Path));
        foreach (RuntimeDll d in dlls.Where(d => d.Path is not null))
        {
            Assert.True(d.Loads);
            Assert.True(Path.IsPathRooted(d.Path), d.Path);
            Assert.Equal(d.File, Path.GetFileName(d.Path), ignoreCase: true);
        }
    }

    /// <summary>
    /// The whole path, out of process, on Windows: a copy of the console without LibRaw's own DLL,
    /// run on a session of three <c>.NEF</c> darks. Every row of the inventory carries the message, the full
    /// run ends with it in its error, both exit 1, and each process writes the runtime's line once,
    /// at the level this process's own probe gives (Info on a computer that has the runtime).
    /// </summary>
    [Fact]
    public void R110_ACopyOfTheConsoleWithoutLibRawsDll_GivesEveryRawTheMessage_AndLogsTheRuntimeOnce()
    {
        // The runtime is Microsoft's and raw_r.dll is LibRaw's Windows build: elsewhere (the
        // Linux pipeline) there is nothing of either to leave out, so nothing to show.
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using TempWorkspace w = new();
        string app = CopyConsoleWithout(Path.Combine(w.Dir, "app"), "raw_r.dll");
        string session = Path.Combine(w.Dir, "session");
        Directory.CreateDirectory(Path.Combine(session, "darks"));
        for (int i = 1; i <= 3; i++)
        {
            File.WriteAllText(Path.Combine(session, "darks", $"DSC_000{i}.NEF"), "not a raw");
        }

        string level = VcRuntime.Probe().Any(d => !d.Loads) ? "WARN" : "INFO";
        string log = Path.Combine(app, "log", "darkscheck.log");

        (int code, string stdout, _) = RunCopy(app, "--inventory", session);
        Assert.Equal(1, code);
        string[] rows = stdout.Split('\n').Where(l => l.Contains("DSC_000", StringComparison.Ordinal)).ToArray();
        Assert.Equal(3, rows.Length);
        Assert.All(rows, r => Assert.EndsWith("error: " + English, r.TrimEnd('\r'), StringComparison.Ordinal));
        Assert.Equal([level], RuntimeLines(log));

        File.Delete(log);
        (code, _, string stderr) = RunCopy(app, session);
        Assert.Equal(1, code);
        Assert.Contains("ERROR: no dark could be decoded: DSC_0001.NEF: " + English + "; DSC_0002.NEF: " + English, stderr, StringComparison.Ordinal);
        Assert.Equal([level], RuntimeLines(log));
    }

    /// <summary>
    /// Copies the console as the test's own output folder holds it, every file but one, so the copy
    /// starts as the console does and lacks only that DLL.
    /// </summary>
    private static string CopyConsoleWithout(string to, string left)
    {
        string from = AppContext.BaseDirectory;
        Assert.True(File.Exists(Path.Combine(from, "darkscheck.exe")), "the console is not in the test's output folder");
        Assert.True(File.Exists(Path.Combine(from, "runtimes", "win-x64", "native", left)), left + " is not where the copy leaves it out");
        foreach (string f in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
        {
            if (string.Equals(Path.GetFileName(f), left, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string target = Path.Combine(to, Path.GetRelativePath(from, f));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(f, target);
        }

        return to;
    }

    /// <summary>Runs the copy's <c>darkscheck.exe</c> in English, its log at Debug, and waits for it.</summary>
    private static (int Code, string Stdout, string Stderr) RunCopy(string app, params string[] args)
    {
        ProcessStartInfo start = new(Path.Combine(app, "darkscheck.exe"))
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = app,
        };
        foreach (string a in args.Prepend("en").Prepend("--lang"))
        {
            start.ArgumentList.Add(a);
        }

        start.Environment["DARKSCHECK_LOG"] = "debug";
        using Process p = Process.Start(start) ?? throw new InvalidOperationException("the copy did not start");
        Task<string> stdout = p.StandardOutput.ReadToEndAsync();
        Task<string> stderr = p.StandardError.ReadToEndAsync();
        Assert.True(p.WaitForExit(120_000), "the copy did not end within two minutes");
        return (p.ExitCode, stdout.GetAwaiter().GetResult(), stderr.GetAwaiter().GetResult());
    }

    /// <summary>The levels of the runtime's lines in a log file, in their order.</summary>
    private static string[] RuntimeLines(string log)
        => File.ReadAllLines(log).Select(l => RuntimeLine().Match(l)).Where(m => m.Success).Select(m => m.Groups[1].Value).ToArray();

    [GeneratedRegex(@"^\S+ \S+ (\w+)\s+\[\d+:\d+\] VcRuntime Visual C\+\+ runtime: ")]
    private static partial Regex RuntimeLine();
}
