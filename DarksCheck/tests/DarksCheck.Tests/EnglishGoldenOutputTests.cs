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

using System.Text.RegularExpressions;

namespace DarksCheck.Tests;

/// <summary>
/// The English console (R-093, ADR 0021): each scenario of <see cref="GoldenOutputTests"/> run with
/// <c>--lang en</c> is compared with its English golden, <c>Golden/en_*.txt</c>, and with the same
/// scenario run with <c>--lang ro</c>: the same exit code, the same numbers the same number of
/// times, and none of a list of Romanian words. The Romanian goldens stay those of
/// <see cref="GoldenOutputTests"/>. Regenerate with DARKSCHECK_UPDATE_GOLDEN=1, then read every line.
/// </summary>
public partial class EnglishGoldenOutputTests
{
    private static string GoldenDir => Path.Combine(TestData.FindRepoRoot(), "tests", "DarksCheck.Tests", "Golden");

    // Words and fragments that only Romanian text has. Whole words, case-sensitive; "e" is left out
    // since English writes electrons as "e-".
    private static readonly string[] RomanianWords =
    [
        "si", "sau", "din", "pe", "nu", "cu", "fara", "de", "la", "un", "o", "al", "ale", "lui", "sunt", "este",
        "cadru", "cadre", "grade", "sesiune", "sesiunii", "partener", "partenerului", "eticheta", "biblioteca",
        "lipsa", "fierbinti", "morti", "scurgeri", "scurgere", "lumina", "deriva", "zgomot", "platou", "platoul", "sfarsit", "sfarsitul", "seriei", "gaura", "restul",
        "sarit", "sarite", "raport", "raportul", "verificarea", "tabel", "harta", "hartile", "folosire", "Folosire",
        "NEMASURAT", "ATENTIE", "REFA", "EROARE", "TABEL", "SCURGERI", "DERIVA", "ZGOMOT", "PIXELI", "EXPLICATII", "SCARA",
    ];

    [Fact]
    public void R093_Inventory_InEnglish()
    {
        using SessionFixture s = new SessionFixture().WithDarks(3, biases: 2, lights: 2);
        File.WriteAllBytes(Path.Combine(s.DarksDir, "DSC_0004.fit"), new byte[100]);

        Check("en_inventory.txt", false, [s.Root, "--inventory"], s.Root, null);
    }

    [Fact]
    public void R093_Darks_InEnglish()
    {
        using SessionFixture s = new SessionFixture().WithDarkSeries(4, i => new SyntheticDark { Seed = 100 + i, Hot = SyntheticDark.OnePerLevel, HotFactor = 0.9 + 0.2 * i / 3 }, biases: 3);

        Check("en_darks.txt", true, [s.Root, "--threads", "2"], s.Root, null);
    }

    [Fact]
    public void R093_Redo_InEnglish()
    {
        using SessionFixture s = new SessionFixture().WithDarkSeries(12, i => new SyntheticDark { Seed = 100 + i, Hot = SyntheticLight.Lattice, HotFactor = 0.35 + 1.45 * Math.Exp(-i / 6.0) });

        Check("en_redo.txt", true, [s.Root, "--threads", "2"], s.Root, null);
    }

    [Fact]
    public void R093_Lights_InEnglish()
    {
        using SessionFixture s = new SessionFixture()
            .WithDarkSeries(4, i => new SyntheticDark { Seed = 100 + i, Hot = SyntheticLight.Lattice })
            .WithLights(5, i => new SyntheticLight { Seed = 300 + i, Hot = SyntheticLight.Lattice, HotFactor = 1.3, Stars = SyntheticLight.ThreeStars });
        using SessionFixture p = new SessionFixture().WithDarkSeries(3, i => new SyntheticDark { Seed = 700 + i, Hot = SyntheticLight.Lattice, HotFactor = 2 }, inSubfolder: false);

        Check("en_lights.txt", true, [s.Root, "--threads", "2", "--against", p.Root], s.Root, p.Root);
    }

    [Fact]
    public void R093_LightsOnly_InEnglish()
    {
        using SessionFixture s = new SessionFixture()
            .WithLights(3, i => new SyntheticLight { Seed = 300 + i, Hot = SyntheticLight.Lattice, HotFactor = 1.0, Stars = SyntheticLight.ThreeStars })
            .WithBiases(2);
        using SessionFixture p = new SessionFixture().WithDarkSeries(4, i => new SyntheticDark { Seed = 700 + i, Hot = SyntheticLight.Lattice, HotFactor = 2 }, inSubfolder: false);

        Check("en_lights-only.txt", true, [s.Root, "--threads", "2", "--against", p.Root], s.Root, p.Root);
    }

    [Fact]
    public void R093_LightsFrom_InEnglish()
    {
        using SessionFixture s = new SessionFixture().WithDarkSeries(4, i => new SyntheticDark { Seed = 100 + i, Hot = SyntheticLight.Lattice }, inSubfolder: false);
        using SessionFixture other = new SessionFixture()
            .WithLights(3, i => new SyntheticLight { Seed = 300 + i, Hot = SyntheticLight.Lattice, HotFactor = 1.3, Stars = SyntheticLight.ThreeStars });

        Check("en_lights-from.txt", true, [s.Root, "--threads", "2", "--lights", other.Root], s.Root, null, other.Root);
    }

    [Fact]
    public void R093_Help_InEnglish()
    {
        Check("en_help.txt", false, ["--help"], "", null);
    }

    /// <summary>
    /// Runs the scenario in both languages, compares the English run with its golden and with the
    /// Romanian run.
    /// </summary>
    private static void Check(string golden, bool progressOmitted, string[] args, string root, string? partner, string? lights = null)
    {
        (int en, string enOut, string enErr) = Cli.Run([.. args, "--lang", "en"]);
        (int ro, string roOut, string roErr) = Cli.Run([.. args, "--lang", "ro"]);

        string enText = Scrub(enOut, root, partner, lights) + "--- stderr ---\n" + (progressOmitted ? "(progress, omitted)\n" : enErr) + "--- exit " + en + " ---\n";
        string roText = Scrub(roOut, root, partner, lights) + (progressOmitted ? "" : Scrub(roErr, root, partner, lights));
        Compare(golden, enText);

        Assert.Equal(ro, en);
        Assert.Equal(Numbers(roText), Numbers(Scrub(enOut, root, partner, lights) + (progressOmitted ? "" : Scrub(enErr, root, partner, lights))));
        string[] found = RomanianWords.Where(w => Regex.IsMatch(enText, @"(?<![\w-])" + Regex.Escape(w) + @"(?![\w-])")).ToArray();
        Assert.True(found.Length == 0, "Romanian words in the English output: " + string.Join(", ", found));
    }

    /// <summary>Every number of the text, in order of value, so a reordered sentence still matches.</summary>
    private static List<string> Numbers(string text) => [.. NumberPattern().Matches(text).Select(m => m.Value).Order(StringComparer.Ordinal)];

    /// <summary>The version line and the folders, which differ per build and per machine.</summary>
    private static string Scrub(string text, string root, string? partner, string? lights = null)
    {
        string[] lines = text.Split('\n');
        if (lines.Length > 0 && lines[0].StartsWith("darkscheck ", StringComparison.Ordinal))
        {
            lines[0] = "darkscheck <version>";
        }

        string joined = string.Join("\n", lines);
        if (partner is not null)
        {
            joined = joined.Replace(partner, "<partner>", StringComparison.Ordinal).Replace(Path.GetFileName(partner), "<partner>", StringComparison.Ordinal);
        }

        if (lights is not null)
        {
            joined = joined.Replace(lights, "<lights>", StringComparison.Ordinal).Replace(Path.GetFileName(lights), "<lights>", StringComparison.Ordinal);
        }

        return root.Length == 0 ? joined : joined.Replace(root, "<session>", StringComparison.Ordinal).Replace(Path.GetFileName(root), "<session>", StringComparison.Ordinal);
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

    [GeneratedRegex(@"\d+(?:\.\d+)?")]
    private static partial Regex NumberPattern();
}
