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
using NLog;

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
        /// The diagnostic log: the run's start and done lines with the time of each stage, and at
        /// Debug the figures of every result and every recommendation (R-171, ADR 0069).
        /// </summary>
        static readonly Logger Log = LogManager.GetLogger("AnalysisRun");

        /// <summary>
        /// Reads the image, takes the WCS of <see cref="Parameters.WcsPath"/> when one is given,
        /// and runs the chain on it
        /// (<see cref="Execute(Raster, string, Parameters, IProgress{string})"/>). A file that
        /// cannot be read, a borrowed WCS that does not fit (<see cref="BorrowWcs"/>) and a linear
        /// image (<see cref="RefusedImageException"/>) throw. The run's start line is written
        /// before the read, so a file that does not load still leaves it, with the stages reached
        /// (R-171).
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
            using RunClock clock = RunClock.Start(Log, "analysis", Inputs(path, p));
            progress?.Report(Tr.Pick("reading", "citire"));
            Raster r = ImageLoader.Load(path);
            clock.Lap("load");
            if (p.WcsPath != null)
            {
                r.Source = r.Source with { Wcs = BorrowWcs(p.WcsPath, r) };
                clock.Lap("wcs");
            }

            AnalysisResult res = Run(r, path, p, progress, clock);
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
            // The reader's exception travels as the inner one, so its stack reaches the
            // diagnostic log; the user's message, which holds its text already, is unchanged.
            catch (Exception e) { throw new Exception("--wcs: " + e.Message, e); }
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
            using RunClock clock = RunClock.Start(Log, "analysis", Inputs(path, p) + " raster=" + LogText.Int(r.W) + "x" + LogText.Int(r.H));
            return Run(r, path, p, progress, clock);
        }

        /// <summary>
        /// The chain proper, as <see cref="Execute(Raster, string, Parameters, IProgress{string})"/>
        /// describes it, each stage timed by <paramref name="clock"/> and each result written to
        /// the diagnostic log at Debug as soon as it is measured, so a stage that throws leaves the
        /// figures before it (R-171).
        /// </summary>
        /// <param name="r">The image, 0..1, with its <see cref="Raster.Source"/> when it has one.</param>
        /// <param name="path">The path the report prints.</param>
        /// <param name="p">The parameters.</param>
        /// <param name="progress">Told the name of each stage as it starts; null for none.</param>
        /// <param name="clock">The run's clock, started by the caller; its done line is written here.</param>
        /// <returns>The analysis; its <see cref="AnalysisResult.FullPath"/> stays null.</returns>
        static AnalysisResult Run(Raster r, string path, Parameters p, IProgress<string> progress, RunClock clock)
        {
            if (Log.IsDebugEnabled && r.Source?.Wcs != null)
                Log.Debug("wcs " + (p.WcsPath != null ? "from " + p.WcsPath : "of the image") + ": " + LogText.Figures(r.Source.Wcs));

            if (RasterOps.LooksLinear(r, out double median))
            {
                clock.Done("refused: the image looks linear, median " + LogText.Num(median) + " under " + LogText.Num(Thresholds.LinearMedianMax));
                throw new RefusedImageException(Tr.Pick(
                    $"the image looks LINEAR (median {Fmt.F4(median)}, under {Fmt.F2(Thresholds.LinearMedianMax)}): the tool measures only finished, stretched images. "
                    + "Stretch it in Siril and analyse the result.",
                    $"imaginea pare LINIARA (mediana {Fmt.F4(median)}, sub {Fmt.F2(Thresholds.LinearMedianMax)}): unealta masoara doar imagini finite, stretch-uite. "
                    + "Stretch-uieste in Siril si analizeaza rezultatul."));
            }

            var res = new AnalysisResult { Path = path, Parameters = p, Raster = r };

            // The stage names of the log are its own, in English; the progress names are the
            // user's. A text a record stores in the user's language (the compression and the
            // reader's note, on the loaded line already; the crop's axis; a region's name) is left
            // out of the figures, so they read the same in both languages.
            progress?.Report(Tr.Pick("file", "fisier"));
            res.File = FileFacts.Of(r);
            res.BitDepth = BitDepthAnalyzer.Analyze(r);
            res.Crop = CropAnalyzer.Analyze(r.W, r.H, p.TargetAspect);
            clock.Lap("file");
            Figures("file", res.File, "Compression", "ReaderNote");
            Figures("bitDepth", res.BitDepth);
            Figures("crop", res.Crop, "Axis");

            progress?.Report(Tr.Pick("tones", "tonuri"));
            res.Tonal = TonalAnalyzer.Analyze(r, p);
            clock.Lap("tones");
            Figures("tones", res.Tonal);
            progress?.Report(Tr.Pick("dust", "praf"));
            res.Simulation = PrintStretchSimulator.Simulate(r, res.Tonal, p);
            clock.Lap("simulation");
            Figures("simulation", res.Simulation);

            progress?.Report(Tr.Pick("noise", "zgomot"));
            res.Noise = NoiseAnalyzer.Analyze(r, p);
            clock.Lap("noise");
            Figures("noise", res.Noise);

            progress?.Report(Tr.Pick("background", "fundal"));
            res.Background = BackgroundAnalyzer.Analyze(r, res.Simulation.Map);
            clock.Lap("background");
            Figures("background", res.Background, "Name");

            progress?.Report(Tr.Pick("stars", "stele"));
            res.Stars = StarAnalyzer.Analyze(r);
            clock.Lap("stars");
            Figures("stars", res.Stars, "Name");
            res.Size = PrintSizeAnalyzer.Analyze(res.Crop.W, res.Crop.H, res.Stars.FwhmMedian, p);
            clock.Lap("size");
            Figures("size", res.Size);

            progress?.Report(Tr.Pick("quantization", "cuantizare"));
            res.Quantization = QuantizationAnalyzer.Analyze(r);
            clock.Lap("quantization");
            Figures("quantization", res.Quantization);

            progress?.Report("JPEG");
            res.Jpeg = JpegAnalyzer.Analyze(r);
            clock.Lap("jpeg");
            Figures("jpeg", res.Jpeg);

            progress?.Report(Tr.Pick("report", "raport"));
            res.Recommendations = Recommendations.Build(res);
            clock.Lap("recommendations");
            if (Log.IsDebugEnabled)
            {
                // The text and the because as the user reads them, so in the run's language.
                foreach (Recommendation x in res.Recommendations)
                    Log.Debug("recommendation topic=" + x.Topic + " verdict=" + x.Verdict + " text=" + x.Text + " because=" + x.Because);
            }

            var sw = new StringWriter();
            ReportWriter.WriteBody(res, sw);
            res.ReportBody = sw.ToString();
            clock.Lap("report");
            clock.Done("verdict=" + res.Overall + " recommendations=" + LogText.Int(res.Recommendations.Count) + " " + LogText.Int(r.W) + "x" + LogText.Int(r.H)
                + " workingSet=" + LogText.WorkingSet());
            return res;
        }

        /// <summary>
        /// The run's inputs for its start line: the path and every parameter, by the rule of
        /// <see cref="LogText.Figures"/>; the profile's note, stored in the user's language, left
        /// out.
        /// </summary>
        /// <param name="path">The image's path, as given.</param>
        /// <param name="p">The parameters.</param>
        /// <returns>The text.</returns>
        static string Inputs(string path, Parameters p) => "path=" + path + " " + LogText.Figures(p, "DmaxProfileNote");

        /// <summary>Writes one result's figures to the diagnostic log at Debug, under its name.</summary>
        /// <param name="name">The result's name in the log: <c>file</c>, <c>tones</c>, ...</param>
        /// <param name="result">The result record.</param>
        /// <param name="skip">The members left out (<see cref="LogText.Figures"/>).</param>
        static void Figures(string name, object result, params string[] skip)
        {
            if (Log.IsDebugEnabled) Log.Debug(name + ": " + LogText.Figures(result, skip));
        }
    }
}
