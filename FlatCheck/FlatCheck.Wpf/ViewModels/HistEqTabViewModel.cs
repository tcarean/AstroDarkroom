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

// HistEqTabViewModel.cs - tabul "Histogram": un cadru vazut prin egalizare de histograma.
//
// Lista are masterul din process/ pe prima pozitie si apoi cadrele din flats/, raw sau
// FITS. Masterul e acolo pentru o singura intrebare - s-au mediat imperfectiunile de pe
// cadrele individuale? - si nu se preselecteaza si nu ruleaza singur la deschidere: e un
// FITS float de 24 MP, iar tabul Structura il citeste deja atunci (regula "secvential, nu
// paralel" din MainViewModel).
//
// LOADER-UL ACOPERA POZA, NU TABUL. Lista ramane apasabila cat timp se calculeaza, si
// un click pe alt fisier ANULEAZA rularea curenta si porneste alta. Ca o rulare depasita
// sa nu atinga ecranul, contorul de generatii din TabViewModel.RunGuardedAsync lasa doar
// ultima rulare sa incheie starea de ocupat, iar lambda de aici verifica jetonul propriu
// dupa await inainte sa scrie ceva.
//
// POZA NU TRECE PRIN PNG. Vederea aleasa se face bitmap direct din rasterul la rezolutie
// intreaga (RasterBitmaps), fara encodare: la 100 % un pixel de pe ecran e un pixel de
// senzor, si pe mozaic se vede tabla de sah ca in Siril. PNG-ul ramane al consolei.
// Prima varianta encoda PNG-uri din rasterele micsorate (150-380 ms si 0.85-3.6 MB pe
// vedere, masurat), si zoom-ul ar fi marit medii de blocuri. Bitmap-urile se tin in cache
// pe chip, inghetate; costul e memoria rasterului, nimic altceva.
//
// Zoom-ul se reseteaza la FISIER nou, nu la schimbarea vederii: a te uita la acelasi loc
// pe G1 dupa Mozaic e exact rostul chipurilor.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;

namespace FlatCheck.Wpf
{
    public sealed class HistEqTabViewModel : TabViewModel
    {
        SirilSession _session;
        CancellationToken _parent;
        CancellationTokenSource _runCts;

        HistEqFileRow _selected;
        ImageSource _image;
        string _caption = "";
        string _frame = "";
        string _channels = "";
        string _masterNote = "";

        /// <summary>Ultimul rezultat, ca vederile sa se poata comuta fara sa se reciteasca fisierul.</summary>
        HistEqResult _last;

        readonly Dictionary<HistEqViewOption, ImageSource> _cache = new();

        public HistEqTabViewModel()
        {
            View.Changed += RefreshImage;
        }

        /// <summary>Masterul intai, apoi cadrele din flats/.</summary>
        public ObservableCollection<HistEqFileRow> Files { get; } = new();

        public HistEqFileRow Selected
        {
            get => _selected;
            set { if (Set(ref _selected, value)) Recompute(); }
        }

        /// <summary>Bitmap-ul vederii alese, la rezolutie intreaga, sau null.</summary>
        public ImageSource Image { get => _image; private set => Set(ref _image, value); }

        /// <summary>Comutatorul de vederi: mozaicul, RGB, pozitiile.</summary>
        public HistEqViewViewModel View { get; } = new();

        /// <summary>Potrivit sau un factor; controlul din XAML il citeste si il scrie.</summary>
        public ImageZoomViewModel Zoom { get; } = new();

        /// <summary>Ghidul de citire. Documentatie, nu diagnostic.</summary>
        public HistEqGuideViewModel Guide { get; } = new();

        /// <summary>Ce e vederea de pe ecran si la ce marime.</summary>
        public string Caption { get => _caption; private set => Set(ref _caption, value); }

        /// <summary>Ce NU se poate citi din poza. Proprietate de instanta: {Binding} nu vede una statica.</summary>
        public string Caveat => HistEqReport.Caveat();

        /// <summary>Marimea si felul cadrului, din raport.</summary>
        public string Frame { get => _frame; private set => Set(ref _frame, value); }

        /// <summary>Tabelul canalelor, din raport, un rand pe linie.</summary>
        public string Channels { get => _channels; private set => Set(ref _channels, value); }

        /// <summary>Ce inseamna sa te uiti la master; gol cand e ales un cadru.</summary>
        public string MasterNote { get => _masterNote; private set => Set(ref _masterNote, value); }

        /// <summary>Ultima rulare pornita, ca un test sa poata astepta una pe care n-a pornit-o el.</summary>
        internal Task Pending { get; private set; } = Task.CompletedTask;

        internal void Attach(SirilSession session, CancellationToken ct)
        {
            _session = session;
            _parent = ct;

            Files.Clear();
            foreach (HistEqCandidate c in session.HistEqCandidates())
                Files.Add(new HistEqFileRow
                {
                    Path = c.Path,
                    Name = Path.GetFileName(c.Path),
                    // folderul real al fisierului: masterul poate fi in masters/ sau process/
                    Where = Path.GetFileName(Path.GetDirectoryName(c.Path)) + "/",
                    IsMaster = c.IsMaster,
                });

            _selected = null;
            Raise(nameof(Selected));

            if (Files.Count == 0)
                Fail("Nimic de egalizat", SessionMessages.NoFlatFrames(session));
            else
                ClearError();
        }

        void Recompute()
        {
            if (_session == null || Selected == null) return;
            Pending = RecomputeAsync();
        }

        internal Task RecomputeAsync()
        {
            HistEqFileRow row = Selected;
            if (row == null) return Task.CompletedTask;

            // Rularea de dinainte, daca mai merge, e depasita: jetonul ei se anuleaza, iar
            // lambda ei se opreste la prima verificare de dupa await, fara sa scrie nimic.
            CancellationTokenSource old = _runCts;
            old?.Cancel();
            old?.Dispose();
            var cts = CancellationTokenSource.CreateLinkedTokenSource(_parent);
            _runCts = cts;
            CancellationToken ct = cts.Token;

            return RunGuardedAsync($"Egalizez histograma pe {row.Name}...", async () =>
            {
                var text = new StringWriter();
                var outcome = await Task.Run(() =>
                {
                    RunReport.WriteBanner(text);
                    var run = HistEqRun.Execute(row.Path, text);
                    ct.ThrowIfCancellationRequested();
                    return run;
                }, ct).ConfigureAwait(true);

                // Inapoi pe firul de interfata: daca intre timp s-a ales alt fisier, jetonul
                // asta e anulat si rezultatul nu mai are ce cauta pe ecran.
                ct.ThrowIfCancellationRequested();

                Report = text.ToString();
                Populate(outcome.Result, row.IsMaster);
            });
        }

        /// <summary>
        /// Internal, nu private, ca testele de randare sa poata aduce un rezultat construit
        /// direct cu HistEqAnalyzer, fara fisier. Fisier nou = zoom la potrivit.
        /// </summary>
        internal void Populate(HistEqResult r, bool isMaster = false)
        {
            _last = r;
            _cache.Clear();
            View.Rebuild(r);
            Zoom.Reset();

            Frame = HistEqReport.FrameLine(r);
            Channels = string.Join("\n", Enumerable.Range(0, r.Channels.Length).Select(i => HistEqReport.ChannelLine(r, i)));
            MasterNote = isMaster ? HistEqReport.MasterVersusFrame() : "";

            RefreshImage();
            HasResult = true;
        }

        void RefreshImage()
        {
            HistEqResult r = _last;
            HistEqViewOption option = View.Selected;
            if (r == null || option == null) return;

            if (!_cache.TryGetValue(option, out ImageSource bitmap))
            {
                bitmap = option.Kind switch
                {
                    HistEqViewKind.Channel => RasterBitmaps.Grey(r.Channels[option.Index].Full, r.FullW, r.FullH),
                    HistEqViewKind.Mosaic => RasterBitmaps.Grey(r.MosaicFull, r.SourceW, r.SourceH),
                    _ => RasterBitmaps.Rgb(r.CompositeFullRgb, r.FullW, r.FullH),
                };
                _cache[option] = bitmap;
            }

            Image = bitmap;
            Caption = HistEqReport.ViewCaption(r, option.Label);
        }

        internal override void Reset()
        {
            CancellationTokenSource cts = _runCts;
            _runCts = null;
            try { cts?.Cancel(); } catch (ObjectDisposedException) { }
            cts?.Dispose();

            _last = null;
            _cache.Clear();
            Image = null;
            Zoom.Reset();
            Caption = "";
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
