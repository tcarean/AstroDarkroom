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

using System.Diagnostics;
using System.Globalization;
using System.Windows.Media.Imaging;
using DarksCheck.Core;
using NLog;
using static DarksCheck.Core.Ui;

namespace DarksCheck.Wpf;

/// <summary>
/// One labelled line of a section (R-071): the label, the whole value as the console prints it,
/// the value without its verdict word, and the word, shown as a chip.
/// </summary>
/// <param name="Label">The row's label.</param>
/// <param name="Value">The whole value as the console prints it.</param>
/// <param name="Rest">The value without its leading verdict word; the whole value when it has
/// none.</param>
/// <param name="Verdict">The leading verdict word; null when the row carries none.</param>
/// <param name="Bold">The substrings of <paramref name="Rest"/> the window draws bold (ADR 0016);
/// empty for none.</param>
public sealed record RowVm(string Label, string Value, string Rest, string? Verdict, IReadOnlyList<string> Bold)
{
    public bool HasVerdict => Verdict is not null;
}

/// <summary>One check of the report as a card: its title and its rows, the core's.</summary>
/// <param name="Title">The section's title as the report prints it.</param>
/// <param name="Rows">The section's rows, in the report's order.</param>
public sealed record SectionVm(string Title, IReadOnlyList<RowVm> Rows);

/// <summary>
/// One dark of the frames table (R-071): the console's cells as they are; Skipped or Error set
/// when the frame has no figures; Off and Note mark a frame off the plateau of R-023, with the
/// core's reason.
/// </summary>
/// <param name="Index">The frame's number in the series, as the console shows it.</param>
/// <param name="FileName">The file's name.</param>
/// <param name="Skipped">Why the frame was not read; null when it was.</param>
/// <param name="Error">Why the frame could not be measured; null when it was.</param>
/// <param name="Time">The shot's time cell.</param>
/// <param name="Mean">The mean above the black cell.</param>
/// <param name="Sigma">The reference plane's sigma cell.</param>
/// <param name="Residual">The residual sigma cell.</param>
/// <param name="Bands">The random banding ratio cell.</param>
/// <param name="Amp">The hot pixels' amplitude ratio cell, against the master.</param>
/// <param name="Plateau">The degrees from the plateau of R-023 cell (`-` without a fit).</param>
/// <param name="Light">The frame's light excess cell (`-` when not measured).</param>
/// <param name="Side">The worst side's offset after the bias cell (`-` without a side).</param>
/// <param name="Above">The count of pixels above the hot level cell.</param>
/// <param name="Off">True when the core gave the frame a note: a frame off the plateau.</param>
/// <param name="Note">The core's note for a frame off the plateau; null for the others.</param>
public sealed record FrameRowVm(int Index, string FileName, string? Skipped, string? Error, string Time, string Mean, string Sigma, string Residual, string Bands, string Amp, string Plateau, string Light, string Side, string Above, bool Off, string? Note)
{
    /// <summary>
    /// The table row of the core as the window binds it; Off is set when the row has a note.
    /// </summary>
    /// <param name="r">The core's row of the darks table.</param>
    /// <returns>The row, its cells unchanged.</returns>
    internal static FrameRowVm From(DarkTableRow r) => new(r.Index, r.FileName, r.Skipped, r.Error, r.Time, r.Mean, r.Sigma, r.Residual, r.Bands, r.Amp, r.Plateau, r.Light, r.Side, r.Above, r.Note is not null, r.Note);
}

/// <summary>One light of the lights table (R-071): the console's cells as they are.</summary>
/// <param name="Index">The light's number, as the console shows it.</param>
/// <param name="FileName">The file's name.</param>
/// <param name="Skipped">Why the light was not read; null when it was.</param>
/// <param name="Error">Why the light could not be measured; null when it was.</param>
/// <param name="Time">The shot's time cell.</param>
/// <param name="Sky">The sky level cell.</param>
/// <param name="LocalSigma">The local sigma cell.</param>
/// <param name="Candidates">The hot-pixel candidates cell.</param>
/// <param name="Isolated">The isolated candidates cell (hot pixels, not stars).</param>
/// <param name="Amp">The amplitude ratio cell against the dark master.</param>
/// <param name="Q25">The ratio's first quartile cell.</param>
/// <param name="Q75">The ratio's third quartile cell.</param>
public sealed record LightRowVm(int Index, string FileName, string? Skipped, string? Error, string Time, string Sky, string LocalSigma, string Candidates, string Isolated, string Amp, string Q25, string Q75)
{
    /// <summary>The table row of the core as the window binds it.</summary>
    /// <param name="r">The core's row of the lights table.</param>
    /// <returns>The row, its cells unchanged.</returns>
    internal static LightRowVm From(LightTableRow r) => new(r.Index, r.FileName, r.Skipped, r.Error, r.Time, r.Sky, r.LocalSigma, r.Candidates, r.Isolated, r.Amp, r.Q25, r.Q75);
}

/// <summary>
/// A binned map of the Harti tab: the title, the image (null when the map has no blocks or the
/// group is absent), the report's row about it, and what the card says without an image.
/// </summary>
/// <param name="Title">The card's title.</param>
/// <param name="Image">The frozen grey map; null when the map has no blocks or the group is
/// absent.</param>
/// <param name="Note">The value of the report's row of the same name; empty when there is
/// none.</param>
/// <param name="EmptyText">What the card shows instead of a missing image.</param>
public sealed record MapVm(string Title, BitmapSource? Image, string Note, string EmptyText)
{
    public bool HasImage => Image is not null;
}

/// <summary>
/// The window's state (R-070 to R-076): the folder, the settings, the run with its progress
/// and cancellation, and the result shaped for the tabs. Every sentence and every figure comes
/// from the core's report rows and records; the view-model only arranges them.
/// </summary>
public sealed class MainViewModel : ObservableObject
{
    /// <summary>The diagnostic log: the runs, numbered, the partner, the export, the label (R-101).</summary>
    private static readonly Logger Log = LogManager.GetLogger(Loggers.Window);

    private CancellationTokenSource? _cts;

    // The opens of this view-model, counted from 1, so the log tells two runs of one folder apart.
    private int _runs;
    private string _folder = "";
    private string? _partner;
    private string? _problemText;
    private bool _running;
    private double _progress;
    private string _busyMessage = "";
    private string _report = "";
    private string? _status;
    private string _threadsText;
    private string _maxFramesText;
    private string _isoFactorText;
    private string _doublingText;
    private DarkSeriesResult? _result;

    /// <summary>
    /// The view-model before any folder: the four setting texts and the partner from the stored
    /// settings, the core's defaults for the ones never stored (the thread count stays empty,
    /// meaning the core's choice).
    /// </summary>
    public MainViewModel()
    {
        StoredSettings s = UserSettings.Load();
        _threadsText = s.Threads ?? "";
        _maxFramesText = s.MaxFrames ?? Th.MaxFramesInMemory.ToString(CultureInfo.InvariantCulture);
        _isoFactorText = s.IsoFactor ?? Normalisation.Default.IsoFactorPerStop.ToString("0.##", CultureInfo.InvariantCulture);
        _doublingText = s.Doubling ?? Normalisation.Default.DoublingCelsius.ToString("0.##", CultureInfo.InvariantCulture);
        _partner = string.IsNullOrWhiteSpace(s.Partner) ? null : s.Partner;
    }

    public string Banner => VersionInfo.Banner;

    public string Copyright => VersionInfo.Copyright;

    public string Folder
    {
        get => _folder;
        private set
        {
            if (Set(ref _folder, value))
            {
                Raise(nameof(HasFolder));
            }
        }
    }

    public bool HasFolder => !string.IsNullOrEmpty(_folder);

    /// <summary>
    /// The partner folder of `--against` (R-073), remembered between runs; null for none.
    /// </summary>
    public string? Partner
    {
        get => _partner;
        set
        {
            string? v = string.IsNullOrWhiteSpace(value) ? null : value;
            if (Set(ref _partner, v))
            {
                Log.Info("partner=" + LogText.Str(v));
                Raise(nameof(HasPartner));
                Raise(nameof(PartnerText));
                SaveSettings();
                if (HasResult)
                {
                    Status = WindowTexts.PartnerChanged;
                }
            }
        }
    }

    public bool HasPartner => _partner is not null;

    public string PartnerText => _partner ?? WindowTexts.NoPartner;

    public string? ProblemText
    {
        get => _problemText;
        private set
        {
            if (Set(ref _problemText, value))
            {
                Raise(nameof(HasProblem));
            }
        }
    }

    public bool HasProblem => !string.IsNullOrEmpty(_problemText);

    public string ProblemTitle => WindowTexts.ProblemTitle;

    public string EmptyTitle => WindowTexts.EmptyTitle;

    public string EmptyLead => WindowTexts.EmptyLead;

    public string IndexLead => WindowTexts.IndexLead;

    public string MapsLead => WindowTexts.MapsLead;

    public string LightsChartLead => WindowTexts.LightsChartLead;

    public string HotHeader => WindowTexts.HotHeader;

    public string HotHeaderTip { get; private set; } = "";

    public string NoiseHeaderTip { get; private set; } = "";

    public string PlateauHeaderTip => DarkReport.PlateauColumnTip;

    public string HotTabLead => WindowTexts.HotTabLead;

    public string CandidatesTip => WindowTexts.CandidatesTip;

    public string IsolatedTip => WindowTexts.IsolatedTip;

    public string MaxFramesTip => WindowTexts.MaxFramesTip;

    public string IsoFactorTip => WindowTexts.IsoFactorTip;

    public string DoublingTip => WindowTexts.DoublingTip;

    public bool IsRunning
    {
        get => _running;
        private set
        {
            if (Set(ref _running, value))
            {
                Raise(nameof(IsIdle));
            }
        }
    }

    public bool IsIdle => !_running;

    /// <summary>0..1 over the frames of the current phase.</summary>
    public double Progress { get => _progress; private set => Set(ref _progress, value); }

    public string BusyMessage { get => _busyMessage; private set => Set(ref _busyMessage, value); }

    /// <summary>
    /// The whole report, exactly as the console prints it; what the export writes.
    /// </summary>
    public string Report { get => _report; private set => Set(ref _report, value); }

    public bool HasResult => _result is not null;

    /// <summary>
    /// A line under the top bar: what was written, that the run was stopped, that the partner
    /// changed.
    /// </summary>
    public string? Status { get => _status; private set => Set(ref _status, value); }

    // ---- the settings (R-075), as typed; parsed with the invariant culture when a folder is
    // opened

    /// <summary>How many frames decode in parallel; empty = half the cores.</summary>
    public string ThreadsText
    {
        get => _threadsText;
        set
        {
            if (Set(ref _threadsText, value))
            {
                SaveSettings();
            }
        }
    }

    /// <summary>
    /// How many frames are held in memory (and how many lights are measured); the core's cap when
    /// blank or not a positive integer.
    /// </summary>
    public string MaxFramesText
    {
        get => _maxFramesText;
        set
        {
            if (Set(ref _maxFramesText, value))
            {
                Raise(nameof(MaxFramesIsValid));
                SaveSettings();
            }
        }
    }

    /// <summary>
    /// The gain per ISO stop; 2 = linear in ISO. Not a number or not positive = the default.
    /// </summary>
    public string IsoFactorText
    {
        get => _isoFactorText;
        set
        {
            if (Set(ref _isoFactorText, value))
            {
                Raise(nameof(IsoFactorIsValid));
                SaveSettings();
            }
        }
    }

    /// <summary>
    /// The degrees that double the dark current; not a number or not positive = the default.
    /// </summary>
    public string DoublingText
    {
        get => _doublingText;
        set
        {
            if (Set(ref _doublingText, value))
            {
                Raise(nameof(DoublingIsValid));
                SaveSettings();
            }
        }
    }

    public bool MaxFramesIsValid => ParseInt(_maxFramesText) is not null;

    public bool IsoFactorIsValid => ParseDouble(_isoFactorText) is not null;

    public bool DoublingIsValid => ParseDouble(_doublingText) is not null;

    internal DarkOptions Options => new(
        ParseInt(_threadsText) ?? 0,
        ParseInt(_maxFramesText) ?? Th.MaxFramesInMemory,
        _partner,
        ParseDouble(_isoFactorText) ?? Normalisation.Default.IsoFactorPerStop,
        ParseDouble(_doublingText) ?? Normalisation.Default.DoublingCelsius);

    internal DarkSeriesResult? Result => _result;

    // ---- the tabs

    public IReadOnlyList<RowVm> SessionRows { get; private set; } = [];
    public string HeaderLine { get; private set; } = "";
    public IReadOnlyList<SectionVm> Sections { get; private set; } = [];
    public IReadOnlyList<FrameRowVm> Frames { get; private set; } = [];
    public IReadOnlyList<LightRowVm> Lights { get; private set; } = [];
    public bool HasLights => Lights.Count > 0;
    public string FrameTableNote { get; private set; } = "";
    public string LightTableNote { get; private set; } = "";
    public IReadOnlyList<MapVm> Maps { get; private set; } = [];
    public IReadOnlyList<RowVm> HotRows { get; private set; } = [];
    public IReadOnlyList<ChartPoint> WarmingPoints { get; private set; } = [];
    public IReadOnlyList<GuideLine> WarmingGuides { get; private set; } = [];

    /// <summary>
    /// The core's sentence under the warming chart: what the points are, the plateau and the band
    /// of the frames to discard (R-071).
    /// </summary>
    public string WarmingNote { get; private set; } = "";
    public IReadOnlyList<ChartPoint> ResidualPoints { get; private set; } = [];
    public IReadOnlyList<GuideLine> ResidualGuides { get; private set; } = [];
    public IReadOnlyList<ChartPoint> BandingPoints { get; private set; } = [];
    public IReadOnlyList<GuideLine> BandingGuides { get; private set; } = [];
    public IReadOnlyList<ChartPoint> NoisePoints { get; private set; } = [];
    public IReadOnlyList<ChartPoint> NoiseExpected { get; private set; } = [];
    public IReadOnlyList<ChartPoint> LightPoints { get; private set; } = [];
    public IReadOnlyList<GuideLine> LightGuides { get; private set; } = [];
    public IReadOnlyList<RowVm> LightRows { get; private set; } = [];
    public IReadOnlyList<RowVm> IndexRows { get; private set; } = [];

    /// <summary>
    /// The scale rows and paragraphs of R-080, for the tab of R-078; the title is the core's.
    /// </summary>
    public IReadOnlyList<RowVm> ScaleRows { get; private set; } = [];
    public IReadOnlyList<string> ScaleParagraphs { get; private set; } = [];
    public string ScaleTitle => ScaleReport.Title;
    public string Label { get; private set; } = "";
    public string Tag { get; private set; } = "";

    /// <summary>
    /// Opens a session or library folder and runs the whole analysis on a pool thread, reporting
    /// progress on the UI thread. A run in flight is cancelled first and the previous result is
    /// cleared; a cancelled run leaves no result, and the status line says it was stopped. An error
    /// the core throws for a bad folder becomes the problem card and the tabs stay hidden; any
    /// other exception propagates. The diagnostic log gets the run's number, folder and options,
    /// and its outcome: done, stopped, failed with the exception, or replaced by a newer run, and an
    /// exception that propagates with its stack before it leaves (R-101).
    /// </summary>
    /// <param name="folder">The session or library folder; not null.</param>
    public async Task OpenAsync(string folder)
    {
        CancelPending();
        CancellationTokenSource cts = new();
        _cts = cts;
        int run = ++_runs;
        Folder = folder;
        ProblemText = null;
        Status = null;
        Clear();
        IsRunning = true;
        Progress = 0;
        BusyMessage = WindowTexts.Busy("", 0, 0);
        DarkOptions options = Options;
        Stopwatch clock = Stopwatch.StartNew();
        Log.Info("run " + LogText.Int(run) + " started: folder=" + folder + " threads=" + (options.Threads > 0 ? LogText.Int(options.Threads) : "auto")
            + " maxFrames=" + LogText.Int(options.MaxFrames) + " isoFactor=" + LogText.Num(options.IsoFactorPerStop)
            + " doubling=" + LogText.Num(options.DoublingCelsius) + " against=" + LogText.Str(options.Against));
        Progress<RunProgress> progress = new(p =>
        {
            // a run superseded by a later OpenAsync still posts a few callbacks: not over the new
            // run's bar
            if (cts.IsCancellationRequested)
            {
                return;
            }

            Progress = p.Total > 0 ? p.Done / (double)p.Total : 0;
            BusyMessage = WindowTexts.Busy(p.Phase, p.Done, p.Total);
        });
        try
        {
            DarkSeriesResult r = await Task.Run(() => DarkSeriesRun.Run(folder, options, progress, cts.Token), cts.Token).ConfigureAwait(true);
            if (!cts.IsCancellationRequested)
            {
                Populate(r);
                Log.Info("run " + LogText.Int(run) + " done in " + LogText.Seconds(clock.Elapsed) + "; the tabs show the result");
            }
            else
            {
                LogStopped(run, cts, clock);
            }
        }
        catch (OperationCanceledException)
        {
            // A stop the user asked for, another folder or the window closing: a line, no exception.
            LogStopped(run, cts, clock);
        }
        catch (Exception e) when (e is IOException or InvalidDataException or InvalidOperationException or ArgumentException or UnauthorizedAccessException or AggregateException)
        {
            // The analysis runs frames in parallel; a failure inside one lands here wrapped, as
            // in the console, and becomes the problem card, not an unobserved task exception.
            if (ReferenceEquals(_cts, cts))
            {
                ProblemText = e is AggregateException a ? string.Join("; ", a.Flatten().InnerExceptions.Select(x => x.Message).Distinct(StringComparer.Ordinal)) : e.Message;
                Log.Error(e, "run " + LogText.Int(run) + " failed after " + LogText.Seconds(clock.Elapsed) + "; the problem card shows the message");
            }
            else
            {
                Log.Debug(e, "run " + LogText.Int(run) + " failed after a newer run started; nothing is shown");
            }
        }
        catch (Exception e) when (LogUnhandled(run, e))
        {
            // Never entered: the filter writes the exception and returns false, so it leaves this
            // method in the returned task as it always did.
        }
        finally
        {
            // Only the run still current touches the window: a run superseded by a later
            // OpenAsync must not write its "stopped" line over the new run's state.
            if (ReferenceEquals(_cts, cts))
            {
                if (cts.IsCancellationRequested)
                {
                    Status = WindowTexts.Cancelled;
                }

                IsRunning = false;
                _cts = null;
            }

            cts.Dispose();
        }
    }

    /// <summary>
    /// Writes the line of a run that was stopped: at Info when it is still the current run (the
    /// user pressed Stop, or the window is closing), at Debug when a newer run replaced it. A
    /// cancellation the user asked for is no exception of the log's (R-101).
    /// </summary>
    /// <param name="run">The run's number.</param>
    /// <param name="cts">The run's own cancellation source.</param>
    /// <param name="clock">Runs from the run's start.</param>
    private void LogStopped(int run, CancellationTokenSource cts, Stopwatch clock)
    {
        if (ReferenceEquals(_cts, cts))
        {
            Log.Info("run " + LogText.Int(run) + " stopped after " + LogText.Seconds(clock.Elapsed));
        }
        else
        {
            Log.Debug("run " + LogText.Int(run) + " stopped after " + LogText.Seconds(clock.Elapsed) + " by a newer run; nothing is shown");
        }
    }

    /// <summary>
    /// Writes, at Error with its stack, an exception the window does not handle, while it leaves
    /// <see cref="OpenAsync"/> in a task nobody awaits, where it would otherwise surface, if ever,
    /// only when the task is collected (R-101). Used as an exception filter, it changes nothing: it
    /// returns false.
    /// </summary>
    /// <param name="run">The run's number.</param>
    /// <param name="e">The exception; not null.</param>
    /// <returns>False, always.</returns>
    internal static bool LogUnhandled(int run, Exception e)
    {
        Log.Error(e, "run " + LogText.Int(run) + " failed with an exception the window does not handle; it shows no result");

        // Its task is never awaited: the hook that meets it when the task is collected names it
        // instead of writing the stack a second time.
        LogSetup.Wrote(e);
        return false;
    }

    /// <summary>
    /// Runs the current folder again, with the settings and the partner as they are now.
    /// </summary>
    public Task RerunAsync() => HasFolder ? OpenAsync(_folder) : Task.CompletedTask;

    /// <summary>Stops the run in flight, if any.</summary>
    public void CancelPending()
    {
        try
        {
            _cts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    /// <summary>
    /// Writes the report, the CSV files and the maps into <paramref name="dir"/> (R-074): the same
    /// files as `--out`, refused inside the session or the partner. The written paths or the error
    /// go to the status line; nothing happens before a result.
    /// </summary>
    /// <param name="dir">The folder to write into.</param>
    public void Export(string dir)
    {
        if (_result is null)
        {
            return;
        }

        try
        {
            // Each file's line as soon as the file is written, so an export that fails part-way
            // still names what it wrote (R-101).
            Status = WindowTexts.Written(DarkOutputs.WriteAll(_result, dir, p => Log.Info(LogText.Wrote(p))));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            Log.Warn(e, "the export into " + dir + " failed; the status line shows the message");
            Status = T("ERROR: ", "EROARE: ") + e.Message;
        }
    }

    /// <summary>The window copied the label: says so under the top bar, and in the log.</summary>
    public void LabelCopied()
    {
        Log.Info("label copied: " + Label);
        Status = WindowTexts.LabelCopied;
    }

    /// <summary>Stores the four setting texts as typed and the partner.</summary>
    private void SaveSettings() => UserSettings.Save(new StoredSettings(_threadsText, _maxFramesText, _isoFactorText, _doublingText, _partner));

    /// <summary>A setting text as a positive whole number, with the invariant culture.</summary>
    /// <param name="s">The text as typed.</param>
    /// <returns>The number when it is a positive integer; null otherwise, so the core's default is
    /// used.</returns>
    private static int? ParseInt(string s) => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) && v > 0 ? v : null;

    /// <summary>A setting text as a positive number, with the invariant culture.</summary>
    /// <param name="s">The text as typed.</param>
    /// <returns>The number when it parses and is above 0; null otherwise, so the core's default is
    /// used.</returns>
    private static double? ParseDouble(string s) => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) && v > 0 ? v : null;

    /// <summary>
    /// Forgets the result: every tab's rows, tables, maps and charts empty, and the change raised.
    /// </summary>
    private void Clear()
    {
        _result = null;
        Report = "";
        SessionRows = HotRows = LightRows = IndexRows = ScaleRows = [];
        ScaleParagraphs = [];
        HeaderLine = Label = Tag = FrameTableNote = LightTableNote = WarmingNote = NoiseHeaderTip = HotHeaderTip = "";
        Sections = [];
        Frames = [];
        Lights = [];
        Maps = [];
        WarmingPoints = ResidualPoints = BandingPoints = NoisePoints = NoiseExpected = LightPoints = [];
        WarmingGuides = ResidualGuides = BandingGuides = LightGuides = [];
        RaiseAll();
    }

    /// <summary>
    /// Shapes a finished run for the tabs: the report text, the cards, the tables, the maps and the
    /// charts, all from the core's rows and figures; nothing is computed here.
    /// </summary>
    /// <param name="r">The run's result; not null.</param>
    internal void Populate(DarkSeriesResult r)
    {
        _result = r;
        using (StringWriter sw = new())
        {
            RunReport.WriteBanner(sw);
            DarkReport.Write(r, sw);
            Report = sw.ToString();
        }

        SessionRows = InventoryReport.HeaderRows(r.Inventory).Concat(DarkReport.HeaderRows(r)).Select(Row).ToList();

        HeaderLine = DarkReport.HeaderLine(r);
        IReadOnlyList<ReportSection> sections = DarkReport.Sections(r);
        Sections = sections.Select(s => new SectionVm(s.Title, s.Rows.Select(Row).ToList())).ToList();
        HotRows = Rows(sections, "5.");
        LightRows = Rows(sections, "9.");
        IndexRows = Rows(sections, "INDEX");
        ScaleRows = ScaleReport.Rows(r).Select(Row).ToList();
        ScaleParagraphs = ScaleReport.Paragraphs(r);
        Frames = DarkReport.FrameRows(r).Select(FrameRowVm.From).ToList();
        Lights = DarkReport.LightRows(r).Select(LightRowVm.From).ToList();
        FrameTableNote = DarkReport.FrameTableNote(r);
        NoiseHeaderTip = DarkReport.NoiseColumnTip(r);
        HotHeaderTip = DarkReport.HotColumnTip(r);
        LightTableNote = r.Lights is null ? "" : DarkReport.LightTableNote(r);
        Label = r.Label;
        Tag = r.Index.Tag;

        DarkSeriesAnalysis d = r.Darks;
        int rp = r.ReferencePlane;
        IReadOnlyList<ReportRow> leak = sections[0].Rows;
        Maps =
        [
            new MapVm("MASTER DARK", Map(d.MeanMaps[rp]), Note(leak, "master dark"), WindowTexts.NoMap),
            new MapVm("MASTER BIAS", r.Biases is null ? null : Map(r.Biases.MeanMaps[rp]), Note(leak, "master bias"), WindowTexts.NoMap),
            new MapVm("DARK MINUS BIAS", r.DarkCurrent is null ? null : Map(r.DarkCurrent.Maps[rp]), Note(leak, "dark - bias"), WindowTexts.NoMap),
        ];

        // the charts: one point per dark, NaN where the frame has no figure
        HashSet<int> outliers = r.Verdicts.Noise.OutlierFrames.ToHashSet();
        HashSet<int> off = d.Warming.OffPlateauFrames.ToHashSet();
        List<ChartPoint> warming = [], residual = [], banding = [];
        foreach (DarkFrameFigures f in d.Frames)
        {
            string label = f.Info.Index.ToString(CultureInfo.InvariantCulture);
            bool ok = f.Error is null;
            warming.Add(new ChartPoint(f.Info.Index, ok ? f.AmpRatioMedian : double.NaN, label, off.Contains(f.Info.Index) ? VerdictPalette.Warn : VerdictPalette.Accent));
            residual.Add(new ChartPoint(f.Info.Index, ok ? f.Residuals[rp].Sigma : double.NaN, label, outliers.Contains(f.Info.Index) ? VerdictPalette.Warn : VerdictPalette.Good));
            banding.Add(new ChartPoint(f.Info.Index, ok ? f.Residuals[rp].RowBandingRatio : double.NaN, label, VerdictPalette.Neutral));
        }

        WarmingPoints = warming;
        Warming wm = d.Warming;
        GuideLine master = new(1.0, T("the master", "masterul"), VerdictPalette.Neutral);
        WarmingGuides = wm.HasFit
            ?
            [
                master,
                new GuideLine(wm.Plateau, T($"plateau {RunReport.F(wm.Plateau, 2)}", $"platoul {RunReport.F(wm.Plateau, 2)}"), VerdictPalette.Neutral),
                new GuideLine(wm.OffPlateauLow, T($"{RunReport.F(wm.OffPlateauLow, 2)} discard", $"{RunReport.F(wm.OffPlateauLow, 2)} de aruncat"), VerdictPalette.Warn),
                new GuideLine(wm.OffPlateauHigh, T($"{RunReport.F(wm.OffPlateauHigh, 2)} discard", $"{RunReport.F(wm.OffPlateauHigh, 2)} de aruncat"), VerdictPalette.Warn),
            ]
            : [master];
        WarmingNote = DarkReport.WarmingChartNote(r);
        ResidualPoints = residual;
        ResidualGuides = double.IsNaN(r.Verdicts.Noise.OutlierLimitAdu) ? [] : [new GuideLine(r.Verdicts.Noise.OutlierLimitAdu, T($"{RunReport.F(Th.NoiseOutlierFactor, 1)} times the usual noise", $"de {RunReport.F(Th.NoiseOutlierFactor, 1)} ori zgomotul obisnuit"), VerdictPalette.Warn)];
        BandingPoints = banding;
        BandingGuides = [new GuideLine(1.0, T("no banding", "fara benzi"), VerdictPalette.Neutral)];
        NoisePoints = d.Noise.Curve.Select(p => new ChartPoint(p.N, p.Measured, p.N.ToString(CultureInfo.InvariantCulture), VerdictPalette.Accent, T($"n={p.N}: {RunReport.F(p.Measured, 3)} measured, {RunReport.F(p.Expected, 3)} expected", $"n={p.N}: {RunReport.F(p.Measured, 3)} masurat, {RunReport.F(p.Expected, 3)} asteptat"))).ToList();
        NoiseExpected = d.Noise.Curve.Select(p => new ChartPoint(p.N, p.Expected, p.N.ToString(CultureInfo.InvariantCulture), VerdictPalette.Neutral)).ToList();

        if (r.Lights is LightsAnalysis l)
        {
            LightPoints = l.Frames.Select(f => new ChartPoint(f.Info.Index, f.Error is null ? f.AmpRatioMedian : double.NaN, f.Info.Index.ToString(CultureInfo.InvariantCulture), VerdictPalette.For(IndexAnalyzer.Judge(f.AmpRatioMedian)))).ToList();
            LightGuides =
            [
                new GuideLine(Th.MatchDirectLow, $"{RunReport.F(Th.MatchDirectLow, 1)} direct", VerdictPalette.Good),
                new GuideLine(Th.MatchDirectHigh, $"{RunReport.F(Th.MatchDirectHigh, 1)} direct", VerdictPalette.Good),
                new GuideLine(Th.MatchOptLow, $"{RunReport.F(Th.MatchOptLow, 1)} -opt", VerdictPalette.Warn),
                new GuideLine(Th.MatchOptHigh, $"{RunReport.F(Th.MatchOptHigh, 1)} -opt", VerdictPalette.Warn),
                new GuideLine(Th.MatchCosmeticLow, $"{RunReport.F(Th.MatchCosmeticLow, 1)} cosmetic", VerdictPalette.Bad),
            ];
        }

        RaiseAll();
    }

    /// <summary>A report row of the core as the window binds it.</summary>
    /// <param name="r">The core's row.</param>
    /// <returns>The row with its label, value, rest, verdict and bold parts.</returns>
    private static RowVm Row(ReportRow r) => new(r.Label, r.Value, r.Rest, r.Verdict, r.BoldParts);

    /// <summary>
    /// The rows of every section whose title starts with the given text, in order.
    /// </summary>
    /// <param name="sections">The report's sections.</param>
    /// <param name="titleStart">The start of the title, compared ordinally ("5.", "9.",
    /// "INDEX").</param>
    /// <returns>The rows as the window binds them; empty when no section matches.</returns>
    private static List<RowVm> Rows(IReadOnlyList<ReportSection> sections, string titleStart) =>
        sections.Where(s => s.Title.StartsWith(titleStart, StringComparison.Ordinal)).SelectMany(s => s.Rows).Select(Row).ToList();

    /// <summary>The value of the first row with the given label.</summary>
    /// <param name="rows">The rows to search.</param>
    /// <param name="label">The label, compared exactly.</param>
    /// <returns>The row's value; empty when no row has that label.</returns>
    private static string Note(IReadOnlyList<ReportRow> rows, string label) => rows.FirstOrDefault(r => r.Label == label)?.Value ?? "";

    /// <summary>A binned map as the grey bitmap the PNG of the export holds.</summary>
    /// <param name="m">The map; not null.</param>
    /// <returns>The frozen bitmap; null for a map without blocks.</returns>
    private static BitmapSource? Map(BinnedMap m) => DarkOutputs.MapRaster(m) is (byte[] grey, int w, int h) ? RasterBitmaps.Grey(grey, w, h) : null;

    /// <summary>Raises the change of every property that depends on the result.</summary>
    private void RaiseAll()
    {
        foreach (string name in new[]
        {
            nameof(HasResult), nameof(SessionRows), nameof(HeaderLine), nameof(Sections), nameof(Frames), nameof(Lights), nameof(HasLights),
            nameof(FrameTableNote), nameof(NoiseHeaderTip), nameof(HotHeaderTip), nameof(LightTableNote), nameof(Maps), nameof(HotRows), nameof(WarmingPoints), nameof(WarmingGuides), nameof(WarmingNote),
            nameof(ResidualPoints), nameof(ResidualGuides), nameof(BandingPoints), nameof(BandingGuides), nameof(NoisePoints), nameof(NoiseExpected),
            nameof(LightPoints), nameof(LightGuides), nameof(LightRows), nameof(IndexRows), nameof(ScaleRows), nameof(ScaleParagraphs), nameof(Label), nameof(Tag),
        })
        {
            Raise(name);
        }
    }
}
