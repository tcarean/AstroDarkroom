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
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LightsCheck.Core;

namespace LightsCheck.Wpf;

/// <summary>
/// One paragraph of an explanation the core words: the Respinse tab's (R-037) or the Dithering
/// card's (R-047); the window sets the emphasised one in bold.
/// </summary>
/// <param name="Text">The paragraph, as the core wrote it (Romanian).</param>
/// <param name="Emphasis">True for the paragraph the window sets in bold.</param>
public sealed record ParagraphVm(string Text, bool Emphasis);

/// <summary>A table of the Dithering tab, the core's cells as they are (R-037).</summary>
/// <param name="Title">The table's title line.</param>
/// <param name="Columns">The header cells, one per column.</param>
/// <param name="Rows">
/// The rows, each a list of cells in the order of <paramref name="Columns"/>.
/// </param>
public sealed record TableVm(string Title, IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<string>> Rows);

/// <summary>
/// One labelled line of a section, with the verdict chip when the line opens with one.
/// </summary>
/// <param name="Label">The row's label, as the console prints it before the colon.</param>
/// <param name="Value">The row's value, as the console prints it after the colon.</param>
/// <param name="Verdict">
/// The verdict word the row opens with (shown as a chip), or null for a row without one.
/// </param>
public sealed record RowVm(string Label, string Value, string? Verdict)
{
    public bool HasVerdict => Verdict is not null;

    /// <summary>
    /// The theme's MetricRowTemplate binds these too; a report row has no note and no key row.
    /// </summary>
    public string? Note => null;

    public bool HasNote => false;

    public bool IsKey => false;
}

/// <summary>
/// One frame of the table: the core's formatted cells, plus the path to decode for the cut-outs
/// (null when the frame was not measured).
/// </summary>
public sealed class FrameRowVm
{
    /// <summary>Copies the core's cells of one frame and keeps the path for the cut-outs.</summary>
    /// <param name="row">
    /// The frame's row of <see cref="LightsCheck.Core.SeriesReport.FrameRows"/>.
    /// </param>
    /// <param name="path">
    /// The raw to decode for the cut-outs; null when the frame was not measured.
    /// </param>
    internal FrameRowVm(FrameTableRow row, string? path)
    {
        Index = row.Index;
        File = row.FileName;
        Time = row.Time;
        Class = row.Class;
        Elongation = row.Elongation;
        Fwhm = row.Fwhm;
        Stars = row.Stars;
        Background = row.Background;
        Pause = row.Pause;
        Step = row.Step;
        Dx = row.Dx;
        Dy = row.Dy;
        Move = row.Move;
        Spot = row.Spot;
        Reasons = row.Reasons;
        Path = path;
    }

    public int Index { get; }
    public string File { get; }
    public string Time { get; }
    public string Class { get; }
    public string Elongation { get; }
    public string Fwhm { get; }
    public string Stars { get; }
    public string Background { get; }
    public string Pause { get; }
    public string Step { get; }
    public string Dx { get; }
    public string Dy { get; }
    public string Move { get; }
    public string Spot { get; }
    public string Reasons { get; }
    public string? Path { get; }
}

/// <summary>A star cut-out for the selected frame.</summary>
/// <param name="Caption">The caption the core writes for the cut-out (position and shape).</param>
/// <param name="Image">The cut-out as a frozen grey bitmap, usable from any thread.</param>
public sealed record CropVm(string Caption, BitmapSource Image);

/// <summary>
/// The window's state (R-029 to R-032): the folder, the inputs, the run with its progress and
/// cancellation, and the result shaped for the tabs. Every figure and every sentence comes from
/// the core; the view-model arranges them, and writes only a few labels of its own (the guide
/// lines', the session card's "scara", the "EROARE: " before an error).
/// </summary>
public sealed class MainViewModel : ObservableObject
{
    private CancellationTokenSource? _cts;
    private string _folder = "";
    private string? _problemText;
    private bool _running;
    private double _progress;
    private string _busyMessage = "";
    private string _report = "";
    private string _pixelText = SeriesOptions.D3300PixelUm.ToString("F2", CultureInfo.InvariantCulture);
    private string _threadsText = "";
    private string _eastArrow;
    private string _northArrow;

    /// <summary>
    /// An empty window state, with the SynScan arrows read from <see cref="UserSettings"/>; reads
    /// the settings file, nothing else.
    /// </summary>
    public MainViewModel()
    {
        // The stored strings count only when they are one of the two arrows of their axis; the
        // core's default otherwise.
        (string? east, string? north) = UserSettings.Load();
        _eastArrow = ArrowKeys.FromSymbol(east) is Arrow.Left or Arrow.Right ? east! : ArrowKeys.Symbol(ArrowKeys.Default.East);
        _northArrow = ArrowKeys.FromSymbol(north) is Arrow.Up or Arrow.Down ? north! : ArrowKeys.Symbol(ArrowKeys.Default.North);
    }
    private string? _status;
    private FrameRowVm? _selectedFrame;
    private IReadOnlyList<CropVm> _crops = [];
    private string? _cropsNote;
    private SeriesResult? _result;

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

    /// <summary>0..1 over the frames measured.</summary>
    public double Progress { get => _progress; private set => Set(ref _progress, value); }

    public string BusyMessage { get => _busyMessage; private set => Set(ref _busyMessage, value); }

    /// <summary>
    /// The whole report, exactly as the console prints it; what the export writes.
    /// </summary>
    public string Report { get => _report; private set => Set(ref _report, value); }

    public bool HasResult => _result is not null;

    /// <summary>A line under the top bar: what was written, or that the run was stopped.</summary>
    public string? Status { get => _status; private set => Set(ref _status, value); }

    /// <summary>
    /// The pixel size in microns, as typed; the D3300's by default, used when the files do not
    /// say it.
    /// </summary>
    public string PixelText
    {
        get => _pixelText;
        set
        {
            if (Set(ref _pixelText, value))
            {
                Raise(nameof(PixelIsValid));
            }
        }
    }

    public bool PixelIsValid => double.TryParse(_pixelText, NumberStyles.Float, CultureInfo.InvariantCulture, out double p) && SeriesOptions.PixelSizeIsValid(p);

    /// <summary>How many frames in parallel, as typed; empty = half the cores.</summary>
    public string ThreadsText { get => _threadsText; set => Set(ref _threadsText, value); }

    /// <summary>
    /// The SynScan arrows that move the mount east and north, as symbols; the other two follow
    /// (R-041). Remembered in <see cref="UserSettings"/>.
    /// </summary>
    public IReadOnlyList<string> EastChoices { get; } = [ArrowKeys.Symbol(ArrowKeys.Default.East), ArrowKeys.Symbol(ArrowKeys.Default.West)];

    public IReadOnlyList<string> NorthChoices { get; } = [ArrowKeys.Symbol(ArrowKeys.Default.North), ArrowKeys.Symbol(ArrowKeys.Default.South)];

    public string EastArrow
    {
        get => _eastArrow;
        set
        {
            if (Set(ref _eastArrow, value))
            {
                UserSettings.Save(_eastArrow, _northArrow);
            }
        }
    }

    public string NorthArrow
    {
        get => _northArrow;
        set
        {
            if (Set(ref _northArrow, value))
            {
                UserSettings.Save(_eastArrow, _northArrow);
            }
        }
    }

    internal SeriesOptions Options => new(
        PixelIsValid ? double.Parse(_pixelText, NumberStyles.Float, CultureInfo.InvariantCulture) : SeriesOptions.D3300PixelUm,
        int.TryParse(_threadsText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int t) && t > 0 ? t : 0,
        new ArrowKeys(ArrowKeys.FromSymbol(_eastArrow) ?? ArrowKeys.Default.East, ArrowKeys.FromSymbol(_northArrow) ?? ArrowKeys.Default.North));

    internal SeriesResult? Result => _result;

    // ---- the tabs

    public IReadOnlyList<RowVm> SessionRows { get; private set; } = [];
    public IReadOnlyList<RowVm> SummaryRows { get; private set; } = [];
    public IReadOnlyList<RowVm> FocusRows { get; private set; } = [];
    public IReadOnlyList<RowVm> NoiseRows { get; private set; } = [];
    public IReadOnlyList<RowVm> DewRows { get; private set; } = [];
    public IReadOnlyList<RowVm> TrailRows { get; private set; } = [];
    public IReadOnlyList<RowVm> DriftRows { get; private set; } = [];
    public IReadOnlyList<RowVm> DitherRows { get; private set; } = [];
    public IReadOnlyList<FrameRowVm> Frames { get; private set; } = [];
    public IReadOnlyList<FrameRowVm> Rejected { get; private set; } = [];
    public IReadOnlyList<ChartPoint> MapPoints { get; private set; } = [];
    public IReadOnlyList<ChartPoint> StepPoints { get; private set; } = [];
    public IReadOnlyList<ChartPoint> NearestPoints { get; private set; } = [];
    public IReadOnlyList<GuideLine> StepGuides { get; private set; } = [];
    public IReadOnlyList<ChartPoint> FwhmPoints { get; private set; } = [];
    public IReadOnlyList<ChartPoint> ElongationPoints { get; private set; } = [];
    public IReadOnlyList<GuideLine> ElongationGuides { get; private set; } = [];
    public IReadOnlyList<ChartPoint> StarPoints { get; private set; } = [];
    public IReadOnlyList<ChartPoint> BackgroundPoints { get; private set; } = [];
    public IReadOnlyList<TableVm> DitherTables { get; private set; } = [];
    public string MapXLabel { get; private set; } = "X";
    public string MapYLabel { get; private set; } = "Y";
    public string DxHeader { get; private set; } = "dX";
    public string DyHeader { get; private set; } = "dY";
    public string RejectedHeading { get; private set; } = "";
    public IReadOnlyList<ParagraphVm> RejectedExplanation { get; } = SeriesReport.RejectedExplanation.Select(p => new ParagraphVm(p.Text, p.Emphasis)).ToList();

    /// <summary>
    /// What the Dithering card says under its rows and tables: the core's paragraphs, one bold
    /// (R-047).
    /// </summary>
    public IReadOnlyList<ParagraphVm> DitherExplanation { get; } = SeriesReport.DitherExplanation.Select(p => new ParagraphVm(p.Text, p.Emphasis)).ToList();

    public FrameRowVm? SelectedFrame
    {
        get => _selectedFrame;
        set
        {
            if (Set(ref _selectedFrame, value))
            {
                CropsPending = LoadCropsAsync(value);
            }
        }
    }

    /// <summary>
    /// The cut-outs of the selected frame being decoded; the tests await it, the window just binds
    /// Crops.
    /// </summary>
    internal Task CropsPending { get; private set; } = Task.CompletedTask;

    public IReadOnlyList<CropVm> Crops { get => _crops; private set => Set(ref _crops, value); }

    public string? CropsNote { get => _cropsNote; private set => Set(ref _cropsNote, value); }

    /// <summary>
    /// Opens a session folder and runs the whole analysis on a pool thread, reporting progress
    /// on the UI thread. A run in flight is cancelled first; a cancelled run leaves the window
    /// empty, its result having been cleared at the start, and says in <see cref="Status"/>
    /// that it was stopped. A folder the core refuses (an IO, format, state or argument error,
    /// or a refused access) becomes the problem card and the tabs stay hidden; any other
    /// exception propagates.
    /// </summary>
    /// <param name="folder">
    /// The session folder to analyse, as chosen or given on the command line.
    /// </param>
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
        BusyMessage = WindowTexts.Busy(0, 0);
        SeriesOptions options = Options;
        Progress<(int Done, int Total)> progress = new(p =>
        {
            Progress = p.Total > 0 ? p.Done / (double)p.Total : 0;
            BusyMessage = p.Done == p.Total ? WindowTexts.BusyRegistering : WindowTexts.Busy(p.Done, p.Total);
        });
        try
        {
            SeriesResult r = await Task.Run(() => SeriesRun.Run(folder, options, progress, cts.Token), cts.Token).ConfigureAwait(true);
            if (!cts.IsCancellationRequested)
            {
                Populate(r);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e) when (e is IOException or InvalidDataException or InvalidOperationException or ArgumentException or UnauthorizedAccessException)
        {
            if (ReferenceEquals(_cts, cts))
            {
                ProblemText = e.Message;
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
    /// Writes the report, the CSV and the rejected list into <paramref name="dir"/> (R-032), and
    /// shows the paths written, or the error, in <see cref="Status"/>; nothing without a result.
    /// </summary>
    /// <param name="dir">The folder to write into, as chosen in the dialog.</param>
    public void Export(string dir)
    {
        if (_result is null)
        {
            return;
        }

        try
        {
            Status = WindowTexts.Written(SeriesOutputs.WriteAll(_result, dir));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Status = "EROARE: " + e.Message;
        }
    }

    /// <summary>
    /// Drops the result: every tab's rows, tables, points and cut-outs back to empty and the
    /// headers to their defaults, then raises the change of each.
    /// </summary>
    private void Clear()
    {
        _result = null;
        Report = "";
        SessionRows = SummaryRows = FocusRows = NoiseRows = DewRows = TrailRows = DriftRows = DitherRows = [];
        Frames = Rejected = [];
        DitherTables = [];
        (DxHeader, DyHeader) = ("dX", "dY");
        (MapXLabel, MapYLabel) = ("X", "Y");
        RejectedHeading = "";
        MapPoints = StepPoints = NearestPoints = FwhmPoints = ElongationPoints = StarPoints = BackgroundPoints = [];
        StepGuides = ElongationGuides = [];
        SelectedFrame = null;
        Crops = [];
        CropsNote = null;
        RaiseAll();
    }

    /// <summary>
    /// Shapes a finished run for the tabs: the console's text, the session lines, the core's row
    /// and table producers, the frames table and the chart points (NaN where a frame has no
    /// figure), the guide lines at the thresholds, the axis labels and headers. Every figure
    /// comes from the core; the guide lines' labels and the session card's "scara" label are
    /// written here.
    /// </summary>
    /// <param name="r">The finished run's result.</param>
    internal void Populate(SeriesResult r)
    {
        _result = r;
        using (StringWriter sw = new())
        {
            RunReport.WriteBanner(sw);
            SeriesReport.Write(r, sw);
            Report = sw.ToString();
        }

        List<FrameInfo> infos = r.Frames.Select(f => f.Info).ToList();
        using (StringWriter header = new())
        {
            InventoryReport.WriteHeader(new Inventory(r.Session, infos, r.Cadence), header);
            SessionRows = header.ToString().ReplaceLineEndings("\n").Split('\n')
                .Where(l => l.StartsWith("    ", StringComparison.Ordinal) && l.Contains(" : ", StringComparison.Ordinal))
                .Select(l => new RowVm(l[4..l.IndexOf(" : ", StringComparison.Ordinal)].Trim(), l[(l.IndexOf(" : ", StringComparison.Ordinal) + 3)..], null))
                .Append(new RowVm("scara", SeriesReport.ScaleLine(r), null))
                .ToList();
        }

        SummaryRows = Rows(SeriesReport.SummaryRows(r));
        FocusRows = Rows(SeriesReport.FocusRows(r));
        NoiseRows = Rows(SeriesReport.NoiseRows(r));
        DewRows = Rows(SeriesReport.DewRows(r));
        TrailRows = Rows(SeriesReport.TrailRows(r));
        DriftRows = Rows(SeriesReport.DriftRows(r));
        DitherRows = Rows(SeriesReport.DitherScalarRows(r));
        DitherTables = SeriesReport.DitherTables(r).Select(t => new TableVm(t.Title, t.Columns, t.Rows)).ToList();

        SeriesAnalysis s = r.Series;
        DitherAnalysis d = r.Dither;
        IReadOnlyList<FrameTableRow> table = SeriesReport.FrameRows(r);
        HashSet<int> rejected = SeriesReport.RejectedRows(r).Select(x => x.Index).ToHashSet();
        List<FrameRowVm> frames = [];
        List<ChartPoint> map = [], steps = [], nearest = [], fwhm = [], elong = [], stars = [], bg = [];
        for (int i = 0; i < r.Frames.Count; i++)
        {
            AnalyzedFrame f = r.Frames[i];
            FrameMeasurement? m = f.Measurement;
            RegistrationResult? g = f.Registration is { Ok: true } rg ? rg : null;
            Color colour = ClassColour(s.Classes[i]);
            string label = f.Info.Index.ToString(CultureInfo.InvariantCulture);
            frames.Add(new FrameRowVm(table[i], m is null ? null : f.Info.Path));
            map.Add(new ChartPoint(g?.Dx ?? double.NaN, g?.Dy ?? double.NaN, label, colour, Ring: d.SpotRun[i] is not null));
            steps.Add(new ChartPoint(f.Info.Index, d.Step[i] ?? double.NaN, label, d.IsMove[i] ? VerdictPalette.Warn : VerdictPalette.Neutral, Tip: table[i].StepTip));
            nearest.Add(new ChartPoint(f.Info.Index, d.NearestOther[i] ?? double.NaN, label, Color.FromRgb(0x4E, 0xA8, 0xDE)));
            fwhm.Add(new ChartPoint(f.Info.Index, m?.FwhmMedian ?? double.NaN, label, colour));
            elong.Add(new ChartPoint(f.Info.Index, m?.ElongationMedian ?? double.NaN, label, colour));
            stars.Add(new ChartPoint(f.Info.Index, m?.Detected ?? double.NaN, label, VerdictPalette.Good));
            bg.Add(new ChartPoint(f.Info.Index, m?.BackgroundMedian ?? double.NaN, label, Color.FromRgb(0x9C, 0xCC, 0x65)));
        }

        Frames = frames;
        Rejected = frames.Where(f => rejected.Contains(f.Index)).ToList();
        RejectedHeading = SeriesReport.RejectedHeading(r);
        MapPoints = map;
        StepPoints = steps;
        NearestPoints = nearest;
        StepGuides = [new GuideLine(Th.DitherStep, $"miscare >= {RunReport.F(Th.DitherStep, 0)} px", VerdictPalette.Warn), new GuideLine(Th.CoverageLoose, $"{RunReport.F(Th.CoverageLoose, 0)} px (walking noise)", VerdictPalette.Good)];
        FwhmPoints = fwhm;
        ElongationPoints = elong;
        ElongationGuides = [new GuideLine(Th.ElongationMarginal, "la limita", VerdictPalette.Warn), new GuideLine(Th.ElongationBad, "SLAB", VerdictPalette.Bad)];
        StarPoints = stars;
        BackgroundPoints = bg;
        (MapXLabel, MapYLabel) = SeriesReport.AxisLabels(s);
        (DxHeader, DyHeader) = SeriesReport.PositionHeaders(s);
        RaiseAll();
    }

    /// <summary>
    /// Decodes the selected frame again on a pool thread and publishes its star cut-outs, unless
    /// another frame was selected meanwhile; a frame without a path, without a measurement or
    /// without a cut-out gets the note instead, and a decoder error becomes the note.
    /// </summary>
    /// <param name="row">The frame selected; null for no selection, which clears the note.</param>
    private async Task LoadCropsAsync(FrameRowVm? row)
    {
        Crops = [];
        if (row?.Path is null || _result is null)
        {
            CropsNote = row is null ? null : WindowTexts.NoCrops;
            return;
        }

        FrameMeasurement? m = _result.Frames.FirstOrDefault(f => f.Info.Index == row.Index)?.Measurement;
        if (m is null)
        {
            CropsNote = WindowTexts.NoCrops;
            return;
        }

        CropsNote = WindowTexts.CropsBusy;
        string path = row.Path;
        try
        {
            IReadOnlyList<StarCrop> crops = await Task.Run(() => StarCrops.Build(path, m)).ConfigureAwait(true);
            if (!ReferenceEquals(_selectedFrame, row))
            {
                return;
            }

            Crops = crops.Select(c => new CropVm(SeriesReport.CropCaption(c), RasterBitmaps.Grey(c.Grey, c.Width, c.Height))).ToList();
            CropsNote = crops.Count == 0 ? WindowTexts.NoCrops : null;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            // The same net as SeriesRun casts per frame: whatever the decoder throws is this
            // frame's error, shown in its place, never an unobserved task exception.
            CropsNote = "EROARE: " + e.Message;
        }
    }

    /// <summary>The core's report rows as the window's rows, one for one.</summary>
    /// <param name="rows">The rows of one of the core's row producers.</param>
    /// <returns>One <see cref="RowVm"/> per row, in order.</returns>
    private static List<RowVm> Rows(IReadOnlyList<ReportRow> rows) => rows.Select(r => new RowVm(r.Label, r.Value, r.Verdict)).ToList();

    /// <summary>The colour a frame of a class is drawn in on the charts.</summary>
    /// <param name="c">The frame's class.</param>
    /// <returns>
    /// Good, warning or bad from <see cref="VerdictPalette"/>; neutral for any other class.
    /// </returns>
    internal static Color ClassColour(FrameClass c) => c switch
    {
        FrameClass.Good => VerdictPalette.Good,
        FrameClass.Marginal => VerdictPalette.Warn,
        FrameClass.Bad => VerdictPalette.Bad,
        _ => VerdictPalette.Neutral,
    };

    /// <summary>
    /// Raises the change of every property the result feeds, after a clear or a populate.
    /// </summary>
    private void RaiseAll()
    {
        foreach (string name in new[]
        {
            nameof(HasResult), nameof(SessionRows), nameof(SummaryRows), nameof(FocusRows), nameof(NoiseRows), nameof(DewRows), nameof(TrailRows),
            nameof(DriftRows), nameof(DitherRows), nameof(Frames), nameof(Rejected), nameof(RejectedHeading), nameof(MapPoints), nameof(StepPoints),
            nameof(NearestPoints), nameof(StepGuides), nameof(FwhmPoints), nameof(ElongationPoints), nameof(ElongationGuides), nameof(StarPoints),
            nameof(BackgroundPoints), nameof(MapXLabel), nameof(MapYLabel), nameof(DitherTables), nameof(DxHeader), nameof(DyHeader),
        })
        {
            Raise(name);
        }
    }
}
