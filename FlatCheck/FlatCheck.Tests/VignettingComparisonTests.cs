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
/// The master's vignetting against the lens's published profiles: the sixth test of the
/// structure mode, on the depth scale in stops. Pinned: a master that IS the model reads
/// 0.00 and PERFECT; a master that falls twice as fast reads REFA with a plus sign; the
/// verdict is the NEAREST profile's, not the worst; and every way the comparison can be
/// impossible leaves the five-test verdict alone and names the gap.
/// </summary>
public class VignettingComparisonTests
{
    const int W = 240, H = 160;

    static LoadedFrame Master() => new LoadedFrame
    {
        Header = new Dictionary<string, string> { ["SIMPLE"] = "T", ["FOCALLEN"] = "20", ["INSTRUME"] = "Nikon D3300" },
        FocalMm = 20,
        Camera = "Nikon D3300",
    };

    static LoadedFrame Raw() => new LoadedFrame
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

    /// <summary>
    /// The Adobe profile of the fixture, built as a flat at the fixture size, raised to a power.
    /// </summary>
    static Img ModelFlat(ProfileSources sources, double power = 1.0, double crop = 1.523)
    {
        LensChoice adobe = sources.Catalog.Lenses.First(l => l.Source == ProfileSource.Adobe && l.Mount == "NIKON CORPORATION");
        LensProfile p = LensCatalog.Nearest(sources.Catalog.ProfilesOf(adobe), 20, 2.8);
        Img flat = SyntheticFlatBuilder.Build(p, W, H, crop).Flat;
        var img = new Img(H, W);
        for (int i = 0; i < img.D.Length; i++) img.D[i] = Math.Pow(flat.D[i], power) * 20000;
        return img;
    }

    static (StructureResult Structure, ShotIdentity Shot, ProfileSources Sources) Setup(TempWorkspace ws, double power = 1.0, bool withRaw = true, bool tokinaOnTwoFormats = false)
    {
        var sources = LensProfileTestData.Sources(ws, tokinaOnTwoFormats);
        var r = StructureAnalyzer.Analyze(ModelFlat(sources, power));
        var shot = LensIdentification.Identify(Master(), withRaw ? Raw() : null, sources, rawName: "DSC_0001.NEF");
        return (r, shot, sources);
    }

    [Fact]
    public void AMasterThatIsTheModelReadsZeroStopsAndPerfect()
    {
        using var ws = new TempWorkspace();
        var (r, shot, sources) = Setup(ws);

        VignettingComparison c = VignettingComparer.Analyze(r, shot, sources.Catalog);

        Assert.True(c.Compared);
        Assert.Equal(ComparisonGap.None, c.Gap);
        Assert.Equal(2, c.Profiles.Count);                            // the Nikon Adobe file and the lensfun entry
        ProfileDepth adobe = c.Profiles.First(p => p.Profile.Lens.Source == ProfileSource.Adobe);
        Assert.Equal(0.0, adobe.DiffStops, 2);                        // same raster, same operator
        Assert.Equal("PERFECT", adobe.Verdict);
        Assert.Same(adobe, c.Deciding);
        Assert.Equal("PERFECT", c.Verdict);
        Assert.Equal(r.VignetteStops, c.MasterStops);
        Assert.True(c.SpreadStops > 0.05, $"spread {c.SpreadStops}");   // the two databases disagree, as in life
    }

    [Fact]
    public void AMasterThatFallsTwiceAsFastReadsRefaWithAPlusSign()
    {
        using var ws = new TempWorkspace();
        var (r, shot, sources) = Setup(ws, power: 2.0);

        VignettingComparison c = VignettingComparer.Analyze(r, shot, sources.Catalog);

        Assert.True(c.Compared);
        ProfileDepth adobe = c.Profiles.First(p => p.Profile.Lens.Source == ProfileSource.Adobe);
        Assert.Equal(adobe.CornerStops, adobe.DiffStops, 2);           // twice the depth: the difference IS the model's depth
        Assert.True(adobe.DiffStops > 0);
        Assert.Equal(Stats.DepthErrorPct(adobe.DiffStops), adobe.ErrorPct, 9);
        Assert.Equal("REFA", c.Verdict);
    }

    /// <summary>
    /// Without an Adobe row (and by range alone) a master that sits on one calibration and 0.2
    /// stops off the other is not wrong: the profiles disagree by about that much on the same
    /// lens, so the verdict follows the nearest one, not the worst (ADR 0031). With an Adobe row
    /// among lenses the raw named, the Adobe row decides instead (R-128, AdobeVerdictTests).
    /// </summary>
    [Fact]
    public void WithoutAnAdobeRowTheVerdictIsTheNearestProfiles_NotTheWorst()
    {
        using var ws = new TempWorkspace();
        var (r, shot, sources) = Setup(ws, tokinaOnTwoFormats: true);
        var lensfun = shot.Candidates.Where(l => l.Source == ProfileSource.Lensfun).ToList();

        VignettingComparison c = VignettingComparer.Analyze(r, shot, sources.Catalog, lensfun);

        Assert.Equal(2, c.Profiles.Count);
        Assert.False(c.AdobeDecides);
        ProfileDepth other = c.Profiles.First(p => !ReferenceEquals(p, c.Deciding));
        Assert.True(Math.Abs(other.DiffStops) > Math.Abs(c.Deciding.DiffStops));
        Assert.True(Stats.Severity(other.Verdict) >= Stats.Severity(c.Deciding.Verdict));
        Assert.Equal(c.Deciding.Verdict, c.Verdict);
    }

    /// <summary>
    /// The raw's candidates take in every calibration of a lensfun lens of the right maker and
    /// range, so a lens calibrated on two formats is two rows - same name, different "where",
    /// different depth - and the nearest is chosen among all of them. With one key for both
    /// the second calibration never reached the table.
    /// </summary>
    [Fact]
    public void ALensfunLensCalibratedOnTwoFormatsIsTwoRows()
    {
        using var ws = new TempWorkspace();
        var (r, shot, sources) = Setup(ws, tokinaOnTwoFormats: true);

        var c = VignettingComparer.Analyze(r, shot, sources.Catalog);

        Assert.True(c.Compared);
        Assert.Equal(3, c.Profiles.Count);                             // the Nikon Adobe file, and the lensfun entry twice
        var lensfun = c.Profiles.Where(p => p.Profile.Lens.Source == ProfileSource.Lensfun).ToList();
        Assert.Equal(2, lensfun.Count);
        Assert.Equal(lensfun[0].Profile.Lens.Name, lensfun[1].Profile.Lens.Name);
        Assert.NotEqual(lensfun[0].Profile.Lens.Where, lensfun[1].Profile.Lens.Where);
        Assert.NotEqual(lensfun[0].Profile.Model.CropFactor, lensfun[1].Profile.Model.CropFactor);
        Assert.True(Math.Abs(lensfun[0].CornerStops - lensfun[1].CornerStops) > 0.1, "two calibrations, two depths");
        Assert.Equal(ProfileSource.Adobe, c.Deciding.Profile.Lens.Source);   // the master IS the Adobe model

        var rows = StructureReport.ProfileRows(c);
        Assert.Equal(3, rows.Select(x => x.Key).Distinct().Count());
        Assert.Contains(rows, x => x.Where.EndsWith("calibrat pe full frame 1"));
        Assert.Contains(rows, x => x.Where.EndsWith("calibrat pe APS-C Canon 1.61"));
    }

    /// <summary>
    /// The comparison takes each lens's nearest profile to the raw's focal and f-stop; when
    /// that is far (a raw at 40 mm f/1.0 against a lens with 18-20 mm, f/2.8-4) the row
    /// keeps its verdict but its note carries the warning and is marked, in both applications.
    /// </summary>
    [Fact]
    public void ARawFarFromEveryProfileMarksTheRowWithTheMismatch()
    {
        using var ws = new TempWorkspace();
        var sources = LensProfileTestData.Sources(ws);
        var r = StructureAnalyzer.Analyze(ModelFlat(sources));
        var near = LensIdentification.Identify(Master(), Raw(), sources, rawName: "DSC_0001.NEF");
        var far = LensIdentification.Identify(Master(), Raw(), sources, focal: 40, fstop: 1.0, rawName: "DSC_0001.NEF");

        var rowsNear = StructureReport.ProfileRows(VignettingComparer.Analyze(r, near, sources.Catalog));
        var rowsFar = StructureReport.ProfileRows(VignettingComparer.Analyze(r, far, sources.Catalog));

        Assert.DoesNotContain(rowsNear, x => x.Note.Contains("ATENTIE"));
        Assert.All(rowsFar, x => Assert.Contains("ATENTIE: focala ceruta 40 mm n-are profil", x.Note));
        Assert.All(rowsFar, x => Assert.Contains("diafragma ceruta f/1.0", x.Note));
        Assert.All(rowsFar, x => Assert.True(x.IsWarning));
        Assert.Equal(rowsNear.Select(x => x.Profile), rowsFar.Select(x => x.Profile));   // the same nearest profiles, 20 mm f/2.8
    }

    [Fact]
    public void TheProfilesDepthIsMeasuredWithTheMastersOwnOperator()
    {
        using var ws = new TempWorkspace();
        var (r, shot, sources) = Setup(ws);
        ProfileDepth adobe = VignettingComparer.Analyze(r, shot, sources.Catalog).Profiles.First(p => p.Profile.Lens.Source == ProfileSource.Adobe);

        // CornerStats on the built model raster, not the polynomial at the box centre: the two
        // agree to a few hundredths, and only the first agrees with the master to better than a
        // thousandth of a stop (what remains is the master's 2x2 block mean, which the model
        // raster - built straight at the analysis size - does not go through).
        double atPoint = SyntheticFlatBuilder.Build(adobe.Profile, W, H, 1.523).CornerStops;
        Assert.Equal(atPoint, adobe.CornerStops, 1);
        Assert.Equal(r.VignetteStops, adobe.CornerStops, 3);
    }

    [Fact]
    public void WithoutARawThereIsNoLensAndTheGapSaysSo()
    {
        using var ws = new TempWorkspace();
        var (r, shot, sources) = Setup(ws, withRaw: false);

        VignettingComparison c = VignettingComparer.Analyze(r, shot, sources.Catalog);

        Assert.False(c.Compared);
        Assert.Null(c.Verdict);
        Assert.Equal(ComparisonGap.NoRaw, c.Gap);
        Assert.Empty(c.Profiles);
        Assert.Null(c.Deciding);
    }

    /// <summary>
    /// A raw that was read but carries neither a lens ID nor a focal range (a camera on a
    /// telescope) is its own gap, not "no raw": the person has 17 raws in flats/ and would look
    /// for a file that is there.
    /// </summary>
    [Fact]
    public void ARawThatCarriesNoLensIsItsOwnGapNotAMissingRaw()
    {
        using var ws = new TempWorkspace();
        var sources = LensProfileTestData.Sources(ws);
        var r = StructureAnalyzer.Analyze(ModelFlat(sources));
        var noLens = new LoadedFrame { Header = new Dictionary<string, string>(), Camera = "Canon EOS 40D" };
        ShotIdentity shot = LensIdentification.Identify(Master(), noLens, sources, rawName: "IMG_7290.CR2");

        VignettingComparison c = VignettingComparer.Analyze(r, shot, sources.Catalog);

        Assert.False(c.Compared);
        Assert.Null(c.Verdict);
        Assert.Equal(ComparisonGap.RawWithoutLens, c.Gap);
        Assert.Empty(c.Profiles);
    }

    /// <summary>
    /// A raw that names a lens no profile on this machine carries - a Canon whose Adobe file is
    /// not installed - is a "no match", never a raw without a lens, with or without the ID
    /// (R-108).
    /// </summary>
    [Fact]
    public void R108_ARawThatNamesALensNoProfileCarriesIsANoMatch()
    {
        using var ws = new TempWorkspace();
        var sources = LensProfileTestData.Sources(ws);           // the Nikon fixtures only
        var r = StructureAnalyzer.Analyze(ModelFlat(sources));
        var canon = new LoadedFrame
        {
            Header = new Dictionary<string, string>(),
            Camera = "Canon EOS 6D",
            FocalMm = 85,
            FNumber = 5.6,
            LensId = 155,
            LensMount = "Canon EF",
            LensName = "EF85mm f/1.8 USM",
            LensMinFocalMm = 85,
            LensMaxFocalMm = 85,
            LensMaxAperture = 1.834,
        };

        VignettingComparison c = VignettingComparer.Analyze(r, LensIdentification.Identify(Master(), canon, sources, rawName: "EOS_6D.CR2"), sources.Catalog);
        Assert.Equal(ComparisonGap.NoLensMatch, c.Gap);
        Assert.Empty(c.Profiles);

        canon.LensId = null;                                      // the name alone says the same
        c = VignettingComparer.Analyze(r, LensIdentification.Identify(Master(), canon, sources, rawName: "EOS_6D.CR2"), sources.Catalog);
        Assert.Equal(ComparisonGap.NoLensMatch, c.Gap);
    }

    [Fact]
    public void WithLensesButNoFStopTheGapIsTheFStop()
    {
        using var ws = new TempWorkspace();
        var (r, _, sources) = Setup(ws, withRaw: false);
        var shot = LensIdentification.Identify(Master(), null, sources);
        var lenses = sources.Catalog.Search("Tokina");

        VignettingComparison c = VignettingComparer.Analyze(r, shot, sources.Catalog, lenses);

        Assert.False(c.Compared);
        Assert.Equal(ComparisonGap.NoFStop, c.Gap);

        // and with the f-stop given, the same lenses compare - all three of them, including the
        // Canon file
        shot = LensIdentification.Identify(Master(), null, sources, fstop: 2.8);
        c = VignettingComparer.Analyze(r, shot, sources.Catalog, lenses);
        Assert.True(c.Compared);
        Assert.Equal(3, c.Profiles.Count);
    }

    [Fact]
    public void ALensfunProfileNeedsTheCrop_AnAdobeOneFallsBackToItsCalibrationSensor()
    {
        using var ws = new TempWorkspace();
        var (r, _, sources) = Setup(ws);
        var master = Master();
        master.Camera = "Pentax K-1";                                 // not in the lensfun camera table
        var shot = LensIdentification.Identify(master, Raw(), sources, rawName: "x.NEF");
        Assert.Null(shot.Crop);

        LensChoice lensfun = sources.Catalog.Lenses.First(l => l.Source == ProfileSource.Lensfun);
        VignettingComparison onlyLensfun = VignettingComparer.Analyze(r, shot, sources.Catalog, new[] { lensfun });
        Assert.False(onlyLensfun.Compared);
        Assert.Equal(ComparisonGap.NoCrop, onlyLensfun.Gap);
        Assert.Equal(new[] { lensfun }, onlyLensfun.SkippedForCrop);

        LensChoice adobe = sources.Catalog.Lenses.First(l => l.Source == ProfileSource.Adobe);
        VignettingComparison both = VignettingComparer.Analyze(r, shot, sources.Catalog, new[] { adobe, lensfun });
        Assert.True(both.Compared);
        Assert.Single(both.Profiles);
        Assert.Equal(0.0, both.Profiles[0].CameraCrop);
        Assert.Single(both.SkippedForCrop);
    }

    [Fact]
    public void AnEmptyCatalogIsItsOwnGap()
    {
        using var ws = new TempWorkspace();
        var (r, shot, _) = Setup(ws);

        VignettingComparison c = VignettingComparer.Analyze(r, shot, LensCatalog.Empty);

        Assert.Equal(ComparisonGap.NoProfiles, c.Gap);
        Assert.False(c.Compared);
    }

    [Fact]
    public void TheDepthArithmeticIsTheOneAgainstUses()
    {
        Assert.Equal(AgainstResult.DepthErrorOf(0.42), Stats.DepthErrorPct(0.42), 12);
        Assert.Equal(AgainstResult.DepthErrorOf(-0.42), Stats.DepthErrorPct(0.42), 12);
        Assert.Equal("PERFECT", Stats.DepthVerdict(0.02));
        Assert.Equal("BUN", Stats.DepthVerdict(-0.1));
        Assert.Equal("ACCEPTABIL", Stats.DepthVerdict(0.2));
        Assert.Equal("REFA", Stats.DepthVerdict(0.34));
        Assert.Equal("REFA", Stats.DepthVerdict(double.NaN));
        Assert.Equal(0.42, Stats.StopsForError(Stats.DepthErrorPct(0.42)), 12);
    }
}
