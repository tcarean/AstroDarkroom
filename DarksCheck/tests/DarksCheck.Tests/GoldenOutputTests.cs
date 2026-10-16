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

namespace DarksCheck.Tests;

/// <summary>
/// The console's whole output on a synthetic session, compared with the text under Golden/.
/// Regenerate with DARKSCHECK_UPDATE_GOLDEN=1 after an intended output change, then read the
/// diff line by line (the kit's rule "No test edited to pass"). The version line and the
/// session path are scrubbed, since they differ per build and per machine.
/// </summary>
public class GoldenOutputTests
{
    private static string GoldenDir => Path.Combine(TestData.FindRepoRoot(), "tests", "DarksCheck.Tests", "Golden");

    [Fact]
    public void Inventory_MatchesTheGoldenText()
    {
        using SessionFixture s = new SessionFixture().WithDarks(3, biases: 2, lights: 2);
        File.WriteAllBytes(Path.Combine(s.DarksDir, "DSC_0004.fit"), new byte[100]);
        (int code, string stdout, string stderr) = Cli.Run(s.Root, "--inventory");

        string actual = Scrub(stdout, s.Root) + "--- stderr ---\n" + stderr + "--- exit " + code + " ---\n";
        Compare("inventory.txt", actual);
    }

    [Fact]
    public void Darks_MatchesTheGoldenText()
    {
        using SessionFixture s = new SessionFixture().WithDarkSeries(4, i => new SyntheticDark { Seed = 100 + i, Hot = SyntheticDark.OnePerLevel, HotFactor = 0.9 + 0.2 * i / 3 }, biases: 3);
        (int code, string stdout, string stderr) = Cli.Run(s.Root, "--threads", "2");
        // The progress lines on stderr arrive in the order the threads finish: not golden material.
        Assert.Contains("masurare dark-uri: 4 / 4", stderr, StringComparison.Ordinal);
        Compare("darks.txt", Scrub(stdout, s.Root) + "--- stderr ---\n(progresul, omis)\n--- exit " + code + " ---\n");
    }

    [Fact]
    public void Help_MatchesTheGoldenText()
    {
        (int code, string stdout, string stderr) = Cli.Run("--help");
        Compare("help.txt", Scrub(stdout, "") + "--- stderr ---\n" + stderr + "--- exit " + code + " ---\n");
    }

    private static string Scrub(string text, string root)
    {
        string[] lines = text.Split('\n');
        if (lines.Length > 0 && lines[0].StartsWith("darkscheck ", StringComparison.Ordinal))
        {
            lines[0] = "darkscheck <versiune>";
        }

        string joined = string.Join("\n", lines);
        return root.Length == 0 ? joined : joined.Replace(root, "<sesiune>", StringComparison.Ordinal);
    }

    private static void Compare(string name, string actual)
    {
        string path = Path.Combine(GoldenDir, name);
        if (Environment.GetEnvironmentVariable("DARKSCHECK_UPDATE_GOLDEN") == "1")
        {
            Directory.CreateDirectory(GoldenDir);
            File.WriteAllText(path, actual);
            return;
        }

        Assert.True(File.Exists(path), $"golden missing: {path}; generate it with DARKSCHECK_UPDATE_GOLDEN=1");
        Assert.Equal(File.ReadAllText(path).ReplaceLineEndings("\n"), actual);
    }
}
