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
using LightsCheck.Cli;
using LightsCheck.Core;

namespace LightsCheck.Tests;

/// <summary>
/// The <c>--lang</c> option: how it is read, its default, a value it does not know, and the
/// arrow options' English names (R-060, R-061, R-064).
/// </summary>
public class LanguageTests
{
    [Fact]
    public void R060_LangIsReadInBothFormsAnywhereInAnyCaseTheLastCounting()
    {
        Assert.Equal(UiLanguage.Romanian, LanguageArgument.Extract(["--lang", "ro"]).Language);
        Assert.Equal(UiLanguage.Romanian, LanguageArgument.Extract(["--lang=ro"]).Language);
        Assert.Equal(UiLanguage.English, LanguageArgument.Extract(["x", "--LANG=EN", "--frame", "2"]).Language);
        Assert.Equal(UiLanguage.English, LanguageArgument.Extract(["--lang", "ro", "x", "--lang=en"]).Language);
        Assert.Null(LanguageArgument.Extract(["x"]).Language);
        Assert.Null(LanguageArgument.Extract(["x"]).BadValue);

        LanguageArgument a = LanguageArgument.Extract(["--frame", "2", "--lang", "ro", "folder"]);
        Assert.Equal(["--frame", "2", "folder"], a.Rest);
    }

    [Fact]
    public void R060_TheUsageAndTheParseErrorsAreInTheChosenLanguage()
    {
        (int code, string help, _) = Cli.Run("--lang", "en", "--help");
        Assert.Equal(0, code);
        Assert.Contains("Usage: lightscheck <session folder>", help, StringComparison.Ordinal);
        Assert.Contains("--lang en|ro", help, StringComparison.Ordinal);
        Assert.Contains("This program comes with ABSOLUTELY NO WARRANTY", help, StringComparison.Ordinal);

        (_, string ro, _) = Cli.Run("--help", "--lang=ro");
        Assert.Contains("Folosire: lightscheck <folder sesiune>", ro, StringComparison.Ordinal);

        // A --lang alone leaves an empty command line: the usage, in that language.
        (int bare, string bareHelp, _) = Cli.Run("--lang", "ro");
        Assert.Equal(0, bare);
        Assert.Contains("Folosire:", bareHelp, StringComparison.Ordinal);

        (int enCode, _, string enErr) = Cli.Run("--lang", "en", "x", "--frame", "0");
        Assert.Equal(1, enCode);
        Assert.Equal("ERROR: --frame expects the number of the frame (1 = the first in lights\\)\n", enErr);
        (_, _, string roErr) = Cli.Run("--lang", "ro", "x", "--frame", "0");
        Assert.Equal("EROARE: --frame cere numarul cadrului (1 = primul din lights\\)\n", roErr);
    }

    [Fact]
    public void R060_ARunInEnglishWordsTheErrorsOfTheCore()
    {
        using TempWorkspace ws = new();
        string missing = Path.Combine(ws.Dir, "nope");
        (int code, _, string stderr) = Cli.Run("--lang", "en", missing);
        Assert.Equal(1, code);
        Assert.Equal($"ERROR: cannot find the folder {missing}\n", stderr);
    }

    [Fact]
    public void R060_TheFilesOfOutFollowTheRunsLanguageUnderTheirEnglishNames()
    {
        using SessionFixture s = new SessionFixture().WithStarSeries(SessionFixture.Pattern);
        using TempWorkspace ws = new();
        (int code, string stdout, _) = Cli.Run(s.Root, "--threads", "2", "--out", ws.Dir, "--lang", "en");
        Assert.Equal(0, code);
        Assert.Contains("    -> written: " + Path.Combine(ws.Dir, "lightscheck-frames.csv"), stdout, StringComparison.Ordinal);

        string[] csv = File.ReadAllLines(Path.Combine(ws.Dir, "lightscheck-frames.csv"));
        Assert.StartsWith("index,file,time,", csv[0], StringComparison.Ordinal);   // the header is English in both languages
        Assert.Contains(",good,", csv[1], StringComparison.Ordinal);
        Assert.Contains(csv, l => l.Contains(",BAD,", StringComparison.Ordinal) && l.Contains("elongation", StringComparison.Ordinal));
        Assert.StartsWith("lightscheck ", File.ReadAllText(Path.Combine(ws.Dir, "lightscheck-report.txt")), StringComparison.Ordinal);
        Assert.Contains("=== SESSION:", File.ReadAllText(Path.Combine(ws.Dir, "lightscheck-report.txt")), StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(ws.Dir, "lightscheck-rejected.txt")));

        (_, string ro, _) = Cli.Run(s.Root, "--threads", "2", "--out", ws.Dir, "--lang", "ro");
        Assert.Contains("    -> scris: " + Path.Combine(ws.Dir, "lightscheck-frames.csv"), ro, StringComparison.Ordinal);
        Assert.Contains(",bun,", File.ReadAllLines(Path.Combine(ws.Dir, "lightscheck-frames.csv"))[1], StringComparison.Ordinal);
    }

    [Fact]
    public void R060_WithoutLangTheProgramSpeaksEnglish()
    {
        Assert.Equal(UiLanguage.English, Ui.StartupDefault);

        // The real executable, whose Ui.Default no test assembly has touched.
        string exe = Path.Combine(AppContext.BaseDirectory, OperatingSystem.IsWindows() ? "lightscheck.exe" : "lightscheck");
        Assert.True(File.Exists(exe), "the console's executable is not beside the tests: " + exe);
        ProcessStartInfo start = new(exe, "--help") { RedirectStandardOutput = true, UseShellExecute = false };
        using Process p = Process.Start(start)!;
        string stdout = p.StandardOutput.ReadToEnd();
        Assert.True(p.WaitForExit(30_000), "lightscheck.exe --help did not end within 30 s");
        Assert.Equal(0, p.ExitCode);
        Assert.Contains("Usage: lightscheck", stdout, StringComparison.Ordinal);
        Assert.DoesNotContain("Folosire", stdout, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(new[] { "--lang", "fr", "--help" }, "ERROR: --lang expects en or ro, got 'fr'\n")]
    [InlineData(new[] { "--lang=de" }, "ERROR: --lang expects en or ro, got 'de'\n")]
    [InlineData(new[] { "--help", "--lang" }, "ERROR: --lang expects en or ro, and got nothing\n")]
    [InlineData(new[] { "--lang=" }, "ERROR: --lang expects en or ro, and got nothing\n")]
    public void R061_AValueThatNamesNoLanguageStopsTheConsoleInEnglish(string[] args, string expected)
    {
        (int code, string stdout, string stderr) = Cli.Run(args);
        Assert.Equal(1, code);
        Assert.Equal(expected, stderr);
        Assert.Equal(2, stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);   // the banner alone
    }

    [Fact]
    public void R064_TheArrowOptionsTakeTheirEnglishNamesAndWordsInEitherLanguage()
    {
        Assert.Equal(new ArrowKeys(Arrow.Left, Arrow.Down), Options.Parse(["x", "--east", "left", "--north", "down"]).Arrows);
        Assert.Equal(new ArrowKeys(Arrow.Right, Arrow.Up), Options.Parse(["x", "--east", "right", "--north", "up"]).Arrows);
        Assert.Equal(new ArrowKeys(Arrow.Left, Arrow.Down), Options.Parse(["x", "--est", "left", "--north", "jos"]).Arrows);
        using (Ui.Use(UiLanguage.English))
        {
            Assert.Equal("--east expects left or right: the SynScan arrow that moves the mount east", Options.Parse(["x", "--east", "up"]).Error);
            Assert.Equal("--north expects up or down: the SynScan arrow that moves the mount north", Options.Parse(["x", "--north", "left"]).Error);
            Assert.Equal("--north expects a value", Options.Parse(["x", "--north"]).Error);
        }

        (_, string help, _) = Cli.Run("--lang", "en", "--help");
        Assert.Contains("--east left|right", help, StringComparison.Ordinal);
        Assert.Contains("--north up|down", help, StringComparison.Ordinal);
        Assert.DoesNotContain("--est ", help, StringComparison.Ordinal);

        using SessionFixture s = new SessionFixture().WithStarSeries(SessionFixture.Pattern);
        // The fixture's lights are FITS: the Veil solve with its y column mirrored (ADR 0023).
        string[] wcs =
        [
            FitsTestData.Card("CTYPE1", "'RA---TAN'"), FitsTestData.Card("CTYPE2", "'DEC--TAN'"),
            FitsTestData.Card("CDELT1", "-0.00172"), FitsTestData.Card("CDELT2", "0.00172"),
            FitsTestData.Card("PC1_1", "0.0037"), FitsTestData.Card("PC1_2", "0.99999"),
            FitsTestData.Card("PC2_1", "0.99999"), FitsTestData.Card("PC2_2", "-0.0037"),
        ];
        File.WriteAllBytes(Path.Combine(s.Root, "result.fit"), FitsTestData.BuildFits(8, 8, (x, y) => 1, extraCards: wcs));
        (_, string english, _) = Cli.Run(s.Root, "--threads", "2", "--east", "left", "--north", "down");
        (_, string romanian, _) = Cli.Run(s.Root, "--threads", "2", "--est", "stanga", "--nord", "jos");
        Assert.Equal(romanian, english);   // the tests' default language is Romanian for both runs
        Assert.Contains("RA 15 px → V", english, StringComparison.Ordinal);

        // In English the west letter is W.
        (_, string inEnglish, _) = Cli.Run(s.Root, "--threads", "2", "--east", "left", "--lang", "en");
        Assert.Contains("RA 15 px → W", inEnglish, StringComparison.Ordinal);
    }
}
