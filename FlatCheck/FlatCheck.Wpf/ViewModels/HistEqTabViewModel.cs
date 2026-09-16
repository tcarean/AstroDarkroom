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
// Lista are masterul pe prima pozitie, flat-ul netezit de langa el cand exista, si apoi
// cadrele din flats/, raw sau FITS. Masterul e acolo pentru o singura intrebare - s-au
// mediat imperfectiunile de pe cadrele individuale? - si nu se preselecteaza si nu ruleaza
// singur la deschidere: e un FITS float de 24 MP, iar tabul Structura il citeste deja
// atunci (regula "secvential, nu paralel" din MainViewModel). Netezitul e acolo pentru
// cealalta intrebare - ce a scos netezirea si ce a lasat - si e un al treilea fel de rand,
// nu al doilea master: chip NETEZIT, IsMaster fals.
//
// LISTA SE REFACE CAND NETEZIREA SCRIE. Tabul Netezire poate produce fisierul in timpul
// sesiunii; MainViewModel leaga evenimentul lui la FileWritten, care reciteste lista din
// sesiune - o singura definitie a listei, HistEqCandidates - si pastreaza alegerea, fara
// sa porneasca nimic. Cu o exceptie: daca fisierul rescris e chiar cel ales, se
// recalculeaza, altfel poza de pe ecran ar fi a unor octeti care nu mai exista pe disc.
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

        /// <summary>Masterul intai, netezitul cand exista, apoi cadrele din flats/.</summary>
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

            FillFiles();

            _selected = null;
            Raise(nameof(Selected));

            if (Files.Count == 0)
                Fail("Nimic de egalizat", SessionMessages.NoFlatFrames(session));
            else
                ClearError();
        }

        /// <summary>Lista, din sesiune: masterul, netezitul cand exista, cadrele.</summary>
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
                    // folderul real al fisierului: masterul poate fi in masters/ sau process/
                    Where = Path.GetFileName(Path.GetDirectoryName(c.Path)) + "/",
                    Kind = c.Kind,
                    ClosesGroup = i == lastOfGroup,
                });
            }
        }

        /// <summary>
        /// Alt tab a scris un fisier. Daca e flat-ul netezit al sesiunii curente, lista se
        /// reface si alegerea se pastreaza (dupa cale); daca fisierul ales e chiar cel
        /// rescris, se recalculeaza. Orice alta cale, sau lipsa sesiunii, nu face nimic.
        /// </summary>
        internal void FileWritten(string path)
        {
            if (_session == null || path == null) return;
            if (!string.Equals(path, _session.SmoothedMasterPath, StringComparison.OrdinalIgnoreCase)) return;

            string selectedPath = Selected?.Path;

            // Cu un ListBox legat in doua sensuri, Files.Clear() impinge null in setter-ul
            // lui Selected chiar acum; Recompute() se opreste la Selected == null fara sa
            // atinga _runCts, deci o rulare in curs nu e anulata si Populate-ul ei ajunge pe
            // ecran ca pana acum. Alegerea se pune apoi direct in camp, nu prin setter, ca
            // sa nu porneasca o rulare pe un fisier care nu s-a schimbat.
            FillFiles();
            _selected = selectedPath == null ? null
                : Files.FirstOrDefault(r => string.Equals(r.Path, selectedPath, StringComparison.OrdinalIgnoreCase));
            Raise(nameof(Selected));

            if (_selected != null && string.Equals(selectedPath, path, StringComparison.OrdinalIgnoreCase))
                Recompute();
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
                Populate(outcome.Result, row.Kind);
            });
        }

        /// <summary>
        /// Internal, nu private, ca testele de randare sa poata aduce un rezultat construit
        /// direct cu HistEqAnalyzer, fara fisier. Fisier nou = zoom la potrivit.
        /// </summary>
        internal void Populate(HistEqResult r, HistEqKind kind = HistEqKind.Frame)
        {
            _last = r;
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

            // fara sesiune, FileWritten nu mai are ce reface: un folder inchis nu se
            // umple la loc fiindca Netezirea a scris in el
            _session = null;
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
