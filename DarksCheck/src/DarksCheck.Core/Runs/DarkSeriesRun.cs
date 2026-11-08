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
using static DarksCheck.Core.Ui;

namespace DarksCheck.Core;

/// <summary>
/// What a run of the darks takes from the user: the parallelism, the cap on frames held in memory,
/// a partner folder of darks to match against (R-063), and the two settings of the normalisation
/// (R-076): the gain per ISO stop and the degrees per doubling of the dark current.
/// </summary>
/// <param name="Threads">How many frames are decoded in parallel; 0 or less takes half the
/// processor count (at least 1).</param>
/// <param name="MaxFrames">How many frames of a group of darks or biases are held in memory at
/// most; above it one frame in k is taken. The lights are all measured (R-050).</param>
/// <param name="Against">The partner folder of darks (a library or a session), or null for
/// none.</param>
/// <param name="IsoFactorPerStop">The gain factor per ISO stop of the normalisation (2 by
/// default).</param>
/// <param name="DoublingCelsius">The degrees Celsius per doubling of the dark current, of the
/// normalisation.</param>
/// <param name="LightsFrom">The folder to take the lights from instead of the session's
/// <c>lights/</c> (R-055): a session or a folder of lights; null for the session's own.</param>
internal sealed record DarkOptions(int Threads = 0, int MaxFrames = Th.MaxFramesInMemory, string? Against = null, double IsoFactorPerStop = 2, double DoublingCelsius = Th.DoublingCelsius, string? LightsFrom = null)
{
    internal int EffectiveThreads => Threads > 0 ? Threads : Math.Max(1, Environment.ProcessorCount / 2);
}

/// <summary>A step of the run and how far it is: the phase names are the report's words.</summary>
/// <param name="Phase">The phase's name, in the run's language, as the console prints it.</param>
/// <param name="Done">How many items of the phase are done.</param>
/// <param name="Total">How many items the phase has.</param>
internal sealed record RunProgress(string Phase, int Done, int Total);

/// <summary>
/// The darks of a session measured: the inventory, the series' figures, the biases' figures
/// when there are biases (or why they could not be read), the dark current map, and the six
/// verdicts.
/// </summary>
/// <param name="Inventory">The frames' metadata, the cadence and the match of check 1.</param>
/// <param name="Darks">The figures of the dark series.</param>
/// <param name="Biases">The biases' figures, or null when there are no biases or they could not be
/// used.</param>
/// <param name="BiasError">Why the biases could not be used (in the run's language), or null.</param>
/// <param name="DarkCurrent">The dark current (darks minus biases), or null without usable
/// biases.</param>
/// <param name="Verdicts">The six verdicts of checks 2 to 8.</param>
/// <param name="ReferencePlane">The CFA position the maps and the single-plane figures are read on
/// (G1 when the colour order is known).</param>
/// <param name="Index">The darks' index (R-060).</param>
/// <param name="Label">The label for the darks' folder name (R-061).</param>
/// <param name="LightsIndex">The lights' equivalent index (R-064); NaN without measured
/// lights.</param>
/// <param name="Lights">The lights measured against the dark, or null when there are no lights or
/// they failed.</param>
/// <param name="LightsError">Why the lights could not be measured (in the run's language), or
/// null.</param>
/// <param name="Partner">The partner folder matched (R-063), or null without one, when it failed,
/// or when its darks stand in for the session's.</param>
/// <param name="PartnerError">Why the partner could not be used (in the run's language), or
/// null.</param>
/// <param name="DarksFromPartner">The partner's root when its darks stand in for a session without
/// darks (R-054), else null.</param>
/// <param name="DarksFromPartnerLabel">The partner's own label (R-061) when its darks stand in
/// for the session's, whose <paramref name="Label"/> is then the lights' (R-066); else
/// null.</param>
/// <param name="LabelTag">The tag <paramref name="Label"/> ends with: the darks' index tag, or
/// the lights' equivalent index tag for a session whose darks are the partner's (R-066); the
/// window shows it beside the label without deciding it.</param>
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
    string? PartnerError,
    string? DarksFromPartner = null,
    string? DarksFromPartnerLabel = null,
    string LabelTag = "")
{
    /// <summary>
    /// A frame that could not be read or decoded, in any group, or a partner that could not be
    /// used; a frame the cap skipped is not one.
    /// </summary>
    internal bool HasErrors => Inventory.HasErrors
        || Darks.Frames.Any(f => f.Error is not null && !f.Skipped)
        || (Biases?.Frames.Any(f => f.Error is not null && !f.Skipped) ?? false) || BiasError is not null
        || (Lights?.Frames.Any(f => f.Error is not null) ?? false) || LightsError is not null
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
    /// <summary>
    /// Runs the whole analysis on one folder. A frame that cannot be read becomes that frame's
    /// error; a group (biases, lights, partner) that fails becomes that group's error in the
    /// result.
    /// </summary>
    /// <param name="folder">The session folder or the library folder.</param>
    /// <param name="options">The user's settings of the run; not null.</param>
    /// <param name="progress">Receives each phase's counts, synchronously, on the thread doing the
    /// work; null for none.</param>
    /// <param name="cancel">Stops the decoding and the measuring; the run then throws
    /// <see cref="OperationCanceledException"/>.</param>
    /// <returns>Every figure, verdict and error of the run.</returns>
    /// <exception cref="InvalidOperationException">
    /// The folder has no frames, a session without darks is run without a partner, the partner
    /// cannot be opened for such a session, or no dark could be decoded.
    /// </exception>
    /// <exception cref="ArgumentException">The folder is null, empty or blank.</exception>
    /// <exception cref="DirectoryNotFoundException">The folder does not exist.</exception>
    /// <exception cref="IOException">A folder cannot be listed.</exception>
    /// <exception cref="UnauthorizedAccessException">A folder may not be listed.</exception>
    internal static DarkSeriesResult Run(string folder, DarkOptions options, IProgress<RunProgress>? progress = null, CancellationToken cancel = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        int threads = options.EffectiveThreads;
        using RunClock clock = RunClock.Start(Log, "darks", "folder=" + folder + " threads=" + LogText.Int(threads) + " maxFrames=" + LogText.Int(options.MaxFrames)
            + " against=" + LogText.Str(options.Against) + " lightsFrom=" + LogText.Str(options.LightsFrom) + " isoFactor=" + LogText.Num(options.IsoFactorPerStop) + " doubling=" + LogText.Num(options.DoublingCelsius));
        Inventory inv = InventoryRun.Run(folder, options.LightsFrom);
        Normalisation nz = new(options.IsoFactorPerStop, options.DoublingCelsius);

        // A session with lights and no darks takes the partner's darks as its own (R-054, ADR
        // 0011): they fill the stack, take checks 2 to 8, check 9 and INDEX, and the match of
        // check 1 reads their settings; the partner rows of INDEX are then meaningless and skipped.
        // The label stays the session's own, on the lights' index (R-066, ADR 0039); the partner's
        // label goes on the partner's row.
        IReadOnlyList<string> darkPaths = inv.Session.Darks;
        SessionFolder? partnerSessionUsed = null;
        string? darksFromPartner = null;
        if (!inv.Session.HasDarks)
        {
            if (options.Against is null)
            {
                throw new InvalidOperationException(RunReport.LightsOnlyNeedsPartner(inv.Session.Root));
            }

            SessionFolder partnerSession;
            try
            {
                partnerSession = SessionFolder.Open(options.Against);
            }
            catch (Exception e) when (e is IOException or InvalidOperationException or ArgumentException or UnauthorizedAccessException)
            {
                // the window shows this on the problem card: it must say whose folder failed
                throw new InvalidOperationException(T("the partner: ", "partenerul: ") + e.Message, e);
            }

            FrameInfo[] partnerDarks = InventoryRun.ReadAll(partnerSession.Darks);
            darkPaths = partnerSession.Darks;
            partnerSessionUsed = partnerSession;
            darksFromPartner = partnerSession.Root;
            inv = inv with { Match = MatchAnalyzer.Analyze(partnerDarks, inv.Biases, inv.Lights), DarksFromPartner = partnerSession.Root, PartnerDarks = partnerDarks };
            Log.Info("the partner's darks stand in for the session's (R-054): " + partnerSession.Root + ", " + LogText.Int(partnerDarks.Length) + " darks");
            if (Log.IsDebugEnabled)
            {
                Log.Debug("match against the partner's darks: " + LogText.Figures(inv.Match));
            }
        }

        clock.Lap("inventory");

        // The biases first: a FITS carries no black level, and the biases' median is the camera's
        // pedestal, which the darks' median would overstate by the dark current (ADR 0007).
        BiasAnalysis? biases = null;
        string? biasError = null;
        double? biasBlack = null;
        if (inv.Session.Biases.Count > 0)
        {
            try
            {
                FrameStack bstack = FrameStack.Load(inv.Session.Biases, threads, options.MaxFrames, Phase(T("Reading the biases", "Se citesc bias-urile"), progress), normalisation: nz, cancel: cancel);
                biases = BiasAnalyzer.Analyze(bstack, threads);
                biasBlack = bstack.BlackEstimated ? bstack.Black : null;
            }
            catch (InvalidOperationException e)
            {
                Log.Warn(e, "the biases could not be used; the report says why");
                biasError = e.Message;
            }

            // The biases' stack (16-bit mosaics, 48 MB each at 24 MP) is garbage now; collected
            // here, before the darks are decoded, so the peak memory is one stack, not two.
            GC.Collect();
            Released("the biases were released");
            clock.Lap("biases");
            if (biases is not null && Log.IsDebugEnabled)
            {
                Log.Debug("biases: " + LogText.Figures(biases, "Frames", "MeanMaster"));
                foreach (BiasFrameFigures f in biases.Frames.Where(x => x.Error is null))
                {
                    Log.Debug("bias " + LogText.Int(f.Info.Index) + " " + f.Info.FileName + ": " + LogText.Figures(f, "Info", "Error", "Skipped"));
                }
            }
        }

        DarkSeriesAnalysis darks;
        int refPlane;
        {
            FrameStack stack = FrameStack.Load(darkPaths, threads, options.MaxFrames, Phase(darksFromPartner is null ? T("Reading the darks", "Se citesc dark-urile") : T("Reading the partner's darks", "Se citesc dark-urile partenerului"), progress), biasBlack, nz, cancel);
            clock.Lap("darks");
            darks = DarkSeriesAnalyzer.Analyze(stack, threads, Phase(T("Measuring the darks", "Se masoara dark-urile"), progress));
            refPlane = stack.ReferencePlane;
            clock.Lap("measure");
        }

        GC.Collect();
        Released("the darks' stack was released");
        if (Log.IsDebugEnabled)
        {
            // The scale's source is the report's words, in the run's language; the stack's line has it in the log's.
            Log.Debug("series: " + LogText.Figures(darks, "Frames", "MedianMaster", "MeanMaster", "ScaleSource"));
            foreach (DarkFrameFigures f in darks.Frames.Where(x => x.Error is null))
            {
                Log.Debug("dark " + LogText.Int(f.Info.Index) + " " + f.Info.FileName + ": " + LogText.Figures(f, "Info", "Error", "Skipped"));
            }
        }

        DarkCurrent? dc = null;
        if (biases is not null)
        {
            try
            {
                dc = BiasAnalyzer.DarkMinusBias(darks, biases, refPlane);
                if (Log.IsDebugEnabled)
                {
                    Log.Debug("dark current: " + LogText.Figures(dc));
                }
            }
            catch (InvalidOperationException e)
            {
                Log.Warn(e, "the dark current could not be measured; the biases are left out of the run");
                biasError = e.Message;
                biases = null;
            }
        }

        DarkVerdicts verdicts = DarkVerdictAnalyzer.Judge(darks, biases, dc, refPlane);
        IndexFigures index = IndexAnalyzer.RankIndex(darks.MedianMaster, darks.Black, darks.White, darks.Norm, darks.Width, darks.Height);
        // A session without darks is labelled on its lights' index, known only after the lights
        // (R-066); its partner's own label is kept for the partner's row.
        string? partnerLabel = partnerSessionUsed is null ? null : IndexAnalyzer.Label(partnerSessionUsed, index);
        string labelTag = partnerSessionUsed is null ? index.Tag : IndexFigures.TagFor(double.NaN);
        string label = IndexAnalyzer.Label(inv.Session, labelTag);
        clock.Lap("verdicts");
        if (Log.IsDebugEnabled)
        {
            Log.Debug("verdicts: " + LogText.Figures(verdicts));
            Log.Debug("index: " + LogText.Figures(index) + " label=" + label + " partnerLabel=" + LogText.Str(partnerLabel));
        }

        LightsAnalysis? lights = null;
        string? lightsError = null;
        if (inv.Session.Lights.Count > 0)
        {
            try
            {
                lights = LightsAnalyzer.Analyze(inv.Session.Lights, darks, threads, Phase(T("Measuring the lights", "Se masoara lights-urile"), progress), cancel);
            }
            catch (InvalidOperationException e)
            {
                Log.Warn(e, "the lights could not be measured; the report says why");
                lightsError = e.Message;
            }

            GC.Collect();
            Released("the lights were released");
            clock.Lap("lights");
            if (lights is not null && Log.IsDebugEnabled)
            {
                Log.Debug("lights: " + LogText.Figures(lights, "Frames"));
            }
        }

        PartnerAnalysis? partner = null;
        string? partnerError = null;
        if (options.Against is not null && darksFromPartner is null)
        {
            try
            {
                partner = MatchPartner(options.Against, darks, lights, threads, options.MaxFrames, Phase(T("Reading the partner", "Se citeste partenerul"), progress), cancel);
            }
            catch (Exception e) when (e is IOException or InvalidDataException or InvalidOperationException or ArgumentException or UnauthorizedAccessException)
            {
                Log.Warn(e, "the partner " + options.Against + " could not be used; the report says why");
                partnerError = e.Message;
            }

            GC.Collect();
            Released("the partner's stack was released");
            clock.Lap("partner");
            if (partner is not null && Log.IsDebugEnabled)
            {
                // The first error is the report's words; the partner's frames wrote their own lines.
                Log.Debug("partner: " + LogText.Figures(partner, "FirstError"));
            }
        }

        double lightsIndex = lights is null ? double.NaN : IndexAnalyzer.EquivalentIndex(index, lights.LightsOverDark);
        if (partnerSessionUsed is not null)
        {
            labelTag = IndexFigures.TagFor(lightsIndex);
            label = IndexAnalyzer.Label(inv.Session, labelTag);
            Log.Info("the session's label is its lights' (R-066): " + label);
        }

        DarkSeriesResult result = new(inv, darks, biases, biasError, dc, verdicts, refPlane, index, label, lightsIndex, lights, lightsError, partner, partnerError, darksFromPartner, partnerLabel, labelTag);
        clock.Done(Outcome(result));
        return result;
    }

    /// <summary>
    /// The run's outcome as its last line writes it: the six verdicts by their names, the index,
    /// the label, the lights' equivalent index, each group's frames decoded out of its files, and
    /// whether the run met an error (R-101).
    /// </summary>
    /// <param name="r">The run's result; not null.</param>
    /// <returns>The text.</returns>
    internal static string Outcome(DarkSeriesResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        DarkVerdicts v = r.Verdicts;
        return "leak=" + v.Leak.Verdict + " drift=" + v.Drift.Verdict + " noise=" + v.Noise.Verdict + " pattern=" + v.Pattern.Verdict
            + " deadSat=" + v.DeadSat.Verdict + " enough=" + v.Enough.Verdict
            + " index=" + LogText.Num(r.Index.Index) + " label=" + r.Label + " lightsIndex=" + LogText.Num(double.IsNaN(r.LightsIndex) ? null : r.LightsIndex)
            + " frames darks=" + LogText.Int(r.Darks.Used) + "/" + LogText.Int(r.Darks.Frames.Count)
            + " biases=" + (r.Biases is null ? "none" : LogText.Int(r.Biases.Used) + "/" + LogText.Int(r.Biases.Frames.Count))
            + " lights=" + (r.Lights is null ? "none" : LogText.Int(r.Lights.Used) + "/" + LogText.Int(r.Lights.Frames.Count))
            + " partner=" + (r.Partner is null ? "none" : LogText.Int(r.Partner.Used) + "/" + LogText.Int(r.Partner.Frames))
            + " errors=" + LogText.Yes(r.HasErrors);
    }

    /// <summary>
    /// Writes the process's working set after a group's frames were released and collected, at
    /// Debug, beside the per-frame figures of the decoding loops (ADR 0007's memory trap).
    /// </summary>
    /// <param name="what">What was released, in the log's words: <c>the biases were released</c>.</param>
    private static void Released(string what)
    {
        if (Log.IsDebugEnabled)
        {
            Log.Debug("working set " + LogText.WorkingSet() + " after " + what);
        }
    }

    /// <summary>The diagnostic log: the run's lines, the groups that fail, the figures behind every verdict (R-101).</summary>
    private static readonly Logger Log = LogManager.GetLogger(nameof(DarkSeriesRun));

    /// <summary>
    /// The partner folder (a session or a library) decoded to its median master: its index, the
    /// session's darks over it on the shared pixels, the lights over it through the darks, the
    /// overlap of the two hot-pixel sets at <see cref="Th.PartnerOverlapAdu"/>.
    /// </summary>
    /// <param name="folder">The partner folder.</param>
    /// <param name="own">The session's dark series, whose master and normalisation the partner is
    /// matched against.</param>
    /// <param name="lights">The session's lights, or null when there are none; their ratio is then
    /// not chained.</param>
    /// <param name="threads">How many frames are decoded in parallel.</param>
    /// <param name="maxFrames">How many of the partner's darks are held in memory at most.</param>
    /// <param name="progress">Receives the decoding counts; null for none.</param>
    /// <param name="cancel">Stops the decoding.</param>
    /// <returns>The partner's figures.</returns>
    /// <exception cref="InvalidOperationException">
    /// No partner dark could be decoded, or the partner's frames are not the size of the session's.
    /// </exception>
    private static PartnerAnalysis MatchPartner(string folder, DarkSeriesAnalysis own, LightsAnalysis? lights, int threads, int maxFrames, IProgress<(int Done, int Total)>? progress, CancellationToken cancel)
    {
        SessionFolder session = SessionFolder.Open(folder);
        FrameStack stack = FrameStack.Load(session.Darks, threads, maxFrames, progress, null, own.Normalisation, cancel);
        if (stack.Decoded.Count == 0)
        {
            throw new InvalidOperationException(T("none of the partner's darks could be read", "niciun dark al partenerului nu s-a putut citi") + FrameStack.FirstErrors(stack));
        }

        if (stack.Width != own.Width || stack.Height != own.Height)
        {
            throw new InvalidOperationException(T(
                $"the partner has {stack.Width} x {stack.Height} px, the session's darks {own.Width} x {own.Height}",
                $"partenerul are {stack.Width} x {stack.Height} px, dark-urile sesiunii {own.Width} x {own.Height}"));
        }

        float[] median = DarkSeriesAnalyzer.MedianMaster(stack, threads);
        FrameInfo first = stack.Decoded[0].Info;
        IndexFigures index = IndexAnalyzer.RankIndex(median, stack.Black, stack.White, stack.Norm, stack.Width, stack.Height);
        MatchFigures match = IndexAnalyzer.Match(IndexAnalyzer.SharedRatios(own.MedianMaster, own.Black, own.White, own.Norm, median, stack.Black, stack.White, stack.Norm), own.Normalisation);
        MatchFigures lightsOverPartner = lights is null ? MatchFigures.None : IndexAnalyzer.Chain(lights.LightsOverDark, match, own.Normalisation);

        // the overlap of the two sets at 50 ADU-eq (the partner's) and 20 (ours), as the hand
        // analysis read it
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

    /// <summary>
    /// Wraps the run's progress for one phase, so the decoders report plain counts.
    /// </summary>
    /// <param name="phase">The phase's name, in the run's language.</param>
    /// <param name="progress">The run's progress, or null.</param>
    /// <returns>The phase's progress, or null when the run has none.</returns>
    private static PhaseProgress? Phase(string phase, IProgress<RunProgress>? progress)
        => progress is null ? null : new PhaseProgress(phase, progress);

    /// <summary>
    /// Forwards a phase's counts synchronously, on the reporting thread (Progress&lt;T&gt; would
    /// post them to the thread pool and they could land after the report).
    /// </summary>
    /// <param name="phase">The phase's name every count is reported under.</param>
    /// <param name="target">The run's progress the counts go to.</param>
    private sealed class PhaseProgress(string phase, IProgress<RunProgress> target) : IProgress<(int Done, int Total)>
    {
        /// <summary>Reports the count under the phase's name.</summary>
        /// <param name="value">How many items are done out of how many.</param>
        public void Report((int Done, int Total) value) => target.Report(new RunProgress(phase, value.Done, value.Total));
    }
}
