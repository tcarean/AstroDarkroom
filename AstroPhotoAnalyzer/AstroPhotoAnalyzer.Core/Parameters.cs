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

// Parameters.cs - what the user chooses: the print medium, the candidate sizes, the viewing
// distance, the frame. With the defaults from the conversation that gave birth to the tool.
//
// The console and the window both build a Parameters and hand it to the core; neither knows
// what Dmax a baryta has - that is here, once. Where each Dmax comes from: ADR 0028.

using System;
using System.Collections.Generic;
using System.Linq;

namespace AstroPhotoAnalyzer
{
    /// <summary>
    /// The print media the tool knows, each with the Dmax <see cref="Parameters.DmaxOf"/> gives
    /// it; the enum's order is the order of <see cref="Parameters.MediumNames"/>.
    /// </summary>
    internal enum PrintMedium
    {
        /// <summary>
        /// Glossy pigment baryta paper (Harman Gloss Baryta, Photo Rag Baryta). Dmax 2.4:
        /// measured, 2.41 against the paper's white in the Chromatone workshop's ICC profile for
        /// Hahnemuhle Photo Rag Baryta on a Canon iPF8300 (ADR 0028).
        /// </summary>
        Baryta,

        /// <summary>
        /// Matte canvas, latex or pigment. Dmax 1.5, the weakest black of the three: the rule
        /// from the conversation that gave birth to the tool, backed by the Harman Canvas ICC
        /// profile (1.49 against the canvas's white; ADR 0028).
        /// </summary>
        Canvas,

        /// <summary>
        /// A photographic C-print (Fuji Crystal Archive) under acrylic. Dmax 2.2: the rule from
        /// the conversation that gave birth to the tool, with no measurement behind it
        /// (ADR 0028).
        /// </summary>
        Acrylic,
    }

    /// <summary>
    /// What the user chose for one run, with the core's defaults for everything left unset: the
    /// one place that knows a medium's Dmax, the default sizes and framing, and the rules
    /// between two parameters (<see cref="Validate"/>). Init-only; a variant is made with
    /// <c>with</c>, which shares the two arrays (CandidateWidthsCm, SimulationTargets): the
    /// elements of neither may be changed in place.
    /// </summary>
    internal sealed record Parameters
    {
        internal PrintMedium Medium { get; init; } = PrintMedium.Baryta;

        /// <summary>An explicit Dmax; it beats the medium. Null = from the medium.</summary>
        internal double? DmaxOverride { get; init; }

        internal double Dmax => DmaxOverride ?? DmaxOf(Medium);

        /// <summary>
        /// The Dmax is the user's, not the medium's: a given Dmax that is the medium's own changes
        /// nothing and is not announced "(explicit)" - the window shows its field filled with the
        /// chosen medium's Dmax, and a report that called it explicit would lie.
        /// </summary>
        internal bool DmaxIsExplicit => DmaxOverride.HasValue && Math.Abs(DmaxOverride.Value - DmaxOf(Medium)) > 1e-9;

        /// <summary>
        /// The name of the ICC profile the explicit Dmax was read from (R-028); null when the
        /// Dmax is the medium's or typed by the user. For the texts that say where the figure
        /// comes from (the report, the paper-black row of the Tonuri ladder); CurveRun also
        /// reopens DmaxProfilePath only while it is set.
        /// </summary>
        internal string DmaxProfile { get; init; }

        /// <summary>
        /// The path of the same profile, so that the Editare Curbe tab can reopen it for the gamut figure
        /// (R-144); null when DmaxProfile is null.
        /// </summary>
        internal string DmaxProfilePath { get; init; }

        /// <summary>
        /// The profile's reservation about its Dmax (the CMYK profiles), for Anexa F; null when
        /// there is none.
        /// </summary>
        internal string DmaxProfileNote { get; init; }

        /// <summary>
        /// The same parameters, with the Dmax read from the ICC printer profile at
        /// <paramref name="path"/>: the device's black against the paper's white, to two
        /// decimals (IccPaperBlack). Null, with the reason in <paramref name="error"/>, when the
        /// profile cannot be read or is not a printer's.
        /// </summary>
        /// <param name="path">The ICC profile's file, as the user gave it.</param>
        /// <param name="error">
        /// Set to the reason, a sentence for the user in the interface language, when the profile cannot be used;
        /// null when it can.
        /// </param>
        /// <returns>
        /// A copy with DmaxOverride, DmaxProfile (the profile's description), DmaxProfilePath and
        /// DmaxProfileNote set; null when the profile cannot be used.
        /// </returns>
        internal Parameters WithDmaxFromProfile(string path, out string error)
        {
            IccBlack black = IccPaperBlack.Read(path);
            error = black.Problem;
            return black.Readable ? this with { DmaxOverride = black.Dmax, DmaxProfile = black.Description, DmaxProfilePath = path, DmaxProfileNote = black.Note } : null;
        }

        /// <summary>The candidate print widths, in cm, for the sizes table.</summary>
        internal double[] CandidateWidthsCm { get; init; } = { 50, 60, 70, 80, 90, 100 };

        /// <summary>
        /// The viewing distance, in metres. Null = 1.5 x the diagonal of the printed image.
        /// </summary>
        internal double? ViewingDistanceM { get; init; }

        internal double PassepartoutCm { get; init; } = 7;
        internal double FrameCm { get; init; } = 3;

        /// <summary>
        /// The target aspect ratio (width/height). Null = the native one, with no crop.
        /// </summary>
        internal double? TargetAspect { get; init; }

        /// <summary>
        /// The target background the user gave, optional, rounded to TargetBackgroundDecimals
        /// decimals (the ones the report prints, Fmt.Level): it adds its row to the simulation's
        /// table, as its primary row. Null = no extra row; the primary row is the recommended one
        /// (R-023). A value that after rounding is not strictly between 0 and
        /// 1, NaN or infinite, is refused by Validate with TargetBackgroundError's message
        /// (ADR 0040).
        /// </summary>
        internal double? TargetBackground
        {
            get => _targetBackground;
            init => _targetBackground = value.HasValue ? RoundTargetBackground(value.Value) : null;
        }
        double? _targetBackground;

        /// <summary>
        /// The decimals the given target background is rounded to: the three the report prints
        /// (Fmt.Level), so that a target typed with more is the row the table shows and not one
        /// that nothing finds again (ADR 0040).
        /// </summary>
        internal const int TargetBackgroundDecimals = 3;

        /// <summary>
        /// The target background rounded to TargetBackgroundDecimals decimals; NaN and infinity
        /// stay as they are.
        /// </summary>
        /// <param name="given">The level as typed, 0..1 when valid; any double accepted.</param>
        /// <returns>
        /// The level rounded with Math.Round to TargetBackgroundDecimals decimals.
        /// </returns>
        internal static double RoundTargetBackground(double given) => Math.Round(given, TargetBackgroundDecimals);

        /// <summary>
        /// Why a given target background cannot be simulated, for the user: NaN, infinity, or a
        /// value that after rounding is not strictly between 0 and 1
        /// (gamma = ln(target) / ln(background) does not exist at 0 and at 1). A single message,
        /// the core's: the console writes it with "--target-bg: " in front, the window as it is
        /// (R-023, ADR 0040). Null when the value is good.
        /// </summary>
        /// <param name="given">The level as typed, before rounding; any double accepted.</param>
        /// <returns>
        /// The sentence, in the interface language, which quotes the value given; null when the value is good.
        /// </returns>
        internal static string TargetBackgroundError(double given)
        {
            double v = RoundTargetBackground(given);
            if (double.IsNaN(v) || double.IsInfinity(v) || v <= 0 || v >= 1)
                return Tr.Pick($"the target background must be a number strictly between 0 and 1 (it is rounded to {TargetBackgroundDecimals} decimals); got {Quote(given)}",
                               $"fundalul-tinta trebuie sa fie un numar strict intre 0 si 1 (se rotunjeste la {TargetBackgroundDecimals} zecimale); a primit {Quote(given)}");
            return null;
        }

        /// <summary>
        /// A given value as a refusal quotes it: NaN and infinity in words, since ToString would
        /// write them "NaN" and with the infinity symbol, which the console cannot show in every
        /// code page; any other value with up to nine decimals, the invariant culture.
        /// </summary>
        /// <param name="given">The value as the user gave it; any double.</param>
        /// <returns>
        /// "NaN, care nu e un numar", "infinit", "minus infinit", or the number as text.
        /// </returns>
        static string Quote(double given)
            => double.IsNaN(given) ? Tr.Pick("NaN, which is not a number", "NaN, care nu e un numar")
             : double.IsPositiveInfinity(given) ? Tr.Pick("infinity", "infinit")
             : double.IsNegativeInfinity(given) ? Tr.Pick("minus infinity", "minus infinit")
             : given.ToString("0.#########", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>
        /// Why a number parameter cannot be used: NaN or infinity, which both applications'
        /// parsers read ("NaN", "Infinity", "-Infinity") and whose range checks let NaN through
        /// (every comparison with it is false). One message per parameter, the core's, so that
        /// the console and the window refuse the same values in the same words (R-024, ADR 0045).
        /// </summary>
        /// <param name="what">The parameter's name for the user, in the interface language ("Dmax", "the frame").</param>
        /// <param name="value">The value given; null (not given) is never refused.</param>
        /// <returns>The sentence, in the interface language, quoting the value; null when it is finite or null.</returns>
        static string FiniteError(string what, double? value)
            => value.HasValue && !double.IsFinite(value.Value) ? Tr.Pick($"{what} must be a number; got {Quote(value.Value)}", $"{what} trebuie sa fie un numar; a primit {Quote(value.Value)}") : null;

        /// <summary>
        /// <see cref="FiniteError"/> for a parameter that must also be positive: the aspect
        /// ratio, which the parsers build from two sides, so that "1:Infinity" arrives as 0 and
        /// "Infinity:Infinity" as NaN, past their checks on the sides.
        /// </summary>
        /// <param name="what">The parameter's name for the user, in the interface language.</param>
        /// <param name="value">The value given; null (not given) is never refused.</param>
        /// <returns>The sentence, in the interface language, quoting the value; null when it is finite and positive, or null.</returns>
        static string PositiveError(string what, double? value)
            => value.HasValue && !(double.IsFinite(value.Value) && value.Value > 0) ? Tr.Pick($"{what} must be a positive number; got {Quote(value.Value)}", $"{what} trebuie sa fie un numar pozitiv; a primit {Quote(value.Value)}") : null;

        /// <summary>
        /// The simulation's grid: 0.05 .. 0.20 in steps of 0.01, the levels a night sky is
        /// printed at (over 0.20 a washed-out grey). The floor was 0.10, the black of a paper of
        /// Dmax 2.0 (L* 9); on a baryta of 2.4 (L* 3.6) the dust reaches black under 0.10 (0.093
        /// on the reference image, 0.067 on Rosetta) and those rows were missing from the table,
        /// though the exact target's ladder already went down to ExactTargetLo = 0.05; the owner
        /// asked for the grid from 0.05 on 2026-10-06 (ADR 0034). It is not exposed; a target
        /// outside it comes in through TargetBackground.
        /// </summary>
        internal double[] SimulationTargets { get; init; } = DefaultSimulationTargets();

        /// <summary>The default grid of <see cref="SimulationTargets"/>.</summary>
        /// <returns>
        /// The 16 levels 0.05, 0.06, ... 0.20, each rounded to two decimals; a new array each
        /// call.
        /// </returns>
        static double[] DefaultSimulationTargets()
        {
            var t = new double[16];
            for (int i = 0; i < t.Length; i++) t[i] = Math.Round(0.05 + 0.01 * i, 2);
            return t;
        }

        /// <summary>
        /// A FITS whose WCS is lent to the analysed image (R-027): a TIFF or PNG exported from the
        /// same stack does not carry the header, but has the same geometry. Null = the file's own
        /// WCS, if it has one.
        /// </summary>
        internal string WcsPath { get; init; }

        /// <summary>
        /// The seed of the choice of tiles. Fixed, so that two runs on the same file give the same
        /// figures.
        /// </summary>
        internal int Seed { get; init; } = 0;

        /// <summary>
        /// What the applications' syntax checks cannot judge: the target background after
        /// rounding (TargetBackgroundError, R-023), then NaN and infinity in each number
        /// parameter, in the parameters' order (FiniteError, PositiveError for the aspect;
        /// R-024), then the rule that ties the passepartout to the frame (R-025). Returns the
        /// message for the user, or null when all is well. The console and the window both call
        /// it, so as to refuse the same things with the same words.
        /// </summary>
        /// <returns>
        /// The first rule broken, as a sentence for the user in the interface language; null when none is.
        /// </returns>
        internal string Validate()
        {
            if (TargetBackground.HasValue)
            {
                string bg = TargetBackgroundError(TargetBackground.Value);
                if (bg != null) return bg;
            }
            string notANumber = FiniteError("Dmax", DmaxOverride)
                ?? FiniteError(Tr.Pick("the viewing distance", "distanta de privire"), ViewingDistanceM)
                ?? (CandidateWidthsCm ?? Array.Empty<double>()).Select(w => FiniteError(Tr.Pick("the print width", "latimea de print"), w)).FirstOrDefault(e => e != null)
                ?? PositiveError(Tr.Pick("the aspect ratio", "raportul de aspect"), TargetAspect)
                ?? FiniteError(Tr.Pick("the passepartout", "passepartout-ul"), PassepartoutCm)
                ?? FiniteError(Tr.Pick("the frame", "rama"), FrameCm);
            if (notANumber != null) return notANumber;
            double minPassepartout = Thresholds.PassepartoutMinFrameRatio * FrameCm;
            if (PassepartoutCm > 0 && PassepartoutCm < minPassepartout)
                return Tr.Pick($"the passepartout ({Fmt.F1(PassepartoutCm)} cm) is under twice the width of the frame ({Fmt.F1(FrameCm)} cm): "
                               + $"under {Fmt.F0(Thresholds.PassepartoutMinFrameRatio)} x the frame, the frame crowds the image. "
                               + $"Widen the passepartout to at least {Fmt.F1(minPassepartout)} cm, take a narrower frame, or set 0 for no passepartout.",
                               $"passepartout-ul ({Fmt.F1(PassepartoutCm)} cm) e sub dublul latimii ramei ({Fmt.F1(FrameCm)} cm): "
                               + $"sub {Fmt.F0(Thresholds.PassepartoutMinFrameRatio)} x rama, rama strange imaginea. "
                               + $"Mareste passepartout-ul la cel putin {Fmt.F1(minPassepartout)} cm, ia o rama mai ingusta, sau pune 0 pentru fara passepartout.");
            return null;
        }

        /// <summary>The Dmax of a print medium (ADR 0028), the one place that holds it.</summary>
        /// <param name="m">
        /// The medium; a value outside the enum throws ArgumentOutOfRangeException.
        /// </param>
        /// <returns>The medium's Dmax: 2.4, 1.5 or 2.2.</returns>
        internal static double DmaxOf(PrintMedium m) => m switch
        {
            PrintMedium.Baryta => 2.4,
            PrintMedium.Canvas => 1.5,
            PrintMedium.Acrylic => 2.2,
            _ => throw new ArgumentOutOfRangeException(nameof(m)),
        };

        /// <summary>
        /// The names of all the media, in the enum's order: the list the console writes its
        /// --help and its unknown-medium error from, and the window fills its combo with.
        /// </summary>
        internal static IReadOnlyList<string> MediumNames { get; }
            = ((PrintMedium[])Enum.GetValues(typeof(PrintMedium))).Select(NameOf).ToArray();

        /// <summary>
        /// The medium named as on the command line and in the window: baryta, canvas, acrilic.
        /// </summary>
        /// <param name="name">
        /// The name, in any case; null or an unknown name throws ArgumentException.
        /// </param>
        /// <returns>The medium of that name.</returns>
        internal static PrintMedium MediumFromName(string name) => name?.ToLowerInvariant() switch
        {
            "baryta" => PrintMedium.Baryta,
            "canvas" => PrintMedium.Canvas,
            "acrilic" => PrintMedium.Acrylic,
            _ => throw new ArgumentException(Tr.Pick("unknown medium: ", "mediu necunoscut: ") + name),
        };

        /// <summary>
        /// The name of a medium on the command line and in the window, the inverse of
        /// <see cref="MediumFromName"/>.
        /// </summary>
        /// <param name="m">
        /// The medium; a value outside the enum throws ArgumentOutOfRangeException.
        /// </param>
        /// <returns>"baryta", "canvas" or "acrilic".</returns>
        internal static string NameOf(PrintMedium m) => m switch
        {
            PrintMedium.Baryta => "baryta",
            PrintMedium.Canvas => "canvas",
            PrintMedium.Acrylic => "acrilic",
            _ => throw new ArgumentOutOfRangeException(nameof(m)),
        };
    }
}
