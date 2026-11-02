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

using NLog;
using static LightsCheck.Core.Ui;

namespace LightsCheck.Core;

/// <summary>
/// One light of a session, decoded and measured, with the central crop the series keeps for the
/// sensor's pattern (R-046).
/// </summary>
/// <param name="Info">What the file says about the shot.</param>
/// <param name="Measurement">The frame's figures.</param>
/// <param name="Crop">
/// The central square of the mosaic, in ADU above black, of side <see cref="Th.PatternCrop"/>
/// or the frame's shorter side.
/// </param>
internal sealed record MeasuredFrame(FrameInfo Info, FrameMeasurement Measurement, PatternCrop Crop);

/// <summary>
/// Chains open, decode and measure for one frame (R-017): the session folder gives the file,
/// the loader the mosaic and its scale, the analyzer the figures. A FITS carries no ADU
/// scale, so one is assumed from its BITPIX: the full integer range above zero, or 0..1 for
/// a float frame, which is how Siril writes a calibrated light.
/// </summary>
internal static class FrameRun
{
    /// <summary>
    /// Opens the session and measures one of its lights. The folder errors of
    /// <see cref="SessionFolder.Open"/> propagate; an index outside the session throws
    /// <see cref="InvalidOperationException"/> with a message in the run's language.
    /// </summary>
    /// <param name="folder">The session folder.</param>
    /// <param name="frameIndex">The light's 1-based position in the session's sorted list.</param>
    /// <returns>The light, decoded and measured.</returns>
    internal static MeasuredFrame Run(string folder, int frameIndex)
    {
        using RunClock clock = RunClock.Start(Log, "frame", folder + " index=" + LogText.Int(frameIndex));
        SessionFolder session = SessionFolder.Open(folder);
        clock.Lap("open");
        if (frameIndex < 1 || frameIndex > session.Lights.Count)
        {
            throw new InvalidOperationException(T($"frame {frameIndex} does not exist: the session has {session.Lights.Count} frames in {session.LightsDir}", $"cadrul {frameIndex} nu exista: sesiunea are {RoCount(session.Lights.Count)}cadre in {session.LightsDir}"));
        }

        string path = session.Lights[frameIndex - 1];
        MeasuredFrame frame = Measure(frameIndex, path, clock);
        FrameMeasurement m = frame.Measurement;
        clock.Done("detected=" + LogText.Int(m.Detected) + " fwhm=" + LogText.Num(m.FwhmMedian) + " elongation=" + LogText.Num(m.ElongationMedian));
        return frame;
    }

    /// <summary>
    /// Decodes and measures one file; the metadata comes with the same load. A file the readers
    /// cannot decode throws their exception. The measurement's figures go to the diagnostic log at
    /// Debug (R-071).
    /// </summary>
    /// <param name="index">
    /// The frame's 1-based position in the session, carried into its metadata.
    /// </param>
    /// <param name="path">The frame's file, a camera raw or a FITS.</param>
    /// <param name="clock">
    /// The run's clock, whose <c>load</c>, <c>measure</c> and <c>crop</c> stages are timed; null
    /// (the series, which measures frames in parallel) times nothing.
    /// </param>
    /// <returns>The frame's metadata, figures and central crop.</returns>
    internal static MeasuredFrame Measure(int index, string path, RunClock? clock = null)
    {
        LoadedFrame f = ImageLoader.Load(path);
        FrameInfo info = FrameInfo.From(index, path, f);
        clock?.Lap("load");
        AduScale scale = f.Scale ?? AssumedScale(f.Bitpix);
        // The double mosaic (193 MB on 24 MP) is dropped as soon as the float raster exists.
        Raster raw = Raster.FromImg(f.Image, scale.Black);
        f.Image = null;
        FrameMeasurement m = FrameAnalyzer.Analyze(raw, scale, f.Cfa);
        clock?.Lap("measure");
        if (Log.IsDebugEnabled)
        {
            Log.Debug("frame " + LogText.Int(index) + " " + info.FileName + ": " + LogText.Measured(m));
        }

        PatternCrop crop = PatternCrop.Cut(raw);
        clock?.Lap("crop");
        return new MeasuredFrame(info, m, crop);
    }

    /// <summary>The diagnostic log: the run's lines and each frame's figures (R-071).</summary>
    private static readonly Logger Log = LogManager.GetLogger(nameof(FrameRun));

    /// <summary>
    /// The scale a FITS is assumed to use, from its BITPIX: never a guess from the data.
    /// </summary>
    /// <param name="bitpix">
    /// The FITS BITPIX: 8, 16 or 32 for integers; any other value is taken as float.
    /// </param>
    /// <returns>
    /// The white level of the integer range (255, 65535 or 2^32 - 1) or 1.0 for a float frame,
    /// black at 0, with a source text for the report, in the run's language.
    /// </returns>
    internal static AduScale AssumedScale(int bitpix) => bitpix switch
    {
        8 => new AduScale(255, 0, T("FITS (assumed from BITPIX 8)", "FITS (presupus din BITPIX 8)")),
        16 => new AduScale(65535, 0, T("FITS (assumed from BITPIX 16)", "FITS (presupus din BITPIX 16)")),
        32 => new AduScale(4294967295.0, 0, T("FITS (assumed from BITPIX 32)", "FITS (presupus din BITPIX 32)")),
        _ => new AduScale(1.0, 0, T("FITS (assumed 0..1, a floating-point frame)", "FITS (presupus 0..1, cadru in virgula mobila)")),
    };
}
