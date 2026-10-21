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

using System.Text;

namespace DarksCheck.Core;

/// <summary>
/// The scale section (R-080, ADR 0015): what an ADU is on the open set, as rows the console
/// prints and the window binds, and four paragraphs that say why a fraction of an ADU in the
/// master can matter. Every figure comes from the result; the electrons come from
/// <see cref="CameraFacts"/>, with their provenance in the words.
/// </summary>
internal static class ScaleReport
{
    internal const string Title = "EXPLICATII SCARA ADU";

    /// <summary>The console's wrap width for the paragraphs.</summary>
    private const int WrapColumns = 96;

    /// <summary>Under this white level the file's values are floats (a scaled FITS), not integer ADU, and no bit depth is read from it.</summary>
    private const double MinIntegerWhite = 255;

    /// <summary>The figures of the set the rows and the paragraphs share, computed once.</summary>
    private sealed record Figures(
        DarkSeriesAnalysis Darks,
        int ReferencePlane,
        string Plane,
        double Norm,
        string Eq,
        int? Bits,
        FrameInfo? First,
        string Settings,
        bool SettingsKnown,
        CameraFacts? Facts,
        double? Gain,
        double? BiasIso,
        double? Sky,
        double? SkySigma,
        int Lights)
    {
        internal static Figures Of(DarkSeriesResult r)
        {
            DarkSeriesAnalysis d = r.Darks;
            int rp = r.ReferencePlane;
            FrameInfo? first = d.Frames.FirstOrDefault(f => f.Error is null)?.Info;
            bool known = first?.Iso is double && first.ExposureSeconds is double;
            string settings = known
                ? $"ISO {F(first!.Iso!.Value, 0)}, {InventoryReport.Exposure(first.ExposureSeconds!.Value)} s"
                : "ISO sau expunere necunoscute";
            CameraFacts? facts = CameraFacts.For(first?.Camera);
            double? gain = first?.Iso is double iso ? facts?.GainAt(iso) : null;
            double? biasIso = r.Biases?.Frames.FirstOrDefault(b => b.Error is null && !b.Skipped)?.Info.Iso;
            (double? sky, double? sigma, int n) = SkyOf(r, rp);
            return new Figures(d, rp, d.PlaneNames[rp], d.Norm, DarkReport.Eq(d),
                d.White >= MinIntegerWhite ? (int)Math.Ceiling(Math.Log2(d.White + 1)) : null,
                first, settings, known, facts, gain, biasIso, sky, sigma, n);
        }

        /// <summary>The median over the decoded lights of the sky median and of the sky sigma on the reference plane, with their count.</summary>
        private static (double? Sky, double? Sigma, int Count) SkyOf(DarkSeriesResult r, int rp)
        {
            if (r.Lights is not LightsAnalysis la)
            {
                return (null, null, 0);
            }

            List<double> sky = [], sigma = [];
            foreach (LightFigures f in la.Frames)
            {
                if (f.Error is null && !f.Skipped && rp < f.SkyMedian.Count && !double.IsNaN(f.SkyMedian[rp]))
                {
                    sky.Add(f.SkyMedian[rp]);
                    sigma.Add(f.SkySigma[rp]);
                }
            }

            return sky.Count == 0 ? (null, null, 0) : (Planes.Median(sky), Planes.Median(sigma), sky.Count);
        }
    }

    /// <summary>The rows of the scale table.</summary>
    internal static IReadOnlyList<ReportRow> Rows(DarkSeriesResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        Figures g = Figures.Of(r);
        DarkSeriesAnalysis d = g.Darks;
        return
        [
            new("fisier", g.Bits is int bits
                ? $"{bits} biti, 0 .. {F(d.White, 0)} ADU; negrul la {F(d.Black, 0)}, albul la {F(d.White, 0)}: semnal util 0 .. {F(d.White - d.Black, 0)} ADU"
                : $"valori in virgula mobila, scara fisierului 0 .. {F(d.White, 3)}; negrul la {F(d.Black, 3)}"),
            new("1 ADU-eq", !g.SettingsKnown
                ? "ISO sau expunere necunoscute: ADU-eq e luat egal cu ADU brut"
                : Math.Abs(g.Norm - 1) < 1e-9
                    ? $"1 ADU brut: setul e la {g.Settings}, scara de referinta (ISO 400 si 120 s)"
                    : $"{F(g.Norm, 2)} ADU brut la {g.Settings} (ADU-eq e ADU adus la ISO 400 si 120 s)"),
            new("cuantizare", $"pas {d.QuantStep} ADU langa negru"),
            new("electroni", Electrons(g)),
            new("zgomot de citire", ReadNoise(r, g)),
            new("zgomot pe cadru", FrameNoise(r, g)),
            new("praguri pe set", $"pixel fierbinte {F(Th.HotAdu, 0)} ADU-eq = {F(Th.HotAdu * g.Norm, 0)} ADU; scurgere pe harta {F(Th.LeakAdu, 1)} ADU-eq = {F(Th.LeakAdu * g.Norm, 1)} ADU; lumina constanta pe o latura {F(Th.LeakConstantAdu, 2)} ADU-eq = {F(Th.LeakConstantAdu * g.Norm, 2)} ADU; lumina pe un cadru {F(Th.LeakFrameAdu, 1)} ADU-eq = {F(Th.LeakFrameAdu * g.Norm, 2)} ADU"),
            new("gasit", Found(r, g)),
            new("cer", g.Sky is double s && g.SkySigma is double ss
                ? $"mediana {F(s, 1)} ADU peste negru pe un light ({g.Plane}, {g.Lights} lights), sigma {F(ss, 1)} ADU pe pixel"
                : "fara lights in sesiune"),
            new("zgomotul stack-ului", g.SkySigma is double sig && g.Lights > 0
                ? $"sigma pe pixel {F(sig, 1)} / sqrt({g.Lights}) = {F(sig / Math.Sqrt(g.Lights), 2)} ADU; un offset din master ramane intreg in stack"
                : "fara lights: nu se poate estima"),
            new("saturatie", $"{F(d.White, 0)} ADU" + (g.Facts is null ? "" : $" ({F(g.Facts.FullWellElectrons, 0)} e- full well dupa sursa)")),
        ];
    }

    /// <summary>The four paragraphs, with the set's numbers in them.</summary>
    internal static IReadOnlyList<string> Paragraphs(DarkSeriesResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        Figures g = Figures.Of(r);
        DarkSeriesAnalysis d = g.Darks;
        string scale = g.Bits is int bits
            ? $"pe {bits} biti, de la 0 la {F(d.White, 0)}, cu negrul camerei la {F(d.Black, 0)}"
            : $"aici valori in virgula mobila de la 0 la {F(d.White, 3)}, cu negrul la {F(d.Black, 3)}";
        string p1 = $"ADU e unitatea fisierului: {scale}. "
            + $"ADU-eq e ADU brut adus la ISO 400 si 120 s, ca seriile cu setari diferite sa se compare intre ele: pe acest set 1 ADU-eq = {F(g.Norm, 2)} ADU brut. "
            + $"Un pas de cuantizare are {d.QuantStep} ADU, dar o medie pe multi pixeli se masoara mult sub el, pentru ca zgomotul amesteca treptele.";
        string p2 = "O abatere din masterul dark (o scurgere de lumina, un offset) nu e zgomot: masterul se scade din fiecare light, deci abaterea ramane intreaga in stack, oricate cadre ai aduna, pe cand zgomotul aleator scade cu radacina numarului de cadre. "
            + $"Pe un singur pixel, cei {F(d.Noise.SigmaFrame, 1)} ADU de zgomot ai unui dark acopera orice abatere sub 1 ADU; pe mii de pixeli, o treapta de 1 ADU cu margine dreapta se vede la o intindere tare pe cer intunecat.";
        string p3 = g.Sky is double s && g.SkySigma is double ss && s > 0 && g.Lights > 0
            ? $"Fata de cer: un light al acestei sesiuni are {F(s, 1)} ADU peste negru, deci 1 ADU e {F(100 / s, 2)} % din cer, cam cat eroarea unui flat; "
                + $"in stack-ul celor {g.Lights} lights zgomotul pe pixel scade la {F(ss, 1)} / sqrt({g.Lights}) = {F(ss / Math.Sqrt(g.Lights), 2)} ADU, iar un offset de 1 ADU din master ramane 1 ADU."
            : "Fata de cer: sesiunea nu are lights, deci raportul nu poate spune cat e 1 ADU fata de cerul tau si cat ramane zgomotul in stack; deschide sesiunea cu lights si randurile `cer` si `zgomotul stack-ului` se completeaza.";
        string p4 = $"Pragurile: {F(Th.HotAdu, 0)} ADU-eq pentru un pixel fierbinte; {F(Th.LeakAdu, 1)} ADU-eq pe harta pentru o scurgere evidenta (nivelul analizei de mana); "
            + $"{F(Th.LeakConstantAdu, 2)} ADU-eq si de {F(Th.LeakConstantSigmas, 0)} ori eroarea pentru lumina constanta pe o latura; {F(Th.LeakFrameAdu, 1)} ADU-eq si de {F(Th.LeakFrameSigmas, 0)} ori abaterea tipica pentru lumina pe un cadru. "
            + $"Ultimele doua spun 'e lumina reala, masurata', nu 'se vede in imagine': la {F(Th.LeakConstantAdu * g.Norm, 2)} ADU brut efectul e la marginea vizibilului, la {F(Th.LeakAdu * g.Norm, 1)} ADU brut se vede; remedierea (ocularul acoperit, seria refacuta) costa la fel in ambele cazuri.";
        return [p1, p2, p3, p4];
    }

    /// <summary>The console's block: the title, the rows, a blank line, the paragraphs wrapped at <see cref="WrapColumns"/>.</summary>
    internal static void Write(DarkSeriesResult r, TextWriter w)
    {
        ArgumentNullException.ThrowIfNull(w);
        w.WriteLine();
        w.WriteLine("  " + Title);
        foreach (ReportRow row in Rows(r))
        {
            w.WriteLine($"    {row.Label,-19}: {row.Value}");
        }

        foreach (string paragraph in Paragraphs(r))
        {
            w.WriteLine();
            foreach (string line in Wrap(paragraph, WrapColumns - 4))
            {
                w.WriteLine("    " + line);
            }
        }
    }

    /// <summary>Breaks a paragraph into lines of at most <paramref name="width"/> characters at spaces; a word longer than the width stands on its own line.</summary>
    internal static IReadOnlyList<string> Wrap(string text, int width)
    {
        ArgumentNullException.ThrowIfNull(text);
        List<string> lines = [];
        StringBuilder line = new();
        foreach (string word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length > 0 && line.Length + 1 + word.Length > width)
            {
                lines.Add(line.ToString());
                line.Clear();
            }

            if (line.Length > 0)
            {
                line.Append(' ');
            }

            line.Append(word);
        }

        if (line.Length > 0)
        {
            lines.Add(line.ToString());
        }

        return lines;
    }

    private static string Electrons(Figures g)
    {
        if (g.Facts is not CameraFacts facts)
        {
            return $"necunoscut pentru {g.First?.Camera ?? "aparatul acestor cadre"}: cat valoreaza 1 ADU in electroni nu e in tabelul aplicatiei";
        }

        if (g.Gain is not double gain || g.First?.Iso is not double iso)
        {
            return $"{facts.Model}: castigul e cunoscut intre ISO {F(facts.Gain[0].Iso, 0)} si {F(facts.Gain[^1].Iso, 0)} ({CameraFacts.SourceWords}), nu la ISO-ul acestor cadre";
        }

        return $"1 ADU = {F(gain, 2)} e- la ISO {F(iso, 0)} ({facts.Model}, {CameraFacts.SourceWords}); 1 ADU-eq = {F(g.Norm, 2)} ADU = {F(gain * g.Norm, 1)} e-; full well {F(facts.FullWellElectrons, 0)} e-, QE {F(facts.QuantumEfficiencyPercent, 0)} %";
    }

    /// <summary>The read noise per pixel; in electrons too when the biases' own ISO has a gain in the table.</summary>
    private static string ReadNoise(DarkSeriesResult r, Figures g)
    {
        if (r.Biases is BiasAnalysis b && !double.IsNaN(b.ReadNoise[g.ReferencePlane]))
        {
            double rn = b.ReadNoise[g.ReferencePlane];
            double? biasGain = g.BiasIso is double iso ? g.Facts?.GainAt(iso) : null;
            string electrons = biasGain is double bg ? $" = {F(rn * bg, 1)} e-" + (g.First?.Iso == g.BiasIso ? "" : $" (la ISO {F(g.BiasIso!.Value, 0)}, al bias-urilor)") : "";
            return $"{F(rn, 2)} ADU pe pixel ({g.Plane}, din bias-uri){electrons}";
        }

        return "NEMASURAT: fara bias-uri" + (g.Facts is null ? "" : $" (sursa da {F(g.Facts.ReadNoiseElectrons, 1)} e- pentru {g.Facts.Model})");
    }

    private static string FrameNoise(DarkSeriesResult r, Figures g)
    {
        string sigma = double.IsNaN(g.Darks.Noise.SigmaFrame) ? "NEMASURAT" : $"sigma {F(g.Darks.Noise.SigmaFrame, 2)} ADU pe pixel pe un dark";
        if (r.DarkCurrent is not DarkCurrent dc)
        {
            return $"{sigma}; curentul de intuneric cere bias-uri";
        }

        string eq = Math.Abs(g.Norm - 1) < 1e-9 ? "" : $" ({F(dc.Median / g.Norm, 2)} {g.Eq})";
        return $"{sigma}; curentul de intuneric median {F(dc.Median, 2)} ADU{eq} la {g.Settings}";
    }

    private static string Found(DarkSeriesResult r, Figures g)
    {
        LeakVerdict l = r.Verdicts.Leak;
        List<string> parts = [];
        if (l.ConstantLight is SideLight c)
        {
            parts.Add($"lumina constanta {Signed(c.LightAdu)} {g.Eq} pe latura de {DarkReport.SideName(c.Side)} = {Signed(c.LightAdu * g.Norm)} ADU" + (g.Gain is double gain ? $" = {Signed(c.LightAdu * g.Norm * gain)} e- pe pixel" : ""));
        }

        if (l.Frames is FrameLight fl && fl.LitFrames.Count > 0)
        {
            double max = fl.LitExcessAdu.Max();
            parts.Add($"lumina pe cadre pana la {Signed(max)} {g.Eq} = {Signed(max * g.Norm)} ADU in {fl.LitFrames.Count} {(fl.LitFrames.Count == 1 ? "cadru" : "cadre")}" + (g.Gain is double gain2 ? $" ({Signed(max * g.Norm * gain2)} e- pe pixel)" : ""));
        }

        if (!double.IsNaN(l.OffsetAdu) && Math.Abs(l.OffsetAdu) > Th.LeakAdu)
        {
            parts.Add($"{DarkReport.RegionName(l.Where)} la {Signed(l.OffsetAdu)} {g.Eq} de centru = {Signed(l.OffsetAdu * g.Norm)} ADU");
        }

        return parts.Count == 0 ? "nimic peste praguri" : string.Join("; ", parts);
    }

    private static string F(double v, int decimals) => DarkReport.F(v, decimals);

    private static string Signed(double v) => DarkReport.Signed(v, 2);
}
