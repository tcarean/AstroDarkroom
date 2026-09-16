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

// SmoothTabViewModel.cs - tabul "Netezire": un al doilea master, fara textura.
//
// RULEAZA PE BUTON, NU LA DESCHIDERE. Celelalte taburi calculeaza cand se deschide
// folderul; asta scrie un fisier de 96 MB langa master, si un fisier nu se produce
// fiindca cineva s-a uitat la un folder. Attach pregateste doar caile si ghidul.
//
// SECVENTIAL: butonul poate fi apasat in timp ce structura inca ruleaza pe acelasi
// master de 24 MP; RunAsync asteapta intai lantul de la deschidere (MainViewModel), din
// motivul de memorie de acolo.
//
// FISIERUL SE SCRIE DUPA CE REZULTATUL E PE ECRAN, si suprascrierea se intreaba printr-un
// delegat dat din afara - MessageBox in aplicatie, un raspuns fix in teste. Un "nu" lasa
// rezultatul pe ecran si fisierul vechi pe disc, si spune asta.
//
// Ca tabul de structura, tine RASTERELE celor doua harti, nu octetii: harta se
// reencodeaza la schimbarea plafonului si se tine in cache pe (harta, plafon). Plafonul
// implicit e +-2 %, nu +-10 %: ce se scoate e mic, si la 10 % harta ar fi alba.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FlatCheck.Wpf
{
    public sealed class SmoothTabViewModel : TabViewModel
    {
        SirilSession _session;
        CancellationToken _ct;
        Func<Task> _waitIdle;
        readonly Func<string, bool> _confirm;

        string _source = "";
        string _target = "";
        bool _canRun;
        string _frame = "";
        string _scaleLine = "";
        string _rejected = "";
        IReadOnlyList<MetricRow> _removed = Array.Empty<MetricRow>();
        string _warning = "";
        IReadOnlyList<MetricRow> _before = Array.Empty<MetricRow>();
        IReadOnlyList<MetricRow> _after = Array.Empty<MetricRow>();
        string _beforeVerdict = "";
        string _afterVerdict = "";
        string _savedTo = "";
        string _saveError = "";
        byte[] _map;
        string _mapLegend = "";
        bool _showAfterMap;

        Img _removedRaster;
        Img _afterResidual;
        readonly Dictionary<(bool After, MapScaleOption Scale), byte[]> _mapCache = new();

        /// <param name="confirm">Intrebarea de suprascriere: primeste textul, intoarce da/nu.</param>
        public SmoothTabViewModel(Func<string, bool> confirm = null)
        {
            _confirm = confirm ?? (_ => true);
            Run = new RelayCommand(() => Pending = RunAsync(), () => CanRun && IsIdle);
            Advise = new RelayCommand(() => Pending = AdviseAsync(), () => CanRun && IsIdle);
            MapScale.Changed += RefreshMap;
        }

        IReadOnlyList<MetricRow> _adviceRows = Array.Empty<MetricRow>();
        string _adviceTexture = "";
        string _adviceVerdict = "";
        string _adviceNote = "";

        /// <summary>Al doilea buton: propune scara, bifeaza chipul, omul apasa netezirea.</summary>
        internal RelayCommand Advise { get; }
        public System.Windows.Input.ICommand AdviseCommand => Advise;
        public string AdviseLabel => SessionMessages.SmoothAdviseLabel();

        /// <summary>Cate un rand pe treapta incercata; gol pana la prima propunere si pe "nimic de netezit".</summary>
        public IReadOnlyList<MetricRow> AdviceRows { get => _adviceRows; private set => Set(ref _adviceRows, value); }

        /// <summary>Textura fina a intrarii, cu pragul.</summary>
        public string AdviceTexture { get => _adviceTexture; private set => Set(ref _adviceTexture, value); }

        /// <summary>"SCARA PROPUSA: 300 px" / "NIMIC DE NETEZIT" / "NEDETERMINAT: ...".</summary>
        public string AdviceVerdict
        {
            get => _adviceVerdict;
            private set { if (Set(ref _adviceVerdict, value)) Raise(nameof(HasAdvice)); }
        }

        public bool HasAdvice => !string.IsNullOrEmpty(_adviceVerdict);

        public string AdviceNote { get => _adviceNote; private set => Set(ref _adviceNote, value); }

        /// <summary>Ce nu stie regula. Instanta, ca {Binding} sa o vada.</summary>
        public string AdviceCaveat => SmoothReport.AdviceCaveat().Replace('\n', ' ');

        /// <summary>Scara propusa la ultima propunere; NaN cand n-a fost una.</summary>
        internal double ProposedScalePx { get; private set; } = double.NaN;

        /// <summary>
        /// Propunerea: sfatuitorul pe masterul sesiunii, dupa lantul de la deschidere ca si
        /// netezirea, apoi chipul propus bifat. Nu scrie niciun fisier; rezultatul de pe
        /// ecran, daca exista unul, ramane.
        /// </summary>
        internal async Task AdviseAsync()
        {
            if (_session == null || !CanRun) return;
            string path = _session.FlatMasterPath;
            CancellationToken ct = _ct;

            await RunGuardedAsync("Propun scara de netezire...", async () =>
            {
                Run.RaiseCanExecuteChanged();
                Advise.RaiseCanExecuteChanged();
                if (_waitIdle != null) await _waitIdle().ConfigureAwait(true);
                ct.ThrowIfCancellationRequested();

                var text = new StringWriter();
                var outcome = await Task.Run(() =>
                {
                    RunReport.WriteBanner(text);
                    var run = SmoothRun.Advise(path, text);
                    ct.ThrowIfCancellationRequested();
                    return run;
                }, ct).ConfigureAwait(true);

                if (outcome.Refused)
                {
                    Report = text.ToString();
                    Fail(SessionMessages.SmoothRefusedTitle(), outcome.Refusal);
                    return;
                }
                PopulateAdvice(outcome.Advice);
                if (!HasResult) Report = text.ToString();
            }).ConfigureAwait(true);
            Run.RaiseCanExecuteChanged();
            Advise.RaiseCanExecuteChanged();
        }

        /// <summary>Internal, ca testele de randare sa poata aduce o propunere fara fisier.</summary>
        internal void PopulateAdvice(SmoothScaleAdvice a)
        {
            AdviceTexture = SmoothReport.InputTextureLine(a);
            AdviceRows = a.Trials.Select(t => new MetricRow
            {
                Label = $"scara {t.ScalePx:F0} px",
                Value = SmoothReport.TrialVerdict(t),
                Note = $"textura ramasa {t.TextureAfterPct:F2} %, asimetrie {t.AzimuthalDelta:+0.00;-0.00}, "
                     + $"inclinare {t.TiltDelta:+0.00;-0.00}, scos {t.RemovedPtpPct:F2} % ptp",
            }).ToList();
            AdviceNote = SmoothReport.AdviceNote(a).Replace('\n', ' ');
            AdviceVerdict = SmoothReport.AdviceVerdict(a);
            ProposedScalePx = a.ScalePx;
            if (a.Kind == SmoothAdviceKind.Proposed) Scale.SelectScale(a.ScalePx);
        }

        /// <summary>Masterul din care se face flat-ul netezit.</summary>
        public string Source { get => _source; private set => Set(ref _source, value); }

        /// <summary>Unde se scrie flat-ul netezit.</summary>
        public string Target { get => _target; private set => Set(ref _target, value); }

        /// <summary>Exista un master de netezit si nu ruleaza deja ceva.</summary>
        public bool CanRun
        {
            get => _canRun;
            private set { if (Set(ref _canRun, value)) { Run.RaiseCanExecuteChanged(); Advise?.RaiseCanExecuteChanged(); } }
        }

        /// <summary>Butonul. Porneste rularea; rezultatul nu e asteptat de nimeni in afara testelor.</summary>
        internal RelayCommand Run { get; }

        /// <summary>XAML leaga aici: RelayCommand e internal.</summary>
        public System.Windows.Input.ICommand RunCommand => Run;

        /// <summary>Eticheta butonului, din SessionMessages.</summary>
        public string ButtonLabel => SessionMessages.SmoothButtonLabel();

        /// <summary>Scara de netezire: trei chipuri. Se citeste la click.</summary>
        public SmoothScaleViewModel Scale { get; } = new();

        /// <summary>Ghidul: ce face, ce nu poate sti. Deschis implicit.</summary>
        public SmoothGuideViewModel Guide { get; } = new();

        public string Frame { get => _frame; private set => Set(ref _frame, value); }
        public string ScaleLine { get => _scaleLine; private set => Set(ref _scaleLine, value); }
        public string Rejected { get => _rejected; private set => Set(ref _rejected, value); }

        /// <summary>Cele patru cifre despre componenta scoasa, ca randuri fara verdict.</summary>
        public IReadOnlyList<MetricRow> Removed { get => _removed; private set => Set(ref _removed, value); }

        /// <summary>Atentionarea cand s-a scos mai mult decat textura, sau gol.</summary>
        public string Warning
        {
            get => _warning;
            private set { if (Set(ref _warning, value)) Raise(nameof(HasWarning)); }
        }

        public bool HasWarning => !string.IsNullOrEmpty(_warning);

        /// <summary>Bateria de structura pe intrare si pe iesire - aceleasi randuri ca tabul 1.</summary>
        public IReadOnlyList<MetricRow> Before { get => _before; private set => Set(ref _before, value); }
        public IReadOnlyList<MetricRow> After { get => _after; private set => Set(ref _after, value); }
        public string BeforeVerdict { get => _beforeVerdict; private set => Set(ref _beforeVerdict, value); }
        public string AfterVerdict { get => _afterVerdict; private set => Set(ref _afterVerdict, value); }

        /// <summary>Ce nu stie fisierul, si cum ajunge in Siril. Instanta, ca {Binding} sa le vada.</summary>
        public string Caveat => SmoothReport.Caveat().Replace('\n', ' ');
        public string SirilNote => SmoothReport.SirilNote().Replace('\n', ' ');

        /// <summary>Unde s-a scris flat-ul, sau gol.</summary>
        public string SavedTo { get => _savedTo; private set => Set(ref _savedTo, value); }

        /// <summary>De ce nu s-a scris (refuz de suprascriere, disc), sau gol. Cifrele raman.</summary>
        public string SaveError { get => _saveError; private set => Set(ref _saveError, value); }

        /// <summary>Harta afisata: componenta scoasa, sau reziduul flat-ului netezit.</summary>
        public byte[] Map { get => _map; private set => Set(ref _map, value); }

        public bool ShowAfterMap
        {
            get => _showAfterMap;
            set { if (Set(ref _showAfterMap, value)) RefreshMap(); }
        }

        /// <summary>Plafonul hartii; implicit +-2 %, banda PERFECT - ce se scoate e mic.</summary>
        public MapScaleViewModel MapScale { get; } = new(Th.ResidPerfect);

        public string MapLegend { get => _mapLegend; private set => Set(ref _mapLegend, value); }

        /// <summary>Ultima rulare pornita de buton, ca un test sa o poata astepta.</summary>
        internal Task Pending { get; private set; } = Task.CompletedTask;

        /// <summary>Scara folosita la ultima rulare, in pixeli de senzor; NaN inainte de prima.</summary>
        internal double LastScalePx { get; private set; } = double.NaN;

        internal void Attach(SirilSession session, CancellationToken ct, Func<Task> waitIdle = null)
        {
            _session = session;
            _ct = ct;
            _waitIdle = waitIdle;
            Source = session.FlatMasterPath ?? "";
            Target = session.SmoothedMasterPath ?? "";
            CanRun = session.FlatMasterPath != null;
            Advise.RaiseCanExecuteChanged();
            ClearError();
        }

        internal async Task RunAsync()
        {
            if (_session == null || !CanRun) return;
            string path = _session.FlatMasterPath;
            string target = _session.SmoothedMasterPath;
            double scale = Scale.ScalePx;
            CancellationToken ct = _ct;

            await RunGuardedAsync("Netezesc masterul de flat...", async () =>
            {
                Run.RaiseCanExecuteChanged();
                if (_waitIdle != null) await _waitIdle().ConfigureAwait(true);
                ct.ThrowIfCancellationRequested();

                var text = new StringWriter();
                var outcome = await Task.Run(() =>
                {
                    RunReport.WriteBanner(text);
                    var run = SmoothRun.Execute(path, scale, text);
                    ct.ThrowIfCancellationRequested();
                    return run;
                }, ct).ConfigureAwait(true);

                if (outcome.Refused)
                {
                    Report = text.ToString();
                    Fail(SessionMessages.SmoothRefusedTitle(), outcome.Refusal);
                    return;
                }

                LastScalePx = scale;
                Populate(outcome.Result);
                // octetii primiti sunt pe scara automata: in cache, ca sa nu se reencodeze
                _mapCache[(false, MapScale.Options[0])] = outcome.Png;
                _mapCache[(true, MapScale.Options[0])] = outcome.SecondaryPng;

                // Fisierul, dupa ce cifrele sunt pe ecran: un refuz de suprascriere sau
                // un disc plin nu au de ce sa ascunda un rezultat deja calculat.
                SavedTo = "";
                SaveError = "";
                if (File.Exists(target) && !_confirm(SessionMessages.SmoothOverwriteQuestion(target)))
                {
                    SaveError = SessionMessages.SmoothNotOverwritten(target);
                    Report = text.ToString();
                    return;
                }
                try
                {
                    await Task.Run(() =>
                    {
                        using var fs = new FileStream(target, FileMode.Create, FileAccess.Write);
                        SmoothRun.WriteFits(outcome, path, fs);
                    }, ct).ConfigureAwait(true);
                    text.WriteLine(SmoothReport.SavedLine(target));
                    SavedTo = target;
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                {
                    SaveError = "Nu am putut scrie flat-ul netezit:\n" + Describe(e);
                }
                Report = text.ToString();
            }).ConfigureAwait(true);
            // butonul se reactiveaza dupa ce ecranul de asteptare s-a stins
            Run.RaiseCanExecuteChanged();
        }

        /// <summary>Internal, ca testele de randare sa poata aduce un rezultat fara fisier.</summary>
        internal void Populate(SmoothResult r)
        {
            Frame = SmoothReport.FrameLine(r);
            ScaleLine = SmoothReport.ScaleLine(r);
            Rejected = SmoothReport.RejectedLine(r);
            Removed = SmoothReport.RemovedLines(r).Select(line =>
            {
                int colon = line.IndexOf(':');
                return new MetricRow
                {
                    Label = line.Substring(0, colon).Trim(),
                    Value = line.Substring(colon + 1).Trim(),
                };
            }).ToList();
            Warning = SmoothReport.Warning(r)?.Replace('\n', ' ') ?? "";
            Before = StructureRows.Tests(r.Before);
            After = StructureRows.Tests(r.After);
            BeforeVerdict = r.Before.Verdict;
            AfterVerdict = r.After.Verdict;
            _removedRaster = r.Removed;
            _afterResidual = r.After.Residual;
            _mapCache.Clear();
            RefreshMap();
            HasResult = true;
        }

        void RefreshMap()
        {
            Img raster = _showAfterMap ? _afterResidual : _removedRaster;
            if (raster == null) { Map = null; MapLegend = ""; return; }

            var range = MapScale.RangeFor(raster);
            var key = (_showAfterMap, MapScale.Selected);
            if (!_mapCache.TryGetValue(key, out byte[] png))
            {
                png = PngWriter.EncodeResidualMap(raster, range);
                _mapCache[key] = png;
            }
            Map = png;
            MapLegend = RunReport.MapLegend(range);
        }

        internal override void Reset()
        {
            base.Reset();
            _session = null;
            _waitIdle = null;
            Source = "";
            Target = "";
            CanRun = false;
            Frame = "";
            ScaleLine = "";
            Rejected = "";
            Removed = Array.Empty<MetricRow>();
            Warning = "";
            Before = Array.Empty<MetricRow>();
            After = Array.Empty<MetricRow>();
            BeforeVerdict = "";
            AfterVerdict = "";
            SavedTo = "";
            SaveError = "";
            Map = null;
            MapLegend = "";
            LastScalePx = double.NaN;
            AdviceRows = Array.Empty<MetricRow>();
            AdviceTexture = "";
            AdviceVerdict = "";
            AdviceNote = "";
            ProposedScalePx = double.NaN;
            _removedRaster = null;
            _afterResidual = null;
            _mapCache.Clear();
        }
    }
}
