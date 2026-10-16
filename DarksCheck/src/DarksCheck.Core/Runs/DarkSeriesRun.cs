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

/// <summary>What a run of the darks takes from the user: the parallelism, the cap on frames held in memory, a partner folder of darks to match against (R-063), and the two settings of the normalisation (R-076): the gain per ISO stop and the degrees per doubling of the dark current.</summary>
internal sealed record DarkOptions(int Threads = 0, int MaxFrames = Th.MaxFramesInMemory, string? Against = null, double IsoFactorPerStop = 2, double DoublingCelsius = Th.DoublingCelsius)
{
    internal int EffectiveThreads => Threads > 0 ? Threads : Math.Max(1, Environment.ProcessorCount / 2);
}

/// <summary>A step of the run and how far it is: the phase names are the report's words.</summary>
internal sealed record RunProgress(string Phase, int Done, int Total);

/// <summary>
/// The darks of a session measured: the inventory, the series' figures, the biases' figures
/// when there are biases (or why they could not be read), the dark current map, and the six
/// verdicts.
/// </summary>
internal sealed record DarkSeriesResult(
    Inventory Inventory,
    DarkSeriesAnalysis Darks,
    BiasAnalysis? Biases,
    string? BiasError,
    DarkCurrent? DarkCurrent,
    DarkVerdicts Verdicts,
    int ReferencePlane,
    IndexFigures Index,
    string Label,
    double LightsIndex,
    LightsAnalysis? Lights,
    string? LightsError,
    PartnerAnalysis? Partner,
    string? PartnerError)
{
    /// <summary>A frame that could not be read or decoded, in any group, or a partner that could not be used; a frame the cap skipped is not one.</summary>
    internal bool HasErrors => Inventory.HasErrors
        || Darks.Frames.Any(f => f.Error is not null && !f.Skipped)
        || (Biases?.Frames.Any(f => f.Error is not null && !f.Skipped) ?? false) || BiasError is not null
        || (Lights?.Frames.Any(f => f.Error is not null && !f.Skipped) ?? false) || LightsError is not null
        || PartnerError is not null || (Partner?.Failed ?? 0) > 0;
}

/// <summary>
/// Chains the whole analysis (R-025): the inventory, the biases decoded and measured, the
/// darks decoded into a stack and measured, the dark current, the verdicts, the index and the
/// label, the lights one by one against the dark (R-050), the partner darks (R-063). Each
/// stack is released before the next group is decoded. Nothing is written here; DarkOutputs
/// does that on request.
/// </summary>
internal static class DarkSeriesRun
{
    internal static DarkSeriesResult Run(string folder, DarkOptions options, IProgress<RunProgress>? progress = null, CancellationToken cancel = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        Inventory inv = InventoryRun.Run(folder);
        int threads = options.EffectiveThreads;
        Normalisation nz = new(options.IsoFactorPerStop, options.DoublingCelsius);

        // The biases first: a FITS carries no black level, and the biases' median is the camera's
        // pedestal, which the darks' median would overstate by the dark current (ADR 0007).
        BiasAnalysis? biases = null;
        string? biasError = null;
        double? biasBlack = null;
        if (inv.Session.Biases.Count > 0)
        {
            try
            {
                FrameStack bstack = FrameStack.Load(inv.Session.Biases, threads, options.MaxFrames, Phase("decodare bias-uri", progress), normalisation: nz, cancel: cancel);
                biases = BiasAnalyzer.Analyze(bstack, threads);
                biasBlack = bstack.BlackEstimated ? bstack.Black : null;
            }
            catch (InvalidOperationException e)
            {
                biasError = e.Message;
            }

            // The biases' stack (16-bit mosaics, 48 MB each at 24 MP) is garbage now; collected
            // here, before the darks are decoded, so the peak memory is one stack, not two.
            GC.Collect();
        }

        DarkSeriesAnalysis darks;
        int refPlane;
        {
            FrameStack stack = FrameStack.Load(inv.Session.Darks, threads, options.MaxFrames, Phase("decodare dark-uri", progress), biasBlack, nz, cancel);
            darks = DarkSeriesAnalyzer.Analyze(stack, threads, Phase("masurare dark-uri", progress));
            refPlane = stack.ReferencePlane;
        }

        GC.Collect();
        DarkCurrent? dc = null;
        if (biases is not null)
        {
            try
            {
                dc = BiasAnalyzer.DarkMinusBias(darks, biases, refPlane);
            }
            catch (InvalidOperationException e)
            {
                biasError = e.Message;
                biases = null;
            }
        }

        DarkVerdicts verdicts = DarkVerdictAnalyzer.Judge(darks, biases, dc, refPlane);
        IndexFigures index = IndexAnalyzer.RankIndex(darks.MedianMaster, darks.Black, darks.White, darks.Norm, darks.Width, darks.Height);
        string label = IndexAnalyzer.Label(inv.Session, index);

        LightsAnalysis? lights = null;
        string? lightsError = null;
        if (inv.Session.Lights.Count > 0)
        {
            try
            {
                lights = LightsAnalyzer.Analyze(inv.Session.Lights, darks, threads, options.MaxFrames, Phase("masurare lights", progress), cancel);
            }
            catch (InvalidOperationException e)
            {
                lightsError = e.Message;
            }

            GC.Collect();
        }

        PartnerAnalysis? partner = null;
        string? partnerError = null;
        if (options.Against is not null)
        {
            try
            {
                partner = MatchPartner(options.Against, darks, lights, threads, options.MaxFrames, Phase("decodare partener", progress), cancel);
            }
            catch (Exception e) when (e is IOException or InvalidDataException or InvalidOperationException or ArgumentException or UnauthorizedAccessException)
            {
                partnerError = e.Message;
            }

            GC.Collect();
        }

        double lightsIndex = lights is null ? double.NaN : IndexAnalyzer.EquivalentIndex(index, lights.LightsOverDark);
        return new DarkSeriesResult(inv, darks, biases, biasError, dc, verdicts, refPlane, index, label, lightsIndex, lights, lightsError, partner, partnerError);
    }

    /// <summary>The partner folder (a session or a library) decoded to its median master: its index, the session's darks over it on the shared pixels, the lights over it through the darks, the overlap of the two hot-pixel sets at <see cref="Th.PartnerOverlapAdu"/>.</summary>
    private static PartnerAnalysis MatchPartner(string folder, DarkSeriesAnalysis own, LightsAnalysis? lights, int threads, int maxFrames, IProgress<(int Done, int Total)>? progress, CancellationToken cancel)
    {
        SessionFolder session = SessionFolder.Open(folder);
        FrameStack stack = FrameStack.Load(session.Darks, threads, maxFrames, progress, null, own.Normalisation, cancel);
        if (stack.Decoded.Count == 0)
        {
            throw new InvalidOperationException("niciun dark al partenerului nu s-a putut decoda" + FrameStack.FirstErrors(stack));
        }

        if (stack.Width != own.Width || stack.Height != own.Height)
        {
            throw new InvalidOperationException($"partenerul are {stack.Width} x {stack.Height} px, dark-urile sesiunii {own.Width} x {own.Height}");
        }

        float[] median = DarkSeriesAnalyzer.MedianMaster(stack, threads);
        FrameInfo first = stack.Decoded[0].Info;
        IndexFigures index = IndexAnalyzer.RankIndex(median, stack.Black, stack.White, stack.Norm, stack.Width, stack.Height);
        MatchFigures match = IndexAnalyzer.Match(IndexAnalyzer.SharedRatios(own.MedianMaster, own.Black, own.White, own.Norm, median, stack.Black, stack.White, stack.Norm), own.Normalisation);
        MatchFigures lightsOverPartner = lights is null ? MatchFigures.None : IndexAnalyzer.Chain(lights.LightsOverDark, match, own.Normalisation);

        // the overlap of the two sets at 50 ADU-eq (the partner's) and 20 (ours), as the hand analysis read it
        double own50 = DarkSeriesAnalyzer.HotLevel(Th.PartnerOverlapAdu, own.Norm), own20 = DarkSeriesAnalyzer.HotLevel(Th.HotAdu, own.Norm);
        double p50 = DarkSeriesAnalyzer.HotLevel(Th.PartnerOverlapAdu, stack.Norm);
        int ownHot50 = 0, ownIn = 0, partnerHot50 = 0, partnerIn = 0;
        for (int i = 0; i < median.Length; i++)
        {
            double o = own.MedianMaster[i] - own.Black, p = median[i] - stack.Black;
            if (o > own50)
            {
                ownHot50++;
                if (p > p50)
                {
                    ownIn++;
                }
            }

            if (p > p50)
            {
                partnerHot50++;
                if (o > own20)
                {
                    partnerIn++;
                }
            }
        }

        StackFrame[] failed = stack.Frames.Where(f => f.Error is not null && !f.Skipped).ToArray();
        string? firstError = failed.Length == 0 ? null : failed[0].Info.FileName + ": " + failed[0].Error;
        return new PartnerAnalysis(session.Root, stack.Frames.Count, stack.Decoded.Count, stack.Norm, first.Iso, first.ExposureSeconds, index, IndexAnalyzer.Label(session, index), match, lightsOverPartner, failed.Length, firstError, ownHot50, ownIn, partnerHot50, partnerIn);
    }

    private static PhaseProgress? Phase(string phase, IProgress<RunProgress>? progress)
        => progress is null ? null : new PhaseProgress(phase, progress);

    /// <summary>Forwards a phase's counts synchronously, on the reporting thread (Progress&lt;T&gt; would post them to the thread pool and they could land after the report).</summary>
    private sealed class PhaseProgress(string phase, IProgress<RunProgress> target) : IProgress<(int Done, int Total)>
    {
        public void Report((int Done, int Total) value) => target.Report(new RunProgress(phase, value.Done, value.Total));
    }
}
