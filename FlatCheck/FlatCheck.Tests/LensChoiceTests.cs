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

using NLog;

namespace FlatCheck.Tests;

/// <summary>
/// R-121, the core's half: a lens chosen among the candidates found by the raw's range alone
/// (a manual lens, whose ID no profile carries) narrows the structure comparison to that lens,
/// keeps the full candidate list on the identity, and makes the report say which lens was
/// chosen and which were left out. The window's picker sits on top of this; the console never
/// builds a query with a key.
/// </summary>
public class LensChoiceTests
{
    /// <summary>A D3300 raw with a manual lens: the "non-CPU lens" ID 74, no name, the range
    /// 135 mm f/2.0 from the camera's menu, shot at f/2.8.</summary>
    static LoadedFrame Range135Raw() => new LoadedFrame
    {
        Header = new Dictionary<string, string>(),
        FocalMm = 135,
        FNumber = 2.8,
        Camera = "NIKON D3300",
        LensId = 74,
        LensMinFocalMm = 135,
        LensMaxFocalMm = 135,
        LensMaxAperture = 2,
    };

    /// <summary>The master's header, as the window reads it without pixels.</summary>
    static LoadedFrame Master() => new LoadedFrame
    {
        Header = new Dictionary<string, string> { ["SIMPLE"] = "T" },
        FocalMm = 135,
        Camera = "Nikon D3300",
    };

    /// <summary>A radial master's five tests and the catalog with the two 135 f/2 lenses.</summary>
    static (StructureResult Structure, ProfileSources Sources) Setup(TempWorkspace ws)
    {
        var sources = LensProfileTestData.Sources(ws, rangePair: true);
        var r = StructureAnalyzer.Analyze(TestImages.From(256, 256, FitsTestData.Radial(256, 256)));
        return (r, sources);
    }

    /// <summary>The catalog key of the lens with that name.</summary>
    static string KeyOf(ProfileSources sources, string name)
        => sources.Catalog.Lenses.First(l => l.Name == name).Key;

    /// <summary>The comparison on the range raw, with or without a chosen key and a lens text.</summary>
    static StructureOutcome Compare(StructureResult r, ProfileSources sources, string key = null, string text = null)
        => StructureRun.Compare(r, Master(), new StructureQuery
        {
            Sources = sources,
            RawPath = "DSC_0001.NEF",
            LoadInfo = _ => Range135Raw(),
            LensKey = key,
            LensText = text,
        });

    /// <summary>The console's report block for the outcome.</summary>
    static string Report(StructureOutcome o)
    {
        var w = new StringWriter();
        StructureReport.Write(o, "pp_flat_stacked.fit", w);
        return w.ToString();
    }

    /// <summary>
    /// R-121: without a choice the range gives both lenses, in catalog order (Adobe first), the
    /// nearest's verdict, no chosen lens, and the report's sentence to read one's own row.
    /// </summary>
    [Fact]
    public void R121_WithoutAChoiceTheRangeGivesBothLensesAndTheReportSaysToReadOnesOwnRow()
    {
        using var ws = new TempWorkspace();
        var (r, sources) = Setup(ws);

        StructureOutcome o = Compare(r, sources);

        Assert.True(o.Compared, o.Profiles.Gap.ToString());
        Assert.True(o.Shot.CandidatesByRangeOnly);
        Assert.Equal(2, o.Shot.Candidates.Count);
        Assert.Equal(2, o.Profiles.Profiles.Count);
        Assert.Equal(LensProfileTestData.Nikkor135Dc, o.Profiles.Profiles[0].Profile.Lens.Name);
        Assert.Equal(LensProfileTestData.Samyang135, o.Profiles.Profiles[1].Profile.Lens.Name);
        Assert.Null(o.Shot.ChosenLens);
        Assert.Contains(StructureReport.RangeOnlyNote(o.Shot).Trim(), Report(o));
    }

    /// <summary>
    /// R-121: a chosen key leaves that lens's row alone, the comparison's verdict is its
    /// profile's, the identity keeps every candidate (the picker needs them) and records the
    /// choice, and the report names the lens chosen and the one left out instead of the
    /// range-only sentence.
    /// </summary>
    [Fact]
    public void R121_AChosenLensIsTheOnlyRowAndTheReportNamesItAndTheLensesLeftOut()
    {
        using var ws = new TempWorkspace();
        var (r, sources) = Setup(ws);
        string key = KeyOf(sources, LensProfileTestData.Samyang135);

        StructureOutcome o = Compare(r, sources, key);

        Assert.True(o.Compared, o.Profiles.Gap.ToString());
        ProfileDepth row = Assert.Single(o.Profiles.Profiles);
        Assert.Equal(LensProfileTestData.Samyang135, row.Profile.Lens.Name);
        Assert.Equal(row.Verdict, o.Profiles.Verdict);
        Assert.Equal(key, o.Shot.ChosenLens.Key);
        Assert.Equal(2, o.Shot.Candidates.Count);
        Assert.True(o.Shot.CandidatesByRangeOnly);
        string report = Report(o);
        Assert.Contains(StructureReport.ChosenLensNote(o.Shot).Trim(), report);
        Assert.Contains(LensProfileTestData.Nikkor135Dc, StructureReport.ChosenLensNote(o.Shot));
        Assert.DoesNotContain(StructureReport.RangeOnlyNote(o.Shot).Trim(), report);
        Assert.NotEmpty(StructureReport.LensChoicePrompt(o.Shot));
    }

    /// <summary>
    /// R-121: a key that matches no candidate (the raw changed, the profile gone) is ignored:
    /// every candidate is compared, nothing is chosen, and a Debug line says so.
    /// </summary>
    [Fact]
    public void R121_AKeyAmongNoCandidateComparesEveryCandidateAndSaysSoInTheLog()
    {
        using var ws = new TempWorkspace();
        var (r, sources) = Setup(ws);
        using var capture = LogCapture.Start();

        StructureOutcome o = Compare(r, sources, "Adobe|NIKON CORPORATION|Nobody 135mm f/2|NIKON D3");

        Assert.True(o.Compared, o.Profiles.Gap.ToString());
        Assert.Equal(2, o.Profiles.Profiles.Count);
        Assert.Null(o.Shot.ChosenLens);
        Assert.Contains(capture.Events, e => e.Level == LogLevel.Debug && e.Message.Contains("Nobody 135mm f/2") && e.Message.Contains("not among"));
        Assert.Contains(StructureReport.RangeOnlyNote(o.Shot).Trim(), Report(o));
    }

    /// <summary>R-121: the console's lens text, when given, wins over the key.</summary>
    [Fact]
    public void R121_TheConsolesLensTextWinsOverTheKey()
    {
        using var ws = new TempWorkspace();
        var (r, sources) = Setup(ws);

        StructureOutcome o = Compare(r, sources, KeyOf(sources, LensProfileTestData.Samyang135), text: LensProfileTestData.Nikkor135Dc);

        Assert.True(o.Compared, o.Profiles.Gap.ToString());
        ProfileDepth row = Assert.Single(o.Profiles.Profiles);
        Assert.Equal(LensProfileTestData.Nikkor135Dc, row.Profile.Lens.Name);
        Assert.Null(o.Shot.ChosenLens);
    }
}
