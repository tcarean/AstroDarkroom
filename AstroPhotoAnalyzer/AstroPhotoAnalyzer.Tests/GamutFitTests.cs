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

// GamutFitTests - the paper fit (R-175), its variants and evidence (R-176) and the files it
// writes (R-177, ADR 0082). The fit's rule is injected as a function of L*, a*, b*, so the
// tests know the largest chroma the "paper" holds; the real rule comes from the profile
// (GamutAnalyzer) and is measured on the reference image (V-016).

using System.Text;

namespace AstroPhotoAnalyzer.Tests;

public class GamutFitTests
{
    /// <summary>
    /// A paper that holds a chroma of 10 from L* 30 up, 5 from L* 10 up, next to nothing (0.02)
    /// between L* 8 and 10, and cannot reach any tone under L* 8: the whole difference is 0
    /// inside, 4 plus the excess outside, 6 for a tone out of reach.
    /// </summary>
    static double FakeRule(double l, double a, double b)
    {
        double c = Math.Sqrt(a * a + b * b);
        if (l < 8) return 6;
        double cmax = Cmax(l);
        return c > cmax ? 4 + (c - cmax) : 0;
    }

    static double Cmax(double l) => l >= 30 ? 10 : l >= 10 ? 5 : 0.02;

    /// <summary>A one-row raster whose pixels are the given D50 Lab colours.</summary>
    static Raster RasterOf(params (double L, double A, double B)[] colours)
    {
        var r = new Raster(1, colours.Length, 3);
        for (int i = 0; i < colours.Length; i++)
        {
            var (red, green, blue) = ColorMath.SrgbOfLabD50(colours[i].L, colours[i].A, colours[i].B);
            r.Planes[0][i] = (float)red; r.Planes[1][i] = (float)green; r.Planes[2][i] = (float)blue;
        }
        return r;
    }

    static (double L, double A, double B) LabAt(Raster r, int i) => ColorMath.LabD50OfSrgb(r.Planes[0][i], r.Planes[1][i], r.Planes[2][i]);

    static double ChromaOf((double L, double A, double B) lab) => Math.Sqrt(lab.A * lab.A + lab.B * lab.B);

    static double HueOf((double L, double A, double B) lab) => Math.Atan2(lab.B, lab.A) * 180 / Math.PI;

    [Fact]
    public void R175_TheFitReducesTheChromaAloneToWhatThePaperHoldsAndCountsTheToneOutOfReach()
    {
        GamutFitTable t = GamutFitTable.Build(FakeRule);
        Assert.Null(t.Problem);
        // Inside, outside above and under L* 30, hues around the circle, one tone out of reach,
        // and one whose edge is next to nothing but whose grey the paper keeps: fitted to grey,
        // not left as out of reach (the second review of 2026-11-06).
        Raster r = RasterOf((50, 4, 3), (50, 16, 0), (50, 0, -18), (50, -12, 12), (20, 6, 6), (20, -2, 1), (5, 10, 0), (9, 0.5, 0));
        GamutFitOutcome o = GamutFit.Apply(r, t, knee: 1, keepFacts: true);
        Raster f = o.Image;
        for (int i = 0; i < r.W; i++)
        {
            var before = LabAt(r, i);
            var after = LabAt(f, i);
            Assert.InRange(after.L, before.L - 0.15, before.L + 0.15);
            if (before.L < 8)
            {
                // Out of reach: untouched.
                Assert.InRange(ChromaOf(after), ChromaOf(before) - 0.15, ChromaOf(before) + 0.15);
                continue;
            }
            double want = Math.Min(ChromaOf(before), Cmax(before.L));
            Assert.InRange(ChromaOf(after), want - 0.3, want + 0.3);
            if (ChromaOf(before) > 1) Assert.InRange(HueOf(after), HueOf(before) - 1, HueOf(before) + 1);
        }
        Assert.Equal(new[] { false, true, true, true, true, false, true, true }, o.OutsideBefore);
        Assert.Equal(new[] { false, false, false, false, false, false, true, false }, o.OutsideAfter);
        Assert.Equal(100.0 * 6 / 8, o.PctOutsideBefore, 3);
        Assert.Equal(100.0 / 8, o.PctOutsideAfter, 3);
        Assert.Equal(100.0 / 8, o.PctUnreachable, 3);
        Assert.Equal(100.0 * 5 / 8, o.PctTouched, 3);
    }

    [Fact]
    public void R175_TheTableIsReadBetweenItsNodesAndTheExactRuleDecidesNearTheEdge()
    {
        // A paper whose largest chroma grows with the hue, 5 at 0 degrees to 15 at 360, and with
        // L* by a tenth, plus a wave of 0.3 whose zeros fall on the table's nodes (whatever their
        // count), so the table sees a straight edge and misses the true one by up to 0.3 between
        // its nodes: a pixel landed on the edge must be decided by the rule, not by the table.
        static double Cmax(double l, double hDeg) => 5 + 10 * hDeg / 360 + l / 10 + 0.3 * Math.Sin(l * Math.PI * (Thresholds.GamutFitLstarNodes - 1) / 100);
        static double Rule(double l, double a, double b)
        {
            double h = Math.Atan2(b, a) * 180 / Math.PI; if (h < 0) h += 360;
            double cmax = Cmax(l, h), c = Math.Sqrt(a * a + b * b);
            return c > cmax ? 4 + (c - cmax) : 0;
        }
        GamutFitTable t = GamutFitTable.Build(Rule);
        var rng = new Random(7);
        var colours = new List<(double, double, double)>();
        for (int i = 0; i < 400; i++)
        {
            double l = 10 + 70 * rng.NextDouble(), h = 360 * rng.NextDouble() * Math.PI / 180;
            double cmax = Cmax(l, h * 180 / Math.PI), c = cmax * (0.8 + 0.4 * rng.NextDouble());
            colours.Add((l, c * Math.Cos(h), c * Math.Sin(h)));
        }
        Raster r = RasterOf(colours.ToArray());
        GamutFitOutcome o = GamutFit.Apply(r, t, knee: 1, keepFacts: true);
        Assert.Equal(0, o.PctOutsideAfter);
        for (int i = 0; i < r.W; i++)
        {
            if (!o.OutsideBefore[i]) continue;
            var after = LabAt(o.Image, i);
            // Fitted onto the edge, not far inside it: the rule of the pixel itself.
            Assert.InRange(Rule(after.L, after.A, after.B), 0, 0.001);
            double h = Math.Atan2(after.B, after.A) * 180 / Math.PI; if (h < 0) h += 360;
            double cmax = Cmax(after.L, h);
            Assert.InRange(ChromaOf(after), cmax - 0.04, cmax + 0.001);   // the search's last step is 0.02
        }
    }

    [Fact]
    public void R176_TheThreeVariantsTouchMoreAsTheKneeComesDownAndTheEvidenceSaysHowMuch()
    {
        GamutFitTable t = GamutFitTable.Build(FakeRule);
        // At L* 50 the paper holds 10: a ramp of chroma from 0.05 to 20.05, 101 pixels, none
        // exactly on the edge (a pixel on it is decided by the search's last step).
        var colours = new List<(double, double, double)>();
        for (int i = 0; i <= 100; i++) colours.Add((50, 0.2 * i + 0.05, 0));
        Raster r = RasterOf(colours.ToArray());

        GamutFitOutcome exact = GamutFit.Apply(r, t, Thresholds.GamutFitKneeExact, keepFacts: true);
        GamutFitOutcome soft = GamutFit.Apply(r, t, Thresholds.GamutFitKneeSoft, keepFacts: true);
        GamutFitOutcome softer = GamutFit.Apply(r, t, Thresholds.GamutFitKneeSofter, keepFacts: true);

        // Exact touches the 51 pixels over 10 (from 10.05); soft from 9 (from 9.05: 56), softer from 8 (61).
        Assert.InRange(exact.PctTouched, 100.0 * 50 / 101, 100.0 * 52 / 101);
        Assert.InRange(soft.PctTouched, 100.0 * 55 / 101, 100.0 * 57 / 101);
        Assert.InRange(softer.PctTouched, 100.0 * 60 / 101, 100.0 * 62 / 101);
        foreach (GamutFitOutcome o in new[] { exact, soft, softer })
        {
            Assert.Equal(100.0 * 51 / 101, o.PctOutsideBefore, 2);
            Assert.Equal(0, o.PctOutsideAfter);
            // Nothing leaves the edge: the soft knees stay under it, and a ramp stays a ramp.
            double last = -1;
            for (int i = 0; i <= 100; i++)
            {
                double c = ChromaOf(LabAt(o.Image, i));
                Assert.InRange(c, 0, 10.3);
                Assert.True(c >= last - 0.05, $"the chroma falls back at pixel {i}: {c} after {last}");
                last = c;
            }
        }
        // The soft knee keeps steps just past the edge where the hard one flattens: the pixels
        // at 10.05 and 11.05 differ in chroma under the knee, not under the hard fit. Far past
        // the edge every variant ends flat: the knee has only a tenth of the edge to spend.
        double hardSpread = ChromaOf(LabAt(exact.Image, 55)) - ChromaOf(LabAt(exact.Image, 50));
        double softSpread = ChromaOf(LabAt(soft.Image, 55)) - ChromaOf(LabAt(soft.Image, 50));
        Assert.InRange(hardSpread, -0.05, 0.05);
        Assert.True(softSpread > 0.15, $"the soft knee spread is {softSpread}");

        // The evidence: the chroma given up and its 99 % mark, the dark share, the recipe.
        Assert.InRange(exact.ChromaLostMedian, 4.5, 5.5);     // the touched pixels lose 0.2..10, median about 5
        Assert.InRange(exact.ChromaLostP99, 9.5, 10.2);     // 20.05 - 10, within the search's last step
        Assert.Equal(0, exact.PctOutsideUnderLowSeparation);  // every pixel outside sits at L* 50
        GamutFitRecipe recipe = GamutFit.Recipe(r, t);
        Assert.InRange(recipe.LstarCeiling, 50, 51);          // the band holding 90 % of the pixels outside
        Assert.InRange(recipe.Factor, 0.45, 0.50);            // 20.05 x 0.5 = 10.03 sits on the edge: 0.50 or the step under it
        Assert.InRange(recipe.PctLeft, 0, 1);
    }

    [Fact]
    public void R177_TheTiffWriterWritesSixteenBitsWithTheProfileAndReadsBackToTheSameSamples()
    {
        using var ws = new TempWorkspace();
        var rng = new Random(3);
        // 130 rows: three strips, the last one short, so the offsets and counts live in the heap.
        var r = new Raster(130, 64, 3);
        foreach (float[] plane in r.Planes) for (int i = 0; i < plane.Length; i++) plane[i] = (float)rng.NextDouble();
        byte[] icc = IccTestData.V2("sRGB test");
        string target = ws.PathTo("scris.tif");
        TiffWriter.Save(r, icc, "AstroPhotoAnalyzer: Exacta", target);

        // The tool reads it back: the same samples within one 16-bit level, the same profile.
        Raster back = ImageLoader.Load(target);
        Assert.Equal("TIFF", back.Source.Format);
        Assert.Equal(16, back.Source.BitDepth);
        Assert.Equal(icc, back.Source.Icc);
        for (int p = 0; p < 3; p++)
            for (int i = 0; i < r.W * r.H; i++)
                Assert.InRange(back.Planes[p][i], r.Planes[p][i] - 1.0 / 65535, r.Planes[p][i] + 1.0 / 65535);

        // An independent reader: the tags by hand, little-endian classic TIFF.
        byte[] bytes = File.ReadAllBytes(target);
        Assert.Equal(new byte[] { 0x49, 0x49, 42, 0 }, bytes[..4]);
        var tags = ReadTags(bytes);
        Assert.Equal(64u, tags[256].Value);          // ImageWidth
        Assert.Equal(130u, tags[257].Value);         // ImageLength
        Assert.Equal(new ushort[] { 16, 16, 16 }, Shorts(bytes, tags[258]));   // BitsPerSample
        Assert.Equal(1u, tags[259].Value);           // Compression: none
        Assert.Equal(2u, tags[262].Value);           // Photometric: RGB
        Assert.Equal(3u, tags[277].Value);           // SamplesPerPixel
        Assert.Equal(1u, tags[284].Value);           // PlanarConfiguration: chunky
        Assert.Equal(icc, Bytes(bytes, tags[34675]));   // the ICC profile
        Assert.Equal("AstroPhotoAnalyzer: Exacta", Encoding.ASCII.GetString(Bytes(bytes, tags[270])).TrimEnd('\0'));
        // Three strips of 64, 64 and 2 rows, their offsets and counts in the heap; the first
        // sample of each strip is the raster's red at that strip's first row, as a 16-bit level.
        Assert.Equal(3u, tags[273].Count);
        uint[] offsets = Longs(bytes, tags[273]), counts = Longs(bytes, tags[279]);
        uint rowBytes = (uint)r.W * 6;
        Assert.Equal(new uint[] { TiffWriter.RowsPerStrip * rowBytes, TiffWriter.RowsPerStrip * rowBytes, (130 - 2 * TiffWriter.RowsPerStrip) * rowBytes }, counts);
        for (int s = 0; s < 3; s++)
        {
            ushort red = BitConverter.ToUInt16(bytes, (int)offsets[s]);
            Assert.Equal((int)Math.Round(r.Planes[0][s * TiffWriter.RowsPerStrip * r.W] * 65535), red);
        }
        Assert.Equal(offsets[0] + counts[0], offsets[1]);
        Assert.Equal(offsets[2] + counts[2], (uint)bytes.Length);
    }

    [Fact]
    public void R177_SavingAVariantWritesAFitsOrATiffByTheExtensionAndNeverTheInput()
    {
        using var ws = new TempWorkspace();
        string icc = ws.WriteFile("hartie.icc", IccPrinterTestData.V2Lut16("Hartie de test", 20));
        // A colour FITS with a saturated patch: on the hand-built profile every chroma over 4 is
        // outside (a*, b* print as grey), so the fit has something to do.
        string input = ws.WriteFile("nebuloasa.fit", FitsTestData.Build(96, 64, 3, (x, y, c) => x < 48 ? (c == 0 ? 0.6 : 0.2) : 0.3,
            bitpix: -32, extraCards: new[] { FitsTestData.StringCard("OBJECT", "M8") }));
        Parameters p = Scenes.OnBuiltForPaper().WithDmaxFromProfile(icc, out string error) ?? throw new Exception(error);
        AnalysisResult a = AnalysisRun.Execute(input, p);
        GamutFitSet set = GamutFitRun.Execute(a, 200, 150);
        Assert.Null(set.Problem);
        Assert.Equal(new[] { "Exacta", "Lina", "Foarte lina" }, set.Variants.Select(v => v.Name).ToArray());
        GamutFitVariant v = set.Variants[0];
        Assert.True(v.Evidence.PctOutsideBefore > 40, "the saturated half is outside: " + v.Evidence.PctOutsideBefore);
        Assert.Equal(0, v.Evidence.PctOutsideAfter);

        string fits = ws.PathTo("nebuloasa_Exacta.fit");
        GamutFitExport.Save(a, set, v, fits);
        string header = Encoding.ASCII.GetString(File.ReadAllBytes(fits), 0, 2880 * 2);
        Assert.Contains("OBJECT  = 'M8", header);
        Assert.Contains("BITPIX  =                  -32", header);
        Assert.Contains("HISTORY", header);
        Assert.Contains("Exacta", header);
        Raster fromFits = ImageLoader.Load(fits);
        Assert.Equal(3, fromFits.Planes.Length);
        Assert.True(fromFits.Planes[0][0] < 0.6 && fromFits.Planes[0][0] > 0.2, "the red of the saturated patch was pulled in: " + fromFits.Planes[0][0]);
        Assert.Equal(0.3f, fromFits.Planes[0][95], 3);   // the grey half untouched

        string tiff = ws.PathTo("nebuloasa_Exacta.tif");
        GamutFitExport.Save(a, set, v, tiff);
        Raster fromTiff = ImageLoader.Load(tiff);
        Assert.Equal("TIFF", fromTiff.Source.Format);
        Assert.Equal(16, fromTiff.Source.BitDepth);
        Assert.Equal("sRGB", fromTiff.Source.IccDescription);   // the standard sRGB profile: the FITS had none
        for (int i = 0; i < fromFits.W * fromFits.H; i++)
            Assert.InRange(fromTiff.Planes[0][i], fromFits.Planes[0][i] - 1.0 / 65535, fromFits.Planes[0][i] + 1.0 / 65535);

        byte[] before = File.ReadAllBytes(input);
        var e = Assert.Throws<Exception>(() => GamutFitExport.Save(a, set, v, input));
        Assert.Contains("imaginea de intrare", e.Message);
        Assert.Equal(before, File.ReadAllBytes(input));
        // The proposed name carries the input's kind.
        Assert.Equal("nebuloasa_Exacta.fit", GamutFitTexts.ProposedFileName(input, v.Name));
        Assert.Equal("poza_Lina.tif", GamutFitTexts.ProposedFileName(@"C:\x\poza.tif", "Lina"));
    }

    static Dictionary<ushort, (ushort Type, uint Count, uint Value, int Offset)> ReadTags(byte[] b)
    {
        int ifd = BitConverter.ToInt32(b, 4);
        int n = BitConverter.ToUInt16(b, ifd);
        var tags = new Dictionary<ushort, (ushort, uint, uint, int)>();
        for (int i = 0; i < n; i++)
        {
            int e = ifd + 2 + 12 * i;
            ushort tag = BitConverter.ToUInt16(b, e), type = BitConverter.ToUInt16(b, e + 2);
            uint count = BitConverter.ToUInt32(b, e + 4);
            uint value = type == 3 && count == 1 ? BitConverter.ToUInt16(b, e + 8) : BitConverter.ToUInt32(b, e + 8);
            tags[tag] = (type, count, value, e + 8);
        }
        return tags;
    }

    static ushort[] Shorts(byte[] b, (ushort Type, uint Count, uint Value, int Offset) t)
    {
        int at = t.Count * 2 <= 4 ? t.Offset : (int)t.Value;
        var o = new ushort[t.Count];
        for (int i = 0; i < t.Count; i++) o[i] = BitConverter.ToUInt16(b, at + 2 * i);
        return o;
    }

    static uint[] Longs(byte[] b, (ushort Type, uint Count, uint Value, int Offset) t)
    {
        if (t.Count == 1) return new[] { t.Value };
        var o = new uint[t.Count];
        for (int i = 0; i < t.Count; i++) o[i] = BitConverter.ToUInt32(b, (int)t.Value + 4 * i);
        return o;
    }

    static byte[] Bytes(byte[] b, (ushort Type, uint Count, uint Value, int Offset) t)
    {
        int at = t.Count <= 4 ? t.Offset : (int)t.Value;
        return b[at..(at + (int)t.Count)];
    }
}
