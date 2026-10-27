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

// ResultTabs.cs - the six tabs with results. Each receives the AnalysisResult through Populate
// and turns it into rows; none phrases a sentence of diagnosis - those come from
// Recommendations and from ReportWriter, the figures from Fmt. The descriptions, the guides and
// the checks are the view-models' own explanation, every number in them interpolated from
// Thresholds, Parameters or the result (ADR 0020); one is derived here, the Preview guide's
// 1 / ScreenStepBackFactor, when the print is larger than the 100 % view.
//
// All in one file because they are all the same thing: a list of rows and a bool HasResult.
// When one grows, it takes a file of its own.

using System;
using System.Collections.Generic;
using System.Linq;

namespace AstroPhotoAnalyzer.Wpf
{
    /// <summary>
    /// The base of the seven tabs: <see cref="HasResult"/>, filled by <see cref="Populate"/>
    /// from an analysis and emptied by <see cref="Clear"/>.
    /// </summary>
    public abstract class ResultTabViewModel : ObservableObject
    {
        bool _has;
        public bool HasResult { get => _has; protected set => Set(ref _has, value); }

        /// <summary>
        /// Takes an analysis and sets <see cref="HasResult"/>: the six tabs of this file fill
        /// their rows from it; the Curbe tab only keeps it, until "Calculeaza".
        /// </summary>
        /// <param name="r">The analysis; not null.</param>
        internal abstract void Populate(AnalysisResult r);

        /// <summary>
        /// Empties the tab and clears <see cref="HasResult"/>; an override empties its rows too.
        /// </summary>
        internal virtual void Clear()
        {
            HasResult = false;
        }

        /// <summary>A <see cref="MetricRow"/>, for the tabs' row lists.</summary>
        /// <param name="label">The row's label.</param>
        /// <param name="value">The formatted value.</param>
        /// <param name="note">The note, on its own line under the label; null for none.</param>
        /// <param name="description">What the row measures; null for none.</param>
        /// <returns>The row.</returns>
        protected static MetricRow Row(string label, string value, string note = null, string description = null)
            => new MetricRow { Label = label, Value = value, Note = note, Description = description };
    }

    // ------------------------------------------------------------ Raport

    /// <summary>
    /// The Raport tab (R-112): the overall verdict, one section per topic with its
    /// recommendations, and the full report text behind a toggle.
    /// </summary>
    public sealed class ReportTabViewModel : ResultTabViewModel
    {
        IReadOnlyList<SectionRow> _sections = Array.Empty<SectionRow>();
        string _verdict = "";
        string _text = "";
        bool _showText;

        public IReadOnlyList<SectionRow> Sections { get => _sections; private set => Set(ref _sections, value); }
        public string VerdictLabel { get => _verdict; private set => Set(ref _verdict, value); }

        /// <summary>
        /// The full report, with the banner - exactly what is saved in Raport.txt.
        /// </summary>
        public string Text { get => _text; private set => Set(ref _text, value); }
        public bool ShowText { get => _showText; set => Set(ref _showText, value); }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        internal override void Clear()
        {
            base.Clear();
            Sections = Array.Empty<SectionRow>();
            VerdictLabel = "";
            Text = "";
        }
    }

    // ------------------------------------------------------------ Tonuri

    /// <summary>
    /// The Tonuri tab (R-113): the tonal rows with their descriptions, the L* histogram with
    /// paper black and the visibility threshold marked, the simulation table with a preview
    /// button per row, the cloud rows, the level-L* ladder, the guide, and the requests for the
    /// map and preview windows.
    /// </summary>
    public sealed class TonesTabViewModel : ResultTabViewModel
    {
        /// <summary>
        /// The red sentence under the table when the file has no WCS (R-113); the same in the map
        /// window.
        /// </summary>
        public static string NoWcsSentence => Tr.Pick(
            "The file has no WCS: the clouds were not identified by astrometric coordinates.",
            "Fisierul nu are WCS: norii nu au fost identificati dupa coordonate astrometrice.");

        IReadOnlyList<MetricRow> _rows = Array.Empty<MetricRow>();
        IReadOnlyList<double> _hist = Array.Empty<double>();
        IReadOnlyList<SimulationRowItem> _simulation = Array.Empty<SimulationRowItem>();
        IReadOnlyList<string> _clouds = Array.Empty<string>();
        IReadOnlyList<string> _ladder = Array.Empty<string>();
        IReadOnlyList<GuideParagraph> _guide = Array.Empty<GuideParagraph>();
        double _paperBlack = double.NaN, _lowSeparation = double.NaN;
        string _definition, _exactLine, _noWcs;
        AnalysisResult _result;

        public IReadOnlyList<MetricRow> Rows { get => _rows; private set => Set(ref _rows, value); }
        public IReadOnlyList<double> Histogram { get => _hist; private set => Set(ref _hist, value); }

        /// <summary>
        /// Paper black, in L*, as a position in the histogram (the bins are 0..100).
        /// </summary>
        public double PaperBlack { get => _paperBlack; private set => Set(ref _paperBlack, value); }

        /// <summary>
        /// The visibility threshold, Thresholds.LowSeparationLstar, as a position in the histogram:
        /// the dashed line. It does not depend on the image or the paper (ADR 0029), but it is
        /// set with the result, so that an empty chart has no line.
        /// </summary>
        public double LowSeparation { get => _lowSeparation; private set => Set(ref _lowSeparation, value); }

        /// <summary>
        /// The chart's heading: what each of the two lines is. The threshold comes from
        /// Thresholds, not typed.
        /// </summary>
        public string HistogramHeading => Tr.Pick(
            "L* HISTOGRAM (0..100); red line = paper black, dashed yellow line = "
            + $"the visibility threshold L* {Fmt.F0(Thresholds.LowSeparationLstar)}",
            "HISTOGRAMA L* (0..100); linia rosie = negrul hartiei, linia galbena intrerupta = "
            + $"pragul de vizibilitate L* {Fmt.F0(Thresholds.LowSeparationLstar)}");

        /// <summary>
        /// The note under the chart. Each sentence says what lies left of one line and never
        /// which line is left of the other: on canvas (Dmax 1.5) paper black is L* 20.7, right of
        /// the threshold, and the text must hold there too. The second sentence speaks only of
        /// the tones the paper still separates, since part of what is left of the threshold is
        /// also under paper black, on every paper.
        /// </summary>
        public string HistogramNote => Tr.Pick(
            "Left of the red line everything prints as the same black. "
            + $"Left of the dashed yellow line (under L* {Fmt.F0(Thresholds.LowSeparationLstar)}), the tones the paper "
            + "still separates are barely told apart by the eye, in room light. The histogram is on a subsample, in percent.",
            "La stanga liniei rosii totul se printeaza cu acelasi negru. "
            + $"La stanga liniei galbene intrerupte (sub L* {Fmt.F0(Thresholds.LowSeparationLstar)}), tonurile pe care hartia "
            + "le mai separa abia se deosebesc cu ochiul, in lumina de camera. Histograma e pe subesantion, in procente.");

        /// <summary>
        /// The simulation table: the header, then a row per target, each with its preview
        /// button, then the footer (R-113, R-131).
        /// </summary>
        public IReadOnlyList<SimulationRowItem> SimulationRows { get => _simulation; private set => Set(ref _simulation, value); }

        /// <summary>
        /// The definition of the dust and the sky in use, under the table (R-044).
        /// </summary>
        public string Definition { get => _definition; private set => Set(ref _definition, value); }

        /// <summary>
        /// The exact target, the two at the neighbouring Dmax, the Dmax from which the dust reaches
        /// paper black (R-043).
        /// </summary>
        public string ExactLine { get => _exactLine; private set => Set(ref _exactLine, value); }

        /// <summary>The red sentence without a WCS; null with one (hidden).</summary>
        public string NoWcsWarning { get => _noWcs; private set => Set(ref _noWcs, value); }

        /// <summary>
        /// The catalogued clouds with a row, all of them, as mono text (the report shows the first
        /// DustCloudReportRows).
        /// </summary>
        public IReadOnlyList<string> CloudRows { get => _clouds; private set => Set(ref _clouds, value); }

        /// <summary>
        /// The level - L* table of the tab's landmarks, from TonalAnalyzer.LstarLadder; in the
        /// window only.
        /// </summary>
        public IReadOnlyList<string> LadderRows { get => _ladder; private set => Set(ref _ladder, value); }

        /// <summary>
        /// The "how to read the tab" panel. Its figures come from Thresholds, Parameters and
        /// ColorMath, none typed in.
        /// </summary>
        public IReadOnlyList<GuideParagraph> Guide { get => _guide; private set => Set(ref _guide, value); }

        /// <summary>
        /// The tooltip of a row's Preview button: the threshold comes from Thresholds, not typed.
        /// </summary>
        public string PreviewTooltip => Tr.Pick($"The image at the row's target, with what is under paper black and under L* {Fmt.F0(Thresholds.LowSeparationLstar)} painted", $"Imaginea la tinta randului, cu ce e sub negrul hartiei si sub L* {Fmt.F0(Thresholds.LowSeparationLstar)} vopsit");

        /// <summary>
        /// Opens the dust / sky map (R-130). Whoever listens to MapRequested opens the window;
        /// nothing opens here.
        /// </summary>
        public System.Windows.Input.ICommand OpenMap { get; }

        public event Action<MapViewModel> MapRequested;
        public event Action<PreviewWindowViewModel> PreviewRequested;

        /// <summary>Makes the map command, enabled once there is a result.</summary>
        public TonesTabViewModel()
        {
            OpenMap = new RelayCommand(() => { if (_result != null) MapRequested?.Invoke(BuildMap()); }, () => _result != null);
        }

        /// <summary>The map for the current result, built on request (R-130).</summary>
        /// <returns>A new map view-model; a result must be present.</returns>
        internal MapViewModel BuildMap() => MapViewModel.From(_result);

        /// <summary>The preview of a simulation row, built on request (R-131).</summary>
        /// <param name="row">A row of the current result's simulation table.</param>
        /// <returns>A new preview view-model; a result must be present.</returns>
        internal PreviewWindowViewModel BuildPreview(SimulationRow row) => PreviewWindowViewModel.From(_result, row);

        /// <inheritdoc/>
        internal override void Populate(AnalysisResult r)
        {
            TonalResult t = r.Tonal;
            SimulationResult s = r.Simulation;
            _result = r;
            Rows = new[]
            {
                Row(Tr.Pick("Channel means (0..255)", "Medii pe canal (0..255)"), string.Join(" / ", t.MeanLevels.Select(Fmt.F1)),
                    description: Tr.Pick("The mean of each channel over all the pixels, in 8-bit levels. The sky fills almost the whole frame, "
                        + "so the means sit close to the background; the differences between the channels are analysed as a cast in the Colour tab.",
                        "Media fiecarui canal pe toti pixelii, in niveluri de 8 biti. Cerul ocupa aproape tot cadrul, "
                        + "asa ca mediile stau aproape de fundal; diferentele dintre canale sunt analizate ca dominanta in tabul Culoare.")),
                Row(Tr.Pick($"Under level {Thresholds.DarkLevelA} / {Thresholds.DarkLevelB}", $"Sub nivelul {Thresholds.DarkLevelA} / {Thresholds.DarkLevelB}"), $"{Fmt.Pct(t.PctBelowDarkA)} / {Fmt.Pct(t.PctBelowDarkB)}",
                    description: Tr.Pick($"How much of the image (all the channels together) is under {Thresholds.DarkLevelA} and under {Thresholds.DarkLevelB} of 255, "
                        + "the thresholds you read on Siril's histogram. Context only: the print verdict is given on L*, below.",
                        $"Cat din imagine (toate canalele la un loc) e sub {Thresholds.DarkLevelA} si sub {Thresholds.DarkLevelB} din 255, "
                        + "pragurile pe care le citesti pe histograma din Siril. Doar context: verdictul de print se da pe L*, mai jos.")),
                Row(Tr.Pick("Background level (median of the reference channel)", "Nivelul fundalului (mediana canalului de referinta)"), Fmt.F3(t.BackgroundMedian),
                    description: Tr.Pick("The median of the green channel (of the only channel, in mono), on the 0..1 scale. As the sky dominates the frame, "
                        + "the median is the background level: how high the sky sits. The simulation below starts from here. "
                        + "The colour of the sky is another measurement, in the Colour tab.",
                        "Mediana canalului verde (a singurului canal la mono), pe scara 0..1. Cum cerul domina cadrul, "
                        + "mediana e nivelul fundalului: cat de sus sta cerul. De aici porneste simularea de mai jos. "
                        + "Culoarea cerului e alta masuratoare, in tabul Culoare.")),
                Row(Tr.Pick("Median L*", "L* median"), Fmt.F1(t.LstarMedian),
                    description: Tr.Pick("The perceived lightness of the median pixel, from 0 (black) to 100 (white). L* is the paper's scale: "
                        + "equal steps of L* look like equal steps of grey. On an astrophoto, the median L* is practically the sky.",
                        "Luminozitatea perceputa a pixelului median, de la 0 (negru) la 100 (alb). L* e scara hartiei: "
                        + "pasi egali de L* se vad ca pasi egali de gri. Pe o astrofoto, L* median e practic cerul.")),
                Row(Tr.Pick($"Under paper black (L* {Fmt.F1(t.PaperBlackLstar)}, Dmax {Fmt.Dmax(t.Dmax)})", $"Sub negrul hartiei (L* {Fmt.F1(t.PaperBlackLstar)}, Dmax {Fmt.Dmax(t.Dmax)})"), Fmt.Pct(t.PctBelowPaperBlack),
                    description: Tr.Pick("The pixels darker than the deepest black the chosen paper gives. They all come out as the same black, "
                        + "so the shades between them are lost. It is a fact of the image, not the verdict: the verdict is given on the dust and the sky, "
                        + "in the simulation below.",
                        "Pixelii mai intunecati decat cel mai adanc negru pe care il da hartia aleasa. Toti ies cu acelasi negru, "
                        + "deci nuantele dintre ei se pierd. E un fapt al imaginii, nu verdictul: verdictul se da pe praf si pe cer, "
                        + "in simularea de mai jos.")),
                Row(Tr.Pick($"Under L* {Fmt.F0(Thresholds.LowSeparationLstar)} (weak separation)", $"Sub L* {Fmt.F0(Thresholds.LowSeparationLstar)} (separare slaba)"), Fmt.Pct(t.PctBelowLowSeparation),
                    description: Tr.Pick("Tones the paper still separates, but which the eye, in ordinary room light, barely tells apart. "
                        + "It includes the pixels under paper black. A large share means a sky that will look like a uniform patch.",
                        "Tonuri pe care hartia inca le separa, dar pe care ochiul, in lumina obisnuita de camera, abia le deosebeste. "
                        + "Include si pixelii de sub negrul hartiei. Un procent mare inseamna un cer care va parea o pata uniforma.")),
                Row(Tr.Pick($"Over L* {Fmt.F0(Thresholds.HighlightLstar)}", $"Peste L* {Fmt.F0(Thresholds.HighlightLstar)}"), Fmt.Pct(t.PctAboveHighlight),
                    description: Tr.Pick("The pixels lighter than middle grey: stars, the core, bright nebulosity. "
                        + "It shows how much of the frame carries the highlights.",
                        "Pixelii mai luminosi decat griul de mijloc: stele, nucleu, nebulozitate stralucitoare. "
                        + "Arata cat din cadru poarta luminile.")),
                // One percentile per line: on a single line the value would be the widest in the
                // panel and would narrow the description column for every row.
                Row(Tr.Pick("Reference channel percentiles", "Percentile canal referinta"), string.Join("\n", TonalResult.PercentileRanks.Zip(t.ReferencePercentiles, (p, v) => $"{"p" + Fmt.F1(p),-6} = {Fmt.F1(v),5}")),
                    description: Tr.Pick("The level (0..255) under which a percentage of the reference channel's pixels falls: p50 is the background, "
                        + "p0.1 the deepest black that counts, p99.9 and p100 the brightest stars. p100 = 255 means saturated pixels.",
                        "Nivelul (0..255) sub care cade un procent din pixelii canalului de referinta: p50 e fundalul, "
                        + "p0.1 cel mai adanc negru care conteaza, p99.9 si p100 stelele cele mai luminoase. p100 = 255 inseamna pixeli saturati.")),
            };
            Histogram = t.LstarHistogramPct;
            PaperBlack = t.PaperBlackLstar;
            LowSeparation = Thresholds.LowSeparationLstar;
            // The same mono text as in Anexa B, plus the preview button on every row (the header
            // has none).
            var sim = new List<SimulationRowItem> { new SimulationRowItem { Text = ReportWriter.SimulationHeader } };
            foreach (SimulationRow row in s.Rows)
            {
                SimulationRow captured = row;
                sim.Add(new SimulationRowItem
                {
                    Text = ReportWriter.SimulationLine(row),
                    Target = row.AsIs ? Tr.Pick("as is", "asa cum e") : Fmt.Level(row.Target) + (row.Explicit ? Tr.Pick(" (given)", " (dat)") : ""),
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
            // The English column titles end where the Romanian ones do, over the right-aligned figures.
            LadderRows = new[] { Tr.Pick("   L*  level (grey)   landmark", "   L*   nivel (gri)   reper") }
                .Concat(TonalAnalyzer.LstarLadder(r.Parameters, s).Select(l =>
                    $"{Fmt.F1(l.Lstar),5}   {Fmt.F1(l.GrayLevel),11}   {l.Meaning}{(l.Chosen ? Tr.Pick("   <- the chosen medium", "   <- mediul ales") : "")}"))
                .ToList();
            Guide = GuideParagraphs();
            HasResult = true;
            (OpenMap as RelayCommand)?.RaiseCanExecuteChanged();
        }

        /// <inheritdoc/>
        internal override void Clear()
        {
            base.Clear();
            _result = null;
            Rows = Array.Empty<MetricRow>();
            Histogram = Array.Empty<double>();
            PaperBlack = double.NaN;
            LowSeparation = double.NaN;
            SimulationRows = Array.Empty<SimulationRowItem>();
            CloudRows = Array.Empty<string>();
            LadderRows = Array.Empty<string>();
            Guide = Array.Empty<GuideParagraph>();
            Definition = null;
            ExactLine = null;
            NoWcsWarning = null;
            (OpenMap as RelayCommand)?.RaiseCanExecuteChanged();
        }

        /// <summary>
        /// The panel's text: explanation, not diagnosis - it does not depend on the image, only
        /// on the thresholds.
        /// </summary>
        /// <returns>The six paragraphs, "HOW TO READ THE TAB" ("CUM SE CITESTE TABUL").</returns>
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
                    Lead = Tr.Pick("What the tab asks.", "Ce intreaba tabul."),
                    Text = Tr.Pick("Does the background survive the paper? On screen the black goes down almost to zero; on paper the darkest tone is that "
                        + "of the ink at saturation, and everything under it prints the same. The sky of an astrophotograph sits right in this zone, "
                        + "so a background that looks good on the monitor can come out on paper as a black patch without shades.",
                        "Supravietuieste fundalul hartiei? Pe ecran negrul coboara aproape la zero; pe hartie cel mai intunecat ton e cel "
                        + "al cernelii la saturatie, iar tot ce e sub el se printeaza la fel. Cerul unei astrofotografii sta tocmai in zona asta, "
                        + "asa ca un fundal care arata bine pe monitor poate iesi pe hartie ca o pata neagra fara nuante."),
                },
                new GuideParagraph
                {
                    Lead = Tr.Pick("Two scales, on purpose.", "Doua scari, deliberat."),
                    Text = Tr.Pick("The levels 0..255 are the language of the histogram in Siril and in any editor; L* (0..100) is the language of the paper, "
                        + "the lightness as the eye sees it. The conversion takes three steps: the sRGB curve is undone, the channels are added "
                        + "with weights (green weighs ten times as much as blue) and the cube root of L* is applied. "
                        + $"On a grey, level {Fmt.F0(ColorMath.GrayLevelOfLstar(barytaBlack))} is L* {Fmt.F0(barytaBlack)} (the baryta black), "
                        + $"{Thresholds.DarkLevelA} is L* {Fmt.F0(ColorMath.LstarOfGrayLevel(Thresholds.DarkLevelA))}, "
                        + $"{Thresholds.DarkLevelB} is L* {Fmt.F0(ColorMath.LstarOfGrayLevel(Thresholds.DarkLevelB))}. "
                        + "On colour, the same level gives a different L* by its hue, which is why both are shown.",
                        "Nivelurile 0..255 sunt limbajul histogramei din Siril si din orice editor; L* (0..100) e limbajul hartiei, "
                        + "luminozitatea asa cum o vede ochiul. Trecerea se face in trei pasi: se anuleaza curba sRGB, se aduna canalele "
                        + "ponderat (verdele cantareste de zece ori cat albastrul) si se aplica radacina cubica a L*-ului. "
                        + $"Pe un gri, nivelul {Fmt.F0(ColorMath.GrayLevelOfLstar(barytaBlack))} e L* {Fmt.F0(barytaBlack)} (negrul baryta), "
                        + $"{Thresholds.DarkLevelA} e L* {Fmt.F0(ColorMath.LstarOfGrayLevel(Thresholds.DarkLevelA))}, "
                        + $"{Thresholds.DarkLevelB} e L* {Fmt.F0(ColorMath.LstarOfGrayLevel(Thresholds.DarkLevelB))}. "
                        + "Pe culoare, acelasi nivel da L* diferit dupa nuanta, de aceea se arata amandoua."),
                },
                new GuideParagraph
                {
                    Lead = Tr.Pick("Paper black", "Negrul hartiei"),
                    Text = Tr.Pick($"comes from the Dmax, the maximum optical density of the medium: baryta {Black(PrintMedium.Baryta)}, "
                        + $"canvas {Black(PrintMedium.Canvas)}, a C-print under acrylic {Black(PrintMedium.Acrylic)}. The medium is chosen in the parameters; "
                        + "the same image can be print-safe on baryta and too dark on canvas.",
                        $"vine din Dmax, densitatea optica maxima a mediului: baryta {Black(PrintMedium.Baryta)}, "
                        + $"canvas {Black(PrintMedium.Canvas)}, C-print sub acril {Black(PrintMedium.Acrylic)}. Mediul se alege in parametri; "
                        + "aceeasi imagine poate fi print-safe pe baryta si prea intunecata pe canvas."),
                },
                new GuideParagraph
                {
                    Lead = Tr.Pick("The dust and the sky.", "Praful si cerul."),
                    Text = Tr.Pick($"The image is divided into patches of {Thresholds.DustPatchPx} x {Thresholds.DustPatchPx} px, each with the L* of its mean luminance: "
                        + "what the eye integrates over a spot of paper, not a pixel. The patches are sorted into five classes, relatively, not by a fixed threshold, along one of two paths. "
                        + "With a plate-solved FITS (a WCS in the header, or lent with the \"WCS from FITS\" field) and with the Milky Way in the frame: the band is the galactic latitude |b| under "
                        + $"{Fmt.F0(Thresholds.BandGalacticLatitudeDeg)} degrees; the sky is modelled from the patches outside it; the dust is a patch {Fmt.F0(Thresholds.DustDepthLstar)} L* below the band's local level "
                        + $"(the median over ~{Thresholds.BandLocalWindowPatches * Thresholds.DustPatchPx} px) or below the modelled sky, that is darker than the band would be there; the bright clouds, "
                        + $"{Fmt.F0(Thresholds.BrightCloudLstar)} L* over the local level; the peaks, the brightest {Fmt.F1(Thresholds.PeakFraction * 100)} % of the patches. "
                        + $"This path needs the band and sky outside it, each with at least {Fmt.F0(Thresholds.BandMinFraction * 100)} % of the patches, and a field that covers at least "
                        + $"{Fmt.F0(Thresholds.BandMinLatitudeSpanDeg)} degrees of galactic latitude, the width of the band: on a narrower field the band is not a strip but a gradient, and the frame is deep sky in the band. Otherwise, as without a WCS, the classes are relative to the sky "
                        + $"modelled from all the patches (the {Fmt.F0(Thresholds.SkyModelLowPercentile)} percentile of each cell), with thresholds from the image: the nebula is a patch more than {Fmt.F0(Thresholds.SkyNoiseK)} sigma of the "
                        + "sky over the model, the dust one as much under it, the bright nebula one over the nebula's Otsu threshold, the peaks as above. "
                        + $"On either path, the sky's L* is the median of the sky patches, and the dust's L* is the darkest extended area: the {Fmt.F0(Thresholds.DustClassFraction * 100)} % percentile of the dust patches "
                        + $"when the class holds at least {Fmt.F0(Thresholds.DustClassMinFraction * 100)} % of the patches (under that they are cores or artefacts), or the darkest {Fmt.F0(Thresholds.SkyDarkestFraction * 100)} % of the sky patches, "
                        + $"whichever of the two is darker. The Lynds catalogue gives the names of the clouds and a row for each one with at least {Thresholds.DustCloudMinPatches} patches, with the target at which it reaches black. "
                        + "The \"Dust / sky map\" button shows the classes on the image and the clouds by name.",
                        $"Imaginea se imparte in dale de {Thresholds.DustPatchPx} x {Thresholds.DustPatchPx} px, fiecare cu L*-ul luminantei ei medii: "
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
                        + "Butonul \"Harta praf / cer\" arata clasele pe imagine si norii pe nume."),
                },
                new GuideParagraph
                {
                    Lead = Tr.Pick("The verdict.", "Verdictul."),
                    Text = Tr.Pick("The dust under paper black, the sky over it: caution, the clouds would print pure black. "
                        + "The sky under black: a problem, the sky and the dust come out as the same black. It is not a share of the pixels: it is the darkest "
                        + "extended structure against paper black. Then comes the viewing: the paper separates tones even lower, but a sky under "
                        + $"L* {Fmt.F0(Thresholds.LowSeparationLstar)} looks almost black in room light, so it too is a caution, however deep the paper black. "
                        + "Print-safe means both: the dust at or over paper black and the sky at the threshold or over it; a row with the dust on black "
                        + $"and the sky below it is marked \"sky under L* {Fmt.F0(Thresholds.LowSeparationLstar)}\". "
                        + "The recommendation is the MINIMUM print-safe level - upwards "
                        + "on an image that is too dark, downwards on one that is too bright.",
                        "Praful sub negrul hartiei, cerul peste: atentie, norii s-ar tipari negru pur. "
                        + "Cerul sub negru: problema, cerul si praful ies cu acelasi negru. Nu e un procent de pixeli: e cea mai intunecata "
                        + "structura intinsa fata de negrul hartiei. Apoi vine privitul: hartia separa tonurile si mai jos, dar un cer sub "
                        + $"L* {Fmt.F0(Thresholds.LowSeparationLstar)} se vede aproape negru in lumina de camera, deci si el e atentie, oricat de adanc ar fi negrul hartiei. "
                        + "Print-safe inseamna amandoua: praful la sau peste negrul hartiei si cerul la prag sau peste; un rand cu praful pe negru "
                        + $"si cerul dedesubt e marcat \"cer sub L* {Fmt.F0(Thresholds.LowSeparationLstar)}\". "
                        + "Recomandarea e nivelul MINIM print-safe - in sus "
                        + "pe o imagine prea intunecata, in jos pe una prea luminoasa."),
                },
                new GuideParagraph
                {
                    Lead = Tr.Pick("The simulation", "Simularea"),
                    Text = Tr.Pick($"answers \"and what if I move the background?\". It applies to a reduced copy of the image a gamma curve, the same on every channel, "
                        + $"that takes the measured background to each target of the grid {Fmt.Level(grid.Min())}..{Fmt.Level(grid.Max())}, then re-measures the dust and the sky. "
                        + "The \"as is\" row sits among the targets, at its level. The lowest target marked print-safe is the recommended one: redo "
                        + "the stretch from the linear data in Siril up to that background. Under the table: the exact target at which the dust reaches black, "
                        + $"and the same target at a Dmax {Fmt.F1(Thresholds.DmaxSensitivityStep)} lower and higher, because the Dmax of a paper on a printer is rarely known better. "
                        + "It is a simulation: a real stretch (Siril, VeraLux) holds the shadows lower than the gamma curve, so the exact target is the curve's, "
                        + $"not the stretch's. The Preview button of a row shows the image at its target, with what is under black and under L* {Fmt.F0(Thresholds.LowSeparationLstar)} painted. "
                        + $"A target that is too high washes the sky into grey; watch the \"L* > {Fmt.F0(Thresholds.HighlightLstar)}\" column.",
                        $"raspunde la \"si daca mut fundalul?\". Aplica pe o copie redusa a imaginii o curba gamma, aceeasi pe toate canalele, "
                        + $"care duce fundalul masurat la fiecare tinta din grila {Fmt.Level(grid.Min())}..{Fmt.Level(grid.Max())}, apoi remasoara praful si cerul. "
                        + "Randul \"asa cum e\" sta printre tinte, la nivelul lui. Cea mai mica tinta marcata print-safe e cea recomandata: refa "
                        + "stretch-ul din datele liniare in Siril pana la fundalul acela. Sub tabel: tinta exacta la care praful atinge negrul, "
                        + $"si aceeasi tinta la Dmax cu {Fmt.F1(Thresholds.DmaxSensitivityStep)} mai mic si mai mare, fiindca Dmax-ul unei hartii pe o imprimanta rar e stiut mai bine. "
                        + "E o simulare: un stretch adevarat (Siril, VeraLux) tine umbrele mai jos decat curba gamma, deci tinta exacta e a curbei, "
                        + $"nu a lui. Butonul Preview al unui rand arata imaginea la tinta lui, cu ce e sub negru si sub L* {Fmt.F0(Thresholds.LowSeparationLstar)} vopsit. "
                        + $"O tinta prea mare spala cerul in gri; urmareste coloana \"peste L*{Fmt.F0(Thresholds.HighlightLstar)}\"."),
                },
            };
        }
    }

    // ------------------------------------------------------------ Export

    /// <summary>
    /// The Export tab (R-114): the JPEG table with its guide, and the rows of the TIFF size, the
    /// rounding, the stress test and the plateau, each with its description.
    /// </summary>
    public sealed class ExportTabViewModel : ResultTabViewModel
    {
        IReadOnlyList<string> _jpeg = Array.Empty<string>();
        IReadOnlyList<GuideParagraph> _jpegGuide = Array.Empty<GuideParagraph>();
        IReadOnlyList<MetricRow> _rows = Array.Empty<MetricRow>();

        public IReadOnlyList<string> JpegRows { get => _jpeg; private set => Set(ref _jpeg, value); }

        /// <summary>
        /// The paragraphs under the JPEG table: how it is measured, what each column is, what is
        /// recommended.
        /// </summary>
        public IReadOnlyList<GuideParagraph> JpegGuide { get => _jpegGuide; private set => Set(ref _jpegGuide, value); }
        public IReadOnlyList<MetricRow> Rows { get => _rows; private set => Set(ref _rows, value); }

        /// <inheritdoc/>
        internal override void Populate(AnalysisResult r)
        {
            QuantizationResult q = r.Quantization;
            // The report's header without its indent; each English column title ends where the Romanian one does.
            JpegRows = new[] { Tr.Pick("JPEG 4:4:4     size      mean err   max err   p99    p99.9   > 2 lev    mean level",
                                       "JPEG 4:4:4   marime     err medie   err max   p99    p99.9   > 2 niv   nivel mediu") }
                .Concat(r.Jpeg.Rows.Select(j =>
                    $"q{j.Quality,-3}         {Fmt.Mb(j.Megabytes),8}   {Fmt.F3(j.MeanAbsError),9}   {Fmt.F1(j.MaxError),7}   {Fmt.F2(j.P99),4}   {Fmt.F2(j.P999),5}   {Fmt.Pct(j.PctOverThreshold),7}   {Fmt.F0(j.MeanLevelOverThreshold),11}"))
                .ToList();
            JpegGuide = JpegGuideParagraphs();

            // The descriptions: what the row measures, with the thresholds from Thresholds, not
            // typed in.
            string lo = Fmt.Int(Thresholds.ShadowLiftLevelLo), hi = Fmt.Int(Thresholds.ShadowLiftLevelHi);
            var rows = new List<MetricRow>
            {
                Row("TIFF 8-bit LZW", Fmt.Mb(r.Jpeg.Tiff8LzwMegabytes),
                    description: Tr.Pick("The size of the file you send to the lab: the same image, reduced to 8 bits, written as a TIFF with LZW compression "
                        + "(lossless) and a horizontal predictor. It is the recommended format: no compression artefacts, accepted by any lab, "
                        + "at half the size of a 16-bit TIFF. The figure is that of the tool's encoder, not Siril's; the order of magnitude is the same.",
                        "Marimea fisierului pe care il trimiti la laborator: aceeasi imagine, redusa la 8 biti, scrisa TIFF cu compresie LZW "
                        + "(fara pierdere) si predictor orizontal. E formatul recomandat: fara artefacte de compresie, acceptat de orice laborator, "
                        + "la jumatate din marimea unui TIFF de 16 biti. Cifra e a encoderului din unealta, nu a lui Siril; ordinul de marime e acelasi.")),
            };
            if (q.Applicable)
            {
                rows.Add(Row(Tr.Pick("Rounding 16 -> 8 bits: mean / max / p99.9", "Rotunjire 16 -> 8 biti: medie / maxim / p99.9"), $"{Fmt.F3(q.RoundingMeanAbs)} / {Fmt.F3(q.RoundingMax)} / {Fmt.F3(q.RoundingP999)}",
                    description: Tr.Pick("How much is lost when each 16-bit value is rounded to the nearest of 255 levels: the mean, the maximum and the p99.9 "
                        + "of the error, over all the channels. Half a level is the mathematical maximum, so a maximum of 0.5 is not a defect, it is the definition "
                        + "of rounding. It is a loss of precision, not of visible information: the eye does not tell two neighbouring levels of 255 apart. "
                        + "The row appears only when the source has more than 8 bits.",
                        "Cat se pierde cand fiecare valoare de 16 biti se rotunjeste la cel mai apropiat nivel din 255: media, maximul si p99.9 "
                        + "ale erorii, pe toate canalele. Jumatate de nivel e maximul matematic posibil, deci un maxim de 0.5 nu e un defect, e definitia "
                        + "rotunjirii. E o pierdere de precizie, nu de informatie vizibila: ochiul nu deosebeste doua niveluri vecine din 255. "
                        + "Randul apare doar cand sursa are mai mult de 8 biti.")));
                rows.Add(Row(Tr.Pick($"Shadow-lift stress, levels {lo}..{hi}: from 16 bits", $"Stres shadow-lift, niveluri {lo}..{hi}: din 16 biti"), Tr.Pick($"{q.LevelsUsedFrom16} used / {q.MissingFrom16} missing", $"{q.LevelsUsedFrom16} folosite / {q.MissingFrom16} lipsa"),
                    description: Tr.Pick("It answers \"what happens if the shadows are pulled further?\". The reference channel is lifted with a gamma curve "
                        + $"{Fmt.F1(Thresholds.ShadowLiftGamma)}, a typical lab auto-correct, then quantized to 8 bits; the levels used between {lo} and {hi} "
                        + "are counted, and how many are missing between the first and the last one used. Starting from the 16-bit data, the stretched shadows "
                        + "have something to fill the levels with: this row is the reference for the one under it.",
                        "Raspunde la \"ce se intampla daca se mai trage de umbre?\". Canalul de referinta e ridicat cu o curba gamma "
                        + $"{Fmt.F1(Thresholds.ShadowLiftGamma)}, un auto-correct tipic de laborator, apoi cuantizat la 8 biti; se numara nivelurile "
                        + $"folosite intre {lo} si {hi} si cate lipsesc intre primul si ultimul folosit. Pornind din datele de 16 biti, umbrele intinse "
                        + "au de unde sa umple nivelurile: randul asta e referinta pentru cel de sub el.")));
                rows.Add(Row(Tr.Pick("... from 8 bits", "... din 8 biti"),
                    Tr.Pick($"{q.LevelsUsedFrom8} used / {q.MissingFrom8} missing", $"{q.LevelsUsedFrom8} folosite / {q.MissingFrom8} lipsa"),
                    q.Keep16BitMaster ? Tr.Pick("keep the 16-bit master", "pastreaza master-ul de 16 biti") : null,
                    Tr.Pick("The same lift, but starting from the data already rounded to 8 bits, that is from the print file. Each missing level is a step in "
                        + "the histogram, a gap nothing fills any more, visible as banding if the shadows are smooth. "
                        + $"Over {Fmt.Int(Thresholds.MissingLevelsWarn)} more missing levels than on the 16-bit path, keep the 16-bit master: any later edit "
                        + "(a reframing, a retouch, another print) starts from it, not from the 8-bit file. As long as the lab prints the file as it is, "
                        + "8 bits are enough; the test is about what is left for later.",
                        "Acelasi lift, dar pornind din datele deja rotunjite la 8 biti, adica din fisierul de tipar. Fiecare nivel lipsa e o treapta in "
                        + "histograma, un gol pe care nu-l mai umple nimic, vizibil ca banding daca umbrele sunt netede. "
                        + $"Peste {Fmt.Int(Thresholds.MissingLevelsWarn)} de niveluri lipsa in plus fata de calea de 16 biti, pastreaza master-ul de 16 biti: orice editare ulterioara "
                        + "(recadrare, retus, alt print) pleaca din el, nu din fisierul de 8 biti. Cat timp laboratorul printeaza fisierul asa cum e, "
                        + "8 biti ajung; testul e despre ce ramane pentru mai tarziu.")));
            }
            string plateauDesc = Tr.Pick($"On up to {Fmt.Int(Thresholds.PlateauTileCount)} clean sky tiles (a range under {Fmt.F0(Thresholds.PlateauTileMaxRangeLevels)} levels "
                + $"and a mean under {Fmt.F0(Thresholds.PlateauTileMaxMeanLevels)}, so as not to catch the core) it is measured, at 8 bits, how long the runs "
                + "of neighbouring pixels with exactly the same value are. A noiseless sky quantized to 8 bits makes wide steps; the residual noise breaks them, "
                + "like a natural dither. The plateau in pixels, times the pixel pitch at the reference row's dpi, gives the length of the step on "
                + $"paper: over {Fmt.F1(Thresholds.PlateauVisibleMm)} mm it is a visible contour, a banding risk - 16 bits if the lab accepts it, "
                + "or dither at the export. n/a when no clean sky tiles are found.",
                $"Pe pana la {Fmt.Int(Thresholds.PlateauTileCount)} de dale de cer curat (interval sub {Fmt.F0(Thresholds.PlateauTileMaxRangeLevels)} niveluri "
                + $"si medie sub {Fmt.F0(Thresholds.PlateauTileMaxMeanLevels)}, ca sa nu prinda nucleul) se masoara, pe 8 biti, cat de lungi sunt sirurile "
                + "de pixeli vecini cu exact aceeasi valoare. Un cer fara zgomot cuantizat la 8 biti face trepte late; zgomotul rezidual le sparge, "
                + "ca un dither natural. Palierul in pixeli, inmultit cu pasul pixelului la dpi-ul randului de referinta, da lungimea treptei pe "
                + $"hartie: peste {Fmt.F1(Thresholds.PlateauVisibleMm)} mm e un contur vizibil, risc de banding - 16 biti daca laboratorul accepta, "
                + "sau dither la export. n/a cand nu se gasesc dale de cer curat.");
            rows.Add(q.PlateauKnown
                ? Row(Tr.Pick("Plateau on the quantized sky", "Palier pe cerul cuantizat"), $"{Fmt.F1(q.PlateauPx)} px",
                    Tr.Pick($"over {q.PlateauTiles} tiles", $"pe {q.PlateauTiles} dale")
                    + (r.Size.ReferenceRow != null ? Tr.Pick($"; {Fmt.F2(q.PlateauMmAt(r.Size.ReferenceRow.Dpi))} mm at {Fmt.F0(r.Size.ReferenceRow.Dpi)} dpi", $"; {Fmt.F2(q.PlateauMmAt(r.Size.ReferenceRow.Dpi))} mm la {Fmt.F0(r.Size.ReferenceRow.Dpi)} dpi") : ""), plateauDesc)
                : Row(Tr.Pick("Plateau on the quantized sky", "Palier pe cerul cuantizat"), "n/a", Tr.Pick("no clean sky tiles", "fara dale de cer curat"), plateauDesc));
            Rows = rows;
            HasResult = true;
        }

        /// <summary>
        /// The text under the JPEG table: explanation, not diagnosis - it does not depend on the
        /// image, only on the thresholds.
        /// </summary>
        /// <returns>The five paragraphs.</returns>
        static IReadOnlyList<GuideParagraph> JpegGuideParagraphs()
        {
            string count = Fmt.F0(Thresholds.JpegErrorCountLevels);
            return new[]
            {
                new GuideParagraph
                {
                    Lead = Tr.Pick("How it is measured.", "Cum se masoara."),
                    Text = Tr.Pick("The image reduced to 8 bits is encoded as JPEG in memory at each quality, 4:4:4 (no chroma subsampling), "
                        + "decoded and compared with the unrounded 16-bit values, x 255. The error therefore includes the rounding to 8 bits, "
                        + "which is why not even q100 gives zero. The encoder is the tool's, not the one in Siril or Photoshop: the figures are indicative, "
                        + "the order and the proportions hold.",
                        "Imaginea redusa la 8 biti e encodata JPEG in memorie la fiecare calitate, 4:4:4 (fara subesantionarea culorii), "
                        + "decodata si comparata cu valorile de 16 biti nerotunjite, x 255. Eroarea include deci si rotunjirea la 8 biti, "
                        + "de aceea nici q100 nu da zero. Encoderul e cel al uneltei, nu cel din Siril sau Photoshop: cifrele sunt orientative, "
                        + "ordinea si proportiile raman."),
                },
                new GuideParagraph
                {
                    Lead = Tr.Pick("Size.", "Marime."),
                    Text = Tr.Pick("The file's megabytes at that quality, to compare with the 8-bit LZW TIFF in the panel below.",
                        "Megabytii fisierului la calitatea respectiva, de comparat cu TIFF-ul 8-bit LZW din panoul de mai jos."),
                },
                new GuideParagraph
                {
                    Lead = Tr.Pick("Mean err / max / p99 / p99.9.", "Err medie / max / p99 / p99.9."),
                    Text = Tr.Pick("The error per sample, in levels of 255: the mean over the whole image, the largest, and the values under which 99 % "
                        + "and 99.9 % of the samples lie. The mean does not fall monotonically with the quality this close to lossless (the trip through "
                        + "YCbCr in integers costs 1-2 levels by itself), so the verdict is not given on it but on the maximum: "
                        + $"over {Fmt.F0(Thresholds.JpegMaxErrorReject)} levels, the quality is not recommended.",
                        "Eroarea pe esantion, in niveluri din 255: media pe toata imaginea, cea mai mare, si valorile sub care stau 99 %, "
                        + "respectiv 99.9 % din esantioane. Media nu scade monoton cu calitatea atat de aproape de fara-pierdere (trecerea prin "
                        + "YCbCr in intregi costa singura 1-2 niveluri), asa ca verdictul nu se da pe ea, ci pe maxim: "
                        + $"peste {Fmt.F0(Thresholds.JpegMaxErrorReject)} niveluri, calitatea nu se recomanda."),
                },
                new GuideParagraph
                {
                    Lead = Tr.Pick($"> {count} lev / mean level.", $"> {count} niv / nivel mediu."),
                    Text = Tr.Pick($"What percentage of the samples have an error over {count} levels, and at what mean level (0..255) those samples lie - "
                        + "that is where the compression strikes. A low mean level means the dark sky, where JPEG does worst and where "
                        + "the eye sees the blocks on a smooth background; a high one means stars and edges, where the errors hide in the detail. "
                        + $"Over {Fmt.F0(Thresholds.JpegOverThresholdWarnPct)} % of the samples at q100: caution.",
                        $"Cat la suta din esantioane au eroarea peste {count} niveluri, si la ce nivel mediu (0..255) stau acele esantioane - "
                        + "adica unde loveste compresia. Un nivel mediu mic inseamna cerul intunecat, unde JPEG-ul lucreaza cel mai prost si unde "
                        + "ochiul vede blocurile pe fundal neted; unul mare inseamna stele si muchii, unde erorile se ascund in detaliu. "
                        + $"Peste {Fmt.F0(Thresholds.JpegOverThresholdWarnPct)} % din esantioane la q100: atentie."),
                },
                new GuideParagraph
                {
                    Lead = Tr.Pick("What is recommended.", "Ce se recomanda."),
                    Text = Tr.Pick("An 8-bit LZW TIFF. If the lab accepts only JPEG: q100, 4:4:4. Under q100 the maximum error jumps on the stars and the edges, "
                        + "and the size gained does not matter for a print.",
                        "TIFF 8-bit LZW. Daca laboratorul accepta doar JPEG: q100, 4:4:4. Sub q100 eroarea maxima sare pe stele si pe muchii, "
                        + "iar castigul de marime nu conteaza pentru un print."),
                },
            };
        }
    }

    // ------------------------------------------------------------ Rezolutie

    /// <summary>
    /// The Rezolutie tab (R-115): the star rows with their descriptions, the size table as the
    /// report writes it, and the dpi - distance ladder.
    /// </summary>
    public sealed class ResolutionTabViewModel : ResultTabViewModel
    {
        IReadOnlyList<MetricRow> _rows = Array.Empty<MetricRow>();
        IReadOnlyList<string> _sizes = Array.Empty<string>();
        IReadOnlyList<string> _dpi = Array.Empty<string>();

        public IReadOnlyList<MetricRow> Rows { get => _rows; private set => Set(ref _rows, value); }
        public IReadOnlyList<string> SizeRows { get => _sizes; private set => Set(ref _sizes, value); }

        /// <summary>
        /// The dpi - distance ladder, in the window only: the report does not repeat it for every
        /// image.
        /// </summary>
        public IReadOnlyList<string> DpiRows { get => _dpi; private set => Set(ref _dpi, value); }

        /// <inheritdoc/>
        internal override void Populate(AnalysisResult r)
        {
            StarResult s = r.Stars;
            PrintSizeResult ps = r.Size;
            // The descriptions: what the row measures, with the thresholds from Thresholds, not
            // typed in. The corners have no description - the centre's covers them, and four
            // times the same text would load the panel.
            string minStars = Fmt.Int(Thresholds.StarMinCount), regionStars = Fmt.Int(Thresholds.RegionMinStars);
            string fwhmDesc = Tr.Pick("The width of a star at half its height, in pixels: the smallest detail the image holds, "
                + "however many pixels the file has. It is measured on the row and the column through the peak and the mean is taken; the median over all "
                + "the stars is the figure that counts. Q1 / Q3 show how spread out they are: a wide interval means unequal stars across the "
                + "frame - soft corners, tracking trails, or stars on nebulosity, which come out wider.",
                "Latimea unei stele la jumatatea inaltimii ei, in pixeli: cel mai mic detaliu pe care il tine imaginea, "
                + "indiferent cati pixeli are fisierul. Se masoara pe linia si pe coloana prin varf si se ia media; mediana pe toate "
                + "stelele e cifra care conteaza. Q1 / Q3 arata cat de imprastiate sunt: un interval larg inseamna stele inegale in "
                + "cadru - colturi moi, urme de tracking, sau stele pe nebulozitate, care ies mai late.");
            var rows = new List<MetricRow>
            {
                Row(Tr.Pick("Stars measured", "Stele masurate"), Fmt.Int(s.Count),
                    s.Insufficient ? Tr.Pick($"too few for a FWHM (minimum {minStars})", $"prea putine pentru FWHM (minim {minStars})") : null,
                    Tr.Pick("How many stars passed the filters: an unsaturated local peak, with a minimum amplitude against the background around it "
                        + $"and at least {Fmt.F0(Thresholds.StarMinSnr)} times that background's noise (the grain of a stretched sky is not a star), "
                        + $"with a width between {Fmt.F1(Thresholds.StarFwhmMin)} and {Fmt.F0(Thresholds.StarFwhmMax)} px on both axes. "
                        + "They are searched for on the reference channel (green; the only channel in mono). "
                        + $"Under {minStars} stars, the global FWHM is not reliable and is not reported.",
                        "Cate stele au trecut de filtre: un varf local nesaturat, cu amplitudine minima fata de fundalul din jurul lui "
                        + $"si de cel putin {Fmt.F0(Thresholds.StarMinSnr)} ori zgomotul acelui fundal (granulatia unui cer intins nu e stea), "
                        + $"cu latimea intre {Fmt.F1(Thresholds.StarFwhmMin)} si {Fmt.F0(Thresholds.StarFwhmMax)} px pe ambele axe. "
                        + "Se cauta pe canalul de referinta (verdele; singurul canal la mono). "
                        + $"Sub {minStars} de stele, FWHM-ul global nu e de incredere si nu se raporteaza.")),
                Row(Tr.Pick("Median FWHM (Q1 / Q3)", "FWHM median (Q1 / Q3)"), s.Insufficient ? "n/a" : $"{Fmt.F2(s.FwhmMedian)} px ({Fmt.F2(s.FwhmQ1)} / {Fmt.F2(s.FwhmQ3)})",
                    description: fwhmDesc),
                Row(Tr.Pick("Real detail across the width", "Detaliu real pe latime"), ps.FwhmKnown ? $"~{Fmt.F0(ps.EffectiveDetailPx)} px" : "n/a",
                    description: Tr.Pick("The width of the image (after the crop, if there is one) divided by the FWHM: how many stars fit side by side across "
                        + "the width, that is the file's true resolution. The rest of the pixels are interpolation. The print size is "
                        + "judged on this figure, not on the number of pixels; the table below starts from here.",
                        "Latimea imaginii (dupa decupaj, daca e cazul) impartita la FWHM: cate stele incap una langa alta pe "
                        + "latime, adica rezolutia adevarata a fisierului. Restul pixelilor sunt interpolare. Marimea de print se "
                        + "judeca pe cifra asta, nu pe numarul de pixeli; tabelul de mai jos porneste de aici.")),
            };
            foreach (StarRegion reg in s.Regions)
            {
                bool centre = reg == s.Regions[0];
                rows.Add(Row(Tr.Pick("Median FWHM in the " + reg.Name, "FWHM median in " + (centre ? reg.Name : reg.Name.Replace("colt ", "coltul "))),
                    reg.Known ? Fmt.F2(reg.FwhmMedian) + " px" : "n/a", Tr.Pick($"{reg.Count} star{(reg.Count == 1 ? "" : "s")}", $"{reg.Count} stele"),
                    centre
                        ? Tr.Pick($"The median FWHM on five zones: the centre ({Fmt.F0(Thresholds.CenterFraction * 100)} % x {Fmt.F0(Thresholds.CenterFraction * 100)} % of the frame) "
                            + $"and the four corners ({Fmt.F0(Thresholds.RegionFraction * 100)} % of each side each, starting from the corner). "
                            + $"A zone is reported only with at least {regionStars} stars in it, otherwise n/a. "
                            + "Compared with each other, they show whether the optics are equally good over the whole field.",
                            $"FWHM median pe cinci zone: centrul ({Fmt.F0(Thresholds.CenterFraction * 100)} % x {Fmt.F0(Thresholds.CenterFraction * 100)} % din cadru) "
                            + $"si cele patru colturi (cate {Fmt.F0(Thresholds.RegionFraction * 100)} % din fiecare latura, pornind din colt). "
                            + $"O zona se raporteaza doar cu cel putin {regionStars} stele in ea, altfel n/a. "
                            + "Comparate intre ele, arata daca optica e la fel de buna pe tot campul.")
                        : null));
            }
            if (!double.IsNaN(s.CornerToCenterRatio))
                rows.Add(Row(Tr.Pick("Corner / centre", "Colt / centru"), Fmt.F2(s.CornerToCenterRatio), s.SoftCorners ? Tr.Pick("soft corners", "colturi moi") : null,
                    Tr.Pick("The widest known corner divided by the centre. 1.00 means the same stars everywhere; "
                        + $"over {Fmt.F1(Thresholds.CornerSoftRatio)} the corners are soft (coma, field curvature, tilt) and the size table "
                        + "no longer holds for the whole frame: at the size at which the centre is at the limit, the corners are already blobs. "
                        + "In that case the whole frame is not enlarged; a crop is considered. "
                        + $"n/a when the centre or all the corners have under {regionStars} stars.",
                        "Cel mai lat colt cunoscut impartit la centru. 1.00 inseamna stele la fel peste tot; "
                        + $"peste {Fmt.F1(Thresholds.CornerSoftRatio)} colturile sunt moi (coma, curbura de camp, tilt) si tabelul de marimi "
                        + "nu mai e valabil pentru tot cadrul: la marimea la care centrul e la limita, colturile sunt deja pete. "
                        + "In cazul asta nu se mareste tot cadrul, se ia in calcul un decupaj. "
                        + $"n/a cand centrul sau toate colturile au sub {regionStars} stele.")));
            Rows = rows;

            // The report's lines without their indent; each English column title ends where the Romanian one does.
            SizeRows = new[]
                {
                    Tr.Pick($"sizes for {Fmt.Dim(ps.PixelW, ps.PixelH)} px{(r.Crop.Needed ? " (after the crop)" : "")}:",
                            $"marimi pentru {Fmt.Dim(ps.PixelW, ps.PixelH)} px{(r.Crop.Needed ? " (dupa decupaj)" : "")}:"),
                    Tr.Pick(" width cm     height   dpi   star mm    point from       dist. used   outer frame cm",
                            "latime cm   inaltime   dpi   stea mm   punct de la   dist. folosita   rama ext. cm"),
                }
                .Concat(ps.Rows.Select(ReportWriter.SizeTableRow))
                .Concat(ReportWriter.SizeTableFooter(ps))
                .ToList();
            DpiRows = new[] { Tr.Pick("dpi pitch mm    grid vanishes from   that is", "dpi   pas mm   grila dispare de la   adica") }
                .Concat(PrintSizeAnalyzer.DpiLadder(r.Parameters.ViewingDistanceM).Select(l =>
                    $"{Fmt.F0(l.Dpi),3}   {Fmt.F2(l.PitchMm),6}   {Fmt.F2(l.DistanceM) + " m",19}   {l.Meaning}"))
                .ToList();
            HasResult = true;
        }
    }

    // ------------------------------------------------------------ Fundal

    /// <summary>
    /// The Culoare tab (R-116): the background's colour and the cast rows with their
    /// descriptions, the region table as the report writes it, and its guide.
    /// </summary>
    public sealed class BackgroundTabViewModel : ResultTabViewModel
    {
        IReadOnlyList<MetricRow> _rows = Array.Empty<MetricRow>();
        IReadOnlyList<string> _regions = Array.Empty<string>();
        IReadOnlyList<GuideParagraph> _guide = Array.Empty<GuideParagraph>();

        public IReadOnlyList<MetricRow> Rows { get => _rows; private set => Set(ref _rows, value); }

        /// <summary>
        /// The region table, as in the report, with one difference: the "!" mark also says
        /// whether the departure is coloured or neutral - in the report the distinction is in
        /// the recommendations.
        /// </summary>
        public IReadOnlyList<string> RegionRows { get => _regions; private set => Set(ref _regions, value); }

        /// <summary>
        /// The paragraphs under the region table: what they are, what delta is, what "!" is,
        /// coloured or neutral.
        /// </summary>
        public IReadOnlyList<GuideParagraph> Guide { get => _guide; private set => Set(ref _guide, value); }

        /// <inheritdoc/>
        internal override void Populate(AnalysisResult r)
        {
            BackgroundResult b = r.Background;
            string cast = Fmt.F1(Thresholds.CastWarnLevels);
            var rows = new List<MetricRow>
            {
                Row(b.ColorApplicable
                        ? Tr.Pick("Background colour, R / G / B (0..255)", "Culoarea fundalului, R / G / B (0..255)")
                        : Tr.Pick("Background, one channel (0..255)", "Fundalul, un singur canal (0..255)"),
                    string.Join(" / ", b.Levels.Select(Fmt.F1)),
                    description: Tr.Pick($"The median on each channel of the darkest {Fraction(Thresholds.BackgroundDarkestFraction)} of the image, by lightness, "
                        + $"without the bottom band of {Fmt.F0(Thresholds.BackgroundExcludeBottomFraction * 100)} % of the height, where the horizon and its pollution lie. "
                        + "It is the \"clean\" sky: the cast below and the edges in the next table are judged against it. "
                        + "It is not the same number as the background level in the Tones tab, which is the median over the whole frame: that one says how high the sky is, "
                        + "this one what colour it has.",
                        $"Mediana pe fiecare canal a celui mai intunecat {Fraction(Thresholds.BackgroundDarkestFraction)} din imagine, dupa luminozitate, "
                        + $"fara banda de jos de {Fmt.F0(Thresholds.BackgroundExcludeBottomFraction * 100)} % din inaltime, unde stau orizontul si poluarea lui. "
                        + "E cerul \"curat\": fata de el se judeca tenta de mai jos si marginile din tabelul urmator. "
                        + "Nu e acelasi numar cu nivelul fundalului din tabul Tonuri, care e mediana pe tot cadrul: acela spune cat de sus e cerul, "
                        + "acesta ce culoare are.")),
            };
            if (b.ColorApplicable)
            {
                string levels = Tr.Pick(" levels", " niveluri");
                rows.Add(Row("G - (R+B)/2", Fmt.S1(b.GreenExcess) + levels,
                    b.GreenExcess > Thresholds.CastWarnLevels ? Tr.Pick("green cast", "tenta verde") : b.GreenExcess < -Thresholds.CastWarnLevels ? Tr.Pick("magenta cast", "tenta magenta") : null,
                    Tr.Pick("How far green is over the mean of red and blue, in levels: positive = a green cast, negative = magenta. "
                        + "A neutral sky has no excess of green; such a cast comes from the sensor or the calibration, not from the object. "
                        + "On screen, with the background dark, it does not show; on paper, with the background raised to clear paper black, it shows. "
                        + $"Over {cast} levels, either way: to neutralize before printing (SCNR / Background Neutralization in Siril).",
                        "Cat e verdele peste media rosului si albastrului, in niveluri: pozitiv = tenta verde, negativ = magenta. "
                        + "Un cer neutru nu are exces de verde; o astfel de tenta vine de la senzor sau de la calibrare, nu de la obiect. "
                        + "Pe ecran, cu fundalul intunecat, nu se vede; pe hartie, cu fundalul ridicat ca sa treaca de negrul hartiei, se vede. "
                        + $"Peste {cast} niveluri, in oricare sens: de neutralizat inainte de print (SCNR / Background Neutralization in Siril).")));
                rows.Add(Row("R - B", Fmt.S1(b.RedMinusBlue) + levels,
                    b.RedMinusBlue > Thresholds.CastWarnLevels ? Tr.Pick("warm cast", "tenta calda") : b.RedMinusBlue < -Thresholds.CastWarnLevels ? Tr.Pick("cool cast", "tenta rece") : null,
                    Tr.Pick("Red minus blue, in levels: positive = a warm cast (horizon glow, urban light pollution), negative = a cool, bluish cast. "
                        + $"The same threshold, {cast} levels. Together with the row above it describes the background colour completely: "
                        + "a neutral background has both close to zero.",
                        "Rosu minus albastru, in niveluri: pozitiv = tenta calda (lumina de orizont, poluare urbana), negativ = tenta rece, albastruie. "
                        + $"Acelasi prag, {cast} niveluri. Impreuna cu randul de mai sus descrie complet culoarea fundalului: "
                        + "un fundal neutru le are pe amandoua aproape de zero.")));
            }
            Rows = rows;
            RegionRows = b.Regions.Select(e =>
                $"{e.Name,-18} {string.Join(" / ", e.Levels.Select(Fmt.F1)),-22} delta {string.Join(" / ", e.Deltas.Select(Fmt.S1))}"
                + (!e.Flagged ? "" : !b.ColorApplicable ? "  !" : e.Coloured ? Tr.Pick("  ! coloured", "  ! colorat") : Tr.Pick("  ! neutral", "  ! neutru"))).ToList();
            Guide = GuideParagraphs();
            HasResult = true;
        }

        /// <summary>
        /// "quarter" ("sfert") for 0.25, otherwise the percentage - so that the text reads
        /// naturally while the threshold is today's.
        /// </summary>
        /// <param name="f">The fraction, 0..1.</param>
        /// <returns>"quarter" or "sfert", or "20 %" and the like.</returns>
        static string Fraction(double f) => Math.Abs(f - 0.25) < 1e-9 ? Tr.Pick("quarter", "sfert") : Fmt.F0(f * 100) + " %";

        /// <summary>
        /// The text under the table: explanation, not diagnosis - it does not depend on the
        /// image, only on the thresholds.
        /// </summary>
        /// <returns>The five paragraphs.</returns>
        static IReadOnlyList<GuideParagraph> GuideParagraphs()
        {
            string grad = Fmt.F0(Thresholds.CornerGradientLevels);
            return new[]
            {
                new GuideParagraph
                {
                    Lead = Tr.Pick("The regions.", "Regiunile."),
                    Text = Tr.Pick($"Four corners, each {Fmt.F0(Thresholds.EdgeRegionHeightFraction * 100)} % of the height x "
                        + $"{Fmt.F0(Thresholds.EdgeRegionWidthFraction * 100)} % of the width, and four sides: bands of the same thickness along the middle of "
                        + "each side, between the corners. For each, the medians per channel (R / G / B, 0..255), then the delta.",
                        $"Patru colturi, fiecare de {Fmt.F0(Thresholds.EdgeRegionHeightFraction * 100)} % din inaltime x "
                        + $"{Fmt.F0(Thresholds.EdgeRegionWidthFraction * 100)} % din latime, si patru laturi: benzi de aceeasi grosime pe mijlocul "
                        + "fiecarei laturi, intre colturi. Pentru fiecare, medianele pe canal (R / G / B, 0..255), apoi delta."),
                },
                new GuideParagraph
                {
                    Lead = "Delta.",
                    Text = Tr.Pick("The region minus the background colour above, on each channel, in levels. Zero means an edge identical with the clean sky. "
                        + $"It is almost always positive, because the background is already the darkest {Fraction(Thresholds.BackgroundDarkestFraction)} of the image.",
                        "Regiune minus culoarea fundalului de mai sus, pe fiecare canal, in niveluri. Zero inseamna margine identica cu cerul curat. "
                        + $"E aproape intotdeauna pozitiva, fiindca fundalul e deja cel mai intunecat {Fraction(Thresholds.BackgroundDarkestFraction)} al imaginii."),
                },
                new GuideParagraph
                {
                    Lead = Tr.Pick("The ! mark.", "Semnul !."),
                    Text = Tr.Pick($"It appears when the largest departure on a channel passes {grad} levels. It only says that the edge differs from the background, "
                        + "not yet whether it is a defect or the object.",
                        $"Apare cand cea mai mare abatere pe un canal trece de {grad} niveluri. Spune doar ca marginea difera de fundal, "
                        + "nu inca daca e defect sau obiect."),
                },
                new GuideParagraph
                {
                    Lead = Tr.Pick("Coloured or neutral.", "Colorat sau neutru."),
                    Text = Tr.Pick("If the three deltas move together, the edge is most likely the object itself (the Milky Way entering the corner) "
                        + "and stays at Ok, with the advice to look in Preview before you correct anything. If they move differently (the difference between "
                        + $"the channels over {grad} levels and over {Fmt.F0(Thresholds.CornerColourSpreadRatio * 100)} % of the mean departure), it is a gradient with a colour: "
                        + "horizon glow, light pollution - to correct on the linear data (GraXpert), not with a local curve on the stretched image. "
                        + "On the reference image: the horizon corner +9.4 / +1.7 / -9.9 is coloured; the corner the Milky Way enters, "
                        + "+11 / +10 / +16, is neutral.",
                        "Daca cele trei delta se misca impreuna, marginea e cel mai probabil obiectul insusi (Calea Lactee care intra in colt) "
                        + "si ramane la Ok, cu indemnul sa te uiti in Preview inainte sa corectezi ceva. Daca se misca diferit (diferenta dintre "
                        + $"canale peste {grad} niveluri si peste {Fmt.F0(Thresholds.CornerColourSpreadRatio * 100)} % din abaterea medie), e un gradient cu culoare: "
                        + "lumina de orizont, poluare - de corectat pe liniar (GraXpert), nu cu o curba locala pe imaginea stretch-uita. "
                        + "Pe imaginea de referinta: coltul de orizont +9.4 / +1.7 / -9.9 e colorat; coltul in care intra Calea Lactee, "
                        + "+11 / +10 / +16, e neutru."),
                },
                new GuideParagraph
                {
                    Lead = Tr.Pick("In mono", "Pe mono"),
                    Text = Tr.Pick("there is one channel and no \"coloured\": any ! means only \"different from the background\".",
                        "e un singur canal si nu exista \"colorat\": orice ! inseamna doar \"diferit de fundal\"."),
                },
            };
        }
    }

    // ------------------------------------------------------------ Preview

    /// <summary>
    /// The Preview tab (R-117): the guide card with the screen-versus-paper rule, and the tiles
    /// - centre at 100 % and 200 %, the four corners, the whole frame, the print simulation and
    /// the crop - each with what to check in it.
    /// </summary>
    public sealed class PreviewTabViewModel : ResultTabViewModel
    {
        IReadOnlyList<PreviewTile> _tiles = Array.Empty<PreviewTile>();
        IReadOnlyList<GuideParagraph> _guide = Array.Empty<GuideParagraph>();
        public IReadOnlyList<PreviewTile> Tiles { get => _tiles; private set => Set(ref _tiles, value); }

        /// <summary>
        /// The card above the tiles: the screen - paper rule, with the step back computed on the
        /// image.
        /// </summary>
        public IReadOnlyList<GuideParagraph> Guide { get => _guide; private set => Set(ref _guide, value); }

        /// <inheritdoc/>
        internal override void Populate(AnalysisResult r)
        {
            Raster img = r.Raster;
            var tiles = new List<PreviewTile>();
            int cw = Math.Min(480, img.W), ch = Math.Min(320, img.H);
            int cx = (img.W - cw) / 2, cy = (img.H - ch) / 2;

            // What to check in each tile: guidance, not diagnosis. The thresholds from Thresholds.
            string corner = Tr.Pick("Against the centre: stars stretched towards the corner = coma; in one corner only = tilt; in all four = field curvature. "
                + "A brighter or warmer corner = the gradient of the Colour tab; bands at the edge = the frames of the stack. A bad corner can go with a crop.",
                "Fata de centru: stele alungite spre colt = coma; intr-un singur colt = tilt; in toate patru = curbura de camp. "
                + "Colt mai luminos sau mai cald = gradientul din tabul Culoare; benzi la margine = cadrele stivei. Un colt rau poate iesi prin decupaj.");

            tiles.Add(new PreviewTile
            {
                Title = Tr.Pick("Centre, 100 %", "Centru, 100 %"),
                Image = RasterBitmaps.Region(img, cx, cy, cw, ch),
                Check = Tr.Pick("The texture, not the detail. The grain of the sky - the denoise recommendation for the screen is judged here, with the step back "
                    + "from the card above - the shape of the stars, round and soft, without a sharpening halo, and steps in the sky.",
                    "Textura, nu detaliul. Granulatia cerului - recomandarea de denoise pentru ecran se judeca aici, cu pasul inapoi "
                    + "din cardul de sus - forma stelelor, rotunde si moi, fara halo de sharpening, si trepte in cer."),
            });
            tiles.Add(new PreviewTile
            {
                Title = Tr.Pick("Centre, 200 %", "Centru, 200 %"),
                Image = RasterBitmaps.Region(img, cx + cw / 4, cy + ch / 4, cw / 2, ch / 2, zoom: 2),
                Note = Tr.Pick("the file's pixels, enlarged twice", "pixelii din fisier, mariti de doua ori"),
                Check = Tr.Pick("The magnifier, not the print: file artefacts. The sky must be a fine grain, not patches of the same value (the plateau of "
                    + "Export); the stars soft discs, not squares or crosses. 8 x 8 blocks mean the file went through JPEG; steps "
                    + "visible on \"16 bits\" mean 8 bits in disguise.",
                    "Lupa, nu printul: artefacte de fisier. Cerul trebuie sa fie granulatie fina, nu petice de aceeasi valoare (palierul din "
                    + "Export); stelele discuri moi, nu patrate sau cruci. Blocuri de 8 x 8 inseamna ca fisierul a trecut prin JPEG; trepte "
                    + "vizibile pe \"16 biti\" inseamna 8 biti deghizati."),
            });
            int kw = Math.Min(320, img.W), kh = Math.Min(220, img.H);
            tiles.Add(new PreviewTile { Title = Tr.Pick("Upper-left corner, 100 %", "Colt stanga-sus, 100 %"), Image = RasterBitmaps.Region(img, 0, 0, kw, kh), Check = corner });
            tiles.Add(new PreviewTile { Title = Tr.Pick("Upper-right corner, 100 %", "Colt dreapta-sus, 100 %"), Image = RasterBitmaps.Region(img, img.W - kw, 0, kw, kh), Check = corner });
            tiles.Add(new PreviewTile { Title = Tr.Pick("Lower-left corner, 100 %", "Colt stanga-jos, 100 %"), Image = RasterBitmaps.Region(img, 0, img.H - kh, kw, kh), Check = corner });
            tiles.Add(new PreviewTile { Title = Tr.Pick("Lower-right corner, 100 %", "Colt dreapta-jos, 100 %"), Image = RasterBitmaps.Region(img, img.W - kw, img.H - kh, kw, kh), Check = corner });

            const int side = 520;
            tiles.Add(new PreviewTile
            {
                Title = Tr.Pick("Whole frame, as is", "Cadru intreg, asa cum e"),
                Image = RasterBitmaps.Whole(img, side),
                Check = Tr.Pick("The composition and the global gradient, seen from a distance or with half-closed eyes: a brighter part, the horizon band, "
                    + "a cast. Here you answer the question of the neutral edges: is the marked corner the Milky Way entering the frame, or a light?",
                    "Compozitia si gradientul global, privit de la distanta sau mijind ochii: o parte mai luminoasa, banda orizontului, "
                    + "o dominanta. Aici raspunzi intrebarii de la marginile neutre: coltul marcat e Calea Lactee care intra in cadru, sau o lumina?"),
            });
            SimulationRow primary = r.Simulation.Primary;
            if (!primary.AsIs)
            {
                Raster small = RasterOps.Subsample(img, Math.Max(1, (int)Math.Ceiling(Math.Max(img.W, img.H) / (double)side)));
                tiles.Add(new PreviewTile
                {
                    Title = Tr.Pick($"Print simulation, background {Fmt.F2(primary.Target)}", $"Simulare print, fundal {Fmt.F2(primary.Target)}"),
                    Image = RasterBitmaps.Whole(RasterOps.Gamma(small, primary.Gamma), side),
                    Note = Tr.Pick("a gamma curve on every channel; on screen it looks flat - the paper brings it back", "curba gamma pe toate canalele; pe ecran pare plata - hartia o aduce inapoi"),
                    Check = Tr.Pick("Beside the whole frame: what appears when the sky rises. Not the flatness - the paper's Dmax brings it back - but the cast "
                        + $"(a cast of over {Fmt.F1(Thresholds.CastWarnLevels)} levels becomes visible here), the gradient, and whether the faint "
                        + "nebulosity stays separate from the sky. Read it as if the darkest tone were paper black.",
                        "Alaturi de cadrul intreg: ce apare cand cerul urca. Nu platitudinea - Dmax-ul hartiei o aduce inapoi - ci dominanta "
                        + $"(o tenta de peste {Fmt.F1(Thresholds.CastWarnLevels)} niveluri devine vizibila aici), gradientul, si daca nebulozitatea "
                        + "slaba ramane separata de cer. Citeste-o ca si cum cel mai inchis ton ar fi negrul hartiei."),
                });
            }
            if (r.Crop.Needed)
                tiles.Add(new PreviewTile
                {
                    Title = Tr.Pick("Crop to the target ratio", "Decupaj la raportul tinta"),
                    Image = RasterBitmaps.Whole(RasterOps.Crop(img, r.Crop.X0, r.Crop.Y0, r.Crop.W, r.Crop.H), side),
                    Note = Tr.Pick($"{Fmt.Dim(r.Crop.W, r.Crop.H)} px, centred; cut: {ReportWriter.CropCutText(r.Crop)}", $"{Fmt.Dim(r.Crop.W, r.Crop.H)} px, centrat; taiati {ReportWriter.CropCutText(r.Crop)}"),
                    Check = Tr.Pick("What the requested ratio cuts: does it go into the object or the composition? Does it remove the soft corner or the bands of the stack? "
                        + "The only tile where the answer can be \"I change the ratio\".",
                        "Ce taie raportul cerut: intra in obiect sau in compozitie? Scoate coltul moale sau benzile stivei? "
                        + "Singura dala unde raspunsul poate fi \"schimb raportul\"."),
                });
            Tiles = tiles;
            Guide = GuideParagraphs(r.Size);
            HasResult = true;
        }

        /// <summary>
        /// The card at the top: the screen - paper rule. The figures come from PrintSizeResult and
        /// Thresholds.
        /// </summary>
        /// <param name="ps">
        /// The size analysis: the screen-equivalent width, the step-back factor and the reference
        /// row.
        /// </param>
        /// <returns>The four paragraphs, "HOW TO USE THE PREVIEW" ("CUM SE FOLOSESTE PREVIEW-UL").</returns>
        static IReadOnlyList<GuideParagraph> GuideParagraphs(PrintSizeResult ps)
        {
            string eq = Fmt.F0(ps.ScreenEquivalentWidthCm);
            double f = ps.ScreenStepBackFactor;
            string stepBack = double.IsNaN(f) ? ""
                : f >= 1
                    ? Tr.Pick($" For the reference print, {Fmt.F0(ps.ReferenceRow.WidthCm)} cm, look at the 100 % tiles from {Fmt.F1(f)} x your distance from the monitor: "
                        + "what no longer shows from there does not show on paper either.",
                        $" Pentru printul de referinta, {Fmt.F0(ps.ReferenceRow.WidthCm)} cm, priveste dalele de 100 % de la {Fmt.F1(f)} x distanta ta fata de monitor: "
                        + "ce nu se mai vede de acolo nu se vede nici pe hartie.")
                    : Tr.Pick($" The reference print, {Fmt.F0(ps.ReferenceRow.WidthCm)} cm, is larger than the 100 % view: its pixel on paper is "
                        + $"{Fmt.F1(1 / f)} times the one on screen, so what you see at 100 % shows on paper too, enlarged.",
                        $" Printul de referinta, {Fmt.F0(ps.ReferenceRow.WidthCm)} cm, e mai mare decat vederea la 100 %: pixelul lui pe hartie e de "
                        + $"{Fmt.F1(1 / f)} ori cat cel de pe ecran, deci ce vezi la 100 % se vede si pe hartie, marit.");
            return new[]
            {
                new GuideParagraph
                {
                    Lead = Tr.Pick("The screen is harsher than the paper.", "Ecranul e mai aspru decat hartia."),
                    Text = Tr.Pick($"An ordinary monitor has a pixel of ~{Fmt.F2(Thresholds.ScreenPixelMm)} mm; at 100 % you see the file as a print "
                        + $"~{eq} cm wide, seen from your distance from the monitor.",
                        $"Un monitor obisnuit are pixelul de ~{Fmt.F2(Thresholds.ScreenPixelMm)} mm; la 100 % vezi fisierul ca pe un print lat de "
                        + $"~{eq} cm, privit de la distanta ta fata de monitor."),
                },
                new GuideParagraph
                {
                    Lead = Tr.Pick("The step back.", "Pasul inapoi."),
                    Text = (stepBack.Length > 0 ? stepBack.Substring(1) + " " : "")
                        + Tr.Pick("The 100 % tiles are made for this; the 200 % one is not: it is a magnifier, no paper looks like that.",
                            "Dalele de 100 % sunt facute pentru asta; cea de 200 % nu: e o lupa, nicio hartie nu arata asa."),
                },
                new GuideParagraph
                {
                    Lead = Tr.Pick("What to look for.", "Ce cauti."),
                    Text = Tr.Pick("The texture and the shape of the stars in the centre; the shape of the stars and the gradient in the corners; the composition and the global gradient on the whole "
                        + "frame; the cast and the faint nebulosity in the simulation; what the ratio cuts in the crop. Each tile says under it what you check.",
                        "Textura si forma stelelor in centru; forma stelelor si gradientul in colturi; compozitia si gradientul global pe cadrul "
                        + "intreg; dominanta si nebulozitatea slaba in simulare; ce taie raportul in decupaj. Fiecare dala spune sub ea ce verifici."),
                },
                new GuideParagraph
                {
                    Lead = Tr.Pick("What it is not.", "Ce nu e."),
                    Text = Tr.Pick("It is not a viewer: pixel crops and reductions, from memory; nothing is written to disk. The colour is not managed: "
                        + "the pixels are shown as they are in the file, assumed sRGB.",
                        "Nu e un vizualizator: decupaje la pixel si reduceri, din memorie; nu se scrie nimic pe disc. Nu e gestionata culoarea: "
                        + "pixelii sunt aratati asa cum sunt in fisier, presupusi sRGB."),
                },
            };
        }

        /// <inheritdoc/>
        internal override void Clear()
        {
            base.Clear();
            Tiles = Array.Empty<PreviewTile>();
            Guide = Array.Empty<GuideParagraph>();
        }
    }
}
