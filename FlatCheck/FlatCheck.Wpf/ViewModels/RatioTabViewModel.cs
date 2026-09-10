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

// RatioTabViewModel.cs - tabul 2, atribuirea prin rotatie la 180 de grade.
//
// DE CE NU MAI CAUTA IN FOLDER. Pana la rescriere, tabul isi umplea o lista din
// process/ si preselecta doua fisiere. Era gresit din constructie: modul cere doua
// MASTERE, cate unul pentru fiecare orientare, iar un folder de sesiune Siril nu
// contine niciodata asa ceva - cele doua orientari sunt doua sedinte, deci doua foldere.
// Ce oferea lista erau cadrele individuale, si o pereche de cadre individuale produce
// un rezultat sigur si fals: zgomotul unui singur cadru e de acelasi ordin cu semnalul
// cautat. Deci acum se cer explicit doua fisiere, si nimic nu se presupune.
//
// Singurul tab care NU ruleaza la pornire, si acum cu si mai multa dreptate: pana nu
// alege omul doua fisiere, nu exista nimic de calculat. Pana atunci arata documentatia -
// vezi SessionMessages.RatioNeedsTwoMasters.

using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace FlatCheck.Wpf
{
    public sealed class RatioTabViewModel : TabViewModel
    {
        SirilSession _session;
        CancellationToken _ct;

        string _fileA;
        string _fileB;
        IReadOnlyList<MetricRow> _worldRows = System.Array.Empty<MetricRow>();
        string _provenanceA = "";
        string _provenanceB = "";
        IReadOnlyList<MetricRow> _tests = System.Array.Empty<MetricRow>();
        IReadOnlyList<MetricRow> _informative = System.Array.Empty<MetricRow>();
        string _localSpotWarning = "";
        string _verdict = "";
        string _advice = "";
        string _mapLegend = "";
        bool _showWorldMap;

        /// <summary>
        /// Cele doua rastere, nu octetii: harta se reencodeaza la schimbarea scarii, si
        /// amandoua vin deja in rezultat. Vezi nota din StructureTabViewModel.
        /// </summary>
        Img _sensorResidual;
        Img _worldRaster;

        /// <summary>Encodarile deja facute, pe (harta, plafon).</summary>
        readonly Dictionary<(bool World, MapScaleOption Scale), byte[]> _mapCache = new();

        public RatioTabViewModel()
        {
            MapScale.Changed += RefreshMap;
        }

        public string FileA { get => _fileA; private set => Set(ref _fileA, value); }
        public string FileB { get => _fileB; private set => Set(ref _fileB, value); }

        /// <summary>
        /// Componenta fixa in lume: cifra cu bara ei de eroare si eticheta informativa, si
        /// imprastierea colturilor. Doua randuri MetricRow, ca restul ecranului, fiecare cu
        /// descrierea lui din nucleu - stateau ca doua Grid-uri scrise de mana, unul fara
        /// niciun cuvant sub el si celalalt cu o pastila colorata care arata a verdict fara
        /// sa fie. Lista inlocuibila, ca <see cref="Tests"/>, din acelasi motiv.
        /// </summary>
        public IReadOnlyList<MetricRow> WorldRows { get => _worldRows; private set => Set(ref _worldRows, value); }

        /// <summary>Cate cadre si de cand, din headerele FITS. Precondistia se vede aici.</summary>
        public string ProvenanceA { get => _provenanceA; private set => Set(ref _provenanceA, value); }
        public string ProvenanceB { get => _provenanceB; private set => Set(ref _provenanceB, value); }

        /// <summary>
        /// Bateria de structura pe masterul combinat - aceleasi cinci randuri ca tabul 1,
        /// construite de <see cref="StructureRows"/>, deci cu aceleasi etichete, aceeasi
        /// ordine si aceleasi descrieri. Erau zece proprietati si cinci blocuri de Grid
        /// scrise de mana in XAML, si de aceea au ramas fara descrieri cand tabul 1 le-a
        /// capatat.
        ///
        /// LISTA SE INLOCUIESTE, nu se goleste si se reumple - si asta nu e un detaliu:
        /// Choose() porneste RecomputeAsync fara sa astepte, iar tokenul e al sesiunii, nu
        /// al rularii, deci doua alegeri rapide lasa doua calcule in aer si NICIUNUL nu-l
        /// anuleaza pe celalalt. O ObservableCollection golita si reumpluta de amandoua ar
        /// iesi cu randuri duplicate; o referinta inlocuita lasa pur si simplu ultimul sa
        /// castige. Acelasi motiv ca la Histograms pe tabul de expunere.
        /// </summary>
        public IReadOnlyList<MetricRow> Tests { get => _tests; private set => Set(ref _tests, value); }

        /// <summary>
        /// Marimile fara verdict ale masterului combinat - vignetare, centru de iluminare,
        /// pete locale - exact ce tipareste consola sub INFORMATIV in raportul --ratio.
        /// Aceeasi lista inlocuibila ca <see cref="Tests"/>, din acelasi motiv.
        /// </summary>
        public IReadOnlyList<MetricRow> Informative { get => _informative; private set => Set(ref _informative, value); }

        /// <summary>
        /// Atentionarea de praf pe masterul combinat, sau gol. Ca in tabul 1: sta sub
        /// verdict fara sa-l coloreze, cu textul din StructureReport.LocalSpotWarning.
        /// </summary>
        public string LocalSpotWarning { get => _localSpotWarning; private set => Set(ref _localSpotWarning, value); }

        public string Verdict { get => _verdict; private set => Set(ref _verdict, value); }
        public string Advice { get => _advice; private set => Set(ref _advice, value); }

        /// <summary>
        /// Cele doua harti si comutatorul dintre ele. Modul desparte masterul in doua
        /// componente, deci are doua forme de aratat: reziduul partii fixe pe aparat, care
        /// se potriveste cu verdictul, si raportul A/B, care arata forma gradientului
        /// extern. <see cref="Map"/> e cea afisata.
        /// </summary>
        public byte[] Map { get => _map; private set => Set(ref _map, value); }

        byte[] _map;

        public bool ShowWorldMap
        {
            get => _showWorldMap;
            set { if (Set(ref _showWorldMap, value)) RefreshMap(); }
        }

        /// <summary>Comutatorul de scara. Se aplica hartii afisate, oricare din cele doua e.</summary>
        public MapScaleViewModel MapScale { get; } = new();

        /// <summary>
        /// Legenda urmeaza harta AFISATA: pe scara automata cele doua componente au
        /// intervale diferite, si tocmai capetele reale sunt ce trebuie spus.
        /// </summary>
        public string MapLegend { get => _mapLegend; private set => Set(ref _mapLegend, value); }

        /// <summary>Acelasi ghid ca la tabul de structura: acelasi reziduu, aceeasi scara.</summary>
        public MapGuideViewModel MapGuide { get; } = new();

        /// <summary>
        /// Reface harta pentru (componenta afisata, scara curenta). Vezi nota din
        /// StructureTabViewModel despre de ce se tine rasterul si nu octetii.
        /// </summary>
        void RefreshMap()
        {
            var raster = _showWorldMap ? _worldRaster : _sensorResidual;
            if (raster == null)
            {
                Map = null;
                MapLegend = "";
                return;
            }

            var range = MapScale.RangeFor(raster);
            var key = (_showWorldMap, MapScale.Selected);
            if (!_mapCache.TryGetValue(key, out var png))
            {
                png = PngWriter.EncodeResidualMap(raster, range);
                _mapCache[key] = png;
            }

            Map = png;
            MapLegend = RunReport.MapLegend(range);
        }

        // ------------------------------------------------------------ textele fixe
        // Vin din nucleu, nu se rescriu aici: aceleasi propozitii sunt in raportul salvat.

        // Proprietati de INSTANTA, desi valoarea e constanta: {Binding} rezolva prin
        // DataContext, deci nu vede proprietati statice - o legare la una ar esua tacut,
        // si tocmai documentatia tabului ar fi ramas goala.
        public string Documentation => SessionMessages.RatioNeedsTwoMasters();
        public string Precondition => RatioReport.Precondition();
        public string WorldMeaning => RatioReport.WorldMeaning();
        public string SensorCaveat => RatioReport.SensorCaveat();

        /// <summary>
        /// Retine sesiunea si atat. Nu propune nimic si nu calculeaza nimic: cele doua
        /// mastere vin din doua sedinte, iar folderul curent e cel mult unul dintre ele.
        /// </summary>
        internal void Attach(SirilSession session, CancellationToken ct)
        {
            _session = session;
            _ct = ct;
            HasResult = false;
            ClearError();
        }

        /// <summary>Folderul de la care porneste dialogul pentru setul A.</summary>
        internal string InitialDirectory => _session != null && Directory.Exists(_session.ProcessDir)
            ? _session.ProcessDir
            : null;

        /// <summary>
        /// Un fisier ales de om. Se valideaza dupa nume, iar daca nu e un master de flat
        /// se explica ce sunt fisierele astea si de ce testul cere unul.
        /// </summary>
        internal void Choose(bool isA, string path)
        {
            if (string.IsNullOrEmpty(path)) return;

            if (!SirilSession.IsFlatMasterName(path))
            {
                Fail("Fisierul ales nu e un master de flat", SessionMessages.NotAFlatMaster(path));
                return;
            }

            // Prins la alegere, nu la calcul: asa omul vede imediat ce a gresit, si
            // selectia buna de dinainte nu se pierde.
            string other = isA ? FileB : FileA;
            if (string.Equals(path, other, System.StringComparison.OrdinalIgnoreCase))
            {
                Fail("Aceleasi doua fisiere", SessionMessages.SameFileTwice());
                return;
            }

            ClearError();
            if (isA) FileA = path; else FileB = path;
            if (!string.IsNullOrEmpty(FileA) && !string.IsNullOrEmpty(FileB))
                _ = RecomputeAsync();
        }

        internal Task RecomputeAsync()
        {
            if (string.IsNullOrEmpty(FileA) || string.IsNullOrEmpty(FileB))
            {
                HasResult = false;
                return Task.CompletedTask;
            }

            string a = FileA, b = FileB;

            return RunGuardedAsync("Compar cele doua mastere de flat...", async () =>
            {
                var text = new StringWriter();
                var outcome = await Task.Run(() =>
                {
                    RunReport.WriteBanner(text);
                    return RatioRun.Execute(a, b, text);
                }, _ct).ConfigureAwait(true);

                Report = text.ToString();
                Populate(outcome.Result);

                // Rasterele, plus octetii pe scara automata direct in cache - sunt exact
                // ce ar reencoda RefreshMap pentru chipul "auto".
                _sensorResidual = outcome.Result.Sensor.Residual;
                _worldRaster = outcome.Result.World;
                _mapCache.Clear();
                _mapCache[(false, MapScale.Options[0])] = outcome.Png;
                _mapCache[(true, MapScale.Options[0])] = outcome.SecondaryPng;
                RefreshMap();

                HasResult = true;
            });
        }

        void Populate(RatioResult r)
        {
            WorldRows = new[]
            {
                new MetricRow
                {
                    Label = "gradient extern / orientare",
                    Value = RatioReport.WorldValue(r),
                    Verdict = r.WorldLabel,          // informativa; nota spune ca nu voteaza
                    Note = RatioReport.WorldNote(),
                },
                new MetricRow
                {
                    Label = "imprastiere colturi",
                    Value = StructureRows.Pct(r.WorldCornerPct),
                    Note = RatioReport.WorldCornerNote(),
                },
            };
            ProvenanceA = RatioReport.Provenance(r.StackCountA, r.DateObsA);
            ProvenanceB = RatioReport.Provenance(r.StackCountB, r.DateObsB);

            Tests = StructureRows.Tests(r.Sensor);
            Informative = StructureRows.Informative(r.Sensor);
            LocalSpotWarning = StructureReport.LocalSpotWarning(r.Sensor) ?? "";

            Verdict = r.Verdict;
            // Sfatul vine din nucleu, ca la tabul de structura: e acelasi verdict, pe
            // aceeasi scara, deci trebuie sa fie aceeasi fraza.
            Advice = StructureReport.Advice(r.Verdict);
        }

        internal override void Reset()
        {
            base.Reset();
            _session = null;
            FileA = null;
            FileB = null;
            WorldRows = System.Array.Empty<MetricRow>();
            ProvenanceA = ProvenanceB = "";
            Tests = System.Array.Empty<MetricRow>();
            Informative = System.Array.Empty<MetricRow>();
            LocalSpotWarning = "";
            Verdict = "";
            Advice = "";
            // Rasterele si encodarile pleaca; alegerea de scara ramane, ca la Structura.
            _sensorResidual = null;
            _worldRaster = null;
            _mapCache.Clear();
            ShowWorldMap = false;
            RefreshMap();
        }
    }
}
