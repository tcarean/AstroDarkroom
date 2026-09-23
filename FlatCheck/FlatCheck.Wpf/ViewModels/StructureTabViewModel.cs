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
//
// VIGNETAREA FATA DE PROFILUL OBIECTIVULUI se compara in aceeasi rulare, cand exista un
// raw in flats/ (SirilSession.ReferenceRawPath): rularea e atunci exact "flatcheck master
// --raw <acel raw>", cu catalogul comun al ferestrei, deci raportul salvat ramane cel al
// consolei si verdictul apare o singura data, gata combinat - nu PERFECT pe cinci teste
// si REFA doua secunde mai tarziu. Fara raw rularea e cea simpla, identica cu "flatcheck
// master", iar cardul spune de ce nu s-a putut compara (textul nucleului, nu al tabului).

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
        string _sourceWarning = "";
        string _size = "";
        string _levels = "";
        string _verdict = "";
        string _advice = "";
        string _localSpotWarning = "";
        string _verdictDetail = "";
        string _whyResidual = StructureReport.WhyResidualMatters();
        string _profileMaster = "", _profileSummary = "", _profileScaleNote = "", _profileNote = "", _profileGap = "";
        IReadOnlyList<ScaleRow> _profileScale = Array.Empty<ScaleRow>();
        IReadOnlyList<LensProfileRow> _profileRows = Array.Empty<LensProfileRow>();
        string _savedTo = "";
        string _saveError = "";
        byte[] _map;
        string _mapLegend = "";

        /// <summary>Rasterul din care se deseneaza harta, tinut ca s-o poata reencoda pe alta scara.</summary>
        Img _residual;

        /// <summary>Encodarile deja facute, ca sa nu se reencodeze la fiecare click inainte si inapoi.</summary>
        readonly Dictionary<MapScaleOption, byte[]> _mapCache = new();

        /// <summary>Catalogul comun de profile, sau null (teste): atunci nu se compara si cardul explica.</summary>
        readonly ProfileCatalog _catalog;
        /// <summary>Metadatele unui fisier fara pixeli; in teste, un cadru fabricat in locul unui raw.</summary>
        readonly Func<string, LoadedFrame> _loadInfo;

        public StructureTabViewModel() : this(null, null) { }

        internal StructureTabViewModel(ProfileCatalog catalog, Func<string, LoadedFrame> loadInfo)
        {
            _catalog = catalog;
            _loadInfo = loadInfo ?? ImageLoader.LoadInfo;
            MapScale.Changed += RefreshMap;
        }

        /// <summary>Fisierul analizat, ca sa se vada pe ce s-a lucrat.</summary>
        public string Source { get => _source; private set => Set(ref _source, value); }

        /// <summary>
        /// Avertismentul ca folderul are DOUA mastere (masters/ si process/) si s-a luat
        /// cel mai nou. Gol in cazul obisnuit. Vezi SirilSession.FindFlatMaster.
        /// </summary>
        public string SourceWarning
        {
            get => _sourceWarning;
            private set { if (Set(ref _sourceWarning, value)) Raise(nameof(HasSourceWarning)); }
        }

        public bool HasSourceWarning => !string.IsNullOrEmpty(_sourceWarning);

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

        /// <summary>
        /// Cele doua parti ale verdictului - forma (cele cinci teste) si adancimea (fata de
        /// profil) - cand s-a comparat; gol altfel. Textul e cel de sub VERDICT din consola.
        /// </summary>
        public string VerdictDetail { get => _verdictDetail; private set => Set(ref _verdictDetail, value); }

        // ----------------------------------------- vignetarea fata de profilul obiectivului

        /// <summary>Titlul cardului, din nucleu, ca sa fie cel din raport.</summary>
        public string ProfilesHeading => StructureReport.ProfilesHeading;

        /// <summary>Un rand pe profil; gol cand nu s-a comparat. Lista se inlocuieste, nu se umple.</summary>
        public IReadOnlyList<LensProfileRow> ProfileRows
        {
            get => _profileRows;
            private set { if (Set(ref _profileRows, value)) Raise(nameof(HasProfileRows)); }
        }

        public bool HasProfileRows => _profileRows.Count > 0;

        /// <summary>"1.26 stopuri (colt = 42 % din centru)" - adancimea masterului, cea din randul informativ.</summary>
        public string ProfileMaster { get => _profileMaster; private set => Set(ref _profileMaster, value); }

        /// <summary>Al cui e verdictul si cat difera profilele intre ele.</summary>
        public string ProfileSummary { get => _profileSummary; private set => Set(ref _profileSummary, value); }

        /// <summary>Scara adancimii in stopuri - aceleasi patru benzi ca scara de reziduu, cu banda verdictului marcata.</summary>
        public IReadOnlyList<ScaleRow> ProfileScale { get => _profileScale; private set => Set(ref _profileScale, value); }

        /// <summary>De unde vine scara de mai sus: scara celor cinci teste tradusa in stopuri, nu un prag nou.</summary>
        public string ProfileScaleNote { get => _profileScaleNote; private set => Set(ref _profileScaleNote, value); }

        /// <summary>Ce poate si ce nu poate spune comparatia.</summary>
        public string ProfileNote { get => _profileNote; private set => Set(ref _profileNote, value); }

        /// <summary>De ce nu s-a putut compara, cand nu s-a putut; gol altfel.</summary>
        public string ProfileGap { get => _profileGap; private set => Set(ref _profileGap, value); }

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
        {
            string rawPath = session.ReferenceRawPath;
            bool compares = rawPath != null && _catalog != null;
            return RunGuardedAsync(compares ? "Analizez structura masterului de flat si caut profilul obiectivului..."
                                            : "Analizez structura masterului de flat...", async () =>
            {
                FlatMasterLookup master = session.FindFlatMaster();
                string path = master.Path;
                SourceWarning = master.Ambiguous ? SessionMessages.TwoFlatMasters(master) : "";
                var text = new StringWriter();

                // Cu un raw langa master, rularea e cea a consolei cu --raw: catalogul comun,
                // raw-ul din flats/, comparatia in acelasi raport. Fara, rularea simpla.
                StructureQuery query = null;
                if (compares)
                {
                    ProfileSources sources = await _catalog.EnsureAsync().ConfigureAwait(true);
                    ct.ThrowIfCancellationRequested();
                    query = new StructureQuery { Sources = sources, RawPath = rawPath, LoadInfo = _loadInfo };
                }

                var (outcome, gap) = await Task.Run(() =>
                {
                    RunReport.WriteBanner(text);
                    var run = StructureRun.Execute(path, text, query);
                    RunReport.WriteMapSaved(session.ResidualPngPath, text);
                    ct.ThrowIfCancellationRequested();
                    // Fara comparatie, motivul pentru card: acelasi nucleu spune de ce, pe
                    // metadatele masterului (doar headerul) si fara catalog.
                    StructureOutcome why = run.Result.Profiles != null ? null
                        : StructureRun.Compare(run.Result.Structure, _loadInfo(path), new StructureQuery { RawPath = rawPath, LoadInfo = _loadInfo });
                    return (run, why);
                }, ct).ConfigureAwait(true);

                Source = path;
                Report = text.ToString();
                Populate(outcome.Result, gap);

                // Rasterul, apoi harta pe scara aleasa. Octetii primiti de la rulare sunt
                // cei pe scara automata: se pun direct in cache, ca sa nu se reencodeze
                // aceeasi imagine cand chipul e chiar "auto".
                _residual = outcome.Result.Structure.Residual;
                _mapCache.Clear();
                _mapCache[MapScale.Options[0]] = outcome.Png;
                RefreshMap();

                HasResult = true;

                // Pe disc merge mereu encodarea automata, oricare chip e bifat. Vezi nota
                // de sus: consola scrie asta, si fisierul trebuie sa fie acelasi.
                Save(session, outcome.Png);
            });
        }

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

        /// <param name="o">Rularea, cu sau fara comparatie.</param>
        /// <param name="gap">Fara comparatie in rulare: o comparatie care spune de ce nu s-a putut; altfel null.</param>
        internal void Populate(StructureOutcome o, StructureOutcome gap = null)
        {
            Populate(o.Structure);
            // Internal si cu HasResult, ca testul de randare sa poata arata tabul dintr-un
            // rezultat, fara sesiune - ca la tabul de netezire.
            HasResult = true;

            // Verdictul rularii - cel mai rau dintre forma si adancime - si sfatul ei, care e
            // al adancimii cand ea l-a inrautatit. Textele sunt cele din raport.
            Verdict = o.Verdict;
            Advice = StructureReport.Advice(o);
            // Pe trei randuri, cu etichetele fara umplutura consolei: partile vin din raport.
            VerdictDetail = o.Compared
                ? $"forma: {StructureReport.ShapeDetail(o)}\nadancimea: {StructureReport.DepthDetail(o)}\n{StructureReport.WorseOfBoth}"
                : "";

            StructureOutcome explained = o.Profiles != null ? o : gap;
            if (explained != null && explained.Compared)
            {
                VignettingComparison c = explained.Profiles;
                ProfileGap = "";
                ProfileMaster = StructureReport.MasterDepth(c);
                ProfileRows = StructureReport.ProfileRows(c).Select(r => new LensProfileRow
                {
                    Source = r.Source, Lens = r.Lens, Where = r.Where, Profile = r.Profile, Depth = r.Depth,
                    Diff = r.Diff, Error = r.Error, Verdict = r.Verdict, IsNearest = r.IsNearest,
                    Note = (r.IsNearest ? "Cel mai apropiat profil - da verdictul. " : "") + r.Note, IsWarning = r.IsWarning,
                }).ToList();
                ProfileSummary = StructureReport.ComparisonSummary(c).Replace('\n', ' ');
                ProfileScale = StructureReport.DepthScaleRows()
                    .Select(s => new ScaleRow { Band = s.Band, Range = s.Range, Meaning = s.Meaning, IsCurrent = s.Band == c.Verdict })
                    .ToList();
                ProfileScaleNote = StructureReport.DepthScaleNote().Replace('\n', ' ') + " "
                                 + StructureReport.DepthLightScaleNote(c.Nearest.DiffStops).Replace('\n', ' ');
                ProfileNote = StructureReport.ComparisonNote().Replace('\n', ' ');
            }
            else
            {
                ProfileRows = Array.Empty<LensProfileRow>();
                ProfileScale = Array.Empty<ScaleRow>();
                ProfileMaster = ProfileSummary = ProfileScaleNote = ProfileNote = "";
                ProfileGap = explained == null ? ""
                    : "Nu se poate compara: " + StructureReport.GapText(explained) + "\n" + StructureReport.GapMeaning().Replace('\n', ' ');
            }
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
            SourceWarning = "";
            Size = "";
            Levels = "";
            Verdict = "";
            Advice = "";
            LocalSpotWarning = "";
            VerdictDetail = "";
            ProfileRows = Array.Empty<LensProfileRow>();
            ProfileScale = Array.Empty<ScaleRow>();
            ProfileMaster = ProfileSummary = ProfileScaleNote = ProfileNote = ProfileGap = "";
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
