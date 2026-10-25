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

namespace DarksCheck.Core;

/// <summary>
/// One decoded frame of a stack: its metadata and its mosaic as 16-bit values, or why it has none:
/// an error, or skipped by the frame cap (not an error).
/// </summary>
/// <param name="Info">The frame's metadata.</param>
/// <param name="Pixels">The mosaic, row-major, in ADU on the stack's scale; null when the frame
/// failed or was skipped.</param>
/// <param name="Error">Why the frame has no mosaic, in Romanian for the report; null when it
/// decoded.</param>
/// <param name="Skipped">True when the frame cap left the frame out; its
/// <paramref name="Error"/> then says so.</param>
internal sealed record StackFrame(FrameInfo Info, ushort[]? Pixels, string? Error, bool Skipped = false);

/// <summary>
/// A group of frames decoded into memory as 16-bit mosaics (24 MP = 48 MB each; ADR 0007),
/// with the geometry and the scale they share: every frame must have the first decoded
/// frame's size, or it is listed with an error and left out. The scale is the file's
/// (libraw's exact black and white for a raw; BITPIX's range for an integer FITS; a float
/// FITS on 0..1 is brought to 0..65535). A FITS carries no black level, and a FITS Siril
/// converted from a raw keeps the camera's pedestal (150 on the D3300), so for a FITS the
/// black is the median of the first decoded frame's reference plane: on a dark that is the
/// pedestal to within the dark current (ADR 0007). The normalisation factor turns ADU into
/// ADU equivalent at ISO 400 and 120 s (ADR 0004): 1 when the files do not say ISO or exposure.
/// </summary>
internal sealed class FrameStack
{
    internal IReadOnlyList<StackFrame> Frames { get; }
    internal int Width { get; }
    internal int Height { get; }
    internal double Black { get; }
    internal double White { get; }
    internal CfaPattern? Cfa { get; }
    internal string ScaleSource { get; }
    internal double Norm { get; }

    /// <summary>
    /// The settings the normalisation was read with (R-076); the lights and the partner take the
    /// same.
    /// </summary>
    internal Normalisation Normalisation { get; }

    /// <summary>
    /// True when the files carry no black level of their own (a FITS), so the stack's black is an
    /// estimate or a value handed in.
    /// </summary>
    internal bool BlackEstimated { get; }

    /// <summary>
    /// A stack with its frames, geometry and scale, as <see cref="Load"/> found them.
    /// </summary>
    /// <param name="frames">Every file of the group, in file order, decoded or not.</param>
    /// <param name="w">The mosaic's width, in pixels; 0 when nothing decoded.</param>
    /// <param name="h">The mosaic's height, in pixels; 0 when nothing decoded.</param>
    /// <param name="black">The black level, in ADU; NaN while a FITS's black is not yet
    /// set.</param>
    /// <param name="white">The white level, in ADU.</param>
    /// <param name="cfa">The colour order; null when the file does not say it.</param>
    /// <param name="scaleSource">Where the black and the white come from, in Romanian for the
    /// report.</param>
    /// <param name="norm">The first decoded frame's normalisation factor, above 0.</param>
    /// <param name="normalisation">The settings the factor was read with.</param>
    /// <param name="blackEstimated">True when the black is an estimate or a value handed in
    /// (a FITS).</param>
    private FrameStack(IReadOnlyList<StackFrame> frames, int w, int h, double black, double white, CfaPattern? cfa, string scaleSource, double norm, Normalisation normalisation, bool blackEstimated = false)
    {
        Frames = frames;
        Width = w;
        Height = h;
        Black = black;
        White = white;
        Cfa = cfa;
        ScaleSource = scaleSource;
        Norm = norm;
        Normalisation = normalisation;
        BlackEstimated = blackEstimated;
    }

    /// <summary>The frames that decoded, in file order.</summary>
    internal IReadOnlyList<StackFrame> Decoded => Frames.Where(f => f.Pixels is not null).ToList();

    /// <summary>
    /// The name of CFA position p (R, G1, G2, B) from the file's colour order, or `CFA[r,c]` when
    /// unknown.
    /// </summary>
    /// <param name="p">The CFA position, 0..3.</param>
    /// <returns>The position's name, for the report.</returns>
    internal string PlaneName(int p) => Mosaic.Label(Cfa, p);

    /// <summary>
    /// The position named G1 when the colour order is known, else position 1 (the second of the
    /// first row).
    /// </summary>
    internal int ReferencePlane
    {
        get
        {
            for (int p = 0; p < 4; p++)
            {
                if (PlaneName(p) == "G1")
                {
                    return p;
                }
            }

            return 1;
        }
    }

    /// <summary>
    /// Decodes the files, <paramref name="threads"/> at a time, with the decoder's double raster
    /// converted to 16-bit as soon as it lands (a 24 MP raster of doubles is 194 MB,
    /// <paramref name="threads"/> of them in flight at most). The first file in order sets the
    /// geometry and the scale, the others must match it. Above <paramref name="maxFrames"/> frames
    /// only every k-th file is decoded, so the memory stays bounded; the others are listed as
    /// skipped. <paramref name="fitsBlack"/> is the black level to use for a FITS, when a group
    /// read before (the biases) already established it; null estimates it from the first frame.
    /// <paramref name="normalisation"/> is the settings the ADU-eq factor is read with
    /// (<see cref="Normalisation.Default"/> when null).
    /// </summary>
    /// <param name="paths">The group's files, in order; not null.</param>
    /// <param name="threads">How many files are decoded at once; below 1 counts as 1.</param>
    /// <param name="maxFrames">The frame cap, at least 1: above it every k-th file is decoded,
    /// k = ceil(n / maxFrames).</param>
    /// <param name="progress">Told (done, total) after each file; null for none.</param>
    /// <param name="fitsBlack">The black level for a FITS, in ADU; null to estimate it.</param>
    /// <param name="normalisation">The normalisation settings; null for the defaults.</param>
    /// <param name="cancel">Stops the decoding; the cancellation is thrown, not recorded as a
    /// frame's error.</param>
    /// <returns>The stack: every file listed, the decoded ones with their mosaic; a stack of
    /// size 0 when no file decoded.</returns>
    internal static FrameStack Load(IReadOnlyList<string> paths, int threads, int maxFrames, IProgress<(int Done, int Total)>? progress = null, double? fitsBlack = null, Normalisation? normalisation = null, CancellationToken cancel = default)
    {
        ArgumentNullException.ThrowIfNull(paths);
        Normalisation nz = normalisation ?? Normalisation.Default;
        int n = paths.Count;
        int step = n > maxFrames ? (int)Math.Ceiling(n / (double)maxFrames) : 1;
        StackFrame[] frames = new StackFrame[n];
        (int W, int H, double Black, double White, CfaPattern? Cfa, string Source, double Norm)? geometry = null;
        FrameInfo? firstInfo = null;
        object gate = new();
        int done = 0;
        ParallelOptions po = new() { MaxDegreeOfParallelism = Math.Max(1, threads), CancellationToken = cancel };

        // The first file alone, so the geometry is the first file's whatever the thread timing;
        // then the rest in parallel.
        void Decode(int i)
        {
            string path = paths[i];
            if (i % step != 0)
            {
                frames[i] = new StackFrame(FrameInfo.Read(i + 1, path), null, $"sarit (peste {maxFrames} cadre, se decodeaza unul din {step})", Skipped: true);
                progress?.Report((Interlocked.Increment(ref done), n));
                return;
            }

            try
            {
                LoadedFrame f = ImageLoader.Load(path);
                FrameInfo info = FrameInfo.From(i + 1, path, f);
                (ushort[] px, double black, double white, string source) = ToUshort(f);
                lock (gate)
                {
                    geometry ??= (f.Image.W, f.Image.H, black, white, f.Cfa, source, nz.NormOf(info));
                    firstInfo ??= info;
                }

                (int w, int h, _, _, _, _, _) = geometry.Value;
                frames[i] = f.Image.W != w || f.Image.H != h
                    ? new StackFrame(info, null, $"dimensiune diferita: {f.Image.W} x {f.Image.H} fata de {w} x {h}")
                    : !SameSettings(info, firstInfo)
                        ? new StackFrame(info, null, $"setari diferite de primul cadru: ISO {Fmt(info.Iso)}, {Fmt(info.ExposureSeconds)} s")
                        : new StackFrame(info, px, null);
                f = null!;

                // The decoder's raster (194 MB of doubles at 24 MP) is garbage now, but on a
                // machine with memory to spare the collector lets such rasters pile up for
                // gigabytes before it runs: measured at 14 GB of working set for 50 frames
                // (ADR 0007). One collection per decoded frame costs milliseconds against a
                // second of decoding and keeps the peak at the frames kept plus those in flight.
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: false);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                frames[i] = new StackFrame(FrameInfo.Read(i + 1, path), null, e.Message);
            }

            progress?.Report((Interlocked.Increment(ref done), n));
        }

        // One file at a time until one decodes and sets the geometry (the first file may be
        // corrupt); the rest in parallel.
        int next = 0;
        while (next < n && geometry is null)
        {
            Decode(next++);
        }

        Parallel.For(next, n, po, Decode);

        if (geometry is null)
        {
            return new FrameStack(frames, 0, 0, 0, 0, null, "niciun cadru decodat", 1, nz);
        }

        (int gw, int gh, double gb, double gwhite, CfaPattern? gcfa, string gsource, double gnorm) = geometry.Value;
        FrameStack stack = new(frames, gw, gh, gb, gwhite, gcfa, gsource, gnorm, nz);
        if (double.IsNaN(gb))
        {
            string fmt(double v) => v.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
            if (fitsBlack is double known)
            {
                stack = new FrameStack(frames, gw, gh, known, gwhite, gcfa, gsource + $", negru {fmt(known)} din bias-uri", gnorm, nz, blackEstimated: true);
            }
            else
            {
                // no black level in the file: the median of the first decoded frame's reference
                // plane
                StackFrame first = frames.First(f => f.Pixels is not null);
                double black = Planes.Median(Planes.Extract(first.Pixels!, gw, gh, stack.ReferencePlane));
                stack = new FrameStack(frames, gw, gh, black, gwhite, gcfa, gsource + $", negru {fmt(black)} estimat din mediana cadrului {first.Info.Index}", gnorm, nz, blackEstimated: true);
            }
        }

        return stack;
    }

    /// <summary>
    /// One file decoded on its own, for a group that is never stacked (the lights): the frame
    /// with its 16-bit mosaic, or with the error when its size is not <paramref name="w"/> x
    /// <paramref name="h"/>; the file's black (NaN for a FITS) and white. Throws what the
    /// readers throw.
    /// </summary>
    /// <param name="index">The frame's 1-based index in its group.</param>
    /// <param name="path">The file.</param>
    /// <param name="w">The width the frame must have, in pixels.</param>
    /// <param name="h">The height the frame must have, in pixels.</param>
    /// <returns>The frame, the file's black level in ADU (NaN for a FITS) and its white
    /// level.</returns>
    internal static (StackFrame Frame, double Black, double White) DecodeOne(int index, string path, int w, int h)
    {
        LoadedFrame f = ImageLoader.Load(path);
        FrameInfo info = FrameInfo.From(index, path, f);
        (ushort[] px, double black, double white, _) = ToUshort(f);
        return f.Image.W == w && f.Image.H == h
            ? (new StackFrame(info, px, null), black, white)
            : (new StackFrame(info, null, $"dimensiune diferita: {f.Image.W} x {f.Image.H} fata de {w} x {h}"), black, white);
    }

    /// <summary>A setting for an error message: up to three decimals, invariant culture.</summary>
    /// <param name="v">The value; null when unknown.</param>
    /// <returns>The value as text, or "?" when null.</returns>
    private static string Fmt(double? v) => v is double d ? d.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) : "?";

    /// <summary>
    /// The same ISO (when both say it) and an exposure within <see cref="Th.ExposureTolerance"/> of
    /// the first frame's (when both say it): each setting on its own, so ISO 800 at 60 s is not ISO
    /// 400 at 120 s.
    /// </summary>
    /// <param name="a">The frame to compare; not null.</param>
    /// <param name="first">The first decoded frame of the group; not null.</param>
    /// <returns>True when neither setting known on both sides differs.</returns>
    internal static bool SameSettings(FrameInfo a, FrameInfo first)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(first);
        bool sameIso = a.Iso is not double ia || first.Iso is not double ib || ia == ib;
        bool sameExposure = a.ExposureSeconds is not double ea || first.ExposureSeconds is not double eb || MatchAnalyzer.SameExposure(ea, eb);
        return sameIso && sameExposure;
    }

    /// <summary>
    /// ": file: error; file: error" for the first three frames that failed (not the skipped ones),
    /// or "" when none did.
    /// </summary>
    /// <param name="stack">The stack; not null.</param>
    /// <returns>The errors, with a leading ": ", or an empty string.</returns>
    internal static string FirstErrors(FrameStack stack)
    {
        ArgumentNullException.ThrowIfNull(stack);
        string[] errors = stack.Frames.Where(f => f.Error is not null && !f.Skipped).Take(3).Select(f => f.Info.FileName + ": " + f.Error).ToArray();
        return errors.Length == 0 ? "" : ": " + string.Join("; ", errors);
    }

    /// <summary>
    /// ISO / 400 times exposure / 120 s with the default settings, or 1 when either is unknown
    /// (ADR 0004); <see cref="Normalisation.NormOf"/> takes the settings.
    /// </summary>
    /// <param name="info">The frame's metadata; not null.</param>
    /// <returns>The factor, above 0.</returns>
    internal static double NormOf(FrameInfo info) => Normalisation.Default.NormOf(info);

    /// <summary>
    /// A decoded frame's raster as 16-bit values, rounded and clamped to 0..65535, with its
    /// scale: the file's black and white, or for a FITS a NaN black and the white BITPIX
    /// allows; a float FITS on 0..1 is multiplied by 65535, one already in ADU is kept, both
    /// with 65535 as the white.
    /// </summary>
    /// <param name="f">The decoded frame.</param>
    /// <returns>The mosaic, the black (NaN when the file has none), the white, and the scale's
    /// source in Romanian for the report.</returns>
    private static (ushort[] Pixels, double Black, double White, string Source) ToUshort(LoadedFrame f)
    {
        Img img = f.Image;
        // NaN marks a missing black level; Load replaces it with the first frame's median
        double black = f.Scale?.Black ?? double.NaN;
        double white = f.Scale?.White ?? (f.Bitpix < 0 ? 1.0 : (1 << Math.Min(f.Bitpix, 16)) - 1);
        string source = f.Scale?.Source ?? $"FITS (presupus din BITPIX {f.Bitpix})";
        double factor = 1;
        if (f.Scale is null && f.Bitpix < 0)
        {
            // a float frame: on 0..1 it is brought to the 16-bit range so the stack stays
            // integer; already in ADU it is kept, with 65535 as the presumed white
            double max = img.D.Length == 0 ? 0 : img.D.Max();
            if (max <= 1.0)
            {
                factor = 65535;
                white = 65535;
                source += ", adus la 0 .. 65535";
            }
            else
            {
                white = 65535;
                source += ", valori in ADU, alb presupus 65535";
            }
        }

        ushort[] px = new ushort[img.D.Length];
        for (int i = 0; i < px.Length; i++)
        {
            double v = Math.Round(img.D[i] * factor);
            px[i] = (ushort)Math.Clamp(v, 0, 65535);
        }

        return (px, black, white, source);
    }
}
