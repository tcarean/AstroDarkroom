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
/// R-137: no output is written over a file the run reads. Until 2026-11-13 the only guard
/// was <c>--synthetic</c> against the input file: <c>flatcheck master.fit -o master.fit</c>
/// printed its report and then replaced the master with the PNG (CR-011 of the review of
/// 2026-11-13).
/// </summary>
public class OutputOverInputTests
{
    /// <summary>The parse refuses each pair, whatever the mode and however the same file is
    /// spelled, and names the output as typed.</summary>
    [Theory]
    [InlineData("m.fit -o m.fit", "-o nu poate scrie poza peste un fisier de intrare (m.fit); da alta cale")]
    [InlineData("m.fit --out ./m.fit", "-o nu poate scrie poza peste un fisier de intrare (./m.fit); da alta cale")]
    [InlineData("-o sub/../m.fit m.fit --expo", "-o nu poate scrie poza peste un fisier de intrare (sub/../m.fit); da alta cale")]
    [InlineData("m.fit --histeq -o m.fit", "-o nu poate scrie poza peste un fisier de intrare (m.fit); da alta cale")]
    [InlineData("a.fit --against b.fit -o b.fit", "-o nu poate scrie poza peste un fisier de intrare (b.fit); da alta cale")]
    [InlineData("m.fit --raw flats/f.nef -o flats/f.nef", "-o nu poate scrie poza peste un fisier de intrare (flats/f.nef); da alta cale")]
    [InlineData("m.fit --synthetic s.fit --raw s.fit --lens x", "--synthetic nu poate scrie peste raw-ul dat cu --raw; da alta cale")]
    [InlineData("m.fit --synthetic m.fit --lens x", "--synthetic nu poate scrie peste cadrul de referinta; da alta cale")]
    public void R137_AnOutputNamingAnInputIsRefusedByTheParse(string line, string error)
    {
        var o = Options.Parse(line.Split(' '));

        Assert.Equal(error, o.Error);
    }

    /// <summary>The same refusals in English.</summary>
    [Theory]
    [InlineData("m.fit -o m.fit", "-o cannot write the picture over an input file (m.fit); give another path")]
    [InlineData("m.fit --synthetic s.fit --raw s.fit", "--synthetic cannot write over the raw given with --raw; give another path")]
    public void R137_TheRefusalInEnglish(string line, string error)
    {
        using (Ui.Use(UiLanguage.English))
            Assert.Equal(error, Options.Parse(line.Split(' ')).Error);
    }

    /// <summary>Only the files the run reads are protected: any other name is an output like
    /// before, a FITS name included.</summary>
    [Theory]
    [InlineData("m.fit -o other.fit")]
    [InlineData("a.fit --against b.fit -o c.png")]
    [InlineData("m.fit --synthetic s.fit --raw f.nef --lens x -o map.png")]
    public void R137_AnOutputThatIsNoInputIsAccepted(string line)
    {
        Assert.Null(Options.Parse(line.Split(' ')).Error);
    }

    /// <summary>From the console: exit 1, the sentence on stderr, no report, and the master's
    /// bytes as they were. It printed the report and wrote the PNG into the master.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("--histeq")]
    public void R137_TheMasterNamedAsTheOutputIsLeftIntact(string mode)
    {
        using var ws = new TempWorkspace();
        byte[] fits = FitsTestData.BuildFits(64, 48, (x, y) => 1000 + x + y);
        string master = ws.WriteFile("pp_flat_stacked.fit", fits);
        var args = new List<string> { master, "-o", master };
        if (mode.Length > 0) args.Add(mode);
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        int exit = FlatCheckCli.Run(args.ToArray(), stdout, stderr);

        Assert.Equal(1, exit);
        Assert.Contains("-o nu poate scrie poza peste un fisier de intrare (" + master + "); da alta cale", stderr.ToString());
        Assert.DoesNotContain("VERDICT", stdout.ToString());
        Assert.Equal(fits, File.ReadAllBytes(master));
    }

    /// <summary>--against: the second flat named as the output is left intact too.</summary>
    [Fact]
    public void R137_TheSecondFlatNamedAsTheOutputIsLeftIntact()
    {
        using var ws = new TempWorkspace();
        byte[] fits = FitsTestData.BuildFits(64, 48, (x, y) => 1000 + x + y);
        string a = ws.WriteFile("a.fit", fits);
        string b = ws.WriteFile("b.fit", fits);
        var stderr = new StringWriter();

        int exit = FlatCheckCli.Run(new[] { a, "--against", b, "-o", b }, new StringWriter(), stderr);

        Assert.Equal(1, exit);
        Assert.Contains("un fisier de intrare (" + b + ")", stderr.ToString());
        Assert.Equal(fits, File.ReadAllBytes(b));
    }

    /// <summary>--light on a folder: the frames are known only from the disk, so the refusal
    /// comes from the run, before a frame is read (the frames here are no FITS at all, and
    /// the run does not get to say so).</summary>
    [Fact]
    public void R137_AFrameOfTheSeriesNamedAsTheOutputIsLeftIntact()
    {
        using var ws = new TempWorkspace();
        byte[] bytes = { 1, 2, 3 };
        ws.WriteFile("pp_light_00001.fit", bytes);
        string second = ws.WriteFile("pp_light_00002.fit", bytes);
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        int exit = FlatCheckCli.Run(new[] { ws.Dir, "--light", "-o", second }, stdout, stderr);

        Assert.Equal(1, exit);
        Assert.Equal("EROARE: -o nu poate scrie poza peste un cadru din serie (" + second + "); da alta cale",
                     stderr.ToString().Trim());
        Assert.Equal(bytes, File.ReadAllBytes(second));
    }

    /// <summary>A file of the folder the pattern does not match is no frame of the series:
    /// the run goes on (and fails here on the frames, which are no FITS), without the
    /// refusal.</summary>
    [Fact]
    public void R137_AFileOfTheFolderOutsideThePatternIsNoFrameOfTheSeries()
    {
        using var ws = new TempWorkspace();
        ws.WriteFile("pp_light_00001.fit", new byte[] { 1, 2, 3 });
        string other = ws.PathTo("map.png");
        var stderr = new StringWriter();

        FlatCheckCli.Run(new[] { ws.Dir, "--light", "-o", other }, new StringWriter(), stderr);

        Assert.DoesNotContain("cadru din serie", stderr.ToString());
    }
}
