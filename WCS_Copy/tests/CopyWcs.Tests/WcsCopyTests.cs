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
    public void Run_SourceMissing_IsRefusedWithTheReadLineAndNoWarning()
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
    public void Run_DimensionMismatchWithoutForce_IsRefusedAndWritesNothing()
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
    public void Run_DimensionMismatchWithForce_CopiesWithOneWarning()
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
    public void Run_SourceWithoutCtype1_WarnsBeforeTheOutcome()
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
    public void Run_DryRun_ListsTheRawCardsAndWritesNothing()
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
    public void Run_NoOptical_LeavesTheOpticalCardsOut()
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
    public void Run_NoRelevantKey_IsRefusedAndWritesNothing()
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
    public void Run_Copy_WritesTheTargetAndEndsWithTheCountAndTheReminder()
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
    public void Run_WriteFailure_IsRefusedWithTheWriteLine()
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
}
