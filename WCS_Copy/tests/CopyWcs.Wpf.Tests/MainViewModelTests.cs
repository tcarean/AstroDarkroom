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

    /// <summary>
    /// A look at a box's file that waits, for the path given, until the test releases it (5 s at
    /// most), as a look on a slow share would, then looks as the window does (R-052).
    /// </summary>
    internal static Func<string, bool, FileLook> HeldLook(ManualResetEventSlim release, string heldPath)
        => (path, source) =>
        {
            if (string.Equals(path, heldPath, StringComparison.Ordinal))
            {
                release.Wait(TimeSpan.FromSeconds(5));
            }

            return MainViewModel.Look(path, source);
        };

    /// <summary>Waits until a condition holds, 10 s at most: a look ends on another thread.</summary>
    private static async Task WaitFor(Func<bool> condition, string what)
    {
        Stopwatch clock = Stopwatch.StartNew();
        while (!condition())
        {
            Assert.True(clock.Elapsed < TimeSpan.FromSeconds(10), "waited 10 s for " + what);
            await Task.Delay(10);
        }
    }

    [Fact]
    public async Task R030_TheCommandsWaitForBothFilesAndTheCopyForTheNewFilesPath()
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

        // each box's file is looked at off the window's thread (R-052): the test waits for it
        vm.Source = f.Source;
        await vm.FactsRead;
        Assert.False(vm.CanPreview);
        vm.Dest = f.Dest;
        await vm.FactsRead;
        Assert.True(vm.CanPreview);
        Assert.True(vm.CanCopy);

        // a path typed with spaces around it names the same file
        vm.Source = "  " + f.Source + " ";
        await vm.FactsRead;
        Assert.True(vm.CanPreview);
        Assert.Equal("Sursa: 100 x 80, plate-solved", vm.SourceFacts);

        vm.WriteInPlace = false;
        Assert.True(vm.WritesNewFile);
        Assert.False(vm.CanCopy);
        Assert.True(vm.CanPreview);
        vm.Output = "   ";
        Assert.False(vm.CanCopy);
        vm.Output = f.FilePath("out.fit");
        Assert.True(vm.CanCopy);

        vm.Dest = f.FilePath("missing.fit");
        await vm.FactsRead;
        Assert.False(vm.CanPreview);
        Assert.False(vm.CanCopy);
    }

    /// <summary>The run goes off the window's thread, and both commands are off until it ends (R-030).</summary>
    [Fact]
    public async Task R030_TheCommandsAreOffWhileARunIsInFlight()
    {
        using WindowFixture f = new();
        MainViewModel vm = Open(f);
        // both files looked at first (R-052), so the commands are off for the run alone
        await vm.FactsRead;
        Assert.True(vm.CanPreview);

        Task run = vm.Preview();
        Assert.False(vm.CanPreview);
        Assert.False(vm.CanCopy);

        await run;
        Assert.True(vm.CanPreview);
        Assert.True(vm.CanCopy);
        Assert.True(vm.HasCards);
    }

    [Fact]
    public async Task R030_TheFactsUnderEachBoxComeFromTheHeaderAndVanishWhenTheFileCannotBeRead()
    {
        using WindowFixture f = new();
        MainViewModel vm = Open(f);
        await vm.FactsRead;

        Assert.Equal("Sursa: 100 x 80, plate-solved", vm.SourceFacts);
        Assert.Equal("Destinatia: 100 x 80", vm.DestFacts);

        // the source is not plate-solved, the destination already is
        vm.Source = f.Dest;
        vm.Dest = f.Source;
        await vm.FactsRead;
        Assert.Equal("Sursa: 100 x 80, fara plate solve", vm.SourceFacts);
        Assert.Equal("Destinatia: 100 x 80, deja plate-solved", vm.DestFacts);

        // a missing file and a file that is not FITS say nothing: the run will say why
        vm.Source = f.FilePath("missing.fit");
        await vm.FactsRead;
        Assert.Equal("", vm.SourceFacts);
        File.WriteAllText(f.FilePath("text.fit"), "not a FITS file at all, just text that is long enough to read");
        vm.Dest = f.FilePath("text.fit");
        await vm.FactsRead;
        Assert.Equal("", vm.DestFacts);

        // a header the reader accepts but that names no image size (NAXIS = 0) says nothing
        // either: the reader gives -1 for a missing key, and "-1 x -1" is not a fact of the file
        List<string> noSize = FitsTestData.PlateSolvedCards()
            .Where(c => !c.StartsWith("NAXIS1", StringComparison.Ordinal) && !c.StartsWith("NAXIS2", StringComparison.Ordinal))
            .Select(c => c.StartsWith("NAXIS ", StringComparison.Ordinal) ? FitsTestData.Card("NAXIS", "0") : c)
            .ToList();
        using WindowFixture g = new(sourceCards: noSize);
        vm.Source = g.Source;
        await vm.FactsRead;
        Assert.Equal("", vm.SourceFacts);
    }

    [Fact]
    public async Task R031_ThePreviewShowsTheConsolesDryRunLinesAndWritesNothing()
    {
        using WindowFixture f = new();
        byte[] before = File.ReadAllBytes(f.Dest);
        MainViewModel vm = Open(f);

        await vm.Preview();

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
        Assert.All(vm.Cards, c => Assert.Equal(c.TrimEnd(' '), c));
        Assert.False(vm.HasNotes);
        Assert.Equal(before, File.ReadAllBytes(f.Dest));

        // without the optical keys: the console's --no-optical list
        vm.IncludeOptical = false;
        await vm.Preview();
        (_, lines, _) = Console(f.Source, f.Dest, "--dry-run", "--no-optical");
        Assert.Equal("Chei care ar fi copiate (11):", vm.Heading);
        Assert.Equal(lines[1..].Select(l => l[2..]), vm.Cards);
    }

    [Fact]
    public async Task R032_TheCopyInPlaceWritesTheConsolesBytes()
    {
        using WindowFixture window = new();
        using WindowFixture console = new();
        MainViewModel vm = Open(window);

        await vm.Copy();

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
    public async Task R032_TheCopyIntoANewFileWritesTheConsolesBytesAndLeavesTheDestination()
    {
        using WindowFixture window = new();
        using WindowFixture console = new();
        byte[] before = File.ReadAllBytes(window.Dest);
        MainViewModel vm = Open(window);
        vm.WriteInPlace = false;
        vm.Output = window.FilePath("out.fit");
        vm.Force = false;
        vm.IncludeOptical = false;

        await vm.Copy();

        (int code, _, _) = Console(console.Source, console.Dest, "-o", console.FilePath("out.fit"), "--no-optical");
        Assert.Equal(0, code);
        Assert.Equal(File.ReadAllBytes(console.FilePath("out.fit")), File.ReadAllBytes(window.FilePath("out.fit")));
        Assert.Equal(before, File.ReadAllBytes(window.Dest));
        Assert.Equal(11, vm.Cards.Count);
        Assert.Equal("Destinatia: 100 x 80", vm.DestFacts);
    }

    /// <summary>
    /// A destination that still holds an old plate solve (R-016): the preview shows the core's
    /// line naming the keys it would drop under the cards, the line the console prints after
    /// them; the copy shows it before the two closing lines, as the console prints them, and
    /// writes the console's bytes.
    /// </summary>
    [Fact]
    public async Task R016_ThePreviewAndTheCopyShowTheConsolesDropLine()
    {
        using WindowFixture window = new(destCards: FitsTestData.OldSolveCards());
        using WindowFixture console = new(destCards: FitsTestData.OldSolveCards());
        MainViewModel vm = Open(window);

        await vm.Preview();

        (_, string[] lines, _) = Console(window.Source, window.Dest, "--dry-run");
        Assert.Equal(lines[0], vm.Heading);
        Assert.Equal(lines[1..^1].Select(l => l[2..]), vm.Cards);
        Assert.Equal([lines[^1]], vm.Notes);
        Assert.StartsWith("Chei vechi de plate solve ale destinatiei care ar fi eliminate (6): ", vm.Notes[0], StringComparison.Ordinal);

        await vm.Copy();

        (int code, string[] copyLines, _) = Console(console.Source, console.Dest);
        Assert.Equal(0, code);
        Assert.Equal(File.ReadAllBytes(console.Dest), File.ReadAllBytes(window.Dest));
        Assert.Equal(copyLines.Select(l => l.Replace(console.Dir, window.Dir, StringComparison.Ordinal)), vm.Notes);
        Assert.Equal(3, vm.Notes.Count);
        Assert.StartsWith("Eliminate 6 chei vechi de plate solve ale destinatiei: ", vm.Notes[0], StringComparison.Ordinal);
    }

    /// <summary>
    /// A destination whose own plate solve is the mirror image of the source's (R-017): the copy
    /// is refused in the core's words, the console's stderr line, and writes nothing; with the
    /// option on, the preview goes through under the core's warning.
    /// </summary>
    [Fact]
    public async Task R017_AMirroredDestinationIsRefusedInTheCoresWords_AndTheOptionLetsItThrough()
    {
        using WindowFixture f = new(destCards: FitsTestData.MirroredSolveCards());
        byte[] before = File.ReadAllBytes(f.Dest);
        MainViewModel vm = Open(f);

        await vm.Copy();

        Assert.True(vm.HasError);
        Assert.Equal("Refuzat", vm.RefusedHeading);
        Assert.Equal(Console(f.Source, f.Dest).Stderr, vm.Error);
        Assert.False(vm.HasCards);
        Assert.Equal(before, File.ReadAllBytes(f.Dest));

        vm.Force = true;
        await vm.Preview();

        Assert.False(vm.HasError);
        Assert.Equal(["ATENTIE: plate solve-ul destinatiei e in oglinda fata de cel al sursei. Continui oricum (--force)."], vm.Warnings);
        Assert.Equal(before, File.ReadAllBytes(f.Dest));
    }

    /// <summary>A path typed with spaces around it names the same file: the core gets it trimmed, so the written file has no space in its name.</summary>
    [Fact]
    public async Task R032_ThePathsReachTheCoreWithoutTheirSurroundingWhitespace()
    {
        using WindowFixture f = new();
        MainViewModel vm = Open(f);
        string outFile = f.FilePath("out.fit");
        vm.WriteInPlace = false;
        vm.Output = "  " + outFile + " ";
        await vm.FactsRead;
        Assert.True(vm.CanCopy);

        await vm.Copy();

        Assert.False(vm.HasError);
        Assert.True(File.Exists(outFile));
        Assert.Equal("Copiate 15 chei in " + outFile + ".", vm.Notes[0]);
        Assert.Equal("dest.fit, out.fit, source.fit", string.Join(", ", Directory.GetFiles(f.Dir).Select(Path.GetFileName).Order()));
    }

    /// <summary>
    /// A path pasted from File Explorer's "Copy as path" comes in double quotes (R-030): the box
    /// still names the file, its facts show and the commands come on, and the core gets the path
    /// without the quotes, so the file written has none in its name (R-032).
    /// </summary>
    [Fact]
    public async Task R030_APathInDoubleQuotesNamesTheFile_AndReachesTheCoreWithoutThem()
    {
        using WindowFixture f = new();
        MainViewModel vm = new() { Source = "\"" + f.Source + "\"", Dest = " \"" + f.Dest + "\" " };
        await vm.FactsRead;

        Assert.True(vm.CanPreview);
        Assert.Equal("Sursa: 100 x 80, plate-solved", vm.SourceFacts);
        Assert.Equal("Destinatia: 100 x 80", vm.DestFacts);

        string outFile = f.FilePath("out.fit");
        vm.WriteInPlace = false;
        vm.Output = "\"" + outFile + "\"";
        await vm.Copy();

        Assert.False(vm.HasError);
        Assert.True(File.Exists(outFile));
        Assert.Equal("dest.fit, out.fit, source.fit", string.Join(", ", Directory.GetFiles(f.Dir).Select(Path.GetFileName).Order()));

        // one pair only, and only around the whole path: a quote left inside is not the file
        vm.Source = "\"\"" + f.Source + "\"\"";
        await vm.FactsRead;
        Assert.False(vm.CanPreview);
    }

    [Fact]
    public async Task R033_AMismatchIsRefusedInTheCoresWordsAndNothingIsWritten()
    {
        using WindowFixture f = new(destWidth: 200);
        byte[] before = File.ReadAllBytes(f.Dest);
        MainViewModel vm = Open(f);

        await vm.Copy();

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
    public async Task R033_AForcedMismatchCopiesAndShowsTheWarning()
    {
        using WindowFixture window = new(destWidth: 200);
        using WindowFixture console = new(destWidth: 200);
        MainViewModel vm = Open(window);
        vm.Force = true;

        await vm.Copy();

        (int code, string[] lines, _) = Console(console.Source, console.Dest, "--force");
        Assert.Equal(0, code);
        Assert.False(vm.HasError);
        Assert.True(vm.HasWarnings);
        Assert.Equal(lines[0], Assert.Single(vm.Warnings));
        Assert.Equal("ATENTIE: Dimensiuni diferite: sursa (100, 80) vs destinatie (200, 80). Continui oricum (--force).", vm.Warnings[0]);
        Assert.Equal(File.ReadAllBytes(console.Dest), File.ReadAllBytes(window.Dest));
    }

    [Fact]
    public async Task R033_ASourceWithoutCtype1WarnsAndTheRunGoesOn()
    {
        List<string> cards = FitsTestData.PlateSolvedCards().Where(c => !c.StartsWith("CTYPE1", StringComparison.Ordinal)).ToList();
        using WindowFixture f = new(sourceCards: cards);
        MainViewModel vm = Open(f);
        await vm.FactsRead;
        Assert.Equal("Sursa: 100 x 80, fara plate solve", vm.SourceFacts);

        await vm.Preview();

        Assert.Equal("ATENTIE: sursa nu pare plate-solved (lipseste CTYPE1).", Assert.Single(vm.Warnings));
        Assert.False(vm.HasError);
        Assert.Equal(14, vm.Cards.Count);
    }

    [Fact]
    public async Task R033_AnUnreadableSourceOrANoKeySourceIsRefusedInTheCoresWords()
    {
        using WindowFixture f = new();
        MainViewModel vm = Open(f);
        vm.Source = f.FilePath("missing.fit");

        await vm.Preview();
        Assert.True(vm.HasError);
        Assert.StartsWith("Nu pot citi sursa '", vm.Error, StringComparison.Ordinal);
        Assert.Equal(Console(f.FilePath("missing.fit"), f.Dest, "--dry-run").Stderr, vm.Error);

        // a source with no key to copy (the processed header's OBJECT is an optical key, so
        // those are left out): the heading of the preview is not shown. A copy of the
        // destination, since the destination itself as the source is R-019's refusal
        string unsolved = f.FilePath("unsolved.fit");
        File.Copy(f.Dest, unsolved);
        vm.Source = unsolved;
        vm.IncludeOptical = false;
        await vm.Preview();
        Assert.Equal("Nicio cheie relevanta gasita in sursa. Nimic de copiat.", vm.Error);
        Assert.False(vm.HasHeading);
        Assert.False(vm.HasCards);
    }

    /// <summary>
    /// The new file's box naming the source is the core's refusal, in the console's words, and
    /// the solved source keeps every byte (R-019, R-033; CR-001, where the window's copy
    /// overwrote the source).
    /// </summary>
    [Fact]
    public async Task R019_ANewFileThatIsTheSourceIsRefusedInTheConsolesWords()
    {
        using WindowFixture f = new();
        byte[] before = File.ReadAllBytes(f.Source);
        MainViewModel vm = Open(f);
        vm.WriteInPlace = false;
        vm.Output = f.Source;

        await vm.Copy();

        Assert.True(vm.HasError);
        Assert.False(vm.HasCards);
        Assert.Equal(before, File.ReadAllBytes(f.Source));
        (int code, _, string stderr) = Console(f.Source, f.Dest, "-o", f.Source);
        Assert.Equal(1, code);
        Assert.Equal(stderr, vm.Error);
        Assert.StartsWith("Fisierul nou '", vm.Error, StringComparison.Ordinal);
    }

    /// <summary>
    /// A source and a destination typed relative keep naming the files of the folder they were
    /// typed in, whatever the working folder does afterwards (R-051; CR-006 of the review of
    /// 2026-11-16, where each use looked the path up again): another night's folder, which holds
    /// a source without a plate solve and a wider destination under the same names, is neither
    /// read nor written.
    /// </summary>
    [Fact]
    public async Task R051_ARelativeSourceAndDestinationKeepTheFilesTheyNamedWhenTyped()
    {
        using WindowFixture night = new();
        List<string> unsolved = FitsTestData.PlateSolvedCards().Where(c => !c.StartsWith("CTYPE1", StringComparison.Ordinal)).ToList();
        using WindowFixture other = new(destWidth: 200, sourceCards: unsolved);
        byte[] otherSource = File.ReadAllBytes(other.Source);
        byte[] otherDest = File.ReadAllBytes(other.Dest);
        MainViewModel vm;
        using (new WorkingFolder(night.Dir))
        {
            vm = new() { Source = "source.fit", Dest = " \"dest.fit\" " };
            WorkingFolder.MoveTo(other.Dir);

            await vm.Preview();
            Assert.False(vm.HasError);
            Assert.False(vm.HasWarnings);
            Assert.Equal(15, vm.Cards.Count);

            await vm.Copy();
        }

        Assert.False(vm.HasError);
        Assert.Equal("Copiate 15 chei in " + night.Dest + ".", vm.Notes[0]);
        Assert.True(FitsHeader.ReadPrimary(night.Dest).Contains("CTYPE1"));
        Assert.Equal(otherSource, File.ReadAllBytes(other.Source));
        Assert.Equal(otherDest, File.ReadAllBytes(other.Dest));
    }

    /// <summary>
    /// A bare name in the new file's box is written beside the destination, not in whatever
    /// folder the window was started from, and the closing line names it whole (R-051; CR-006).
    /// </summary>
    [Fact]
    public async Task R051_ARelativeNewFileIsWrittenBesideTheDestination()
    {
        using WindowFixture f = new();
        using TempWorkspace elsewhere = new();
        byte[] before = File.ReadAllBytes(f.Dest);
        MainViewModel vm = new() { Source = f.Source, Dest = f.Dest, WriteInPlace = false };
        using (new WorkingFolder(elsewhere.Dir))
        {
            vm.Output = " out.fit ";
            await vm.Copy();
        }

        Assert.False(vm.HasError);
        Assert.Equal("Copiate 15 chei in " + f.FilePath("out.fit") + ".", vm.Notes[0]);
        Assert.True(File.Exists(f.FilePath("out.fit")));
        Assert.Equal(before, File.ReadAllBytes(f.Dest));
        Assert.Empty(Directory.GetFileSystemEntries(elsewhere.Dir));
    }

    /// <summary>
    /// The look at a box's file goes off the window's thread (R-052; CR-008 of the review of
    /// 2026-11-16, where each keystroke checked the file on it): with the look held, as on a share
    /// that does not answer, the box's setter comes back at once, the line is empty and the
    /// commands are off; the facts come when the look ends.
    /// </summary>
    [Fact]
    public async Task R052_TheWindowStaysLiveWhileAFileIsLookedAt()
    {
        using WindowFixture f = new();
        using ManualResetEventSlim release = new();
        MainViewModel vm = new() { Dest = f.Dest };
        await vm.FactsRead;
        vm.LookCore = HeldLook(release, f.Source);

        Stopwatch clock = Stopwatch.StartNew();
        vm.Source = f.Source;
        clock.Stop();

        Assert.True(clock.ElapsedMilliseconds < 2000, $"the box's setter took {clock.ElapsedMilliseconds} ms with its look held");
        Assert.Equal("", vm.SourceFacts);
        Assert.False(vm.CanPreview);

        release.Set();
        await vm.FactsRead;
        Assert.Equal("Sursa: 100 x 80, plate-solved", vm.SourceFacts);
        Assert.True(vm.CanPreview);
    }

    /// <summary>
    /// A run waits only for each box's latest look: one overtaken by a newer text, still in flight
    /// as on a share that does not answer, does not hold the run whose boxes no longer name its
    /// file (R-052; the review of the branch on 2026-11-18, where such a look held a Preview 3 s).
    /// </summary>
    [Fact]
    public async Task R052_ARunWaitsOnlyForEachBoxsLatestLook()
    {
        using WindowFixture f = new();
        using ManualResetEventSlim release = new();
        using ManualResetEventSlim started = new();
        string share = f.FilePath("on-a-share.fit");
        MainViewModel vm = new() { Source = f.Source, LookCore = HeldLook(release, share) };
        vm.Dest = share;
        vm.Dest = f.Dest;
        await WaitFor(() => vm.CanPreview, "the local file's look");
        vm.RunCore = request =>
        {
            started.Set();
            return WcsCopy.Run(request);
        };

        Task preview = vm.Preview();
        Assert.True(started.Wait(TimeSpan.FromSeconds(3)), "the run waited for a look its box no longer needs");

        release.Set();
        await preview;
        Assert.True(vm.HasCards);
        Assert.Equal("Destinatia: 100 x 80", vm.DestFacts);
    }

    /// <summary>
    /// A look that ends after its box's text changed again is dropped (R-052): the first path's
    /// look is held while a second path is typed, whose look ends first; when the first ends, at
    /// last, the line keeps the second path's facts.
    /// </summary>
    [Fact]
    public async Task R052_OnlyTheLatestTextsLookIsShown()
    {
        using WindowFixture f = new();
        using WindowFixture wider = new(destWidth: 200);
        using ManualResetEventSlim release = new();
        MainViewModel vm = new() { LookCore = HeldLook(release, f.Dest) };

        Stopwatch clock = Stopwatch.StartNew();
        vm.Dest = f.Dest;
        vm.Dest = wider.Dest;
        clock.Stop();
        Assert.True(clock.ElapsedMilliseconds < 2000, $"the two texts took {clock.ElapsedMilliseconds} ms with the first look held");

        await WaitFor(() => vm.DestFacts.Length > 0, "the second text's look");
        Assert.Equal("Destinatia: 200 x 80", vm.DestFacts);

        release.Set();
        await vm.FactsRead;
        Assert.Equal("Destinatia: 200 x 80", vm.DestFacts);
    }

    /// <summary>
    /// A Preview clicked while a look is in flight runs once the look ends (R-052), so that the
    /// run's lines in the log are its own (R-043): the look's read line comes before the run's
    /// start line, and the run reads each file once.
    /// </summary>
    [Fact]
    public async Task R052_ARunWaitsForTheLooksInFlight()
    {
        using WindowFixture f = new();
        using ManualResetEventSlim release = new();
        using ManualResetEventSlim started = new();
        MainViewModel vm = new() { Source = f.Source, Dest = f.Dest };
        await vm.FactsRead;
        vm.LookCore = HeldLook(release, f.Dest);
        vm.RunCore = request =>
        {
            started.Set();
            return WcsCopy.Run(request);
        };
        using LogCapture capture = LogCapture.Start();

        // the same file in another text: a new look, held
        vm.Dest = " " + f.Dest;
        Task preview = vm.Preview();
        Assert.False(started.Wait(TimeSpan.FromMilliseconds(300)), "the run started while a look was in flight");

        release.Set();
        await preview;
        Assert.True(started.IsSet);
        Assert.True(vm.HasCards);
        // the reader's read lines alone: at Trace it adds one line per card under each
        List<string> lines = capture.Events.Where(e => e.Logger is "WcsCopy" or "FitsHeader").Select(e => e.Message).ToList();
        int start = lines.FindIndex(m => m.StartsWith("copy start ", StringComparison.Ordinal));
        int done = lines.FindIndex(m => m.StartsWith("copy done ", StringComparison.Ordinal));
        string look = Assert.Single(lines[..start], m => m.StartsWith("read ", StringComparison.Ordinal));
        Assert.StartsWith("read " + f.Dest + " (", look, StringComparison.Ordinal);
        Assert.Equal(2, lines[start..done].Count(m => m.StartsWith("read ", StringComparison.Ordinal)));
    }

    /// <summary>
    /// A change of a box's text, of the in-place or new-file choice, or of either option empties
    /// the card, which no longer says what a run would do (R-053; CR-009 of the review of
    /// 2026-11-16, where it kept the last preview's cards under boxes naming other files).
    /// </summary>
    [Theory]
    [InlineData("source")]
    [InlineData("destination")]
    [InlineData("new file")]
    [InlineData("write a new file")]
    [InlineData("optical")]
    [InlineData("force")]
    public async Task R053_AChangeOfABoxOrAnOptionEmptiesTheCard(string change)
    {
        using WindowFixture f = new();
        MainViewModel vm = Open(f);
        vm.Output = f.FilePath("out.fit");
        await vm.Preview();
        Assert.True(vm.HasCard && vm.HasOutcome && vm.HasHeading && vm.HasCards);

        switch (change)
        {
            case "source":
                vm.Source = " " + f.Source;
                break;
            case "destination":
                vm.Dest = " " + f.Dest;
                break;
            case "new file":
                vm.Output = f.FilePath("other.fit");
                break;
            case "write a new file":
                vm.WritesNewFile = true;
                break;
            case "optical":
                vm.IncludeOptical = false;
                break;
            default:
                vm.Force = true;
                break;
        }

        Assert.False(vm.HasCard);
        Assert.False(vm.HasOutcome);
        Assert.False(vm.HasHeading);
        Assert.Empty(vm.Cards);
        Assert.False(vm.HasCards);
        Assert.False(vm.HasNotes);
        Assert.False(vm.HasError);
        Assert.False(vm.HasWarnings);
    }

    /// <summary>A value set to what it already is changes nothing a run would do, and keeps the card (R-053).</summary>
    [Fact]
    public async Task R053_AValueSetToWhatItIsKeepsTheCard()
    {
        using WindowFixture f = new();
        MainViewModel vm = Open(f);
        await vm.Preview();

        vm.Source = f.Source;
        vm.Dest = f.Dest;
        vm.WriteInPlace = true;
        vm.IncludeOptical = true;
        vm.Force = false;

        Assert.True(vm.HasOutcome);
        Assert.Equal(15, vm.Cards.Count);
    }

    /// <summary>
    /// While a run is in flight the card shows the run's in-progress line and nothing of the last
    /// outcome, in the run's language; the run's own outcome comes when it ends (R-053; CR-009,
    /// where a forced copy of a large stack still showed the refusal before it).
    /// </summary>
    [Fact]
    public async Task R053_DuringARunTheCardSaysTheRunIsInProgress()
    {
        using WindowFixture f = new(destWidth: 200);
        using ManualResetEventSlim release = new();
        MainViewModel vm = Open(f);
        await vm.Preview();
        Assert.True(vm.HasError);
        Assert.Equal("", vm.RunningLine);
        vm.Force = true;
        vm.RunCore = WindowCloseTests.HeldRun(release);

        Task copy = vm.Copy();
        Assert.True(vm.IsRunning);
        Assert.True(vm.HasCard);
        Assert.False(vm.HasOutcome);
        Assert.False(vm.HasError);
        Assert.Equal("Copiere in curs...", vm.RunningLine);
        using (Ui.Use(UiLanguage.English))
        {
            Assert.Equal("Copy in progress...", vm.RunningLine);
        }

        release.Set();
        await copy;
        Assert.Equal("", vm.RunningLine);
        Assert.True(vm.HasOutcome);
        Assert.True(vm.HasWarnings);
        Assert.StartsWith("Copiate 15 chei in ", vm.Notes[0], StringComparison.Ordinal);

        release.Reset();
        Task preview = vm.Preview();
        Assert.Equal("Previzualizare in curs...", vm.RunningLine);
        using (Ui.Use(UiLanguage.English))
        {
            Assert.Equal("Preview in progress...", vm.RunningLine);
        }

        release.Set();
        await preview;
        Assert.Equal("", vm.RunningLine);
        Assert.True(vm.HasHeading);
    }

    /// <summary>The source's cards without <c>CTYPE1</c>: a file Siril has not plate-solved yet.</summary>
    private static List<string> Unsolved()
        => FitsTestData.PlateSolvedCards().Where(c => !c.StartsWith("CTYPE1", StringComparison.Ordinal)).ToList();

    /// <summary>
    /// A Browse that gives back the path the box already holds looks at the file again (R-054;
    /// CR-010 of the review of 2026-11-16): a source pasted before Siril saved it, then saved and
    /// picked, comes on without an edit of the box; the destination's Browse the same.
    /// </summary>
    [Fact]
    public async Task R054_BrowsingThePathTheBoxHoldsLooksAgain()
    {
        using WindowFixture f = new();
        string source = f.FilePath("later.fit");
        string dest = f.FilePath("later-dest.fit");
        MainViewModel vm = new() { Source = source, Dest = dest };
        await vm.FactsRead;
        Assert.Equal("", vm.SourceFacts);
        Assert.False(vm.CanPreview);

        File.Copy(f.Source, source);
        File.Copy(f.Dest, dest);
        vm.PickSource(source);
        vm.PickDest(dest);
        await vm.FactsRead;

        Assert.Equal("Sursa: 100 x 80, plate-solved", vm.SourceFacts);
        Assert.Equal("Destinatia: 100 x 80", vm.DestFacts);
        Assert.True(vm.CanPreview);
    }

    /// <summary>
    /// A look again keeps the fact line and the commands as they are while it is in flight, then
    /// shows what it found (R-054): a source plate-solved since it was looked at.
    /// </summary>
    [Fact]
    public async Task R054_ALookAgainKeepsTheLineAndTheCommandsUntilItEnds()
    {
        using WindowFixture f = new(sourceCards: Unsolved());
        using ManualResetEventSlim release = new();
        MainViewModel vm = Open(f);
        await vm.FactsRead;
        File.WriteAllBytes(f.Source, FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards(), FitsTestData.DeterministicData(400)));
        vm.LookCore = HeldLook(release, f.Source);

        vm.LookAgain();
        Assert.Equal("Sursa: 100 x 80, fara plate solve", vm.SourceFacts);
        Assert.True(vm.CanPreview);

        release.Set();
        await vm.FactsRead;
        Assert.Equal("Sursa: 100 x 80, plate-solved", vm.SourceFacts);
        Assert.True(vm.CanPreview);
    }

    /// <summary>
    /// After a Preview the files are looked at again, so the line under the source says what the
    /// run found: a source plate-solved in Siril while the window stayed in front (R-054; CR-010,
    /// where the line kept saying `fara plate solve`).
    /// </summary>
    [Fact]
    public async Task R054_ARunLooksAgainWhenItEnds()
    {
        using WindowFixture f = new(sourceCards: Unsolved());
        MainViewModel vm = Open(f);
        await vm.FactsRead;
        Assert.Equal("Sursa: 100 x 80, fara plate solve", vm.SourceFacts);
        File.WriteAllBytes(f.Source, FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards(), FitsTestData.DeterministicData(400)));

        await vm.Preview();

        Assert.False(vm.HasWarnings);
        Assert.Equal("Sursa: 100 x 80, plate-solved", vm.SourceFacts);
    }

    /// <summary>
    /// A look again asked while a run is in flight, the window back in front during a copy, is
    /// left to the run's end, whose own look again covers it (R-054): started at once, its read
    /// lines would fall among the run's in the log (R-043).
    /// </summary>
    [Fact]
    public async Task R054_ALookAgainAskedDuringARunWaitsForTheRunsEnd()
    {
        using WindowFixture f = new();
        using ManualResetEventSlim release = new();
        using ManualResetEventSlim looked = new();
        MainViewModel vm = Open(f);
        await vm.FactsRead;
        vm.RunCore = WindowCloseTests.HeldRun(release);
        vm.LookCore = (path, source) =>
        {
            looked.Set();
            return MainViewModel.Look(path, source);
        };

        Task preview = vm.Preview();
        vm.LookAgain();
        Assert.False(looked.Wait(TimeSpan.FromMilliseconds(300)), "a look started while the run was in flight");

        release.Set();
        await preview;
        Assert.True(looked.IsSet);
        Assert.Equal("Sursa: 100 x 80, plate-solved", vm.SourceFacts);
    }

    [Fact]
    public async Task R036_TheFactsAndTheHeadingFollowTheLanguageOfTheRun()
    {
        using WindowFixture f = new(destWidth: 200);

        using (Ui.Use(UiLanguage.English))
        {
            MainViewModel vm = Open(f);
            await vm.FactsRead;
            Assert.Equal("Source: 100 x 80, plate-solved", vm.SourceFacts);
            Assert.Equal("Destination: 200 x 80", vm.DestFacts);
            Assert.Equal("Refused", vm.RefusedHeading);

            await vm.Copy();
            Assert.StartsWith("Different dimensions: source (100, 80) vs destination (200, 80).", vm.Error, StringComparison.Ordinal);

            vm.Source = f.Dest;
            await vm.FactsRead;
            Assert.Equal("Source: 200 x 80, not plate-solved", vm.SourceFacts);
            vm.Dest = f.Source;
            await vm.FactsRead;
            Assert.Equal("Destination: 100 x 80, already plate-solved", vm.DestFacts);
        }

        MainViewModel ro = Open(f);
        await ro.FactsRead;
        Assert.Equal("Sursa: 100 x 80, plate-solved", ro.SourceFacts);
        Assert.Equal("Refuzat", ro.RefusedHeading);
    }
}
