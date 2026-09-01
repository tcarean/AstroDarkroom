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
    public void Run_SuccessfulInPlaceCopy_UpdatesDestReturnsZeroPreservesPixelData()
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
    public void Run_SuccessfulCopyViaOutputOption_WritesNewFileLeavesOriginalDestUntouched()
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
    public void Run_CopiedCards_AreByteIdenticalToSourceNotJustValueEqual()
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

    [Fact]
    public void Run_DimensionMismatchWithoutForce_ReturnsOneWritesNothing()
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
    public void Run_DimensionMismatchWithForce_WarnsButSucceeds()
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
    public void Run_SourceMissingCtype1_PrintsWarningButContinues()
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
    public void Run_DryRun_ListsCardsAndLeavesDestByteForByteUnchanged()
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

    [Fact]
    public void Run_NoOpticalFlag_OmitsOpticalKeysKeepsWcsKeys()
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
    public void Run_WithoutNoOpticalFlag_IncludesOpticalKeys()
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
    public void Run_SourceFileMissing_ReturnsOneWithReadErrorOnStderr()
    {
        using var ws = new TempWorkspace();
        var destPath = ws.WriteFile("dest.fit", FitsTestData.MinimalFitsFile(FitsTestData.ProcessedCards()));
        var missingSource = ws.FilePath("does-not-exist.fit");

        var result = Run(missingSource, destPath);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Nu pot citi sursa", result.Stderr);
    }

    [Fact]
    public void Run_DestFileMissing_ReturnsOneWithReadErrorOnStderr()
    {
        using var ws = new TempWorkspace();
        var sourcePath = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards()));
        var missingDest = ws.FilePath("does-not-exist.fit");

        var result = Run(sourcePath, missingDest);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Nu pot citi destinatia", result.Stderr);
    }

    [Fact]
    public void Run_NoRelevantKeysInSource_ReturnsOneWritesNothing()
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
    public void Run_WriteTargetInNonExistentDirectory_ReturnsOneWithWriteErrorOnStderr()
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
    public void Run_BadArguments_ReturnsTwoUsageOnStdoutErrorOnStderr()
    {
        var result = Run("only-one-arg");

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Sunt necesare exact doua fisiere", result.Stderr);
        Assert.Contains("copywcs <sursa.fit>", result.Stdout);
    }

    [Fact]
    public void Run_HelpFlag_ReturnsZeroUsageOnStdoutNoStderrContent()
    {
        var result = Run("-h");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("copywcs <sursa.fit>", result.Stdout);
        Assert.Equal("", result.Stderr);
    }
}
