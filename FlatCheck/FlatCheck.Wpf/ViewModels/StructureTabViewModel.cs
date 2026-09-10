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

// StructureTabViewModel.cs - tabul 1.
//
// Ruleaza StructureRun pe process/pp_flat_stacked.fit si arata acelasi lucru pe care il
// scrie consola, doar asezat altfel. Cifrele NU se recalculeaza aici: fiecare rand ia o
// valoare din StructureResult si o formateaza. Singurul lucru care se compune aici sunt
// procentele de colt, si alea prin acelasi raport ca in raport.
//
// Pe langa afisare, tabul are o obligatie de disc: salveaza raportul in folderul
// sesiunii, ca fisier text, IDENTIC cu ce ar fi scris consola - antet de versiune,
// copyright, raport, linia hartii. De aia StructureRun primeste un StringWriter si nu
// isi compune nimeni textul de mana.
//
// HARTA SE REENCODEAZA LA SCHIMBAREA SCARII, deci tabul tine rasterul rezidual, nu doar
// octetii PNG primiti de la rulare. Rasterul e la rezolutia de analiza (~1500x1000
// double, vreo 12 MB), si e deja in StructureResult - deci nu se incarca si nu se
// recalculeaza nimic pentru o schimbare de scara. Alternativa - patru PNG-uri encodate
// din nucleu la fiecare rulare - ar fi fost ~7 MB si trei encodari pe care poate nu le
// priveste nimeni.
//
// FISIERUL SALVAT RAMANE PE SCARA AUTOMATA, oricare chip e bifat. Consola scrie auto, si
// cele doua aplicatii trebuie sa scrie acelasi fisier pentru acelasi master; chipurile
// sunt un ajutor de privit, nu o setare de iesire.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FlatCheck.Wpf
{
    public sealed class StructureTabViewModel : TabViewModel
    {
        string _source = "";
        string _size = "";
        string _levels = "";
        string _verdict = "";
        string _advice = "";
        string _localSpotWarning = "";
        string _whyResidual = StructureReport.WhyResidualMatters();
        string _savedTo = "";
        string _saveError = "";
        byte[] _map;
        string _mapLegend = "";

        /// <summary>Rasterul din care se deseneaza harta, tinut ca s-o poata reencoda pe alta scara.</summary>
        Img _residual;

        /// <summary>Encodarile deja facute, ca sa nu se reencodeze la fiecare click inainte si inapoi.</summary>
        readonly Dictionary<MapScaleOption, byte[]> _mapCache = new();

        public StructureTabViewModel()
        {
            MapScale.Changed += RefreshMap;
        }

        /// <summary>Fisierul analizat, ca sa se vada pe ce s-a lucrat.</summary>
        public string Source { get => _source; private set => Set(ref _source, value); }

        public string Size { get => _size; private set => Set(ref _size, value); }
        public string Levels { get => _levels; private set => Set(ref _levels, value); }

        public string Verdict { get => _verdict; private set => Set(ref _verdict, value); }
        public string Advice { get => _advice; private set => Set(ref _advice, value); }

        /// <summary>
        /// Atentionarea pentru o pata compacta (praf, condens, scurgere mica), sau gol.
        /// Sta sub verdict fara sa-l coloreze: nu e un verdict, e o intrebare pusa
        /// omului - flat de sesiune sau de biblioteca? Textul vine din
        /// StructureReport.LocalSpotWarning, ca sa fie cel din raportul salvat.
        /// </summary>
        public string LocalSpotWarning { get => _localSpotWarning; private set => Set(ref _localSpotWarning, value); }

        /// <summary>Constant, dar tinut ca proprietate ca sa se poata lega din XAML.</summary>
        public string WhyResidual { get => _whyResidual; private set => Set(ref _whyResidual, value); }

        /// <summary>Harta reziduala ca octeti PNG - se afiseaza fara sa atinga discul.</summary>
        public byte[] Map { get => _map; private set => Set(ref _map, value); }

        /// <summary>Comutatorul de scara al hartii. Nu atinge nicio cifra, doar culorile.</summary>
        public MapScaleViewModel MapScale { get; } = new();

        /// <summary>Ce inseamna capetele culorilor pe scara chiar afisata.</summary>
        public string MapLegend { get => _mapLegend; private set => Set(ref _mapLegend, value); }

        /// <summary>Ghidul de citire al hartii. Documentatie, nu diagnostic - vezi MapGuideViewModel.</summary>
        public MapGuideViewModel MapGuide { get; } = new();

        /// <summary>Ce decid cifrele si ce decide ochiul. Sta sub verdict, in coloana cifrelor.</summary>
        public HowToReadViewModel HowToRead { get; } = new();

        /// <summary>Unde s-a salvat raportul, sau gol daca inca nu s-a salvat.</summary>
        public string SavedTo { get => _savedTo; private set => Set(ref _savedTo, value); }

        /// <summary>
        /// Salvarea poate esua din motive care nu au legatura cu analiza (folder read-only,
        /// fisier deschis in alt program). Se raporteaza separat, ca sa nu para ca a picat
        /// masuratoarea cand de fapt cifrele sunt bune si doar fisierul n-a putut fi scris.
        /// </summary>
        public string SaveError { get => _saveError; private set => Set(ref _saveError, value); }

        public ObservableCollection<MetricRow> Tests { get; } = new();
        public ObservableCollection<MetricRow> Informative { get; } = new();
        public ObservableCollection<ScaleRow> Scale { get; } = new();

        /// <summary>
        /// Ruleaza modul si salveaza raportul. Sesiunea da si fisierul de intrare, si
        /// locul de iesire - tabul nu alege singur nicio cale.
        /// </summary>
        internal Task RunAsync(SirilSession session, CancellationToken ct)
            => RunGuardedAsync("Analizez structura masterului de flat...", async () =>
            {
                string path = session.FlatMasterPath;
                var text = new StringWriter();

                var outcome = await Task.Run(() =>
                {
                    RunReport.WriteBanner(text);
                    var run = StructureRun.Execute(path, text);
                    RunReport.WriteMapSaved(session.ResidualPngPath, text);
                    ct.ThrowIfCancellationRequested();
                    return run;
                }, ct).ConfigureAwait(true);

                Source = path;
                Report = text.ToString();
                Populate(outcome.Result);

                // Rasterul, apoi harta pe scara aleasa. Octetii primiti de la rulare sunt
                // cei pe scara automata: se pun direct in cache, ca sa nu se reencodeze
                // aceeasi imagine cand chipul e chiar "auto".
                _residual = outcome.Result.Residual;
                _mapCache.Clear();
                _mapCache[MapScale.Options[0]] = outcome.Png;
                RefreshMap();

                HasResult = true;

                // Pe disc merge mereu encodarea automata, oricare chip e bifat. Vezi nota
                // de sus: consola scrie asta, si fisierul trebuie sa fie acelasi.
                Save(session, outcome.Png);
            });

        /// <summary>
        /// Reface harta pentru scara curenta.
        ///
        /// Se encodeaza pe firul de interfata, si asta a fost masurat, nu presupus: pe
        /// masterele reale de 24 MP (raster de analiza 1504x1004) un plafon costa 90-255 ms,
        /// o singura data, fiindca pe urma vine din cache. Mutarea pe Task.Run ar fi cerut
        /// un contor de generatii ca sa nu ajunga un rezultat vechi peste unul nou - mai
        /// multa complexitate decat intarzierea pe care o scoate.
        /// </summary>
        void RefreshMap()
        {
            if (_residual == null) return;

            var range = MapScale.RangeFor(_residual);
            if (!_mapCache.TryGetValue(MapScale.Selected, out var png))
            {
                png = PngWriter.EncodeResidualMap(_residual, range);
                _mapCache[MapScale.Selected] = png;
            }

            Map = png;
            MapLegend = RunReport.MapLegend(range);
        }

        void Populate(StructureResult r)
        {
            Size = $"{r.Width} x {r.Height} px  (dupa 2x2 + downsample)";
            // Formatarea vine din nucleu: pe un master float scris de Siril, "F0" dadea
            // "0 / 0", iar fereastra si consola trebuie sa spuna acelasi lucru.
            Levels = $"median / max ADU  {StructureReport.Levels(r)}";

            // Cele cinci randuri se construiesc in StructureRows, de unde le ia si tabul
            // Ratio: e aceeasi baterie, si doua compuneri ale ei au divergat deja o data.
            Tests.Clear();
            foreach (var row in StructureRows.Tests(r)) Tests.Add(row);

            // Si randurile informative vin din StructureRows, tot pentru amandoua taburile.
            Informative.Clear();
            foreach (var row in StructureRows.Informative(r)) Informative.Add(row);

            // Atentionarea, nu un verdict: textul si pragul vin din nucleu.
            LocalSpotWarning = StructureReport.LocalSpotWarning(r) ?? "";

            Scale.Clear();
            foreach (var (band, range, meaning) in StructureReport.Scale())
                Scale.Add(new ScaleRow
                {
                    Band = band,
                    Range = range,
                    Meaning = meaning,
                    IsCurrent = band == r.Verdict,
                });

            Verdict = r.Verdict;
            Advice = StructureReport.Advice(r.Verdict);
        }

        /// <summary>Formatarea unei cifre, comuna cu randurile de test.</summary>
        static string Pct(double v) => StructureRows.Pct(v);

        /// <summary>
        /// Raportul si harta, in folderul sesiunii. Esecul de scriere nu anuleaza
        /// rezultatul - cifrele sunt deja calculate si afisate.
        /// </summary>
        void Save(SirilSession session, byte[] png)
        {
            SavedTo = "";
            SaveError = "";
            try
            {
                File.WriteAllText(session.ResultTextPath, Report);
                File.WriteAllBytes(session.ResidualPngPath, png);
                SavedTo = session.ResultTextPath;
            }
            catch (Exception e)
            {
                SaveError = "Nu am putut salva raportul:\n" + Describe(e);
            }
        }

        internal override void Reset()
        {
            base.Reset();
            Source = "";
            Size = "";
            Levels = "";
            Verdict = "";
            Advice = "";
            LocalSpotWarning = "";
            SavedTo = "";
            SaveError = "";
            Map = null;
            MapLegend = "";
            // Rasterul si encodarile pleaca; alegerea de scara NU - e o preferinta de
            // privit, nu un rezultat, deci trebuie sa treaca peste schimbarea folderului.
            _residual = null;
            _mapCache.Clear();
            Tests.Clear();
            Informative.Clear();
            Scale.Clear();
        }
    }
}
