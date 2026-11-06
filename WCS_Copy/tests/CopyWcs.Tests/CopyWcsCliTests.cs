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

using System.Text;

namespace CopyWcs.Tests;

public class CopyWcsCliTests
{
    private static (int ExitCode, string Stdout, string Stderr) Run(params string[] args)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var code = CopyWcsCli.Run(args, stdout, stderr);
        return (code, stdout.ToString(), stderr.ToString());
    }

    [Fact]
    public void R012_Run_SuccessfulInPlaceCopy_UpdatesDestReturnsZeroPreservesPixelData()
    {
        using var ws = new TempWorkspace();
        var data = FitsTestData.DeterministicData(400);
        var sourcePath = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards(), data));
        var destPath = ws.WriteFile("dest.fit", FitsTestData.MinimalFitsFile(FitsTestData.ProcessedCards(), data));

        var result = Run(sourcePath, destPath);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("", result.Stderr);

        var rewritten = FitsHeader.ReadPrimary(destPath);
        Assert.True(rewritten.Contains("CTYPE1"));
        Assert.True(rewritten.Contains("CRVAL1"));

        var bytes = File.ReadAllBytes(destPath);
        Assert.Equal(data, bytes[^data.Length..]);
    }

    [Fact]
    public void R012_Run_SuccessfulCopyViaOutputOption_WritesNewFileLeavesOriginalDestUntouched()
    {
        using var ws = new TempWorkspace();
        var data = FitsTestData.DeterministicData(400);
        var sourcePath = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards(), data));
        var destBytesOriginal = FitsTestData.MinimalFitsFile(FitsTestData.ProcessedCards(), data);
        var destPath = ws.WriteFile("dest.fit", destBytesOriginal);
        var outputPath = ws.FilePath("out.fit");

        var result = Run(sourcePath, destPath, "-o", outputPath);

        Assert.Equal(0, result.ExitCode);
        Assert.True(File.Exists(outputPath));
        Assert.Equal(destBytesOriginal, File.ReadAllBytes(destPath));

        var written = FitsHeader.ReadPrimary(outputPath);
        Assert.True(written.Contains("CTYPE1"));
    }

    [Fact]
    public void R010_Run_CopiedCards_AreByteIdenticalToSourceNotJustValueEqual()
    {
        using var ws = new TempWorkspace();
        var data = FitsTestData.DeterministicData(100);
        var sourceCards = FitsTestData.PlateSolvedCards();
        var sourcePath = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(sourceCards, data));
        var destPath = ws.WriteFile("dest.fit", FitsTestData.MinimalFitsFile(FitsTestData.ProcessedCards(), data));

        Run(sourcePath, destPath);

        var expectedCtype1Card = sourceCards.Single(c => c.StartsWith("CTYPE1"));
        var rewritten = FitsHeader.ReadPrimary(destPath);
        Assert.Equal(expectedCtype1Card, rewritten.GetCard("CTYPE1"));
    }

    /// <summary>
    /// The header is read and written byte for byte: a byte above 0x7F in a copied card, which a
    /// valid header never holds, reaches the destination as it is, the rest of the card verbatim
    /// (ADR 0010; until then the ASCII reader and writer turned 0xC5 into <c>?</c>).
    /// </summary>
    [Fact]
    public void R010_Run_AByteAboveAsciiInACopiedCard_IsCopiedAsItIs()
    {
        using var ws = new TempWorkspace();
        var data = FitsTestData.DeterministicData(100);
        var sourceCards = FitsTestData.PlateSolvedCards();
        var obj = sourceCards.FindIndex(c => c.StartsWith("OBJECT", StringComparison.Ordinal));
        sourceCards[obj] = FitsTestData.Card("OBJECT", "'M42'", "Orion #");
        var source = FitsTestData.MinimalFitsFile(sourceCards, data);
        // The marker's byte becomes 0xC5 (A-ring in Latin-1), which no ASCII string holds.
        WithByte(source, "Orion #", 0xC5);
        var sourcePath = ws.WriteFile("source.fit", source);
        var destPath = ws.WriteFile("dest.fit", FitsTestData.MinimalFitsFile(FitsTestData.ProcessedCards(), data));

        Assert.Equal(0, Run(sourcePath, destPath).ExitCode);

        var written = Encoding.Latin1.GetString(File.ReadAllBytes(destPath));
        Assert.Contains(FitsTestData.Card("OBJECT", "'M42'", "Orion " + (char)0xC5), written, StringComparison.Ordinal);
        // No question mark in the header; the data area holds the byte 0x3F by construction.
        Assert.DoesNotContain('?', written[..^data.Length]);
    }

    /// <summary>
    /// The whole header is rewritten, so a card of the destination the copy never touches goes
    /// through the reader and the writer too: a byte above 0x7F in it stays as it is (ADR 0010).
    /// </summary>
    [Fact]
    public void R010_Run_AByteAboveAsciiInADestinationCardNotCopied_StaysAsItIs()
    {
        using var ws = new TempWorkspace();
        var data = FitsTestData.DeterministicData(100);
        var destCards = FitsTestData.ProcessedCards();
        destCards.Add(FitsTestData.Pad("COMMENT Stacked in Ume#"));
        var dest = FitsTestData.MinimalFitsFile(destCards, data);
        WithByte(dest, "Ume#", 0xC5);
        var sourcePath = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards(), data));
        var destPath = ws.WriteFile("dest.fit", dest);

        Assert.Equal(0, Run(sourcePath, destPath).ExitCode);

        var written = Encoding.Latin1.GetString(File.ReadAllBytes(destPath));
        Assert.Contains(FitsTestData.Pad("COMMENT Stacked in Ume" + (char)0xC5), written, StringComparison.Ordinal);
        // No question mark in the header; the data area holds the byte 0x3F by construction.
        Assert.DoesNotContain('?', written[..^data.Length]);
    }

    /// <summary>
    /// A destination card whose keyword field is <c>OBJECT</c> followed by two bytes 0xA0 is not
    /// the <c>OBJECT</c> card: the copy appends the source's and leaves it as it was, bytes
    /// included (the review of 2026-10-31: Latin-1 reads 0xA0 as a no-break space, which
    /// <c>string.Trim()</c> would strip from the keyword).
    /// </summary>
    [Fact]
    public void R010_Run_ADestinationKeywordPaddedWithTheByte0xA0_IsNotReplaced()
    {
        using var ws = new TempWorkspace();
        var data = FitsTestData.DeterministicData(100);
        var destCards = FitsTestData.ProcessedCards();
        destCards.RemoveAll(c => c.StartsWith("OBJECT", StringComparison.Ordinal));
        destCards.Add(FitsTestData.Pad("OBJECT##= 'M43'"));
        var dest = FitsTestData.MinimalFitsFile(destCards, data);
        WithByte(dest, "OBJECT#", 0xA0);
        WithByte(dest, "OBJECT" + (char)0xA0 + "#", 0xA0);
        var sourcePath = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards(), data));
        var destPath = ws.WriteFile("dest.fit", dest);

        Assert.Equal(0, Run(sourcePath, destPath).ExitCode);

        var written = Encoding.Latin1.GetString(File.ReadAllBytes(destPath));
        Assert.Contains(FitsTestData.Pad("OBJECT" + (char)0xA0 + (char)0xA0 + "= 'M43'"), written, StringComparison.Ordinal);
        Assert.Contains(FitsTestData.Card("OBJECT", "'M42'"), written, StringComparison.Ordinal);
    }

    /// <summary>
    /// The HISTORY card is ASCII whatever the source's name holds: a source named with a letter
    /// with a diacritic (a with breve, by its code point so this file stays ASCII) is named in
    /// the card with the base letter (ADR 0010).
    /// </summary>
    [Fact]
    public void R011_Run_ASourceNamedWithADiacritic_WritesTheBaseLetterInHistory()
    {
        using var ws = new TempWorkspace();
        var data = FitsTestData.DeterministicData(100);
        var sourcePath = ws.WriteFile("Nebul" + (char)0x0103 + ".fit", FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards(), data));
        var destPath = ws.WriteFile("dest.fit", FitsTestData.MinimalFitsFile(FitsTestData.ProcessedCards(), data));

        Assert.Equal(0, Run(sourcePath, destPath).ExitCode);

        var written = File.ReadAllBytes(destPath);
        Assert.Contains("HISTORY WCS copied from Nebula.fit".PadRight(80), Encoding.Latin1.GetString(written), StringComparison.Ordinal);
        // Over the whole file: its data is the 100 bytes 0 to 99, all below 0x80.
        Assert.DoesNotContain(written, b => b > 0x7F);
    }

    /// <summary>Puts a byte into a file's bytes where a marker's last character is: the one way a test file gets a byte above 0x7F.</summary>
    /// <param name="file">The file's bytes, changed in place.</param>
    /// <param name="marker">An ASCII text the file holds once, whose last character is replaced.</param>
    /// <param name="value">The byte to put there.</param>
    private static void WithByte(byte[] file, string marker, byte value)
    {
        var at = Encoding.Latin1.GetString(file).IndexOf(marker, StringComparison.Ordinal);
        Assert.True(at >= 0, "the marker is in the file");
        file[at + marker.Length - 1] = value;
    }

    [Fact]
    public void R005_Run_DimensionMismatchWithoutForce_ReturnsOneWritesNothing()
    {
        using var ws = new TempWorkspace();
        var sourcePath = ws.WriteFile("source.fit",
            FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards(naxis1: 100, naxis2: 80)));
        var destBytesOriginal = FitsTestData.MinimalFitsFile(FitsTestData.ProcessedCards(naxis1: 50, naxis2: 50));
        var destPath = ws.WriteFile("dest.fit", destBytesOriginal);

        var result = Run(sourcePath, destPath);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Dimensiuni diferite", result.Stderr);
        Assert.Equal(destBytesOriginal, File.ReadAllBytes(destPath));
    }

    [Fact]
    public void R005_Run_DimensionMismatchWithForce_WarnsButSucceeds()
    {
        using var ws = new TempWorkspace();
        var sourcePath = ws.WriteFile("source.fit",
            FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards(naxis1: 100, naxis2: 80)));
        var destPath = ws.WriteFile("dest.fit",
            FitsTestData.MinimalFitsFile(FitsTestData.ProcessedCards(naxis1: 50, naxis2: 50)));

        var result = Run(sourcePath, destPath, "--force");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("ATENTIE", result.Stdout);
        Assert.Contains("Continui oricum", result.Stdout);
    }

    [Fact]
    public void R006_Run_SourceMissingCtype1_PrintsWarningButContinues()
    {
        using var ws = new TempWorkspace();
        var sourceCards = new List<string>
        {
            FitsTestData.Card("SIMPLE", "T"),
            FitsTestData.Card("NAXIS1", "100"),
            FitsTestData.Card("NAXIS2", "80"),
            FitsTestData.Card("OBJECT", "'M42'"),
        };
        var sourcePath = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(sourceCards));
        var destPath = ws.WriteFile("dest.fit",
            FitsTestData.MinimalFitsFile(FitsTestData.ProcessedCards(naxis1: 100, naxis2: 80)));

        var result = Run(sourcePath, destPath);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("nu pare plate-solved", result.Stdout);
    }

    [Fact]
    public void R009_Run_DryRun_ListsCardsAndLeavesDestByteForByteUnchanged()
    {
        using var ws = new TempWorkspace();
        var sourcePath = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards()));
        var destBytesOriginal = FitsTestData.MinimalFitsFile(FitsTestData.ProcessedCards());
        var destPath = ws.WriteFile("dest.fit", destBytesOriginal);

        var result = Run(sourcePath, destPath, "--dry-run");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Chei care ar fi copiate", result.Stdout);
        Assert.Contains("CTYPE1", result.Stdout);
        Assert.Equal(destBytesOriginal, File.ReadAllBytes(destPath));
    }

    private static string[] Lines(string stdout) => stdout.ReplaceLineEndings("\n").TrimEnd('\n').Split('\n');

    [Fact]
    public void R016_Run_DryRun_PrintsTheDropLineAfterTheCards()
    {
        using var ws = new TempWorkspace();
        var sourcePath = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards()));
        var destBytes = FitsTestData.MinimalFitsFile(FitsTestData.OldSolveCards());
        var destPath = ws.WriteFile("dest.fit", destBytes);

        var result = Run(sourcePath, destPath, "--dry-run");

        Assert.Equal(0, result.ExitCode);
        string[] lines = Lines(result.Stdout);
        Assert.Equal("Chei care ar fi copiate (15):", lines[2]);
        Assert.All(lines[3..^1], l => Assert.StartsWith("  ", l, StringComparison.Ordinal));
        Assert.Equal(15, lines[3..^1].Length);
        Assert.Equal("Chei vechi de plate solve ale destinatiei care ar fi eliminate (6): CD1_1, CD1_2, CD2_1, CD2_2, CROTA2, A_3_0", lines[^1]);
        Assert.Equal(destBytes, File.ReadAllBytes(destPath));
    }

    [Fact]
    public void R016_Run_Copy_PrintsTheDropLineBeforeTheLastTwoLines()
    {
        using var ws = new TempWorkspace();
        var sourcePath = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards()));
        var destPath = ws.WriteFile("dest.fit", FitsTestData.MinimalFitsFile(FitsTestData.OldSolveCards()));

        var result = Run(sourcePath, destPath);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("", result.Stderr);
        string[] lines = Lines(result.Stdout);
        Assert.Equal(5, lines.Length);
        Assert.Equal("Eliminate 6 chei vechi de plate solve ale destinatiei: CD1_1, CD1_2, CD2_1, CD2_2, CROTA2, A_3_0.", lines[2]);
        Assert.Equal($"Copiate 15 chei in {destPath}.", lines[3]);
        Assert.StartsWith("Verifica vizual", lines[4], StringComparison.Ordinal);
        Assert.False(FitsHeader.ReadPrimary(destPath).Contains("CD1_1"));
    }

    [Fact]
    public void R016_Run_InEnglish_TheDropLinesReadAsTheRequirementSays()
    {
        using var ws = new TempWorkspace();
        var sourcePath = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards()));
        var destPath = ws.WriteFile("dest.fit", FitsTestData.MinimalFitsFile(FitsTestData.OldSolveCards()));

        using (Ui.Use(UiLanguage.English))
        {
            string[] preview = Lines(Run(sourcePath, destPath, "--dry-run").Stdout);
            string[] copy = Lines(Run(sourcePath, destPath, "-o", ws.FilePath("out.fit")).Stdout);

            Assert.Equal("Old plate-solve keys of the destination that would be dropped (6): CD1_1, CD1_2, CD2_1, CD2_2, CROTA2, A_3_0", preview[^1]);
            Assert.Equal("Dropped 6 old plate-solve keys of the destination: CD1_1, CD1_2, CD2_1, CD2_2, CROTA2, A_3_0.", copy[^3]);
        }
    }

    [Fact]
    public void R017_Run_AMirroredDestination_IsRefusedWithExitOne_AndForceWarnsFirst()
    {
        using var ws = new TempWorkspace();
        var sourcePath = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards()));
        var destBytes = FitsTestData.MinimalFitsFile(FitsTestData.MirroredSolveCards());
        var destPath = ws.WriteFile("dest.fit", destBytes);

        var refused = Run(sourcePath, destPath, "--dry-run");
        var forced = Run(sourcePath, destPath, "--force", "-o", ws.FilePath("out.fit"));

        Assert.Equal(1, refused.ExitCode);
        Assert.StartsWith("Plate solve-ul destinatiei e in oglinda fata de cel al sursei: ", refused.Stderr, StringComparison.Ordinal);
        Assert.Equal(2, Lines(refused.Stdout).Length);
        Assert.Equal(destBytes, File.ReadAllBytes(destPath));
        Assert.Equal(0, forced.ExitCode);
        Assert.Equal("ATENTIE: plate solve-ul destinatiei e in oglinda fata de cel al sursei. Continui oricum (--force).", Lines(forced.Stdout)[2]);
        Assert.True(File.Exists(ws.FilePath("out.fit")));
    }

    [Fact]
    public void R016_Run_NothingToDrop_PrintsNoDropLine()
    {
        using var ws = new TempWorkspace();
        var sourcePath = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards()));
        var destPath = ws.WriteFile("dest.fit", FitsTestData.MinimalFitsFile(FitsTestData.ProcessedCards()));

        string[] preview = Lines(Run(sourcePath, destPath, "--dry-run").Stdout);
        string[] copy = Lines(Run(sourcePath, destPath).Stdout);

        Assert.StartsWith("  ", preview[^1], StringComparison.Ordinal);
        Assert.Equal(4, copy.Length);
    }

    [Fact]
    public void R007_Run_NoOpticalFlag_OmitsOpticalKeysKeepsWcsKeys()
    {
        using var ws = new TempWorkspace();
        var sourcePath = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards()));
        var destPath = ws.WriteFile("dest.fit", FitsTestData.MinimalFitsFile(FitsTestData.ProcessedCards()));

        var result = Run(sourcePath, destPath, "--no-optical");

        Assert.Equal(0, result.ExitCode);
        var rewritten = FitsHeader.ReadPrimary(destPath);
        Assert.True(rewritten.Contains("CTYPE1"));
        Assert.False(rewritten.Contains("TELESCOP"));
    }

    [Fact]
    public void R007_Run_WithoutNoOpticalFlag_IncludesOpticalKeys()
    {
        using var ws = new TempWorkspace();
        var sourcePath = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards()));
        var destPath = ws.WriteFile("dest.fit", FitsTestData.MinimalFitsFile(FitsTestData.ProcessedCards()));

        var result = Run(sourcePath, destPath);

        Assert.Equal(0, result.ExitCode);
        var rewritten = FitsHeader.ReadPrimary(destPath);
        Assert.True(rewritten.Contains("TELESCOP"));
    }

    [Fact]
    public void R004_Run_SourceFileMissing_ReturnsOneWithReadErrorOnStderr()
    {
        using var ws = new TempWorkspace();
        var destPath = ws.WriteFile("dest.fit", FitsTestData.MinimalFitsFile(FitsTestData.ProcessedCards()));
        var missingSource = ws.FilePath("does-not-exist.fit");

        var result = Run(missingSource, destPath);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Nu pot citi sursa", result.Stderr);
    }

    [Fact]
    public void R004_Run_DestFileMissing_ReturnsOneWithReadErrorOnStderr()
    {
        using var ws = new TempWorkspace();
        var sourcePath = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards()));
        var missingDest = ws.FilePath("does-not-exist.fit");

        var result = Run(sourcePath, missingDest);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Nu pot citi destinatia", result.Stderr);
    }

    [Fact]
    public void R008_Run_NoRelevantKeysInSource_ReturnsOneWritesNothing()
    {
        using var ws = new TempWorkspace();
        var minimalCards = new List<string>
        {
            FitsTestData.Card("SIMPLE", "T"),
            FitsTestData.Card("NAXIS1", "100"),
            FitsTestData.Card("NAXIS2", "80"),
        };
        var sourcePath = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(minimalCards));
        var destBytesOriginal = FitsTestData.MinimalFitsFile(minimalCards);
        var destPath = ws.WriteFile("dest.fit", destBytesOriginal);

        var result = Run(sourcePath, destPath);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Nicio cheie relevanta", result.Stderr);
        Assert.Equal(destBytesOriginal, File.ReadAllBytes(destPath));
    }

    [Fact]
    public void R014_Run_WriteTargetInNonExistentDirectory_ReturnsOneWithWriteErrorOnStderr()
    {
        using var ws = new TempWorkspace();
        var sourcePath = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards()));
        var destPath = ws.WriteFile("dest.fit", FitsTestData.MinimalFitsFile(FitsTestData.ProcessedCards()));
        var badOutput = ws.FilePath(Path.Combine("no-such-subfolder", "out.fit"));

        var result = Run(sourcePath, destPath, "-o", badOutput);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Eroare la scriere", result.Stderr);
    }

    [Fact]
    public void R002_Run_BadArguments_ReturnsTwoUsageOnStdoutErrorOnStderr()
    {
        var result = Run("only-one-arg");

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Sunt necesare exact doua fisiere", result.Stderr);
        Assert.Contains("copywcs <sursa.fit>", result.Stdout);
    }

    [Fact]
    public void R003_Run_HelpFlag_ReturnsZeroUsageOnStdoutNoStderrContent()
    {
        var result = Run("-h");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("copywcs <sursa.fit>", result.Stdout);
        Assert.Equal("", result.Stderr);
    }
}
