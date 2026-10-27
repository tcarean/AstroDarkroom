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

// AnalysisRun.cs - the whole chain: load -> measure -> recommend -> format.
//
// The console and the window BOTH call from here and do nothing more than show the result. The
// order of the analyzers matters only where one result feeds another: the tones before the
// simulation, the stars and the crop before the size table, the table before the
// recommendations (the plateau is judged at the reference row's dpi).
//
// The progress is an IProgress<string> with the stage's name: the window shows it in an
// overlay, the console ignores it. It is not a percentage - the stages have durations too
// different (the JPEG is half of the whole) for a percentage to say anything.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AstroPhotoAnalyzer
{
    /// <summary>
    /// An image the tool refuses to measure - not a reading error, but one of CONTENT: it is
    /// linear, not stretched. The console reports it as EROARE and exits with 1.
    /// </summary>
    internal sealed class RefusedImageException : Exception
    {
        /// <summary>A refusal with the message the user sees.</summary>
        /// <param name="message">The reason and what to do, in the interface language.</param>
        internal RefusedImageException(string message) : base(message) { }
    }

    /// <summary>
    /// Everything one analysis produced: the inputs, every result, the recommendations and the
    /// report body. The console prints its body; the window fills its tabs from it and keeps it
    /// for the Editare Curbe tab.
    /// </summary>
    internal sealed class AnalysisResult
    {
        internal string Path;

        /// <summary>
        /// The image's full path at the moment it was read; null for a raster from memory. Path
        /// stays as it was given (the report prints it that way); saving a curve needs the full
        /// one, which no longer changes if the working directory changes (R-145).
        /// </summary>
        internal string FullPath;
        internal Parameters Parameters;
        internal Raster Raster;

        internal FileFactsResult File;
        internal BitDepthResult BitDepth;
        internal TonalResult Tonal;
        internal SimulationResult Simulation;
        internal NoiseResult Noise;
        internal BackgroundResult Background;
        internal CropResult Crop;
        internal StarResult Stars;
        internal PrintSizeResult Size;
        internal QuantizationResult Quantization;
        internal JpegResult Jpeg;

        internal IReadOnlyList<Recommendation> Recommendations;
        internal Verdict Overall => Recommendations.Count == 0 ? Verdict.Ok : Recommendations.Max(r => r.Verdict);

        /// <summary>
        /// The report WITHOUT the banner - the console has already written it; the saved file
        /// gets it through WriteFull.
        /// </summary>
        internal string ReportBody;
    }

    /// <summary>The analysis chain both applications call (ADR 0007).</summary>
    internal static class AnalysisRun
    {
        /// <summary>
        /// Reads the image, takes the WCS of <see cref="Parameters.WcsPath"/> when one is given,
        /// and runs the chain on it
        /// (<see cref="Execute(Raster, string, Parameters, IProgress{string})"/>). A file that
        /// cannot be read, a borrowed WCS that does not fit (<see cref="BorrowWcs"/>) and a linear
        /// image (<see cref="RefusedImageException"/>) throw.
        /// </summary>
        /// <param name="path">
        /// The image's path, as the user gave it; the report prints it so.
        /// </param>
        /// <param name="p">The parameters.</param>
        /// <param name="progress">
        /// Told the name of each stage as it starts, in the interface language ("reading" first,
        /// "citire" in Romanian); null for none.
        /// </param>
        /// <returns>The analysis, with <see cref="AnalysisResult.FullPath"/> set.</returns>
        internal static AnalysisResult Execute(string path, Parameters p, IProgress<string> progress = null)
        {
            progress?.Report(Tr.Pick("reading", "citire"));
            Raster r = ImageLoader.Load(path);
            if (p.WcsPath != null) r.Source = r.Source with { Wcs = BorrowWcs(p.WcsPath, r) };
            AnalysisResult res = Execute(r, path, p, progress);
            res.FullPath = System.IO.Path.GetFullPath(path);
            return res;
        }

        /// <summary>
        /// The WCS of another FITS, for a raster of the same geometry (R-027). A PNG or TIFF
        /// exported from Siril is in the displayed orientation, that is the FITS flipped whatever
        /// its ROWORDER says (ADR 0049), so the lent WCS is always the flipped one.
        /// Throws, with a message in the interface language that starts with "--wcs:", when the file is missing,
        /// cannot be read, has another size or holds no TAN WCS.
        /// </summary>
        /// <param name="wcsPath">The FITS whose header holds the WCS.</param>
        /// <param name="r">The analysed raster, whose width and height the FITS must have.</param>
        /// <returns>The WCS, never null.</returns>
        internal static Wcs BorrowWcs(string wcsPath, Raster r)
        {
            if (!File.Exists(wcsPath)) throw new Exception("--wcs: " + Tr.Pick("the file does not exist: ", "fisierul nu exista: ") + wcsPath);
            IReadOnlyDictionary<string, string> hdr; int w, h;
            try { (hdr, w, h) = FitsReader.ReadImageHeader(wcsPath); }
            catch (Exception e) { throw new Exception("--wcs: " + e.Message); }
            if (w != r.W || h != r.H)
                throw new Exception(Tr.Pick($"--wcs: the FITS is {Fmt.Dim(w, h)} px, the image {Fmt.Dim(r.W, r.H)}: a WCS is lent only between images of the same size.",
                                            $"--wcs: FITS-ul are {Fmt.Dim(w, h)} px, imaginea {Fmt.Dim(r.W, r.H)}: WCS-ul se imprumuta doar intre imagini de aceeasi marime."));
            Wcs wcs = Wcs.TryFrom(hdr, w, h, rowsFlipped: true);
            if (wcs == null)
                throw new Exception(Tr.Pick("--wcs: the FITS has no WCS (CTYPE RA---TAN / DEC--TAN with CRPIX, CRVAL and CD, PC + CDELT or CDELT + CROTA2): ",
                                            "--wcs: FITS-ul n-are WCS (CTYPE RA---TAN / DEC--TAN cu CRPIX, CRVAL si CD, PC + CDELT sau CDELT + CROTA2): ") + wcsPath);
            return wcs;
        }

        /// <summary>
        /// The chain on a raster already read: refuses a linear image, then runs the analyzers in
        /// order, builds the recommendations and writes the report body.
        /// </summary>
        /// <param name="r">
        /// The image, 0..1, with its <see cref="Raster.Source"/> when it has one.
        /// </param>
        /// <param name="path">
        /// The path the report prints; a raster from memory may give any text.
        /// </param>
        /// <param name="p">The parameters.</param>
        /// <param name="progress">
        /// Told the name of each stage as it starts, in the interface language: "file", "tones",
        /// "dust", "noise", "background", "stars", "quantization", "JPEG", "report" (Romanian
        /// "fisier", "tonuri", "praf", "zgomot", "fundal", "stele", "cuantizare", "JPEG",
        /// "raport"); null for none.
        /// </param>
        /// <returns>The analysis; its <see cref="AnalysisResult.FullPath"/> stays null.</returns>
        internal static AnalysisResult Execute(Raster r, string path, Parameters p, IProgress<string> progress = null)
        {
            if (RasterOps.LooksLinear(r, out double median))
                throw new RefusedImageException(Tr.Pick(
                    $"the image looks LINEAR (median {Fmt.F4(median)}, under {Fmt.F2(Thresholds.LinearMedianMax)}): the tool measures only finished, stretched images. "
                    + "Stretch it in Siril and analyse the result.",
                    $"imaginea pare LINIARA (mediana {Fmt.F4(median)}, sub {Fmt.F2(Thresholds.LinearMedianMax)}): unealta masoara doar imagini finite, stretch-uite. "
                    + "Stretch-uieste in Siril si analizeaza rezultatul."));

            var res = new AnalysisResult { Path = path, Parameters = p, Raster = r };

            progress?.Report(Tr.Pick("file", "fisier"));
            res.File = FileFacts.Of(r);
            res.BitDepth = BitDepthAnalyzer.Analyze(r);
            res.Crop = CropAnalyzer.Analyze(r.W, r.H, p.TargetAspect);

            progress?.Report(Tr.Pick("tones", "tonuri"));
            res.Tonal = TonalAnalyzer.Analyze(r, p);
            progress?.Report(Tr.Pick("dust", "praf"));
            res.Simulation = PrintStretchSimulator.Simulate(r, res.Tonal, p);

            progress?.Report(Tr.Pick("noise", "zgomot"));
            res.Noise = NoiseAnalyzer.Analyze(r, p);

            progress?.Report(Tr.Pick("background", "fundal"));
            res.Background = BackgroundAnalyzer.Analyze(r, res.Simulation.Map);

            progress?.Report(Tr.Pick("stars", "stele"));
            res.Stars = StarAnalyzer.Analyze(r);
            res.Size = PrintSizeAnalyzer.Analyze(res.Crop.W, res.Crop.H, res.Stars.FwhmMedian, p);

            progress?.Report(Tr.Pick("quantization", "cuantizare"));
            res.Quantization = QuantizationAnalyzer.Analyze(r);

            progress?.Report("JPEG");
            res.Jpeg = JpegAnalyzer.Analyze(r);

            progress?.Report(Tr.Pick("report", "raport"));
            res.Recommendations = Recommendations.Build(res);
            var sw = new StringWriter();
            ReportWriter.WriteBody(res, sw);
            res.ReportBody = sw.ToString();
            return res;
        }
    }
}
