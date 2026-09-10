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
        string _world = "";
        string _worldLabel = "";
        string _worldCorner = "";
        string _provenanceA = "";
        string _provenanceB = "";
        string _corners = "";
        string _cornerVerdict = "";
        string _azimuthal = "";
        string _azimuthalVerdict = "";
        string _residual = "";
        string _residualVerdict = "";
        string _tilt = "";
        string _tiltVerdict = "";
        string _maxTile = "";
        string _maxTileVerdict = "";
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

        /// <summary>Componenta fixa in lume, cu bara ei de eroare. Text din nucleu.</summary>
        public string World { get => _world; private set => Set(ref _world, value); }
        public string WorldLabel { get => _worldLabel; private set => Set(ref _worldLabel, value); }
        public string WorldCorner { get => _worldCorner; private set => Set(ref _worldCorner, value); }

        /// <summary>Cate cadre si de cand, din headerele FITS. Precondistia se vede aici.</summary>
        public string ProvenanceA { get => _provenanceA; private set => Set(ref _provenanceA, value); }
        public string ProvenanceB { get => _provenanceB; private set => Set(ref _provenanceB, value); }

        // Bateria de structura pe masterul combinat - aceleasi cinci teste ca tabul 1.
        public string Corners { get => _corners; private set => Set(ref _corners, value); }
        public string CornerVerdict { get => _cornerVerdict; private set => Set(ref _cornerVerdict, value); }
        public string Azimuthal { get => _azimuthal; private set => Set(ref _azimuthal, value); }
        public string AzimuthalVerdict { get => _azimuthalVerdict; private set => Set(ref _azimuthalVerdict, value); }
        public string Residual { get => _residual; private set => Set(ref _residual, value); }
        public string ResidualVerdict { get => _residualVerdict; private set => Set(ref _residualVerdict, value); }
        public string Tilt { get => _tilt; private set => Set(ref _tilt, value); }
        public string TiltVerdict { get => _tiltVerdict; private set => Set(ref _tiltVerdict, value); }
        public string MaxTile { get => _maxTile; private set => Set(ref _maxTile, value); }
        public string MaxTileVerdict { get => _maxTileVerdict; private set => Set(ref _maxTileVerdict, value); }

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
            World = RatioReport.WorldValue(r);
            WorldLabel = r.WorldLabel;
            WorldCorner = $"{r.WorldCornerPct:F2} %";
            ProvenanceA = RatioReport.Provenance(r.StackCountA, r.DateObsA);
            ProvenanceB = RatioReport.Provenance(r.StackCountB, r.DateObsB);

            Corners = $"{r.Sensor.CornerSpreadPct:F2} %";
            CornerVerdict = r.Sensor.CornerVerdict;
            Azimuthal = $"{r.Sensor.AzimuthalMaxPct:F2} %";
            AzimuthalVerdict = r.Sensor.AzimuthalVerdict;
            Residual = $"{r.Sensor.ResidualPtpPct:F2} %";
            ResidualVerdict = r.Sensor.ResidualVerdict;
            Tilt = $"{r.Sensor.TiltPct:F2} %";
            TiltVerdict = r.Sensor.TiltVerdict;
            MaxTile = $"{r.Sensor.MaxTilePct:F2} %";
            MaxTileVerdict = r.Sensor.MaxTileVerdict;

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
            World = WorldLabel = WorldCorner = "";
            ProvenanceA = ProvenanceB = "";
            Corners = Azimuthal = Residual = Tilt = MaxTile = "";
            CornerVerdict = AzimuthalVerdict = ResidualVerdict = TiltVerdict = MaxTileVerdict = "";
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
