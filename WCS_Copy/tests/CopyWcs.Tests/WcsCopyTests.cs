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

using System.Runtime.Versioning;
using System.Text.RegularExpressions;

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

    /// <summary>
    /// A write that fails names the file asked for, never its temporary file, whose name the user
    /// never typed: a new file in a folder that does not exist reads as it did before every write
    /// went through a temporary file, and the log keeps the system's own message under it (R-014;
    /// the review of 2026-11-18: the line named <c>out.fit.copywcs-3f9a0c1e.tmp</c>).
    /// </summary>
    [Fact]
    public void R014_Run_AWriteThatFails_NamesTheNewFile_NotItsTemporaryFile()
    {
        using var ws = new TempWorkspace();
        var source = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards()));
        var dest = ws.WriteFile("dest.fit", FitsTestData.MinimalFitsFile(FitsTestData.ProcessedCards()));
        var output = Path.Combine(ws.FilePath("no-such-folder"), "out.fit");
        using var log = LogCapture.Start();

        WcsCopyOutcome outcome;
        using (Ui.Use(UiLanguage.English))
        {
            outcome = WcsCopy.Run(Request(source, dest, output));
        }

        Assert.Equal(WcsCopyKind.Refused, outcome.Kind);
        Assert.StartsWith("Write error: ", outcome.Error, StringComparison.Ordinal);
        Assert.Contains($"'{output}'", outcome.Error, StringComparison.Ordinal);
        Assert.DoesNotContain(".copywcs-", outcome.Error, StringComparison.Ordinal);
        Assert.Contains("out.fit.copywcs-", log.Text, StringComparison.Ordinal);
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

    /// <summary>The count of the copy's closing line follows each language's grammar (R-012, ADR 0013): one key in the singular, and in Romanian "de" from 20 keys on unless the last two digits are 01 to 19.</summary>
    [Theory]
    [InlineData(1, "Copied 1 key into ", "Copiata 1 cheie in ")]
    [InlineData(19, "Copied 19 keys into ", "Copiate 19 chei in ")]
    [InlineData(20, "Copied 20 keys into ", "Copiate 20 de chei in ")]
    [InlineData(72, "Copied 72 keys into ", "Copiate 72 de chei in ")]
    [InlineData(101, "Copied 101 keys into ", "Copiate 101 chei in ")]
    [InlineData(120, "Copied 120 keys into ", "Copiate 120 de chei in ")]
    public void R012_Run_TheCopysCount_FollowsEachLanguagesGrammar(int keys, string english, string romanian)
    {
        using var ws = new TempWorkspace();
        List<string> cards = FitsTestData.ProcessedCards();
        cards.Add(FitsTestData.Card("CTYPE1", "'RA---TAN'"));
        for (var i = 1; i < keys; i++)
        {
            cards.Add(FitsTestData.Card($"A_{4 + (i / 5)}_{i % 5}", "1.0e-12"));
        }

        var source = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(cards));
        var dest = ws.WriteFile("dest.fit", FitsTestData.MinimalFitsFile(FitsTestData.ProcessedCards()));

        var ro = WcsCopy.Run(Request(source, dest, ws.FilePath("ro.fit"), includeOptical: false));
        WcsCopyOutcome en;
        using (Ui.Use(UiLanguage.English))
        {
            en = WcsCopy.Run(Request(source, dest, ws.FilePath("en.fit"), includeOptical: false));
        }

        Assert.Equal(keys, ro.Cards.Count);
        Assert.StartsWith(romanian, ro.Notes[0], StringComparison.Ordinal);
        Assert.StartsWith(english, en.Notes[0], StringComparison.Ordinal);
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
        Assert.False(File.Exists(output));
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
    [InlineData(100, "Eliminate 100 de chei vechi")]
    [InlineData(101, "Eliminate 101 chei vechi")]
    [InlineData(119, "Eliminate 119 chei vechi")]
    [InlineData(120, "Eliminate 120 de chei vechi")]
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

    [Fact]
    public void R017_Run_AMirroredDestination_IsRefusedWithoutForce_ADryRunToo_AndNothingIsWritten()
    {
        using var ws = new TempWorkspace();
        var source = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards()));
        var destBytes = FitsTestData.MinimalFitsFile(FitsTestData.MirroredSolveCards());
        var dest = ws.WriteFile("dest.fit", destBytes);

        foreach (var dryRun in new[] { false, true })
        {
            var outcome = WcsCopy.Run(Request(source, dest, dryRun: dryRun));

            Assert.Equal(WcsCopyKind.Refused, outcome.Kind);
            Assert.Equal("Plate solve-ul destinatiei e in oglinda fata de cel al sursei: imaginea a fost oglindita/flip-uita "
                         + "dupa ce sursa a fost rezolvata, asa ca WCS-ul copiat ar fi gresit. Intoarce intai imaginea inapoi "
                         + "sau pastreaza-i plate solve-ul. Foloseste --force daca stii ce faci.", outcome.Error);
            Assert.Empty(outcome.Cards);
            Assert.Null(outcome.Dropped);
            Assert.Equal(destBytes, File.ReadAllBytes(dest));
        }
    }

    [Fact]
    public void R017_Run_AMirroredDestination_WithForce_WarnsAndCopies()
    {
        using var ws = new TempWorkspace();
        var source = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards()));
        var dest = ws.WriteFile("dest.fit", FitsTestData.MinimalFitsFile(FitsTestData.MirroredSolveCards()));

        var outcome = WcsCopy.Run(Request(source, dest, force: true));

        Assert.Equal(WcsCopyKind.Copied, outcome.Kind);
        var warning = Assert.Single(outcome.Warnings);
        Assert.Equal("ATENTIE: plate solve-ul destinatiei e in oglinda fata de cel al sursei. Continui oricum (--force).", warning);
        Assert.False(FitsHeader.ReadPrimary(dest).Contains("CD2_2"));
        using (Ui.Use(UiLanguage.English))
        {
            var english = WcsCopy.Run(Request(source, ws.WriteFile("dest2.fit", FitsTestData.MinimalFitsFile(FitsTestData.MirroredSolveCards())), dryRun: true));
            Assert.Equal(WcsCopyKind.Refused, english.Kind);
            Assert.StartsWith("The destination's own plate solve is a mirror image of the source's: ", english.Error, StringComparison.Ordinal);
            var forced = WcsCopy.Run(Request(source, ws.FilePath("dest2.fit"), force: true, dryRun: true));
            Assert.Equal("WARNING: the destination's own plate solve is a mirror image of the source's. Continuing anyway (--force).", Assert.Single(forced.Warnings));
        }
    }

    [Fact]
    public void R017_Run_ARefusedCopyIntoANewFile_WritesNoFile()
    {
        using var ws = new TempWorkspace();
        var source = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards()));
        var dest = ws.WriteFile("dest.fit", FitsTestData.MinimalFitsFile(FitsTestData.MirroredSolveCards()));
        var output = ws.FilePath("out.fit");

        Assert.Equal(WcsCopyKind.Refused, WcsCopy.Run(Request(source, dest, output)).Kind);
        Assert.False(File.Exists(output));
    }

    [Fact]
    public void R017_Run_AMirrorAndASizeMismatch_UnderForce_WarnTheSizeFirst()
    {
        using var ws = new TempWorkspace();
        var source = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards(100, 80)));
        var dest = ws.WriteFile("dest.fit", FitsTestData.MinimalFitsFile(FitsTestData.MirroredSolveCards(200, 80)));

        var outcome = WcsCopy.Run(Request(source, dest, ws.FilePath("out.fit"), force: true));

        Assert.Equal(WcsCopyKind.Copied, outcome.Kind);
        Assert.Equal(2, outcome.Warnings.Count);
        Assert.StartsWith("ATENTIE: Dimensiuni diferite", outcome.Warnings[0], StringComparison.Ordinal);
        Assert.StartsWith("ATENTIE: plate solve-ul destinatiei e in oglinda", outcome.Warnings[1], StringComparison.Ordinal);
    }

    /// <summary>The same plate solve written DEC first has the opposite determinant without being a mirror: it is not compared, so it is not refused (R-017).</summary>
    [Fact]
    public void R017_Run_ADestinationSolvedDecFirst_IsNotRefused()
    {
        using var ws = new TempWorkspace();
        var source = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards()));
        List<string> decFirst = FitsTestData.MirroredSolveCards();
        decFirst[decFirst.FindIndex(c => c.StartsWith("CTYPE1", StringComparison.Ordinal))] = FitsTestData.Card("CTYPE1", "'DEC--TAN'");
        decFirst[decFirst.FindIndex(c => c.StartsWith("CTYPE2", StringComparison.Ordinal))] = FitsTestData.Card("CTYPE2", "'RA---TAN'");
        var dest = ws.WriteFile("dest.fit", FitsTestData.MinimalFitsFile(decFirst));

        var outcome = WcsCopy.Run(Request(source, dest, dryRun: true));

        Assert.Equal(WcsCopyKind.Previewed, outcome.Kind);
        Assert.Empty(outcome.Warnings);
    }

    /// <summary>The check needs both plate solves: a source without CTYPE1 only warns as R-006 says, and a destination of the same orientation goes through.</summary>
    [Fact]
    public void R017_Run_IsLeftOutWithoutTwoPlateSolves_AndPassesTheSameOrientation()
    {
        using var ws = new TempWorkspace();
        var cards = FitsTestData.PlateSolvedCards();
        cards.RemoveAll(c => c.StartsWith("CTYPE1", StringComparison.Ordinal));
        var unsolved = ws.WriteFile("unsolved.fit", FitsTestData.MinimalFitsFile(cards));
        var source = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards()));
        var mirrored = ws.WriteFile("mirrored.fit", FitsTestData.MinimalFitsFile(FitsTestData.MirroredSolveCards()));
        var sameWay = ws.WriteFile("same.fit", FitsTestData.MinimalFitsFile(FitsTestData.OldSolveCards()));

        var noSourceSolve = WcsCopy.Run(Request(unsolved, mirrored, dryRun: true));
        var sameOrientation = WcsCopy.Run(Request(source, sameWay, dryRun: true));

        Assert.Equal(WcsCopyKind.Previewed, noSourceSolve.Kind);
        Assert.Contains("nu pare plate-solved", Assert.Single(noSourceSolve.Warnings), StringComparison.Ordinal);
        Assert.Equal(WcsCopyKind.Previewed, sameOrientation.Kind);
        Assert.Empty(sameOrientation.Warnings);
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

    /// <summary>The names of the files in a folder, sorted, so a test sees every file a run left there.</summary>
    private static string[] FilesIn(string folder)
        => Directory.GetFiles(folder).Select(f => Path.GetFileName(f)).Order(StringComparer.Ordinal).ToArray();

    /// <summary>
    /// A new file that is the source, as given or in another letter case, is refused before
    /// anything is written, a dry run too, and the solved source keeps every byte (R-019; CR-001
    /// of the review of 2026-11-16: the copy overwrote the source and said it had copied).
    /// </summary>
    [Fact]
    public void R019_Run_ANewFileThatIsTheSource_IsRefused_ADryRunToo_AndTheSourceIsLeftAsItWas()
    {
        using var ws = new TempWorkspace();
        var sourceBytes = FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards(), FitsTestData.DeterministicData(400));
        var source = ws.WriteFile("Source.fit", sourceBytes);
        var destBytes = FitsTestData.MinimalFitsFile(FitsTestData.ProcessedCards(), FitsTestData.DeterministicData(400));
        var dest = ws.WriteFile("dest.fit", destBytes);

        foreach (var output in new[] { source, ws.FilePath("source.fit") })
        {
            foreach (var dryRun in new[] { false, true })
            {
                var outcome = WcsCopy.Run(Request(source, dest, output, dryRun: dryRun));

                Assert.Equal(WcsCopyKind.Refused, outcome.Kind);
                Assert.Equal($"Fisierul nou '{output}' este chiar sursa: scrierea lui ar suprascrie sursa plate-solved. "
                             + "Alege alt nume pentru fisierul nou.", outcome.Error);
                Assert.Empty(outcome.Warnings);
                Assert.Empty(outcome.Cards);
                Assert.Null(outcome.Dropped);
                Assert.Equal(sourceBytes, File.ReadAllBytes(source));
                Assert.Equal(destBytes, File.ReadAllBytes(dest));
                Assert.Equal(["Source.fit", "dest.fit"], FilesIn(ws.Dir));
            }
        }

        using (Ui.Use(UiLanguage.English))
        {
            Assert.Equal($"The new file '{source}' is the source itself: writing it would overwrite the plate-solved source. "
                         + "Choose another name for the new file.", WcsCopy.Run(Request(source, dest, source)).Error);
        }
    }

    /// <summary>
    /// The same file as the source and the destination, as given or in another letter case, is
    /// refused before anything is written, a dry run too (R-019; CR-013: the file was copied into
    /// itself and gained a HISTORY card saying it was copied from itself).
    /// </summary>
    [Fact]
    public void R019_Run_TheSameFileAsSourceAndDestination_IsRefusedAndWritesNothing()
    {
        using var ws = new TempWorkspace();
        var bytes = FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards(), FitsTestData.DeterministicData(400));
        var stack = ws.WriteFile("stack.fit", bytes);

        foreach (var dest in new[] { stack, ws.FilePath("STACK.FIT") })
        {
            foreach (var dryRun in new[] { false, true })
            {
                var outcome = WcsCopy.Run(Request(stack, dest, dryRun: dryRun));

                Assert.Equal(WcsCopyKind.Refused, outcome.Kind);
                Assert.Equal("Sursa si destinatia sunt acelasi fisier. Nimic de copiat.", outcome.Error);
                Assert.Empty(outcome.Warnings);
                Assert.Empty(outcome.Cards);
                Assert.Equal(bytes, File.ReadAllBytes(stack));
                Assert.Equal(["stack.fit"], FilesIn(ws.Dir));
            }
        }

        using (Ui.Use(UiLanguage.English))
        {
            Assert.Equal("The source and the destination are the same file. Nothing to copy.", WcsCopy.Run(Request(stack, stack)).Error);
        }
    }

    /// <summary>
    /// A new file that is the destination is refused under any spelling, the destination's own
    /// included, and the destination keeps every byte, a dry run too (R-019; CR-014: another letter
    /// case ended in a "used by another process" write error, and the same spelling rewrote the
    /// destination in place, though R-012 says a new file leaves it as it was).
    /// </summary>
    [Fact]
    public void R019_Run_ANewFileThatIsTheDestination_IsRefusedUnderAnySpelling()
    {
        using var ws = new TempWorkspace();
        var source = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards()));
        var destBytes = FitsTestData.MinimalFitsFile(FitsTestData.ProcessedCards(), FitsTestData.DeterministicData(400));
        var dest = ws.WriteFile("Stack.fit", destBytes);

        string[] spellings = [dest, ws.FilePath("stack.fit"), @"\\?\" + dest, Path.Combine(ws.Dir, "sub", "..", "Stack.fit")];
        foreach (var output in spellings)
        {
            foreach (var dryRun in new[] { false, true })
            {
                var outcome = WcsCopy.Run(Request(source, dest, output, dryRun: dryRun));

                Assert.Equal(WcsCopyKind.Refused, outcome.Kind);
                Assert.Equal($"Fisierul nou '{output}' este chiar destinatia. Scrie in destinatie sau alege alt nume pentru fisierul nou.",
                             outcome.Error);
                Assert.Equal(destBytes, File.ReadAllBytes(dest));
                Assert.Equal(["Stack.fit", "source.fit"], FilesIn(ws.Dir));
            }
        }

        using (Ui.Use(UiLanguage.English))
        {
            Assert.Equal($"The new file '{dest}' is the destination itself. Write into the destination instead, or choose another name "
                         + "for the new file.", WcsCopy.Run(Request(source, dest, dest)).Error);
        }
    }

    /// <summary>A new file that does not exist yet is none of the two files, in another folder's spelling too: the copy writes it (R-019).</summary>
    [Fact]
    public void R019_Run_ANewFileThatDoesNotExistYet_IsWritten()
    {
        using var ws = new TempWorkspace();
        var source = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards()));
        var dest = ws.WriteFile("dest.fit", FitsTestData.MinimalFitsFile(FitsTestData.ProcessedCards()));

        var outcome = WcsCopy.Run(Request(source, dest, Path.Combine(ws.Dir, "sub", "..", "source_wcs.fit")));

        Assert.Equal(WcsCopyKind.Copied, outcome.Kind);
        Assert.True(File.Exists(ws.FilePath("source_wcs.fit")));
    }

    /// <summary>
    /// The paths are told apart before the pixel grids are compared (R-019): a new file that is the
    /// source is refused with its own line though the destination's size differs too, so the one
    /// refusal --force cannot let through comes first.
    /// </summary>
    [Fact]
    public void R019_Run_ANewFileThatIsTheSource_IsRefusedBeforeTheDimensionsAreCompared()
    {
        using var ws = new TempWorkspace();
        var sourceBytes = FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards(100, 80));
        var source = ws.WriteFile("source.fit", sourceBytes);
        var dest = ws.WriteFile("dest.fit", FitsTestData.MinimalFitsFile(FitsTestData.ProcessedCards(200, 80)));

        var outcome = WcsCopy.Run(Request(source, dest, source));

        Assert.Equal(WcsCopyKind.Refused, outcome.Kind);
        Assert.StartsWith($"Fisierul nou '{source}' este chiar sursa", outcome.Error, StringComparison.Ordinal);
        Assert.Equal(sourceBytes, File.ReadAllBytes(source));
    }

    /// <summary>The cards without the one of a keyword: a header that lacks it.</summary>
    private static List<string> Without(List<string> cards, string keyword)
        => cards.Where(c => !c.StartsWith(keyword.PadRight(8), StringComparison.Ordinal)).ToList();

    /// <summary>
    /// A source with no image size in its primary header (R-060) is refused with its own line,
    /// not as the size -1, and the option to force does not let it through; a dry run is refused
    /// too, and nothing is written.
    /// </summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void R060_Run_ASourceWithNoImageSize_IsRefusedForceOrNot(bool force, bool dryRun)
    {
        using var ws = new TempWorkspace();
        var source = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(Without(FitsTestData.PlateSolvedCards(100, 80), "NAXIS1")));
        var destBytes = FitsTestData.MinimalFitsFile(FitsTestData.ProcessedCards(100, 80));
        var dest = ws.WriteFile("dest.fit", destBytes);

        var outcome = WcsCopy.Run(Request(source, dest, force: force, dryRun: dryRun));

        Assert.Equal(WcsCopyKind.Refused, outcome.Kind);
        Assert.Equal(
            "Sursa nu are dimensiunea imaginii in header-ul principal (lipseste NAXIS1 sau NAXIS2): imaginea poate fi intr-o extensie, pe care unealta nu o citeste.",
            outcome.Error);
        Assert.Empty(outcome.Warnings);
        Assert.Empty(outcome.Cards);
        Assert.Equal(destBytes, File.ReadAllBytes(dest));
    }

    /// <summary>A destination with no NAXIS2 (R-060) is refused with the destination's line, in English too.</summary>
    [Fact]
    public void R060_Run_ADestinationWithNoImageSize_IsRefusedWithItsLine()
    {
        using var ws = new TempWorkspace();
        using IDisposable _ = Ui.Use(UiLanguage.English);
        var source = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards(100, 80)));
        var destBytes = FitsTestData.MinimalFitsFile(Without(FitsTestData.ProcessedCards(100, 80), "NAXIS2"));
        var dest = ws.WriteFile("dest.fit", destBytes);

        var outcome = WcsCopy.Run(Request(source, dest, force: true));

        Assert.Equal(WcsCopyKind.Refused, outcome.Kind);
        Assert.Equal(
            "The destination has no image size in its main header (NAXIS1 or NAXIS2 is missing): its image may be in an extension, which this tool does not read.",
            outcome.Error);
        Assert.Equal(destBytes, File.ReadAllBytes(dest));
    }

    /// <summary>
    /// Two files that both lack the size (R-060) are refused with the source's line, where they
    /// passed as equal sizes and were copied.
    /// </summary>
    [Fact]
    public void R060_Run_TwoFilesWithNoImageSize_AreRefusedWithTheSourcesLine()
    {
        using var ws = new TempWorkspace();
        var source = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(Without(FitsTestData.PlateSolvedCards(100, 80), "NAXIS1")));
        var destBytes = FitsTestData.MinimalFitsFile(Without(FitsTestData.ProcessedCards(100, 80), "NAXIS1"));
        var dest = ws.WriteFile("dest.fit", destBytes);

        var outcome = WcsCopy.Run(Request(source, dest));

        Assert.Equal(WcsCopyKind.Refused, outcome.Kind);
        Assert.StartsWith("Sursa nu are dimensiunea imaginii", outcome.Error, StringComparison.Ordinal);
        Assert.Equal(destBytes, File.ReadAllBytes(dest));
    }

    /// <summary>The data area of the destinations the midway failures use: larger than the 80 KB a copy streams at a time.</summary>
    private const int LargeData = 200_000;

    /// <summary>
    /// Makes a destination's data unreadable from its middle on, as a disk that fills or drops
    /// does to a write: a byte-range lock, which Windows enforces on every other handle, so the
    /// copy's read fails after the header is written and its first 80 KB of data copied. The
    /// header blocks stay readable, so both headers are read as usual.
    /// </summary>
    /// <param name="path">The destination; its data starts at byte 2880.</param>
    /// <returns>The handle holding the lock; disposing it releases the lock.</returns>
    [SupportedOSPlatform("windows")]
    private static FileStream LockTheMiddleOfTheData(string path)
    {
        var hold = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        hold.Lock(2880 + 150_000, 1000);
        return hold;
    }

    /// <summary>
    /// A new file whose write fails midway is not left behind, cut short (R-014; CR-003 of the
    /// review of 2026-11-16: the header and the first part of the data stayed as the new file).
    /// </summary>
    [Fact]
    [SupportedOSPlatform("windows")]
    public void R014_Run_AWriteThatFailsMidway_LeavesNoNewFile()
    {
        using var ws = new TempWorkspace();
        var source = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards()));
        var dest = ws.WriteFile("dest.fit", FitsTestData.MinimalFitsFile(FitsTestData.ProcessedCards(), FitsTestData.DeterministicData(LargeData)));
        var output = ws.FilePath("out.fit");
        using var hold = LockTheMiddleOfTheData(dest);

        var outcome = WcsCopy.Run(Request(source, dest, output));

        Assert.Equal(WcsCopyKind.Refused, outcome.Kind);
        Assert.StartsWith("Eroare la scriere", outcome.Error, StringComparison.Ordinal);
        Assert.Equal(["dest.fit", "source.fit"], FilesIn(ws.Dir));
    }

    /// <summary>
    /// A file already at the new file's name keeps every byte when the write over it fails midway
    /// (R-014; CR-003: the file was emptied when the write began, so a good earlier copy was lost).
    /// </summary>
    [Fact]
    [SupportedOSPlatform("windows")]
    public void R014_Run_AWriteThatFailsMidway_LeavesAnExistingNewFileAsItWas()
    {
        using var ws = new TempWorkspace();
        var source = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards()));
        var dest = ws.WriteFile("dest.fit", FitsTestData.MinimalFitsFile(FitsTestData.ProcessedCards(), FitsTestData.DeterministicData(LargeData)));
        var earlierBytes = FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards(), FitsTestData.DeterministicData(LargeData));
        var output = ws.WriteFile("out.fit", earlierBytes);
        using var hold = LockTheMiddleOfTheData(dest);

        var outcome = WcsCopy.Run(Request(source, dest, output));

        Assert.Equal(WcsCopyKind.Refused, outcome.Kind);
        Assert.Equal(earlierBytes, File.ReadAllBytes(output));
        Assert.Equal(["dest.fit", "out.fit", "source.fit"], FilesIn(ws.Dir));
    }

    /// <summary>
    /// An in-place write that cannot replace the destination (read-only) leaves the destination as
    /// it was and no temporary file beside it (R-014; CR-016: a `.tmp` as big as the image stayed).
    /// </summary>
    [Fact]
    public void R014_Run_AFailedInPlaceWrite_LeavesNoTemporaryFile()
    {
        using var ws = new TempWorkspace();
        var source = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards()));
        var destBytes = FitsTestData.MinimalFitsFile(FitsTestData.ProcessedCards(), FitsTestData.DeterministicData(400));
        var dest = ws.WriteFile("dest.fit", destBytes);
        File.SetAttributes(dest, FileAttributes.ReadOnly);
        try
        {
            var outcome = WcsCopy.Run(Request(source, dest));

            Assert.Equal(WcsCopyKind.Refused, outcome.Kind);
            Assert.StartsWith("Eroare la scriere", outcome.Error, StringComparison.Ordinal);
            Assert.Equal(destBytes, File.ReadAllBytes(dest));
            Assert.Equal(["dest.fit", "source.fit"], FilesIn(ws.Dir));
        }
        finally
        {
            File.SetAttributes(dest, FileAttributes.Normal);
        }
    }

    /// <summary>
    /// A file of the user's named as the temporary file used to be, `dest.fit.tmp`, is left alone
    /// by an in-place copy, which writes through a name of its own (R-012; part of CR-018: that
    /// file was overwritten and moved over the destination).
    /// </summary>
    [Fact]
    public void R012_Run_AFileNamedLikeTheOldTemporaryFile_IsLeftAlone()
    {
        using var ws = new TempWorkspace();
        var source = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards()));
        var dest = ws.WriteFile("dest.fit", FitsTestData.MinimalFitsFile(FitsTestData.ProcessedCards(), FitsTestData.DeterministicData(400)));
        byte[] owners = [1, 2, 3, 4];
        var tmp = ws.WriteFile("dest.fit.tmp", owners);

        var outcome = WcsCopy.Run(Request(source, dest));

        Assert.Equal(WcsCopyKind.Copied, outcome.Kind);
        Assert.Equal(owners, File.ReadAllBytes(tmp));
        Assert.True(FitsHeader.ReadPrimary(dest).Contains("CTYPE1"));
        Assert.Equal(["dest.fit", "dest.fit.tmp", "source.fit"], FilesIn(ws.Dir));
    }

    /// <summary>
    /// A name as long as the file system allows, 255 characters on NTFS, is written, as a new file
    /// and in place: the temporary file's name is shortened to fit beside it (R-012; the second
    /// review of 2026-11-18: its 21-character suffix made every name over 234 characters a write
    /// error, though <c>-o</c> wrote such names before every write went through a temporary file).
    /// </summary>
    [Fact]
    public void R012_Run_ANameAsLongAsTheFileSystemAllows_IsWritten_AsANewFileAndInPlace()
    {
        using var ws = new TempWorkspace();
        var source = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards()));
        var data = FitsTestData.DeterministicData(400);
        string destName = new string('n', 251) + ".fit";
        string outputName = new string('o', 251) + ".fit";
        var dest = ws.WriteFile(destName, FitsTestData.MinimalFitsFile(FitsTestData.ProcessedCards(), data));
        var output = ws.FilePath(outputName);

        var copy = WcsCopy.Run(Request(source, dest, output));
        var inPlace = WcsCopy.Run(Request(source, dest));

        Assert.Equal(WcsCopyKind.Copied, copy.Kind);
        Assert.Equal(WcsCopyKind.Copied, inPlace.Kind);
        Assert.True(FitsHeader.ReadPrimary(output).Contains("CTYPE1"));
        Assert.True(FitsHeader.ReadPrimary(dest).Contains("CTYPE1"));
        Assert.Equal(data, File.ReadAllBytes(dest)[^data.Length..]);
        Assert.Equal([destName, outputName, "source.fit"], FilesIn(ws.Dir));
    }

    /// <summary>
    /// A file replaced by a copy keeps what makes it the user's file: its creation time and its
    /// attributes, Hidden here, in place and as an existing new file (R-012; CR-018 of the review of
    /// 2026-11-16: a plain move gave it the copy's creation time and the Archive attribute alone).
    /// </summary>
    /// <param name="inPlace">True for the destination rewritten in place, false for an existing file given to -o.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void R012_Run_AReplacedFileKeepsItsCreationTimeAndItsAttributes(bool inPlace)
    {
        using var ws = new TempWorkspace();
        var source = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards()));
        var data = FitsTestData.DeterministicData(400);
        var dest = ws.WriteFile("dest.fit", FitsTestData.MinimalFitsFile(FitsTestData.ProcessedCards(), data));
        string target = inPlace ? dest : ws.WriteFile("out.fit", [1, 2, 3]);
        var created = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Local);
        File.SetCreationTime(target, created);
        File.SetAttributes(target, FileAttributes.Hidden | FileAttributes.Archive);

        var outcome = WcsCopy.Run(Request(source, dest, inPlace ? null : target));

        Assert.Equal(WcsCopyKind.Copied, outcome.Kind);
        Assert.True(FitsHeader.ReadPrimary(target).Contains("CTYPE1"));
        Assert.Equal(data, File.ReadAllBytes(target)[^data.Length..]);
        Assert.Equal(created, File.GetCreationTime(target));
        Assert.True(File.GetAttributes(target).HasFlag(FileAttributes.Hidden), File.GetAttributes(target).ToString());
        Assert.Equal(inPlace ? ["dest.fit", "source.fit"] : ["dest.fit", "out.fit", "source.fit"], FilesIn(ws.Dir));
    }

    /// <summary>
    /// A read-only destination is refused at the replace in place (R-014): the line says access is
    /// denied and names no temporary file, the destination keeps every byte and nothing is left
    /// beside it, through the replace as through the move it was.
    /// </summary>
    [Fact]
    public void R014_Run_AReadOnlyDestination_IsAWriteErrorThatLeavesTheFolderAsItWas()
    {
        using var ws = new TempWorkspace();
        using IDisposable _ = Ui.Use(UiLanguage.English);
        var source = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards()));
        var destBytes = FitsTestData.MinimalFitsFile(FitsTestData.ProcessedCards(), FitsTestData.DeterministicData(400));
        var dest = ws.WriteFile("dest.fit", destBytes);
        File.SetAttributes(dest, FileAttributes.ReadOnly);
        try
        {
            var outcome = WcsCopy.Run(Request(source, dest));

            Assert.Equal(WcsCopyKind.Refused, outcome.Kind);
            Assert.StartsWith("Write error: Access to the path", outcome.Error, StringComparison.Ordinal);
            Assert.EndsWith("denied.", outcome.Error, StringComparison.Ordinal);
            Assert.DoesNotContain(".copywcs-", outcome.Error, StringComparison.Ordinal);
            Assert.Equal(destBytes, File.ReadAllBytes(dest));
            Assert.Equal(["dest.fit", "source.fit"], FilesIn(ws.Dir));
        }
        finally
        {
            File.SetAttributes(dest, FileAttributes.Normal);
        }
    }

    /// <summary>
    /// A new file whose path names an existing folder is a write error that says access is
    /// denied, as before the replace (R-014; the review of the branch, 2026-11-18: the move that
    /// replaced the overwriting one said a file already existed), and the folder is left alone.
    /// </summary>
    [Fact]
    public void R014_Run_ANewFileThatIsAFolder_IsAWriteErrorThatSaysAccessIsDenied()
    {
        using var ws = new TempWorkspace();
        using IDisposable _ = Ui.Use(UiLanguage.English);
        var source = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards()));
        var destBytes = FitsTestData.MinimalFitsFile(FitsTestData.ProcessedCards(), FitsTestData.DeterministicData(400));
        var dest = ws.WriteFile("dest.fit", destBytes);
        string folder = Directory.CreateDirectory(ws.FilePath("outdir")).FullName;

        var outcome = WcsCopy.Run(Request(source, dest, folder));

        Assert.Equal(WcsCopyKind.Refused, outcome.Kind);
        Assert.Equal("Write error: Access to the path is denied.", outcome.Error);
        Assert.Empty(Directory.GetFileSystemEntries(folder));
        Assert.Equal(destBytes, File.ReadAllBytes(dest));
        Assert.Equal(["dest.fit", "source.fit"], FilesIn(ws.Dir));
    }

    /// <summary>The message .NET gives for Windows' error 1177 (ERROR_UNABLE_TO_MOVE_REPLACEMENT_2), measured on .NET 8 by the second review of the branch.</summary>
    private const string Message1177 =
        "Unable to move the replacement file to the file to be replaced. The file to be replaced has been renamed using the backup name.";

    /// <summary>
    /// Windows' replace failing halfway (R-014; ERROR_UNABLE_TO_MOVE_REPLACEMENT_2, 1177, which
    /// shares and security software are reported to cause, never seen on this PC): the old file
    /// is already under the backup name and the new one could not take its place. The write puts
    /// the backup back, so the destination keeps every byte and nothing is left beside it; the
    /// line keeps Windows' words, whose last sentence the put-back makes untrue, and says the file
    /// was put back; the log says so at Warn.
    /// </summary>
    [Fact]
    public void R014_Run_AReplaceThatFailsHalfway_PutsTheOriginalBack()
    {
        using var ws = new TempWorkspace();
        using IDisposable _ = Ui.Use(UiLanguage.English);
        var source = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards()));
        var destBytes = FitsTestData.MinimalFitsFile(FitsTestData.ProcessedCards(), FitsTestData.DeterministicData(400));
        var dest = ws.WriteFile("dest.fit", destBytes);
        string? backupSeen = null;
        FitsHeader.ReplaceForTests = (temp, target, backup) =>
        {
            backupSeen = backup;
            File.Move(target, backup);
            throw new IOException(Message1177, unchecked((int)0x80070499));
        };
        try
        {
            using var log = LogCapture.Start();
            var outcome = WcsCopy.Run(Request(source, dest));

            Assert.Equal(WcsCopyKind.Refused, outcome.Kind);
            Assert.Equal("Write error: " + Message1177 + " The file was put back as it was.", outcome.Error);
            Assert.Matches("^" + Regex.Escape(dest) + @"\.copywcs-[0-9a-f]{8}\.bak$", backupSeen);
            Assert.Equal(destBytes, File.ReadAllBytes(dest));
            Assert.Equal(["dest.fit", "source.fit"], FilesIn(ws.Dir));
            Assert.Contains(log.Events, e => e.Level.Name == "Warn"
                && e.Message == "the replace of " + dest + " failed halfway; the old file was put back from " + backupSeen);
        }
        finally
        {
            FitsHeader.ReplaceForTests = null;
        }
    }

    /// <summary>
    /// The same in Romanian: the put-back sentence is the pair's second half (R-014).
    /// </summary>
    [Fact]
    public void R014_Run_AReplaceThatFailsHalfway_SaysItWasPutBackInRomanian()
    {
        using var ws = new TempWorkspace();
        var source = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards()));
        var dest = ws.WriteFile("dest.fit", FitsTestData.MinimalFitsFile(FitsTestData.ProcessedCards(), FitsTestData.DeterministicData(400)));
        FitsHeader.ReplaceForTests = (temp, target, backup) =>
        {
            File.Move(target, backup);
            throw new IOException(Message1177, unchecked((int)0x80070499));
        };
        try
        {
            var outcome = WcsCopy.Run(Request(source, dest));

            Assert.Equal("Eroare la scriere: " + Message1177 + " Fisierul a fost pus inapoi cum era.", outcome.Error);
        }
        finally
        {
            FitsHeader.ReplaceForTests = null;
        }
    }

    /// <summary>
    /// A replace that fails halfway, and a backup that cannot be put back (a folder now holds the
    /// destination's name): nothing more is deleted, the new file kept as its temporary file too,
    /// and the line says where the old file is kept (R-014), the one case where a failed write
    /// leaves files beside the target; the log says so at Error.
    /// </summary>
    [Fact]
    public void R014_Run_ABackupThatCannotBePutBack_IsKeptAndTheLineNamesIt()
    {
        using var ws = new TempWorkspace();
        var source = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards()));
        var destBytes = FitsTestData.MinimalFitsFile(FitsTestData.ProcessedCards(), FitsTestData.DeterministicData(400));
        var dest = ws.WriteFile("dest.fit", destBytes);
        string? backupSeen = null;
        string? tempSeen = null;
        FitsHeader.ReplaceForTests = (temp, target, backup) =>
        {
            backupSeen = backup;
            tempSeen = temp;
            File.Move(target, backup);
            Directory.CreateDirectory(target);
            throw new IOException(Message1177, unchecked((int)0x80070499));
        };
        try
        {
            using var log = LogCapture.Start();
            var outcome = WcsCopy.Run(Request(source, dest));

            Assert.Equal(WcsCopyKind.Refused, outcome.Kind);
            Assert.NotNull(backupSeen);
            Assert.NotNull(tempSeen);
            Assert.Equal($"Eroare la scriere: {Message1177} Fisierul a fost pastrat ca '{backupSeen}'.", outcome.Error);
            Assert.Equal(destBytes, File.ReadAllBytes(backupSeen));
            Assert.True(FitsHeader.ReadPrimary(tempSeen).Contains("CTYPE1"), "the new file is kept");
            Assert.Contains(log.Events, e => e.Level.Name == "Error"
                && e.Message == "the replace of " + dest + " failed halfway; the old file is kept as " + backupSeen + " and the new one as " + tempSeen);
        }
        finally
        {
            FitsHeader.ReplaceForTests = null;
            Directory.Delete(dest);
        }
    }

    /// <summary>
    /// A backup whose move back fails (another program holds it open without delete sharing) is
    /// kept, the move's exception at Error, and the line says where, in English too (R-014).
    /// </summary>
    [Fact]
    public void R014_Run_ABackupWhoseMoveBackFails_IsKeptAndTheLineNamesIt()
    {
        using var ws = new TempWorkspace();
        using IDisposable _ = Ui.Use(UiLanguage.English);
        var source = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards()));
        var destBytes = FitsTestData.MinimalFitsFile(FitsTestData.ProcessedCards(), FitsTestData.DeterministicData(400));
        var dest = ws.WriteFile("dest.fit", destBytes);
        string? backupSeen = null;
        FileStream? hold = null;
        FitsHeader.ReplaceForTests = (temp, target, backup) =>
        {
            backupSeen = backup;
            File.Move(target, backup);
            hold = new FileStream(backup, FileMode.Open, FileAccess.Read, FileShare.Read);
            throw new IOException(Message1177, unchecked((int)0x80070499));
        };
        try
        {
            using var log = LogCapture.Start();
            var outcome = WcsCopy.Run(Request(source, dest));

            Assert.Equal(WcsCopyKind.Refused, outcome.Kind);
            Assert.Equal($"Write error: {Message1177} The file was kept as '{backupSeen}'.", outcome.Error);
            Assert.False(File.Exists(dest));
            Assert.Contains(log.Events, e => e.Level.Name == "Error" && e.Exception is IOException
                && e.Message == "the old file could not be put back from " + backupSeen + " to " + dest);
        }
        finally
        {
            FitsHeader.ReplaceForTests = null;
            hold?.Dispose();
        }

        Assert.Equal(destBytes, File.ReadAllBytes(backupSeen!));
    }

    /// <summary>
    /// A backup that cannot be deleted after a replace that succeeded (another program holds it
    /// open): the copy is done, the old file stays under the backup's name, and the log says so at
    /// Warn (R-012).
    /// </summary>
    [Fact]
    public void R012_Run_ABackupThatCannotBeDeleted_LeavesTheCopyDoneAndSaysSoInTheLog()
    {
        using var ws = new TempWorkspace();
        var source = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards()));
        var destBytes = FitsTestData.MinimalFitsFile(FitsTestData.ProcessedCards(), FitsTestData.DeterministicData(400));
        var dest = ws.WriteFile("dest.fit", destBytes);
        string? backupSeen = null;
        FileStream? hold = null;
        FitsHeader.ReplaceForTests = (temp, target, backup) =>
        {
            backupSeen = backup;
            File.Replace(temp, target, backup, ignoreMetadataErrors: true);
            hold = new FileStream(backup, FileMode.Open, FileAccess.Read, FileShare.Read);
        };
        try
        {
            using var log = LogCapture.Start();
            var outcome = WcsCopy.Run(Request(source, dest));

            Assert.Equal(WcsCopyKind.Copied, outcome.Kind);
            Assert.True(FitsHeader.ReadPrimary(dest).Contains("CTYPE1"));
            Assert.Contains(log.Events, e => e.Level.Name == "Warn" && e.Exception is not null
                && e.Message == "the backup " + backupSeen + " of the replaced file could not be removed");
        }
        finally
        {
            FitsHeader.ReplaceForTests = null;
            hold?.Dispose();
        }

        Assert.Equal(destBytes, File.ReadAllBytes(backupSeen!));
    }

    /// <summary>A replace that succeeds deletes the backup of the old file, in place and over an existing new file (R-012).</summary>
    [Fact]
    public void R012_Run_AReplaceThatSucceeds_LeavesNoBackup()
    {
        using var ws = new TempWorkspace();
        var source = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards()));
        var dest = ws.WriteFile("dest.fit", FitsTestData.MinimalFitsFile(FitsTestData.ProcessedCards(), FitsTestData.DeterministicData(400)));
        var output = ws.WriteFile("out.fit", [1, 2, 3]);
        string? backupSeen = null;
        FitsHeader.ReplaceForTests = (temp, target, backup) =>
        {
            backupSeen = backup;
            File.Replace(temp, target, backup, ignoreMetadataErrors: true);
            Assert.True(File.Exists(backup));
        };
        try
        {
            Assert.Equal(WcsCopyKind.Copied, WcsCopy.Run(Request(source, dest, output)).Kind);
            Assert.Equal(WcsCopyKind.Copied, WcsCopy.Run(Request(source, dest)).Kind);
        }
        finally
        {
            FitsHeader.ReplaceForTests = null;
        }

        Assert.NotNull(backupSeen);
        Assert.Equal(["dest.fit", "out.fit", "source.fit"], FilesIn(ws.Dir));
    }

    /// <summary>A header with the two checksum cards a program such as the SyQon CLI writes last, as in V-002's destination.</summary>
    /// <param name="cards">The header's cards; the two are added at its end.</param>
    /// <returns>The same list.</returns>
    private static List<string> WithChecksum(List<string> cards)
    {
        cards.Add(FitsTestData.Card("CHECKSUM", "'6jF39j936jE36j93'", "HDU checksum updated 2026-10-27T10:58:49"));
        cards.Add(FitsTestData.Card("DATASUM", "'2647693551'", "data unit checksum updated 2026-10-27T10:58:49"));
        return cards;
    }

    /// <summary>
    /// A destination holding CHECKSUM is written without it, into a new file and in place, from a
    /// solved source and from one without CTYPE1, and keeps its DATASUM card byte for byte (R-018;
    /// CR-004 of the review of 2026-11-16: the checksum was written back, no longer matching).
    /// </summary>
    [Fact]
    public void R018_Run_ADestinationWithAChecksum_IsWrittenWithoutIt_AndKeepsItsDatasum()
    {
        using var ws = new TempWorkspace();
        var source = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards()));
        var unsolvedCards = FitsTestData.PlateSolvedCards();
        unsolvedCards.RemoveAll(c => c.StartsWith("CTYPE1", StringComparison.Ordinal));
        var unsolved = ws.WriteFile("unsolved.fit", FitsTestData.MinimalFitsFile(unsolvedCards));
        var destCards = WithChecksum(FitsTestData.ProcessedCards());
        var datasum = destCards[^1];
        var dest = ws.WriteFile("dest.fit", FitsTestData.MinimalFitsFile(destCards, FitsTestData.DeterministicData(400)));
        var other = ws.WriteFile("other.fit", FitsTestData.MinimalFitsFile(destCards, FitsTestData.DeterministicData(400)));
        var output = ws.FilePath("out.fit");

        Assert.Equal(WcsCopyKind.Copied, WcsCopy.Run(Request(source, dest, output)).Kind);
        Assert.Equal(WcsCopyKind.Copied, WcsCopy.Run(Request(source, dest)).Kind);
        Assert.Equal(WcsCopyKind.Copied, WcsCopy.Run(Request(unsolved, other)).Kind);

        foreach (var path in new[] { output, dest, other })
        {
            var written = FitsHeader.ReadPrimary(path);
            Assert.False(written.Contains("CHECKSUM"), path);
            Assert.Equal(datasum, written.GetCard("DATASUM"));
        }
    }

    /// <summary>A dry run, and a refused run, leave the destination's CHECKSUM where it is, as every other byte (R-018).</summary>
    [Fact]
    public void R018_Run_ADryRunOrARefusal_LeavesTheChecksum()
    {
        using var ws = new TempWorkspace();
        var source = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards()));
        var destBytes = FitsTestData.MinimalFitsFile(WithChecksum(FitsTestData.ProcessedCards(200, 80)));
        var dest = ws.WriteFile("dest.fit", destBytes);

        Assert.Equal(WcsCopyKind.Previewed, WcsCopy.Run(Request(source, dest, force: true, dryRun: true)).Kind);
        Assert.Equal(WcsCopyKind.Refused, WcsCopy.Run(Request(source, dest)).Kind);
        Assert.Equal(destBytes, File.ReadAllBytes(dest));
    }

    /// <summary>The first card of R-010's long OBJECT, its string ending in the ampersand that says it goes on.</summary>
    private static readonly string LongObject = FitsTestData.Card("OBJECT", "'NGC 6960, the Western Veil Nebula, with 52 Cygni in front &'");

    /// <summary>The CONTINUE card that ends R-010's long OBJECT.</summary>
    private static readonly string LongObjectEnd = FitsTestData.Continue("'of the filaments'");

    /// <summary>The default plate-solved header with its OBJECT as a long string of two cards.</summary>
    private static List<string> SourceWithALongObject()
    {
        var cards = FitsTestData.PlateSolvedCards();
        int at = cards.FindIndex(c => c.StartsWith("OBJECT", StringComparison.Ordinal));
        cards[at] = LongObject;
        cards.Insert(at + 1, LongObjectEnd);
        return cards;
    }

    /// <summary>
    /// A long string value of the source reaches the destination whole, its CONTINUE card right
    /// under its card, and the closing line still counts keys (R-010; CR-005 of the review of
    /// 2026-11-16: the first card came alone, a value cut with a stray ampersand).
    /// </summary>
    [Fact]
    public void R010_Run_ALongStringValue_TravelsWithItsContinueCards()
    {
        using var ws = new TempWorkspace();
        var source = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(SourceWithALongObject()));
        var dest = ws.WriteFile("dest.fit", FitsTestData.MinimalFitsFile(FitsTestData.ProcessedCards(), FitsTestData.DeterministicData(400)));

        var outcome = WcsCopy.Run(Request(source, dest));

        Assert.Equal(WcsCopyKind.Copied, outcome.Kind);
        Assert.Equal($"Copiate 15 chei in {dest}.", outcome.Notes[0]);
        List<string> written = FitsTestData.HeaderCards(File.ReadAllBytes(dest));
        int obj = written.FindIndex(c => c.StartsWith("OBJECT", StringComparison.Ordinal));
        Assert.Equal(LongObject, written[obj]);
        Assert.Equal(LongObjectEnd, written[obj + 1]);
        Assert.Single(written, c => c.StartsWith("CONTINUE", StringComparison.Ordinal));
    }

    /// <summary>
    /// A card of the destination that the copy replaces takes the CONTINUE cards right under it
    /// away with it (R-010; CR-005: the old continuation stayed under the new short value, and
    /// astropy reads the two as one).
    /// </summary>
    [Fact]
    public void R010_Run_AReplacedCard_TakesItsOldContinueCardsWithIt()
    {
        using var ws = new TempWorkspace();
        var source = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards()));
        var destCards = FitsTestData.ProcessedCards();
        destCards.Add(FitsTestData.Card("TELESCOP", "'Sky-Watcher Evostar 72ED on a Star Adventurer GTi, guided &'"));
        destCards.Add(FitsTestData.Continue("'old tail'"));
        destCards.Add(FitsTestData.Card("SITELAT", "46.77"));
        var dest = ws.WriteFile("dest.fit", FitsTestData.MinimalFitsFile(destCards));

        Assert.Equal(WcsCopyKind.Copied, WcsCopy.Run(Request(source, dest)).Kind);

        List<string> written = FitsTestData.HeaderCards(File.ReadAllBytes(dest));
        int tel = written.FindIndex(c => c.StartsWith("TELESCOP", StringComparison.Ordinal));
        Assert.Equal(FitsTestData.Card("TELESCOP", "'My Scope'"), written[tel]);
        Assert.StartsWith("SITELAT", written[tel + 1], StringComparison.Ordinal);
        Assert.DoesNotContain(written, c => c.StartsWith("CONTINUE", StringComparison.Ordinal));
    }

    /// <summary>
    /// A dry run lists a CONTINUE card under the card it continues, and its heading counts the
    /// key once: 15 keys, 16 cards (R-009, R-010; CR-005).
    /// </summary>
    [Fact]
    public void R009_Run_DryRun_ListsTheContinueCardUnderItsCard_AndCountsTheKeyOnce()
    {
        using var ws = new TempWorkspace();
        var source = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(SourceWithALongObject()));
        var dest = ws.WriteFile("dest.fit", FitsTestData.MinimalFitsFile(FitsTestData.ProcessedCards()));

        var outcome = WcsCopy.Run(Request(source, dest, dryRun: true));

        Assert.Equal(["Chei care ar fi copiate (15):"], outcome.Notes);
        Assert.Equal(16, outcome.Cards.Count);
        int obj = outcome.Cards.ToList().FindIndex(c => c.StartsWith("OBJECT", StringComparison.Ordinal));
        Assert.Equal(LongObject, outcome.Cards[obj]);
        Assert.Equal(LongObjectEnd, outcome.Cards[obj + 1]);
    }

    /// <summary>The default plate-solved header with a WCSAXES card where astrometry.net writes it, right under the image size.</summary>
    private static List<string> SourceWithWcsaxes()
    {
        var cards = FitsTestData.PlateSolvedCards();
        cards.Insert(cards.FindIndex(c => c.StartsWith("NAXIS2", StringComparison.Ordinal)) + 1, FitsTestData.Card("WCSAXES", "2"));
        return cards;
    }

    /// <summary>
    /// Into a destination with no plate solve, the keys the copy sets follow the destination's
    /// cards in the order of R-007, a card the destination has (OBJECT) staying where it was, and
    /// WCSAXES is already first of the plate solve: nothing moves (R-061).
    /// </summary>
    [Fact]
    public void R061_Run_IntoADestinationWithoutAPlateSolve_TheKeysFollowItsCardsInTheListsOrder()
    {
        using var ws = new TempWorkspace();
        var source = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(SourceWithWcsaxes()));
        var dest = ws.WriteFile("dest.fit", FitsTestData.MinimalFitsFile(FitsTestData.ProcessedCards()));

        Assert.Equal(WcsCopyKind.Copied, WcsCopy.Run(Request(source, dest)).Kind);

        Assert.Equal(
            ["SIMPLE", "BITPIX", "NAXIS", "NAXIS1", "NAXIS2", "OBJECT", "WCSAXES", "CTYPE1", "CTYPE2", "CRPIX1", "CRPIX2",
             "CRVAL1", "CRVAL2", "CDELT1", "CDELT2", "PLTSOLVD", "A_0_2", "BP_1_1", "FOCALLEN", "XPIXSZ", "TELESCOP", "HISTORY"],
            FitsHeader.ReadPrimary(dest).Keywords);
    }

    /// <summary>
    /// A source's WCSAXES goes right above the first card of the destination's own plate solve,
    /// as the FITS standard asks (4.0, section 8.2), where it was appended after every card of the
    /// destination, below the WCS cards the copy replaces where they stand (R-061; CR-019 of the
    /// review of 2026-11-16).
    /// </summary>
    [Fact]
    public void R061_Run_ACopiedWcsaxes_GoesRightAboveTheDestinationsPlateSolve()
    {
        using var ws = new TempWorkspace();
        var source = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(SourceWithWcsaxes()));
        var dest = ws.WriteFile("dest.fit", FitsTestData.MinimalFitsFile(FitsTestData.OldSolveCards()));

        Assert.Equal(WcsCopyKind.Copied, WcsCopy.Run(Request(source, dest)).Kind);

        // Card 7, where the old CTYPE1 stood, and CTYPE1 under it.
        Assert.Equal(["SIMPLE", "BITPIX", "NAXIS", "NAXIS1", "NAXIS2", "OBJECT", "WCSAXES", "CTYPE1"],
            FitsHeader.ReadPrimary(dest).Keywords.Take(8));
    }

    /// <summary>
    /// A copy that sets no WCSAXES, from a source with neither WCSAXES nor CTYPE1, which drops
    /// nothing (R-016), leaves a destination's own WCSAXES where it stands, below its plate solve:
    /// only a WCSAXES the copy set moves (R-061; the review of the branch of CR-019: a move asked
    /// whatever the source held passed every test).
    /// </summary>
    [Fact]
    public void R061_Run_ACopyThatSetsNoWcsaxes_LeavesTheDestinationsOwnWhereItStands()
    {
        using var ws = new TempWorkspace();
        List<string> sourceCards = FitsTestData.ProcessedCards();
        sourceCards.Add(FitsTestData.Card("FOCALLEN", "700.0"));
        var source = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(sourceCards));
        List<string> destCards = FitsTestData.OldSolveCards();
        destCards.Add(FitsTestData.Card("WCSAXES", "2"));
        var dest = ws.WriteFile("dest.fit", FitsTestData.MinimalFitsFile(destCards));

        Assert.Equal(WcsCopyKind.Copied, WcsCopy.Run(Request(source, dest)).Kind);

        List<string> keys = FitsHeader.ReadPrimary(dest).Keywords.ToList();
        Assert.Equal(keys.IndexOf("SITELAT") + 1, keys.IndexOf("WCSAXES"));
        Assert.True(keys.IndexOf("CTYPE1") < keys.IndexOf("WCSAXES"));
    }

    /// <summary>A destination's own WCSAXES that stands below its plate solve, which the copy replaces where it stands, is moved above that solve too (R-061; CR-019).</summary>
    [Fact]
    public void R061_Run_ADestinationsOwnWcsaxesBelowItsSolve_IsMovedAboveIt()
    {
        using var ws = new TempWorkspace();
        var source = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(SourceWithWcsaxes()));
        List<string> destCards = FitsTestData.OldSolveCards();
        destCards.Add(FitsTestData.Card("WCSAXES", "2", "an old solve's own"));
        var dest = ws.WriteFile("dest.fit", FitsTestData.MinimalFitsFile(destCards));

        Assert.Equal(WcsCopyKind.Copied, WcsCopy.Run(Request(source, dest)).Kind);

        FitsHeader written = FitsHeader.ReadPrimary(dest);
        Assert.Equal(["SIMPLE", "BITPIX", "NAXIS", "NAXIS1", "NAXIS2", "OBJECT", "WCSAXES", "CTYPE1"], written.Keywords.Take(8));
        Assert.Equal(FitsTestData.Card("WCSAXES", "2"), written.GetCard("WCSAXES"));
        Assert.Single(written.Keywords, k => k == "WCSAXES");
    }
}
