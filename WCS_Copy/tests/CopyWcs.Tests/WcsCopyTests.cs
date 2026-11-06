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

namespace CopyWcs.Tests;

/// <summary>
/// The copy run of the core, apart from any printing: what kind of outcome each situation
/// gives, which lines it carries and in which order, and that a preview or a refusal writes
/// nothing. The console's acceptance tests (<see cref="CopyWcsCliTests"/>) check the same
/// situations through the printed text.
/// </summary>
public class WcsCopyTests
{
    private static WcsCopyRequest Request(string source, string dest, string? output = null,
        bool includeOptical = true, bool force = false, bool dryRun = false)
        => new(source, dest, output, includeOptical, force, dryRun);

    [Fact]
    public void R004_Run_SourceMissing_IsRefusedWithTheReadLineAndNoWarning()
    {
        using var ws = new TempWorkspace();
        var dest = ws.WriteFile("dest.fit", FitsTestData.MinimalFitsFile(FitsTestData.ProcessedCards()));

        var outcome = WcsCopy.Run(Request(ws.FilePath("missing.fit"), dest));

        Assert.Equal(WcsCopyKind.Refused, outcome.Kind);
        Assert.StartsWith("Nu pot citi sursa", outcome.Error, StringComparison.Ordinal);
        Assert.Empty(outcome.Warnings);
        Assert.Empty(outcome.Cards);
        Assert.Null(outcome.Target);
        Assert.Empty(outcome.Notes);
    }

    [Fact]
    public void R005_Run_DimensionMismatchWithoutForce_IsRefusedAndWritesNothing()
    {
        using var ws = new TempWorkspace();
        var source = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards(100, 80)));
        var destBytes = FitsTestData.MinimalFitsFile(FitsTestData.ProcessedCards(200, 80));
        var dest = ws.WriteFile("dest.fit", destBytes);

        var outcome = WcsCopy.Run(Request(source, dest));

        Assert.Equal(WcsCopyKind.Refused, outcome.Kind);
        Assert.Contains("Dimensiuni diferite", outcome.Error, StringComparison.Ordinal);
        Assert.Contains("(100, 80)", outcome.Error, StringComparison.Ordinal);
        Assert.Contains("(200, 80)", outcome.Error, StringComparison.Ordinal);
        Assert.Equal(destBytes, File.ReadAllBytes(dest));
    }

    [Fact]
    public void R005_Run_DimensionMismatchWithForce_CopiesWithOneWarning()
    {
        using var ws = new TempWorkspace();
        var source = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards(100, 80)));
        var dest = ws.WriteFile("dest.fit", FitsTestData.MinimalFitsFile(FitsTestData.ProcessedCards(200, 80)));

        var outcome = WcsCopy.Run(Request(source, dest, force: true));

        Assert.Equal(WcsCopyKind.Copied, outcome.Kind);
        var warning = Assert.Single(outcome.Warnings);
        Assert.StartsWith("ATENTIE: Dimensiuni diferite", warning, StringComparison.Ordinal);
        Assert.Equal(dest, outcome.Target);
        Assert.True(FitsHeader.ReadPrimary(dest).Contains("CTYPE1"));
    }

    [Fact]
    public void R006_Run_SourceWithoutCtype1_WarnsBeforeTheOutcome()
    {
        using var ws = new TempWorkspace();
        var cards = FitsTestData.PlateSolvedCards();
        cards.RemoveAll(c => c.StartsWith("CTYPE1", StringComparison.Ordinal));
        var source = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(cards));
        var dest = ws.WriteFile("dest.fit", FitsTestData.MinimalFitsFile(FitsTestData.ProcessedCards()));

        var outcome = WcsCopy.Run(Request(source, dest, dryRun: true));

        Assert.Equal(WcsCopyKind.Previewed, outcome.Kind);
        var warning = Assert.Single(outcome.Warnings);
        Assert.Contains("nu pare plate-solved", warning, StringComparison.Ordinal);
    }

    [Fact]
    public void R009_Run_DryRun_ListsTheRawCardsAndWritesNothing()
    {
        using var ws = new TempWorkspace();
        var source = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards()));
        var destBytes = FitsTestData.MinimalFitsFile(FitsTestData.ProcessedCards());
        var dest = ws.WriteFile("dest.fit", destBytes);
        var expected = FitsHeader.ReadPrimary(source);

        var outcome = WcsCopy.Run(Request(source, dest, dryRun: true));

        Assert.Equal(WcsCopyKind.Previewed, outcome.Kind);
        Assert.Null(outcome.Error);
        Assert.Null(outcome.Target);
        Assert.NotEmpty(outcome.Cards);
        Assert.All(outcome.Cards, c => Assert.Equal(80, c.Length));
        Assert.Contains(expected.GetCard("CTYPE1"), outcome.Cards);
        var note = Assert.Single(outcome.Notes);
        Assert.Equal($"Chei care ar fi copiate ({outcome.Cards.Count}):", note);
        Assert.Equal(destBytes, File.ReadAllBytes(dest));
    }

    [Fact]
    public void R007_Run_NoOptical_LeavesTheOpticalCardsOut()
    {
        using var ws = new TempWorkspace();
        var source = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards()));
        var dest = ws.WriteFile("dest.fit", FitsTestData.MinimalFitsFile(FitsTestData.ProcessedCards()));

        var all = WcsCopy.Run(Request(source, dest, dryRun: true));
        var wcsOnly = WcsCopy.Run(Request(source, dest, includeOptical: false, dryRun: true));

        Assert.Contains(all.Cards, c => c.StartsWith("FOCALLEN", StringComparison.Ordinal));
        Assert.DoesNotContain(wcsOnly.Cards, c => c.StartsWith("FOCALLEN", StringComparison.Ordinal));
        Assert.Contains(wcsOnly.Cards, c => c.StartsWith("CTYPE1", StringComparison.Ordinal));
    }

    [Fact]
    public void R008_Run_NoRelevantKey_IsRefusedAndWritesNothing()
    {
        using var ws = new TempWorkspace();
        var source = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(FitsTestData.ProcessedCards()));
        var destBytes = FitsTestData.MinimalFitsFile(FitsTestData.ProcessedCards());
        var dest = ws.WriteFile("dest.fit", destBytes);

        var outcome = WcsCopy.Run(Request(source, dest, includeOptical: false));

        Assert.Equal(WcsCopyKind.Refused, outcome.Kind);
        Assert.StartsWith("Nicio cheie relevanta", outcome.Error, StringComparison.Ordinal);
        Assert.Equal(destBytes, File.ReadAllBytes(dest));
    }

    [Fact]
    public void R012_Run_Copy_WritesTheTargetAndEndsWithTheCountAndTheReminder()
    {
        using var ws = new TempWorkspace();
        var data = FitsTestData.DeterministicData(400);
        var source = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards(), data));
        var dest = ws.WriteFile("dest.fit", FitsTestData.MinimalFitsFile(FitsTestData.ProcessedCards(), data));
        var output = ws.FilePath("out.fit");

        var outcome = WcsCopy.Run(Request(source, dest, output));

        Assert.Equal(WcsCopyKind.Copied, outcome.Kind);
        Assert.Null(outcome.Error);
        Assert.Empty(outcome.Warnings);
        Assert.Equal(output, outcome.Target);
        Assert.Equal(2, outcome.Notes.Count);
        Assert.Equal($"Copiate {outcome.Cards.Count} chei in {output}.", outcome.Notes[0]);
        Assert.Contains("Verifica vizual", outcome.Notes[1], StringComparison.Ordinal);
        var written = FitsHeader.ReadPrimary(output);
        Assert.True(written.Contains("HISTORY"));
        Assert.Equal(data, File.ReadAllBytes(output)[^data.Length..]);
    }

    [Fact]
    public void R014_Run_WriteFailure_IsRefusedWithTheWriteLine()
    {
        using var ws = new TempWorkspace();
        var source = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards()));
        var dest = ws.WriteFile("dest.fit", FitsTestData.MinimalFitsFile(FitsTestData.ProcessedCards()));
        var output = Path.Combine(ws.FilePath("no-such-folder"), "out.fit");

        var outcome = WcsCopy.Run(Request(source, dest, output));

        Assert.Equal(WcsCopyKind.Refused, outcome.Kind);
        Assert.StartsWith("Eroare la scriere", outcome.Error, StringComparison.Ordinal);
        Assert.False(File.Exists(output));
    }

    [Fact]
    public void R016_Run_Copy_DropsTheDestinationsOldPlateSolveCards_AndNamesThem()
    {
        using var ws = new TempWorkspace();
        var data = FitsTestData.DeterministicData(400);
        var source = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards(), data));
        var dest = ws.WriteFile("dest.fit", FitsTestData.MinimalFitsFile(FitsTestData.OldSolveCards(), data));

        var outcome = WcsCopy.Run(Request(source, dest));

        Assert.Equal(WcsCopyKind.Copied, outcome.Kind);
        Assert.Equal("Eliminate 6 chei vechi de plate solve ale destinatiei: CD1_1, CD1_2, CD2_1, CD2_2, CROTA2, A_3_0.", outcome.Dropped);
        Assert.Equal(2, outcome.Notes.Count);
        var written = FitsHeader.ReadPrimary(dest);
        foreach (var key in FitsTestData.OldSolveKeysToDrop)
        {
            Assert.False(written.Contains(key), key);
        }

        // An optical key the source lacks stays; a key both have takes the source's card.
        Assert.True(written.Contains("SITELAT"));
        Assert.Equal(FitsHeader.ReadPrimary(source).GetCard("CRVAL1"), written.GetCard("CRVAL1"));
        Assert.Equal(FitsHeader.ReadPrimary(source).GetCard("A_0_2"), written.GetCard("A_0_2"));
        Assert.Equal(data, File.ReadAllBytes(dest)[^data.Length..]);
    }

    [Fact]
    public void R016_Run_DryRun_NamesTheKeysItWouldDropAndWritesNothing()
    {
        using var ws = new TempWorkspace();
        var source = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards()));
        var destBytes = FitsTestData.MinimalFitsFile(FitsTestData.OldSolveCards());
        var dest = ws.WriteFile("dest.fit", destBytes);

        var outcome = WcsCopy.Run(Request(source, dest, dryRun: true));

        Assert.Equal(WcsCopyKind.Previewed, outcome.Kind);
        Assert.Equal("Chei vechi de plate solve ale destinatiei care ar fi eliminate (6): CD1_1, CD1_2, CD2_1, CD2_2, CROTA2, A_3_0", outcome.Dropped);
        Assert.Equal(["Chei care ar fi copiate (15):"], outcome.Notes);
        Assert.Equal(destBytes, File.ReadAllBytes(dest));
    }

    [Fact]
    public void R016_Run_IntoANewFile_LeavesTheOldCardsOutAndTheDestinationAsItWas()
    {
        using var ws = new TempWorkspace();
        var source = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards()));
        var destBytes = FitsTestData.MinimalFitsFile(FitsTestData.OldSolveCards());
        var dest = ws.WriteFile("dest.fit", destBytes);
        var output = ws.FilePath("out.fit");

        var outcome = WcsCopy.Run(Request(source, dest, output));

        Assert.Equal(WcsCopyKind.Copied, outcome.Kind);
        Assert.NotNull(outcome.Dropped);
        var written = FitsHeader.ReadPrimary(output);
        Assert.All(FitsTestData.OldSolveKeysToDrop, key => Assert.False(written.Contains(key), key));
        Assert.Equal(destBytes, File.ReadAllBytes(dest));
    }

    [Fact]
    public void R016_Run_SourceWithoutCtype1_DropsNothing()
    {
        using var ws = new TempWorkspace();
        var cards = FitsTestData.PlateSolvedCards();
        cards.RemoveAll(c => c.StartsWith("CTYPE1", StringComparison.Ordinal));
        var source = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(cards));
        var dest = ws.WriteFile("dest.fit", FitsTestData.MinimalFitsFile(FitsTestData.OldSolveCards()));

        var outcome = WcsCopy.Run(Request(source, dest));

        Assert.Equal(WcsCopyKind.Copied, outcome.Kind);
        Assert.Single(outcome.Warnings);
        Assert.Null(outcome.Dropped);
        var written = FitsHeader.ReadPrimary(dest);
        Assert.All(FitsTestData.OldSolveKeysToDrop, key => Assert.True(written.Contains(key), key));
    }

    [Fact]
    public void R016_Run_NothingToDrop_HasNoLine()
    {
        using var ws = new TempWorkspace();
        var source = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards()));
        var dest = ws.WriteFile("dest.fit", FitsTestData.MinimalFitsFile(FitsTestData.ProcessedCards()));

        Assert.Null(WcsCopy.Run(Request(source, dest, dryRun: true)).Dropped);
        Assert.Null(WcsCopy.Run(Request(source, dest)).Dropped);
    }

    /// <summary>The one refusal that comes after the cards are dropped in memory: a write that fails names nothing and changes nothing.</summary>
    [Fact]
    public void R016_Run_AWriteError_DropsNothingAndHasNoLine()
    {
        using var ws = new TempWorkspace();
        var source = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards()));
        var destBytes = FitsTestData.MinimalFitsFile(FitsTestData.OldSolveCards());
        var dest = ws.WriteFile("dest.fit", destBytes);
        var output = Path.Combine(ws.FilePath("no-such-folder"), "out.fit");

        var outcome = WcsCopy.Run(Request(source, dest, output));

        Assert.Equal(WcsCopyKind.Refused, outcome.Kind);
        Assert.StartsWith("Eroare la scriere", outcome.Error, StringComparison.Ordinal);
        Assert.Null(outcome.Dropped);
        Assert.Equal(destBytes, File.ReadAllBytes(dest));
    }

    [Fact]
    public void R016_Run_DryRun_SourceWithoutCtype1_NamesNothing()
    {
        using var ws = new TempWorkspace();
        var cards = FitsTestData.PlateSolvedCards();
        cards.RemoveAll(c => c.StartsWith("CTYPE1", StringComparison.Ordinal));
        var source = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(cards));
        var dest = ws.WriteFile("dest.fit", FitsTestData.MinimalFitsFile(FitsTestData.OldSolveCards()));

        var outcome = WcsCopy.Run(Request(source, dest, dryRun: true));

        Assert.Equal(WcsCopyKind.Previewed, outcome.Kind);
        Assert.Null(outcome.Dropped);
    }

    /// <summary>A keyword the source lacks and the destination holds twice: both cards go, and the line names the key once, in the singular.</summary>
    [Fact]
    public void R016_Run_AKeywordHeldTwice_DropsBothCards_AndNamesItOnceInTheSingular()
    {
        using var ws = new TempWorkspace();
        var source = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards()));
        List<string> destCards = FitsTestData.ProcessedCards();
        destCards.Add(FitsTestData.Card("CD1_1", "-0.000264"));
        destCards.Add(FitsTestData.Card("CD1_1", "-0.000265"));
        var dest = ws.WriteFile("dest.fit", FitsTestData.MinimalFitsFile(destCards));

        var outcome = WcsCopy.Run(Request(source, dest));

        Assert.Equal("Eliminata 1 cheie veche de plate solve a destinatiei: CD1_1.", outcome.Dropped);
        Assert.False(FitsHeader.ReadPrimary(dest).Contains("CD1_1"));
        using (Ui.Use(UiLanguage.English))
        {
            var english = WcsCopy.Run(Request(source, ws.WriteFile("dest2.fit", FitsTestData.MinimalFitsFile(destCards)), dryRun: true));
            Assert.Equal("Old plate-solve keys of the destination that would be dropped (1): CD1_1", english.Dropped);
            var copied = WcsCopy.Run(Request(source, ws.FilePath("dest2.fit")));
            Assert.Equal("Dropped 1 old plate-solve key of the destination: CD1_1.", copied.Dropped);
        }
    }

    /// <summary>From 20 keys on the Romanian count takes "de", unless its last two digits are 01 to 19.</summary>
    [Theory]
    [InlineData(19, "Eliminate 19 chei vechi")]
    [InlineData(20, "Eliminate 20 de chei vechi")]
    [InlineData(24, "Eliminate 24 de chei vechi")]
    public void R016_Run_TheRomanianCount_TakesDeFrom20(int keys, string start)
    {
        using var ws = new TempWorkspace();
        var source = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards()));
        List<string> destCards = FitsTestData.ProcessedCards();
        for (var i = 0; i < keys; i++)
        {
            destCards.Add(FitsTestData.Card($"A_{4 + (i / 5)}_{i % 5}", "1.0e-12"));
        }

        var dest = ws.WriteFile("dest.fit", FitsTestData.MinimalFitsFile(destCards));

        var outcome = WcsCopy.Run(Request(source, dest, ws.FilePath("out.fit")));

        Assert.StartsWith(start + " de plate solve ale destinatiei: A_4_0, A_4_1", outcome.Dropped, StringComparison.Ordinal);
    }

    /// <summary>A path that is empty or blank (an unset shell variable) is a refusal like any other unreadable file, not a crash.</summary>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void R004_BlankSourcePath_IsRefusedNotThrown(string source)
    {
        using var ws = new TempWorkspace();
        var dest = ws.WriteFile("dest.fit", FitsTestData.MinimalFitsFile(FitsTestData.ProcessedCards()));

        var outcome = WcsCopy.Run(Request(source, dest));

        Assert.Equal(WcsCopyKind.Refused, outcome.Kind);
        Assert.StartsWith($"Nu pot citi sursa '{source}'", outcome.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void R004_BlankDestinationPath_IsRefusedNotThrown()
    {
        using var ws = new TempWorkspace();
        var source = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards()));

        var outcome = WcsCopy.Run(Request(source, ""));

        Assert.Equal(WcsCopyKind.Refused, outcome.Kind);
        Assert.StartsWith("Nu pot citi destinatia ''", outcome.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void R014_BlankOutputPath_IsRefusedWithTheWriteLine()
    {
        using var ws = new TempWorkspace();
        var source = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards()));
        var destBytes = FitsTestData.MinimalFitsFile(FitsTestData.ProcessedCards());
        var dest = ws.WriteFile("dest.fit", destBytes);

        var outcome = WcsCopy.Run(Request(source, dest, ""));

        Assert.Equal(WcsCopyKind.Refused, outcome.Kind);
        Assert.StartsWith("Eroare la scriere", outcome.Error, StringComparison.Ordinal);
        Assert.Equal(destBytes, File.ReadAllBytes(dest));
    }
}
