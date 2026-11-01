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

// LibRawLoadTests.cs - what the user and the log are told when LibRaw cannot be loaded (R-124,
// R-110, ADR 0085).
//
// LibRaw cannot be made to fail here: this machine has the Visual C++ runtime and the test
// output has LibRaw's DLLs. So the refusal is built from a loader's exception directly, the probe
// runs for real and its "missing" branch is reached through a stand-in loader, and the four DLLs
// the probe asks for are read from the import tables of the DLLs the test output carries.

using System.Text;
using System.Text.RegularExpressions;
using NLog;

namespace FlatCheck.Tests;

public class LibRawLoadTests
{
    /// <summary>What .NET says in a folder publish when Sdcb.LibRaw's resolver gives up: it names
    /// libraw.dll, its last try, whether raw_r.dll was missing or the runtime it imports (a
    /// single-file publish says only "Dll was not found."; measured 2026-11-01).</summary>
    const string LoaderMessage = "Unable to load DLL 'libraw.dll' or one of its dependencies: The specified module could not be found. (0x8007007E)";

    /// <summary>
    /// On Windows the message names the Visual C++ runtime, missing or too old, with the launcher's
    /// words (AstroDarkroom's VcRuntime.Warning), and ends with Microsoft's link, so that nothing
    /// of the message follows the address a user copies. No advice about publishing, no
    /// libraw.dll.
    /// </summary>
    [Fact]
    public void R124_OnWindowsTheMessageNamesTheRuntimeAndEndsWithMicrosoftsLink()
    {
        using (Ui.Use(UiLanguage.English))
            Assert.Equal("cannot load LibRaw: most likely Microsoft's Visual C++ runtime is missing or too old, so FlatCheck "
                       + "cannot read camera raw files until its latest version is installed (the FITS modes work without it): "
                       + "https://aka.ms/vc14/vc_redist.x64.exe",
                         RawReader.CannotLoadMessage(windows: true));
        using (Ui.Use(UiLanguage.Romanian))
            Assert.Equal("nu pot incarca LibRaw: cel mai probabil runtime-ul Microsoft Visual C++ lipseste sau este prea vechi, "
                       + "deci FlatCheck nu poate citi fisiere raw de aparat pana nu instalezi ultima lui versiune (modurile pe "
                       + "FITS merg si fara el): https://aka.ms/vc14/vc_redist.x64.exe",
                         RawReader.CannotLoadMessage(windows: true));

        foreach (UiLanguage language in new[] { UiLanguage.English, UiLanguage.Romanian })
            using (Ui.Use(language))
            {
                string m = RawReader.CannotLoadMessage(windows: true);
                Assert.EndsWith(": " + VcRuntime.DownloadUrl, m);
                Assert.DoesNotContain("libraw.dll", m);
                Assert.DoesNotContain("IncludeNativeLibrariesForSelfExtract", m);
                Assert.True(m.All(c => c < 128), "a character outside ASCII: " + m);
            }
    }

    /// <summary>
    /// Elsewhere there is no Visual C++ runtime: the message keeps the advice about LibRaw's
    /// native files, which exist for Windows x64 and Linux x64 only.
    /// </summary>
    [Fact]
    public void R124_ElsewhereTheMessageSaysWhereLibRawsNativeLibrariesMustBe()
    {
        using (Ui.Use(UiLanguage.English))
            Assert.Equal("cannot load LibRaw, so camera raw files cannot be read (the FITS modes work without it): its native "
                       + "libraries exist for Windows x64 and Linux x64 only, and must sit beside the program or inside it, as a "
                       + "publish with IncludeNativeLibrariesForSelfExtract puts them",
                         RawReader.CannotLoadMessage(windows: false));
        using (Ui.Use(UiLanguage.Romanian))
        {
            string m = RawReader.CannotLoadMessage(windows: false);
            Assert.StartsWith("nu pot incarca LibRaw", m);
            Assert.Contains("Windows x64 si Linux x64", m);
            Assert.DoesNotContain("Visual C++", m);
            Assert.True(m.All(c => c < 128), "a character outside ASCII: " + m);
        }
    }

    /// <summary>
    /// The refusal carries no inner exception, so the window, which adds every inner message it
    /// does not see in the text, shows the message alone; the loader's exception, with its
    /// libraw.dll, goes to the log on RawReader's own Warn line, which on Windows says where
    /// raw_r.dll is among the folders .NET searches and what the runtime probe found.
    /// </summary>
    [Fact]
    public void R124_TheRefusalCarriesNoInnerExceptionAndTheLogKeepsTheLoaders()
    {
        var cause = new DllNotFoundException(LoaderMessage);
        using var capture = LogCapture.Start();

        Exception refusal = RawReader.CannotLoad(cause);

        var e = Assert.IsType<InvalidOperationException>(refusal);
        Assert.Null(e.InnerException);
        Assert.Equal(RawReader.CannotLoadMessage(OperatingSystem.IsWindows()), e.Message);
        var line = Assert.Single(capture.Events);
        Assert.Equal(LogLevel.Warn, line.Level);
        Assert.Equal("RawReader", line.Logger);
        Assert.Same(cause, line.Exception);
        Assert.StartsWith("LibRaw could not be loaded, so camera raw files cannot be read", line.Message);
        Assert.Contains(LoaderMessage, line.Line);
        if (OperatingSystem.IsWindows())
        {
            // The test output has raw_r.dll where .NET looks for it, and this machine the runtime
            // or not: either way the probe's clause is there, in the launcher's words.
            Assert.Contains("; raw_r.dll in ", line.Message);
            Assert.DoesNotContain("in none of", line.Message);
            Assert.Contains("; Visual C++ runtime: ", line.Message);
        }
    }

    /// <summary>The log line's words, for each answer of the probe (R-110).</summary>
    [Fact]
    public void R110_TheLineSaysWhereRawRIsAndWhichRuntimeDllsDoNotLoad()
    {
        Assert.Equal("LibRaw could not be loaded, so camera raw files cannot be read; raw_r.dll in app/native; "
                   + "Visual C++ runtime: vcruntime140.dll, vcruntime140_1.dll, msvcp140.dll, vcomp140.dll load",
                     RawReader.CannotLoadLine(windows: true, "app/native", new[] { "app", "app/native" }, Array.Empty<string>()));
        Assert.Equal("LibRaw could not be loaded, so camera raw files cannot be read; raw_r.dll in none of [app, temp]; "
                   + "Visual C++ runtime: missing vcruntime140_1.dll, vcomp140.dll",
                     RawReader.CannotLoadLine(windows: true, null, new[] { "app", "temp" }, new[] { "vcruntime140_1.dll", "vcomp140.dll" }));
        Assert.Equal("LibRaw could not be loaded, so camera raw files cannot be read",
                     RawReader.CannotLoadLine(windows: false, null, Array.Empty<string>(), Array.Empty<string>()));
    }

    /// <summary>raw_r.dll is looked for in the folders given, in their order; null when none
    /// holds it.</summary>
    [Fact]
    public void R110_RawRIsTheFirstOfTheSearchedFoldersThatHoldsIt()
    {
        var present = new HashSet<string> { Path.Combine("b", "raw_r.dll"), Path.Combine("c", "raw_r.dll") };

        Assert.Equal("b", RawReader.FolderHolding("raw_r.dll", new[] { "a", "b", "c" }, present.Contains));
        Assert.Null(RawReader.FolderHolding("raw_r.dll", new[] { "a" }, present.Contains));
        Assert.Null(RawReader.FolderHolding("raw_r.dll", Array.Empty<string>(), present.Contains));
    }

    /// <summary>The probe asks for each of the four DLLs once, in their order, and gives back the
    /// ones that do not load in that order: the launcher's VcRuntime.Missing.</summary>
    [Fact]
    public void R124_TheProbeAsksForEachRuntimeDllOnceAndKeepsTheirOrder()
    {
        var asked = new List<string>();

        IReadOnlyList<string> missing = VcRuntime.Missing(file =>
        {
            asked.Add(file);
            return file != "vcruntime140_1.dll" && file != "vcomp140.dll";
        });

        Assert.Equal(new[] { "vcruntime140.dll", "vcruntime140_1.dll", "msvcp140.dll", "vcomp140.dll" }, VcRuntime.Files);
        Assert.Equal(VcRuntime.Files, asked);
        Assert.Equal(new[] { "vcruntime140_1.dll", "vcomp140.dll" }, missing);
        Assert.Empty(VcRuntime.Missing(_ => true));
        Assert.Equal("https://aka.ms/vc14/vc_redist.x64.exe", VcRuntime.DownloadUrl);
    }

    /// <summary>
    /// The four DLLs are the Visual C++ runtime that LibRaw's DLLs import, read from their import
    /// tables in the test output (measured 2026-11-01: raw_r.dll imports all four, jpeg8, lcms2
    /// and zlib1 import vcruntime140.dll). A new LibRaw build that imports another part of the
    /// runtime fails here before the probe and the message go stale.
    /// </summary>
    [Fact]
    public void R124_TheFourDllsAreTheVisualCppRuntimeThatLibRawsDllsImport()
    {
        string native = Path.Combine(AppContext.BaseDirectory, "runtimes", "win-x64", "native");

        string[] runtime = Directory.GetFiles(native, "*.dll")
            .SelectMany(Imports)
            .Where(name => Regex.IsMatch(name, "^(vcruntime|msvcp|vcomp|concrt|vccorlib)[0-9]", RegexOptions.IgnoreCase))
            .Select(name => name.ToLowerInvariant())
            .Distinct()
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(VcRuntime.Files.OrderBy(name => name, StringComparer.Ordinal), runtime);
        Assert.Contains("vcomp140.dll", Imports(Path.Combine(native, "raw_r.dll")).Select(n => n.ToLowerInvariant()));
    }

    /// <summary>
    /// The DLL names a PE file imports: its import directory and its delay-load one, the names
    /// read through the section table. Enough of the format for LibRaw's four x64 DLLs.
    /// </summary>
    static IEnumerable<string> Imports(string path)
    {
        byte[] b = File.ReadAllBytes(path);
        int pe = BitConverter.ToInt32(b, 0x3C);
        int sections = BitConverter.ToUInt16(b, pe + 6);
        int optionalSize = BitConverter.ToUInt16(b, pe + 20);
        int optional = pe + 24;
        int directories = optional + (BitConverter.ToUInt16(b, optional) == 0x20B ? 112 : 96);
        var table = Enumerable.Range(0, sections)
            .Select(i => optional + optionalSize + 40 * i)
            .Select(s => (Va: BitConverter.ToUInt32(b, s + 12),
                          Size: Math.Max(BitConverter.ToUInt32(b, s + 8), BitConverter.ToUInt32(b, s + 16)),
                          Raw: BitConverter.ToUInt32(b, s + 20)))
            .ToArray();

        int Offset(uint rva)
        {
            var s = table.First(t => rva >= t.Va && rva < t.Va + t.Size);
            return (int)(rva - s.Va + s.Raw);
        }

        string Name(uint rva)
        {
            int start = Offset(rva), end = Array.IndexOf(b, (byte)0, start);
            return Encoding.ASCII.GetString(b, start, end - start);
        }

        // (directory index, descriptor size, offset of the name's RVA): the import directory and
        // the delay-load one.
        foreach ((int index, int size, int nameAt) in new[] { (1, 20, 12), (13, 32, 4) })
        {
            uint rva = BitConverter.ToUInt32(b, directories + 8 * index);
            if (rva == 0) continue;
            for (int o = Offset(rva); BitConverter.ToUInt32(b, o + nameAt) != 0; o += size)
                yield return Name(BitConverter.ToUInt32(b, o + nameAt));
        }
    }
}
