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

using System.Globalization;
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

    /// <summary>The rows of the scale table.</summary>
    internal static IReadOnlyList<ReportRow> Rows(DarkSeriesResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        DarkSeriesAnalysis d = r.Darks;
        int rp = r.ReferencePlane;
        string plane = d.PlaneNames[rp];
        double norm = d.Norm;
        FrameInfo? first = d.Frames.FirstOrDefault(f => f.Error is null)?.Info;
        CameraFacts? facts = CameraFacts.For(first?.Camera);
        double? gain = first?.Iso is double iso ? facts?.GainAt(iso) : null;
        string eq = DarkReport.Eq(d);
        string settings = first?.Iso is double iso2 && first.ExposureSeconds is double exp
            ? $"ISO {F(iso2, 0)}, {InventoryReport.Exposure(exp)} s"
            : "ISO sau expunere necunoscute";
        int bits = (int)Math.Ceiling(Math.Log2(d.White + 1));

        List<ReportRow> rows =
        [
            new("fisier", $"{bits} biti, 0 .. {F(d.White, 0)} ADU; negrul la {F(d.Black, 0)}, albul la {F(d.White, 0)}: semnal util 0 .. {F(d.White - d.Black, 0)} ADU"),
            new("1 ADU-eq", Math.Abs(norm - 1) < 1e-9
                ? $"1 ADU brut: setul e la {settings}, scara de referinta (ISO 400 si 120 s)"
                : $"{F(norm, 2)} ADU brut la {settings} (ADU-eq e ADU adus la ISO 400 si 120 s)"),
            new("cuantizare", $"pas {d.QuantStep} ADU langa negru"),
            new("electroni", Electrons(facts, gain, first, norm)),
            new("zgomot de citire", ReadNoise(r, rp, plane, gain, facts)),
            new("zgomot pe cadru", FrameNoise(r, norm, eq, settings)),
            new("praguri pe set", $"pixel fierbinte {F(Th.HotAdu, 0)} ADU-eq = {F(Th.HotAdu * norm, 0)} ADU; scurgere pe harta {F(Th.LeakAdu, 1)} ADU-eq = {F(Th.LeakAdu * norm, 1)} ADU; lumina constanta pe o latura {F(Th.LeakConstantAdu, 2)} ADU-eq = {F(Th.LeakConstantAdu * norm, 2)} ADU; lumina pe un cadru {F(Th.LeakFrameAdu, 1)} ADU-eq = {F(Th.LeakFrameAdu * norm, 2)} ADU"),
            new("gasit", Found(r, norm, eq, gain)),
            new("cer", Sky(r, rp, plane)),
            new("zgomotul stack-ului", StackNoise(r, rp)),
            new("saturatie", $"{F(d.White, 0)} ADU" + (facts is null ? "" : $" ({F(facts.FullWellElectrons, 0)} e- full well dupa sursa)")),
        ];
        return rows;
    }

    /// <summary>The four paragraphs, with the set's numbers in them.</summary>
    internal static IReadOnlyList<string> Paragraphs(DarkSeriesResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        DarkSeriesAnalysis d = r.Darks;
        int rp = r.ReferencePlane;
        double norm = d.Norm;
        int bits = (int)Math.Ceiling(Math.Log2(d.White + 1));
        string p1 = $"ADU e unitatea fisierului: pe {bits} biti, de la 0 la {F(d.White, 0)}, cu negrul camerei la {F(d.Black, 0)}. "
            + $"ADU-eq e ADU brut adus la ISO 400 si 120 s, ca seriile cu setari diferite sa se compare intre ele: pe acest set 1 ADU-eq = {F(norm, 2)} ADU brut. "
            + $"Un pas de cuantizare are {d.QuantStep} ADU, dar o medie pe multi pixeli se masoara mult sub el, pentru ca zgomotul amesteca treptele.";
        string p2 = "O abatere din masterul dark (o scurgere de lumina, un offset) nu e zgomot: masterul se scade din fiecare light, deci abaterea ramane intreaga in stack, oricate cadre ai aduna, pe cand zgomotul aleator scade cu radacina numarului de cadre. "
            + $"Pe un singur pixel, cei {F(d.Noise.SigmaFrame, 1)} ADU de zgomot ai unui dark acopera orice abatere sub 1 ADU; pe mii de pixeli, o treapta de 1 ADU cu margine dreapta se vede la o intindere tare pe cer intunecat.";
        string p3;
        (double? sky, double? skySigma, int n) = SkyOf(r, rp);
        if (sky is double s && skySigma is double ss && s > 0 && n > 0)
        {
            double stack = ss / Math.Sqrt(n);
            p3 = $"Fata de cer: un light al acestei sesiuni are {F(s, 1)} ADU peste negru, deci 1 ADU e {F(100 / s, 2)} % din cer, cam cat eroarea unui flat; "
                + $"in stack-ul celor {n} lights zgomotul pe pixel scade la {F(ss, 1)} / sqrt({n}) = {F(stack, 2)} ADU, iar un offset de 1 ADU din master ramane 1 ADU.";
        }
        else
        {
            p3 = "Fata de cer: sesiunea nu are lights, deci raportul nu poate spune cat e 1 ADU fata de cerul tau si cat ramane zgomotul in stack; deschide sesiunea cu lights si randurile `cer` si `zgomotul stack-ului` se completeaza.";
        }

        string p4 = $"Pragurile: {F(Th.HotAdu, 0)} ADU-eq pentru un pixel fierbinte; {F(Th.LeakAdu, 1)} ADU-eq pe harta pentru o scurgere evidenta (nivelul analizei de mana); "
            + $"{F(Th.LeakConstantAdu, 2)} ADU-eq si de {F(Th.LeakConstantSigmas, 0)} ori eroarea pentru lumina constanta pe o latura; {F(Th.LeakFrameAdu, 1)} ADU-eq si de {F(Th.LeakFrameSigmas, 0)} ori abaterea tipica pentru lumina pe un cadru. "
            + $"Ultimele doua spun 'e lumina reala, masurata', nu 'se vede in imagine': la {F(Th.LeakConstantAdu * norm, 2)} ADU brut efectul e la marginea vizibilului, la {F(Th.LeakAdu * norm, 1)} ADU brut se vede; remedierea (ocularul acoperit, seria refacuta) costa la fel in ambele cazuri.";
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

    private static string Electrons(CameraFacts? facts, double? gain, FrameInfo? first, double norm)
    {
        if (facts is null)
        {
            return $"necunoscut pentru {first?.Camera ?? "aparatul acestor cadre"}: cat valoreaza 1 ADU in electroni nu e in tabelul aplicatiei";
        }

        if (gain is not double g || first?.Iso is not double iso)
        {
            return $"{facts.Model}: castigul e cunoscut intre ISO {F(facts.Gain[0].Iso, 0)} si {F(facts.Gain[^1].Iso, 0)} ({CameraFacts.SourceWords}), nu la ISO-ul acestor cadre";
        }

        return $"1 ADU = {F(g, 2)} e- la ISO {F(iso, 0)} ({facts.Model}, {CameraFacts.SourceWords}); 1 ADU-eq = {F(norm, 2)} ADU = {F(g * norm, 1)} e-; full well {F(facts.FullWellElectrons, 0)} e-, QE {F(facts.QuantumEfficiencyPercent, 0)} %";
    }

    private static string ReadNoise(DarkSeriesResult r, int rp, string plane, double? gain, CameraFacts? facts)
    {
        if (r.Biases is BiasAnalysis b && !double.IsNaN(b.ReadNoise[rp]))
        {
            double rn = b.ReadNoise[rp];
            return $"{F(rn, 2)} ADU pe pixel ({plane}, din bias-uri)" + (gain is double g ? $" = {F(rn * g, 1)} e-" : "");
        }

        return "NEMASURAT: fara bias-uri" + (facts is null ? "" : $" (sursa da {F(facts.ReadNoiseElectrons, 1)} e- pentru {facts.Model})");
    }

    private static string FrameNoise(DarkSeriesResult r, double norm, string eq, string settings)
    {
        string sigma = double.IsNaN(r.Darks.Noise.SigmaFrame) ? "NEMASURAT" : $"sigma {F(r.Darks.Noise.SigmaFrame, 2)} ADU pe pixel pe un dark";
        return r.DarkCurrent is DarkCurrent dc
            ? $"{sigma}; curentul de intuneric median {F(dc.Median, 2)} ADU{(Math.Abs(norm - 1) < 1e-9 ? "" : $" ({F(dc.Median / norm, 2)} {eq})")} la {settings}"
            : $"{sigma}; curentul de intuneric cere bias-uri";
    }

    private static string Found(DarkSeriesResult r, double norm, string eq, double? gain)
    {
        LeakVerdict l = r.Verdicts.Leak;
        List<string> parts = [];
        if (l.ConstantLight is SideLight c)
        {
            parts.Add($"lumina constanta {Signed(c.LightAdu)} {eq} pe latura de {DarkReport.SideName(c.Side)} = {Signed(c.LightAdu * norm)} ADU" + (gain is double g ? $" = {Signed(c.LightAdu * norm * g)} e- pe pixel" : ""));
        }

        if (l.Frames is FrameLight fl && fl.LitFrames.Count > 0)
        {
            double max = fl.LitExcessAdu.Max();
            parts.Add($"lumina pe cadre pana la {Signed(max)} {eq} = {Signed(max * norm)} ADU in {fl.LitFrames.Count} {(fl.LitFrames.Count == 1 ? "cadru" : "cadre")}" + (gain is double g2 ? $" ({Signed(max * norm * g2)} e- pe pixel)" : ""));
        }

        if (!double.IsNaN(l.OffsetAdu) && Math.Abs(l.OffsetAdu) > Th.LeakAdu)
        {
            parts.Add($"{DarkReport.RegionName(l.Where)} la {Signed(l.OffsetAdu)} {eq} de centru = {Signed(l.OffsetAdu * norm)} ADU");
        }

        return parts.Count == 0 ? "nimic peste praguri" : string.Join("; ", parts);
    }

    private static string Sky(DarkSeriesResult r, int rp, string plane)
    {
        (double? sky, double? sigma, int n) = SkyOf(r, rp);
        return sky is double s && sigma is double ss
            ? $"mediana {F(s, 1)} ADU peste negru pe un light ({plane}, {n} lights), sigma {F(ss, 1)} ADU pe pixel"
            : "fara lights in sesiune";
    }

    private static string StackNoise(DarkSeriesResult r, int rp)
    {
        (_, double? sigma, int n) = SkyOf(r, rp);
        return sigma is double ss && n > 0
            ? $"sigma pe pixel {F(ss, 1)} / sqrt({n}) = {F(ss / Math.Sqrt(n), 2)} ADU; un offset din master ramane intreg in stack"
            : "fara lights: nu se poate estima";
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

    private static string F(double v, int decimals) => double.IsNaN(v) ? "-" : RunReport.F(v, decimals);

    private static string Signed(double v)
    {
        double rounded = Math.Round(v, 2, MidpointRounding.AwayFromZero);
        return (rounded < 0 ? "-" : "+") + Math.Abs(rounded).ToString("F2", CultureInfo.InvariantCulture);
    }
}
