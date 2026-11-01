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

// ResultTabs.cs - the five tabs with results (Raport, Tonuri, Export, Rezolutie, Culoare; the
// Editare Curbe and Soft proof tabs have files of their own). Each receives the AnalysisResult
// through Populate and turns it into rows; none phrases a sentence of diagnosis - those come
// from Recommendations and from ReportWriter, the figures from Fmt. The descriptions and the
// guides are the view-models' own explanation, every number in them interpolated from
// Thresholds, Parameters or the result (ADR 0020).
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
        /// Takes an analysis and sets <see cref="HasResult"/>: the five tabs of this file fill
        /// their rows from it; the Editare Curbe tab only keeps it, for the second run MainViewModel
        /// starts (ADR 0051).
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
    /// button per row, the level-L* ladder, the guide, and the requests for the map and preview
    /// windows. The catalogued clouds have no table here: their rows are the map window's list.
    /// </summary>
    public sealed class TonesTabViewModel : ResultTabViewModel
    {
        /// <summary>
        /// The red sentence under the table when the file has no WCS (R-113); the same in the map
        /// window.
        /// </summary>
        public static string NoWcsSentence => Tr.Pick(
            "The file has no sky coordinates (WCS), so the dust clouds could not be looked up in the catalogue; the dust was found by darkness alone.",
            "Fisierul nu are coordonate pe cer (WCS), asa ca norii de praf nu au putut fi cautati dupa catalog; praful s-a cautat doar dupa cat e de intunecat.");

        IReadOnlyList<MetricRow> _rows = Array.Empty<MetricRow>();
        IReadOnlyList<double> _hist = Array.Empty<double>();
        IReadOnlyList<SimulationRowItem> _simulation = Array.Empty<SimulationRowItem>();
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
            + $"L* {Fmt.F0(Thresholds.LowSeparationLstar)}, under which the eye barely tells the tones apart",
            "HISTOGRAMA L* (0..100); linia rosie = negrul hartiei, linia galbena intrerupta = "
            + $"L* {Fmt.F0(Thresholds.LowSeparationLstar)}, sub care ochiul abia mai deosebeste tonurile");

        /// <summary>
        /// The note under the chart. Each sentence says what lies left of one line and never
        /// which line is left of the other: on canvas (Dmax 1.5) paper black is L* 20.7, right of
        /// the threshold, and the text must hold there too. The second sentence speaks only of
        /// the tones the paper still separates, since part of what is left of the threshold is
        /// also under paper black, on every paper.
        /// </summary>
        public string HistogramNote => Tr.Pick(
            "Left of the red line everything comes out on paper as the same black. "
            + $"Left of the dashed yellow line (under L* {Fmt.F0(Thresholds.LowSeparationLstar)}) the paper still tells the tones apart, "
            + "but the eye, in ordinary room light, almost does not. The histogram shows percentages of the pixels, counted on a reduced copy of the image.",
            "La stanga liniei rosii totul iese pe hartie cu acelasi negru. "
            + $"La stanga liniei galbene intrerupte (sub L* {Fmt.F0(Thresholds.LowSeparationLstar)}) hartia inca deosebeste tonurile, "
            + "dar ochiul, in lumina obisnuita de camera, aproape ca nu. Histograma arata procente din pixeli, numarati pe o copie redusa a imaginii.");

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
        public string PreviewTooltip => Tr.Pick($"Shows the image with the background moved to the row's value, in a separate window; its two check boxes colour what would come out black on paper and what is under L* {Fmt.F0(Thresholds.LowSeparationLstar)}", $"Arata imaginea cu fundalul mutat la valoarea din rand, intr-o fereastra separata; cele doua casute de acolo coloreaza ce ar iesi negru pe hartie si ce e sub L* {Fmt.F0(Thresholds.LowSeparationLstar)}");

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
            // A mono image has one channel, not a green one: the two labels that name the
            // reference channel say so.
            bool colour = t.MeanLevels.Count > 1;
            Rows = new[]
            {
                Row(Tr.Pick("Channel means (0..255)", "Medii pe canal (0..255)"), string.Join(" / ", t.MeanLevels.Select(Fmt.F1)),
                    description: Tr.Pick("How bright each channel (red, green, blue) is on average, on the 0..255 scale of Siril's histogram. "
                        + "The sky fills almost the whole frame, so the means really say how high the sky sits. When the three differ, "
                        + "the sky has a tint; that is judged in the Colour tab.",
                        "Cat de luminos e in medie fiecare canal (rosu, verde, albastru), pe scara 0..255 a histogramei din Siril. "
                        + "Cerul ocupa aproape tot cadrul, asa ca mediile spun de fapt cat de sus sta cerul. Daca cele trei difera, "
                        + "cerul are o tenta; asta se judeca in tabul Culoare.")),
                Row(Tr.Pick($"Under level {Thresholds.DarkLevelA} / {Thresholds.DarkLevelB}", $"Sub nivelul {Thresholds.DarkLevelA} / {Thresholds.DarkLevelB}"), $"{Fmt.Pct(t.PctBelowDarkA)} / {Fmt.Pct(t.PctBelowDarkB)}",
                    description: Tr.Pick($"How much of the image, in percent, is darker than level {Thresholds.DarkLevelA} and than {Thresholds.DarkLevelB}, "
                        + "on the 0..255 scale of Siril's histogram. A landmark only, to compare with what you see in Siril; "
                        + "the print verdict is given on the L* scale, below.",
                        $"Cat la suta din imagine e mai intunecat decat nivelul {Thresholds.DarkLevelA} si decat {Thresholds.DarkLevelB}, "
                        + "pe scara 0..255 a histogramei din Siril. E doar un reper, ca sa compari cu ce vezi in Siril; "
                        + "verdictul pentru print se da pe scara L*, mai jos.")),
                Row(colour ? Tr.Pick("Background level (green channel)", "Nivelul fundalului (canalul verde)") : Tr.Pick("Background level (the only channel)", "Nivelul fundalului (singurul canal)"), Fmt.F3(t.BackgroundMedian),
                    description: Tr.Pick("How high the sky sits, on the 0..1 scale: the middle value of the green channel (in mono, of the only channel). "
                        + "As the sky fills most of the image, the middle pixel is a sky pixel. The simulation below starts from here. "
                        + "The colour of the sky is another measurement, in the Colour tab.",
                        "Cat de sus sta cerul, pe scara 0..1: valoarea din mijloc a canalului verde (la alb-negru, a singurului canal). "
                        + "Cum cerul ocupa cea mai mare parte a imaginii, pixelul din mijloc e un pixel de cer. De aici porneste simularea de mai jos. "
                        + "Culoarea cerului e alta masuratoare, in tabul Culoare.")),
                Row(Tr.Pick("Median L* (the sky)", "L* median (cerul)"), Fmt.F1(t.LstarMedian),
                    description: Tr.Pick("How bright the eye sees the middle pixel, from 0 (black) to 100 (white). L* is the scale the print is judged on: "
                        + "equal steps of L* look like equal steps of grey. On an astrophoto the middle pixel is the sky, so this is the sky's lightness.",
                        "Cat de luminos vede ochiul pixelul din mijloc, de la 0 (negru) la 100 (alb). L* e scara in care se judeca printul: "
                        + "pasi egali de L* se vad ca pasi egali de gri. Pe o astrofoto, pixelul din mijloc e cerul, deci asta e luminozitatea cerului.")),
                Row(Tr.Pick($"Under paper black (L* {Fmt.F1(t.PaperBlackLstar)}, Dmax {Fmt.Dmax(t.Dmax)})", $"Sub negrul hartiei (L* {Fmt.F1(t.PaperBlackLstar)}, Dmax {Fmt.Dmax(t.Dmax)})"), Fmt.Pct(t.PctBelowPaperBlack),
                    description: Tr.Pick("How many of the pixels, in percent, are darker than the deepest black the chosen paper can give. "
                        + "All of them come out on paper as the same black, so the shades between them are lost. The share is only a measure "
                        + "of the image, not the verdict: the verdict is given on the dust and the sky, in the simulation below.",
                        "Cat la suta din pixeli sunt mai intunecati decat cel mai adanc negru pe care il poate da hartia aleasa. "
                        + "Toti acestia ies pe hartie cu acelasi negru, deci nuantele dintre ei se pierd. Procentul e doar o masura "
                        + "a imaginii, nu verdictul: verdictul se da pe praf si pe cer, in simularea de mai jos.")),
                Row(Tr.Pick($"Under L* {Fmt.F0(Thresholds.LowSeparationLstar)} (the eye barely tells apart)", $"Sub L* {Fmt.F0(Thresholds.LowSeparationLstar)} (ochiul abia deosebeste)"), Fmt.Pct(t.PctBelowLowSeparation),
                    description: Tr.Pick($"How many of the pixels, in percent, are under L* {Fmt.F0(Thresholds.LowSeparationLstar)}. The paper still tells these tones apart, "
                        + "but the eye, in ordinary room light, almost does not. The share includes the pixels under paper black. "
                        + "A large share means a sky that will look like a uniform patch.",
                        $"Cat la suta din pixeli sunt sub L* {Fmt.F0(Thresholds.LowSeparationLstar)}. Hartia inca deosebeste tonurile astea, "
                        + "dar ochiul, in lumina obisnuita de camera, aproape ca nu. Procentul ii cuprinde si pe cei de sub negrul hartiei. "
                        + "Un procent mare inseamna un cer care va parea o pata uniforma.")),
                Row(Tr.Pick($"Over L* {Fmt.F0(Thresholds.HighlightLstar)}", $"Peste L* {Fmt.F0(Thresholds.HighlightLstar)}"), Fmt.Pct(t.PctAboveHighlight),
                    description: Tr.Pick("How many of the pixels, in percent, are lighter than middle grey: stars, a galaxy's core, bright nebulosity. "
                        + "It says how much of the frame the bright parts take.",
                        "Cat la suta din pixeli sunt mai luminosi decat griul de mijloc: stele, nucleul galaxiei, nebuloasa stralucitoare. "
                        + "Spune cat din cadru e ocupat de partile luminoase.")),
                // One percentile per line: on a single line the value would be the widest in the
                // panel and would narrow the description column for every row.
                Row(colour ? Tr.Pick("Landmarks on the green channel (0..255)", "Repere pe canalul verde (0..255)") : Tr.Pick("Landmarks on the only channel (0..255)", "Repere pe singurul canal (0..255)"), string.Join("\n", TonalResult.PercentileRanks.Zip(t.ReferencePercentiles, (p, v) => $"{"p" + Fmt.F1(p),-6} = {Fmt.F1(v),5}")),
                    description: Tr.Pick("The level under which a given share of the green channel's pixels lies: p50 = half the pixels are under it, "
                        + "so it is the sky; p0.1 = the deepest black that counts; p99.9 and p100 = the brightest stars. p100 = 255 means burnt-out pixels.",
                        "Nivelul sub care se afla un anumit procent din pixelii canalului verde: p50 = jumatate din pixeli sunt sub el, "
                        + "deci e cerul; p0.1 = cel mai adanc negru care conteaza; p99.9 si p100 = stelele cele mai luminoase. p100 = 255 inseamna pixeli arsi.")),
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
                    Text = Tr.Pick("Does the background survive on paper? On screen the black goes down almost to zero; on paper the darkest tone is that "
                        + "of the ink laid at its maximum, and everything darker than it comes out the same. The sky of an astrophotograph sits right in this zone, "
                        + "so a background that looks good on the monitor can come out on paper as a black patch without shades.",
                        "Supravietuieste fundalul pe hartie? Pe ecran negrul coboara aproape la zero; pe hartie cel mai intunecat ton e cel "
                        + "al cernelii puse la maximum, si tot ce e mai intunecat decat el iese la fel. Cerul unei astrofotografii sta tocmai in zona asta, "
                        + "asa ca un fundal care arata bine pe monitor poate iesi pe hartie ca o pata neagra fara nuante."),
                },
                new GuideParagraph
                {
                    Lead = Tr.Pick("Two scales, in turn.", "Doua scari, pe rand."),
                    Text = Tr.Pick("The levels 0..255 are the scale of the histogram in Siril and in any editor; L* (0..100) is the paper's scale: "
                        + "the lightness as the eye sees it. Going from one to the other is no simple proportion; the program does it by the "
                        + "standard colour formula (sRGB -> L*). "
                        + $"Landmarks, on a grey: level {Fmt.F0(ColorMath.GrayLevelOfLstar(barytaBlack))} is L* {Fmt.F0(barytaBlack)} (the baryta black), "
                        + $"{Thresholds.DarkLevelA} is L* {Fmt.F0(ColorMath.LstarOfGrayLevel(Thresholds.DarkLevelA))}, "
                        + $"{Thresholds.DarkLevelB} is L* {Fmt.F0(ColorMath.LstarOfGrayLevel(Thresholds.DarkLevelB))}. "
                        + "On colour, the same level gives a different L* by its hue, which is why both are shown.",
                        "Nivelurile 0..255 sunt scara histogramei din Siril si din orice editor; L* (0..100) e scara hartiei: "
                        + "luminozitatea asa cum o vede ochiul. Trecerea de la una la alta nu e o simpla proportie; programul o face dupa "
                        + "formula standard a culorii (sRGB -> L*). "
                        + $"Repere, pe un gri: nivelul {Fmt.F0(ColorMath.GrayLevelOfLstar(barytaBlack))} e L* {Fmt.F0(barytaBlack)} (negrul baryta), "
                        + $"{Thresholds.DarkLevelA} e L* {Fmt.F0(ColorMath.LstarOfGrayLevel(Thresholds.DarkLevelA))}, "
                        + $"{Thresholds.DarkLevelB} e L* {Fmt.F0(ColorMath.LstarOfGrayLevel(Thresholds.DarkLevelB))}. "
                        + "Pe culoare, acelasi nivel da alt L* dupa nuanta, de aceea se arata amandoua."),
                },
                new GuideParagraph
                {
                    Lead = Tr.Pick("Paper black", "Negrul hartiei"),
                    Text = Tr.Pick("is the deepest black the paper can give. Its Dmax says it, that is how much light the ink laid at its maximum swallows; "
                        + $"the higher the Dmax, the deeper the black: baryta {Black(PrintMedium.Baryta)}, "
                        + $"canvas {Black(PrintMedium.Canvas)}, a C-print under acrylic {Black(PrintMedium.Acrylic)}. The medium is chosen in the parameters; "
                        + "the same image can be print-safe on baryta and too dark on canvas.",
                        "e cel mai adanc negru pe care il poate da hartia. Il spune Dmax-ul ei, adica cata lumina inghite cerneala pusa la maximum; "
                        + $"cu cat Dmax e mai mare, cu atat negrul e mai adanc: baryta {Black(PrintMedium.Baryta)}, "
                        + $"canvas {Black(PrintMedium.Canvas)}, C-print sub acril {Black(PrintMedium.Acrylic)}. Mediul se alege in parametri; "
                        + "aceeasi imagine poate fi print-safe pe baryta si prea intunecata pe canvas."),
                },
                new GuideParagraph
                {
                    Lead = Tr.Pick("The dust and the sky.", "Praful si cerul."),
                    Text = Tr.Pick($"The program does not judge pixel by pixel but on patches (squares) of {Thresholds.DustPatchPx} x {Thresholds.DustPatchPx} px, about what the eye "
                        + "takes in on a spot of paper; each patch has its mean lightness, in L*. The patches are sorted into five kinds, by comparison "
                        + "with each other, not by a fixed threshold. When the file has sky coordinates (a WCS in the header, or lent through the "
                        + $"\"WCS from FITS\" field) and the Milky Way is in the frame: the band is the zone less than {Fmt.F0(Thresholds.BandGalacticLatitudeDeg)} degrees "
                        + $"from the plane of the Milky Way; the sky is what lies outside the band; the dust is the patches {Fmt.F0(Thresholds.DustDepthLstar)} L* darker than the band "
                        + $"around them (over ~{Thresholds.BandLocalWindowPatches * Thresholds.DustPatchPx} px) or than the sky, that is darker than the band would be there; the bright clouds, "
                        + $"those {Fmt.F0(Thresholds.BrightCloudLstar)} L* over the band around them; the peaks, the brightest {Fmt.F1(Thresholds.PeakFraction * 100)} % of the patches. "
                        + $"This path needs both band and sky outside it (at least {Fmt.F0(Thresholds.BandMinFraction * 100)} % of the patches each) and a field of at least "
                        + $"{Fmt.F0(Thresholds.BandMinLatitudeSpanDeg)} degrees of galactic latitude, the band's width: on a narrower field the band is not a strip but a gentle "
                        + "slope, and the whole frame is deep sky inside the band. Otherwise, as without a WCS, the sky is taken from all the patches "
                        + $"(the lower part of each zone of the image: the level under which {Fmt.F0(Thresholds.SkyModelLowPercentile)} % of the zone's patches lie) and the thresholds come from the image: "
                        + $"the nebula is a patch well over the sky (over {Fmt.F0(Thresholds.SkyNoiseK)} sigma, that is {Fmt.F0(Thresholds.SkyNoiseK)} times the sky's noise), the dust "
                        + "one as much under the sky, the bright nebula the brightest part of the nebula, the peaks as above. "
                        + $"On either path, the sky's L* is the middle value of the sky patches, and the dust's L* is the darkest extended area: the threshold under which "
                        + $"{Fmt.F0(Thresholds.DustClassFraction * 100)} % of the dust patches lie, when the dust has at least {Fmt.F0(Thresholds.DustClassMinFraction * 100)} % of the patches "
                        + $"(under that they are cores or artefacts), or the darkest {Fmt.F0(Thresholds.SkyDarkestFraction * 100)} % of the sky patches, whichever of the two is darker. "
                        + $"The \"Dust / sky map\" button shows the kinds of patches on the image and, from the Lynds catalogue, the clouds by name: each one with at least {Thresholds.DustCloudMinPatches} patches, "
                        + $"listed with its L* and the background at which it reaches black, the darkest {Thresholds.DustCloudReportRows} written on the image.",
                        $"Programul nu judeca pixel cu pixel, ci pe dale (patrate) de {Thresholds.DustPatchPx} x {Thresholds.DustPatchPx} px, cam cat vede ochiul "
                        + "pe o pata de hartie; fiecare dala are luminozitatea ei medie, in L*. Dalele se impart in cinci feluri, prin comparatie "
                        + "intre ele, nu dupa un prag fix. Daca fisierul are coordonate pe cer (WCS in antet, sau imprumutat prin campul "
                        + $"\"WCS din FITS\") si Calea Lactee e in cadru: banda e zona la mai putin de {Fmt.F0(Thresholds.BandGalacticLatitudeDeg)} grade "
                        + $"de planul Caii Lactee; cerul e cel din afara benzii; praful sunt dalele cu {Fmt.F0(Thresholds.DustDepthLstar)} L* mai intunecate decat banda "
                        + $"din jurul lor (pe ~{Thresholds.BandLocalWindowPatches * Thresholds.DustPatchPx} px) sau decat cerul, adica mai intunecate decat ar fi banda acolo; norii luminosi, "
                        + $"cele cu {Fmt.F0(Thresholds.BrightCloudLstar)} L* peste banda din jur; varfurile, cele mai luminoase {Fmt.F1(Thresholds.PeakFraction * 100)} % din dale. "
                        + $"Calea asta cere si banda, si cer in afara ei (cel putin {Fmt.F0(Thresholds.BandMinFraction * 100)} % din dale fiecare) si un camp de cel putin "
                        + $"{Fmt.F0(Thresholds.BandMinLatitudeSpanDeg)} de grade de latitudine galactica, cat e banda de lata: pe un camp mai ingust banda nu e o fasie, ci o "
                        + "trecere lina, si tot cadrul e cer adanc in banda. Altfel, la fel ca fara WCS, cerul se ia din toate dalele "
                        + $"(partea de jos a fiecarei zone a imaginii: nivelul sub care stau {Fmt.F0(Thresholds.SkyModelLowPercentile)} % din dalele zonei) si pragurile vin din imagine: "
                        + $"nebuloasa e dala cu mult peste cer (peste {Fmt.F0(Thresholds.SkyNoiseK)} sigma, adica de {Fmt.F0(Thresholds.SkyNoiseK)} ori zgomotul cerului), praful "
                        + "cea la fel de mult sub cer, nebuloasa stralucitoare partea cea mai luminoasa a nebuloasei, varfurile ca mai sus. "
                        + $"Pe orice cale, L*-ul cerului e valoarea din mijloc a dalelor de cer, iar L*-ul prafului e cea mai intunecata zona intinsa: pragul sub care stau "
                        + $"{Fmt.F0(Thresholds.DustClassFraction * 100)} % din dalele de praf, daca praful are cel putin {Fmt.F0(Thresholds.DustClassMinFraction * 100)} % din dale "
                        + $"(sub atat sunt nuclee sau artefacte), sau cel mai intunecat {Fmt.F0(Thresholds.SkyDarkestFraction * 100)} % din dalele de cer, care din doua e mai intunecata. "
                        + $"Butonul \"Harta praf / cer\" arata felurile de dale pe imagine si, din catalogul Lynds, norii pe nume: fiecare cu cel putin {Thresholds.DustCloudMinPatches} dale, "
                        + $"in lista cu L*-ul lui si fundalul la care el ajunge pe negru, cei mai intunecati {Thresholds.DustCloudReportRows} scrisi pe imagine."),
                },
                new GuideParagraph
                {
                    Lead = Tr.Pick("The verdict.", "Verdictul."),
                    Text = Tr.Pick("When the dust falls under paper black and the sky stays above: caution, the clouds would print pure black. "
                        + "When the sky falls under black too: a problem, the sky and the dust come out as the same black. The pixels are not counted: "
                        + "what is looked at is the darkest extended area against paper black. Then the viewing matters: the paper tells tones apart "
                        + $"even lower, but a sky under L* {Fmt.F0(Thresholds.LowSeparationLstar)} looks almost black in room light, so it too is a caution, however deep the paper black. "
                        + $"Print-safe means both: the dust at or over paper black and the sky at L* {Fmt.F0(Thresholds.LowSeparationLstar)} or over; a row with the dust on black "
                        + $"and the sky under the threshold is marked \"sky under L* {Fmt.F0(Thresholds.LowSeparationLstar)}\". "
                        + "The recommendation is the MINIMUM print-safe level: higher on an image that is too dark, lower on one that is too bright.",
                        "Daca praful cade sub negrul hartiei si cerul ramane deasupra: atentie, norii s-ar tipari negru pur. "
                        + "Daca si cerul cade sub negru: problema, cerul si praful ies cu acelasi negru. Nu se numara pixelii: "
                        + "se uita la cea mai intunecata zona intinsa fata de negrul hartiei. Apoi conteaza privitul: hartia deosebeste tonuri "
                        + $"si mai jos, dar un cer sub L* {Fmt.F0(Thresholds.LowSeparationLstar)} se vede aproape negru in lumina de camera, deci si el e atentie, oricat de adanc ar fi negrul hartiei. "
                        + $"Print-safe inseamna amandoua: praful la sau peste negrul hartiei si cerul la L* {Fmt.F0(Thresholds.LowSeparationLstar)} sau peste; un rand cu praful pe negru "
                        + $"si cerul sub prag e marcat \"cer sub L* {Fmt.F0(Thresholds.LowSeparationLstar)}\". "
                        + "Recomandarea e nivelul MINIM print-safe: mai sus pe o imagine prea intunecata, mai jos pe una prea luminoasa."),
                },
                new GuideParagraph
                {
                    Lead = Tr.Pick("The simulation", "Simularea"),
                    Text = Tr.Pick("answers the question \"and what if I move the background?\". On a reduced copy of the image, the program lifts or lowers "
                        + $"the background to each target of the grid {Fmt.Level(grid.Min())}..{Fmt.Level(grid.Max())}, with a simple curve, the same on every channel "
                        + "(a gamma curve), then measures the dust and the sky again. The \"as is\" row sits among the targets, at its level. "
                        + "The lowest target marked print-safe is the recommended one: redo the stretch from the linear data in Siril up to that background. "
                        + $"Under the table: the exact background at which the dust reaches black, and the same at a Dmax {Fmt.F1(Thresholds.DmaxSensitivityStep)} lower and higher, "
                        + "because the Dmax of a paper on a printer is rarely known better than that. It is a simulation: a real stretch (Siril, VeraLux) "
                        + "holds the shadows lower than the curve here, so the exact target is a guide. The Preview button of a row shows the image at its target "
                        + $"in a separate window; its two check boxes colour what would come out black on paper and what is under L* {Fmt.F0(Thresholds.LowSeparationLstar)}. "
                        + $"A target that is too high washes the sky into grey; watch the \"L* > {Fmt.F0(Thresholds.HighlightLstar)}\" column.",
                        "raspunde la intrebarea \"si daca mut fundalul?\". Pe o copie redusa a imaginii, programul ridica sau coboara "
                        + $"fundalul la fiecare tinta din grila {Fmt.Level(grid.Min())}..{Fmt.Level(grid.Max())}, cu o curba simpla, aceeasi pe toate canalele "
                        + "(o curba gamma), apoi masoara din nou praful si cerul. Randul \"asa cum e\" sta printre tinte, la nivelul lui. "
                        + "Cea mai mica tinta marcata print-safe e cea recomandata: refa stretch-ul din datele liniare in Siril pana la fundalul acela. "
                        + $"Sub tabel: fundalul exact la care praful ajunge pe negru, si acelasi lucru la un Dmax cu {Fmt.F1(Thresholds.DmaxSensitivityStep)} mai mic si mai mare, "
                        + "fiindca Dmax-ul unei hartii pe o imprimanta rar e stiut mai bine de atat. E o simulare: un stretch adevarat (Siril, VeraLux) "
                        + "tine umbrele mai jos decat curba de aici, deci tinta exacta e orientativa. Butonul Preview al unui rand arata imaginea la tinta lui "
                        + $"intr-o fereastra separata; cele doua casute de acolo coloreaza ce ar iesi negru pe hartie si ce e sub L* {Fmt.F0(Thresholds.LowSeparationLstar)}. "
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
        internal override void Clear()
        {
            base.Clear();
            JpegRows = Array.Empty<string>();
            JpegGuide = Array.Empty<GuideParagraph>();
            Rows = Array.Empty<MetricRow>();
        }

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
            JpegGuide = JpegGuideParagraphs(r.Jpeg);

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
            string plateauDesc = Tr.Pick($"At each of {Fmt.Int(Thresholds.PlateauPositions)} random positions a tile is kept when it is clean sky (a range under {Fmt.F0(Thresholds.PlateauTileMaxRangeLevels)} levels "
                + $"and a mean under {Fmt.F0(Thresholds.PlateauTileMaxMeanLevels)}, so as not to catch the core), and on each tile kept it is measured, at 8 bits, how long the runs "
                + "of neighbouring pixels with exactly the same value are. A noiseless sky quantized to 8 bits makes wide steps; the residual noise breaks them, "
                + "like a natural dither. A tile's plateau in pixels, times the pixel pitch at the reference row's dpi, is the length of its step on paper: "
                + $"over {Fmt.F1(Thresholds.PlateauVisibleMm)} mm it is a visible contour. The verdict is the share of the frame in such tiles (tiles over the positions tried): "
                + $"over {Fmt.F2(Thresholds.PlateauFlatFrameWarnPct)} % it is a banding risk - 16 bits if the lab accepts it, or dither at the export. The plateau shown is the mean over the clean tiles, "
                + "the typical step, for information: it cannot decide, because on a heavily denoised frame the many ordinary tiles drown the few fully flat ones. "
                + "n/a when no clean sky tiles are found. The tiles' share of the frame and the noise left on them say where the plateau "
                + "was measured: on an AI-denoised frame the only tiles clean enough can be a few patches the denoiser wiped, while the noise section's median, "
                + "taken on other tiles, chosen with a looser criterion, says the sky is fine; the two speak of different places.",
                $"La fiecare dintre cele {Fmt.Int(Thresholds.PlateauPositions)} de pozitii aleatoare se pastreaza dala daca e cer curat (interval sub {Fmt.F0(Thresholds.PlateauTileMaxRangeLevels)} niveluri "
                + $"si medie sub {Fmt.F0(Thresholds.PlateauTileMaxMeanLevels)}, ca sa nu prinda nucleul), si pe fiecare dala pastrata se masoara, pe 8 biti, cat de lungi sunt sirurile "
                + "de pixeli vecini cu exact aceeasi valoare. Un cer fara zgomot cuantizat la 8 biti face trepte late; zgomotul rezidual le sparge, "
                + "ca un dither natural. Palierul unei dale in pixeli, inmultit cu pasul pixelului la dpi-ul randului de referinta, e lungimea treptei ei pe hartie: "
                + $"peste {Fmt.F1(Thresholds.PlateauVisibleMm)} mm e un contur vizibil. Verdictul e partea din cadru ocupata de astfel de dale (dale raportate la pozitiile incercate): "
                + $"peste {Fmt.F2(Thresholds.PlateauFlatFrameWarnPct)} % e risc de banding - 16 biti daca laboratorul accepta, sau dither la export. Palierul afisat e media pe dalele curate, "
                + "treapta tipica, informativ: nu poate decide, pentru ca pe un cadru cu denoise tare multele dale obisnuite le ineaca pe cele cateva complet plate. "
                + "n/a cand nu se gasesc dale de cer curat. Partea din cadru a dalelor si zgomotul ramas pe ele spun unde s-a masurat "
                + "palierul: pe un cadru cu denoise AI, singurele dale destul de curate pot fi cateva petice sterse de denoise, in timp ce mediana sectiunii de zgomot, "
                + "luata pe alte dale, alese cu un criteriu mai larg, spune ca cerul e bine; cele doua vorbesc despre locuri diferite.");
            rows.Add(q.PlateauKnown
                ? Row(Tr.Pick("Plateau on the quantized sky", "Palier pe cerul cuantizat"), $"{Fmt.F1(q.PlateauPx)} px",
                    Tr.Pick($"over {q.PlateauTiles} tiles, {Fmt.Pct(q.PlateauFramePct)} of the frame, noise left sigma {Fmt.F2(q.PlateauSigmaLsb)} LSB", $"pe {q.PlateauTiles} dale, {Fmt.Pct(q.PlateauFramePct)} din cadru, zgomot ramas sigma {Fmt.F2(q.PlateauSigmaLsb)} LSB")
                    + (r.Size.ReferenceRow != null
                        ? Tr.Pick($"; {Fmt.F2(q.PlateauMmAt(r.Size.ReferenceRow.Dpi))} mm at {Fmt.F0(r.Size.ReferenceRow.Dpi)} dpi; flat steps over {Fmt.F1(Thresholds.PlateauVisibleMm)} mm on {Fmt.F2(q.PlateauVisibleFramePctAt(r.Size.ReferenceRow.Dpi))} % of the frame (threshold {Fmt.F2(Thresholds.PlateauFlatFrameWarnPct)} %)",
                            $"; {Fmt.F2(q.PlateauMmAt(r.Size.ReferenceRow.Dpi))} mm la {Fmt.F0(r.Size.ReferenceRow.Dpi)} dpi; trepte plate de peste {Fmt.F1(Thresholds.PlateauVisibleMm)} mm pe {Fmt.F2(q.PlateauVisibleFramePctAt(r.Size.ReferenceRow.Dpi))} % din cadru (prag {Fmt.F2(Thresholds.PlateauFlatFrameWarnPct)} %)")
                        : ""), plateauDesc)
                : Row(Tr.Pick("Plateau on the quantized sky", "Palier pe cerul cuantizat"), "n/a", Tr.Pick("no clean sky tiles", "fara dale de cer curat"), plateauDesc));
            Rows = rows;
            HasResult = true;
        }

        /// <summary>
        /// The text under the JPEG table: explanation, not diagnosis. It depends on the thresholds,
        /// and on the image only through the core's <c>JpegResult.HighestFailingBelow100</c>: the
        /// "what is recommended" paragraph states that row with its maximum error, the report's
        /// former warning (ADR 0061); the view-model selects and decides nothing.
        /// </summary>
        /// <param name="jpeg">The JPEG measurement, for the quality under 100 that fails, if any.</param>
        /// <returns>The five paragraphs.</returns>
        static IReadOnlyList<GuideParagraph> JpegGuideParagraphs(JpegResult jpeg)
        {
            string count = Fmt.F0(Thresholds.JpegErrorCountLevels);
            // The evidence for "do not go below q100", once a warning of the report (ADR 0061): the
            // highest quality under 100 that fails the maximum-error threshold, named with its figure.
            JpegRow failing = jpeg.HighestFailingBelow100;
            string failingEn = failing == null ? "" : $" Here q{failing.Quality} reaches a maximum error of {Fmt.F1(failing.MaxError)} levels (threshold {Fmt.F0(Thresholds.JpegMaxErrorReject)}): do not go below q100.";
            string failingRo = failing == null ? "" : $" Aici q{failing.Quality} ajunge la o eroare maxima de {Fmt.F1(failing.MaxError)} niveluri (prag {Fmt.F0(Thresholds.JpegMaxErrorReject)}): nu cobori sub q100.";
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
                        + "and the size gained does not matter for a print." + failingEn,
                        "TIFF 8-bit LZW. Daca laboratorul accepta doar JPEG: q100, 4:4:4. Sub q100 eroarea maxima sare pe stele si pe muchii, "
                        + "iar castigul de marime nu conteaza pentru un print." + failingRo),
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
        internal override void Clear()
        {
            base.Clear();
            Rows = Array.Empty<MetricRow>();
            SizeRows = Array.Empty<string>();
            DpiRows = Array.Empty<string>();
        }

        /// <inheritdoc/>
        internal override void Populate(AnalysisResult r)
        {
            StarResult s = r.Stars;
            PrintSizeResult ps = r.Size;
            // The descriptions: what the row measures, with the thresholds from Thresholds, not
            // typed in. The corners have no description - the centre's covers them, and four
            // times the same text would load the panel.
            string minStars = Fmt.Int(Thresholds.StarMinCount), regionStars = Fmt.Int(Thresholds.RegionMinStars);
            string fwhmDesc = Tr.Pick("The width of a star (FWHM): how wide a star comes out in the image, in pixels, measured at half its brightness. "
                + "This is the finest detail the image holds, however many pixels the file has. The main figure is the middle value over all "
                + "the stars. The two in brackets: a quarter of the stars are narrower than the first, a quarter wider than the second. "
                + "When the two are far apart, the stars are not the same over the whole frame: soft corners, tracking trails, or stars "
                + "on nebulosity, which come out wider.",
                "Latimea unei stele (FWHM): cat de lata iese o stea in imagine, in pixeli, masurata la jumatatea stralucirii ei. "
                + "Acesta e cel mai fin detaliu pe care il are imaginea, oricati pixeli ar avea fisierul. Cifra principala e valoarea din "
                + "mijloc pe toate stelele. Cele doua din paranteza: un sfert din stele sunt mai inguste decat prima, un sfert mai late "
                + "decat a doua. Daca cele doua sunt departe una de alta, stelele nu sunt la fel in tot cadrul: colturi moi, urme de "
                + "tracking sau stele pe nebuloasa, care ies mai late.");
            var rows = new List<MetricRow>
            {
                Row(Tr.Pick("Stars measured", "Stele masurate"), Fmt.Int(s.Count),
                    s.Insufficient ? Tr.Pick($"{Fmt.F1(s.DensityPerMp)} per megapixel: too few to measure the stars' width, FWHM (at least {minStars} are needed, and {Fmt.F0(Thresholds.StarMinPerMegapixel)} per megapixel)", $"{Fmt.F1(s.DensityPerMp)} pe megapixel: prea putine ca sa masuram latimea stelelor, FWHM (e nevoie de cel putin {minStars}, si de {Fmt.F0(Thresholds.StarMinPerMegapixel)} pe megapixel)") : null,
                    Tr.Pick("How many stars the program found and could measure. A star is a clear bright point, not burnt out, "
                        + $"enough brighter than the sky around it (at least {Fmt.F0(Thresholds.StarMinSnr)} times the sky's noise, so that the grain is not taken for stars) "
                        + $"and between {Fmt.F1(Thresholds.StarFwhmMin)} and {Fmt.F0(Thresholds.StarFwhmMax)} pixels wide. "
                        + "They are searched for on the green channel; in mono, on the only channel. "
                        + $"Under {minStars} stars, or under {Fmt.F0(Thresholds.StarMinPerMegapixel)} per megapixel, the measurement is not reliable and is not shown: "
                        + "an image without stars keeps a few dozen soft blobs after a denoise, which would look like stars.",
                        "Cate stele a gasit programul si a putut masura. O stea e un punct luminos clar, nears, "
                        + $"destul de mai luminos decat cerul din jur (de cel putin {Fmt.F0(Thresholds.StarMinSnr)} ori zgomotul cerului, ca granulatia sa nu fie luata drept stele) "
                        + $"si cu latimea intre {Fmt.F1(Thresholds.StarFwhmMin)} si {Fmt.F0(Thresholds.StarFwhmMax)} pixeli. "
                        + "Se cauta pe canalul verde; la alb-negru, pe singurul canal. "
                        + $"Sub {minStars} de stele, sau sub {Fmt.F0(Thresholds.StarMinPerMegapixel)} pe megapixel, masuratoarea nu e de incredere si nu se arata: "
                        + "o imagine fara stele pastreaza dupa denoise cateva zeci de pete moi, care ar parea stele.")),
                Row(Tr.Pick("Star width (FWHM)", "Latimea stelelor (FWHM)"), s.Insufficient ? "n/a" : $"{Fmt.F2(s.FwhmMedian)} px ({Fmt.F2(s.FwhmQ1)} / {Fmt.F2(s.FwhmQ3)})",
                    description: fwhmDesc),
                Row(Tr.Pick("Real detail across the width", "Detaliu real pe latime"), ps.FwhmKnown ? $"~{Fmt.F0(ps.EffectiveDetailPx)} px" : "n/a",
                    description: Tr.Pick("How many stars fit side by side across the width of the image (after the crop, if there is one). "
                        + "That is how much detail the file really holds; the extra pixels bring nothing new. The print size is "
                        + "judged on this figure, not on the number of pixels, and the table below starts from here.",
                        "Cate stele incap una langa alta pe latimea imaginii (dupa decupaj, daca e cazul). "
                        + "Atat detaliu are fisierul cu adevarat; pixelii in plus nu aduc nimic nou. Marimea de print se "
                        + "judeca dupa cifra asta, nu dupa numarul de pixeli, si tabelul de mai jos porneste de aici.")),
            };
            foreach (StarRegion reg in s.Regions)
            {
                bool centre = reg == s.Regions[0];
                rows.Add(Row(Tr.Pick("Star width (FWHM) in the " + reg.Name, "Latimea stelelor (FWHM) in " + (centre ? reg.Name : reg.Name.Replace("colt ", "coltul "))),
                    reg.Known ? Fmt.F2(reg.FwhmMedian) + " px" : "n/a", Tr.Pick($"{reg.Count} star{(reg.Count == 1 ? "" : "s")}", $"{reg.Count} stele"),
                    centre
                        ? Tr.Pick($"The stars' width (FWHM) in five zones: the centre ({Fmt.F0(Thresholds.CenterFraction * 100)} % x {Fmt.F0(Thresholds.CenterFraction * 100)} % of the frame) "
                            + $"and the four corners ({Fmt.F0(Thresholds.RegionFraction * 100)} % of each side each, starting from the corner). "
                            + $"A zone is shown only when it has at least {regionStars} stars, otherwise n/a. "
                            + "Compared with each other, they say whether the lens is equally good over the whole field.",
                            $"Latimea stelelor (FWHM) in cinci zone: centrul ({Fmt.F0(Thresholds.CenterFraction * 100)} % x {Fmt.F0(Thresholds.CenterFraction * 100)} % din cadru) "
                            + $"si cele patru colturi (cate {Fmt.F0(Thresholds.RegionFraction * 100)} % din fiecare latura, pornind din colt). "
                            + $"O zona se arata doar daca are cel putin {regionStars} stele, altfel n/a. "
                            + "Comparate intre ele, spun daca obiectivul e la fel de bun pe tot campul.")
                        : null));
            }
            if (!double.IsNaN(s.CornerToCenterRatio))
                rows.Add(Row(Tr.Pick("Corner / centre", "Colt / centru"), Fmt.F2(s.CornerToCenterRatio), s.SoftCorners ? Tr.Pick("soft corners", "colturi moi") : null,
                    Tr.Pick("The stars' width (FWHM) in the weakest corner divided by the centre's. 1.00 means the same stars everywhere. "
                        + $"Over {Fmt.F1(Thresholds.CornerSoftRatio)} the corners are soft (coma, field curvature or tilt): at the size at which "
                        + "the centre is at the limit, the corners are already blobs, so the size table no longer holds for the whole frame. "
                        + "Then the whole frame is not enlarged; the edges are cut instead. "
                        + $"n/a when the centre or all the corners have under {regionStars} stars.",
                        "Latimea stelelor (FWHM) din cel mai slab colt impartita la cea din centru. 1.00 inseamna stele la fel peste tot. "
                        + $"Peste {Fmt.F1(Thresholds.CornerSoftRatio)} colturile sunt moi (coma, curbura de camp sau tilt): la marimea la care "
                        + "centrul e la limita, colturile sunt deja pete, asa ca tabelul de marimi nu mai e valabil pentru tot cadrul. "
                        + "Atunci nu se mareste tot cadrul, ci se taie din margini. "
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
            DpiRows = new[] { Tr.Pick("dpi   pixel mm   pixels vanish from   that is", "dpi   pixel mm   pixelii dispar de la   adica") }
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
        internal override void Clear()
        {
            base.Clear();
            Rows = Array.Empty<MetricRow>();
            RegionRows = Array.Empty<string>();
            Guide = Array.Empty<GuideParagraph>();
        }

        /// <inheritdoc/>
        internal override void Populate(AnalysisResult r)
        {
            BackgroundResult b = r.Background;
            string cast = Fmt.F1(Thresholds.CastWarnChroma);
            var rows = new List<MetricRow>
            {
                Row(b.ColorApplicable
                        ? Tr.Pick("Background colour, R / G / B (0..255)", "Culoarea fundalului, R / G / B (0..255)")
                        : Tr.Pick("Background, one channel (0..255)", "Fundalul, un singur canal (0..255)"),
                    string.Join(" / ", b.Levels.Select(Fmt.F1)),
                    description: Tr.Pick("The median on each channel of the pixels of the sky patches, the class the Background tab calls sky (the band, the dust and the nebulae left out), "
                        + $"without the bottom band of {Fmt.F0(Thresholds.BackgroundExcludeBottomFraction * 100)} % of the height, where the horizon and its pollution lie; here: {b.SourceText}. "
                        + "It is the \"clean\" sky: the cast below and the edges in the next table are judged against it. "
                        + "It is not the same number as the background level in the Tones tab, which is the median over the whole frame: that one says how high the sky is, "
                        + "this one what colour it has.",
                        "Mediana pe fiecare canal a pixelilor din dalele de cer, clasa pe care tabul Fundal o numeste cer (fara banda, praf si nebuloase), "
                        + $"fara banda de jos de {Fmt.F0(Thresholds.BackgroundExcludeBottomFraction * 100)} % din inaltime, unde stau orizontul si poluarea lui; aici: {b.SourceText}. "
                        + "E cerul \"curat\": fata de el se judeca tenta de mai jos si marginile din tabelul urmator. "
                        + "Nu e acelasi numar cu nivelul fundalului din tabul Tonuri, care e mediana pe tot cadrul: acela spune cat de sus e cerul, "
                        + "acesta ce culoare are.")),
            };
            if (b.ColorApplicable)
            {
                string levels = Tr.Pick(" levels", " niveluri");
                bool greenLarger = Math.Abs(b.GreenExcess) >= Math.Abs(b.RedMinusBlue);
                string castName = !b.HasCast ? null
                    : greenLarger ? (b.GreenExcess > 0 ? Tr.Pick("green cast", "tenta verde") : Tr.Pick("magenta cast", "tenta magenta"))
                    : (b.RedMinusBlue > 0 ? Tr.Pick("warm cast", "tenta calda") : Tr.Pick("cool cast", "tenta rece"));
                rows.Add(Row(Tr.Pick("Chroma in L*a*b* (a*, b*)", "Croma in L*a*b* (a*, b*)"), $"{Fmt.F1(b.Chroma)} ({Fmt.S1(b.Lab.A)}, {Fmt.S1(b.Lab.B)})",
                    castName,
                    Tr.Pick("How far the background colour sits from a grey of the same lightness, in CIE L*a*b*: sqrt(a*^2 + b*^2), with a* negative towards green and "
                        + "positive towards magenta, b* negative towards blue and positive towards yellow. It is the size of the cast in the unit the eye works in: "
                        + "about 2 is the smallest difference seen beside a grey, and on paper, where the only reference is the white far above, a sky's cast begins to read at 3 to 5. "
                        + "A gamma lift of the background keeps the channels' ratio and barely moves the chroma, so a cast does not grow when the sky is raised for print. "
                        + $"Over {cast}: to neutralize before printing (SCNR / Background Neutralization in Siril). The two rows below name its direction.",
                        "Cat de departe sta culoarea fundalului de un gri de aceeasi luminozitate, in CIE L*a*b*: sqrt(a*^2 + b*^2), cu a* negativ spre verde si "
                        + "pozitiv spre magenta, b* negativ spre albastru si pozitiv spre galben. E marimea tentei in unitatea in care lucreaza ochiul: "
                        + "cam 2 e cea mai mica diferenta vazuta langa un gri, iar pe hartie, unde singura referinta e albul de mult mai sus, o tenta a cerului incepe sa se citeasca pe la 3-5. "
                        + "O ridicare gamma a fundalului pastreaza raportul canalelor si abia misca croma, deci o tenta nu creste cand cerul e ridicat pentru print. "
                        + $"Peste {cast}: de neutralizat inainte de print (SCNR / Background Neutralization in Siril). Cele doua randuri de mai jos ii spun directia.")));
                rows.Add(Row("G - (R+B)/2", Fmt.S1(b.GreenExcess) + levels, null,
                    Tr.Pick("How far green is over the mean of red and blue, in levels: positive = towards green, negative = towards magenta. "
                        + "A figure for the direction of the cast, not a verdict: the chroma above decides. "
                        + "A neutral sky has no excess of green; a green one comes from the airglow or the calibration, not from the object.",
                        "Cat e verdele peste media rosului si albastrului, in niveluri: pozitiv = spre verde, negativ = spre magenta. "
                        + "O cifra pentru directia tentei, nu un verdict: decide croma de mai sus. "
                        + "Un cer neutru nu are exces de verde; unul verde vine de la airglow sau de la calibrare, nu de la obiect.")));
                rows.Add(Row("R - B", Fmt.S1(b.RedMinusBlue) + levels, null,
                    Tr.Pick("Red minus blue, in levels: positive = towards warm (horizon glow, urban light pollution), negative = towards cool, bluish. "
                        + "Together with the row above it names the cast: the larger of the two gives it its name.",
                        "Rosu minus albastru, in niveluri: pozitiv = spre cald (lumina de orizont, poluare urbana), negativ = spre rece, albastrui. "
                        + "Impreuna cu randul de mai sus da numele tentei: cel mai mare dintre cele doua o numeste.")));
            }
            Rows = rows;
            RegionRows = b.Regions.Select(e =>
                $"{e.Name,-18} {string.Join(" / ", e.Levels.Select(Fmt.F1)),-22} delta {string.Join(" / ", e.Deltas.Select(Fmt.S1))}{e.PixelsNote}"
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
                        + "each side, between the corners. For each, the medians per channel (R / G / B, 0..255), then the delta. "
                        + "The medians are taken on the region's sky pixels alone, the patches the Background tab calls sky, so that the Milky Way or a dust lane "
                        + $"entering a corner does not pass for a gradient of the sky; a region with under {Fmt.F0(Thresholds.RegionSkyMinFraction * 100)} % of sky "
                        + "is measured on every pixel instead, and its row says so, so that a glow bright enough to be classed nebula cannot leave with the object.",
                        $"Patru colturi, fiecare de {Fmt.F0(Thresholds.EdgeRegionHeightFraction * 100)} % din inaltime x "
                        + $"{Fmt.F0(Thresholds.EdgeRegionWidthFraction * 100)} % din latime, si patru laturi: benzi de aceeasi grosime pe mijlocul "
                        + "fiecarei laturi, intre colturi. Pentru fiecare, medianele pe canal (R / G / B, 0..255), apoi delta. "
                        + "Medianele se iau doar pe pixelii de cer ai regiunii, dalele pe care tabul Fundal le numeste cer, ca Calea Lactee sau o banda de praf "
                        + $"care intra intr-un colt sa nu treaca drept gradient al cerului; o regiune cu sub {Fmt.F0(Thresholds.RegionSkyMinFraction * 100)} % cer "
                        + "se masoara pe toti pixelii, si randul ei o spune, ca o lumina destul de puternica sa fie clasata nebuloasa sa nu plece odata cu obiectul."),
                },
                new GuideParagraph
                {
                    Lead = "Delta.",
                    Text = Tr.Pick("The region minus the background colour above, on each channel, in levels. Zero means an edge identical with the clean sky; "
                        + "positive, an edge brighter than it (the object entering, or a glow); negative, darker (dust, or vignetting left over).",
                        "Regiune minus culoarea fundalului de mai sus, pe fiecare canal, in niveluri. Zero inseamna margine identica cu cerul curat; "
                        + "pozitiv, margine mai luminoasa decat el (obiectul care intra, sau o lumina); negativ, mai intunecata (praf, sau vinietare ramasa)."),
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
                        + "and stays at Ok, with the advice to look at the edges before you correct anything. If they move differently (the difference between "
                        + $"the channels over {grad} levels and over {Fmt.F0(Thresholds.CornerColourSpreadRatio * 100)} % of the mean departure), it is a gradient with a colour: "
                        + "horizon glow, light pollution - to correct on the linear data (GraXpert), not with a local curve on the stretched image. "
                        + "On the reference image: the horizon corner +4.1 / -4.0 / -17.1 is coloured; the corner the Milky Way enters, "
                        + "+4.2 / +2.9 / +5.6 on its sky pixels, is neutral (on every pixel it would read +6.5 / +5.1 / +8.7, bluer than the sky, and pass for coloured).",
                        "Daca cele trei delta se misca impreuna, marginea e cel mai probabil obiectul insusi (Calea Lactee care intra in colt) "
                        + "si ramane la Ok, cu indemnul sa te uiti la margini inainte sa corectezi ceva. Daca se misca diferit (diferenta dintre "
                        + $"canale peste {grad} niveluri si peste {Fmt.F0(Thresholds.CornerColourSpreadRatio * 100)} % din abaterea medie), e un gradient cu culoare: "
                        + "lumina de orizont, poluare - de corectat pe liniar (GraXpert), nu cu o curba locala pe imaginea stretch-uita. "
                        + "Pe imaginea de referinta: coltul de orizont +4.1 / -4.0 / -17.1 e colorat; coltul in care intra Calea Lactee, "
                        + "+4.2 / +2.9 / +5.6 pe pixelii lui de cer, e neutru (pe toti pixelii ar citi +6.5 / +5.1 / +8.7, mai albastru decat cerul, si ar trece drept colorat)."),
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
}
