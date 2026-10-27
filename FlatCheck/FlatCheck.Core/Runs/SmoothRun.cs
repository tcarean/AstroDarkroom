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

// SmoothRun.cs - mode 6 end to end: file -> smoothed flat + text + two maps.
//
// Like LightRun, it can REFUSE: a raw (one frame, uncalibrated) or a colour cube (which the
// reader would average into a mono). The refusal produces no file and no figures.
//
// The FITS file is NOT written here, but through WriteFits, at the caller's request, into a
// Stream of its own: the console opens a FileStream at the path from --smooth, the window at
// the session's path after asking about overwriting, the tests a MemoryStream. The same reason
// as for the PNG map of RunOutcome - the mode does not know where the caller wants the file.

using System.Collections.Generic;
using System.IO;
using static FlatCheck.Ui;

namespace FlatCheck
{
    /// <summary>
    /// The result of a <c>--smooth</c> run: the figures, the loaded frame and the two maps,
    /// or only the refusal.
    /// </summary>
    internal sealed class SmoothRunOutcome
    {
        /// <summary>The figures and the smoothed raster, or null on a refusal.</summary>
        internal readonly SmoothResult Result;

        /// <summary>The loaded frame - its cards and its BITPIX go into the file written. Null
        /// on a refusal.</summary>
        internal readonly LoadedFrame Frame;

        /// <summary>The map of the component removed (original / smoothed), centred on 1. Null
        /// on a refusal.</summary>
        internal readonly byte[] Png;

        /// <summary>The residual of the smoothed flat after its own radial model - what the
        /// lens does. Null on a refusal.</summary>
        internal readonly byte[] SecondaryPng;

        /// <summary>Why it was refused, or null.</summary>
        internal readonly string Refusal;

        internal bool Refused => Result == null;

        /// <summary>Bundles a smoothing's parts, or a refusal.</summary>
        /// <param name="r">The figures, or null on a refusal.</param>
        /// <param name="f">The loaded frame, or null on a refusal.</param>
        /// <param name="png">The removed component's map as PNG, or null.</param>
        /// <param name="secondary">The smoothed flat's residual map as PNG, or null.</param>
        /// <param name="refusal">The refusal's text, or null when the run went ahead.</param>
        internal SmoothRunOutcome(SmoothResult r, LoadedFrame f, byte[] png, byte[] secondary, string refusal)
        {
            Result = r;
            Frame = f;
            Png = png;
            SecondaryPng = secondary;
            Refusal = refusal;
        }
    }

    /// <summary>The scale proposal, or the refusal (raw, cube) - the same as for
    /// Execute.</summary>
    internal sealed class SmoothAdviceOutcome
    {
        internal readonly SmoothScaleAdvice Advice;
        /// <summary>The loaded frame, so that the run after the proposal does not read it once
        /// more.</summary>
        internal readonly LoadedFrame Frame;
        internal readonly string Refusal;
        internal bool Refused => Advice == null;

        /// <summary>Bundles the proposal with its frame, or a refusal.</summary>
        /// <param name="a">The proposal, or null on a refusal.</param>
        /// <param name="f">The loaded frame, or null on a refusal.</param>
        /// <param name="refusal">The refusal's text, or null.</param>
        internal SmoothAdviceOutcome(SmoothScaleAdvice a, LoadedFrame f, string refusal)
        {
            Advice = a;
            Frame = f;
            Refusal = refusal;
        }
    }

    /// <summary>
    /// Mode 6, <c>--smooth</c>: writes a second master with the diffuser texture removed, and
    /// proposes the smoothing scale; refuses a raw and a colour cube.
    /// </summary>
    internal static class SmoothRun
    {
        /// <summary>
        /// Loads the file and, unless it is refused, smooths it at the given scale and writes
        /// the report; on a refusal writes only the refusal's text. Writes no file
        /// (<see cref="WriteFits"/> does). Throws what <see cref="ImageLoader.Load"/> throws.
        /// </summary>
        /// <param name="path">A FITS master.</param>
        /// <param name="scalePx">The smoothing scale, the kernel's sigma in sensor pixels
        /// (<c>--scale</c>).</param>
        /// <param name="report">Where the report's text is written.</param>
        /// <returns>The smoothing with its two maps, or the refusal.</returns>
        internal static SmoothRunOutcome Execute(string path, double scalePx, TextWriter report)
        {
            LoadedFrame f = ImageLoader.Load(path);
            string refusal = Refusal(f, path);
            if (refusal != null)
            {
                Log.Info("smooth refused " + path + ": " + RefusalKind(f));
                report.WriteLine();
                report.WriteLine(refusal);
                return new SmoothRunOutcome(null, null, null, null, refusal);
            }
            return Execute(f, path, scalePx, report);
        }

        /// <summary>The diagnostic log: the run's lines and figures (R-110).</summary>
        static readonly NLog.Logger Log = NLog.LogManager.GetLogger(nameof(SmoothRun));

        /// <summary>Why a frame is refused, in English, for the log.</summary>
        /// <param name="f">A frame <see cref="Refusal"/> refused.</param>
        /// <returns>The reason.</returns>
        static string RefusalKind(LoadedFrame f)
            => f.Cards.Count == 0 ? "a raw, which has no FITS cards to write back" : "a cube of " + LogText.Int(f.Planes) + " planes";

        /// <summary>
        /// The scale proposal: loads, refuses what Execute refuses too, otherwise runs the
        /// advisor and writes its table. The frame stays in the result, so a smoothing at the
        /// proposed scale starts without a second read of the 96 MB file. Throws what
        /// <see cref="ImageLoader.Load"/> throws.
        /// </summary>
        /// <param name="path">A FITS master.</param>
        /// <param name="report">Where the advice's table is written.</param>
        /// <returns>The proposal with the loaded frame, or the refusal.</returns>
        internal static SmoothAdviceOutcome Advise(string path, TextWriter report)
        {
            RunClock clock = RunClock.Start(Log, "smooth advice", path);
            LoadedFrame f = ImageLoader.Load(path);
            clock.Lap("load");
            string refusal = Refusal(f, path);
            if (refusal != null)
            {
                report.WriteLine();
                report.WriteLine(refusal);
                clock.Done("refused: " + RefusalKind(f));
                return new SmoothAdviceOutcome(null, null, refusal);
            }
            SmoothScaleAdvice a = SmoothScaleAdvisor.Advise(f.Image);
            clock.Lap("advise");
            if (Log.IsDebugEnabled) Log.Debug("advice on " + path + ": " + LogText.Advice(a));
            SmoothReport.WriteAdvice(a, path, report);
            clock.Lap("report");
            clock.Done("advice=" + a.Kind + " scalePx=" + LogText.Num(a.ScalePx));
            return new SmoothAdviceOutcome(a, f, null);
        }

        // The refusals BEFORE any computation, as with --light: a file written from a raw or
        // from a cube would be a wrong file with the air of a good one.
        /// <summary>
        /// Says whether a loaded frame can be smoothed: a raw (no FITS cards) and a cube (more
        /// than one plane) cannot.
        /// </summary>
        /// <param name="f">The loaded frame.</param>
        /// <param name="path">Its path, quoted in the refusal.</param>
        /// <returns>The refusal's text, or null when the frame can be smoothed.</returns>
        static string Refusal(LoadedFrame f, string path)
            => f.Cards.Count == 0 ? SmoothReport.RefusalRaw(path)
             : f.Planes > 1 ? SmoothReport.RefusalCube(path, f.Planes)
             : null;

        /// <summary>The smoothing of a frame already loaded and already accepted.</summary>
        /// <param name="f">The loaded frame, a single-plane FITS (not refused).</param>
        /// <param name="path">Its path, for the report.</param>
        /// <param name="scalePx">The kernel's sigma, sensor pixels.</param>
        /// <param name="report">Where the report's text is written.</param>
        /// <returns>The smoothing with its two maps.</returns>
        internal static SmoothRunOutcome Execute(LoadedFrame f, string path, double scalePx, TextWriter report)
        {
            RunClock clock = RunClock.Start(Log, "smooth", path + " scalePx=" + LogText.Num(scalePx));
            var r = SmoothAnalyzer.Analyze(f.Image, scalePx);
            clock.Lap("analyze");
            if (Log.IsDebugEnabled) Log.Debug("smoothing of " + path + ": " + LogText.Smooth(r));
            SmoothReport.Write(r, path, report);
            clock.Lap("report");
            byte[] removed = PngWriter.EncodeResidualMap(r.Removed), after = PngWriter.EncodeResidualMap(r.After.Residual);
            clock.Lap("maps");
            clock.Done("removedFlagged=" + LogText.Yes(r.RemovedFlagged) + " after=" + r.After.Verdict);
            return new SmoothRunOutcome(r, f, removed, after, null);
        }

        /// <summary>
        /// The output type for an input type: the same, when possible. Float stays float at the
        /// same values (Siril's 0..1 master stays 0..1), 16 bits stay 16 bits; the rest (8, 32,
        /// -64, and 0 = unknown) become float. Nothing is renormalised - Siril normalises the
        /// flat by itself at calibration, and a file at a surprising scale is a file someone
        /// will read wrong.
        /// </summary>
        /// <param name="inputBitpix">The input's BITPIX (0 when unknown).</param>
        /// <returns>16 for a 16-bit input, -32 (float) otherwise.</returns>
        internal static int OutputBitpix(int inputBitpix) => inputBitpix == 16 ? 16 : -32;

        /// <summary>The HISTORY lines: who, which version, which scale, from which
        /// file.</summary>
        /// <param name="o">A smoothing that was not refused.</param>
        /// <param name="sourcePath">The master smoothed; only its file name is written.</param>
        /// <returns>Two lines, in the run's language, each short enough for one HISTORY
        /// card.</returns>
        internal static IReadOnlyList<string> History(SmoothRunOutcome o, string sourcePath) => new[]
        {
            // under 72 characters each, so they fit on a single HISTORY card
            T($"{VersionInfo.Banner} --smooth: local quadratic fit on tiles, sigma {o.Result.ScalePx:F0} px",
              $"{VersionInfo.Banner} --smooth: fit patratic local pe dale, sigma {o.Result.ScalePx:F0} px"),
            T($"source: {Path.GetFileName(sourcePath)}", $"sursa: {Path.GetFileName(sourcePath)}"),
        };

        /// <summary>
        /// Writes the smoothed flat, with the input's header and the HISTORY lines, into
        /// <paramref name="s"/>.
        /// </summary>
        /// <param name="o">A smoothing that was not refused.</param>
        /// <param name="sourcePath">The master smoothed, named in the HISTORY.</param>
        /// <param name="s">The stream to write the FITS into; left open.</param>
        internal static void WriteFits(SmoothRunOutcome o, string sourcePath, Stream s)
            => FitsWriter.Write(s, o.Result.Smoothed, OutputBitpix(o.Frame.Bitpix), o.Frame.Cards, History(o, sourcePath));
    }
}
