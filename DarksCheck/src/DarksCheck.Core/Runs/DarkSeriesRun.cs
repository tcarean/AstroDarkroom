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

/// <summary>What a run of the darks takes from the user: the parallelism and the cap on frames held in memory.</summary>
internal sealed record DarkOptions(int Threads = 0, int MaxFrames = Th.MaxFramesInMemory)
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
internal sealed record DarkSeriesResult(Inventory Inventory, DarkSeriesAnalysis Darks, BiasAnalysis? Biases, string? BiasError, DarkCurrent? DarkCurrent, DarkVerdicts Verdicts, int ReferencePlane)
{
    /// <summary>A frame that could not be read or decoded, in any group; a frame the cap skipped is not one.</summary>
    internal bool HasErrors => Inventory.HasErrors || Darks.Frames.Any(f => f.Error is not null && !f.Skipped) || (Biases?.Frames.Any(f => f.Error is not null) ?? false) || BiasError is not null;
}

/// <summary>
/// Chains the whole analysis of the darks (R-025): the inventory, the darks decoded into a
/// stack and measured, the biases decoded and measured, the dark current, the verdicts.
/// The darks' stack is released before the biases are decoded. Nothing is written here;
/// DarkOutputs does that on request.
/// </summary>
internal static class DarkSeriesRun
{
    internal static DarkSeriesResult Run(string folder, DarkOptions options, IProgress<RunProgress>? progress = null, CancellationToken cancel = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        Inventory inv = InventoryRun.Run(folder);
        int threads = options.EffectiveThreads;

        // The biases first: a FITS carries no black level, and the biases' median is the camera's
        // pedestal, which the darks' median would overstate by the dark current (ADR 0007).
        BiasAnalysis? biases = null;
        string? biasError = null;
        double? biasBlack = null;
        if (inv.Session.Biases.Count > 0)
        {
            try
            {
                FrameStack bstack = FrameStack.Load(inv.Session.Biases, threads, options.MaxFrames, Phase("decodare bias-uri", progress), cancel: cancel);
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
            FrameStack stack = FrameStack.Load(inv.Session.Darks, threads, options.MaxFrames, Phase("decodare dark-uri", progress), biasBlack, cancel);
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
        return new DarkSeriesResult(inv, darks, biases, biasError, dc, verdicts, refPlane);
    }

    private static PhaseProgress? Phase(string phase, IProgress<RunProgress>? progress)
        => progress is null ? null : new PhaseProgress(phase, progress);

    /// <summary>Forwards a phase's counts synchronously, on the reporting thread (Progress&lt;T&gt; would post them to the thread pool and they could land after the report).</summary>
    private sealed class PhaseProgress(string phase, IProgress<RunProgress> target) : IProgress<(int Done, int Total)>
    {
        public void Report((int Done, int Total) value) => target.Report(new RunProgress(phase, value.Done, value.Total));
    }
}
