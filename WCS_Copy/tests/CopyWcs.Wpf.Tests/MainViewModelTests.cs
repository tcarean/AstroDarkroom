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

using CopyWcs.Tests;

namespace CopyWcs.Wpf.Tests;

/// <summary>
/// The view-model against the console, without a window: the boxes and the two commands
/// (R-030), the preview's cards equal to the console's dry run (R-031), the copy's bytes equal
/// to the console's (R-032), the refusals and the warnings in the core's words with nothing
/// written on a refusal (R-033), and the texts in both languages (R-036). The assembly runs in
/// Romanian (<see cref="RomanianByDefault"/>); the English pass opens its own scope.
/// </summary>
public class MainViewModelTests
{
    /// <summary>The console's run on the same request: the exit code and stdout's lines, the banner and the copyright dropped.</summary>
    private static (int ExitCode, string[] Lines, string Stderr) Console(params string[] args)
    {
        using StringWriter stdout = new();
        using StringWriter stderr = new();
        int code = CopyWcsCli.Run(args, stdout, stderr);
        string[] lines = stdout.ToString().ReplaceLineEndings("\n").TrimEnd('\n').Split('\n');
        return (code, lines[2..], stderr.ToString().Trim());
    }

    private static MainViewModel Open(WindowFixture f) => new() { Source = f.Source, Dest = f.Dest };

    [Fact]
    public void R030_TheCommandsWaitForBothFilesAndTheCopyForTheNewFilesPath()
    {
        using WindowFixture f = new();
        MainViewModel vm = new();

        Assert.True(vm.WriteInPlace);
        Assert.True(vm.IncludeOptical);
        Assert.False(vm.Force);
        Assert.False(vm.CanPreview);
        Assert.False(vm.CanCopy);
        Assert.False(vm.HasOutcome);
        Assert.Equal("", vm.SourceFacts);
        Assert.Equal("", vm.DestFacts);

        vm.Source = f.Source;
        Assert.False(vm.CanPreview);
        vm.Dest = f.Dest;
        Assert.True(vm.CanPreview);
        Assert.True(vm.CanCopy);

        vm.WriteInPlace = false;
        Assert.True(vm.WritesNewFile);
        Assert.False(vm.CanCopy);
        Assert.True(vm.CanPreview);
        vm.Output = "   ";
        Assert.False(vm.CanCopy);
        vm.Output = f.FilePath("out.fit");
        Assert.True(vm.CanCopy);

        vm.Dest = f.FilePath("missing.fit");
        Assert.False(vm.CanPreview);
        Assert.False(vm.CanCopy);
    }

    [Fact]
    public void R030_TheFactsUnderEachBoxComeFromTheHeaderAndVanishWhenTheFileCannotBeRead()
    {
        using WindowFixture f = new();
        MainViewModel vm = Open(f);

        Assert.Equal("Sursa: 100 x 80, plate-solved", vm.SourceFacts);
        Assert.Equal("Destinatia: 100 x 80", vm.DestFacts);

        // the source is not plate-solved, the destination already is
        vm.Source = f.Dest;
        vm.Dest = f.Source;
        Assert.Equal("Sursa: 100 x 80, fara plate solve", vm.SourceFacts);
        Assert.Equal("Destinatia: 100 x 80, deja plate-solved", vm.DestFacts);

        // a missing file and a file that is not FITS say nothing: the run will say why
        vm.Source = f.FilePath("missing.fit");
        Assert.Equal("", vm.SourceFacts);
        File.WriteAllText(f.FilePath("text.fit"), "not a FITS file at all, just text that is long enough to read");
        vm.Dest = f.FilePath("text.fit");
        Assert.Equal("", vm.DestFacts);
    }

    [Fact]
    public void R031_ThePreviewShowsTheConsolesDryRunLinesAndWritesNothing()
    {
        using WindowFixture f = new();
        byte[] before = File.ReadAllBytes(f.Dest);
        MainViewModel vm = Open(f);

        vm.Preview();

        (int code, string[] lines, string stderr) = Console(f.Source, f.Dest, "--dry-run");
        Assert.Equal(0, code);
        Assert.Equal("", stderr);
        Assert.True(vm.HasOutcome);
        Assert.False(vm.HasError);
        Assert.False(vm.HasWarnings);
        Assert.True(vm.HasHeading);
        Assert.Equal(lines[0], vm.Heading);
        Assert.Equal("Chei care ar fi copiate (15):", vm.Heading);
        Assert.True(vm.HasCards);
        Assert.Equal(lines[1..].Select(l => l[2..]), vm.Cards);
        Assert.Equal(15, vm.Cards.Count);
        Assert.All(vm.Cards, c => Assert.Equal(c.TrimEnd(), c));
        Assert.False(vm.HasNotes);
        Assert.Equal(before, File.ReadAllBytes(f.Dest));

        // without the optical keys: the console's --no-optical list
        vm.IncludeOptical = false;
        vm.Preview();
        (_, lines, _) = Console(f.Source, f.Dest, "--dry-run", "--no-optical");
        Assert.Equal("Chei care ar fi copiate (11):", vm.Heading);
        Assert.Equal(lines[1..].Select(l => l[2..]), vm.Cards);
    }

    [Fact]
    public void R032_TheCopyInPlaceWritesTheConsolesBytes()
    {
        using WindowFixture window = new();
        using WindowFixture console = new();
        MainViewModel vm = Open(window);

        vm.Copy();

        (int code, string[] lines, _) = Console(console.Source, console.Dest);
        Assert.Equal(0, code);
        Assert.Equal(File.ReadAllBytes(console.Dest), File.ReadAllBytes(window.Dest));
        Assert.False(vm.HasError);
        Assert.True(vm.HasCards);
        Assert.Equal(15, vm.Cards.Count);
        Assert.False(vm.HasHeading);
        Assert.True(vm.HasNotes);
        // the closing lines name the target, which differs between the two temp folders
        Assert.Equal(lines.Select(l => l.Replace(console.Dir, window.Dir, StringComparison.Ordinal)), vm.Notes);
        Assert.StartsWith("Copiate 15 chei in ", vm.Notes[0], StringComparison.Ordinal);
        Assert.Equal("Destinatia: 100 x 80, deja plate-solved", vm.DestFacts);
    }

    [Fact]
    public void R032_TheCopyIntoANewFileWritesTheConsolesBytesAndLeavesTheDestination()
    {
        using WindowFixture window = new();
        using WindowFixture console = new();
        byte[] before = File.ReadAllBytes(window.Dest);
        MainViewModel vm = Open(window);
        vm.WriteInPlace = false;
        vm.Output = window.FilePath("out.fit");
        vm.Force = false;
        vm.IncludeOptical = false;

        vm.Copy();

        (int code, _, _) = Console(console.Source, console.Dest, "-o", console.FilePath("out.fit"), "--no-optical");
        Assert.Equal(0, code);
        Assert.Equal(File.ReadAllBytes(console.FilePath("out.fit")), File.ReadAllBytes(window.FilePath("out.fit")));
        Assert.Equal(before, File.ReadAllBytes(window.Dest));
        Assert.Equal(11, vm.Cards.Count);
        Assert.Equal("Destinatia: 100 x 80", vm.DestFacts);
    }

    [Fact]
    public void R033_AMismatchIsRefusedInTheCoresWordsAndNothingIsWritten()
    {
        using WindowFixture f = new(destWidth: 200);
        byte[] before = File.ReadAllBytes(f.Dest);
        MainViewModel vm = Open(f);

        vm.Copy();

        Assert.True(vm.HasOutcome);
        Assert.True(vm.HasError);
        Assert.Equal("Refuzat", vm.RefusedHeading);
        Assert.Equal("Dimensiuni diferite: sursa (100, 80) vs destinatie (200, 80). WCS-ul ar fi gresit. Foloseste --force daca stii ce faci.", vm.Error);
        Assert.Equal(Console(f.Source, f.Dest).Stderr, vm.Error);
        Assert.False(vm.HasWarnings);
        Assert.False(vm.HasCards);
        Assert.False(vm.HasNotes);
        Assert.Equal(before, File.ReadAllBytes(f.Dest));
    }

    [Fact]
    public void R033_AForcedMismatchCopiesAndShowsTheWarning()
    {
        using WindowFixture window = new(destWidth: 200);
        using WindowFixture console = new(destWidth: 200);
        MainViewModel vm = Open(window);
        vm.Force = true;

        vm.Copy();

        (int code, string[] lines, _) = Console(console.Source, console.Dest, "--force");
        Assert.Equal(0, code);
        Assert.False(vm.HasError);
        Assert.True(vm.HasWarnings);
        Assert.Equal(lines[0], Assert.Single(vm.Warnings));
        Assert.Equal("ATENTIE: Dimensiuni diferite: sursa (100, 80) vs destinatie (200, 80). Continui oricum (--force).", vm.Warnings[0]);
        Assert.Equal(File.ReadAllBytes(console.Dest), File.ReadAllBytes(window.Dest));
    }

    [Fact]
    public void R033_ASourceWithoutCtype1WarnsAndTheRunGoesOn()
    {
        List<string> cards = FitsTestData.PlateSolvedCards().Where(c => !c.StartsWith("CTYPE1", StringComparison.Ordinal)).ToList();
        using WindowFixture f = new(sourceCards: cards);
        MainViewModel vm = Open(f);
        Assert.Equal("Sursa: 100 x 80, fara plate solve", vm.SourceFacts);

        vm.Preview();

        Assert.Equal("ATENTIE: sursa nu pare plate-solved (lipseste CTYPE1).", Assert.Single(vm.Warnings));
        Assert.False(vm.HasError);
        Assert.Equal(14, vm.Cards.Count);
    }

    [Fact]
    public void R033_AnUnreadableSourceOrANoKeySourceIsRefusedInTheCoresWords()
    {
        using WindowFixture f = new();
        MainViewModel vm = Open(f);
        vm.Source = f.FilePath("missing.fit");

        vm.Preview();
        Assert.True(vm.HasError);
        Assert.StartsWith("Nu pot citi sursa '", vm.Error, StringComparison.Ordinal);
        Assert.Equal(Console(f.FilePath("missing.fit"), f.Dest, "--dry-run").Stderr, vm.Error);

        // a source with no key to copy (the processed header's OBJECT is an optical key, so
        // those are left out): the heading of the preview is not shown
        vm.Source = f.Dest;
        vm.IncludeOptical = false;
        vm.Preview();
        Assert.Equal("Nicio cheie relevanta gasita in sursa. Nimic de copiat.", vm.Error);
        Assert.False(vm.HasHeading);
        Assert.False(vm.HasCards);
    }

    [Fact]
    public void R036_TheFactsAndTheHeadingFollowTheLanguageOfTheRun()
    {
        using WindowFixture f = new(destWidth: 200);

        using (Ui.Use(UiLanguage.English))
        {
            MainViewModel vm = Open(f);
            Assert.Equal("Source: 100 x 80, plate-solved", vm.SourceFacts);
            Assert.Equal("Destination: 200 x 80", vm.DestFacts);
            Assert.Equal("Refused", vm.RefusedHeading);

            vm.Copy();
            Assert.StartsWith("Different dimensions: source (100, 80) vs destination (200, 80).", vm.Error, StringComparison.Ordinal);

            vm.Source = f.Dest;
            Assert.Equal("Source: 200 x 80, not plate-solved", vm.SourceFacts);
            vm.Dest = f.Source;
            Assert.Equal("Destination: 100 x 80, already plate-solved", vm.DestFacts);
        }

        MainViewModel ro = Open(f);
        Assert.Equal("Sursa: 100 x 80, plate-solved", ro.SourceFacts);
        Assert.Equal("Refuzat", ro.RefusedHeading);
    }
}
