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
using static DarksCheck.Core.Ui;

namespace DarksCheck.Core;

/// <summary>
/// The scale section (R-080, ADR 0015, ADR 0016): what an ADU is on the open set, as rows the
/// console prints and the window binds, and four paragraphs that say why a fraction of an ADU
/// in the master can matter. Every figure comes from the result; the electrons come from
/// <see cref="CameraFacts"/>, with their provenance in the words; the masters' noise and the
/// light found are marked bold for the window.
/// </summary>
internal static class ScaleReport
{
    /// <summary>The section's title, in the run's language; the window's tab shows it too.</summary>
    internal static string Title => T("ADU SCALE EXPLAINED", "EXPLICATII SCARA ADU");

    /// <summary>The console's wrap width for the paragraphs.</summary>
    private const int WrapColumns = 96;

    /// <summary>
    /// Under this white level the file's values are floats (a scaled FITS), not integer ADU, and no
    /// bit depth is read from it.
    /// </summary>
    private const double MinIntegerWhite = 255;

    /// <summary>The figures of the set the rows and the paragraphs share, computed once.</summary>
    /// <param name="Darks">The dark series.</param>
    /// <param name="ReferencePlane">The reference CFA position.</param>
    /// <param name="Plane">The reference position's name.</param>
    /// <param name="Norm">The normalisation factor: raw ADU per ADU-eq.</param>
    /// <param name="Eq">The unit word of the figures, "ADU" or "ADU-eq".</param>
    /// <param name="Bits">The file's bit depth read from its white level, or null for a float
    /// file.</param>
    /// <param name="First">The first readable dark's metadata, or null when none was read.</param>
    /// <param name="Settings">That dark's ISO and exposure in words, or the words for unknown, in
    /// the run's language.</param>
    /// <param name="SettingsKnown">True when that dark states both its ISO and its
    /// exposure.</param>
    /// <param name="Facts">The camera's published figures, or null for a camera not in the
    /// table.</param>
    /// <param name="Gain">The electrons per ADU at the darks' ISO, or null when unknown.</param>
    /// <param name="BiasIso">The ISO of the first readable bias, or null.</param>
    /// <param name="Stack">The lights' sky figures, or null without lights.</param>
    /// <param name="MasterNoise">The dark master's noise per pixel in raw ADU; NaN only when the
    /// frames' own noise could not be measured (a single frame gets a figure predicted from its
    /// noise, R-024).</param>
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
        StackNoise? Stack,
        double MasterNoise)
    {
        /// <summary>Computes the shared figures from the run's result.</summary>
        /// <param name="r">The run's result.</param>
        /// <returns>The figures.</returns>
        internal static Figures Of(DarkSeriesResult r)
        {
            DarkSeriesAnalysis d = r.Darks;
            int rp = r.ReferencePlane;
            FrameInfo? first = d.Frames.FirstOrDefault(f => f.Error is null)?.Info;
            bool known = first?.Iso is double && first.ExposureSeconds is double;
            string settings = known
                ? $"ISO {F(first!.Iso!.Value, 0)}, {InventoryReport.Exposure(first.ExposureSeconds!.Value)} s"
                : T("ISO or exposure unknown", "ISO sau expunere necunoscute");
            CameraFacts? facts = CameraFacts.For(first?.Camera);
            double? gain = first?.Iso is double iso ? facts?.GainAt(iso) : null;
            double? biasIso = r.Biases?.Frames.FirstOrDefault(b => b.Error is null && !b.Skipped)?.Info.Iso;
            return new Figures(d, rp, d.PlaneNames[rp], d.Norm, DarkReport.Eq(d),
                d.White >= MinIntegerWhite ? (int)Math.Ceiling(Math.Log2(d.White + 1)) : null,
                first, settings, known, facts, gain, biasIso, StackNoise.Of(r.Lights, rp, d.Noise.MasterNoise), d.Noise.MasterNoise);
        }
    }

    /// <summary>The rows of the scale table.</summary>
    /// <param name="r">The run's result; not null.</param>
    /// <returns>The rows, in the console's order; the masters' noise and the light found carry
    /// their bold parts.</returns>
    internal static IReadOnlyList<ReportRow> Rows(DarkSeriesResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        Figures g = Figures.Of(r);
        DarkSeriesAnalysis d = g.Darks;
        return
        [
            new(T("file", "fisier"), g.Bits is int bits
                ? T($"values from 0 to {F(d.White, 0)} ADU ({bits} bits); the camera's black is at {F(d.Black, 0)} and the white at {F(d.White, 0)}, so 0 .. {F(d.White - d.Black, 0)} ADU are left for light",
                    $"valori de la 0 la {F(d.White, 0)} ADU ({bits} biti); negrul camerei e la {F(d.Black, 0)} si albul la {F(d.White, 0)}, deci pentru lumina raman 0 .. {F(d.White - d.Black, 0)} ADU")
                : T($"values with decimals (floating point), from 0 to {F(d.White, 3)}; the black at {F(d.Black, 3)}",
                    $"valori cu zecimale (virgula mobila), de la 0 la {F(d.White, 3)}; negrul la {F(d.Black, 3)}")),
            new("1 ADU-eq", !g.SettingsKnown
                ? T("the files do not give the ISO or the exposure, so 1 ADU-eq is taken equal to 1 ADU in the file", "fisierele nu spun ISO-ul sau expunerea, deci 1 ADU-eq e luat egal cu 1 ADU din fisier")
                : Math.Abs(g.Norm - 1) < 1e-9
                    ? T($"1 ADU in the file: the set was taken at the reference settings themselves, {g.Settings}", $"1 ADU din fisier: setul e facut chiar la setarile de referinta, {g.Settings}")
                    : T($"{F(g.Norm, 2)} ADU in the file at {g.Settings} (ADU-eq is the ADU as it would be at ISO 400 and 120 s)", $"{F(g.Norm, 2)} ADU din fisier la {g.Settings} (ADU-eq e ADU-ul recalculat ca pentru ISO 400 si 120 s)")),
            new(T("value step", "pasul valorilor"), T($"near the black, the file's values go up {d.QuantStep} by {d.QuantStep} ADU", $"langa negru, valorile din fisier cresc din {d.QuantStep} in {d.QuantStep} ADU")),
            new(T("electrons", "electroni"), Electrons(g)),
            new(T("read noise", "zgomot de citire"), ReadNoise(r, g)),
            new(T("noise on one dark", "zgomot pe un dark"), FrameNoise(r, g)),
            MasterNoiseRow(g),
            new(T("this set's limits", "praguri pe set"), T(
                $"hot pixel: above {F(Th.HotAdu, 0)} ADU-eq ({F(Th.HotAdu * g.Norm, 0)} ADU in the file); light leak on the map: above {F(Th.LeakAdu, 1)} ADU-eq ({F(Th.LeakAdu * g.Norm, 1)} ADU); constant light on a side: above {F(Th.LeakConstantAdu, 2)} ADU-eq ({F(Th.LeakConstantAdu * g.Norm, 2)} ADU); light on a single frame: above {F(Th.LeakFrameAdu, 1)} ADU-eq ({F(Th.LeakFrameAdu * g.Norm, 2)} ADU)",
                $"pixel fierbinte: peste {F(Th.HotAdu, 0)} ADU-eq ({F(Th.HotAdu * g.Norm, 0)} ADU in fisier); scurgere de lumina pe harta: peste {F(Th.LeakAdu, 1)} ADU-eq ({F(Th.LeakAdu * g.Norm, 1)} ADU); lumina constanta pe o latura: peste {F(Th.LeakConstantAdu, 2)} ADU-eq ({F(Th.LeakConstantAdu * g.Norm, 2)} ADU); lumina pe un singur cadru: peste {F(Th.LeakFrameAdu, 1)} ADU-eq ({F(Th.LeakFrameAdu * g.Norm, 2)} ADU)")),
            Found(r, g),
            new(T("sky", "cer"), g.Stack is StackNoise st
                ? T($"on a light, the sky is at {F(st.SkyAdu, 1)} ADU above the black, with {F(st.SkySigmaAdu, 1)} ADU of noise per pixel ({PlaneWords(g.Plane)}, from {st.Lights} lights)",
                    $"pe un light, cerul e la {F(st.SkyAdu, 1)} ADU peste negru, cu {F(st.SkySigmaAdu, 1)} ADU de zgomot pe pixel ({PlaneWords(g.Plane)}, din {st.Lights} lights)")
                : T("no lights in the session", "fara lights in sesiune")),
            new(T("saturation", "saturatie"), T($"at {F(d.White, 0)} ADU the pixel is full and cannot go higher", $"la {F(d.White, 0)} ADU pixelul e plin si nu mai poate creste")
                + (g.Facts is null ? "" : T($"; per the source, a pixel holds {F(g.Facts.FullWellElectrons, 0)} electrons (full well)", $"; dupa sursa, intr-un pixel incap {F(g.Facts.FullWellElectrons, 0)} electroni (full well)"))),
        ];
    }

    /// <summary>
    /// The pixels a figure on the reference position was measured on, for the rows: its colour and
    /// name when it is a green, its name alone otherwise (a colour order the file does not give).
    /// </summary>
    /// <param name="plane">The reference position's name, such as <c>G1</c> or <c>CFA[0,1]</c>.</param>
    /// <returns>The words, in the run's language.</returns>
    private static string PlaneWords(string plane) =>
        plane.StartsWith('G') ? T($"the green pixels {plane}", $"pixelii verzi {plane}") : T($"the pixels {plane}", $"pixelii {plane}");

    /// <summary>The four paragraphs, with the set's numbers in them.</summary>
    /// <param name="r">The run's result; not null.</param>
    /// <returns>The four paragraphs, in the run's language, unwrapped.</returns>
    internal static IReadOnlyList<string> Paragraphs(DarkSeriesResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        Figures g = Figures.Of(r);
        DarkSeriesAnalysis d = g.Darks;
        string scale = g.Bits is int bits
            ? T($"here whole numbers from 0 to {F(d.White, 0)} ({bits} bits), with the camera's black at {F(d.Black, 0)}", $"aici numere intregi de la 0 la {F(d.White, 0)} ({bits} biti), cu negrul camerei la {F(d.Black, 0)}")
            : T($"here values with decimals (floating point) from 0 to {F(d.White, 3)}, with the black at {F(d.Black, 3)}", $"aici valori cu zecimale (virgula mobila) de la 0 la {F(d.White, 3)}, cu negrul la {F(d.Black, 3)}");
        // The camera the table does not know: the paragraph says so, not only the electrons row (ADR 0023).
        string unknownCamera = g.Facts is not null ? "" : T(
            (g.First?.Camera is string cameraEn ? $" {cameraEn} is not in the application's table" : " The files do not say which camera took them")
            + ", so no figure here is given in electrons, and the source's figures are missing: how many electrons a pixel holds (full well) and the read noise.",
            (g.First?.Camera is string cameraRo ? $" Aparatul {cameraRo} nu e in tabelul aplicatiei" : " Fisierele nu spun ce aparat le-a facut")
            + ", asa ca nicio cifra de aici nu e data in electroni si lipsesc cifrele din sursa: cati electroni incap intr-un pixel (full well) si zgomotul de citire.");
        string p1 = T(
            $"ADU is the unit in which the file writes how bright each pixel is: {scale}. "
            + $"ADU-eq is the same ADU worked out as if the frame had been taken at ISO 400 and 120 s, so that you can compare series taken with different settings: on this set 1 ADU-eq = {F(g.Norm, 2)} ADU in the file. "
            + $"The file goes up {d.QuantStep} by {d.QuantStep} ADU, but the mean of many pixels can come out as a fraction of an ADU, because the noise makes some pixels land on one step and others on the next.",
            $"ADU e unitatea in care fisierul scrie cat de luminos e fiecare pixel: {scale}. "
            + $"ADU-eq e acelasi ADU recalculat ca si cum cadrul ar fi fost facut la ISO 400 si 120 s, ca sa poti compara serii facute cu setari diferite: pe acest set 1 ADU-eq = {F(g.Norm, 2)} ADU din fisier. "
            + $"Fisierul urca din {d.QuantStep} in {d.QuantStep} ADU, dar media multor pixeli poate iesi si o fractiune de ADU, pentru ca zgomotul face ca unii pixeli sa cada pe o treapta si altii pe urmatoarea.")
            + unknownCamera;
        string masterWords = double.IsNaN(g.MasterNoise) ? "" : T(
            $" The dark master, made of {d.Used} frames, still has {F(g.MasterNoise, 2)} ADU of noise per pixel; that is what you compare an error with, not the noise of a single frame.",
            $" Masterul dark, facut din {d.Used} cadre, mai are {F(g.MasterNoise, 2)} ADU de zgomot pe pixel; cu acesta compari o greseala, nu cu zgomotul unui singur cadru.");
        string p2 = T(
            "An error in the dark master is not noise. For example: light that got into the camera, or a black level moved a little. The master is subtracted from every light, so the error reaches the stack whole, however many frames you add. The noise, on the other hand, falls as you add frames: 4 times more frames halve it."
            + masterWords
            + $" On a single pixel, the {F(d.Noise.SigmaFrame, 1)} ADU of noise of one dark hides any error below 1 ADU. But the eye sees areas, not pixels: on an area of 50 x 50 pixels the noise evens out, and the error shows 50 times better than on one pixel. That is why a step of 1 ADU with a straight edge shows when you stretch a dark sky hard.",
            "O greseala din masterul dark nu e zgomot. De exemplu: lumina intrata in aparat, sau un nivel de negru putin mutat. Masterul se scade din fiecare light, asa ca greseala ajunge intreaga in stack, oricate cadre ai aduna. Zgomotul, in schimb, scade cand aduni cadre: de 4 ori mai multe cadre il injumatatesc."
            + masterWords
            + $" Pe un singur pixel, cei {F(d.Noise.SigmaFrame, 1)} ADU de zgomot ai unui dark ascund orice greseala sub 1 ADU. Dar ochiul vede zone, nu pixeli: intr-o zona de 50 x 50 pixeli zgomotul se compenseaza, iar greseala se vede de 50 de ori mai bine decat pe un pixel. De aceea o treapta de 1 ADU cu margine dreapta se vede cand intinzi tare (stretch) un cer intunecat.");
        string p3 = g.Stack is StackNoise st && st.SkyAdu > 0
            ? T(
                $"Against the sky: on a light of this session the sky is at {F(st.SkyAdu, 1)} ADU above the black, so 1 ADU is {F(100 / st.SkyAdu, 2)} % of the sky, about as much as a flat is usually off. "
                + $"In the stack of the {st.Lights} lights the noise per pixel falls to {F(st.MeanStackAdu, 2)} ADU if you make it with the mean ({F(st.MedianStackAdu, 2)} with the median). Two noises go into this figure. The lights' noise ({F(st.SkySigmaAdu, 1)} ADU on one light) falls the more lights you add. The dark master's noise ({F(st.MasterNoiseAdu, 2)} ADU) does not fall, because the same master is subtracted from every light. And an error of 1 ADU in the master stays 1 ADU in the stack.",
                $"Fata de cer: pe un light al acestei sesiuni cerul e la {F(st.SkyAdu, 1)} ADU peste negru, deci 1 ADU e {F(100 / st.SkyAdu, 2)} % din cer, cam cat greseste de obicei un flat. "
                + $"In stack-ul celor {st.Lights} lights zgomotul pe pixel scade la {F(st.MeanStackAdu, 2)} ADU daca il faci cu media ({F(st.MedianStackAdu, 2)} cu mediana). In cifra asta intra doua zgomote. Al lights-urilor ({F(st.SkySigmaAdu, 1)} ADU pe un light) scade cu cat aduni mai multe. Al masterului dark ({F(st.MasterNoiseAdu, 2)} ADU) nu scade, pentru ca acelasi master se scade din toate lights-urile. Iar o greseala de 1 ADU din master ramane 1 ADU in stack.")
            : T("Against the sky: the session has no lights, so there is no saying how much 1 ADU is against your sky, nor how much noise stays in the stack. Open the session together with its lights and the rows `master noise` and `sky` are filled in.",
                "Fata de cer: sesiunea nu are lights, deci nu se poate spune cat inseamna 1 ADU fata de cerul tau, nici cat zgomot ramane in stack. Deschide sesiunea impreuna cu lights-urile ei si randurile `zgomot in master` si `cer` se completeaza.");
        string p4 = T(
            $"The thresholds: a pixel is hot above {F(Th.HotAdu, 0)} ADU-eq. On the map of the Maps tab, a light leak is obvious above {F(Th.LeakAdu, 1)} ADU-eq (the threshold of the analysis made by hand). "
            + $"Constant light on a side counts above {F(Th.LeakConstantAdu, 2)} ADU-eq, and only if it is more than {F(Th.LeakConstantSigmas, 0)} times what the measurement can be off by. Light on a single frame counts above {F(Th.LeakFrameAdu, 1)} ADU-eq, and only if it is more than {F(Th.LeakFrameSigmas, 0)} times what a frame usually differs from the others. "
            + $"The last two thresholds say 'this is real light, measured', not 'it shows in the image': at {F(Th.LeakConstantAdu * g.Norm, 2)} ADU in the file it would barely show, at {F(Th.LeakAdu * g.Norm, 1)} ADU it shows. The remedy is the same in both cases: cover the viewfinder and take the series again.",
            $"Pragurile: un pixel e fierbinte peste {F(Th.HotAdu, 0)} ADU-eq. Pe harta din tab-ul Harti, o scurgere de lumina e evidenta peste {F(Th.LeakAdu, 1)} ADU-eq (pragul din analiza facuta de mana). "
            + $"Lumina constanta pe o latura conteaza peste {F(Th.LeakConstantAdu, 2)} ADU-eq, si doar daca e de peste {F(Th.LeakConstantSigmas, 0)} ori mai mare decat cat poate gresi masuratoarea. Lumina pe un singur cadru conteaza peste {F(Th.LeakFrameAdu, 1)} ADU-eq, si doar daca e de peste {F(Th.LeakFrameSigmas, 0)} ori mai mare decat cat difera de obicei un cadru de celelalte. "
            + $"Ultimele doua praguri spun 'aici e lumina reala, masurata', nu 'se vede in imagine': la {F(Th.LeakConstantAdu * g.Norm, 2)} ADU in fisier abia s-ar vedea, la {F(Th.LeakAdu * g.Norm, 1)} ADU se vede. Remediul e acelasi in ambele cazuri: acoperi vizorul si refaci seria.");
        return [p1, p2, p3, p4];
    }

    /// <summary>
    /// The console's block: the title, the rows, a blank line, the paragraphs wrapped at
    /// <see cref="WrapColumns"/>.
    /// </summary>
    /// <param name="r">The run's result.</param>
    /// <param name="w">The writer the block goes to; not null.</param>
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

    /// <summary>
    /// Breaks a paragraph into lines of at most <paramref name="width"/> characters at spaces; a
    /// word longer than the width stands on its own line.
    /// </summary>
    /// <param name="text">The paragraph; not null. Runs of spaces count as one.</param>
    /// <param name="width">The longest line, in characters.</param>
    /// <returns>The lines, without their leading indent; empty for a blank text.</returns>
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

    /// <summary>
    /// The words that set a light in raw ADU against the masters' noise, and the figures to mark
    /// bold; empty without a master noise (one frame).
    /// </summary>
    /// <param name="rawAdu">The light, in raw ADU.</param>
    /// <param name="masterNoise">The dark master's noise per pixel in raw ADU; NaN or not positive
    /// when unmeasured.</param>
    /// <param name="stack">The lights' stack figures, which add the stack's ratio when its noise is
    /// positive; null for none.</param>
    /// <returns>The words in the run's language and the substrings to render bold; an empty text
    /// and no bold when unmeasured.</returns>
    internal static (string Words, IReadOnlyList<string> Bold) AgainstMasters(double rawAdu, double masterNoise, StackNoise? stack)
    {
        if (double.IsNaN(masterNoise) || masterNoise <= 0)
        {
            return ("", []);
        }

        string raw = $"{Signed(rawAdu)} ADU";
        string master = $"{F(masterNoise, 2)} ADU";
        List<string> bold = [raw, master];
        string words = T($"{raw} is {F(rawAdu / masterNoise, 2)} times the dark master's noise per pixel ({master})", $"{raw} e {F(rawAdu / masterNoise, 2)} din zgomotul pe pixel al masterului dark ({master})");
        if (stack is StackNoise st && st.MeanStackAdu > 0)
        {
            string stackWords = $"{F(st.MeanStackAdu, 1)} ADU";
            bold.Add(stackWords);
            words += T($" and {F(rawAdu / st.MeanStackAdu, 2)} times the noise of the lights stack ({stackWords})", $" si {F(rawAdu / st.MeanStackAdu, 2)} din zgomotul stack-ului de lights ({stackWords})");
        }

        return (words, bold);
    }

    /// <summary>
    /// The <c>master noise</c> row (<c>zgomot in master</c>): the dark master's noise per pixel and
    /// the lights' stack's, both bold (ADR 0016).
    /// </summary>
    /// <param name="g">The set's figures.</param>
    /// <returns>The row; UNMEASURED (NEMASURAT) when the master's noise is NaN.</returns>
    private static ReportRow MasterNoiseRow(Figures g)
    {
        if (double.IsNaN(g.MasterNoise))
        {
            return new(MasterNoiseLabel, T("UNMEASURED: the frames' noise could not be measured, so neither could the master's", "NEMASURAT: zgomotul cadrelor nu s-a putut masura, deci nici al masterului"));
        }

        string dark = $"{F(g.MasterNoise, 2)} ADU";
        List<string> bold = [dark];
        string eq = Math.Abs(g.Norm - 1) < 1e-9 ? "" : $" ({F(g.MasterNoise / g.Norm, 2)} ADU-eq)";
        string darkWords = T($"the dark master, made of {g.Darks.Used} frames, still has {dark}{eq} of noise per pixel", $"masterul dark, facut din {g.Darks.Used} cadre, mai are {dark}{eq} de zgomot pe pixel");
        string stackWords;
        if (g.Stack is StackNoise st)
        {
            string mean = $"{F(st.MeanStackAdu, 1)} ADU";
            bold.Add(mean);
            stackWords = T(
                $"the stack of the {st.Lights} lights has {mean} per pixel if you make it with the mean and {F(st.MedianStackAdu, 1)} if you make it with the median (the dark master's noise included)",
                $"stack-ul celor {st.Lights} lights are {mean} pe pixel daca il faci cu media si {F(st.MedianStackAdu, 1)} daca il faci cu mediana (cu zgomotul masterului dark inclus)");
        }
        else
        {
            stackWords = T("the lights stack: the session has no lights", "stack-ul de lights: sesiunea nu are lights");
        }

        return new(MasterNoiseLabel, $"{darkWords}; {stackWords}", bold);
    }

    /// <summary>The label of the masters' noise row, which the third paragraph names too.</summary>
    private static string MasterNoiseLabel => T("master noise", "zgomot in master");

    /// <summary>
    /// The <c>electrons</c> row: what one ADU and one ADU-eq are in electrons at the darks' ISO,
    /// with the camera's full well and QE.
    /// </summary>
    /// <param name="g">The set's figures.</param>
    /// <returns>The row's value; the reason when the camera or its ISO is not in the
    /// table.</returns>
    private static string Electrons(Figures g)
    {
        if (g.Facts is not CameraFacts facts)
        {
            return T(
                $"unknown for {g.First?.Camera ?? "the camera of these frames"}: the application's table does not say how many electrons 1 ADU is on this camera",
                $"necunoscut pentru {g.First?.Camera ?? "aparatul acestor cadre"}: aplicatia nu are in tabel cati electroni inseamna 1 ADU la acest aparat");
        }

        if (g.Gain is not double gain || g.First?.Iso is not double iso)
        {
            return T(
                $"{facts.Model}: the electrons per ADU are known only between ISO {F(facts.Gain[0].Iso, 0)} and {F(facts.Gain[^1].Iso, 0)} ({CameraFacts.SourceWords}), not at the ISO of these frames",
                $"{facts.Model}: cati electroni inseamna 1 ADU se stie doar intre ISO {F(facts.Gain[0].Iso, 0)} si {F(facts.Gain[^1].Iso, 0)} ({CameraFacts.SourceWords}), nu si la ISO-ul acestor cadre");
        }

        return T(
            $"at ISO {F(iso, 0)}, 1 ADU = {F(gain, 2)} electrons (the sensor turns light into electrons, and the camera counts them in ADU); 1 ADU-eq = {F(g.Norm, 2)} ADU = {F(gain * g.Norm, 1)} electrons; a pixel holds {F(facts.FullWellElectrons, 0)} electrons (full well), and it catches {F(facts.QuantumEfficiencyPercent, 0)} % of the light that falls on it (QE). Figures for {facts.Model} from {CameraFacts.SourceWords}.",
            $"la ISO {F(iso, 0)}, 1 ADU = {F(gain, 2)} electroni (senzorul transforma lumina in electroni, iar camera ii numara in ADU); 1 ADU-eq = {F(g.Norm, 2)} ADU = {F(gain * g.Norm, 1)} electroni; intr-un pixel incap {F(facts.FullWellElectrons, 0)} electroni (full well), iar pixelul prinde {F(facts.QuantumEfficiencyPercent, 0)} % din lumina care cade pe el (QE). Cifre pentru {facts.Model} de pe {CameraFacts.SourceWords}.");
    }

    /// <summary>
    /// The read noise per pixel; in electrons too when the biases' own ISO has a gain in the table.
    /// </summary>
    /// <param name="r">The run's result.</param>
    /// <param name="g">The set's figures.</param>
    /// <returns>The row's value; UNMEASURED (NEMASURAT) without biases, with the published figure
    /// when the camera is known.</returns>
    private static string ReadNoise(DarkSeriesResult r, Figures g)
    {
        if (r.Biases is BiasAnalysis b && !double.IsNaN(b.ReadNoise[g.ReferencePlane]))
        {
            double rn = b.ReadNoise[g.ReferencePlane];
            double? biasGain = g.BiasIso is double iso ? g.Facts?.GainAt(iso) : null;
            string electrons = biasGain is double bg
                ? T($" = {F(rn * bg, 1)} electrons", $" = {F(rn * bg, 1)} electroni") + (g.First?.Iso == g.BiasIso ? "" : T($" (at the biases' ISO, {F(g.BiasIso!.Value, 0)})", $" (la ISO-ul bias-urilor, {F(g.BiasIso!.Value, 0)})"))
                : "";
            return T($"{F(rn, 2)} ADU per pixel{electrons}: the noise the camera adds when it reads the sensor, measured on the biases ({PlaneWords(g.Plane)})",
                $"{F(rn, 2)} ADU pe pixel{electrons}: zgomotul pe care camera il adauga cand citeste senzorul, masurat pe bias-uri ({PlaneWords(g.Plane)})");
        }

        return T("UNMEASURED: the session has no biases", "NEMASURAT: sesiunea nu are bias-uri") + (g.Facts is null ? "" : T($" (the source gives {F(g.Facts.ReadNoiseElectrons, 1)} electrons for {g.Facts.Model})", $" (sursa da {F(g.Facts.ReadNoiseElectrons, 1)} electroni pentru {g.Facts.Model})"));
    }

    /// <summary>
    /// The <c>noise on one dark</c> row (<c>zgomot pe un dark</c>): a dark's noise per pixel and
    /// the dark current of the typical pixel (the median).
    /// </summary>
    /// <param name="r">The run's result.</param>
    /// <param name="g">The set's figures.</param>
    /// <returns>The row's value, in the run's language.</returns>
    private static string FrameNoise(DarkSeriesResult r, Figures g)
    {
        string noise = double.IsNaN(g.Darks.Noise.SigmaFrame) ? T("UNMEASURED", "NEMASURAT") : T($"one dark has {F(g.Darks.Noise.SigmaFrame, 2)} ADU of noise per pixel", $"un dark are {F(g.Darks.Noise.SigmaFrame, 2)} ADU de zgomot pe pixel");
        if (r.DarkCurrent is not DarkCurrent dc)
        {
            return noise + T("; the dark current (what a pixel gathers from the sensor's heat) can be measured only with biases", "; curentul de intuneric (ce aduna un pixel din caldura senzorului) se masoara doar cu bias-uri");
        }

        string eq = Math.Abs(g.Norm - 1) < 1e-9 ? "" : $" ({F(dc.Median / g.Norm, 2)} {g.Eq})";
        return T($"{noise}; the dark current (what a typical pixel gathers from the sensor's heat) is {F(dc.Median, 2)} ADU{eq} at {g.Settings}", $"{noise}; curentul de intuneric (ce aduna un pixel obisnuit din caldura senzorului) e {F(dc.Median, 2)} ADU{eq} la {g.Settings}");
    }

    /// <summary>
    /// The <c>what was found</c> row (<c>ce s-a gasit</c>): check 2's constant light, the largest
    /// light on a frame and the map offset over the threshold, each in ADU-eq, ADU in the file and
    /// electrons when known, set against the masters' noise.
    /// </summary>
    /// <param name="r">The run's result.</param>
    /// <param name="g">The set's figures.</param>
    /// <returns>The row with its bold parts; "nothing over the thresholds" when nothing was
    /// found.</returns>
    private static ReportRow Found(DarkSeriesResult r, Figures g)
    {
        LeakVerdict l = r.Verdicts.Leak;
        List<string> parts = [];
        List<string> bold = [];
        if (l.ConstantLight is SideLight c)
        {
            double raw = c.LightAdu * g.Norm;
            (string against, IReadOnlyList<string> b) = AgainstMasters(raw, g.MasterNoise, g.Stack);
            string eq = $"{Signed(c.LightAdu)} {g.Eq}";
            bold.Add(eq);
            bold.AddRange(b);
            parts.Add(T($"constant light on the {DarkReport.SideName(c.Side)} side: {eq} = {Signed(raw)} ADU in the file", $"lumina constanta pe latura de {DarkReport.SideName(c.Side)}: {eq} = {Signed(raw)} ADU in fisier")
                + (g.Gain is double gain ? T($" = {Signed(raw * gain)} electrons per pixel", $" = {Signed(raw * gain)} electroni pe pixel") : "") + (against.Length == 0 ? "" : $"; {against}"));
        }

        if (l.Frames is FrameLight fl && fl.LitFrames.Count > 0)
        {
            double max = fl.LitExcessAdu.Max();
            double raw = max * g.Norm;
            (string against, IReadOnlyList<string> b) = AgainstMasters(raw, g.MasterNoise, g.Stack);
            string eq = $"{Signed(max)} {g.Eq}";
            bold.Add(eq);
            bold.AddRange(b);
            parts.Add(T($"light on frames: up to {eq} = {Signed(raw)} ADU in the file, in {RunReport.Frames(fl.LitFrames.Count)}", $"lumina pe cadre: pana la {eq} = {Signed(raw)} ADU in fisier, in {RunReport.Frames(fl.LitFrames.Count)}")
                + (g.Gain is double gain2 ? T($" ({Signed(raw * gain2)} electrons per pixel)", $" ({Signed(raw * gain2)} electroni pe pixel)") : "") + (against.Length == 0 ? "" : $"; {against}"));
        }

        if (!double.IsNaN(l.OffsetAdu) && Math.Abs(l.OffsetAdu) > Th.LeakAdu)
        {
            parts.Add(T($"{DarkReport.RegionName(l.Where)}: {Signed(l.OffsetAdu)} {g.Eq} against the centre ({Signed(l.OffsetAdu * g.Norm)} ADU in the file)", $"{DarkReport.RegionName(l.Where)}: {Signed(l.OffsetAdu)} {g.Eq} fata de centru ({Signed(l.OffsetAdu * g.Norm)} ADU in fisier)"));
        }

        string label = T("what was found", "ce s-a gasit");
        return parts.Count == 0 ? new(label, T("nothing over the thresholds", "nimic peste praguri")) : new(label, string.Join("; ", parts), bold);
    }

    /// <summary>A figure with fixed decimals, as the darks report prints it.</summary>
    /// <param name="v">The figure; NaN prints "-".</param>
    /// <param name="decimals">The number of decimals.</param>
    /// <returns>The formatted figure.</returns>
    private static string F(double v, int decimals) => DarkReport.F(v, decimals);

    /// <summary>A signed figure with two decimals, as the darks report prints it.</summary>
    /// <param name="v">The figure.</param>
    /// <returns>The formatted figure.</returns>
    private static string Signed(double v) => DarkReport.Signed(v, 2);
}
