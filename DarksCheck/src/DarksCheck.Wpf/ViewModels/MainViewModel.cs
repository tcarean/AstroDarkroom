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

using System.Globalization;
using System.Windows.Media.Imaging;
using DarksCheck.Core;

namespace DarksCheck.Wpf;

/// <summary>One labelled line of a section (R-071): the label, the whole value as the console prints it, the value without its verdict word, and the word, shown as a chip.</summary>
public sealed record RowVm(string Label, string Value, string Rest, string? Verdict)
{
    public bool HasVerdict => Verdict is not null;
}

/// <summary>One check of the report as a card: its title and its rows, the core's.</summary>
public sealed record SectionVm(string Title, IReadOnlyList<RowVm> Rows);

/// <summary>One dark of the frames table (R-071): the console's cells as they are; Skipped or Error set when the frame has no figures.</summary>
public sealed record FrameRowVm(int Index, string FileName, string? Skipped, string? Error, string Time, string Mean, string Sigma, string Residual, string Bands, string Amp, string Above)
{
    internal static FrameRowVm From(DarkTableRow r) => new(r.Index, r.FileName, r.Skipped, r.Error, r.Time, r.Mean, r.Sigma, r.Residual, r.Bands, r.Amp, r.Above);
}

/// <summary>One light of the lights table (R-071): the console's cells as they are.</summary>
public sealed record LightRowVm(int Index, string FileName, string? Skipped, string? Error, string Time, string Sky, string LocalSigma, string Candidates, string Isolated, string Amp, string Q25, string Q75)
{
    internal static LightRowVm From(LightTableRow r) => new(r.Index, r.FileName, r.Skipped, r.Error, r.Time, r.Sky, r.LocalSigma, r.Candidates, r.Isolated, r.Amp, r.Q25, r.Q75);
}

/// <summary>A binned map of the Harti tab: the title, the image (null when the map has no blocks or the group is absent), the report's row about it, and what the card says without an image.</summary>
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
    private CancellationTokenSource? _cts;
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

    /// <summary>The partner folder of `--against` (R-073), remembered between runs; null for none.</summary>
    public string? Partner
    {
        get => _partner;
        set
        {
            string? v = string.IsNullOrWhiteSpace(value) ? null : value;
            if (Set(ref _partner, v))
            {
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

    public string HotHeaderTip => WindowTexts.HotHeaderTip;

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

    /// <summary>The whole report, exactly as the console prints it; what the export writes.</summary>
    public string Report { get => _report; private set => Set(ref _report, value); }

    public bool HasResult => _result is not null;

    /// <summary>A line under the top bar: what was written, that the run was stopped, that the partner changed.</summary>
    public string? Status { get => _status; private set => Set(ref _status, value); }

    // ---- the settings (R-075), as typed; parsed with the invariant culture when a folder is opened

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

    /// <summary>How many frames are held in memory (and how many lights are measured); the core's cap when blank or not a positive integer.</summary>
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

    /// <summary>The gain per ISO stop; 2 = linear in ISO. Not a number or not positive = the default.</summary>
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

    /// <summary>The degrees that double the dark current; not a number or not positive = the default.</summary>
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
    public string Label { get; private set; } = "";
    public string Tag { get; private set; } = "";

    /// <summary>
    /// Opens a session or library folder and runs the whole analysis on a pool thread,
    /// reporting progress on the UI thread. A run in flight is cancelled first; a cancelled
    /// run leaves the window as it was. An error becomes the problem card, the tabs stay hidden.
    /// </summary>
    public async Task OpenAsync(string folder)
    {
        CancelPending();
        CancellationTokenSource cts = new();
        _cts = cts;
        Folder = folder;
        ProblemText = null;
        Status = null;
        Clear();
        IsRunning = true;
        Progress = 0;
        BusyMessage = WindowTexts.Busy("", 0, 0);
        DarkOptions options = Options;
        Progress<RunProgress> progress = new(p =>
        {
            // a run superseded by a later OpenAsync still posts a few callbacks: not over the new run's bar
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
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e) when (e is IOException or InvalidDataException or InvalidOperationException or ArgumentException or UnauthorizedAccessException or AggregateException)
        {
            // The analysis runs frames in parallel; a failure inside one lands here wrapped, as
            // in the console, and becomes the problem card, not an unobserved task exception.
            if (ReferenceEquals(_cts, cts))
            {
                ProblemText = e is AggregateException a ? string.Join("; ", a.Flatten().InnerExceptions.Select(x => x.Message).Distinct(StringComparer.Ordinal)) : e.Message;
            }
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

    /// <summary>Runs the current folder again, with the settings and the partner as they are now.</summary>
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

    /// <summary>Writes the report, the CSV files and the maps into <paramref name="dir"/> (R-074): the same files as `--out`, refused inside the session or the partner.</summary>
    public void Export(string dir)
    {
        if (_result is null)
        {
            return;
        }

        try
        {
            Status = WindowTexts.Written(DarkOutputs.WriteAll(_result, dir));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            Status = "EROARE: " + e.Message;
        }
    }

    /// <summary>The window copied the label: says so under the top bar.</summary>
    public void LabelCopied() => Status = WindowTexts.LabelCopied;

    private void SaveSettings() => UserSettings.Save(new StoredSettings(_threadsText, _maxFramesText, _isoFactorText, _doublingText, _partner));

    private static int? ParseInt(string s) => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) && v > 0 ? v : null;

    private static double? ParseDouble(string s) => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) && v > 0 ? v : null;

    private void Clear()
    {
        _result = null;
        Report = "";
        SessionRows = HotRows = LightRows = IndexRows = [];
        HeaderLine = Label = Tag = FrameTableNote = LightTableNote = "";
        Sections = [];
        Frames = [];
        Lights = [];
        Maps = [];
        WarmingPoints = ResidualPoints = BandingPoints = NoisePoints = NoiseExpected = LightPoints = [];
        WarmingGuides = ResidualGuides = BandingGuides = LightGuides = [];
        RaiseAll();
    }

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
        Frames = DarkReport.FrameRows(r).Select(FrameRowVm.From).ToList();
        Lights = DarkReport.LightRows(r).Select(LightRowVm.From).ToList();
        FrameTableNote = DarkReport.FrameTableNote(r);
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
            new MapVm("DARK - BIAS", r.DarkCurrent is null ? null : Map(r.DarkCurrent.Maps[rp]), Note(leak, "dark - bias"), WindowTexts.NoMap),
        ];

        // the charts: one point per dark, NaN where the frame has no figure
        HashSet<int> outliers = r.Verdicts.Noise.OutlierFrames.ToHashSet();
        List<ChartPoint> warming = [], residual = [], banding = [];
        foreach (DarkFrameFigures f in d.Frames)
        {
            string label = f.Info.Index.ToString(CultureInfo.InvariantCulture);
            bool ok = f.Error is null;
            warming.Add(new ChartPoint(f.Info.Index, ok ? f.AmpRatioMedian : double.NaN, label, VerdictPalette.Accent));
            residual.Add(new ChartPoint(f.Info.Index, ok ? f.Residuals[rp].Sigma : double.NaN, label, outliers.Contains(f.Info.Index) ? VerdictPalette.Warn : VerdictPalette.Good));
            banding.Add(new ChartPoint(f.Info.Index, ok ? f.Residuals[rp].RowBandingRatio : double.NaN, label, VerdictPalette.Neutral));
        }

        WarmingPoints = warming;
        WarmingGuides = [new GuideLine(1.0, "masterul (mediana seriei)", VerdictPalette.Neutral)];
        ResidualPoints = residual;
        ResidualGuides = double.IsNaN(r.Verdicts.Noise.OutlierLimitAdu) ? [] : [new GuideLine(r.Verdicts.Noise.OutlierLimitAdu, $"{RunReport.F(Th.NoiseOutlierFactor, 1)} x mediana", VerdictPalette.Warn)];
        BandingPoints = banding;
        BandingGuides = [new GuideLine(1.0, "zgomot alb", VerdictPalette.Neutral)];
        NoisePoints = d.Noise.Curve.Select(p => new ChartPoint(p.N, p.Measured, p.N.ToString(CultureInfo.InvariantCulture), VerdictPalette.Accent, $"n={p.N}: {RunReport.F(p.Measured, 3)} masurat, {RunReport.F(p.Expected, 3)} asteptat")).ToList();
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

    private static RowVm Row(ReportRow r) => new(r.Label, r.Value, r.Rest, r.Verdict);

    private static List<RowVm> Rows(IReadOnlyList<ReportSection> sections, string titleStart) =>
        sections.Where(s => s.Title.StartsWith(titleStart, StringComparison.Ordinal)).SelectMany(s => s.Rows).Select(Row).ToList();

    private static string Note(IReadOnlyList<ReportRow> rows, string label) => rows.FirstOrDefault(r => r.Label == label)?.Value ?? "";

    private static BitmapSource? Map(BinnedMap m) => DarkOutputs.MapRaster(m) is (byte[] grey, int w, int h) ? RasterBitmaps.Grey(grey, w, h) : null;

    private void RaiseAll()
    {
        foreach (string name in new[]
        {
            nameof(HasResult), nameof(SessionRows), nameof(HeaderLine), nameof(Sections), nameof(Frames), nameof(Lights), nameof(HasLights),
            nameof(FrameTableNote), nameof(LightTableNote), nameof(Maps), nameof(HotRows), nameof(WarmingPoints), nameof(WarmingGuides),
            nameof(ResidualPoints), nameof(ResidualGuides), nameof(BandingPoints), nameof(BandingGuides), nameof(NoisePoints), nameof(NoiseExpected),
            nameof(LightPoints), nameof(LightGuides), nameof(LightRows), nameof(IndexRows), nameof(Label), nameof(Tag),
        })
        {
            Raise(name);
        }
    }
}
