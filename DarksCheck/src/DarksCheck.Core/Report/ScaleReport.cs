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
    /// <param name="MasterNoise">The dark master's noise per pixel in raw ADU; NaN for a single
    /// frame.</param>
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
                ? T($"{bits} bits, 0 .. {F(d.White, 0)} ADU; black at {F(d.Black, 0)}, white at {F(d.White, 0)}: useful signal 0 .. {F(d.White - d.Black, 0)} ADU",
                    $"{bits} biti, 0 .. {F(d.White, 0)} ADU; negrul la {F(d.Black, 0)}, albul la {F(d.White, 0)}: semnal util 0 .. {F(d.White - d.Black, 0)} ADU")
                : T($"floating-point values, file scale 0 .. {F(d.White, 3)}; black at {F(d.Black, 3)}",
                    $"valori in virgula mobila, scara fisierului 0 .. {F(d.White, 3)}; negrul la {F(d.Black, 3)}")),
            new("1 ADU-eq", !g.SettingsKnown
                ? T("ISO or exposure unknown: ADU-eq is taken equal to raw ADU", "ISO sau expunere necunoscute: ADU-eq e luat egal cu ADU brut")
                : Math.Abs(g.Norm - 1) < 1e-9
                    ? T($"1 raw ADU: the set is at {g.Settings}, the reference scale (ISO 400 and 120 s)", $"1 ADU brut: setul e la {g.Settings}, scara de referinta (ISO 400 si 120 s)")
                    : T($"{F(g.Norm, 2)} raw ADU at {g.Settings} (ADU-eq is ADU brought to ISO 400 and 120 s)", $"{F(g.Norm, 2)} ADU brut la {g.Settings} (ADU-eq e ADU adus la ISO 400 si 120 s)")),
            new(T("quantisation", "cuantizare"), T($"step of {d.QuantStep} ADU near the black", $"pas {d.QuantStep} ADU langa negru")),
            new(T("electrons", "electroni"), Electrons(g)),
            new(T("read noise", "zgomot de citire"), ReadNoise(r, g)),
            new(T("noise per frame", "zgomot pe cadru"), FrameNoise(r, g)),
            MasterNoiseRow(g),
            new(T("this set's limits", "praguri pe set"), T(
                $"hot pixel {F(Th.HotAdu, 0)} ADU-eq = {F(Th.HotAdu * g.Norm, 0)} ADU; leak on the map {F(Th.LeakAdu, 1)} ADU-eq = {F(Th.LeakAdu * g.Norm, 1)} ADU; constant light on a side {F(Th.LeakConstantAdu, 2)} ADU-eq = {F(Th.LeakConstantAdu * g.Norm, 2)} ADU; light on a frame {F(Th.LeakFrameAdu, 1)} ADU-eq = {F(Th.LeakFrameAdu * g.Norm, 2)} ADU",
                $"pixel fierbinte {F(Th.HotAdu, 0)} ADU-eq = {F(Th.HotAdu * g.Norm, 0)} ADU; scurgere pe harta {F(Th.LeakAdu, 1)} ADU-eq = {F(Th.LeakAdu * g.Norm, 1)} ADU; lumina constanta pe o latura {F(Th.LeakConstantAdu, 2)} ADU-eq = {F(Th.LeakConstantAdu * g.Norm, 2)} ADU; lumina pe un cadru {F(Th.LeakFrameAdu, 1)} ADU-eq = {F(Th.LeakFrameAdu * g.Norm, 2)} ADU")),
            Found(r, g),
            new(T("sky", "cer"), g.Stack is StackNoise st
                ? T($"median {F(st.SkyAdu, 1)} ADU above the black on a light ({g.Plane}, {st.Lights} lights), sigma {F(st.SkySigmaAdu, 1)} ADU per pixel",
                    $"mediana {F(st.SkyAdu, 1)} ADU peste negru pe un light ({g.Plane}, {st.Lights} lights), sigma {F(st.SkySigmaAdu, 1)} ADU pe pixel")
                : T("no lights in the session", "fara lights in sesiune")),
            new(T("saturation", "saturatie"), $"{F(d.White, 0)} ADU" + (g.Facts is null ? "" : T($" ({F(g.Facts.FullWellElectrons, 0)} e- full well, per the source)", $" ({F(g.Facts.FullWellElectrons, 0)} e- full well dupa sursa)"))),
        ];
    }

    /// <summary>The four paragraphs, with the set's numbers in them.</summary>
    /// <param name="r">The run's result; not null.</param>
    /// <returns>The four paragraphs, in the run's language, unwrapped.</returns>
    internal static IReadOnlyList<string> Paragraphs(DarkSeriesResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        Figures g = Figures.Of(r);
        DarkSeriesAnalysis d = g.Darks;
        string scale = g.Bits is int bits
            ? T($"{bits} bits, from 0 to {F(d.White, 0)}, with the camera's black at {F(d.Black, 0)}", $"pe {bits} biti, de la 0 la {F(d.White, 0)}, cu negrul camerei la {F(d.Black, 0)}")
            : T($"here floating-point values from 0 to {F(d.White, 3)}, with the black at {F(d.Black, 3)}", $"aici valori in virgula mobila de la 0 la {F(d.White, 3)}, cu negrul la {F(d.Black, 3)}");
        // The camera the table does not know: the paragraph says so, not only the electroni row (ADR 0023).
        string unknownCamera = g.Facts is not null ? "" : T(
            (g.First?.Camera is string cameraEn ? $" {cameraEn} is not in the application's table" : " The frames do not name the camera")
            + ", so no figure of this section is given in electrons, and the source's full well and read noise are missing.",
            (g.First?.Camera is string cameraRo ? $" Aparatul {cameraRo} nu e in tabelul aplicatiei" : " Cadrele nu spun aparatul")
            + ", deci nicio cifra de aici nu e data in electroni, iar full well-ul si zgomotul de citire din sursa lipsesc.");
        string p1 = T(
            $"ADU is the file's unit: {scale}. "
            + $"ADU-eq is raw ADU brought to ISO 400 and 120 s, so that series taken with different settings compare with one another: on this set 1 ADU-eq = {F(g.Norm, 2)} raw ADU. "
            + $"A quantisation step is {d.QuantStep} ADU, but a mean over many pixels is measured far below it, because the noise blends the steps.",
            $"ADU e unitatea fisierului: {scale}. "
            + $"ADU-eq e ADU brut adus la ISO 400 si 120 s, ca seriile cu setari diferite sa se compare intre ele: pe acest set 1 ADU-eq = {F(g.Norm, 2)} ADU brut. "
            + $"Un pas de cuantizare are {d.QuantStep} ADU, dar o medie pe multi pixeli se masoara mult sub el, pentru ca zgomotul amesteca treptele.")
            + unknownCamera;
        string masterWords = double.IsNaN(g.MasterNoise) ? "" : T(
            $" The dark master, the median of {d.Used} frames, still has {F(g.MasterNoise, 2)} ADU per pixel; a deviation is set against that, not against the noise of one frame.",
            $" Masterul dark, mediana a {d.Used} cadre, mai are {F(g.MasterNoise, 2)} ADU pe pixel; cu el se compara o abatere, nu cu zgomotul unui cadru.");
        string p2 = T(
            "A deviation in the dark master (a light leak, an offset) is not noise: the master is subtracted from every light, so the deviation stays whole in the stack however many frames you add, while random noise falls with the square root of the number of frames."
            + masterWords
            + $" On a single pixel, the noise of one dark, {F(d.Noise.SigmaFrame, 1)} ADU, hides any deviation below 1 ADU; but the ratio of a deviation to the noise grows with the square root of the number of pixels looked at together: on a 50 x 50 patch it is 50 times larger than on one pixel, and a 1 ADU step with a straight edge shows under a hard stretch of a dark sky.",
            "O abatere din masterul dark (o scurgere de lumina, un offset) nu e zgomot: masterul se scade din fiecare light, deci abaterea ramane intreaga in stack, oricate cadre ai aduna, pe cand zgomotul aleator scade cu radacina numarului de cadre."
            + masterWords
            + $" Pe un singur pixel, cei {F(d.Noise.SigmaFrame, 1)} ADU de zgomot ai unui dark acopera orice abatere sub 1 ADU; dar raportul dintre o abatere si zgomot creste cu radacina numarului de pixeli priviti impreuna: pe un petic de 50 x 50 e de 50 de ori mai mare decat pe un pixel, si o treapta de 1 ADU cu margine dreapta se vede la o intindere tare pe cer intunecat.");
        string p3 = g.Stack is StackNoise st && st.SkyAdu > 0
            ? T(
                $"Against the sky: a light of this session has {F(st.SkyAdu, 1)} ADU above the black, so 1 ADU is {F(100 / st.SkyAdu, 2)} % of the sky, about the error of a flat; "
                + $"in the stack of the {st.Lights} lights the noise per pixel falls to {F(st.MeanStackAdu, 2)} ADU with the mean ({F(st.MedianStackAdu, 2)} with the median): {F(st.SkySigmaAdu, 1)} / sqrt({st.Lights}) from the lights, plus the dark master's noise ({F(st.MasterNoiseAdu, 2)} ADU) in quadrature, which does not average out; and an offset of 1 ADU in the master stays 1 ADU.",
                $"Fata de cer: un light al acestei sesiuni are {F(st.SkyAdu, 1)} ADU peste negru, deci 1 ADU e {F(100 / st.SkyAdu, 2)} % din cer, cam cat eroarea unui flat; "
                + $"in stack-ul celor {st.Lights} lights zgomotul pe pixel scade la {F(st.MeanStackAdu, 2)} ADU cu media ({F(st.MedianStackAdu, 2)} cu mediana): {F(st.SkySigmaAdu, 1)} / sqrt({st.Lights}) de la lights, plus zgomotul masterului dark ({F(st.MasterNoiseAdu, 2)} ADU) in cuadratura, care nu se mediaza; iar un offset de 1 ADU din master ramane 1 ADU.")
            : T("Against the sky: the session has no lights, so the report cannot say how much 1 ADU is against your sky and how much noise stays in the stack; open the session with its lights and the rows `master noise` and `sky` are filled in.",
                "Fata de cer: sesiunea nu are lights, deci raportul nu poate spune cat e 1 ADU fata de cerul tau si cat ramane zgomotul in stack; deschide sesiunea cu lights si randurile `zgomot in master` si `cer` se completeaza.");
        string p4 = T(
            $"The thresholds: {F(Th.HotAdu, 0)} ADU-eq for a hot pixel; {F(Th.LeakAdu, 1)} ADU-eq on the map for an obvious leak (the level of the hand analysis); "
            + $"{F(Th.LeakConstantAdu, 2)} ADU-eq and {F(Th.LeakConstantSigmas, 0)} times the error for constant light on a side; {F(Th.LeakFrameAdu, 1)} ADU-eq and {F(Th.LeakFrameSigmas, 0)} times the typical deviation for light on a frame. "
            + $"The last two say 'this is real light, measured', not 'it shows in the image': at {F(Th.LeakConstantAdu * g.Norm, 2)} raw ADU the effect is at the edge of what can be seen, at {F(Th.LeakAdu * g.Norm, 1)} raw ADU it shows; the remedy (the eyepiece covered, the series taken again) costs the same in both cases.",
            $"Pragurile: {F(Th.HotAdu, 0)} ADU-eq pentru un pixel fierbinte; {F(Th.LeakAdu, 1)} ADU-eq pe harta pentru o scurgere evidenta (nivelul analizei de mana); "
            + $"{F(Th.LeakConstantAdu, 2)} ADU-eq si de {F(Th.LeakConstantSigmas, 0)} ori eroarea pentru lumina constanta pe o latura; {F(Th.LeakFrameAdu, 1)} ADU-eq si de {F(Th.LeakFrameSigmas, 0)} ori abaterea tipica pentru lumina pe un cadru. "
            + $"Ultimele doua spun 'e lumina reala, masurata', nu 'se vede in imagine': la {F(Th.LeakConstantAdu * g.Norm, 2)} ADU brut efectul e la marginea vizibilului, la {F(Th.LeakAdu * g.Norm, 1)} ADU brut se vede; remedierea (ocularul acoperit, seria refacuta) costa la fel in ambele cazuri.");
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
            words += T($" and {F(rawAdu / st.MeanStackAdu, 2)} times the lights stack's ({stackWords})", $" si {F(rawAdu / st.MeanStackAdu, 2)} din al stack-ului de lights ({stackWords})");
        }

        return (words, bold);
    }

    /// <summary>
    /// The <c>master noise</c> row (<c>zgomot in master</c>): the dark master's noise per pixel and
    /// the lights' stack's, both bold (ADR 0016).
    /// </summary>
    /// <param name="g">The set's figures.</param>
    /// <returns>The row; UNMEASURED (NEMASURAT) for a single frame.</returns>
    private static ReportRow MasterNoiseRow(Figures g)
    {
        if (double.IsNaN(g.MasterNoise))
        {
            return new(MasterNoiseLabel, T("UNMEASURED: a single frame has no master whose noise could be measured", "NEMASURAT: un singur cadru nu are master cu zgomot de masurat"));
        }

        string dark = $"{F(g.MasterNoise, 2)} ADU";
        List<string> bold = [dark];
        string darkWords = T($"dark: {dark} per pixel (median of {g.Darks.Used} frames", $"dark: {dark} pe pixel (mediana a {g.Darks.Used} cadre") + (Math.Abs(g.Norm - 1) < 1e-9 ? ")" : $"; {F(g.MasterNoise / g.Norm, 2)} ADU-eq)");
        string stackWords;
        if (g.Stack is StackNoise st)
        {
            string mean = $"{F(st.MeanStackAdu, 1)} ADU";
            bold.Add(mean);
            stackWords = T(
                $"lights stack: {mean} per pixel with the mean, {F(st.MedianStackAdu, 1)} with the median ({st.Lights} lights; the dark master's noise included)",
                $"stack lights: {mean} pe pixel cu media, {F(st.MedianStackAdu, 1)} cu mediana ({st.Lights} lights; cu zgomotul masterului dark inclus)");
        }
        else
        {
            stackWords = T("lights stack: no lights", "stack lights: fara lights");
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
                $"unknown for {g.First?.Camera ?? "the camera of these frames"}: what 1 ADU is worth in electrons is not in the application's table",
                $"necunoscut pentru {g.First?.Camera ?? "aparatul acestor cadre"}: cat valoreaza 1 ADU in electroni nu e in tabelul aplicatiei");
        }

        if (g.Gain is not double gain || g.First?.Iso is not double iso)
        {
            return T(
                $"{facts.Model}: the gain is known between ISO {F(facts.Gain[0].Iso, 0)} and {F(facts.Gain[^1].Iso, 0)} ({CameraFacts.SourceWords}), not at the ISO of these frames",
                $"{facts.Model}: castigul e cunoscut intre ISO {F(facts.Gain[0].Iso, 0)} si {F(facts.Gain[^1].Iso, 0)} ({CameraFacts.SourceWords}), nu la ISO-ul acestor cadre");
        }

        return $"1 ADU = {F(gain, 2)} e-" + T(" at ISO ", " la ISO ") + $"{F(iso, 0)} ({facts.Model}, {CameraFacts.SourceWords}); 1 ADU-eq = {F(g.Norm, 2)} ADU = {F(gain * g.Norm, 1)} e-; full well {F(facts.FullWellElectrons, 0)} e-, QE {F(facts.QuantumEfficiencyPercent, 0)} %";
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
            string electrons = biasGain is double bg ? $" = {F(rn * bg, 1)} e-" + (g.First?.Iso == g.BiasIso ? "" : T($" (at ISO {F(g.BiasIso!.Value, 0)}, the biases')", $" (la ISO {F(g.BiasIso!.Value, 0)}, al bias-urilor)")) : "";
            return T($"{F(rn, 2)} ADU per pixel ({g.Plane}, from the biases){electrons}", $"{F(rn, 2)} ADU pe pixel ({g.Plane}, din bias-uri){electrons}");
        }

        return T("UNMEASURED: no biases", "NEMASURAT: fara bias-uri") + (g.Facts is null ? "" : T($" (the source gives {F(g.Facts.ReadNoiseElectrons, 1)} e- for {g.Facts.Model})", $" (sursa da {F(g.Facts.ReadNoiseElectrons, 1)} e- pentru {g.Facts.Model})"));
    }

    /// <summary>
    /// The <c>noise per frame</c> row: a dark's noise per pixel and the median dark current.
    /// </summary>
    /// <param name="r">The run's result.</param>
    /// <param name="g">The set's figures.</param>
    /// <returns>The row's value, in the run's language.</returns>
    private static string FrameNoise(DarkSeriesResult r, Figures g)
    {
        string sigma = double.IsNaN(g.Darks.Noise.SigmaFrame) ? T("UNMEASURED", "NEMASURAT") : T($"sigma {F(g.Darks.Noise.SigmaFrame, 2)} ADU per pixel on one dark", $"sigma {F(g.Darks.Noise.SigmaFrame, 2)} ADU pe pixel pe un dark");
        if (r.DarkCurrent is not DarkCurrent dc)
        {
            return sigma + T("; the dark current needs biases", "; curentul de intuneric cere bias-uri");
        }

        string eq = Math.Abs(g.Norm - 1) < 1e-9 ? "" : $" ({F(dc.Median / g.Norm, 2)} {g.Eq})";
        return T($"{sigma}; median dark current {F(dc.Median, 2)} ADU{eq} at {g.Settings}", $"{sigma}; curentul de intuneric median {F(dc.Median, 2)} ADU{eq} la {g.Settings}");
    }

    /// <summary>
    /// The <c>found</c> row: check 2's constant light, the largest light on a frame and the map
    /// offset over the threshold, each in ADU-eq, raw ADU and electrons when known, set against the
    /// masters' noise.
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
            parts.Add(T($"constant light {eq} on the {DarkReport.SideName(c.Side)} side = {Signed(raw)} ADU", $"lumina constanta {eq} pe latura de {DarkReport.SideName(c.Side)} = {Signed(raw)} ADU")
                + (g.Gain is double gain ? $" = {Signed(raw * gain)} e-" + T(" per pixel", " pe pixel") : "") + (against.Length == 0 ? "" : $"; {against}"));
        }

        if (l.Frames is FrameLight fl && fl.LitFrames.Count > 0)
        {
            double max = fl.LitExcessAdu.Max();
            double raw = max * g.Norm;
            (string against, IReadOnlyList<string> b) = AgainstMasters(raw, g.MasterNoise, g.Stack);
            string eq = $"{Signed(max)} {g.Eq}";
            bold.Add(eq);
            bold.AddRange(b);
            parts.Add(T($"light on frames up to {eq} = {Signed(raw)} ADU in {RunReport.Frames(fl.LitFrames.Count)}", $"lumina pe cadre pana la {eq} = {Signed(raw)} ADU in {RunReport.Frames(fl.LitFrames.Count)}")
                + (g.Gain is double gain2 ? $" ({Signed(raw * gain2)} e-" + T(" per pixel)", " pe pixel)") : "") + (against.Length == 0 ? "" : $"; {against}"));
        }

        if (!double.IsNaN(l.OffsetAdu) && Math.Abs(l.OffsetAdu) > Th.LeakAdu)
        {
            parts.Add(T($"{DarkReport.RegionName(l.Where)} at {Signed(l.OffsetAdu)} {g.Eq} from the centre = {Signed(l.OffsetAdu * g.Norm)} ADU", $"{DarkReport.RegionName(l.Where)} la {Signed(l.OffsetAdu)} {g.Eq} de centru = {Signed(l.OffsetAdu * g.Norm)} ADU"));
        }

        string label = T("found", "gasit");
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
