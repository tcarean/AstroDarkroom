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

// CurveRun.cs - the core's second run, on request: the suggested curves for an image already
// analysed, and the saving of one (R-140 to R-145, ADR 0032).
//
// It is not in AnalysisRun because it is not a diagnosis and it costs: it runs only when the
// user presses "Calculeaza" in the Curbe tab, on the analysis's result (the raster, the patch
// map, the noise, the parameters). The console does not call it. The purpose (print or screen)
// changes no curve: it adds the paper checks and the gamut figure to the evidence.

using System;
using System.Collections.Generic;
using System.IO;

namespace AstroPhotoAnalyzer
{
    /// <summary>
    /// What the Curbe tab shows for one computation: the anchors, the class names, the original's
    /// preview, the suggestions with their evidence for the purpose, or why there are none.
    /// </summary>
    internal sealed record CurveSet
    {
        internal CurvePurpose Purpose { get; init; }
        internal CurveAnchors Anchors { get; init; }

        /// <summary>
        /// On which path the anchors' classes were made, and their names (DustAnalyzer.ClassNames).
        /// </summary>
        internal DustMethod Method { get; init; }
        internal IReadOnlyList<string> Labels { get; init; }

        /// <summary>
        /// Why nothing was suggested, for the user; null when there are suggestions.
        /// </summary>
        internal string Problem { get; init; }

        /// <summary>
        /// Blanda, Medie, Puternica and, when it exists, Maxima; empty with Problem.
        /// </summary>
        internal IReadOnlyList<CurveSuggestion> Suggestions { get; init; } = Array.Empty<CurveSuggestion>();

        /// <summary>The reduced image, as it is: the first of the gallery.</summary>
        internal Raster OriginalPreview { get; init; }

        /// <summary>Where the anchors come from, for the user.</summary>
        internal string SourceSentence { get; init; }
    }

    /// <summary>
    /// Computes the suggested curves of an analysed image, on request (ADR 0032).
    /// </summary>
    internal static class CurveRun
    {
        /// <summary>
        /// The integer step that brings the image into <paramref name="maxWidth"/> x
        /// <paramref name="maxHeight"/>.
        /// </summary>
        /// <param name="r">The image.</param>
        /// <param name="maxWidth">The largest width, in pixels; under 1 counts as 1.</param>
        /// <param name="maxHeight">The largest height, in pixels; under 1 counts as 1.</param>
        /// <returns>The smallest stride, at least 1, whose subsample fits both.</returns>
        internal static int PreviewStride(Raster r, int maxWidth, int maxHeight)
            => Math.Max(1, (int)Math.Ceiling(Math.Max(r.W / (double)Math.Max(1, maxWidth), r.H / (double)Math.Max(1, maxHeight))));

        /// <summary>
        /// The suggested curves for the analysed image <paramref name="a"/>, with the evidence
        /// for <paramref name="purpose"/> and with previews that fit in the given rectangle.
        /// The anchors and the evidence are measured on the tonal subsample
        /// (<see cref="Thresholds.TonalStride"/>), on the classes of the simulation's map.
        /// </summary>
        /// <param name="a">The analysis, with its raster, simulation, noise and parameters.</param>
        /// <param name="purpose">
        /// Print adds the paper checks and the gamut figure (or its note) to each evidence.
        /// </param>
        /// <param name="maxWidth">The previews' largest width, in screen pixels.</param>
        /// <param name="maxHeight">The previews' largest height, in screen pixels.</param>
        /// <param name="progress">
        /// Told each stage as it starts: "ancore", "gamut" (when the table is built), then each
        /// suggestion's name; null for none.
        /// </param>
        /// <returns>
        /// The set: with the suggestions, or with <see cref="CurveSet.Problem"/> set and none.
        /// </returns>
        internal static CurveSet Execute(AnalysisResult a, CurvePurpose purpose, int maxWidth, int maxHeight, IProgress<string> progress = null)
        {
            Raster r = a.Raster;
            SimulationResult sim = a.Simulation;
            DustMap map = sim.Map;
            double black = sim.PaperBlackLstar;

            progress?.Report("ancore");
            Raster sub = RasterOps.Subsample(r, Thresholds.TonalStride);
            IReadOnlyList<string> labels = map.ClassNames;
            double[] before = CurveAdvisor.ClassLevels(sub, map, black);
            CurveAnchors anchors = CurveAdvisor.Anchors(before);
            Raster original = RasterOps.Subsample(r, PreviewStride(r, maxWidth, maxHeight));
            var set = new CurveSet
            {
                Purpose = purpose,
                Anchors = anchors,
                Method = map.Method,
                Labels = labels,
                OriginalPreview = original,
                SourceSentence = CurveTexts.AnchorSource(map, sim.MethodNote),
            };

            string problem = CurveAdvisor.Problem(anchors, labels);
            if (problem != null) return set with { Problem = problem };
            IReadOnlyList<(string Name, ToneCurve Curve)> curves = CurveAdvisor.Curves(anchors, labels);
            if (curves.Count == 0) return set with { Problem = CurveTexts.NoMonotoneCurve };

            // The gamut: only for print and only with the profile the Dmax came from.
            GamutTable gamut = null; string gamutNote = null; GamutFigure gamutBefore = null;
            if (purpose == CurvePurpose.Print)
            {
                string profile = a.Parameters.DmaxProfile != null && a.Parameters.DmaxOverride.HasValue ? a.Parameters.DmaxProfilePath : null;
                if (profile == null) gamutNote = CurveTexts.GamutNeedsProfile;
                else
                {
                    progress?.Report("gamut");
                    gamut = GamutAnalyzer.Build(profile);
                    if (gamut.Problem != null) { gamutNote = CurveTexts.GamutUnreadable(gamut.Problem); gamut = null; }
                    else gamutBefore = GamutAnalyzer.Measure(gamut, sub, map);
                }
            }

            double clippedBefore = CurveAdvisor.PctClipped(sub);
            var suggestions = new List<CurveSuggestion>();
            foreach (var (name, curve) in curves)
            {
                progress?.Report(name);
                Raster curvedSub = curve.Apply(sub);
                CurveEvidence e = CurveAdvisor.Measure(curve, sub, curvedSub, map, before, clippedBefore, anchors.Sky, a.Noise, black);
                if (purpose == CurvePurpose.Print)
                    e = e with
                    {
                        PaperChecks = CurveTexts.PaperChecks(sim, a.Noise, e),
                        GamutBefore = gamutBefore,
                        Gamut = gamut == null ? null : GamutAnalyzer.Measure(gamut, curvedSub, map),
                        GamutNote = gamutNote,
                    };
                suggestions.Add(new CurveSuggestion { Name = name, Curve = curve, Preview = curve.Apply(original), Evidence = e });
            }
            return set with { Suggestions = suggestions };
        }

        /// <summary>
        /// The screen set of a set computed for print: the same curves, the same previews, the
        /// evidence without the paper checks and without the gamut. The window computes once, for
        /// print, and changes the purpose without computing anything more.
        /// </summary>
        /// <param name="print">A set computed for print; it is not changed.</param>
        /// <returns>A new set, its purpose Screen; its problem, if any, kept.</returns>
        internal static CurveSet ForScreen(CurveSet print)
        {
            var suggestions = new List<CurveSuggestion>();
            foreach (CurveSuggestion s in print.Suggestions)
                suggestions.Add(s with
                {
                    Evidence = s.Evidence with { PaperChecks = Array.Empty<Recommendation>(), GamutBefore = null, Gamut = null, GamutNote = null },
                });
            return print with { Purpose = CurvePurpose.Screen, Suggestions = suggestions };
        }
    }

    /// <summary>Saves a suggested curve applied to the whole image as a new FITS (R-145).</summary>
    internal static class CurveExport
    {
        /// <summary>
        /// Writes to <paramref name="targetPath"/> a new FITS with the suggestion's curve applied
        /// to the whole image (R-145).
        ///
        /// The input image is never opened for writing. A target that is the input itself is
        /// refused twice: first by its path, with a plain message; then, for any other spelling
        /// of the same path (\\?\, a mapped drive against \\server\share, a link), by the
        /// operating system: the image is held open for reading, WITHOUT sharing for writing and
        /// for deletion, while the save lasts, so moving the temporary file over it fails.
        /// Comparing the paths as text cannot see every spelling.
        ///
        /// The image must still be where it was at the analysis: without it a FITS would lose
        /// its header silently, so the save is refused. Every refusal and write error throws,
        /// with the Romanian message of <see cref="CurveTexts"/> where the refusal is the tool's.
        /// </summary>
        /// <param name="a">
        /// The analysis whose raster is saved; its FullPath names the input, and null there (a
        /// raster from memory) writes a fresh FITS with no check.
        /// </param>
        /// <param name="s">The suggestion whose curve is applied and named in HISTORY.</param>
        /// <param name="targetPath">
        /// The file to write, relative or full; replaced when it exists.
        /// </param>
        internal static void Save(AnalysisResult a, CurveSuggestion s, string targetPath)
        {
            Raster r = a.Raster;
            string input = a.FullPath;
            string target = Path.GetFullPath(targetPath);
            IReadOnlyList<string> history = CurveTexts.History(s.Name, s.Curve);
            if (input == null)
            {
                // A raster from memory (the tests): it has no file to protect and no header to
                // keep.
                FitsWriter.Save(r, s.Curve, null, history, target);
                return;
            }
            if (!File.Exists(input)) throw new Exception(CurveTexts.InputGone(input));
            if (string.Equals(input, target, StringComparison.OrdinalIgnoreCase)) throw new Exception(CurveTexts.SaveOverInput);

            using var hold = new FileStream(input, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
            try
            {
                FitsWriter.Save(r, s.Curve, r.Source?.Format == "FITS" ? input : null, history, target);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // The move over an existing target of the same size as the image failed while the
                // image was held: almost certainly the target is the image itself, by another path.
                if (File.Exists(target) && SameLength(target, hold.Length)) throw new Exception(CurveTexts.SaveOverInput + " (" + e.Message + ")");
                throw;
            }
        }

        /// <summary>Whether a file has a given length, without throwing on an I/O error.</summary>
        /// <param name="path">The file.</param>
        /// <param name="length">The length to compare with, in bytes.</param>
        /// <returns>
        /// <c>true</c> when the file's length is <paramref name="length"/>; <c>false</c> when it
        /// differs or an <see cref="IOException"/> stops the reading.
        /// </returns>
        static bool SameLength(string path, long length)
        {
            try { return new FileInfo(path).Length == length; }
            catch (IOException) { return false; }
        }
    }
}
