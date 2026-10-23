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

// ResultTabs.cs - cele sase taburi cu rezultate. Fiecare primeste AnalysisResult prin
// Populate si il transforma in randuri; niciunul nu calculeaza o cifra si nu formuleaza o
// propozitie - textele vin din Recommendations si din ReportWriter, cifrele din Fmt.
//
// Toate intr-un fisier fiindca sunt toate acelasi lucru: o lista de randuri si un bool
// HasResult. Cand unul va creste, isi ia fisierul lui.

using System;
using System.Collections.Generic;
using System.Linq;

namespace AstroPhotoAnalyzer.Wpf
{
    public abstract class ResultTabViewModel : ObservableObject
    {
        bool _has;
        public bool HasResult { get => _has; protected set => Set(ref _has, value); }

        internal abstract void Populate(AnalysisResult r);

        internal virtual void Clear()
        {
            HasResult = false;
        }

        protected static MetricRow Row(string label, string value, string note = null, string description = null)
            => new MetricRow { Label = label, Value = value, Note = note, Description = description };
    }

    // ------------------------------------------------------------ Raport

    public sealed class ReportTabViewModel : ResultTabViewModel
    {
        IReadOnlyList<SectionRow> _sections = Array.Empty<SectionRow>();
        string _verdict = "";
        string _text = "";
        bool _showText;

        public IReadOnlyList<SectionRow> Sections { get => _sections; private set => Set(ref _sections, value); }
        public string VerdictLabel { get => _verdict; private set => Set(ref _verdict, value); }

        /// <summary>Raportul complet, cu banner - exact ce se salveaza in Raport.txt.</summary>
        public string Text { get => _text; private set => Set(ref _text, value); }
        public bool ShowText { get => _showText; set => Set(ref _showText, value); }

        internal override void Populate(AnalysisResult r)
        {
            var sections = new List<SectionRow>();
            foreach (Topic t in Enum.GetValues(typeof(Topic)))
            {
                var recs = r.Recommendations.Where(x => x.Topic == t).ToList();
                Verdict v = recs.Count == 0 ? Verdict.Ok : recs.Max(x => x.Verdict);
                sections.Add(new SectionRow
                {
                    Number = (int)t,
                    Title = VerdictText.Title(t),
                    VerdictLabel = VerdictText.Label(v),
                    Recommendations = recs.Select(x => new RecommendationRow
                    {
                        Mark = x.Verdict == Verdict.Ok ? "-" : x.Verdict == Verdict.Attention ? "!" : "!!",
                        Text = x.Text,
                        Because = x.Because,
                        VerdictLabel = VerdictText.Label(x.Verdict),
                    }).ToList(),
                });
            }
            Sections = sections;
            VerdictLabel = VerdictText.Label(r.Overall);
            var sw = new System.IO.StringWriter();
            ReportWriter.WriteFull(r, sw);
            Text = sw.ToString();
            HasResult = true;
        }

        internal override void Clear()
        {
            base.Clear();
            Sections = Array.Empty<SectionRow>();
            VerdictLabel = "";
            Text = "";
        }
    }

    // ------------------------------------------------------------ Tonuri

    public sealed class TonesTabViewModel : ResultTabViewModel
    {
        /// <summary>Propozitia rosie de sub tabel cand fisierul n-are WCS (R-113); aceeasi in fereastra hartii.</summary>
        public const string NoWcsSentence = "Fisierul nu are WCS: norii nu au fost identificati dupa coordonate astrometrice.";

        IReadOnlyList<MetricRow> _rows = Array.Empty<MetricRow>();
        IReadOnlyList<double> _hist = Array.Empty<double>();
        IReadOnlyList<SimulationRowItem> _simulation = Array.Empty<SimulationRowItem>();
        IReadOnlyList<string> _clouds = Array.Empty<string>();
        IReadOnlyList<string> _ladder = Array.Empty<string>();
        IReadOnlyList<GuideParagraph> _guide = Array.Empty<GuideParagraph>();
        double _paperBlack = double.NaN;
        string _definition, _exactLine, _noWcs;
        AnalysisResult _result;

        public IReadOnlyList<MetricRow> Rows { get => _rows; private set => Set(ref _rows, value); }
        public IReadOnlyList<double> Histogram { get => _hist; private set => Set(ref _hist, value); }

        /// <summary>Negrul hartiei, in L*, ca pozitie in histograma (cosurile sunt 0..100).</summary>
        public double PaperBlack { get => _paperBlack; private set => Set(ref _paperBlack, value); }

        /// <summary>Tabelul simularii: antetul, apoi un rand pe tinta, fiecare cu butonul lui de preview (R-113, R-131).</summary>
        public IReadOnlyList<SimulationRowItem> SimulationRows { get => _simulation; private set => Set(ref _simulation, value); }

        /// <summary>Definitia prafului si a cerului in uz, sub tabel (R-044).</summary>
        public string Definition { get => _definition; private set => Set(ref _definition, value); }

        /// <summary>Tinta exacta, cele doua la Dmax vecin, Dmax-ul de la care praful ajunge pe negrul hartiei (R-043).</summary>
        public string ExactLine { get => _exactLine; private set => Set(ref _exactLine, value); }

        /// <summary>Propozitia rosie fara WCS; null cu WCS (ascunsa).</summary>
        public string NoWcsWarning { get => _noWcs; private set => Set(ref _noWcs, value); }

        /// <summary>Norii catalogati cu randul lor, toti, ca text mono (raportul ii arata pe primii 15).</summary>
        public IReadOnlyList<string> CloudRows { get => _clouds; private set => Set(ref _clouds, value); }

        /// <summary>Tabela nivel - L* a reperelor tabului, din TonalAnalyzer.LstarLadder; doar in fereastra.</summary>
        public IReadOnlyList<string> LadderRows { get => _ladder; private set => Set(ref _ladder, value); }

        /// <summary>Panoul "cum se citeste tabul". Cifrele din el vin din Thresholds si ColorMath, nu sunt scrise de mana.</summary>
        public IReadOnlyList<GuideParagraph> Guide { get => _guide; private set => Set(ref _guide, value); }

        /// <summary>Tooltip-ul butonului Preview al unui rand: pragul vine din Thresholds, nu e tastat.</summary>
        public string PreviewTooltip => $"Imaginea la tinta randului, cu ce e sub negrul hartiei si sub L* {Fmt.F0(Thresholds.LowSeparationLstar)} vopsit";

        /// <summary>Deschide harta praf / cer (R-130). Fereastra o face cine asculta MapRequested; aici nu se deschide nimic.</summary>
        public System.Windows.Input.ICommand OpenMap { get; }

        public event Action<MapViewModel> MapRequested;
        public event Action<PreviewWindowViewModel> PreviewRequested;

        public TonesTabViewModel()
        {
            OpenMap = new RelayCommand(() => { if (_result != null) MapRequested?.Invoke(BuildMap()); }, () => _result != null);
        }

        /// <summary>Harta pentru rezultatul curent, construita la cerere (R-130).</summary>
        internal MapViewModel BuildMap() => MapViewModel.From(_result);

        /// <summary>Preview-ul unui rand al simularii, construit la cerere (R-131).</summary>
        internal PreviewWindowViewModel BuildPreview(SimulationRow row) => PreviewWindowViewModel.From(_result, row);

        internal override void Populate(AnalysisResult r)
        {
            TonalResult t = r.Tonal;
            SimulationResult s = r.Simulation;
            _result = r;
            Rows = new[]
            {
                Row("Medii pe canal (0..255)", string.Join(" / ", t.MeanLevels.Select(Fmt.F1)),
                    description: "Media fiecarui canal pe toti pixelii, in niveluri de 8 biti. Cerul ocupa aproape tot cadrul, "
                        + "asa ca mediile stau aproape de fundal; diferentele dintre canale sunt analizate ca dominanta in tabul Culoare."),
                Row($"Sub nivelul {Thresholds.DarkLevelA} / {Thresholds.DarkLevelB}", $"{Fmt.Pct(t.PctBelowDarkA)} / {Fmt.Pct(t.PctBelowDarkB)}",
                    description: $"Cat din imagine (toate canalele la un loc) e sub {Thresholds.DarkLevelA} si sub {Thresholds.DarkLevelB} din 255, "
                        + "pragurile pe care le citesti pe histograma din Siril. Doar context: verdictul de print se da pe L*, mai jos."),
                Row("Nivelul fundalului (mediana canalului de referinta)", Fmt.F3(t.BackgroundMedian),
                    description: "Mediana canalului verde (a singurului canal la mono), pe scara 0..1. Cum cerul domina cadrul, "
                        + "mediana e nivelul fundalului: cat de sus sta cerul. De aici porneste simularea de mai jos. "
                        + "Culoarea cerului e alta masuratoare, in tabul Culoare."),
                Row("L* median", Fmt.F1(t.LstarMedian),
                    description: "Luminozitatea perceputa a pixelului median, de la 0 (negru) la 100 (alb). L* e scara hartiei: "
                        + "pasi egali de L* se vad ca pasi egali de gri. Pe o astrofoto, L* median e practic cerul."),
                Row($"Sub negrul hartiei (L* {Fmt.F1(t.PaperBlackLstar)}, Dmax {Fmt.Dmax(t.Dmax)})", Fmt.Pct(t.PctBelowPaperBlack),
                    description: "Pixelii mai intunecati decat cel mai adanc negru pe care il da hartia aleasa. Toti ies cu acelasi negru, "
                        + "deci nuantele dintre ei se pierd. E un fapt al imaginii, nu verdictul: verdictul se da pe praf si pe cer, "
                        + "in simularea de mai jos."),
                Row($"Sub L* {Fmt.F0(Thresholds.LowSeparationLstar)} (separare slaba)", Fmt.Pct(t.PctBelowLowSeparation),
                    description: "Tonuri pe care hartia inca le separa, dar pe care ochiul, in lumina obisnuita de camera, abia le deosebeste. "
                        + "Include si pixelii de sub negrul hartiei. Un procent mare inseamna un cer care va parea o pata uniforma."),
                Row($"Peste L* {Fmt.F0(Thresholds.HighlightLstar)}", Fmt.Pct(t.PctAboveHighlight),
                    description: "Pixelii mai luminosi decat griul de mijloc: stele, nucleu, nebulozitate stralucitoare. "
                        + "Arata cat din cadru poarta luminile."),
                // Cate o percentila pe rand: pe un singur rand valoarea ar fi cea mai lata din
                // panel si ar ingusta coloana descrierilor pentru toate randurile.
                Row("Percentile canal referinta", string.Join("\n", TonalResult.PercentileRanks.Zip(t.ReferencePercentiles, (p, v) => $"{"p" + Fmt.F1(p),-6} = {Fmt.F1(v),5}")),
                    description: "Nivelul (0..255) sub care cade un procent din pixelii canalului de referinta: p50 e fundalul, "
                        + "p0.1 cel mai adanc negru care conteaza, p99.9 si p100 stelele cele mai luminoase. p100 = 255 inseamna pixeli saturati."),
            };
            Histogram = t.LstarHistogramPct;
            PaperBlack = t.PaperBlackLstar;
            // Acelasi text mono ca in Anexa B, plus butonul de preview pe fiecare rand (antetul n-are).
            var sim = new List<SimulationRowItem> { new SimulationRowItem { Text = ReportWriter.SimulationHeader } };
            foreach (SimulationRow row in s.Rows)
            {
                SimulationRow captured = row;
                sim.Add(new SimulationRowItem
                {
                    Text = ReportWriter.SimulationLine(row),
                    Target = row.AsIs ? "asa cum e" : Fmt.Level(row.Target) + (row.Explicit ? " (dat)" : ""),
                    Preview = new RelayCommand(() => PreviewRequested?.Invoke(BuildPreview(captured))),
                });
            }
            sim.Add(new SimulationRowItem { Text = ReportWriter.SimulationFooter(s) });
            SimulationRows = sim;
            Definition = ReportWriter.DustDefinition(s);
            ExactLine = ReportWriter.ExactLine(s);
            NoWcsWarning = r.Raster.Source?.Wcs == null ? NoWcsSentence : null;
            CloudRows = s.Clouds.Count == 0
                ? Array.Empty<string>()
                : new[] { ReportWriter.CloudHeader }.Concat(s.Clouds.Select(ReportWriter.CloudLine)).ToList();
            LadderRows = new[] { "   L*   nivel (gri)   reper" }
                .Concat(TonalAnalyzer.LstarLadder(r.Parameters, s).Select(l =>
                    $"{Fmt.F1(l.Lstar),5}   {Fmt.F1(l.GrayLevel),11}   {l.Meaning}{(l.Chosen ? "   <- mediul ales" : "")}"))
                .ToList();
            Guide = GuideParagraphs();
            HasResult = true;
            (OpenMap as RelayCommand)?.RaiseCanExecuteChanged();
        }

        internal override void Clear()
        {
            base.Clear();
            _result = null;
            Rows = Array.Empty<MetricRow>();
            Histogram = Array.Empty<double>();
            SimulationRows = Array.Empty<SimulationRowItem>();
            CloudRows = Array.Empty<string>();
            LadderRows = Array.Empty<string>();
            Guide = Array.Empty<GuideParagraph>();
            Definition = null;
            ExactLine = null;
            NoWcsWarning = null;
            (OpenMap as RelayCommand)?.RaiseCanExecuteChanged();
        }

        // Textul panoului: explicatie, nu diagnostic - nu depinde de imagine, doar de praguri.
        static IReadOnlyList<GuideParagraph> GuideParagraphs()
        {
            double barytaBlack = ColorMath.PaperBlackLstar(Parameters.DmaxOf(PrintMedium.Baryta));
            double[] grid = new Parameters().SimulationTargets;
            static string Black(PrintMedium m)
                => $"{Fmt.Dmax(Parameters.DmaxOf(m))} -> L* {Fmt.F1(ColorMath.PaperBlackLstar(Parameters.DmaxOf(m)))}";
            return new[]
            {
                new GuideParagraph
                {
                    Lead = "Ce intreaba tabul.",
                    Text = "Supravietuieste fundalul hartiei? Pe ecran negrul coboara aproape la zero; pe hartie cel mai intunecat ton e cel "
                        + "al cernelii la saturatie, iar tot ce e sub el se printeaza la fel. Cerul unei astrofotografii sta tocmai in zona asta, "
                        + "asa ca un fundal care arata bine pe monitor poate iesi pe hartie ca o pata neagra fara nuante.",
                },
                new GuideParagraph
                {
                    Lead = "Doua scari, deliberat.",
                    Text = "Nivelurile 0..255 sunt limbajul histogramei din Siril si din orice editor; L* (0..100) e limbajul hartiei, "
                        + "luminozitatea asa cum o vede ochiul. Trecerea se face in trei pasi: se anuleaza curba sRGB, se aduna canalele "
                        + "ponderat (verdele cantareste de zece ori cat albastrul) si se aplica radacina cubica a L*-ului. "
                        + $"Pe un gri, nivelul {Fmt.F0(ColorMath.GrayLevelOfLstar(barytaBlack))} e L* {Fmt.F0(barytaBlack)} (negrul baryta), "
                        + $"{Thresholds.DarkLevelA} e L* {Fmt.F0(ColorMath.LstarOfGrayLevel(Thresholds.DarkLevelA))}, "
                        + $"{Thresholds.DarkLevelB} e L* {Fmt.F0(ColorMath.LstarOfGrayLevel(Thresholds.DarkLevelB))}. "
                        + "Pe culoare, acelasi nivel da L* diferit dupa nuanta, de aceea se arata amandoua.",
                },
                new GuideParagraph
                {
                    Lead = "Negrul hartiei",
                    Text = $"vine din Dmax, densitatea optica maxima a mediului: baryta {Black(PrintMedium.Baryta)}, "
                        + $"canvas {Black(PrintMedium.Canvas)}, C-print sub acril {Black(PrintMedium.Acrylic)}. Mediul se alege in parametri; "
                        + "aceeasi imagine poate fi print-safe pe baryta si prea intunecata pe canvas.",
                },
                new GuideParagraph
                {
                    Lead = "Praful si cerul.",
                    Text = $"Imaginea se imparte in dale de {Thresholds.DustPatchPx} x {Thresholds.DustPatchPx} px, fiecare cu L*-ul luminantei ei medii: "
                        + "ce integreaza ochiul pe o pata de hartie, nu un pixel. Dalele se impart in cinci clase, relativ, nu dupa un prag fix, pe una din doua cai. "
                        + "Cu un FITS plate-solved (WCS in antet, sau imprumutat cu campul \"WCS din FITS\") si cu Calea Lactee in cadru: banda e latitudinea galactica |b| sub "
                        + $"{Fmt.F0(Thresholds.BandGalacticLatitudeDeg)} grade; cerul e modelat din dalele din afara ei; praful e dala cu {Fmt.F0(Thresholds.DustDepthLstar)} L* sub nivelul local al benzii "
                        + $"(mediana pe ~{Thresholds.BandLocalWindowPatches * Thresholds.DustPatchPx} px) sau sub cerul modelat, adica mai intunecata decat ar fi banda acolo; norii luminosi, "
                        + $"cu {Fmt.F0(Thresholds.BrightCloudLstar)} L* peste nivelul local; varfurile, cele mai luminoase {Fmt.F1(Thresholds.PeakFraction * 100)} % din dale. "
                        + $"Calea asta cere banda si cer in afara ei, fiecare cu cel putin {Fmt.F0(Thresholds.BandMinFraction * 100)} % din dale, si un camp care acopera cel putin "
                        + $"{Fmt.F0(Thresholds.BandMinLatitudeSpanDeg)} grade de latitudine galactica, latimea benzii: pe un camp mai ingust banda nu e o fasie, ci un gradient, si cadrul e deep-sky in banda. Altfel, ca si fara WCS, clasele sunt relative la cerul "
                        + $"modelat din toate dalele (percentila {Fmt.F0(Thresholds.SkyModelLowPercentile)} a fiecarei celule), cu praguri din imagine: nebuloasa e dala cu peste {Fmt.F0(Thresholds.SkyNoiseK)} sigma ale "
                        + "cerului deasupra modelului, praful cu la fel de mult sub el, nebuloasa stralucitoare cea de peste pragul Otsu al nebuloasei, varfurile ca mai sus. "
                        + $"Pe orice cale, L*-ul cerului e mediana dalelor de cer, iar L*-ul prafului e zona intinsa cea mai intunecata: percentila {Fmt.F0(Thresholds.DustClassFraction * 100)} % a dalelor de praf "
                        + $"cand clasa are cel putin {Fmt.F0(Thresholds.DustClassMinFraction * 100)} % din dale (sub atat sunt nuclee sau artefacte), sau cel mai intunecat {Fmt.F0(Thresholds.SkyDarkestFraction * 100)} % al dalelor de cer, "
                        + $"care din doua e mai intunecata. Catalogul Lynds da numele norilor si randul fiecaruia cu cel putin {Thresholds.DustCloudMinPatches} dale, cu tinta la care ajunge el pe negru. "
                        + "Butonul \"Harta praf / cer\" arata clasele pe imagine si norii pe nume.",
                },
                new GuideParagraph
                {
                    Lead = "Verdictul.",
                    Text = "Praful sub negrul hartiei, cerul peste: atentie, norii s-ar tipari negru pur. "
                        + "Cerul sub negru: problema, cerul si praful ies cu acelasi negru. Nu e un procent de pixeli: e cea mai intunecata "
                        + "structura intinsa fata de negrul hartiei. Apoi vine privitul: hartia separa tonurile si mai jos, dar un cer sub "
                        + $"L* {Fmt.F0(Thresholds.LowSeparationLstar)} se vede aproape negru in lumina de camera, deci si el e atentie, oricat de adanc ar fi negrul hartiei. "
                        + "Print-safe inseamna amandoua: praful la sau peste negrul hartiei si cerul la prag sau peste; un rand cu praful pe negru "
                        + $"si cerul dedesubt e marcat \"cer sub L* {Fmt.F0(Thresholds.LowSeparationLstar)}\". "
                        + "Recomandarea e nivelul MINIM print-safe - in sus "
                        + "pe o imagine prea intunecata, in jos pe una prea luminoasa.",
                },
                new GuideParagraph
                {
                    Lead = "Simularea",
                    Text = $"raspunde la \"si daca mut fundalul?\". Aplica pe o copie redusa a imaginii o curba gamma, aceeasi pe toate canalele, "
                        + $"care duce fundalul masurat la fiecare tinta din grila {Fmt.Level(grid.Min())}..{Fmt.Level(grid.Max())}, apoi remasoara praful si cerul. "
                        + "Randul \"asa cum e\" sta printre tinte, la nivelul lui. Cea mai mica tinta marcata print-safe e cea recomandata: refa "
                        + "stretch-ul din datele liniare in Siril pana la fundalul acela. Sub tabel: tinta exacta la care praful atinge negrul, "
                        + $"si aceeasi tinta la Dmax cu {Fmt.F1(Thresholds.DmaxSensitivityStep)} mai mic si mai mare, fiindca Dmax-ul unei hartii pe o imprimanta rar e stiut mai bine. "
                        + "E o simulare: un stretch adevarat (Siril, VeraLux) tine umbrele mai jos decat curba gamma, deci tinta exacta e a curbei, "
                        + $"nu a lui. Butonul Preview al unui rand arata imaginea la tinta lui, cu ce e sub negru si sub L* {Fmt.F0(Thresholds.LowSeparationLstar)} vopsit. "
                        + $"O tinta prea mare spala cerul in gri; urmareste coloana \"peste L*{Fmt.F0(Thresholds.HighlightLstar)}\".",
                },
            };
        }
    }

    // ------------------------------------------------------------ Export

    public sealed class ExportTabViewModel : ResultTabViewModel
    {
        IReadOnlyList<string> _jpeg = Array.Empty<string>();
        IReadOnlyList<GuideParagraph> _jpegGuide = Array.Empty<GuideParagraph>();
        IReadOnlyList<MetricRow> _rows = Array.Empty<MetricRow>();

        public IReadOnlyList<string> JpegRows { get => _jpeg; private set => Set(ref _jpeg, value); }

        /// <summary>Paragrafele de sub tabelul JPEG: cum se masoara, ce e fiecare coloana, ce se recomanda.</summary>
        public IReadOnlyList<GuideParagraph> JpegGuide { get => _jpegGuide; private set => Set(ref _jpegGuide, value); }
        public IReadOnlyList<MetricRow> Rows { get => _rows; private set => Set(ref _rows, value); }

        internal override void Populate(AnalysisResult r)
        {
            QuantizationResult q = r.Quantization;
            JpegRows = new[] { "JPEG 4:4:4   marime     err medie   err max   p99    p99.9   > 2 niv   nivel mediu" }
                .Concat(r.Jpeg.Rows.Select(j =>
                    $"q{j.Quality,-3}         {Fmt.Mb(j.Megabytes),8}   {Fmt.F3(j.MeanAbsError),9}   {Fmt.F1(j.MaxError),7}   {Fmt.F2(j.P99),4}   {Fmt.F2(j.P999),5}   {Fmt.Pct(j.PctOverThreshold),7}   {Fmt.F0(j.MeanLevelOverThreshold),11}"))
                .ToList();
            JpegGuide = JpegGuideParagraphs();

            // Descrierile: ce masoara randul, cu pragurile din Thresholds, nu scrise de mana.
            string lo = Fmt.Int(Thresholds.ShadowLiftLevelLo), hi = Fmt.Int(Thresholds.ShadowLiftLevelHi);
            var rows = new List<MetricRow>
            {
                Row("TIFF 8-bit LZW", Fmt.Mb(r.Jpeg.Tiff8LzwMegabytes),
                    description: "Marimea fisierului pe care il trimiti la laborator: aceeasi imagine, redusa la 8 biti, scrisa TIFF cu compresie LZW "
                        + "(fara pierdere) si predictor orizontal. E formatul recomandat: fara artefacte de compresie, acceptat de orice laborator, "
                        + "la jumatate din marimea unui TIFF de 16 biti. Cifra e a encoderului din unealta, nu a lui Siril; ordinul de marime e acelasi."),
            };
            if (q.Applicable)
            {
                rows.Add(Row("Rotunjire 16 -> 8 biti: medie / maxim / p99.9", $"{Fmt.F3(q.RoundingMeanAbs)} / {Fmt.F3(q.RoundingMax)} / {Fmt.F3(q.RoundingP999)}",
                    description: "Cat se pierde cand fiecare valoare de 16 biti se rotunjeste la cel mai apropiat nivel din 255: media, maximul si p99.9 "
                        + "ale erorii, pe toate canalele. Jumatate de nivel e maximul matematic posibil, deci un maxim de 0.5 nu e un defect, e definitia "
                        + "rotunjirii. E o pierdere de precizie, nu de informatie vizibila: ochiul nu deosebeste doua niveluri vecine din 255. "
                        + "Randul apare doar cand sursa are mai mult de 8 biti."));
                rows.Add(Row($"Stres shadow-lift, niveluri {lo}..{hi}: din 16 biti", $"{q.LevelsUsedFrom16} folosite / {q.MissingFrom16} lipsa",
                    description: "Raspunde la \"ce se intampla daca se mai trage de umbre?\". Canalul de referinta e ridicat cu o curba gamma "
                        + $"{Fmt.F1(Thresholds.ShadowLiftGamma)}, un auto-correct tipic de laborator, apoi cuantizat la 8 biti; se numara nivelurile "
                        + $"folosite intre {lo} si {hi} si cate lipsesc intre primul si ultimul folosit. Pornind din datele de 16 biti, umbrele intinse "
                        + "au de unde sa umple nivelurile: randul asta e referinta pentru cel de sub el."));
                rows.Add(Row("... din 8 biti", $"{q.LevelsUsedFrom8} folosite / {q.MissingFrom8} lipsa", q.Keep16BitMaster ? "pastreaza master-ul de 16 biti" : null,
                    "Acelasi lift, dar pornind din datele deja rotunjite la 8 biti, adica din fisierul de tipar. Fiecare nivel lipsa e o treapta in "
                        + "histograma, un gol pe care nu-l mai umple nimic, vizibil ca banding daca umbrele sunt netede. "
                        + $"Peste {Fmt.Int(Thresholds.MissingLevelsWarn)} de niveluri lipsa, pastreaza master-ul de 16 biti: orice editare ulterioara "
                        + "(recadrare, retus, alt print) pleaca din el, nu din fisierul de 8 biti. Cat timp laboratorul printeaza fisierul asa cum e, "
                        + "8 biti ajung; testul e despre ce ramane pentru mai tarziu."));
            }
            string plateauDesc = $"Pe pana la {Fmt.Int(Thresholds.PlateauTileCount)} de dale de cer curat (interval sub {Fmt.F0(Thresholds.PlateauTileMaxRangeLevels)} niveluri "
                + $"si medie sub {Fmt.F0(Thresholds.PlateauTileMaxMeanLevels)}, ca sa nu prinda nucleul) se masoara, pe 8 biti, cat de lungi sunt sirurile "
                + "de pixeli vecini cu exact aceeasi valoare. Un cer fara zgomot cuantizat la 8 biti face trepte late; zgomotul rezidual le sparge, "
                + "ca un dither natural. Palierul in pixeli, inmultit cu pasul pixelului la dpi-ul randului de referinta, da lungimea treptei pe "
                + $"hartie: peste {Fmt.F1(Thresholds.PlateauVisibleMm)} mm e un contur vizibil, risc de banding - 16 biti daca laboratorul accepta, "
                + "sau dither la export. n/a cand nu se gasesc dale de cer curat.";
            rows.Add(q.PlateauKnown
                ? Row("Palier pe cerul cuantizat", $"{Fmt.F1(q.PlateauPx)} px", $"pe {q.PlateauTiles} dale" + (r.Size.ReferenceRow != null ? $"; {Fmt.F2(q.PlateauMmAt(r.Size.ReferenceRow.Dpi))} mm la {Fmt.F0(r.Size.ReferenceRow.Dpi)} dpi" : ""), plateauDesc)
                : Row("Palier pe cerul cuantizat", "n/a", "fara dale de cer curat", plateauDesc));
            Rows = rows;
            HasResult = true;
        }

        // Textul de sub tabelul JPEG: explicatie, nu diagnostic - nu depinde de imagine, doar de praguri.
        static IReadOnlyList<GuideParagraph> JpegGuideParagraphs()
        {
            string count = Fmt.F0(Thresholds.JpegErrorCountLevels);
            return new[]
            {
                new GuideParagraph
                {
                    Lead = "Cum se masoara.",
                    Text = "Imaginea redusa la 8 biti e encodata JPEG in memorie la fiecare calitate, 4:4:4 (fara subesantionarea culorii), "
                        + "decodata si comparata cu valorile de 16 biti nerotunjite, x 255. Eroarea include deci si rotunjirea la 8 biti, "
                        + "de aceea nici q100 nu da zero. Encoderul e cel al uneltei, nu cel din Siril sau Photoshop: cifrele sunt orientative, "
                        + "ordinea si proportiile raman.",
                },
                new GuideParagraph
                {
                    Lead = "Marime.",
                    Text = "Megabytii fisierului la calitatea respectiva, de comparat cu TIFF-ul 8-bit LZW din panoul de mai jos.",
                },
                new GuideParagraph
                {
                    Lead = "Err medie / max / p99 / p99.9.",
                    Text = "Eroarea pe esantion, in niveluri din 255: media pe toata imaginea, cea mai mare, si valorile sub care stau 99 %, "
                        + "respectiv 99.9 % din esantioane. Media nu scade monoton cu calitatea atat de aproape de fara-pierdere (trecerea prin "
                        + "YCbCr in intregi costa singura 1-2 niveluri), asa ca verdictul nu se da pe ea, ci pe maxim: "
                        + $"peste {Fmt.F0(Thresholds.JpegMaxErrorReject)} niveluri, calitatea nu se recomanda.",
                },
                new GuideParagraph
                {
                    Lead = $"> {count} niv / nivel mediu.",
                    Text = $"Cat la suta din esantioane au eroarea peste {count} niveluri, si la ce nivel mediu (0..255) stau acele esantioane - "
                        + "adica unde loveste compresia. Un nivel mediu mic inseamna cerul intunecat, unde JPEG-ul lucreaza cel mai prost si unde "
                        + "ochiul vede blocurile pe fundal neted; unul mare inseamna stele si muchii, unde erorile se ascund in detaliu. "
                        + $"Peste {Fmt.F0(Thresholds.JpegOverThresholdWarnPct)} % din esantioane la q100: atentie.",
                },
                new GuideParagraph
                {
                    Lead = "Ce se recomanda.",
                    Text = "TIFF 8-bit LZW. Daca laboratorul accepta doar JPEG: q100, 4:4:4. Sub q100 eroarea maxima sare pe stele si pe muchii, "
                        + "iar castigul de marime nu conteaza pentru un print.",
                },
            };
        }
    }

    // ------------------------------------------------------------ Rezolutie

    public sealed class ResolutionTabViewModel : ResultTabViewModel
    {
        IReadOnlyList<MetricRow> _rows = Array.Empty<MetricRow>();
        IReadOnlyList<string> _sizes = Array.Empty<string>();
        IReadOnlyList<string> _dpi = Array.Empty<string>();

        public IReadOnlyList<MetricRow> Rows { get => _rows; private set => Set(ref _rows, value); }
        public IReadOnlyList<string> SizeRows { get => _sizes; private set => Set(ref _sizes, value); }

        /// <summary>Scara dpi - distanta, doar in fereastra: raportul n-o repeta la fiecare imagine.</summary>
        public IReadOnlyList<string> DpiRows { get => _dpi; private set => Set(ref _dpi, value); }

        internal override void Populate(AnalysisResult r)
        {
            StarResult s = r.Stars;
            PrintSizeResult ps = r.Size;
            // Descrierile: ce masoara randul, cu pragurile din Thresholds, nu scrise de mana.
            // Colturile nu au descriere - le acopera cea de pe centru, si de patru ori acelasi
            // text ar incarca panoul.
            string minStars = Fmt.Int(Thresholds.StarMinCount), regionStars = Fmt.Int(Thresholds.RegionMinStars);
            string fwhmDesc = "Latimea unei stele la jumatatea inaltimii ei, in pixeli: cel mai mic detaliu pe care il tine imaginea, "
                + "indiferent cati pixeli are fisierul. Se masoara pe linia si pe coloana prin varf si se ia media; mediana pe toate "
                + "stelele e cifra care conteaza. Q1 / Q3 arata cat de imprastiate sunt: un interval larg inseamna stele inegale in "
                + "cadru - colturi moi, urme de tracking, sau stele pe nebulozitate, care ies mai late.";
            var rows = new List<MetricRow>
            {
                Row("Stele masurate", Fmt.Int(s.Count),
                    s.Insufficient ? $"prea putine pentru FWHM (minim {minStars})" : null,
                    "Cate stele au trecut de filtre: un varf local nesaturat, cu amplitudine minima fata de fundalul din jurul lui, "
                        + $"cu latimea intre {Fmt.F1(Thresholds.StarFwhmMin)} si {Fmt.F0(Thresholds.StarFwhmMax)} px pe ambele axe. "
                        + "Se cauta pe canalul de referinta (verdele; singurul canal la mono). "
                        + $"Sub {minStars} de stele, FWHM-ul global nu e de incredere si nu se raporteaza."),
                Row("FWHM median (Q1 / Q3)", s.Insufficient ? "n/a" : $"{Fmt.F2(s.FwhmMedian)} px ({Fmt.F2(s.FwhmQ1)} / {Fmt.F2(s.FwhmQ3)})",
                    description: fwhmDesc),
                Row("Detaliu real pe latime", ps.FwhmKnown ? $"~{Fmt.F0(ps.EffectiveDetailPx)} px" : "n/a",
                    description: "Latimea imaginii (dupa decupaj, daca e cazul) impartita la FWHM: cate stele incap una langa alta pe "
                        + "latime, adica rezolutia adevarata a fisierului. Restul pixelilor sunt interpolare. Marimea de print se "
                        + "judeca pe cifra asta, nu pe numarul de pixeli; tabelul de mai jos porneste de aici."),
            };
            foreach (StarRegion reg in s.Regions)
            {
                bool centre = reg == s.Regions[0];
                rows.Add(Row("FWHM median in " + (centre ? reg.Name : reg.Name.Replace("colt ", "coltul ")),
                    reg.Known ? Fmt.F2(reg.FwhmMedian) + " px" : "n/a", $"{reg.Count} stele",
                    centre
                        ? $"FWHM median pe cinci zone: centrul ({Fmt.F0(Thresholds.CenterFraction * 100)} % x {Fmt.F0(Thresholds.CenterFraction * 100)} % din cadru) "
                            + $"si cele patru colturi (cate {Fmt.F0(Thresholds.RegionFraction * 100)} % din fiecare latura, pornind din colt). "
                            + $"O zona se raporteaza doar cu cel putin {regionStars} stele in ea, altfel n/a. "
                            + "Comparate intre ele, arata daca optica e la fel de buna pe tot campul."
                        : null));
            }
            if (!double.IsNaN(s.CornerToCenterRatio))
                rows.Add(Row("Colt / centru", Fmt.F2(s.CornerToCenterRatio), s.SoftCorners ? "colturi moi" : null,
                    "Cel mai lat colt cunoscut impartit la centru. 1.00 inseamna stele la fel peste tot; "
                        + $"peste {Fmt.F1(Thresholds.CornerSoftRatio)} colturile sunt moi (coma, curbura de camp, tilt) si tabelul de marimi "
                        + "nu mai e valabil pentru tot cadrul: la marimea la care centrul e la limita, colturile sunt deja pete. "
                        + "In cazul asta nu se mareste tot cadrul, se ia in calcul un decupaj. "
                        + $"n/a cand centrul sau toate colturile au sub {regionStars} stele."));
            Rows = rows;

            SizeRows = new[] { $"marimi pentru {Fmt.Dim(ps.PixelW, ps.PixelH)} px{(r.Crop.Needed ? " (dupa decupaj)" : "")}:", "latime cm   inaltime   dpi   stea mm   punct de la   dist. folosita   rama ext. cm" }
                .Concat(ps.Rows.Select(ReportWriter.SizeTableRow))
                .Concat(ReportWriter.SizeTableFooter(ps))
                .ToList();
            DpiRows = new[] { "dpi   pas mm   grila dispare de la   adica" }
                .Concat(PrintSizeAnalyzer.DpiLadder(r.Parameters.ViewingDistanceM).Select(l =>
                    $"{Fmt.F0(l.Dpi),3}   {Fmt.F2(l.PitchMm),6}   {Fmt.F2(l.DistanceM) + " m",19}   {l.Meaning}"))
                .ToList();
            HasResult = true;
        }
    }

    // ------------------------------------------------------------ Fundal

    public sealed class BackgroundTabViewModel : ResultTabViewModel
    {
        IReadOnlyList<MetricRow> _rows = Array.Empty<MetricRow>();
        IReadOnlyList<string> _regions = Array.Empty<string>();
        IReadOnlyList<GuideParagraph> _guide = Array.Empty<GuideParagraph>();

        public IReadOnlyList<MetricRow> Rows { get => _rows; private set => Set(ref _rows, value); }

        /// <summary>
        /// Tabelul regiunilor, ca in raport, cu o singura deosebire: semnul "!" spune si
        /// daca abaterea e colorata sau neutra - in raport distinctia e in recomandari.
        /// </summary>
        public IReadOnlyList<string> RegionRows { get => _regions; private set => Set(ref _regions, value); }

        /// <summary>Paragrafele de sub tabelul regiunilor: ce sunt, ce e delta, ce e "!", colorat sau neutru.</summary>
        public IReadOnlyList<GuideParagraph> Guide { get => _guide; private set => Set(ref _guide, value); }

        internal override void Populate(AnalysisResult r)
        {
            BackgroundResult b = r.Background;
            string cast = Fmt.F1(Thresholds.CastWarnLevels);
            var rows = new List<MetricRow>
            {
                Row(b.ColorApplicable ? "Culoarea fundalului, R / G / B (0..255)" : "Fundalul, un singur canal (0..255)", string.Join(" / ", b.Levels.Select(Fmt.F1)),
                    description: $"Mediana pe fiecare canal a celui mai intunecat {Fraction(Thresholds.BackgroundDarkestFraction)} din imagine, dupa luminozitate, "
                        + $"fara banda de jos de {Fmt.F0(Thresholds.BackgroundExcludeBottomFraction * 100)} % din inaltime, unde stau orizontul si poluarea lui. "
                        + "E cerul \"curat\": fata de el se judeca tenta de mai jos si marginile din tabelul urmator. "
                        + "Nu e acelasi numar cu nivelul fundalului din tabul Tonuri, care e mediana pe tot cadrul: acela spune cat de sus e cerul, "
                        + "acesta ce culoare are."),
            };
            if (b.ColorApplicable)
            {
                rows.Add(Row("G - (R+B)/2", Fmt.S1(b.GreenExcess) + " niveluri",
                    b.GreenExcess > Thresholds.CastWarnLevels ? "tenta verde" : b.GreenExcess < -Thresholds.CastWarnLevels ? "tenta magenta" : null,
                    "Cat e verdele peste media rosului si albastrului, in niveluri: pozitiv = tenta verde, negativ = magenta. "
                        + "Un cer neutru nu are exces de verde; o astfel de tenta vine de la senzor sau de la calibrare, nu de la obiect. "
                        + "Pe ecran, cu fundalul intunecat, nu se vede; pe hartie, cu fundalul ridicat ca sa treaca de negrul hartiei, se vede. "
                        + $"Peste {cast} niveluri, in oricare sens: de neutralizat inainte de print (SCNR / Background Neutralization in Siril)."));
                rows.Add(Row("R - B", Fmt.S1(b.RedMinusBlue) + " niveluri",
                    b.RedMinusBlue > Thresholds.CastWarnLevels ? "tenta calda" : b.RedMinusBlue < -Thresholds.CastWarnLevels ? "tenta rece" : null,
                    "Rosu minus albastru, in niveluri: pozitiv = tenta calda (lumina de orizont, poluare urbana), negativ = tenta rece, albastruie. "
                        + $"Acelasi prag, {cast} niveluri. Impreuna cu randul de mai sus descrie complet culoarea fundalului: "
                        + "un fundal neutru le are pe amandoua aproape de zero."));
            }
            Rows = rows;
            RegionRows = b.Regions.Select(e =>
                $"{e.Name,-18} {string.Join(" / ", e.Levels.Select(Fmt.F1)),-22} delta {string.Join(" / ", e.Deltas.Select(Fmt.S1))}"
                + (!e.Flagged ? "" : !b.ColorApplicable ? "  !" : e.Coloured ? "  ! colorat" : "  ! neutru")).ToList();
            Guide = GuideParagraphs();
            HasResult = true;
        }

        /// <summary>"sfert" pentru 0.25, altfel procentul - ca textul sa ramana romanesc cat timp pragul e cel de acum.</summary>
        static string Fraction(double f) => Math.Abs(f - 0.25) < 1e-9 ? "sfert" : Fmt.F0(f * 100) + " %";

        // Textul de sub tabel: explicatie, nu diagnostic - nu depinde de imagine, doar de praguri.
        static IReadOnlyList<GuideParagraph> GuideParagraphs()
        {
            string grad = Fmt.F0(Thresholds.CornerGradientLevels);
            return new[]
            {
                new GuideParagraph
                {
                    Lead = "Regiunile.",
                    Text = $"Patru colturi, fiecare de {Fmt.F0(Thresholds.EdgeRegionHeightFraction * 100)} % din inaltime x "
                        + $"{Fmt.F0(Thresholds.EdgeRegionWidthFraction * 100)} % din latime, si patru laturi: benzi de aceeasi grosime pe mijlocul "
                        + "fiecarei laturi, intre colturi. Pentru fiecare, medianele pe canal (R / G / B, 0..255), apoi delta.",
                },
                new GuideParagraph
                {
                    Lead = "Delta.",
                    Text = "Regiune minus culoarea fundalului de mai sus, pe fiecare canal, in niveluri. Zero inseamna margine identica cu cerul curat. "
                        + $"E aproape intotdeauna pozitiva, fiindca fundalul e deja cel mai intunecat {Fraction(Thresholds.BackgroundDarkestFraction)} al imaginii.",
                },
                new GuideParagraph
                {
                    Lead = "Semnul !.",
                    Text = $"Apare cand cea mai mare abatere pe un canal trece de {grad} niveluri. Spune doar ca marginea difera de fundal, "
                        + "nu inca daca e defect sau obiect.",
                },
                new GuideParagraph
                {
                    Lead = "Colorat sau neutru.",
                    Text = "Daca cele trei delta se misca impreuna, marginea e cel mai probabil obiectul insusi (Calea Lactee care intra in colt) "
                        + "si ramane la Ok, cu indemnul sa te uiti in Preview inainte sa corectezi ceva. Daca se misca diferit (diferenta dintre "
                        + $"canale peste {grad} niveluri si peste {Fmt.F0(Thresholds.CornerColourSpreadRatio * 100)} % din abaterea medie), e un gradient cu culoare: "
                        + "lumina de orizont, poluare - de corectat pe liniar (GraXpert), nu cu o curba locala pe imaginea stretch-uita. "
                        + "Pe imaginea de referinta: coltul de orizont +9.4 / +1.7 / -9.9 e colorat; coltul in care intra Calea Lactee, "
                        + "+11 / +10 / +16, e neutru.",
                },
                new GuideParagraph
                {
                    Lead = "Pe mono",
                    Text = "e un singur canal si nu exista \"colorat\": orice ! inseamna doar \"diferit de fundal\".",
                },
            };
        }
    }

    // ------------------------------------------------------------ Preview

    public sealed class PreviewTabViewModel : ResultTabViewModel
    {
        IReadOnlyList<PreviewTile> _tiles = Array.Empty<PreviewTile>();
        IReadOnlyList<GuideParagraph> _guide = Array.Empty<GuideParagraph>();
        public IReadOnlyList<PreviewTile> Tiles { get => _tiles; private set => Set(ref _tiles, value); }

        /// <summary>Cardul de deasupra dalelor: regula ecran - hartie, cu pasul inapoi calculat pe imagine.</summary>
        public IReadOnlyList<GuideParagraph> Guide { get => _guide; private set => Set(ref _guide, value); }

        internal override void Populate(AnalysisResult r)
        {
            Raster img = r.Raster;
            var tiles = new List<PreviewTile>();
            int cw = Math.Min(480, img.W), ch = Math.Min(320, img.H);
            int cx = (img.W - cw) / 2, cy = (img.H - ch) / 2;

            // Ce verifici in fiecare dala: text de indrumare, nu diagnostic. Pragurile din Thresholds.
            const string corner = "Fata de centru: stele alungite spre colt = coma; intr-un singur colt = tilt; in toate patru = curbura de camp. "
                + "Colt mai luminos sau mai cald = gradientul din tabul Culoare; benzi la margine = cadrele stivei. Un colt rau poate iesi prin decupaj.";

            tiles.Add(new PreviewTile
            {
                Title = "Centru, 100 %",
                Image = RasterBitmaps.Region(img, cx, cy, cw, ch),
                Check = "Textura, nu detaliul. Granulatia cerului - recomandarea de denoise pentru ecran se judeca aici, cu pasul inapoi "
                    + "din cardul de sus - forma stelelor, rotunde si moi, fara halo de sharpening, si trepte in cer.",
            });
            tiles.Add(new PreviewTile
            {
                Title = "Centru, 200 %",
                Image = RasterBitmaps.Region(img, cx + cw / 4, cy + ch / 4, cw / 2, ch / 2, zoom: 2),
                Note = "pixelii din fisier, mariti de doua ori",
                Check = "Lupa, nu printul: artefacte de fisier. Cerul trebuie sa fie granulatie fina, nu petice de aceeasi valoare (palierul din "
                    + "Export); stelele discuri moi, nu patrate sau cruci. Blocuri de 8 x 8 inseamna ca fisierul a trecut prin JPEG; trepte "
                    + "vizibile pe \"16 biti\" inseamna 8 biti deghizati.",
            });
            int kw = Math.Min(320, img.W), kh = Math.Min(220, img.H);
            tiles.Add(new PreviewTile { Title = "Colt stanga-sus, 100 %", Image = RasterBitmaps.Region(img, 0, 0, kw, kh), Check = corner });
            tiles.Add(new PreviewTile { Title = "Colt dreapta-sus, 100 %", Image = RasterBitmaps.Region(img, img.W - kw, 0, kw, kh), Check = corner });
            tiles.Add(new PreviewTile { Title = "Colt stanga-jos, 100 %", Image = RasterBitmaps.Region(img, 0, img.H - kh, kw, kh), Check = corner });
            tiles.Add(new PreviewTile { Title = "Colt dreapta-jos, 100 %", Image = RasterBitmaps.Region(img, img.W - kw, img.H - kh, kw, kh), Check = corner });

            const int side = 520;
            tiles.Add(new PreviewTile
            {
                Title = "Cadru intreg, asa cum e",
                Image = RasterBitmaps.Whole(img, side),
                Check = "Compozitia si gradientul global, privit de la distanta sau mijind ochii: o parte mai luminoasa, banda orizontului, "
                    + "o dominanta. Aici raspunzi intrebarii de la marginile neutre: coltul marcat e Calea Lactee care intra in cadru, sau o lumina?",
            });
            SimulationRow primary = r.Simulation.Primary;
            if (!primary.AsIs)
            {
                Raster small = RasterOps.Subsample(img, Math.Max(1, (int)Math.Ceiling(Math.Max(img.W, img.H) / (double)side)));
                tiles.Add(new PreviewTile
                {
                    Title = $"Simulare print, fundal {Fmt.F2(primary.Target)}",
                    Image = RasterBitmaps.Whole(RasterOps.Gamma(small, primary.Gamma), side),
                    Note = "curba gamma pe toate canalele; pe ecran pare plata - hartia o aduce inapoi",
                    Check = "Alaturi de cadrul intreg: ce apare cand cerul urca. Nu platitudinea - Dmax-ul hartiei o aduce inapoi - ci dominanta "
                        + $"(o tenta de peste {Fmt.F1(Thresholds.CastWarnLevels)} niveluri devine vizibila aici), gradientul, si daca nebulozitatea "
                        + "slaba ramane separata de cer. Citeste-o ca si cum cel mai inchis ton ar fi negrul hartiei.",
                });
            }
            if (r.Crop.Needed)
                tiles.Add(new PreviewTile
                {
                    Title = "Decupaj la raportul tinta",
                    Image = RasterBitmaps.Whole(RasterOps.Crop(img, r.Crop.X0, r.Crop.Y0, r.Crop.W, r.Crop.H), side),
                    Note = $"{Fmt.Dim(r.Crop.W, r.Crop.H)} px, centrat; {Fmt.Int(r.Crop.CutPerSidePx)} px taiati pe fiecare parte din {r.Crop.Axis}",
                    Check = "Ce taie raportul cerut: intra in obiect sau in compozitie? Scoate coltul moale sau benzile stivei? "
                        + "Singura dala unde raspunsul poate fi \"schimb raportul\".",
                });
            Tiles = tiles;
            Guide = GuideParagraphs(r.Size);
            HasResult = true;
        }

        // Cardul de sus: regula ecran - hartie. Cifrele vin din PrintSizeResult si Thresholds.
        static IReadOnlyList<GuideParagraph> GuideParagraphs(PrintSizeResult ps)
        {
            string eq = Fmt.F0(ps.ScreenEquivalentWidthCm);
            double f = ps.ScreenStepBackFactor;
            string stepBack = double.IsNaN(f) ? ""
                : f >= 1
                    ? $" Pentru printul de referinta, {Fmt.F0(ps.ReferenceRow.WidthCm)} cm, priveste dalele de 100 % de la {Fmt.F1(f)} x distanta ta fata de monitor: "
                        + "ce nu se mai vede de acolo nu se vede nici pe hartie."
                    : $" Printul de referinta, {Fmt.F0(ps.ReferenceRow.WidthCm)} cm, e mai mare decat vederea la 100 %: pixelul lui pe hartie e de "
                        + $"{Fmt.F1(1 / f)} ori cat cel de pe ecran, deci ce vezi la 100 % se vede si pe hartie, marit.";
            return new[]
            {
                new GuideParagraph
                {
                    Lead = "Ecranul e mai aspru decat hartia.",
                    Text = $"Un monitor obisnuit are pixelul de ~{Fmt.F2(Thresholds.ScreenPixelMm)} mm; la 100 % vezi fisierul ca pe un print lat de "
                        + $"~{eq} cm, privit de la distanta ta fata de monitor.",
                },
                new GuideParagraph
                {
                    Lead = "Pasul inapoi.",
                    Text = (stepBack.Length > 0 ? stepBack.Substring(1) + " " : "")
                        + "Dalele de 100 % sunt facute pentru asta; cea de 200 % nu: e o lupa, nicio hartie nu arata asa.",
                },
                new GuideParagraph
                {
                    Lead = "Ce cauti.",
                    Text = "Textura si forma stelelor in centru; forma stelelor si gradientul in colturi; compozitia si gradientul global pe cadrul "
                        + "intreg; dominanta si nebulozitatea slaba in simulare; ce taie raportul in decupaj. Fiecare dala spune sub ea ce verifici.",
                },
                new GuideParagraph
                {
                    Lead = "Ce nu e.",
                    Text = "Nu e un vizualizator: decupaje la pixel si reduceri, din memorie; nu se scrie nimic pe disc. Nu e gestionata culoarea: "
                        + "pixelii sunt aratati asa cum sunt in fisier, presupusi sRGB.",
                },
            };
        }

        internal override void Clear()
        {
            base.Clear();
            Tiles = Array.Empty<PreviewTile>();
            Guide = Array.Empty<GuideParagraph>();
        }
    }
}
