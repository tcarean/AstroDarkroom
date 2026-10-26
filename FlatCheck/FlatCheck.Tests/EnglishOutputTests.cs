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

using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using static FlatCheck.Tests.GoldenOutputTests;

namespace FlatCheck.Tests;

/// <summary>
/// The console in English (R-103). The scenarios of the main golden set run again with
/// <c>--lang en</c>, pinned as <c>Golden/en_*.txt</c> - the place where the English wording is
/// read and reviewed - and each is checked against the same run in Romanian: the same exit code
/// and the same numbers, the same number of times, so a translation cannot move a figure. The
/// 43 parity scenarios get the number check too, without goldens of their own. The Romanian
/// goldens stay the reference for the figures.
/// </summary>
public class EnglishOutputTests : IClassFixture<ParityGoldenTests.Fixtures>
{
    readonly ParityGoldenTests.Fixtures fx;

    public EnglishOutputTests(ParityGoldenTests.Fixtures fx) => this.fx = fx;

    /// <summary>The main golden scenarios, by the name of their Romanian golden.</summary>
    public static TheoryData<string> Scenarios => new()
    {
        "structure_clean", "structure_gradient", "structure_reshoot",
        "light_undercorrected", "light_rejected_wide_field", "light_series_conclusive", "light_series_undetermined",
        "histeq_cfa", "histeq_mono", "histeq_rainbow", "expo_cfa", "expo_cfa_bayerpat", "expo_cfa_sky", "expo_rejects_debayered",
        "expo_manual_scale", "help", "smooth_clean", "smooth_textured", "smooth_refuses_cube", "smooth_auto_proposed",
        "smooth_auto_no_texture", "structure_with_profiles", "structure_lens_no_fstop", "synthetic_written",
        "synthetic_lensfun", "synthetic_far_request", "synthetic_ambiguous", "lenses", "against_radial",
    };

    /// <summary>
    /// The fixture and the arguments of a scenario, exactly as <see cref="GoldenOutputTests"/>
    /// builds them for the golden of the same name.
    /// </summary>
    static string[] Arguments(string name, TempWorkspace ws)
    {
        const int s = Size;
        string Out() => ws.PathTo("out.png");
        string Cfa(string file, double centre = 34000) => Flat(ws, file, FitsTestData.WithBayerMosaic(FitsTestData.Radial(s, s, center: centre)));
        string Bayer(string file) => ws.WriteFile(file, FitsTestData.BuildFits(s, s,
            FitsTestData.WithBayerMosaic(FitsTestData.Radial(s, s, center: 34000)), extraCards: FitsTestData.BayerCards("RGGB")));
        var radial = FitsTestData.Radial(s, s);

        switch (name)
        {
            case "structure_clean": return new[] { Flat(ws, "clean.fit", radial), "-o", Out() };
            case "structure_gradient": return new[] { Flat(ws, "grad.fit", FitsTestData.WithLinearGradient(radial, s, 0.12)), "-o", Out() };
            case "structure_reshoot": return new[] { Flat(ws, "violent.fit", FitsTestData.WithLinearGradient(radial, s, 0.60)), "-o", Out() };
            case "light_undercorrected": return new[] { Flat(ws, "light.fit", radial), "--light", "-o", Out() };
            case "light_rejected_wide_field":
                return new[] { ws.WriteFile("light.fit", FitsTestData.BuildFits(s, s, radial, extraCards: FitsTestData.WcsCards(0.1))),
                               "--light", "-o", Out() };
            case "light_series_conclusive":
                return new[] { SeriesDir(ws, "stabil", Enumerable.Range(0, 6)
                    .Select(i => (Func<int, int, double>)FitsTestData.Radial(s, s, k: 0.44 + 0.002 * i)).ToArray()),
                    "--light", "--fov", "10", "-o", Out() };
            case "light_series_undetermined":
                return new[] { SeriesDir(ws, "baleiaj", Enumerable.Range(0, 6)
                    .Select(i => (Func<int, int, double>)FitsTestData.WithLinearGradient(
                        TestImages.InvertedRadial(s, s, k: -0.5 + 0.2 * i), s, 0.6 - 0.1 * i)).ToArray()),
                    "--light", "--fov", "10", "-o", Out() };
            case "histeq_cfa": return new[] { Bayer("raw_bayerpat.fit"), "--histeq", "-o", Out() };
            case "histeq_mono": return new[] { Flat(ws, "flat.fit", radial), "--histeq", "-o", Out() };
            case "histeq_rainbow": return new[] { Bayer("raw_bayerpat.fit"), "--histeq", "--rainbow", "-o", Out() };
            case "expo_cfa": return new[] { Cfa("raw.fit"), "--expo" };
            case "expo_cfa_bayerpat": return new[] { Bayer("raw_bayerpat.fit"), "--expo" };
            case "expo_cfa_sky": return new[] { Cfa("raw.fit"), "--expo", "--sky" };
            case "expo_rejects_debayered": return new[] { Flat(ws, "debayered.fit", radial), "--expo" };
            case "expo_manual_scale": return new[] { Cfa("raw12bit.fit", 3600), "--expo", "--sat", "4095", "--black", "150" };
            case "help": return Array.Empty<string>();
            case "smooth_clean":
                return new[] { Flat(ws, "clean.fit", radial), "--smooth", ws.PathTo("netezit.fit"), "--scale", "50", "-o", Out() };
            case "smooth_textured":
                return new[] { Flat(ws, "textured.fit", (x, y) => radial(x, y)
                        * (1 + 0.03 * Math.Sin(x / 6.0) * Math.Cos(y / 7.0))
                        * (1 - 0.15 * Math.Exp(-((x - 400) * (x - 400) + (y - 120) * (y - 120)) / (2 * 8.0 * 8.0)))),
                    "--smooth", ws.PathTo("netezit.fit"), "--scale", "50", "-o", Out() };
            case "smooth_refuses_cube":
                return new[] { ws.WriteFile("cube.fit", FitsTestData.BuildFitsCube(64, 64, 3, (x, y, p) => 1000 + p)),
                               "--smooth", ws.PathTo("netezit.fit"), "-o", Out() };
            case "smooth_auto_proposed":
                return new[] { Flat(ws, "textured.fit", (x, y) => radial(x, y) * (1 + 0.03 * Math.Sin(x / 2.0) * Math.Cos(y / 2.3))),
                               "--smooth", ws.PathTo("netezit.fit"), "--scale", "auto", "-o", Out() };
            case "smooth_auto_no_texture":
                return new[] { Flat(ws, "clean.fit", radial), "--smooth", ws.PathTo("netezit.fit"), "--scale", "auto", "-o", Out() };
            case "structure_with_profiles":
                return new[] { Reference(ws, k: 0.6), "--lens", "Tokina", "--fstop", "2.8",
                               "--profiles", LensProfileTestData.ProfilesDir(ws), "-o", Out() };
            case "structure_lens_no_fstop":
                return new[] { Reference(ws), "--lens", "Tokina", "--profiles", LensProfileTestData.ProfilesDir(ws), "-o", Out() };
            case "synthetic_written":
                return new[] { Reference(ws), "--synthetic", ws.PathTo("sintetic.fit"), "--lens", "Tokina AT-X 11-20mm F2.8 PRO DX",
                               "--fstop", "2.8", "--profiles", LensProfileTestData.ProfilesDir(ws) };
            case "synthetic_lensfun":
                return new[] { Reference(ws), "--synthetic", ws.PathTo("s.fit"), "--lens", "Aspherical",
                               "--source", "lensfun", "--fstop", "2.8", "--profiles", LensProfileTestData.ProfilesDir(ws) };
            case "synthetic_far_request":
                return new[] { Reference(ws), "--synthetic", ws.PathTo("s.fit"), "--lens", "Tokina AT-X 11-20mm F2.8 PRO DX",
                               "--focal", "500", "--fstop", "1.0", "--profiles", LensProfileTestData.ProfilesDir(ws) };
            case "synthetic_ambiguous":
                return new[] { Reference(ws), "--synthetic", ws.PathTo("s.fit"), "--lens", "11-20", "--fstop", "2.8",
                               "--profiles", LensProfileTestData.ProfilesDir(ws) };
            case "lenses": return new[] { "--lenses", "--lens", "tokina", "--profiles", LensProfileTestData.ProfilesDir(ws) };
            case "against_radial":
                return new[] { ws.WriteFile("a.fit", FitsTestData.BuildFits(s, s, FitsTestData.Radial(s, s, k: 0.45))), "--against",
                               ws.WriteFile("b.fit", FitsTestData.BuildFits(s, s, FitsTestData.Radial(s, s, k: 0.30))), "-o", Out() };
            default: throw new ArgumentException("no such scenario: " + name);
        }
    }

    /// <summary>Runs a scenario in a fresh workspace, in one language.</summary>
    static (string Output, int Exit) Run(string name, string lang)
    {
        using var ws = new TempWorkspace();
        string[] args = Arguments(name, ws);
        return RunCli(ws, new[] { "--lang", lang }.Concat(args).ToArray());
    }

    [Theory]
    [MemberData(nameof(Scenarios))]
    public void R103_EveryScenarioPrintsTheSameNumbersAndExitCodeInEnglish(string name)
    {
        var en = Run(name, "en");
        var ro = Run(name, "ro");

        AssertGolden("en_" + name, en.Output + "[exit " + en.Exit + "]\n");
        Assert.Equal(ro.Exit, en.Exit);
        AssertSameNumbers(ro.Output, en.Output);
        AssertNoRomanian(en.Output);
    }

    /// <summary>The Romanian run of a scenario is still what its Romanian golden pins: the
    /// <c>--lang ro</c> on the command line changes nothing against the assembly's default.</summary>
    [Theory]
    [InlineData("structure_clean")]
    [InlineData("expo_cfa")]
    [InlineData("help")]
    public void R100_LangRoPrintsTheRomanianGolden(string name)
        => AssertGolden(name, Run(name, "ro").Output);

    [Theory]
    [MemberData(nameof(ParityGoldenTests.Scenarios), MemberType = typeof(ParityGoldenTests))]
    public void R103_EveryParityScenarioPrintsTheSameNumbersInEnglish(string name, string[] args)
    {
        string slug = ParityGoldenTests.Slug(name);
        var full = args.Select(a => File.Exists(fx.Ws.PathTo(a)) || Directory.Exists(fx.Ws.PathTo(a)) ? fx.Ws.PathTo(a) : a).ToList();
        full.Add("-o");
        full.Add(fx.Ws.PathTo("en_" + slug + ".png"));

        var en = RunCli(fx.Ws, new[] { "--lang", "en" }.Concat(full).ToArray());
        string ro = File.ReadAllText(Path.Combine(GoldenFolder(), "parity_" + slug + ".txt")).Replace("\r\n", "\n");

        Assert.EndsWith("[exit " + en.Exit + "]\n", ro);
        // The map's file name differs on purpose (en_ prefix); its digits are the scenario's.
        AssertSameNumbers(Regex.Replace(ro, @"<TMP>/\S+\.png", ""), Regex.Replace(en.Output, @"<TMP>/\S+\.png", "") + "[exit " + en.Exit + "]\n");
        AssertNoRomanian(en.Output);
    }

    /// <summary>The folder of the golden files, beside this source file.</summary>
    static string GoldenFolder([CallerFilePath] string thisFile = "")
        => Path.Combine(Path.GetDirectoryName(thisFile)!, "Golden");

    static readonly Regex Number = new(@"\d+(?:\.\d+)?", RegexOptions.Compiled);

    /// <summary>The numbers of both texts, each counted, are the same.</summary>
    static void AssertSameNumbers(string romanian, string english)
    {
        static List<string> Of(string text) => Number.Matches(text).Select(m => m.Value).OrderBy(v => v, StringComparer.Ordinal).ToList();
        Assert.Equal(Of(romanian), Of(english));
    }

    // Words that are Romanian and never English, with the spaces that make them words.
    static readonly string[] RomanianWords =
    {
        " si ", " sau ", " nu ", " din ", " cu ", " pe ", " de ", " la ", " este ", " fara ", " mai ",
        "stopuri", "eroare", "EROARE", "ATENTIE", "colt", "cadre", "harta", "obiectiv", "vignetare",
        "netezire", "reziduu", "verdictul", "fisier", "diferenta",
    };

    /// <summary>The English text holds none of the Romanian words, outside the workspace paths
    /// (whose folder and file names are the fixtures').</summary>
    static void AssertNoRomanian(string english)
    {
        string text = Regex.Replace(english, @"<TMP>\S*", "");
        foreach (string word in RomanianWords)
            Assert.False(text.Contains(word, StringComparison.Ordinal), $"Romanian \"{word.Trim()}\" in the English output:\n{text}");
    }
}
