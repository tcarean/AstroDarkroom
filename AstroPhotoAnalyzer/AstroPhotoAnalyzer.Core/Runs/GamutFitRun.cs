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

// GamutFitRun.cs - the fourth run, after the analysis: the paper fit of the analysed image in
// three variants, for the Culori pentru hartie tab (R-175 to R-178, ADR 0083), and the save of
// one variant as a new FITS or 16-bit TIFF. Called by the window alone, never by the console,
// like CurveRun and ProofRun. The figures of the evidence are measured on the tonal subsample
// (Thresholds.TonalStride), as the curves' are; the previews and their masks on the whole image,
// or on its reduction when it is over Thresholds.WholeImageMaxPixels (R-188, ADR 0118).

using System;
using System.Collections.Generic;
using NLog;

namespace AstroPhotoAnalyzer
{
    /// <summary>The figures of one variant's evidence (R-176), from the tonal subsample.</summary>
    internal sealed record GamutFitEvidence
    {
        internal double PctOutsideBefore { get; init; }
        internal double PctOutsideAfter { get; init; }
        internal double PctTouched { get; init; }
        internal double PctUnreachable { get; init; }
        internal double ChromaLostMedian { get; init; }
        internal double ChromaLostP99 { get; init; }
        internal double PctOutsideUnderLowSeparation { get; init; }

        /// <summary>The figures of an outcome measured with facts.</summary>
        /// <param name="o">The outcome.</param>
        /// <returns>The evidence.</returns>
        internal static GamutFitEvidence Of(GamutFitOutcome o) => new()
        {
            PctOutsideBefore = o.PctOutsideBefore,
            PctOutsideAfter = o.PctOutsideAfter,
            PctTouched = o.PctTouched,
            PctUnreachable = o.PctUnreachable,
            ChromaLostMedian = o.ChromaLostMedian,
            ChromaLostP99 = o.ChromaLostP99,
            PctOutsideUnderLowSeparation = o.PctOutsideUnderLowSeparation,
        };
    }

    /// <summary>One variant of the fit: its name and knee, its preview with the mask of what is still outside, its evidence.</summary>
    internal sealed record GamutFitVariant
    {
        internal string Name { get; init; }
        internal double Knee { get; init; }

        /// <summary>The fitted image, at the set's step; dropped by the window once drawn.</summary>
        internal Raster Preview { get; init; }

        /// <summary>Per preview pixel, whether the paper still cannot hold it.</summary>
        internal bool[] Outside { get; init; }

        internal GamutFitEvidence Evidence { get; init; }
    }

    /// <summary>What the Culori pentru hartie tab shows for one computation (R-178).</summary>
    internal sealed record GamutFitSet
    {
        /// <summary>Why there is nothing to show, for the user; null with the variants.</summary>
        internal string Problem { get; init; }

        /// <summary>The profile's name, for the saved file and the log.</summary>
        internal string ProfileDescription { get; init; }

        /// <summary>The table, kept for the save of the whole image.</summary>
        internal GamutFitTable Table { get; init; }

        /// <summary>The image as it is, at the set's step (the analysed raster itself at 1): the other side of the comparison.</summary>
        internal Raster OriginalPreview { get; init; }

        /// <summary>Per preview pixel, whether the paper cannot hold the original's colour (the warning of R-161).</summary>
        internal bool[] OriginalOutside { get; init; }

        internal IReadOnlyList<GamutFitVariant> Variants { get; init; } = Array.Empty<GamutFitVariant>();

        /// <summary>The recipe for a hand-made mask, the same under every variant.</summary>
        internal GamutFitRecipe Recipe { get; init; }

        /// <summary>
        /// The step the previews were taken at: 1 for the whole image, more for a reduced copy
        /// of an image over Thresholds.WholeImageMaxPixels (R-188, ADR 0118).
        /// </summary>
        internal int Stride { get; init; }
    }

    /// <summary>Computes the paper fit of an analysed image, after the analysis (ADR 0083, ADR 0051).</summary>
    internal static class GamutFitRun
    {
        /// <summary>
        /// The diagnostic log: the run's start and done lines with the time of each stage, a
        /// refusal as its done line, each variant's figures at Debug (R-171, ADR 0069).
        /// </summary>
        static readonly Logger Log = LogManager.GetLogger("GamutFitRun");

        /// <summary>The knees of the three variants, in the gallery's order.</summary>
        internal static readonly double[] Knees = { Thresholds.GamutFitKneeExact, Thresholds.GamutFitKneeSoft, Thresholds.GamutFitKneeSofter };

        /// <summary>
        /// The fit of the analysed image <paramref name="a"/> through the profile its Dmax came
        /// from, in the three variants, with previews of the whole image, or of its reduction
        /// when it is over Thresholds.WholeImageMaxPixels (<see cref="RasterOps.WholeImageStride"/>,
        /// R-188): whatever the area they are shown in.
        /// </summary>
        /// <param name="a">The analysis, with its raster and parameters.</param>
        /// <param name="progress">Told each stage as it starts; null for none.</param>
        /// <param name="maxPixels">
        /// The most pixels of an image taken whole (<see cref="RasterOps.WholeImageStride"/>):
        /// the window leaves the default; the tests lower it to reach a reduced copy.
        /// </param>
        /// <returns>The set; with Problem set and nothing else when there is no profile or it cannot be used.</returns>
        internal static GamutFitSet Execute(AnalysisResult a, IProgress<string> progress = null, int maxPixels = Thresholds.WholeImageMaxPixels)
        {
            Parameters prm = a.Parameters;
            int stride = RasterOps.WholeImageStride(a.Raster.W, a.Raster.H, maxPixels);
            string path = prm.DmaxProfile != null && prm.DmaxOverride.HasValue ? prm.DmaxProfilePath : null;
            using RunClock clock = RunClock.Start(Log, "gamutfit", "path=" + LogText.Str(a.Path) + " profile=" + LogText.Str(path)
                + " stride=" + LogText.Int(stride));
            if (path == null)
            {
                clock.Done("refused: " + ProofTexts.NoProfile);
                return new GamutFitSet { Problem = ProofTexts.NoProfile };
            }
            progress?.Report(Tr.Pick("profile", "profil"));
            GamutTable gamut = GamutAnalyzer.Build(path);
            clock.Lap("profile");
            if (gamut.Problem != null)
            {
                Log.Warn(gamut.Cause, "the gamut table of " + path + " cannot be built: " + LogText.Str(gamut.Problem));
                clock.Done("refused: " + LogText.Str(gamut.Problem));
                return new GamutFitSet { Problem = gamut.Problem };
            }
            string description = IccPaperBlack.Read(path).Description;
            progress?.Report(Tr.Pick("table", "tabela"));
            // The fit aims a little inside the warning (R-187, ADR 0109): the same table of round
            // trips judged at the target, with a cache of its own.
            GamutTable aim = gamut.WithLimit(Thresholds.GamutFitTargetDeltaE);
            GamutFitTable table = GamutFitTable.Build(gamut.ExactLab, limit: Thresholds.GamutFitTargetDeltaE);
            // The Soft proof tab's own judge, asked at the target, decides which pixels are moved
            // and where they land (R-175): a pixel is judged on its own samples, as that tab
            // judges it.
            table.Judge = (l, a, b) =>
            {
                var (r, g, bl) = ColorMath.SrgbOfLabD50(l, a, b);
                return GamutAnalyzer.Outside(aim, (float)r, (float)g, (float)bl);
            };
            table.JudgeOfPixel = (r, g, bl) => GamutAnalyzer.Outside(aim, r, g, bl);
            // The same judge at the warning's own limit counts what is outside, before and after:
            // the shares and the masks are the Soft proof tab's.
            table.Warning = (l, a, b) =>
            {
                var (r, g, bl) = ColorMath.SrgbOfLabD50(l, a, b);
                return GamutAnalyzer.Outside(gamut, (float)r, (float)g, (float)bl);
            };
            table.WarningOfPixel = (r, g, bl) => GamutAnalyzer.Outside(gamut, r, g, bl);
            clock.Lap("table");
            if (table.Problem != null)
            {
                clock.Done("refused: " + LogText.Str(table.Problem));
                return new GamutFitSet { Problem = table.Problem };
            }

            Raster r = a.Raster;
            Raster original = RasterOps.Subsample(r, stride);
            Raster sub = RasterOps.Subsample(r, Thresholds.TonalStride);
            var variants = new List<GamutFitVariant>();
            bool[] originalOutside = null;
            foreach (double knee in Knees)
            {
                string name = GamutFitTexts.Name(knee);
                progress?.Report(name);
                GamutFitOutcome preview = GamutFit.Apply(original, table, knee, keepFacts: true);
                // The mask of the original is the same under every knee: the first variant's.
                originalOutside ??= preview.OutsideBefore;
                GamutFitOutcome facts = GamutFit.Apply(sub, table, knee, keepFacts: true);
                var v = new GamutFitVariant { Name = name, Knee = knee, Preview = preview.Image, Outside = preview.OutsideAfter, Evidence = GamutFitEvidence.Of(facts) };
                variants.Add(v);
                Log.Debug("variant " + name + " knee=" + LogText.Num(knee) + " before=" + LogText.Num(facts.PctOutsideBefore) + " % after=" + LogText.Num(facts.PctOutsideAfter)
                    + " % touched=" + LogText.Num(facts.PctTouched) + " % unreachable=" + LogText.Num(facts.PctUnreachable) + " % lost p50=" + LogText.Num(facts.ChromaLostMedian)
                    + " p99=" + LogText.Num(facts.ChromaLostP99) + " dark=" + LogText.Num(facts.PctOutsideUnderLowSeparation) + " %");
                clock.Lap(name);
            }
            progress?.Report(Tr.Pick("recipe", "reteta"));
            GamutFitRecipe recipe = GamutFit.Recipe(sub, table);
            clock.Lap("recipe");
            clock.Done("outside=" + LogText.Num(variants[0].Evidence.PctOutsideBefore) + " % target=" + LogText.Num(Thresholds.GamutFitTargetDeltaE) + " recipe=L*<" + LogText.Num(recipe.LstarCeiling) + " x" + LogText.Num(recipe.Factor)
                + " left=" + LogText.Num(recipe.PctLeft) + " % description=" + LogText.Str(description));
            return new GamutFitSet
            {
                ProfileDescription = description,
                Table = table,
                OriginalPreview = original,
                OriginalOutside = originalOutside,
                Variants = variants,
                Recipe = recipe,
                Stride = stride,
            };
        }
    }

    /// <summary>Saves a variant applied to the whole image as a new FITS or 16-bit TIFF (R-177).</summary>
    internal static class GamutFitExport
    {
        /// <summary>The diagnostic log: every file written, with its size and kind (R-171, ADR 0069).</summary>
        static readonly Logger Log = LogManager.GetLogger("GamutFitExport");

        /// <summary>
        /// Writes to <paramref name="targetPath"/> the whole image fitted as <paramref name="v"/>:
        /// a TIFF when the target's extension is .tif or .tiff, with the input's ICC profile (the
        /// standard sRGB when it has none); a FITS otherwise, as R-145. Under the guards of
        /// <see cref="CurveExport.Guarded"/>: the input is never opened for writing.
        /// </summary>
        /// <param name="a">The analysis whose raster is fitted and saved.</param>
        /// <param name="set">The set the variant belongs to, for its table and the profile's name.</param>
        /// <param name="v">The variant to apply.</param>
        /// <param name="targetPath">The file to write, relative or full; replaced when it exists.</param>
        internal static void Save(AnalysisResult a, GamutFitSet set, GamutFitVariant v, string targetPath)
        {
            CurveExport.Guarded(a, targetPath, Log, (target, sourceFits) =>
            {
                Raster fitted = GamutFit.Apply(a.Raster, set.Table, v.Knee, keepFacts: false).Image;
                if (GamutFitTexts.IsTiff(target))
                {
                    byte[] icc = a.Raster.Source?.Icc ?? IccProfiles.Srgb;
                    TiffWriter.Save(fitted, icc, GamutFitTexts.Description(v, set.ProfileDescription), target);
                    Log.Info("wrote " + target + " (" + LogText.FileMiB(target) + ") tiff 16-bit variant=" + v.Name + " icc=" + (a.Raster.Source?.Icc != null ? "input" : "sRGB"));
                }
                else
                {
                    var identity = new ToneCurve(new[] { new CurvePoint(0, 0, null), new CurvePoint(1, 1, null) });
                    FitsWriter.Save(fitted, identity, sourceFits, GamutFitTexts.History(v, set.ProfileDescription), target);
                    Log.Info("wrote " + target + " (" + LogText.FileMiB(target) + ") fits variant=" + v.Name + " header from=" + (sourceFits == null ? "none" : LogText.Str(sourceFits)));
                }
            });
        }
    }
}
