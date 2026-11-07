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

namespace LightsCheck.Tests;

/// <summary>
/// The console's whole output on a synthetic session, compared with the text under Golden/.
/// Regenerate with LIGHTSCHECK_UPDATE_GOLDEN=1 after an intended output change, then read the
/// diff line by line (the kit's rule "No test edited to pass"). The version line and the
/// session path are scrubbed, since they differ per build and per machine.
/// </summary>
[Collection("golden")]
public class GoldenOutputTests
{
    private static string GoldenDir => Path.Combine(TestData.FindRepoRoot(), "tests", "LightsCheck.Tests", "Golden");

    [Fact]
    public void Inventory_MatchesTheGoldenText()
    {
        using SessionFixture s = new SessionFixture().WithLights(5, biases: 3);
        File.WriteAllBytes(Path.Combine(s.LightsDir, "DSC_0006.fit"), new byte[100]);
        (int code, string stdout, string stderr) = Cli.Run(s.Root, "--inventory");

        string actual = Scrub(stdout, s.Root) + "--- stderr ---\n" + stderr + "--- exit " + code + " ---\n";
        Compare("inventory.txt", actual);
    }

    [Fact]
    public void Frame_MatchesTheGoldenText()
    {
        using SessionFixture s = new SessionFixture().WithLights(1, withStars: true);
        (int code, string stdout, string stderr) = Cli.Run(s.Root, "--frame", "1");
        Compare("frame.txt", Scrub(stdout, s.Root) + "--- stderr ---\n" + stderr + "--- exit " + code + " ---\n");
    }

    [Fact]
    public void Series_MatchesTheGoldenText()
    {
        using SessionFixture s = new SessionFixture().WithStarSeries(SessionFixture.Pattern, biases: 2);
        (int code, string stdout, string stderr) = Cli.Run(s.Root, "--threads", "2");
        // The progress lines on stderr arrive in the order the threads finish: not golden material.
        Assert.Contains("cadrul 8 / 8", stderr, StringComparison.Ordinal);
        Compare("series.txt", Scrub(stdout, s.Root) + "--- stderr ---\n(progresul, omis)\n--- exit " + code + " ---\n");
    }

    [Fact]
    public void SeriesWithEveryClass_MatchesTheGoldenText()
    {
        // With darks, so the other branch of the `pattern fix` row's darks clause (R-042, R-046)
        // is pinned beside series.txt's; the measured pattern itself is pinned by R046 in
        // SeriesTests, since hot pixels on the lights would change the frame figures here.
        using SessionFixture s = new SessionFixture().WithStarSeries(SessionFixture.ClassesPattern, biases: 2, darks: 2);
        File.WriteAllBytes(Path.Combine(s.LightsDir, "DSC_0009.fit"), new byte[100]);
        (int code, string stdout, string stderr) = Cli.Run(s.Root, "--threads", "2");
        Assert.Contains("cadrul 13 / 13", stderr, StringComparison.Ordinal);
        Compare("series-classes.txt", Scrub(stdout, s.Root) + "--- stderr ---\n(progresul, omis)\n--- exit " + code + " ---\n");
    }

    [Fact]
    public void Help_MatchesTheGoldenText()
    {
        (int code, string stdout, string stderr) = Cli.Run("--help");
        Compare("help.txt", Scrub(stdout, "") + "--- stderr ---\n" + stderr + "--- exit " + code + " ---\n");
    }

    internal static string Scrub(string text, string root)
    {
        string[] lines = text.Split('\n');
        if (lines.Length > 0 && lines[0].StartsWith("lightscheck ", StringComparison.Ordinal))
        {
            lines[0] = "lightscheck <versiune>";
        }

        string joined = string.Join("\n", lines);
        return root.Length == 0 ? joined : joined.Replace(root, "<sesiune>", StringComparison.Ordinal);
    }

    internal static void Compare(string name, string actual)
    {
        string path = Path.Combine(GoldenDir, name);
        if (Environment.GetEnvironmentVariable("LIGHTSCHECK_UPDATE_GOLDEN") == "1")
        {
            Directory.CreateDirectory(GoldenDir);
            File.WriteAllText(path, actual);
            return;
        }

        Assert.True(File.Exists(path), $"golden lipsa: {path}; genereaza-l cu LIGHTSCHECK_UPDATE_GOLDEN=1");
        Assert.Equal(File.ReadAllText(path).ReplaceLineEndings("\n"), actual);
    }
}
