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

// MainViewModel.cs - fisierul, parametrii, rularea, si cele sase taburi.
//
// O singura rulare la un moment dat: butonul se dezactiveaza cat timp se calculeaza.
// Analiza tine cateva secunde pe 23 MP (JPEG-ul e jumatate din ele) si sta pe un fir
// din pool; progresul vine pe etape, prin IProgress<string>, care isi capteaza contextul
// la constructie - deci pe firul de interfata, cand e construit acolo.
//
// Nu deschide dialoge: alegerea fisierului si a locului de salvare sunt ale ferestrei.
// Un ViewModel care deschide dialoge nu se mai poate testa.

using System;
using System.IO;
using System.Threading.Tasks;

namespace AstroPhotoAnalyzer.Wpf
{
    public sealed class MainViewModel : ObservableObject
    {
        string _path = "";
        bool _busy;
        string _busyMessage = "";
        string _error;
        bool _hasResult;
        AnalysisResult _result;

        public MainViewModel()
        {
            Parameters = new ParameterFields();
            Report = new ReportTabViewModel();
            Tones = new TonesTabViewModel();
            Export = new ExportTabViewModel();
            Resolution = new ResolutionTabViewModel();
            Background = new BackgroundTabViewModel();
            Preview = new PreviewTabViewModel();
        }

        public ParameterFields Parameters { get; }
        public ReportTabViewModel Report { get; }
        public TonesTabViewModel Tones { get; }
        public ExportTabViewModel Export { get; }
        public ResolutionTabViewModel Resolution { get; }
        public BackgroundTabViewModel Background { get; }
        public PreviewTabViewModel Preview { get; }

        public string Banner => VersionInfo.Banner;
        public string Copyright => VersionInfo.Copyright;

        /// <summary>Imaginea aleasa, sau gol.</summary>
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

        /// <summary>Raport.txt langa imagine - numele implicit din dialogul de salvare.</summary>
        public string DefaultReportPath
            => HasPath ? System.IO.Path.Combine(System.IO.Path.GetDirectoryName(_path) ?? "", "Raport.txt") : "Raport.txt";

        /// <summary>Alege imaginea si porneste analiza: fara ea fereastra n-are ce arata.</summary>
        internal Task OpenAsync(string path)
        {
            Path = path ?? "";
            return HasPath ? RunAsync() : Task.CompletedTask;
        }

        /// <summary>Ruleaza (din nou) pe imaginea aleasa, cu parametrii de acum.</summary>
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

        /// <summary>Raport.txt: banner + corp, identic cu ce ar scrie consola cu -o.</summary>
        internal void SaveReport(string file)
        {
            if (_result == null) return;
            using var w = new StreamWriter(file, false);
            ReportWriter.WriteFull(_result, w);
        }

        void Populate(AnalysisResult r)
        {
            Report.Populate(r);
            Tones.Populate(r);
            Export.Populate(r);
            Resolution.Populate(r);
            Background.Populate(r);
            Preview.Populate(r);
        }

        void ClearTabs()
        {
            Report.Clear(); Tones.Clear(); Export.Clear(); Resolution.Clear(); Background.Clear(); Preview.Clear();
        }

        /// <summary>Mesajul exceptiei, cu cauza daca exista; fara stiva - nu ajuta pe nimeni pe ecran.</summary>
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
