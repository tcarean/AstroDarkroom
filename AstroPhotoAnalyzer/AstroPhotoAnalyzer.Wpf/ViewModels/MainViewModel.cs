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

// MainViewModel.cs - the file, the parameters, the run, and the seven tabs.
//
// One run at a time: the button is disabled while it computes. The analysis takes a few
// seconds on 23 MP (the JPEG is half of them) and sits on a pool thread; the progress comes by
// stage, through IProgress<string>, which captures its context at construction - so on the UI
// thread, when it is built there.
//
// It opens no dialogs: choosing the file and the place to save are the window's. A view-model
// that opens dialogs can no longer be tested.

using System;
using System.Threading.Tasks;

namespace AstroPhotoAnalyzer.Wpf
{
    /// <summary>
    /// The window's state (R-110, R-111): the image's path, the parameter fields, the busy and
    /// error flags, and the tab view-models the result is poured into.
    /// </summary>
    public sealed class MainViewModel : ObservableObject
    {
        string _path = "";
        bool _busy;
        string _busyMessage = "";
        string _error;
        string _saveMessage, _saveError;
        bool _hasResult;
        AnalysisResult _result;

        // The words the console prints after -o ("-> raport salvat: <file>"), without the arrow.
        static string SavedPrefix => Tr.Pick("report saved: ", "raport salvat: ");

        // What the window remembers between its runs (R-156): the profile's path, written on
        // every change of the bar's profile.
        readonly WindowSettings _settings;

        /// <summary>Makes the parameter fields and the tabs, all empty, remembering nothing (the tests' constructor).</summary>
        public MainViewModel() : this(WindowSettings.None) { }

        /// <summary>
        /// Makes the parameter fields and the tabs, all empty, and restores the profile the
        /// settings remember (R-156): read again from its file by the core, applied when
        /// readable, shown with its reason when not.
        /// </summary>
        /// <param name="settings">
        /// The settings read at the start, written back on every change of the profile; not
        /// null (<see cref="WindowSettings.None"/> remembers nothing).
        /// </param>
        public MainViewModel(WindowSettings settings)
        {
            _settings = settings ?? WindowSettings.None;
            Parameters = new ParameterFields();
            Report = new ReportTabViewModel();
            Tones = new TonesTabViewModel();
            Export = new ExportTabViewModel();
            Resolution = new ResolutionTabViewModel();
            Background = new BackgroundTabViewModel();
            Curves = new CurvesTabViewModel();
            SoftProof = new SoftProofTabViewModel();

            if (_settings.IccProfilePath != null)
                Parameters.RestoreProfile(new IccProfileRow(IccPaperBlack.Read(_settings.IccProfilePath)));
            // Subscribed after the restore: what was just read back need not be written again.
            Parameters.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName != nameof(ParameterFields.Profile)) return;
                _settings.IccProfilePath = Parameters.Profile?.File;
                _settings.Write();
            };
        }

        public ParameterFields Parameters { get; }
        public ReportTabViewModel Report { get; }
        public TonesTabViewModel Tones { get; }
        public ExportTabViewModel Export { get; }
        public ResolutionTabViewModel Resolution { get; }
        public BackgroundTabViewModel Background { get; }
        public CurvesTabViewModel Curves { get; }
        public SoftProofTabViewModel SoftProof { get; }

        public string Banner => VersionInfo.Banner;
        public string Copyright => VersionInfo.Copyright;

        /// <summary>The image chosen, or empty.</summary>
        public string Path
        {
            get => _path;
            private set { if (Set(ref _path, value)) { Raise(nameof(HasPath)); Raise(nameof(CanRun)); } }
        }

        public bool HasPath => !string.IsNullOrEmpty(_path);

        public bool IsBusy
        {
            get => _busy;
            private set { if (Set(ref _busy, value)) { Raise(nameof(IsIdle)); Raise(nameof(CanRun)); } }
        }

        public bool IsIdle => !_busy;
        public bool CanRun => HasPath && !IsBusy;

        public string BusyMessage { get => _busyMessage; private set => Set(ref _busyMessage, value); }

        public string Error
        {
            get => _error;
            private set { if (Set(ref _error, value)) Raise(nameof(HasError)); }
        }

        public bool HasError => !string.IsNullOrEmpty(_error);

        /// <summary>
        /// Where the last "Salveaza raportul" wrote, "raport salvat: " and the path (R-111);
        /// null until a save, after one that failed, and from the moment the next run is asked,
        /// even one a bad field stops.
        /// </summary>
        public string SaveMessage { get => _saveMessage; private set => Set(ref _saveMessage, value); }

        /// <summary>
        /// Why the last "Salveaza raportul" could not write, <see cref="Describe"/>'s text
        /// (R-111); null until a save, after one that worked, and from the moment the next run is
        /// asked, even one a bad field stops.
        /// </summary>
        public string SaveError { get => _saveError; private set => Set(ref _saveError, value); }

        public bool HasResult
        {
            get => _hasResult;
            private set { if (Set(ref _hasResult, value)) Raise(nameof(CanSave)); }
        }

        public bool CanSave => HasResult && !IsBusy;

        /// <summary>
        /// The curves' computation the last run started (R-146), for whoever must wait for the
        /// gallery; completed before the first run and after one that failed.
        /// </summary>
        internal Task CurvesComputation { get; private set; } = Task.CompletedTask;

        /// <summary>
        /// The soft proof's computation the last run started (R-163), started beside the curves';
        /// completed before the first run and after one that failed.
        /// </summary>
        internal Task ProofComputation { get; private set; } = Task.CompletedTask;

        /// <summary>
        /// Report.txt (Raport.txt in Romanian) beside the image - the default name in the save
        /// dialog.
        /// </summary>
        public string DefaultReportPath
            => HasPath ? System.IO.Path.Combine(System.IO.Path.GetDirectoryName(_path) ?? "", ReportFileName) : ReportFileName;

        /// <summary>The report's default file name, in the interface language.</summary>
        static string ReportFileName => Tr.Pick("Report.txt", "Raport.txt");

        /// <summary>
        /// Chooses the image and starts the analysis: without it the window has nothing to show.
        /// </summary>
        /// <param name="path">
        /// The image's path; null or empty clears the path and runs nothing.
        /// </param>
        internal Task OpenAsync(string path)
        {
            Path = path ?? "";
            return HasPath ? RunAsync() : Task.CompletedTask;
        }

        /// <summary>
        /// "Foloseste" in the profile window (R-132, R-155): the profile goes into the fields and
        /// the bar, and the analysis runs again by itself on the open image. A choice that
        /// changes nothing (the profile in use again, its Dmax still in the field) runs nothing.
        /// </summary>
        /// <param name="profile">The row chosen; null, or one that could not be read, changes nothing.</param>
        /// <returns>The run, or a completed task when nothing runs.</returns>
        internal Task ChooseProfile(IccProfileRow profile)
        {
            // The buttons are disabled while a run lasts; a caller that reaches here anyway
            // changes nothing, rather than changing the fields with no run to show them.
            if (IsBusy) return Task.CompletedTask;
            return Parameters.UseProfile(profile) ? RunAsync() : Task.CompletedTask;
        }

        /// <summary>
        /// "Scoate" in the top bar (R-155): the profile is dropped, the medium's Dmax is back in
        /// the field, and the analysis runs again by itself on the open image. Nothing happens
        /// without a profile, or while a run lasts.
        /// </summary>
        /// <returns>The run, or a completed task when nothing runs.</returns>
        internal Task ClearProfile()
        {
            if (IsBusy || !Parameters.HasProfile) return Task.CompletedTask;
            Parameters.ClearProfile();
            return RunAsync();
        }

        /// <summary>
        /// Runs (again) on the chosen image, with the fields as they are now: a bad field sets
        /// <see cref="Error"/> and runs nothing; otherwise the tabs are cleared, the core runs on
        /// a pool thread, and the tabs are filled, or <see cref="Error"/> gets the exception's
        /// messages. Nothing happens without a path or while a run is on.
        /// </summary>
        internal async Task RunAsync()
        {
            if (!HasPath || IsBusy) return;

            // The save's outcome is the previous run's: gone from the moment a new one is asked,
            // even one a bad field stops, so that it never names another image's report.
            SaveMessage = null;
            SaveError = null;
            Parameters p = Parameters.TryBuild(out string bad);
            if (p == null) { Error = bad; return; }

            Error = null;
            HasResult = false;
            ClearTabs();
            IsBusy = true;
            BusyMessage = Tr.Pick("reading", "citire");
            var progress = new Progress<string>(stage => BusyMessage = stage);
            string path = Path;
            try
            {
                _result = await Task.Run(() => AnalysisRun.Execute(path, p, progress)).ConfigureAwait(true);
                Populate(_result);
                HasResult = true;
                // The curves follow the analysis without a press (R-146, ADR 0051): the window is
                // free meanwhile, the tab says "computing" until they are there. ComputeAsync
                // catches its own exceptions, so the task never faults.
                CurvesComputation = Curves.ComputeAsync();
                // The soft proof too (R-163, ADR 0066): its own run, independent of the curves'.
                ProofComputation = SoftProof.ComputeAsync();
            }
            catch (Exception e)
            {
                _result = null;
                Error = Describe(e);
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Raport.txt: banner + body, identical to what the console would write with -o, written
        /// whole or not at all (ReportWriter.SaveFull, R-005). The outcome is told, not thrown
        /// (R-111): <see cref="SaveMessage"/> names the file written, or <see cref="SaveError"/>
        /// says why it could not be (held open by another program, a folder that refuses writing
        /// or does not exist, a path the system refuses).
        /// </summary>
        /// <param name="file">
        /// The file to write, replaced if it exists; nothing is written without a result.
        /// </param>
        internal void SaveReport(string file)
        {
            if (_result == null) return;
            try
            {
                ReportWriter.SaveFull(_result, file);
                SaveError = null;
                SaveMessage = SavedPrefix + file;
            }
            catch (Exception e)
            {
                SaveMessage = null;
                SaveError = Describe(e);
            }
        }

        /// <summary>Pours one result into the seven tabs.</summary>
        /// <param name="r">The analysis; not null.</param>
        void Populate(AnalysisResult r)
        {
            Report.Populate(r);
            Tones.Populate(r);
            Export.Populate(r);
            Resolution.Populate(r);
            Background.Populate(r);
            Curves.Populate(r);
            SoftProof.Populate(r);
        }

        /// <summary>Empties the seven tabs.</summary>
        void ClearTabs()
        {
            Report.Clear(); Tones.Clear(); Export.Clear(); Resolution.Clear(); Background.Clear(); Curves.Clear(); SoftProof.Clear();
        }

        /// <summary>
        /// The exception's message, with the cause if there is one; no stack - it helps nobody on
        /// screen.
        /// </summary>
        /// <param name="e">The exception; not null.</param>
        /// <returns>
        /// Its message, then each inner message that is not blank and not already contained,
        /// separated by blank lines.
        /// </returns>
        internal static string Describe(Exception e)
        {
            string m = e.Message;
            for (Exception inner = e.InnerException; inner != null; inner = inner.InnerException)
                if (!string.IsNullOrWhiteSpace(inner.Message) && !m.Contains(inner.Message))
                    m += "\n\n" + inner.Message;
            return m;
        }
    }
}
