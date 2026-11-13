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

namespace FlatCheck.Tests;

/// <summary>
/// R-138: a file FlatCheck writes is the earlier file whole or the new one whole. Until
/// 2026-11-13 every output was written into the existing file, which the write first cut to
/// nothing, so a write that failed half way left a cut file under the real name (CR-012 of
/// the review of 2026-11-13).
/// </summary>
public class OutputFileTests
{
    static readonly byte[] Old = Enumerable.Repeat((byte)0x55, 5000).ToArray();

    // ------------------------------------------------------------ the console's two writes

    /// <summary>The map: the old file is not written into (its second name keeps the old
    /// bytes), the path holds a whole PNG, and no temporary file is left.</summary>
    [Fact]
    public void R138_TheConsolesMapReplacesTheOldFileAndNeverWritesIntoIt()
    {
        using var ws = new TempWorkspace();
        string master = ws.WriteFile("m.fit", FitsTestData.BuildFits(64, 48, (x, y) => 1000 + x + y));
        string png = ws.WriteFile("map.png", Old);
        string oldFile = SecondName.Of(png);

        int exit = FlatCheckCli.Run(new[] { master, "--histeq", "-o", png }, new StringWriter(), new StringWriter());

        Assert.Equal(0, exit);
        Assert.Equal(Old, File.ReadAllBytes(oldFile));
        Assert.Equal(new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G' }, File.ReadAllBytes(png).Take(4).ToArray());
        Assert.False(File.Exists(png + ".tmp"));
    }

    /// <summary>The synthetic flat, the 96 MB file of the entry: the same three facts.</summary>
    [Fact]
    public void R138_TheConsolesSyntheticFlatReplacesTheOldFileAndNeverWritesIntoIt()
    {
        using var ws = new TempWorkspace();
        string profiles = LensProfileTestData.ProfilesDir(ws);
        string reference = GoldenOutputTests.Reference(ws);
        string target = ws.WriteFile("synthetic.fit", Old);
        string oldFile = SecondName.Of(target);

        int exit = FlatCheckCli.Run(new[] { reference, "--synthetic", target, "--lens", "Tokina AT-X 11-20mm F2.8 PRO DX", "--fstop", "2.8", "--profiles", profiles },
                                    new StringWriter(), new StringWriter());

        Assert.Equal(0, exit);
        Assert.Equal(Old, File.ReadAllBytes(oldFile));
        Assert.Equal(-32, FitsReader.LoadFull(File.ReadAllBytes(target)).Bitpix);
        Assert.False(File.Exists(target + ".tmp"));
    }

    // ------------------------------------------------------------ OutputFile itself

    /// <summary>A write that fails half way: the earlier file is whole, the temporary file is
    /// gone, and the failure is the writer's own.</summary>
    [Fact]
    public void R138_AWriteThatFailsHalfWayLeavesTheEarlierFileWholeAndNoTemporaryFile()
    {
        using var ws = new TempWorkspace();
        string path = ws.WriteFile("out.fit", Old);

        var e = Assert.Throws<IOException>(() => OutputFile.Write(path, s =>
        {
            s.Write(new byte[1000], 0, 1000);
            throw new IOException("the disk is full");
        }));

        Assert.Equal("the disk is full", e.Message);
        Assert.Equal(Old, File.ReadAllBytes(path));
        Assert.False(File.Exists(path + ".tmp"));
    }

    /// <summary>With no earlier file, a failed write leaves nothing under the name.</summary>
    [Fact]
    public void R138_AFailedFirstWriteLeavesNoFileAtAll()
    {
        using var ws = new TempWorkspace();
        string path = ws.PathTo("out.png");

        Assert.Throws<InvalidOperationException>(() => OutputFile.Write(path, s =>
        {
            s.WriteByte(1);
            throw new InvalidOperationException("no");
        }));

        Assert.False(File.Exists(path));
        Assert.False(File.Exists(path + ".tmp"));
    }

    /// <summary>The three forms write the same way, and a temporary file left by a process
    /// that ended mid-write is simply overwritten.</summary>
    [Fact]
    public void R138_AGoodWriteReplacesTheFile_OverALeftoverTemporaryFileToo()
    {
        using var ws = new TempWorkspace();
        string path = ws.WriteFile("out.bin", Old);
        File.WriteAllBytes(path + ".tmp", new byte[20000]);          // longer than what follows

        OutputFile.WriteAllBytes(path, new byte[] { 1, 2, 3 });
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(path));
        Assert.False(File.Exists(path + ".tmp"));

        OutputFile.Write(path, s => s.Write(new byte[] { 4, 5 }, 0, 2));
        Assert.Equal(new byte[] { 4, 5 }, File.ReadAllBytes(path));

        string text = ws.PathTo("report.txt");
        OutputFile.WriteAllText(text, "VERDICT: BUN\n");
        // UTF-8 without a byte order mark, as File.WriteAllText wrote it
        Assert.Equal(System.Text.Encoding.UTF8.GetBytes("VERDICT: BUN\n"), File.ReadAllBytes(text));
        Assert.False(File.Exists(text + ".tmp"));
    }

    /// <summary>A device is no file to keep whole and cannot be replaced by a move: it is
    /// written into, as before. <c>-o NUL</c>, for a run whose map is not wanted, ended in
    /// "Could not replace NUL" and exit 1 between the first fix and the review of the
    /// branch.</summary>
    [Fact]
    public void R138_ADeviceIsWrittenIntoNotReplaced()
    {
        string device = OperatingSystem.IsWindows() ? "NUL" : "/dev/null";

        OutputFile.WriteAllBytes(device, new byte[] { 1, 2, 3 });
        OutputFile.Write(device, s => s.Write(new byte[] { 4, 5 }, 0, 2));

        Assert.False(File.Exists(device + OutputFile.TemporarySuffix));
    }

    /// <summary>The same from the console: the run ends with its own exit code.</summary>
    [Fact]
    public void R138_TheConsoleStillTakesADeviceAsItsMap()
    {
        using var ws = new TempWorkspace();
        string master = ws.WriteFile("m.fit", FitsTestData.BuildFits(64, 48, (x, y) => 1000 + x + y));
        var stderr = new StringWriter();

        int exit = FlatCheckCli.Run(new[] { master, "--histeq", "-o", OperatingSystem.IsWindows() ? "NUL" : "/dev/null" }, new StringWriter(), stderr);

        Assert.Equal("", stderr.ToString());
        Assert.Equal(0, exit);
    }

    /// <summary>A folder named as the output is refused as a folder, before anything is
    /// written beside it; the sentence for a file that cannot be replaced would name two
    /// causes that are not this one.</summary>
    [Fact]
    public void R138_AFolderNamedAsTheOutputIsRefusedAsAFolder()
    {
        using var ws = new TempWorkspace();
        string folder = Path.Combine(ws.Dir, "maps");
        Directory.CreateDirectory(folder);

        var e = Assert.Throws<IOException>(() => OutputFile.WriteAllBytes(folder, new byte[] { 1 }));

        Assert.Equal(folder + " e un folder; iesirea are nevoie de un nume de fisier.", e.Message);
        using (Ui.Use(UiLanguage.English))
            Assert.Equal(folder + " is a folder; the output needs a file name.",
                         Assert.Throws<IOException>(() => OutputFile.WriteAllBytes(folder, new byte[] { 1 })).Message);
        Assert.False(File.Exists(folder + OutputFile.TemporarySuffix));
    }

    /// <summary>A target that cannot be replaced (here read-only) stays as it was, and the
    /// failure names it and says what to look for, in both languages. On Windows only: a
    /// rename on Linux or macOS asks the folder's permission, not the file's, and replaces a
    /// read-only file.</summary>
    [Fact]
    public void R138_ATargetThatCannotBeReplacedIsNamedAndLeftAsItWas()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var ws = new TempWorkspace();
        string path = ws.WriteFile("out.png", Old);
        File.SetAttributes(path, FileAttributes.ReadOnly);
        try
        {
            var e = Assert.Throws<IOException>(() => OutputFile.WriteAllBytes(path, new byte[] { 1 }));
            Assert.StartsWith("Nu am putut inlocui " + path + ": fisierul e deschis in alt program sau e doar pentru citire (", e.Message);
            using (Ui.Use(UiLanguage.English))
                Assert.StartsWith("Could not replace " + path + ": the file is open in another program or is read-only (",
                                  Assert.Throws<IOException>(() => OutputFile.WriteAllBytes(path, new byte[] { 1 })).Message);
            Assert.Equal(Old, File.ReadAllBytes(path));
            Assert.False(File.Exists(path + ".tmp"));
        }
        finally { File.SetAttributes(path, FileAttributes.Normal); }
    }
}
