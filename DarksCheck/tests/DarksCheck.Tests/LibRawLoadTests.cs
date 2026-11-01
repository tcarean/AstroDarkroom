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
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.RegularExpressions;
using DarksCheck.Core;

namespace DarksCheck.Tests;

/// <summary>
/// When LibRaw cannot be loaded (R-110, ADR 0026): every raw's error names Microsoft's Visual C++
/// runtime, missing or too old, and its link, in either language, and the log says once per
/// process which of the runtime's four DLLs Windows cannot load. The code is LightsCheck's hunk
/// in the copied reader, and so are the first five tests, LightsCheck's R-074 tests under
/// DarksCheck's names; the last one runs a copy of the console without LibRaw's own DLL, out of
/// process, since this process has loaded LibRaw already.
/// </summary>
public partial class LibRawLoadTests
{
    private const string English = "cannot load libraw: Microsoft's Visual C++ runtime is likely missing or too old, so camera raw files "
        + "cannot be read until its latest version is installed (FITS frames are not affected): https://aka.ms/vc14/vc_redist.x64.exe";

    private const string Romanian = "nu pot incarca libraw: probabil runtime-ul Microsoft Visual C++ lipseste sau este prea vechi, deci nu "
        + "pot citi fisiere raw de aparat pana nu instalezi ultima lui versiune (cadrele FITS nu sunt afectate): https://aka.ms/vc14/vc_redist.x64.exe";

    /// <summary>What the message must not hold: words meant for a developer, and the loader's own.</summary>
    private static readonly string[] DeveloperWords = ["IncludeNativeLibrariesForSelfExtract", "darkscheck.exe", "libraw.dll", "raw_r.dll"];

    /// <summary>
    /// The user reads the likely cause and Microsoft's link, in each language, and nothing meant
    /// for a developer: no executable, no DLL, no publish option, not the loader's own words, which
    /// name libraw.dll. The loader's exception stays the inner one, for the log.
    /// </summary>
    [Fact]
    public void R110_TheLoadFailureNamesTheVisualCppRuntimeAndItsLink_InBothLanguages()
    {
        DllNotFoundException loader = new("Unable to load DLL 'libraw.dll' or one of its dependencies: The specified module could not be found. (0x8007007E)");
        RawReader.RuntimeCheck asked = new(_ => true);

        InvalidOperationException en, ro;
        using (Ui.Use(UiLanguage.English))
        {
            en = RawReader.Missing(loader, asked);
        }

        using (Ui.Use(UiLanguage.Romanian))
        {
            ro = RawReader.Missing(loader, asked);
        }

        Assert.Equal(English, en.Message);
        Assert.Equal(Romanian, ro.Message);
        Assert.Equal("https://aka.ms/vc14/vc_redist.x64.exe", RawReader.RuntimeUrl);
        foreach (InvalidOperationException e in new[] { en, ro })
        {
            Assert.EndsWith(" " + RawReader.RuntimeUrl, e.Message, StringComparison.Ordinal);
            Assert.Same(loader, e.InnerException);
            foreach (string developer in DeveloperWords)
            {
                Assert.DoesNotContain(developer, e.Message, StringComparison.OrdinalIgnoreCase);
            }
        }

        Assert.All(ro.Message, c => Assert.True(c < 128, $"'{c}' in the Romanian message, which is written without diacritics"));
    }

    /// <summary>
    /// The first failure asks Windows for the runtime's four DLLs, in their order, and logs at Warn
    /// those that do not load; a second failure asks nothing and logs nothing.
    /// </summary>
    [Fact]
    public void R110_TheRuntimeIsAskedForOnce_AndTheLogNamesTheDllsThatDoNotLoad()
    {
        List<string> asked = [];
        RawReader.RuntimeCheck check = new(file =>
        {
            asked.Add(file);
            return file is not ("vcruntime140_1.dll" or "vcomp140.dll");
        });
        using LogCapture capture = LogCapture.Start();

        RawReader.Missing(new DllNotFoundException("libraw.dll"), check);
        RawReader.Missing(new DllNotFoundException("libraw.dll"), check);

        string[] inOrder = ["vcruntime140.dll", "vcruntime140_1.dll", "msvcp140.dll", "vcomp140.dll"];
        Assert.Equal(inOrder, asked);
        LogCapture.Captured line = Assert.Single(capture.Events, e => e.Logger == nameof(RawReader));
        Assert.Equal(NLog.LogLevel.Warn, line.Level);
        Assert.Equal("libraw cannot be loaded; Visual C++ runtime: missing [vcruntime140_1.dll, vcomp140.dll]", line.Message);
    }

    /// <summary>
    /// The reader's own failures go through the process's one check: two failures leave it asked
    /// and write at most one line, whichever test reached it first, where a check made per failure
    /// would write a line, and search the PATH, at every failure of a session.
    /// </summary>
    [Fact]
    public void R110_TheReadersFailuresShareTheProcesssOneCheck()
    {
        using LogCapture capture = LogCapture.Start();

        RawReader.Missing(new DllNotFoundException("libraw.dll"));
        RawReader.Missing(new DllNotFoundException("libraw.dll"));

        Assert.True(RawReader.Runtime.HasAsked);
        Assert.True(capture.Events.Count(e => e.Logger == nameof(RawReader)) <= 1, "two failures wrote more than one runtime line");
    }

    /// <summary>
    /// When the four load, the line says that none is missing, which a bug report needs to know,
    /// since the user's message names the runtime as the likely cause; it claims no more, since a
    /// runtime too old for LibRaw's DLLs may still have the four (ADR 0026).
    /// </summary>
    [Fact]
    public void R110_WhenTheFourLoad_TheLogSaysNoneIsMissing()
    {
        RawReader.RuntimeCheck check = new(_ => true);
        using LogCapture capture = LogCapture.Start();

        RawReader.Missing(new BadImageFormatException("raw_r.dll"), check);

        LogCapture.Captured line = Assert.Single(capture.Events, e => e.Logger == nameof(RawReader));
        Assert.Equal(NLog.LogLevel.Warn, line.Level);
        Assert.Equal(
            "libraw cannot be loaded; Visual C++ runtime: [vcruntime140.dll, vcruntime140_1.dll, msvcp140.dll, vcomp140.dll] load, so none is missing",
            line.Message);
    }

    /// <summary>
    /// The runtime DLLs the reader asks for are the ones LibRaw's DLLs import, read from the import
    /// tables of the DLLs the build brings for Windows: a Sdcb.LibRaw.runtime.win64 whose DLLs
    /// import another runtime DLL turns this red until <see cref="RawReader.RuntimeFiles"/> names
    /// it. The tables are read as bytes, so this runs on any system.
    /// </summary>
    [Fact]
    public void R110_TheRuntimeFilesAreWhatLibrawsDllsImport()
    {
        string native = Path.Combine(AppContext.BaseDirectory, "runtimes", "win-x64", "native");
        Assert.True(Directory.Exists(native), $"{native} does not exist: the test reads the DLLs the build brings for Windows");
        string[] dlls = Directory.GetFiles(native, "*.dll");
        Assert.NotEmpty(dlls);

        HashSet<string> runtime = new(StringComparer.OrdinalIgnoreCase);
        foreach (string dll in dlls)
        {
            runtime.UnionWith(Imports(dll).Where(name => VcRuntimeDll().IsMatch(name)));
        }

        Assert.Equal(
            RawReader.RuntimeFiles.Select(f => f.ToLowerInvariant()).Order(StringComparer.Ordinal),
            runtime.Select(f => f.ToLowerInvariant()).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// The whole path, out of process, on Windows: a copy of the console without LibRaw's own DLL,
    /// run on a session of three <c>.NEF</c> darks. Every row of the inventory carries the message,
    /// the full run ends with it in its error, both exit 1, and each process writes the runtime's
    /// line once, at Warn.
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

        string logs = Path.Combine(app, "log");

        (int code, string stdout, _) = RunCopy(app, "--inventory", session);
        Assert.Equal(1, code);
        string[] rows = stdout.Split('\n').Where(l => l.Contains("DSC_000", StringComparison.Ordinal)).ToArray();
        Assert.Equal(3, rows.Length);
        Assert.All(rows, r => Assert.EndsWith("error: " + English, r.TrimEnd('\r'), StringComparison.Ordinal));
        Assert.Equal(["WARN"], RuntimeLines(logs));

        // The second process appends to the first one's file, its own one line after the first's;
        // nothing is deleted between the runs, which a scanner holding the closed file could fail.
        (code, _, string stderr) = RunCopy(app, session);
        Assert.Equal(1, code);
        Assert.Contains("ERROR: no dark could be decoded: DSC_0001.NEF: " + English + "; DSC_0002.NEF: " + English, stderr, StringComparison.Ordinal);
        Assert.Equal(["WARN", "WARN"], RuntimeLines(logs));
    }

    /// <summary>
    /// The names of the DLLs a PE file imports, from its import directory. A file that also
    /// delay-loads DLLs fails the test, since its delay-load directory is not read: LibRaw
    /// 0.21.1's four DLLs have none.
    /// </summary>
    /// <param name="path">The DLL to read.</param>
    /// <returns>The imported DLLs' names, as the file spells them.</returns>
    private static List<string> Imports(string path)
    {
        using PEReader pe = new(File.OpenRead(path));
        Assert.True(pe.PEHeaders.PEHeader!.DelayImportTableDirectory.Size == 0, $"{Path.GetFileName(path)} delay-loads DLLs, which this test does not read");
        DirectoryEntry table = pe.PEHeaders.PEHeader!.ImportTableDirectory;
        System.Reflection.Metadata.BlobReader descriptors = pe.GetSectionData(table.RelativeVirtualAddress).GetReader();
        List<string> names = [];
        while (true)
        {
            // IMAGE_IMPORT_DESCRIPTOR: five 32-bit fields, the name's address the fourth; an
            // all-zero descriptor ends the table.
            descriptors.ReadInt32();
            descriptors.ReadInt32();
            descriptors.ReadInt32();
            int name = descriptors.ReadInt32();
            descriptors.ReadInt32();
            if (name == 0)
            {
                return names;
            }

            System.Reflection.Metadata.BlobReader text = pe.GetSectionData(name).GetReader();
            StringBuilder sb = new();
            for (byte b = text.ReadByte(); b != 0; b = text.ReadByte())
            {
                sb.Append((char)b);
            }

            names.Add(sb.ToString());
        }
    }

    /// <summary>
    /// Copies the console as the test's own output folder holds it, every file but one, so the copy
    /// starts as the console does and lacks only that DLL. The output's own <c>log</c> folder stays
    /// behind: LoggingTests' console runs write into it, at the same time as this test may run, and
    /// a copy of it would hold their lines and, once rolled, send the copy's lines to another file.
    /// </summary>
    private static string CopyConsoleWithout(string to, string left)
    {
        string from = AppContext.BaseDirectory;
        Assert.True(File.Exists(Path.Combine(from, "darkscheck.exe")), "the console is not in the test's output folder");
        Assert.True(File.Exists(Path.Combine(from, "runtimes", "win-x64", "native", left)), left + " is not where the copy leaves it out");
        string ownLog = Path.Combine(from, "log") + Path.DirectorySeparatorChar;
        foreach (string f in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
        {
            if (string.Equals(Path.GetFileName(f), left, StringComparison.OrdinalIgnoreCase)
                || f.StartsWith(ownLog, StringComparison.OrdinalIgnoreCase))
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

    /// <summary>
    /// The levels of the runtime's lines in a copy's log folder, every file of it, rolled ones
    /// included; the copy starts without one, so they are the lines of its own runs.
    /// </summary>
    private static string[] RuntimeLines(string logs)
        => Directory.GetFiles(logs, "*.log").Order(StringComparer.Ordinal).SelectMany(File.ReadAllLines)
            .Select(l => RuntimeLine().Match(l)).Where(m => m.Success).Select(m => m.Groups[1].Value).ToArray();

    // The DLLs of the Visual C++ v14 redistributable (vcruntime, msvcp, vcomp, concrt, vccorlib,
    // vcamp, mfc and mfcm), not only the four LibRaw 0.21.1 imports, so a build importing another
    // turns the test red. Read from the import directory; Imports refuses a DLL with delay loads.
    [GeneratedRegex(@"^(vcruntime|msvcp|vcomp|concrt|vccorlib|vcamp|mfcm?)\d", RegexOptions.IgnoreCase)]
    private static partial Regex VcRuntimeDll();

    [GeneratedRegex(@"^\S+ \S+ (\w+)\s+\[\d+:\d+\] RawReader libraw cannot be loaded; Visual C\+\+ runtime: ")]
    private static partial Regex RuntimeLine();
}
