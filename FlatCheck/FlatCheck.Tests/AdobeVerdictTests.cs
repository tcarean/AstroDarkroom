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

namespace FlatCheck.Tests;

/// <summary>
/// R-128: when the raw's lens ID or name found the lens and an Adobe file is among the lenses,
/// the depth verdict is an Adobe row's, not the nearest row of all; among the Adobe rows the
/// file whose own lens ID is the raw's comes first. By range alone, with a --lens text (a
/// search), with a lens chosen, or without an Adobe row, the nearest row still decides. The
/// fixture is
/// the Tokina's: Adobe 0.66 stops at the corner, the lensfun entry about 0.2 deeper, so a
/// master deeper than Adobe sits next to lensfun - the owner's panel flats.
/// </summary>
public class AdobeVerdictTests
{
    const int W = 240, H = 160;

    /// <summary>The master's header: a D3300 at the given focal length.</summary>
    static LoadedFrame Master(string focal = "20") => new LoadedFrame
    {
        Header = new Dictionary<string, string> { ["SIMPLE"] = "T", ["FOCALLEN"] = focal, ["INSTRUME"] = "Nikon D3300" },
        FocalMm = double.Parse(focal, System.Globalization.CultureInfo.InvariantCulture),
        Camera = "Nikon D3300",
    };

    /// <summary>The owner's Tokina raw: ID 139, the range 11-20 mm f/2.8, shot at 20 mm f/2.8.</summary>
    static LoadedFrame TokinaRaw() => new LoadedFrame
    {
        Header = new Dictionary<string, string>(),
        FocalMm = 20,
        FNumber = 2.8,
        Camera = "NIKON D3300",
        LensId = 139,
        LensMinFocalMm = 11,
        LensMaxFocalMm = 20,
        LensMaxAperture = 2.8,
    };

    /// <summary>The profile of the first catalog lens that matches, nearest to the focal and f-stop.</summary>
    static LensProfile ProfileOf(ProfileSources sources, Func<LensChoice, bool> which, double focal, double fstop)
        => LensCatalog.Nearest(sources.Catalog.ProfilesOf(sources.Catalog.Lenses.First(which)), focal, fstop);

    /// <summary>The Tokina's Adobe profile for the Nikon mount at 20 mm f/2.8.</summary>
    static LensProfile TokinaAdobe(ProfileSources sources)
        => ProfileOf(sources, l => l.Source == ProfileSource.Adobe && l.Mount == "NIKON CORPORATION", 20, 2.8);

    /// <summary>
    /// The five tests on a master built from a profile's own model raised to a power: 1 is the
    /// model itself, 1.3 falls 30 % deeper in stops.
    /// </summary>
    static StructureResult MasterFrom(LensProfile p, double power)
    {
        Img flat = SyntheticFlatBuilder.Build(p, W, H, 1.523).Flat;
        var img = new Img(H, W);
        for (int i = 0; i < img.D.Length; i++) img.D[i] = Math.Pow(flat.D[i], power) * 20000;
        return StructureAnalyzer.Analyze(img);
    }

    /// <summary>The report row marked as the one that gives the verdict.</summary>
    static ProfileRow Deciding(VignettingComparison c) => Assert.Single(StructureReport.ProfileRows(c), r => r.GivesVerdict);

    /// <summary>
    /// R-128: on the ID path the Adobe row gives the verdict even when the lensfun row is the
    /// nearer one: a master 0.2 stops deeper than Adobe, next to the lensfun entry, reads
    /// ACCEPTABIL from Adobe, not PERFECT from lensfun.
    /// </summary>
    [Fact]
    public void R128_OnTheIdPathTheAdobeRowGivesTheVerdictThoughALensfunRowIsNearer()
    {
        using var ws = new TempWorkspace();
        var sources = LensProfileTestData.Sources(ws);
        var r = MasterFrom(TokinaAdobe(sources), 1.3);
        var shot = LensIdentification.Identify(Master(), TokinaRaw(), sources, rawName: "DSC_0001.NEF");

        VignettingComparison c = VignettingComparer.Analyze(r, shot, sources.Catalog);

        Assert.True(c.Compared, c.Gap.ToString());
        Assert.False(shot.CandidatesByRangeOnly);
        ProfileDepth adobe = Assert.Single(c.Profiles, p => p.Profile.Lens.Source == ProfileSource.Adobe);
        ProfileDepth lensfun = Assert.Single(c.Profiles, p => p.Profile.Lens.Source == ProfileSource.Lensfun);
        // the case the requirement is about: lensfun is nearer, and kinder
        Assert.True(Math.Abs(lensfun.DiffStops) < Math.Abs(adobe.DiffStops), $"lensfun {lensfun.DiffStops:F3}, Adobe {adobe.DiffStops:F3}");
        Assert.True(Stats.Severity(adobe.Verdict) > Stats.Severity(lensfun.Verdict), $"lensfun {lensfun.Verdict}, Adobe {adobe.Verdict}");

        Assert.Equal(adobe.Verdict, c.Verdict);
        Assert.Equal(adobe.Profile.Lens.Key, Deciding(c).Key);
    }

    /// <summary>
    /// R-128: a degenerate Adobe row (a NaN difference) never decides, as NaN never won under the
    /// nearest rule: the comparison falls back to the nearest of the rows that can be read.
    /// </summary>
    [Fact]
    public void R128_ADegenerateAdobeRowNeverDecides()
    {
        using var ws = new TempWorkspace();
        var sources = LensProfileTestData.Sources(ws);
        var r = MasterFrom(TokinaAdobe(sources), 1.3);
        var shot = LensIdentification.Identify(Master(), TokinaRaw(), sources, rawName: "DSC_0001.NEF");
        VignettingComparison read = VignettingComparer.Analyze(r, shot, sources.Catalog);
        ProfileDepth adobe = Assert.Single(read.Profiles, p => p.Profile.Lens.Source == ProfileSource.Adobe);
        ProfileDepth lensfun = Assert.Single(read.Profiles, p => p.Profile.Lens.Source == ProfileSource.Lensfun);
        var degenerate = new ProfileDepth(adobe.Profile, double.NaN, read.MasterStops, adobe.CameraCrop, adobe.Extrapolation);

        var c = new VignettingComparison(read.MasterStops, shot, new[] { degenerate, lensfun }, ComparisonGap.None, null);

        Assert.False(c.AdobeDecides);
        Assert.Same(lensfun, c.Deciding);
        Assert.Equal(lensfun.Verdict, c.Verdict);
    }

    /// <summary>
    /// R-128: the Tokina under its two Adobe names, one optic, the same figures: the file whose
    /// own ID is the raw's 139 (the atx-i CF) gives the verdict, not the AT-X PRO DX, which
    /// carries 139 only as an alternate, and the summary names it.
    /// </summary>
    [Fact]
    public void R128_AmongTheAdobeFilesTheOneWhoseOwnLensIdIsTheRawsGivesTheVerdict()
    {
        using var ws = new TempWorkspace();
        var sources = LensProfileTestData.Sources(ws, atxi: true);
        var r = MasterFrom(TokinaAdobe(sources), 1.0);
        var shot = LensIdentification.Identify(Master(), TokinaRaw(), sources, rawName: "DSC_0001.NEF");

        VignettingComparison c = VignettingComparer.Analyze(r, shot, sources.Catalog);

        Assert.True(c.Compared, c.Gap.ToString());
        var adobe = c.Profiles.Where(p => p.Profile.Lens.Source == ProfileSource.Adobe).ToList();
        Assert.Equal(2, adobe.Count);
        Assert.Equal(adobe[0].DiffStops, adobe[1].DiffStops, 9);
        Assert.Equal(LensProfileTestData.TokinaAtxi, Deciding(c).Lens);
        Assert.Contains(LensProfileTestData.TokinaAtxi, StructureReport.ComparisonSummary(c));
    }

    /// <summary>
    /// R-128: by range alone (a manual lens) the nearest row decides as before, here the
    /// lensfun Samyang against Adobe's Nikkor 135 f/2 DC, another lens of the same range.
    /// </summary>
    [Fact]
    public void R128_ByRangeAloneTheNearestRowStillGivesTheVerdict()
    {
        using var ws = new TempWorkspace();
        var sources = LensProfileTestData.Sources(ws, rangePair: true);
        var r = MasterFrom(ProfileOf(sources, l => l.Name == LensProfileTestData.Samyang135, 135, 2.8), 1.0);

        StructureOutcome o = StructureRun.Compare(r, Master("135"), new StructureQuery
        {
            Sources = sources,
            RawPath = "DSC_0001.NEF",
            LoadInfo = _ => LensProfileTestData.Range135Raw(),
        });

        Assert.True(o.Compared, o.Profiles.Gap.ToString());
        Assert.True(o.Shot.CandidatesByRangeOnly);
        VignettingComparison c = o.Profiles;
        ProfileDepth adobe = Assert.Single(c.Profiles, p => p.Profile.Lens.Source == ProfileSource.Adobe);
        ProfileDepth lensfun = Assert.Single(c.Profiles, p => p.Profile.Lens.Source == ProfileSource.Lensfun);
        Assert.True(Math.Abs(lensfun.DiffStops) < Math.Abs(adobe.DiffStops), $"lensfun {lensfun.DiffStops:F3}, Adobe {adobe.DiffStops:F3}");
        Assert.NotEqual(adobe.Verdict, lensfun.Verdict);   // so the two rules would part here

        Assert.Equal(lensfun.Verdict, c.Verdict);
        Assert.Equal(lensfun.Profile.Lens.Key, Deciding(c).Key);
    }

    /// <summary>
    /// R-128: a --lens text is a search, not the camera's naming: "135mm" finds the Nikkor 135 DC
    /// (Adobe) and the Samyang (lensfun), two lenses, so the nearest row decides, here the
    /// Samyang, and not the Adobe file of another lens (the owner, 2026-11-02).
    /// </summary>
    [Fact]
    public void R128_ALensTextIsASearchSoTheNearestRowDecides()
    {
        using var ws = new TempWorkspace();
        var sources = LensProfileTestData.Sources(ws, rangePair: true);
        var r = MasterFrom(ProfileOf(sources, l => l.Name == LensProfileTestData.Samyang135, 135, 2.8), 1.0);

        StructureOutcome o = StructureRun.Compare(r, Master("135"), new StructureQuery
        {
            Sources = sources,
            RawPath = "DSC_0001.NEF",
            LoadInfo = _ => LensProfileTestData.Range135Raw(),
            LensText = "135mm",
        });

        Assert.True(o.Compared, o.Profiles.Gap.ToString());
        Assert.True(o.Shot.CandidatesByRangeOnly);                      // the raw alone found them by range
        VignettingComparison c = o.Profiles;
        ProfileDepth adobe = Assert.Single(c.Profiles, p => p.Profile.Lens.Source == ProfileSource.Adobe);
        ProfileDepth lensfun = Assert.Single(c.Profiles, p => p.Profile.Lens.Source == ProfileSource.Lensfun);
        Assert.True(Math.Abs(lensfun.DiffStops) < Math.Abs(adobe.DiffStops), $"lensfun {lensfun.DiffStops:F3}, Adobe {adobe.DiffStops:F3}");
        Assert.NotEqual(adobe.Verdict, lensfun.Verdict);                // so the two rules would part here
        Assert.False(c.AdobeDecides);
        Assert.Equal(lensfun.Verdict, c.Verdict);
        Assert.Equal(lensfun.Profile.Lens.Key, Deciding(c).Key);
    }

    /// <summary>
    /// R-128: with Adobe rows alone (`--source adobe`, the Tokina under its two names) the summary
    /// says how far the profiles differ, without lensfun as the reason and without the nearest
    /// rule's "not wrong".
    /// </summary>
    [Fact]
    public void R128_WithAdobeRowsAloneTheSummaryNamesNoLensfunRow()
    {
        using var ws = new TempWorkspace();
        var sources = LensProfileTestData.Sources(ws, atxi: true);
        var r = MasterFrom(TokinaAdobe(sources), 1.3);

        StructureOutcome o = StructureRun.Compare(r, Master(), new StructureQuery
        {
            Sources = sources,
            RawPath = "DSC_0001.NEF",
            LoadInfo = _ => TokinaRaw(),
            Source = ProfileSource.Adobe,
        });

        Assert.True(o.Compared, o.Profiles.Gap.ToString());
        Assert.Equal(2, o.Profiles.Profiles.Count);
        Assert.True(o.Profiles.AdobeDecides);
        string summary = StructureReport.ComparisonSummary(o.Profiles);
        Assert.Contains("cele 2 profile difera intre ele", summary);
        Assert.DoesNotContain("lensfun", summary);
        Assert.DoesNotContain("nu e gresit", summary);
        using (Ui.Use(UiLanguage.English))
        {
            string english = StructureReport.ComparisonSummary(o.Profiles);
            Assert.Contains("the 2 profiles differ from each other", english);
            Assert.DoesNotContain("lensfun", english);
            Assert.DoesNotContain("not wrong", english);
        }
    }

    /// <summary>
    /// R-128: with no Adobe row among the lenses (`--source lensfun` on the ID path) the
    /// nearest row decides as before, among the lensfun entry's two calibrations.
    /// </summary>
    [Fact]
    public void R128_WithNoAdobeRowTheNearestRowStillGivesTheVerdict()
    {
        using var ws = new TempWorkspace();
        var sources = LensProfileTestData.Sources(ws, tokinaOnTwoFormats: true);
        var r = MasterFrom(TokinaAdobe(sources), 1.3);

        StructureOutcome o = StructureRun.Compare(r, Master(), new StructureQuery
        {
            Sources = sources,
            RawPath = "DSC_0001.NEF",
            LoadInfo = _ => TokinaRaw(),
            Source = ProfileSource.Lensfun,
        });

        Assert.True(o.Compared, o.Profiles.Gap.ToString());
        Assert.False(o.Shot.CandidatesByRangeOnly);
        VignettingComparison c = o.Profiles;
        Assert.Equal(2, c.Profiles.Count);
        Assert.All(c.Profiles, p => Assert.Equal(ProfileSource.Lensfun, p.Profile.Lens.Source));
        ProfileDepth nearest = c.Profiles.OrderBy(p => Math.Abs(p.DiffStops)).First();

        Assert.Equal(nearest.Verdict, c.Verdict);
        Assert.Equal(nearest.Profile.Lens.Key, Deciding(c).Key);
    }

    /// <summary>
    /// R-128 for the run: with the raw's ID 139 beside a master of 0.86 stops (next to the
    /// lensfun entry, 0.2 deeper than Adobe) the report marks the Adobe row, the depth and the
    /// run read ACCEPTABIL, and the console's exit code for that verdict is 2.
    /// </summary>
    [Fact]
    public void R128_TheRunsVerdictAndExitCodeFollowTheAdobeRow()
    {
        using var ws = new TempWorkspace();
        string reference = GoldenOutputTests.Reference(ws, k: 0.5);
        var w = new StringWriter();

        StructureOutcome o = StructureRun.Execute(reference, w, new StructureQuery
        {
            Sources = LensProfileTestData.Sources(ws),
            RawPath = "DSC_0001.NEF",
            LoadInfo = _ => TokinaRaw(),
        }).Result;

        string[] lines = w.ToString().Split('\n');
        int adobeRow = Array.FindIndex(lines, l => l.TrimStart().StartsWith("Adobe "));
        Assert.True(adobeRow >= 0, w.ToString());
        Assert.Contains("<- Adobe, da verdictul", lines[adobeRow + 1]);
        Assert.Equal("ACCEPTABIL", o.Profiles.Verdict);
        Assert.Equal("ACCEPTABIL", o.Verdict);
        Assert.Equal(2, FlatCheckCli.ExitFor(o.Verdict));
    }

    /// <summary>
    /// R-128 in the console: `--lens Tokina` is a search, so on the same master the nearest row
    /// decides - the lensfun entry, PERFECT on depth - and the run keeps the shape's verdict,
    /// exit 0, where the Adobe row would have made it ACCEPTABIL, exit 2.
    /// </summary>
    [Fact]
    public void R128_TheConsolesLensTextLeavesTheNearestRowDeciding()
    {
        using var ws = new TempWorkspace();
        string profiles = LensProfileTestData.ProfilesDir(ws);
        string reference = GoldenOutputTests.Reference(ws, k: 0.5);

        var (output, exit) = GoldenOutputTests.RunCli(ws, reference, "--lens", "Tokina", "--fstop", "2.8", "--profiles", profiles, "-o", ws.PathTo("out.png"));

        string[] lines = output.Split('\n');
        int lensfunRow = Array.FindIndex(lines, l => l.TrimStart().StartsWith("lensfun "));
        Assert.True(lensfunRow >= 0, output);
        Assert.Contains("<- cel mai apropiat, da verdictul", lines[lensfunRow + 1]);
        Assert.DoesNotContain("<- Adobe", output);
        Assert.Contains("adancimea : PERFECT", output);
        Assert.DoesNotContain("VERDICT: ACCEPTABIL", output);
        Assert.Equal(0, exit);
    }

    /// <summary>
    /// R-128 on the Split tab (R-093): the reference profile every column is measured against
    /// is the whole's Adobe row, even when the whole's nearest row is a lensfun one.
    /// </summary>
    [Fact]
    public void R128_TheSplitTabsReferenceProfileIsTheWholesAdobeRow()
    {
        using var ws = new TempWorkspace();
        string Flat(string name, double k) => ws.WriteFile(name, FitsTestData.BuildFits(256, 256, FitsTestData.Radial(256, 256, k: k)));
        string whole = Flat("intreg.fit", 0.45);
        string[] parts = { Flat("parte-1.fit", 0.30), Flat("parte-2.fit", 0.60) };
        var query = new StructureQuery { Sources = LensProfileTestData.Sources(ws, tokinaOnTwoFormats: true), RawPath = "DSC_0001.NEF", LoadInfo = _ => TokinaRaw() };

        SplitResult r = SplitRun.Execute(whole, parts, query, new DateTime(2026, 11, 2, 12, 0, 0), 10, new StringWriter()).Result;

        Assert.True(r.HasReference);
        ProfileDepth nearest = r.Whole.Outcome.Profiles.Profiles.OrderBy(p => Math.Abs(p.DiffStops)).First();
        Assert.Equal(ProfileSource.Lensfun, nearest.Profile.Lens.Source);   // the case: lensfun is the whole's nearest
        Assert.Equal(ProfileSource.Adobe, r.ReferenceProfile.Lens.Source);
        Assert.True(r.ReferenceByAdobe);
        Assert.All(r.Columns, col => Assert.Equal(r.ReferenceProfile.File, col.Reference.Profile.File));
        Assert.Contains("profilul Adobe al intregului", SplitReport.ReferenceLine(r));
        SplitRow reference = Assert.Single(SplitReport.Rows(r), x => x.IsReference);
        Assert.Contains("profilul Adobe al intregului - REFERINTA", reference.Note);
    }
}
