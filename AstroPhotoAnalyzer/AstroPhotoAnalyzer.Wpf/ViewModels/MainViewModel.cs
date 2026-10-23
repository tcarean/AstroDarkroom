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
using System.IO;
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
        bool _hasResult;
        AnalysisResult _result;

        /// <summary>Makes the parameter fields and the tabs, all empty.</summary>
        public MainViewModel()
        {
            Parameters = new ParameterFields();
            Report = new ReportTabViewModel();
            Tones = new TonesTabViewModel();
            Export = new ExportTabViewModel();
            Resolution = new ResolutionTabViewModel();
            Background = new BackgroundTabViewModel();
            Preview = new PreviewTabViewModel();
            Curves = new CurvesTabViewModel();
        }

        public ParameterFields Parameters { get; }
        public ReportTabViewModel Report { get; }
        public TonesTabViewModel Tones { get; }
        public ExportTabViewModel Export { get; }
        public ResolutionTabViewModel Resolution { get; }
        public BackgroundTabViewModel Background { get; }
        public PreviewTabViewModel Preview { get; }
        public CurvesTabViewModel Curves { get; }

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

        public bool HasResult
        {
            get => _hasResult;
            private set { if (Set(ref _hasResult, value)) Raise(nameof(CanSave)); }
        }

        public bool CanSave => HasResult && !IsBusy;

        /// <summary>Raport.txt beside the image - the default name in the save dialog.</summary>
        public string DefaultReportPath
            => HasPath ? System.IO.Path.Combine(System.IO.Path.GetDirectoryName(_path) ?? "", "Raport.txt") : "Raport.txt";

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
        /// Runs (again) on the chosen image, with the fields as they are now: a bad field sets
        /// <see cref="Error"/> and runs nothing; otherwise the tabs are cleared, the core runs on
        /// a pool thread, and the tabs are filled, or <see cref="Error"/> gets the exception's
        /// messages. Nothing happens without a path or while a run is on.
        /// </summary>
        internal async Task RunAsync()
        {
            if (!HasPath || IsBusy) return;

            Parameters p = Parameters.TryBuild(out string bad);
            if (p == null) { Error = bad; return; }

            Error = null;
            HasResult = false;
            ClearTabs();
            IsBusy = true;
            BusyMessage = "citire";
            var progress = new Progress<string>(stage => BusyMessage = stage);
            string path = Path;
            try
            {
                _result = await Task.Run(() => AnalysisRun.Execute(path, p, progress)).ConfigureAwait(true);
                Populate(_result);
                HasResult = true;
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
        /// Raport.txt: banner + body, identical to what the console would write with -o.
        /// </summary>
        /// <param name="file">
        /// The file to write, replaced if it exists; nothing is written without a result.
        /// </param>
        internal void SaveReport(string file)
        {
            if (_result == null) return;
            using var w = new StreamWriter(file, false);
            ReportWriter.WriteFull(_result, w);
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
            Preview.Populate(r);
            Curves.Populate(r);
        }

        /// <summary>Empties the seven tabs.</summary>
        void ClearTabs()
        {
            Report.Clear(); Tones.Clear(); Export.Clear(); Resolution.Clear(); Background.Clear(); Preview.Clear(); Curves.Clear();
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
