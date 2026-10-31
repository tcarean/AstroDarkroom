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

// HistEqTabViewModel.cs - the "Histogram" tab: one frame seen through histogram equalisation.
//
// The list has the master in first position, the smoothed flat beside it when it exists, and then
// the frames from flats/, raw or FITS. The master is there for a single question - did the
// imperfections of the individual frames average out? - and it is not preselected and does not run
// by itself on opening: it is a 24 MP float FITS, and the Structure tab is already reading it at
// that moment (the "sequential, not parallel" rule in MainViewModel). The smoothed one is there for
// the other question - what did the smoothing take out and what did it leave - and it is a third
// kind of row, not a second master: chip NETEZIT, IsMaster false.
//
// THE LIST IS REBUILT WHEN THE SMOOTHING WRITES. The Netezire tab can produce the file during the
// session; MainViewModel wires its event to FileWritten, which re-reads the list from the session -
// a single definition of the list, HistEqCandidates - and keeps the choice, without starting
// anything. With one exception: if the file rewritten is the very one chosen, it recomputes,
// otherwise the picture on screen would be of bytes no longer on disk.
//
// THE LOADER COVERS THE PICTURE, NOT THE TAB. The list stays clickable while it computes, and a
// click on another file CANCELS the current run and starts another. So a superseded run does not
// touch the screen, the generation counter in TabViewModel.RunGuardedAsync lets only the latest run
// close the busy state, and the lambda here checks its own token after the await before writing
// anything.
//
// THE PICTURE DOES NOT GO THROUGH PNG. The chosen view becomes a bitmap directly from the
// full-resolution raster (RasterBitmaps), without encoding: at 100 % a screen pixel is a sensor
// pixel, and on the mosaic the checkerboard shows as in Siril. The PNG stays the console's. The
// first version encoded PNGs from the reduced rasters (150-380 ms and 0.85-3.6 MB per view,
// measured), and the zoom would have magnified block averages. The bitmaps are cached per chip,
// frozen; the cost is the raster's memory, nothing else.
//
// The zoom is reset on a new FILE, not on a change of view: looking at the same place on G1 after
// Mozaic is exactly the point of the chips.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using static FlatCheck.Ui;

namespace FlatCheck.Wpf
{
    /// <summary>The Histogram tab: the master, the smoothed master and the frames of flats/ in a
    /// list, and the chosen one shown histogram-equalised, per view, at full resolution.</summary>
    public sealed class HistEqTabViewModel : TabViewModel
    {
        SirilSession _session;
        CancellationToken _parent;
        HeavyRunGate _gate;
        CancellationTokenSource _runCts;

        HistEqFileRow _selected;
        ImageSource _image;
        string _caption = "";
        string _frame = "";
        string _channels = "";
        string _masterNote = "";
        string _legend = "";

        /// <summary>The last result, so the views can be switched without re-reading the
        /// file.</summary>
        HistEqResult _last;

        /// <summary>The last result's file, for the report rebuilt when the chip goes on.</summary>
        string _lastPath;

        /// <summary>
        /// The run's report as the console would write it without --rainbow, and the same
        /// with it, built once from the kept result when the chip first colours the picture:
        /// the panel says what is on screen without re-reading the file.
        /// </summary>
        string _plainReport, _colouredReport;

        /// <summary>One bitmap per view, grey and in false colours, made on first use. The
        /// coloured mosaic of a 24 MP frame is 72 MB (ADR 0063 records the measured peak).</summary>
        readonly Dictionary<(HistEqViewOption Option, bool Rainbow), ImageSource> _cache = new();

        /// <summary>An empty tab, listening to its view switch.</summary>
        public HistEqTabViewModel()
        {
            View.Changed += RefreshImage;
        }

        /// <summary>The master first, the smoothed one when it exists, then the frames from
        /// flats/.</summary>
        public ObservableCollection<HistEqFileRow> Files { get; } = new();

        public HistEqFileRow Selected
        {
            get => _selected;
            set { if (Set(ref _selected, value)) Recompute(); }
        }

        /// <summary>The chosen view's bitmap, at full resolution, or null.</summary>
        public ImageSource Image { get => _image; private set => Set(ref _image, value); }

        /// <summary>The view switch: the mosaic, RGB, the positions.</summary>
        public HistEqViewViewModel View { get; } = new();

        /// <summary>Fitted or a factor; the control in the XAML reads and writes it.</summary>
        public ImageZoomViewModel Zoom { get; } = new();

        /// <summary>The reading guide. Documentation, not a diagnosis.</summary>
        public HistEqGuideViewModel Guide { get; } = new();

        /// <summary>What the view on screen is and at what size.</summary>
        public string Caption { get => _caption; private set => Set(ref _caption, value); }

        /// <summary>The legend of the false colours while the chip is on, from the core; ""
        /// otherwise. On the RGB view it is the sentence that says the view is not coloured.</summary>
        public string Legend { get => _legend; private set => Set(ref _legend, value); }

        /// <summary>What CANNOT be read from the picture. An instance property: {Binding} does
        /// not see a static one.</summary>
        public string Caveat => HistEqReport.Caveat();

        /// <summary>The frame's size and kind, from the report.</summary>
        public string Frame { get => _frame; private set => Set(ref _frame, value); }

        /// <summary>The channels' table, from the report, one row per line.</summary>
        public string Channels { get => _channels; private set => Set(ref _channels, value); }

        /// <summary>What looking at the master means; empty when a frame is chosen.</summary>
        public string MasterNote { get => _masterNote; private set => Set(ref _masterNote, value); }

        /// <summary>The last run started, so a test can await one it did not start.</summary>
        internal Task Pending { get; private set; } = Task.CompletedTask;

        /// <summary>Fills the list from the session, nothing chosen, and explains when it is
        /// empty; it computes nothing until a click.</summary>
        /// <param name="session">The folder just opened.</param>
        /// <param name="ct">The folder's token, to which every run's own token is
        /// linked.</param>
        /// <param name="gate">The window's heavy-run gate, taken around an equalisation; null
        /// runs at once.</param>
        internal void Attach(SirilSession session, CancellationToken ct, HeavyRunGate gate = null)
        {
            _session = session;
            _parent = ct;
            _gate = gate;

            FillFiles();

            _selected = null;
            Raise(nameof(Selected));

            if (Files.Count == 0)
                Refuse(T("Nothing to equalise", "Nimic de egalizat"), SessionMessages.NoFlatFrames(session),
                       "no master and no flat frame to equalise in " + session.Root);
            else
                ClearError();
        }

        /// <summary>The list, from the session: the master, the smoothed one when it exists, the
        /// frames.</summary>
        void FillFiles()
        {
            Files.Clear();
            IReadOnlyList<HistEqCandidate> candidates = _session.HistEqCandidates();
            int lastOfGroup = -1;
            for (int i = 0; i < candidates.Count; i++)
                if (candidates[i].Kind != HistEqKind.Frame) lastOfGroup = i;

            for (int i = 0; i < candidates.Count; i++)
            {
                HistEqCandidate c = candidates[i];
                Files.Add(new HistEqFileRow
                {
                    Path = c.Path,
                    Name = Path.GetFileName(c.Path),
                    // the file's real folder: the master can be in masters/ or process/
                    Where = Path.GetFileName(Path.GetDirectoryName(c.Path)) + "/",
                    Kind = c.Kind,
                    ClosesGroup = i == lastOfGroup,
                });
            }
        }

        /// <summary>
        /// Another tab wrote a file. If it is the current session's smoothed flat, the list is
        /// rebuilt and the choice kept (by path); if the chosen file is the very one rewritten, it
        /// recomputes. Any other path, or no session, does nothing.
        /// </summary>
        /// <param name="path">The full path of the file written.</param>
        internal void FileWritten(string path)
        {
            if (_session == null || path == null) return;
            if (!string.Equals(path, _session.SmoothedMasterPath, StringComparison.OrdinalIgnoreCase)) return;

            string selectedPath = Selected?.Path;

            // With a ListBox bound both ways, Files.Clear() pushes null into Selected's setter
            // right now; Recompute() stops at Selected == null without touching _runCts, so a run
            // in progress is not cancelled and its Populate reaches the screen as before. The
            // choice is then put straight into the field, not through the setter, so no run starts
            // on a file that has not changed.
            FillFiles();
            _selected = selectedPath == null ? null
                : Files.FirstOrDefault(r => string.Equals(r.Path, selectedPath, StringComparison.OrdinalIgnoreCase));
            Raise(nameof(Selected));

            if (_selected != null && string.Equals(selectedPath, path, StringComparison.OrdinalIgnoreCase))
                Recompute();
        }

        /// <summary>Starts a run on the chosen file and keeps it in <see cref="Pending"/>;
        /// nothing without a session or a choice.</summary>
        void Recompute()
        {
            if (_session == null || Selected == null) return;
            Pending = RecomputeAsync();
        }

        /// <summary>Equalises the chosen file, guarded, after the heavy-run gate, superseding any
        /// run still going, and shows the result and the console's report.</summary>
        /// <returns>A task that completes when the run ended; it never faults.</returns>
        internal Task RecomputeAsync()
        {
            HistEqFileRow row = Selected;
            if (row == null) return Task.CompletedTask;

            // The run from before, if it still goes, is superseded: its token is cancelled, and
            // its lambda stops at the first check after the await, without writing anything.
            CancellationTokenSource old = _runCts;
            old?.Cancel();
            old?.Dispose();
            var cts = CancellationTokenSource.CreateLinkedTokenSource(_parent);
            _runCts = cts;
            CancellationToken ct = cts.Token;
            HeavyRunGate gate = _gate;

            return RunGuardedAsync(T($"Equalising the histogram of {row.Name}...", $"Egalizez histograma pe {row.Name}..."), async () =>
            {
                // The master in the list is a 24 MP file another run may be reading: a pick waits
                // for the gate instead of loading it beside that run, and the waiting screen says
                // so. A pick superseded while it waits leaves the queue here, cancelled (ADR 0061).
                using IDisposable lease = await EnterGateAsync(gate, ct).ConfigureAwait(true);
                ct.ThrowIfCancellationRequested();

                var text = new StringWriter();
                var outcome = await Task.Run(() =>
                {
                    RunReport.WriteBanner(text);
                    var run = HistEqRun.Execute(row.Path, text);
                    ct.ThrowIfCancellationRequested();
                    return run;
                }, ct).ConfigureAwait(true);

                // Back on the UI thread: if another file was chosen meanwhile, this token is
                // cancelled and the result has no business on screen any more.
                ct.ThrowIfCancellationRequested();

                Report = text.ToString();
                Populate(outcome.Result, row.Kind, row.Path);
            });
        }

        /// <summary>
        /// Internal, not private, so the rendering tests can bring a result built directly with
        /// HistEqAnalyzer, without a file. A new file = zoom back to fitted.
        /// </summary>
        /// <param name="r">The analyzer's result; kept for a later change of view.</param>
        /// <param name="kind">What the file is: a master and a smoothed master get their note
        /// under the picture, a frame none.</param>
        /// <param name="path">The file the result came from, printed in the report rebuilt
        /// when the chip goes on; null when there is none (a result built in a test).</param>
        internal void Populate(HistEqResult r, HistEqKind kind = HistEqKind.Frame, string path = null)
        {
            _last = r;
            _lastPath = path;
            _plainReport = Report;
            _colouredReport = null;
            _cache.Clear();
            View.Rebuild(r);
            Zoom.Reset();

            Frame = HistEqReport.FrameLine(r);
            Channels = string.Join("\n", Enumerable.Range(0, r.Channels.Length).Select(i => HistEqReport.ChannelLine(r, i)));
            MasterNote = kind switch
            {
                HistEqKind.Master => HistEqReport.MasterVersusFrame(),
                HistEqKind.Smoothed => HistEqReport.SmoothedVersusMaster(),
                _ => "",
            };

            RefreshImage();
            HasResult = true;
        }

        /// <summary>Shows the chosen view's bitmap, made from the kept result on first use and
        /// cached per chip and colour, with its caption; nothing before a result. The false
        /// colours apply to a one-layer view only (the core's table); the RGB composite stays
        /// as it is with the chip on (ADR 0063).</summary>
        void RefreshImage()
        {
            HistEqResult r = _last;
            HistEqViewOption option = View.Selected;
            if (r == null || option == null) return;

            bool coloured = View.Rainbow && View.RainbowAvailable;
            if (!_cache.TryGetValue((option, coloured), out ImageSource bitmap))
            {
                bitmap = (option.Kind, coloured) switch
                {
                    (HistEqViewKind.Channel, false) => RasterBitmaps.Grey(r.Channels[option.Index].Full, r.FullW, r.FullH),
                    (HistEqViewKind.Channel, true) => RasterBitmaps.Rgb(RainbowLut.Colour(r.Channels[option.Index].Full), r.FullW, r.FullH),
                    (HistEqViewKind.Mosaic, false) => RasterBitmaps.Grey(r.MosaicFull, r.SourceW, r.SourceH),
                    // the mosaic, or the mono frame (its composite is the one grey channel three
                    // times): the core says which raster is the one layer
                    (_, true) => ColouredOneLayer(r),
                    _ => RasterBitmaps.Rgb(r.CompositeFullRgb, r.FullW, r.FullH),
                };
                _cache[(option, coloured)] = bitmap;
            }

            Image = bitmap;
            Caption = HistEqReport.ViewCaption(r, option.Key);
            Legend = coloured ? HistEqReport.RainbowLegend()
                   : View.Rainbow ? HistEqReport.RainbowNotOnRgb()
                   : "";
            // The panel shows the report of what is on screen: the run's own text, or the same
            // run written as the console writes it with --rainbow, rebuilt once from the kept
            // result. The plain text is the very string the run produced, not a copy.
            Report = coloured ? (_colouredReport ??= ColouredReport(r)) : _plainReport;
        }

        /// <summary>The full-resolution one-layer raster in false colours, as a bitmap.</summary>
        /// <param name="r">The kept result.</param>
        /// <returns>An RGB bitmap of the mosaic (CFA) or the channel (mono).</returns>
        static ImageSource ColouredOneLayer(HistEqResult r)
        {
            var (grey, w, h) = r.OneLayer(full: true);
            return RasterBitmaps.Rgb(RainbowLut.Colour(grey), w, h);
        }

        /// <summary>The run's report as the console writes it with --rainbow.</summary>
        /// <param name="r">The kept result.</param>
        /// <returns>The banner and the report, with the false-colours display line and the
        /// legend.</returns>
        string ColouredReport(HistEqResult r)
        {
            var text = new StringWriter();
            RunReport.WriteBanner(text);
            HistEqReport.Write(r, _lastPath ?? "", text, rainbow: true);
            return text.ToString();
        }

        /// <inheritdoc/>
        internal override void Reset()
        {
            CancellationTokenSource cts = _runCts;
            _runCts = null;
            try { cts?.Cancel(); } catch (ObjectDisposedException) { }
            cts?.Dispose();

            // without a session, FileWritten has nothing to rebuild: a closed folder does not
            // fill up again because the smoothing wrote into it
            _session = null;
            _gate = null;
            _last = null;
            _lastPath = null;
            _plainReport = null;
            _colouredReport = null;
            _cache.Clear();
            Image = null;
            Zoom.Reset();
            View.Rainbow = false;      // after _last = null: the Changed it raises finds nothing to redraw
            Caption = "";
            Legend = "";
            Frame = "";
            Channels = "";
            MasterNote = "";
            Files.Clear();
            _selected = null;
            Raise(nameof(Selected));
            base.Reset();
        }
    }
}
