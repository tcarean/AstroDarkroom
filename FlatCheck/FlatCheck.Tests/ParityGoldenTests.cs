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

namespace FlatCheck.Tests;

/// <summary>
/// The 43 scenarios of the retired Python cross-check, pinned as golden files.
///
/// Until 2026-09-26 crosscheck.py ran flatcheck.py and the console over the same synthetic
/// FITS (<see cref="ParityFixtures"/>) and compared every number; on that day the two agreed
/// on all 43 scenarios (0 numeric differences, ADR 0047), and the prototype was frozen. The
/// texts in Golden/parity_*.txt were captured from the console on that day, so they carry the
/// numbers the independent implementation confirmed. That is what makes them different from
/// the main set in <see cref="GoldenOutputTests"/>, which pins behaviour without confirming
/// it: a re-approval here (FLATCHECK_UPDATE_GOLDEN=1) is a decision that the C# alone is now
/// right, and the diff is read with that in mind.
///
/// Keep all 43. They are not to be trimmed like the main set: each one was chosen to reach a
/// branch the main goldens miss (a residual band, a BITPIX, a non-square frame, an exposure
/// band boundary, a series verdict), and the coverage they add is listed in
/// docs/design/python-parity.md. A new scenario is added the same way as a golden one
/// (docs/design/process.md, "Adding a parity golden scenario").
///
/// The exit code is written as the last line of each golden, so one file pins the whole
/// contract of a run.
/// </summary>
public class ParityGoldenTests : IClassFixture<ParityGoldenTests.Fixtures>
{
    /// <summary>One workspace with every fixture, written once for the whole class.</summary>
    public sealed class Fixtures : IDisposable
    {
        internal TempWorkspace Ws { get; } = new();

        public Fixtures() => ParityFixtures.WriteAll(Ws.Dir);

        public void Dispose() => Ws.Dispose();
    }

    readonly Fixtures fx;

    public ParityGoldenTests(Fixtures fx) => this.fx = fx;

    /// <summary>
    /// The scenario table, name and arguments exactly as SCENARIOS in crosscheck.py had them.
    /// The first argument is always the input (a file or a series folder) and is made absolute;
    /// the rest are passed as they are, so "--pattern pp_light_0000?.fit" stays a pattern.
    /// </summary>
    public static TheoryData<string, string[]> Scenarios => new()
    {
        { "structure clean", new[] { "clean.fit" } },
        { "structure gradient", new[] { "grad.fit" } },
        { "structure diagonal", new[] { "diag.fit" } },
        { "structure band BUN", new[] { "grad_bun.fit" } },
        { "structure band REFA", new[] { "grad_refa.fit" } },
        { "structure uniform", new[] { "uniform.fit" } },
        { "structure mild vign", new[] { "vign_mild.fit" } },
        { "structure heavy vign", new[] { "vign_heavy.fit" } },
        { "structure pedestal", new[] { "pedestal.fit" } },
        { "structure wide 768x512", new[] { "wide.fit" } },
        { "structure tall 512x768", new[] { "tall.fit" } },
        { "structure float32", new[] { "float32.fit" } },
        { "structure int32", new[] { "int32.fit" } },
        { "structure float 0..1", new[] { "float01.fit" } },
        { "structure dust shadow", new[] { "dust.fit" } },

        { "ratio clean/gradient", new[] { "clean.fit", "--ratio", "grad.fit" } },
        { "ratio clean/clean", new[] { "clean.fit", "--ratio", "clean.fit" } },
        { "ratio mild/heavy", new[] { "vign_mild.fit", "--ratio", "vign_heavy.fit" } },
        { "ratio wide/wide", new[] { "wide.fit", "--ratio", "wide.fit" } },

        { "light undercorrected", new[] { "clean.fit", "--light" } },
        { "light overcorrected", new[] { "overcorrected.fit", "--light" } },
        { "light pure sky plane", new[] { "skyplane.fit", "--light" } },
        { "light uniform", new[] { "uniform.fit", "--light" } },
        { "light nebulosity", new[] { "nebula.fit", "--light" } },
        { "light wide", new[] { "wide.fit", "--light" } },
        { "light too wide field", new[] { "widefield.fit", "--light" } },
        { "light --fov override", new[] { "widefield.fit", "--light", "--fov", "10" } },

        // --light pe serie: enumerarea, ordinea si ramura de verdict, pe amandoua folderele.
        { "light series stable", new[] { "serie_stabil", "--light", "--fov", "10" } },
        { "light series sweeping", new[] { "serie_baleiaj", "--light", "--fov", "10" } },
        { "light series pattern", new[] { "serie_stabil", "--light", "--fov", "10", "--pattern", "pp_light_0000?.fit" } },

        { "expo cfa", new[] { "raw_cfa.fit", "--expo" } },
        { "expo cfa BAYERPAT", new[] { "raw_bayerpat.fit", "--expo" } },
        { "expo cfa BOTTOM-UP", new[] { "raw_bayerpat_bottomup.fit", "--expo" } },
        { "expo cfa --sky", new[] { "raw_cfa.fit", "--expo", "--sky" } },
        { "expo 12-bit manual", new[] { "raw12bit.fit", "--expo", "--sat", "4095", "--black", "150" } },
        { "expo debayered", new[] { "clean.fit", "--expo" } },
        { "expo 25%", new[] { "expo25.fit", "--expo", "--sat", "4095" } },
        { "expo 30%", new[] { "expo30.fit", "--expo", "--sat", "4095" } },
        { "expo 50%", new[] { "expo50.fit", "--expo", "--sat", "4095" } },
        { "expo 65%", new[] { "expo65.fit", "--expo", "--sat", "4095" } },
        { "expo 81%", new[] { "expo81.fit", "--expo", "--sat", "4095" } },
        { "expo 25% --sky", new[] { "expo25.fit", "--expo", "--sky", "--sat", "4095" } },
        { "expo 65% --sky", new[] { "expo65.fit", "--expo", "--sky", "--sat", "4095" } },
    };

    /// <summary>"expo 25% --sky" -> "expo_25_sky": the golden file name, unique per scenario.</summary>
    internal static string Slug(string name)
        => Regex.Replace(name.ToLowerInvariant(), "[^a-z0-9]+", "_").Trim('_');

    [Theory]
    [MemberData(nameof(Scenarios))]
    public void R098_EveryScenarioPrintsWhatThePrototypeConfirmed(string name, string[] args)
    {
        string slug = Slug(name);
        var full = new List<string> { fx.Ws.PathTo(args[0]) };
        full.AddRange(args.Skip(1));
        full.Add("-o");
        full.Add(fx.Ws.PathTo(slug + ".png"));

        var (output, exit) = GoldenOutputTests.RunCli(fx.Ws, full.ToArray());

        GoldenOutputTests.AssertGolden("parity_" + slug, output + "[exit " + exit + "]\n");
    }

    [Fact]
    public void TheTableHasTheFortyThreeScenariosOfTheCrossCheckAndNoTwoShareAGolden()
    {
        var slugs = Scenarios.Select(row => Slug((string)row[0])).ToList();

        Assert.Equal(43, slugs.Count);
        Assert.Equal(slugs.Count, slugs.Distinct().Count());
    }
}
