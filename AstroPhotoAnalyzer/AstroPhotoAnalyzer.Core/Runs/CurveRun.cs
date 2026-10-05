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

// CurveRun.cs - a doua rulare a nucleului, la cerere: curbele sugerate pentru o imagine
// deja analizata, si salvarea uneia (R-140 pana la R-145, ADR 0031).
//
// Nu e in AnalysisRun fiindca nu e diagnostic si costa: se face doar cand omul apasa
// "Calculeaza" in tabul Curbe, pe rezultatul analizei (rasterul, harta dalelor, zgomotul,
// parametrii). Consola nu o apeleaza. Scopul (print sau ecran) nu schimba nicio curba:
// adauga dovezii verificarile de hartie si cifra de gamut.

using System;
using System.Collections.Generic;
using System.IO;

namespace AstroPhotoAnalyzer
{
    internal sealed record CurveSet
    {
        internal CurvePurpose Purpose { get; init; }
        internal CurveAnchors Anchors { get; init; }
        internal CurveAnchorSource Source { get; init; }

        /// <summary>De ce nu s-a sugerat nimic, pentru om; null cand exista sugestii.</summary>
        internal string Problem { get; init; }

        /// <summary>Blanda, Medie, Puternica si, cand exista, Maxima; gol cu Problem.</summary>
        internal IReadOnlyList<CurveSuggestion> Suggestions { get; init; } = Array.Empty<CurveSuggestion>();

        /// <summary>Imaginea redusa, asa cum e: prima din galerie.</summary>
        internal Raster OriginalPreview { get; init; }

        /// <summary>De unde vin ancorele, pentru om.</summary>
        internal string SourceSentence { get; init; }
    }

    internal static class CurveRun
    {
        /// <summary>Pasul intreg care aduce imaginea in <paramref name="maxWidth"/> x <paramref name="maxHeight"/>.</summary>
        internal static int PreviewStride(Raster r, int maxWidth, int maxHeight)
            => Math.Max(1, (int)Math.Ceiling(Math.Max(r.W / (double)Math.Max(1, maxWidth), r.H / (double)Math.Max(1, maxHeight))));

        /// <summary>
        /// Curbele sugerate pentru imaginea analizata <paramref name="a"/>, cu dovada pentru
        /// <paramref name="purpose"/> si cu previzualizari care incap in dreptunghiul dat.
        /// </summary>
        internal static CurveSet Execute(AnalysisResult a, CurvePurpose purpose, int maxWidth, int maxHeight, IProgress<string> progress = null)
        {
            Raster r = a.Raster;
            SimulationResult sim = a.Simulation;
            DustMap map = sim.Map;
            double black = sim.PaperBlackLstar;

            progress?.Report("ancore");
            Raster sub = RasterOps.Subsample(r, Thresholds.TonalStride);
            CurveAnchorSource source = CurveAdvisor.SourceOf(map);
            double[] before = CurveAdvisor.ClassLevels(sub, map, source, black);
            CurveAnchors anchors = CurveAdvisor.Anchors(before);
            Raster original = RasterOps.Subsample(r, PreviewStride(r, maxWidth, maxHeight));
            var set = new CurveSet
            {
                Purpose = purpose,
                Anchors = anchors,
                Source = source,
                OriginalPreview = original,
                SourceSentence = CurveTexts.AnchorSource(source, sim.MethodNote),
            };

            string problem = CurveAdvisor.Problem(anchors);
            if (problem != null) return set with { Problem = problem };
            IReadOnlyList<(string Name, ToneCurve Curve)> curves = CurveAdvisor.Curves(anchors);
            if (curves.Count == 0) return set with { Problem = CurveTexts.NoMonotoneCurve };

            // Gamutul: doar pentru print si doar cu profilul din care a venit Dmax-ul.
            GamutTable gamut = null; string gamutNote = null; GamutFigure gamutBefore = null;
            bool byClass = source == CurveAnchorSource.Classes;
            if (purpose == CurvePurpose.Print)
            {
                string profile = a.Parameters.DmaxProfile != null && a.Parameters.DmaxOverride.HasValue ? a.Parameters.DmaxProfilePath : null;
                if (profile == null) gamutNote = CurveTexts.GamutNeedsProfile;
                else
                {
                    progress?.Report("gamut");
                    gamut = GamutAnalyzer.Build(profile);
                    if (gamut.Problem != null) { gamutNote = CurveTexts.GamutUnreadable(gamut.Problem); gamut = null; }
                    else gamutBefore = GamutAnalyzer.Measure(gamut, sub, map, byClass);
                }
            }

            double clippedBefore = CurveAdvisor.PctClipped(sub);
            var suggestions = new List<CurveSuggestion>();
            foreach (var (name, curve) in curves)
            {
                progress?.Report(name);
                Raster curvedSub = curve.Apply(sub);
                CurveEvidence e = CurveAdvisor.Measure(curve, sub, curvedSub, map, source, before, clippedBefore, anchors.Sky, a.Noise, black);
                if (purpose == CurvePurpose.Print)
                    e = e with
                    {
                        PaperChecks = CurveTexts.PaperChecks(sim, a.Noise, e),
                        GamutBefore = gamutBefore,
                        Gamut = gamut == null ? null : GamutAnalyzer.Measure(gamut, curvedSub, map, byClass),
                        GamutNote = gamutNote,
                    };
                suggestions.Add(new CurveSuggestion { Name = name, Curve = curve, Preview = curve.Apply(original), Evidence = e });
            }
            return set with { Suggestions = suggestions };
        }
    }

    internal static class CurveExport
    {
        /// <summary>
        /// Scrie la <paramref name="targetPath"/> un FITS nou cu curba sugestiei aplicata pe
        /// toata imaginea (R-145). Imaginea de intrare nu se deschide niciodata pentru scriere,
        /// iar o tinta care e chiar ea se refuza.
        /// </summary>
        internal static void Save(AnalysisResult a, CurveSuggestion s, string targetPath)
        {
            Raster r = a.Raster;
            string input = r.Source?.Path ?? a.Path;
            bool inputOnDisk = !string.IsNullOrEmpty(input) && File.Exists(input);
            if (inputOnDisk && string.Equals(Path.GetFullPath(input), Path.GetFullPath(targetPath), StringComparison.OrdinalIgnoreCase))
                throw new Exception(CurveTexts.SaveOverInput);
            string sourceFits = inputOnDisk && r.Source?.Format == "FITS" ? input : null;
            FitsWriter.Save(r, s.Curve, sourceFits, CurveTexts.History(s.Name, s.Curve), targetPath);
        }
    }
}
