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
/// The lens identified on mounts other than Nikon's (R-018, R-108): the frames are fabricated
/// with the values libraw gave on 2026-10-26 for the four CC0 samples of raw.pixls.us
/// (docs/plans/2026-10-26-identify-the-lens-on-every-mount.md; the files themselves are the
/// validation data V-041 to V-044, read by <see cref="RawSampleTests"/>), so no raw is needed
/// here. The Nikon path is pinned where it was, in LensProfileTests.
/// </summary>
public class LensMountTests
{
    /// <summary>Canon EOS 7D Mark II with the EF 50mm f/1.4 USM: MakerNote LensID 198 and the EXIF name.</summary>
    static LoadedFrame CanonRaw() => new LoadedFrame
    {
        Header = new Dictionary<string, string>(),
        FocalMm = 50,
        FNumber = 4,
        Camera = "Canon EOS 7D Mark II",
        LensId = 198,
        LensMount = "Canon EF",
        LensName = "EF50mm f/1.4 USM",
        LensMinFocalMm = 50,
        LensMaxFocalMm = 50,
        LensMaxAperture = 1.4142135,
    };

    /// <summary>
    /// Sony ILCE-7M3 with the FE 16-35mm F2.8 GM: libraw gives the E-mount type 32831, which no
    /// Adobe file carries (none of the 99 E / FE files has a LensID), and the EXIF name.
    /// </summary>
    static LoadedFrame SonyRaw() => new LoadedFrame
    {
        Header = new Dictionary<string, string>(),
        FocalMm = 32,
        FNumber = 4,
        Camera = "Sony ILCE-7M3",
        LensId = 32831,
        LensMount = "Sony E",
        LensName = "FE 16-35mm F2.8 GM",
        LensMinFocalMm = 16,
        LensMaxFocalMm = 35,
        LensMaxAperture = 2.8,
    };

    /// <summary>Pentax K-70 with the DA 18-135: the ID pair 8 215 as 2263, no name, no range.</summary>
    static LoadedFrame PentaxRaw() => new LoadedFrame
    {
        Header = new Dictionary<string, string>(),
        FocalMm = 18,
        FNumber = 5.6,
        Camera = "Pentax K-70",
        LensId = 2263,
        LensMount = "Pentax K",
    };

    [Fact]
    public void R108_LcpReader_ReadsAPentaxLensIdPairAsOneNumberLikeLibraw()
    {
        var profiles = LcpReader.ReadXml(LensProfileTestData.LcpOneProfile("PENTAX", LensProfileTestData.PentaxDa18135,
            LensProfileTestData.PentaxDa18135, "8 215", "1.5", "18"), "x.lcp");

        Assert.Contains(2263L, profiles.Single().LensIds);

        Assert.Equal(2263L, LcpReader.ParseLensId("8 215"));
        Assert.Equal(2106L, LcpReader.ParseLensId(" 8 58 "));
        Assert.Equal(4L, LcpReader.ParseLensId("4"));
        Assert.Equal(198L, LcpReader.ParseLensId("198"));
        Assert.Null(LcpReader.ParseLensId("1 2 3"));        // neither one number nor two bytes
        Assert.Null(LcpReader.ParseLensId("8 300"));        // a byte is at most 255
        Assert.Null(LcpReader.ParseLensId(""));
        Assert.Null(LcpReader.ParseLensId(null));
        Assert.Null(LcpReader.ParseLensId("x"));
    }

    [Fact]
    public void R018_ACanonRawIsIdentifiedByItsLensIdAndItsName()
    {
        using var ws = new TempWorkspace();
        var sources = LensProfileTestData.Sources(ws, otherMounts: true);

        ShotIdentity id = LensIdentification.Identify(CanonRaw(), null, sources);

        Assert.True(id.HasRawLensId);
        Assert.True(id.HasRawLensName);
        Assert.True(id.HasRawLensRange);
        Assert.False(id.RawSaysNoLens);
        Assert.False(id.CandidatesByRangeOnly);
        // the Adobe file and the lensfun entry by name; the Zeiss that shares ID 198 is not the lens
        Assert.Equal(2, id.Candidates.Count);
        Assert.All(id.Candidates, c => Assert.DoesNotContain("Zeiss", c.Name));
        Assert.Equal(ProfileSource.Adobe, id.Candidates[0].Source);
        Assert.Contains("50mm", id.Candidates[0].Name);
        Assert.Equal(ProfileSource.Lensfun, id.Candidates[1].Source);
        Assert.Equal("Canon EF 50mm f/1.4 USM", id.Candidates[1].Name);
        Assert.Equal("obiectivul \"EF50mm f/1.4 USM\" (ID 198) din raw (Canon EF, 50 mm f/1.4)", id.RawLensLabel);
        Assert.Equal(1.6, id.Crop!.Value, 3);                 // the crop from the lensfun table
        Assert.DoesNotContain(id.Notes, n => n.Contains("nu spune nimic"));

        // an older Canon body writes the ID but no LensModel: the ID alone finds the same Adobe
        // file, the lensfun lens of the same maker and range joins as on a Nikon, and the Zeiss
        // stays, since a name no range can read contradicts nothing (ADR 0030)
        var noName = CanonRaw();
        noName.LensName = null;
        ShotIdentity byId = LensIdentification.Identify(noName, null, sources);
        Assert.Equal(3, byId.Candidates.Count);
        Assert.Contains(byId.Candidates, c => c.Source == ProfileSource.Adobe && c.Name == "Canon EF 50mm f1.4 USM");
        Assert.Contains(byId.Candidates, c => c.Source == ProfileSource.Adobe && c.Name == "Zeiss Milvus 1.4_25 ZE");
        Assert.Contains(byId.Candidates, c => c.Source == ProfileSource.Lensfun && c.Name == "Canon EF 50mm f/1.4 USM");
        Assert.Equal("ID-ul obiectivului 198 din raw (Canon EF, 50 mm f/1.4)", byId.RawLensLabel);

        // a name no file carries falls back to the ID: the same three
        var strangeName = CanonRaw();
        strangeName.LensName = "EF50mm f/1.4 USM II";
        Assert.Equal(3, LensIdentification.Identify(strangeName, null, sources).Candidates.Count);
    }

    /// <summary>
    /// Canon EOS R with the RF 28mm F2.8 STM. On an RF body libraw's LensID is Canon's RF lens
    /// type (313 for this lens, 266 for the RF 70-200 F2.8 on the R6 sample, 303 for the RF 135
    /// on the R5 Mark II sample, measured 2026-10-26), while every Adobe RF file carries 61182,
    /// Canon's generic EF-side type: the ID matches nothing, and the name (the LensModel string
    /// every RF body writes) is the only key. The range is the EF-like one libraw builds.
    /// </summary>
    static LoadedFrame CanonRfRaw() => new LoadedFrame
    {
        Header = new Dictionary<string, string>(),
        FocalMm = 28,
        FNumber = 2.8,
        Camera = "Canon EOS R",
        LensId = 313,
        LensMount = "Canon RF",
        LensName = "RF28mm F2.8 STM",
        LensMinFocalMm = 28,
        LensMaxFocalMm = 28,
        LensMaxAperture = 2.8,
    };

    [Fact]
    public void R018_ACanonRfRawIsIdentifiedByItsName_TheRfIdsMatchNoProfile()
    {
        using var ws = new TempWorkspace();
        var sources = LensProfileTestData.Sources(ws, otherMounts: true);

        ShotIdentity id = LensIdentification.Identify(CanonRfRaw(), null, sources);

        Assert.Equal(2, id.Candidates.Count);
        Assert.Contains(id.Candidates, c => c.Source == ProfileSource.Adobe && c.Name == "Canon RF 28mm F2.8 STM");
        Assert.Contains(id.Candidates, c => c.Source == ProfileSource.Lensfun && c.Name == "Canon RF 28mm F2.8 STM");
        Assert.All(id.Candidates, c => Assert.DoesNotContain("50mm", c.Name));
        Assert.Equal("obiectivul \"RF28mm F2.8 STM\" (ID 313) din raw (Canon RF, 28 mm f/2.8)", id.RawLensLabel);

        // without the name the ID finds nothing (Canon's RF type is in no Adobe file) and the range
        // alone names the candidates, flagged as such; the generic 61182 the files carry would name
        // the whole RF line (both fixtures) if a raw ever gave it
        var noName = CanonRfRaw();
        noName.LensName = null;
        ShotIdentity byRange = LensIdentification.Identify(noName, null, sources);
        Assert.True(byRange.CandidatesByRangeOnly);
        Assert.All(byRange.Candidates, c => Assert.Contains("28mm", c.Name));
        noName.LensMinFocalMm = noName.LensMaxFocalMm = noName.LensMaxAperture = null;
        Assert.Empty(LensIdentification.Identify(noName, null, sources).Candidates);
        var generic = CanonRfRaw();
        generic.LensName = null;
        generic.LensId = 61182;
        generic.LensMinFocalMm = generic.LensMaxFocalMm = generic.LensMaxAperture = null;
        ShotIdentity byGenericId = LensIdentification.Identify(generic, null, sources);
        Assert.Equal(2, byGenericId.Candidates.Count(c => c.Source == ProfileSource.Adobe));
        Assert.Contains(byGenericId.Candidates, c => c.Name == "Canon RF 50mm F1.8 STM");
    }

    [Fact]
    public void R018_ASonyERawIsIdentifiedByItsNameAlone_TheRangeDoesNotPullTheAMountTwin()
    {
        using var ws = new TempWorkspace();
        var sources = LensProfileTestData.Sources(ws, otherMounts: true);

        ShotIdentity id = LensIdentification.Identify(SonyRaw(), null, sources);

        Assert.False(id.CandidatesByRangeOnly);
        Assert.Equal(2, id.Candidates.Count);
        Assert.All(id.Candidates, c => Assert.DoesNotContain("ZA", c.Name));   // the A-mount 16-35 f/2.8 has the same range
        Assert.Contains(id.Candidates, c => c.Source == ProfileSource.Adobe && c.Name == "Sony FE 16-35mm F2.8 GM");
        Assert.Contains(id.Candidates, c => c.Source == ProfileSource.Lensfun && c.Name == "FE 16-35mm f/2.8 GM");
        Assert.Equal("obiectivul \"FE 16-35mm F2.8 GM\" (ID 32831) din raw (Sony E, 16-35 mm f/2.8)", id.RawLensLabel);

        // the name without libraw's E-mount type: the same two, and never "a raw without a lens"
        var nameOnly = SonyRaw();
        nameOnly.LensId = null;
        ShotIdentity byName = LensIdentification.Identify(nameOnly, null, sources);
        Assert.False(byName.HasRawLensId);
        Assert.True(byName.HasRawLensName);
        Assert.False(byName.RawSaysNoLens);
        Assert.Equal(2, byName.Candidates.Count);
        Assert.Equal("obiectivul \"FE 16-35mm F2.8 GM\" din raw (Sony E, 16-35 mm f/2.8)", byName.RawLensLabel);
        Assert.DoesNotContain(byName.Notes, n => n.Contains("nu spune nimic"));
    }

    [Fact]
    public void R018_APentaxRawIsIdentifiedByItsIdPair_TheLensfunTwinJoinsByName()
    {
        using var ws = new TempWorkspace();
        var sources = LensProfileTestData.Sources(ws, otherMounts: true);

        ShotIdentity id = LensIdentification.Identify(PentaxRaw(), null, sources);

        Assert.True(id.HasRawLensId);
        Assert.False(id.HasRawLensName);
        Assert.False(id.HasRawLensRange);
        Assert.False(id.RawSaysNoLens);
        Assert.False(id.CandidatesByRangeOnly);
        // Adobe by the pair; lensfun spells the lens without the brackets and is Pentax's, not
        // "smc"'s, so it joins by the letters of the name, not by maker and range
        Assert.Equal(2, id.Candidates.Count);
        Assert.Equal(ProfileSource.Adobe, id.Candidates[0].Source);
        Assert.Equal(ProfileSource.Lensfun, id.Candidates[1].Source);
        Assert.All(id.Candidates, c => Assert.Contains("18-135", c.Name));
        Assert.Equal("ID-ul obiectivului 2263 din raw (Pentax K)", id.RawLensLabel);
        Assert.Equal(1.534, id.Crop!.Value, 3);
    }

    [Fact]
    public void R108_TheLensIdComesFromNikonsFieldOnANikonAndFromTheMakerNoteElsewhere()
    {
        const ulong signature = 5356031691206446338UL;      // what MakerNotes.LensID holds on a D3300
        Assert.Equal(74L, RawReader.LensIdOf(74, signature, 27));
        Assert.Null(RawReader.LensIdOf(0, signature, 27));   // a Nikon on a telescope: the signature is no ID
        Assert.Null(RawReader.LensIdOf(0, 139, 28));         // Nikon Z: the same field, the same rule
        Assert.Equal(198L, RawReader.LensIdOf(0, 198, 5));   // Canon EF
        Assert.Equal(155L, RawReader.LensIdOf(0, 155, 5));
        Assert.Equal(32831L, RawReader.LensIdOf(0, 32831, 40));  // Sony E
        Assert.Equal(2263L, RawReader.LensIdOf(0, 2263, 33));    // Pentax K
        Assert.Null(RawReader.LensIdOf(0, 155, 0));          // an unknown mount: nothing is guessed
        Assert.Null(RawReader.LensIdOf(0, 0, 5));
        Assert.Null(RawReader.LensIdOf(0, 1UL << 40, 5));    // does not fit 31 bits: not an ID
    }

    [Theory]
    [InlineData(27, "Nikon F")]
    [InlineData(28, "Nikon Z")]
    [InlineData(5, "Canon EF")]
    [InlineData(4, "Canon EF-S")]
    [InlineData(6, "Canon RF")]
    [InlineData(40, "Sony E")]
    [InlineData(25, "Sony A")]
    [InlineData(33, "Pentax K")]
    [InlineData(13, "Fujifilm X")]
    [InlineData(10, "Micro 4/3")]
    [InlineData(0, null)]
    public void R108_TheMountIsNamedAsLibrawNamesIt(int code, string? label)
        => Assert.Equal(label, RawReader.MountLabel(code));

    [Fact]
    public void R108_TheRangeFallsBackToMaxApertureThenToTheTopFields()
    {
        // Canon: the MakerNote's MaxAperture4MinFocal is 0, MaxAperture 1.414
        Assert.Equal<(double?, double?, double?)>((50.0, 50.0, 1.4142135), RawReader.RangeOf(50, 50, 0, 1.4142135, 50, 50, 0));
        // Nikon: the MakerNote alone, the top fields 0
        Assert.Equal<(double?, double?, double?)>((135.0, 135.0, 2.0), RawReader.RangeOf(135, 135, 2, 0, 0, 0, 0));
        // Sony: both filled, the MakerNote first
        Assert.Equal<(double?, double?, double?)>((16.0, 35.0, 2.8), RawReader.RangeOf(16, 35, 2.8, 0, 16, 35, 2.8));
        // Pentax: nothing anywhere
        Assert.Equal<(double?, double?, double?)>((null, null, null), RawReader.RangeOf(0, 0, 0, 0, 0, 0, 0));
        // only the top fields
        Assert.Equal<(double?, double?, double?)>((24.0, 70.0, 2.8), RawReader.RangeOf(0, 0, 0, 0, 24, 70, 2.8));
    }

    [Fact]
    public void R108_TheLabelsNameTheLensAsTheRawGaveIt_InBothLanguages()
    {
        var tokina = new LoadedFrame { LensId = 139, LensMinFocalMm = 11, LensMaxFocalMm = 20, LensMaxAperture = 2.8 };
        var tokinaOnItsMount = new LoadedFrame { LensId = 139, LensMount = "Nikon F", LensMinFocalMm = 11, LensMaxFocalMm = 20, LensMaxAperture = 2.8 };
        var nameOnly = SonyRaw();
        nameOnly.LensId = null;
        var bareName = new LoadedFrame { LensName = "EF85mm f/1.8 USM" };

        Assert.Equal("ID-ul obiectivului 139 din raw (11-20 mm f/2.8)", SyntheticReport.RawLensLabel(tokina));
        Assert.Equal("ID-ul obiectivului 139 din raw (Nikon F, 11-20 mm f/2.8)", SyntheticReport.RawLensLabel(tokinaOnItsMount));
        Assert.Equal("ID-ul obiectivului 2263 din raw (Pentax K)", SyntheticReport.RawLensLabel(PentaxRaw()));
        Assert.Equal("obiectivul \"EF85mm f/1.8 USM\" din raw", SyntheticReport.RawLensLabel(bareName));
        Assert.Equal("(ID obiectiv)", SyntheticReport.RawLensLabel(new LoadedFrame()));
        Assert.Equal("(ID obiectiv)", SyntheticReport.RawLensLabel(null));
        Assert.Contains("(obiectivul \"FE 16-35mm F2.8 GM\", ID 32831, nu se potriveste cu niciun profil)", SyntheticReport.RawRangeLabel(SonyRaw()));
        Assert.Contains("(obiectivul \"FE 16-35mm F2.8 GM\" nu se potriveste cu niciun profil)", SyntheticReport.RawRangeLabel(nameOnly));
        Assert.Contains("(ID-ul obiectivului 139 nu se potriveste cu niciun profil)", SyntheticReport.RawRangeLabel(tokina));
        Assert.Contains("niciun nume de obiectiv", SyntheticReport.RawWithoutLensNote("IMG_7290.CR2"));

        using (Ui.Use(UiLanguage.English))
        {
            Assert.Equal("the lens \"EF50mm f/1.4 USM\" (ID 198) from the raw (Canon EF, 50 mm f/1.4)", SyntheticReport.RawLensLabel(CanonRaw()));
            Assert.Equal("the lens \"FE 16-35mm F2.8 GM\" from the raw (Sony E, 16-35 mm f/2.8)", SyntheticReport.RawLensLabel(nameOnly));
            Assert.Equal("the lens ID 2263 from the raw (Pentax K)", SyntheticReport.RawLensLabel(PentaxRaw()));
            Assert.Equal("the lens ID 139 from the raw (11-20 mm f/2.8)", SyntheticReport.RawLensLabel(tokina));
            Assert.Contains("(the lens ID 139 matches no profile)", SyntheticReport.RawRangeLabel(tokina));
            Assert.Equal("IMG_7290.CR2 says nothing about the lens (no lens ID, no lens name, no focal range): a telescope, or a lens without contacts",
                SyntheticReport.RawWithoutLensNote("IMG_7290.CR2"));
        }
    }

    [Fact]
    public void R108_ARawWhoseNameOrIdMatchesNoProfileIsANoMatch_NotARawWithoutALens()
    {
        using var ws = new TempWorkspace();
        var sources = LensProfileTestData.Sources(ws);           // the Nikon fixtures only: nothing Canon
        var canon = CanonRaw();
        canon.LensMinFocalMm = canon.LensMaxFocalMm = canon.LensMaxAperture = null;   // no range to fall back on

        ShotIdentity id = LensIdentification.Identify(canon, null, sources);

        Assert.Empty(id.Candidates);
        Assert.True(id.RawNamesLens);
        Assert.False(id.RawSaysNoLens);
        Assert.DoesNotContain(id.Notes, n => n.Contains("nu spune nimic"));
    }

    [Fact]
    public void R108_ByLensNameComparesLettersAndDigitsOnly_TheMakerMayLeadTheCatalogName()
    {
        using var ws = new TempWorkspace();
        var sources = LensProfileTestData.Sources(ws, otherMounts: true);
        LensCatalog cat = sources.Catalog;

        // the EXIF spelling against Adobe's stCamera:Lens alias and lensfun's "Canon EF 50mm f/1.4 USM"
        var canon = cat.ByLensName("EF50mm f/1.4 USM", "Canon");
        Assert.Equal(2, canon.Count);
        Assert.Contains(canon, l => l.Source == ProfileSource.Adobe);
        Assert.Contains(canon, l => l.Source == ProfileSource.Lensfun);
        // case and punctuation do not matter; a different lens does
        Assert.Equal(canon, cat.ByLensName("ef 50 mm F1.4 usm", "Canon"));
        Assert.Empty(cat.ByLensName("EF50mm f/1.8 USM", "Canon"));
        Assert.Empty(cat.ByLensName("", "Canon"));
        Assert.Empty(cat.ByLensName(null, null));
        // Sony: the Adobe pretty name carries the maker, the lensfun one does not; both match
        var sony = cat.ByLensName("FE 16-35mm F2.8 GM", "Sony");
        Assert.Equal(2, sony.Count);
        Assert.All(sony, l => Assert.DoesNotContain("ZA", l.Name));
        // without a maker hint every Adobe file is parsed: the same answer
        Assert.Equal(sony, cat.ByLensName("FE 16-35mm F2.8 GM", null));
        Assert.Equal("CANONEF50MMF14USM", LensCatalog.Letters("Canon EF 50mm f/1.4 USM"));
        Assert.Equal("", LensCatalog.Letters(null));
    }
}
