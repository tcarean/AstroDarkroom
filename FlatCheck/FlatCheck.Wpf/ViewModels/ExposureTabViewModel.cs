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

// ExposureTabViewModel.cs - tab 4, the exposure check of a raw flat.
//
// The list comes from flats/ and holds ONLY camera raws: the mode needs the CFA mosaic intact and
// the original ADU scale, and on a raw the white and the black are read exactly from the file
// instead of being guessed from the maximum.
//
// It recomputes both on a change of file and on toggling "Sky", because the two bands differ:
// without --sky a sky flat at 22 % would be reported wrongly as underexposed. A sky flat MUST stay
// modest - the sky fades continuously and is full of faint stars, so the master's SNR comes from
// the number of frames, not from pushing a frame to 55 %.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FlatCheck.Wpf
{
    /// <summary>The Expunere tab: one raw flat from flats/ at a time, its exposure figures,
    /// verdict and advice, and the per-channel histograms of the raw data or of the camera's
    /// preview.</summary>
    public sealed class ExposureTabViewModel : TabViewModel
    {
        SirilSession _session;
        CancellationToken _ct;

        string _selected;
        bool _sky;
        string _scale = "";
        string _centre = "";
        string _global = "";
        string _clipping = "";
        string _channels = "";
        string _channelOrder = "";
        IReadOnlyList<ChannelHistogramRow> _histograms = System.Array.Empty<ChannelHistogramRow>();
        IReadOnlyList<ScreenPairRow> _comparison = System.Array.Empty<ScreenPairRow>();
        string _reading = "";
        string _sourceNote = "";
        string _caption = "";

        /// <summary>
        /// The last result, kept so the rows can be rebuilt on a change of source without
        /// re-reading a 24 MP NEF for a drawing. Null before the first run and after Reset.
        /// </summary>
        ExposureResult _last;

        /// <summary>An empty tab, listening to its histogram-source switch.</summary>
        public ExposureTabViewModel()
        {
            Source.Changed += RebuildRows;
        }
        string _verdict = "";
        string _advice = "";
        bool _isRaw = true;

        /// <summary>The raws of flats/, with the full path; only the name is shown.</summary>
        public ObservableCollection<string> Files { get; } = new();

        /// <summary>
        /// One histogram row per channel: four on CFA, one on mono.
        ///
        /// The list is REPLACED whole on every run, not cleared and refilled, so an
        /// ObservableCollection has no business here. The reason is concrete: choosing a file
        /// starts a run nobody awaits, and a second choice (or the Sky switch) can start another
        /// over it. On plain properties the last one wins and that is that; a collection cleared
        /// and refilled by two runs at once ends up with duplicated rows and nulls in it. The
        /// end-to-end series really does need an ObservableCollection, because it appends its rows
        /// as they come - not here.
        /// </summary>
        public IReadOnlyList<ChannelHistogramRow> Histograms
        {
            get => _histograms;
            private set => Set(ref _histograms, value);
        }

        /// <summary>The histograms' reading guide. Documentation, not a diagnosis.</summary>
        public HistogramGuideViewModel HistogramGuide { get; } = new();

        /// <summary>
        /// The switch between the raw data and the camera's preview. Off when the file has no
        /// preview; then it stays on the raw data.
        /// </summary>
        public HistogramSourceViewModel Source { get; } = new();

        /// <summary>The raw -> screen table, centre against centre, per channel. Empty without a
        /// preview.</summary>
        public IReadOnlyList<ScreenPairRow> Comparison
        {
            get => _comparison;
            private set => Set(ref _comparison, value);
        }

        /// <summary>
        /// Under the table: why the two differ when the preview is shown, why it is missing when
        /// there is none, and nothing when the raw data are shown and the preview is available.
        /// </summary>
        public string SourceNote { get => _sourceNote; private set => Set(ref _sourceNote, value); }

        /// <summary>The drawings' caption. It changes with the source: on the preview there is no
        /// target band.</summary>
        public string Caption { get => _caption; private set => Set(ref _caption, value); }

        public bool HasComparison => _comparison.Count > 0;

        public string Selected
        {
            get => _selected;
            set { if (Set(ref _selected, value)) Recompute(); }
        }

        /// <summary>Requirement 8: the switch changes the thresholds, so it recomputes.</summary>
        public bool Sky
        {
            get => _sky;
            set { if (Set(ref _sky, value)) Recompute(); }
        }

        public string Scale { get => _scale; private set => Set(ref _scale, value); }
        public string Centre { get => _centre; private set => Set(ref _centre, value); }
        public string Global { get => _global; private set => Set(ref _global, value); }
        public string Clipping { get => _clipping; private set => Set(ref _clipping, value); }
        public string Channels { get => _channels; private set => Set(ref _channels, value); }

        /// <summary>
        /// How we know (or do not know) which CFA position is R, G or B. It sits in the view-model
        /// and not in the XAML because it has two variants, and which of them appears is itself
        /// information about the file.
        /// </summary>
        public string ChannelOrder { get => _channelOrder; private set => Set(ref _channelOrder, value); }

        /// <summary>How this file's exposure reads - the text under the histograms.</summary>
        public string Reading { get => _reading; private set => Set(ref _reading, value); }

        public string Verdict { get => _verdict; private set => Set(ref _verdict, value); }
        public string Advice { get => _advice; private set => Set(ref _advice, value); }

        /// <summary>
        /// False when the sanity gate cut the verdict: the file is already calibrated or
        /// debayered. It is not a bad verdict, it is another file than the mode asks for.
        /// </summary>
        public bool IsRawFlat { get => _isRaw; private set => Set(ref _isRaw, value); }

        /// <summary>Lists the raws of the session's flats/, none chosen, and explains when there
        /// is none; it computes nothing until a file is chosen.</summary>
        /// <param name="session">The folder just opened.</param>
        /// <param name="ct">The folder's token, which every run observes.</param>
        internal void Attach(SirilSession session, CancellationToken ct)
        {
            _session = session;
            _ct = ct;

            Files.Clear();
            foreach (string p in session.RawFlats()) Files.Add(p);

            _selected = null;
            Raise(nameof(Selected));

            if (Files.Count == 0)
                Fail("Niciun flat brut de verificat", SessionMessages.NoRawFlats(session));
            else
                ClearError();
        }

        /// <summary>Starts a run on the chosen file without awaiting it; nothing without a
        /// session or a file.</summary>
        void Recompute()
        {
            if (_session == null || string.IsNullOrEmpty(Selected)) return;
            _ = RecomputeAsync();
        }

        /// <summary>Measures the chosen file's exposure, guarded, with the preview decoded, and
        /// shows the result and the console's report.</summary>
        /// <returns>A task that completes when the run ended; it never faults.</returns>
        internal Task RecomputeAsync()
        {
            string path = Selected;
            bool sky = Sky;
            if (string.IsNullOrEmpty(path)) return Task.CompletedTask;

            return RunGuardedAsync($"Masor expunerea pe {Path.GetFileName(path)}...", async () =>
            {
                var text = new StringWriter();
                var outcome = await Task.Run(() =>
                {
                    RunReport.WriteBanner(text);
                    // withPreview: only the window shows the preview, so only it decodes it. The
                    // console and the other modes do not pay for it.
                    return ExposureRun.Execute(path, null, null, sky, text, withPreview: true);
                }, _ct).ConfigureAwait(true);

                Report = text.ToString();
                Populate(outcome.Result);
                HasResult = true;
            });
        }

        /// <summary>
        /// Internal, not private, so a test can bring a result with a preview without a real raw:
        /// it builds it with ExposureAnalyzer.Analyze on a JPEG made in the test and hands it in
        /// here.
        /// </summary>
        /// <param name="r">The analyzer's result; it is kept for a later change of
        /// source.</param>
        internal void Populate(ExposureResult r)
        {
            _last = r;
            Scale = $"white {r.White:F0}   black {r.Black:F0}   ({r.ScaleSource})";
            Centre = $"{r.CentreMedianPct:F1} %";
            Global = $"{r.GlobalMedianPct:F1} %";
            Clipping = $"{r.CentreClipPct:F3} %";
            Channels = DescribeChannels(r);
            ChannelOrder = DescribeOrder(r);
            Reading = ExposureReport.HistogramReading(r);
            IsRawFlat = r.IsRaw;
            Verdict = VerdictText(r);
            Advice = AdviceText(r);

            Comparison = ExposureReport.ScreenComparison(r)
                .Select(c => new ScreenPairRow
                {
                    Channel = c.Channel,
                    Colour = ChannelPalette.BrushFor(c.Colour),
                    Raw = $"{c.RawPct:F1} %",
                    Screen = $"{c.ScreenPct:F1} %",
                    Gap = $"{c.ScreenPct - c.RawPct:+0;-0} puncte",
                }).ToList();
            Raise(nameof(HasComparison));

            // Turning it off forces the choice back to the raw data if it was on the preview, and
            // then Changed rebuilds the rows; otherwise we rebuild them ourselves.
            Source.IsEnabled = r.Preview != null;
            RebuildRows();

            // A populated tab HAS a result, whoever populated it. Without this, the panel stays
            // collapsed when a test brings the result directly, and its bindings are not
            // evaluated.
            HasResult = true;
        }

        /// <summary>
        /// The chosen source's rows, from the kept result. It tolerates its absence: before the
        /// first run and after Reset there is nothing to rebuild.
        /// </summary>
        void RebuildRows()
        {
            ExposureResult r = _last;
            if (r == null) return;

            bool preview = Source.IsPreviewSelected && r.Preview != null;
            Histograms = preview ? PreviewRows(r) : RawRows(r);
            Caption = preview
                ? "Axa orizontala e in % din 255. Curba palida = tot cadrul, curba plina = "
                  + "centrul 20 %, linia verticala = mediana centrului. FARA banda tinta: pe "
                  + "ecranul aparatului nu exista o tinta, si nici n-ar putea exista. Dunga "
                  + "rosie din dreapta = pixeli la 255, adica aici se satureaza ECRANUL, nu "
                  + "senzorul."
                : "Axa orizontala e in % din white (din intervalul util al senzorului). Curba "
                  + "palida = tot canalul, curba plina = centrul 20 %, linia verticala = mediana "
                  + "centrului, adica cifra care decide. Banda verde = tinta, dunga rosie din "
                  + "dreapta = clipping peste 98 % din white.";
            SourceNote = r.Preview == null
                ? ExposureReport.NoPreviewReason(r)
                : preview ? ExposureReport.ScreenReading(r) : "";
        }

        /// <summary>The four CFA channels' medians as a percentage of the useful range, one per
        /// line, the brightest marked.</summary>
        /// <param name="r">The analyzer's result.</param>
        /// <returns>Four lines on a CFA frame; one sentence on a mono or processed one.</returns>
        static string DescribeChannels(ExposureResult r)
        {
            if (!r.IsCfa) return "imagine mono/procesata (fara CFA)";

            var parts = new string[4];
            for (int i = 0; i < 4; i++)
            {
                double pct = (r.ChannelMedians[i] - r.Black) / r.Range * 100;
                parts[i] = $"{r.ChannelLabel(i),-8} {pct:F1} %"
                         + (i == r.BrightestChannel ? "  (cel mai luminos)" : "");
            }
            return string.Join("\n", parts);
        }

        /// <summary>
        /// The colour order is read from the file or not known at all - it is not guessed. When
        /// it is not known, the sentence is the one it always was: it does not even matter for
        /// the verdict, because the limiting one is the brightest channel whatever its colour.
        /// </summary>
        /// <param name="r">The analyzer's result.</param>
        /// <returns>The sentence for the card, Romanian.</returns>
        static string DescribeOrder(ExposureResult r)
        {
            if (!r.IsCfa)
                return "Cadrul nu mai are mozaic CFA, deci nu exista canale de numit.";

            return r.Cfa != null
                ? $"Ordinea culorilor e citita din fisier ({r.Cfa.Source}: {r.Cfa.Letters}), nu ghicita. "
                + "Canalul cel mai luminos clipeaza primul, deci el e cel limitant."
                : "Nu stiu care pozitie CFA e R, G sau B - fisierul n-o spune, iar ordinea difera "
                + "de la aparat la aparat. Nici nu conteaza pentru verdict: canalul cel mai "
                + "luminos clipeaza primul, deci el e cel limitant.";
        }

        /// <summary>
        /// One drawing row per channel. The normalisation and the formatting are of the same kind
        /// as "44.4 %": they prepare a figure so it can be shown, they decide nothing about it.
        /// The thresholds come already chosen in r.Bands, so the band drawn IS the band that
        /// voted.
        /// </summary>
        /// <param name="r">The analyzer's result.</param>
        /// <returns>One row per channel of the raw data, with the target band.</returns>
        static IReadOnlyList<ChannelHistogramRow> RawRows(ExposureResult r)
        {
            ExposureBands b = r.Bands;
            string bandLabel = ExposureReport.BandLabel(b);

            return r.Channels.Select(c => new ChannelHistogramRow
            {
                Label = c.Label,
                Position = r.IsCfa && r.Cfa != null ? $"CFA[{c.Position / 2},{c.Position % 2}]" : "",
                Colour = ChannelPalette.BrushFor(c.Colour),
                Median = $"{c.MedianPct:F1} % tot canalul",
                Centre = $"{c.CentreMedianPct:F1} %",
                Note = ExposureReport.ChannelNote(r, c),
                IsBrightest = c.IsBrightest && r.IsCfa,
                Whole = Normalise(c.Whole),
                CentreCurve = Normalise(c.Centre),
                Marker = c.CentreMedianPct,
                BandLo = b.Lo,
                BandHi = b.Hi,
                // On the sky there is no yellow shoulder on the left, so no zone to draw either:
                // NaN tells the drawing "skip it", not "draw a zone of width 0".
                TooLow = b.HasLowShoulder ? b.TooLow : double.NaN,
                TooHigh = b.TooHigh,
                BandLabel = bandLabel,
            }).ToList();
        }

        /// <summary>
        /// The preview's rows: the same drawings, but WITHOUT bands - all four NaN, and
        /// HistogramCanvas skips NaN by itself, so the drawing needed no flag. A target band on
        /// the screen's axis would say "expose until the screen shows 50-65 %", that is exactly the
        /// mistake this comparison exists to fix. Neither the note nor the highlighting of the
        /// brightest: nothing decides here. The clipping stripe stays, so it shows when the screen
        /// is stuck at 255.
        /// </summary>
        /// <param name="r">The analyzer's result; its preview is not null.</param>
        /// <returns>One row per channel of the preview, without bands.</returns>
        static IReadOnlyList<ChannelHistogramRow> PreviewRows(ExposureResult r)
            => r.Preview.Channels.Select(c => new ChannelHistogramRow
            {
                Label = c.Label,
                Position = "",
                Colour = ChannelPalette.BrushFor(c.Colour),
                Median = $"{c.MedianPct:F1} % tot cadrul",
                Centre = $"{c.CentreMedianPct:F1} %",
                Note = c.ClipPct > Th.ExpoClip ? $"pixeli la 255: {c.ClipPct:F2} % din cadru" : "",
                IsBrightest = false,
                Whole = Normalise(c.Whole),
                CentreCurve = Normalise(c.Centre),
                Marker = c.CentreMedianPct,
                BandLo = double.NaN,
                BandHi = double.NaN,
                TooLow = double.NaN,
                TooHigh = double.NaN,
                BandLabel = null,
            }).ToList();

        /// <summary>
        /// The bins scaled to their own peak. Each curve on its own scale because the central
        /// window holds 4 % of the channel's pixels: on a common scale it would be a flat line,
        /// and it is the very one that decides. What is compared between the curves is the
        /// POSITION, not the height.
        /// </summary>
        /// <param name="h">The histogram, or null.</param>
        /// <returns><see cref="LevelHistogram.Bins"/> values in 0..1; all zero for a null or
        /// empty histogram.</returns>
        static double[] Normalise(LevelHistogram h)
        {
            var o = new double[LevelHistogram.Bins];
            if (h == null) return o;

            int peak = 0;
            foreach (int n in h.Counts) if (n > peak) peak = n;
            if (peak == 0) return o;

            for (int i = 0; i < o.Length; i++) o[i] = h.Counts[i] / (double)peak;
            return o;
        }

        /// <summary>
        /// The verdict in words. The strings are those VerdictPalette colours through
        /// Stats.Severity, so they stay in the same vocabulary as the rest of the application.
        /// </summary>
        /// <param name="r">The analyzer's result.</param>
        /// <returns>INAPLICABIL, OK, ACCEPTABIL or PROBLEMA.</returns>
        static string VerdictText(ExposureResult r) => r.Verdict switch
        {
            ExposureVerdict.NotRaw => Stats.NotApplicable,
            ExposureVerdict.Ok => "OK",
            ExposureVerdict.AcceptableLow => "ACCEPTABIL",
            ExposureVerdict.AcceptableHigh => "ACCEPTABIL",
            _ => "PROBLEMA",
        };

        /// <summary>What to do next, by verdict: the correction in stops toward the bands'
        /// target, with the sky flat's own wording under Sky; the sanity gate's explanation for a
        /// file that is not a raw flat.</summary>
        /// <param name="r">The analyzer's result.</param>
        /// <returns>The advice, Romanian, on one or more lines.</returns>
        static string AdviceText(ExposureResult r)
        {
            if (!r.IsRaw) return NotRawAdvice(r);

            double cen = r.CentreMedianPct;
            bool clipping = r.CentreClipPct > Th.ExpoClip;
            // The target comes from the bands the verdict was given on, it is not re-chosen here.
            ExposureBands b = r.Bands;
            double stops = cen > 0 ? Math.Log2(b.Target / cen) : double.NaN;

            if (r.IsSky)
                return r.Verdict switch
                {
                    ExposureVerdict.TooHigh =>
                        "PREA SUS" + (clipping ? " - CLIPPING in centru!" : " - risc de stele in cadru.")
                        + "\nScade expunerea.",
                    ExposureVerdict.TooLow =>
                        $"PREA JOS pentru sky flat.\nCreste cu ~{stops:F1} stopuri, dar repede - cerul se stinge.",
                    ExposureVerdict.Ok =>
                        $"OK pentru sky flat (centru {b.Lo:F0}-{b.Hi:F0} %).\n"
                      + "La sky flat nivelul modest e corect. SNR-ul vine din numarul de cadre, nu "
                      + "din a urca expunerea - ai risca stele. NU forta mai sus.",
                    _ =>
                        "Cam sus, dar acceptabil.\nSub 60 % e ok; peste, risti stele. Nu urca mai mult.",
                };

            return r.Verdict switch
            {
                ExposureVerdict.TooHigh =>
                    "PREA SUS" + (clipping ? " - CLIPPING in centru!" : ".")
                    + $"\nScade expunerea cu ~{-stops:F1} stopuri."
                    + (clipping
                        ? "\nCentrul clipeaza, deci pierzi informatia de vignetare exact unde semnalul e maxim."
                        : ""),
                ExposureVerdict.TooLow =>
                    $"PREA JOS.\nCreste expunerea cu ~{stops:F1} stopuri.\n"
                  + "Sub ~30 % in centru masterul iese zgomotos (zgomot fixed-pattern).",
                ExposureVerdict.AcceptableLow =>
                    $"Acceptabil, dar cam jos.\nAi putea creste cu ~{stops:F1} stopuri.",
                ExposureVerdict.Ok =>
                    $"OK - centrul e in tinta {b.Lo:F0}-{b.Hi:F0} %.",
                _ =>
                    $"Acceptabil, dar cam sus.\nAi putea scadea cu ~{-stops:F1} stopuri.",
            };
        }

        /// <summary>
        /// The sanity gate. The typical cause is "I opened the NEF in Siril and pressed Save",
        /// which debayers - so exactly that is said, not only that the file is not good.
        /// </summary>
        /// <param name="r">The analyzer's result, with the gate's two reasons.</param>
        /// <returns>The explanation, Romanian, in paragraphs.</returns>
        static string NotRawAdvice(ExposureResult r)
        {
            string why = "";
            if (r.LevelTooLow)
                why += $"Nivelul e ~{r.CentreMedianPct:F1} % (langa zero) - flat-dark deja scazut, sau "
                     + "imagine normalizata. Modul are nevoie de nivelul ADU original.\n\n";
            if (r.LooksDebayered)
                why += $"Cele 4 canale CFA sunt quasi-identice (spread {r.CfaSpread:F3}) - fisierul e "
                     + "DEBAYERIZAT. Modul are nevoie de mozaicul Bayer intact.\n\n";

            return "FISIERUL NU PARE UN FLAT BRUT\n\n" + why
                 + "Cauza tipica: ai deschis NEF-ul in Siril si ai dat Save (asta debayerizeaza).\n\n"
                 + "Solutii: ruleaza direct pe NEF - aplicatia citeste brutul exact - sau produ un "
                 + "FITS brut cu tab-ul Conversion din Siril (NU Open + Save), care pastreaza "
                 + "mozaicul CFA nedebayerizat.";
        }

        /// <inheritdoc/>
        internal override void Reset()
        {
            base.Reset();
            _session = null;
            Files.Clear();
            _selected = null;
            Raise(nameof(Selected));
            Scale = Centre = Global = Clipping = Channels = "";
            ChannelOrder = "";
            Reading = "";
            Histograms = System.Array.Empty<ChannelHistogramRow>();
            Comparison = System.Array.Empty<ScreenPairRow>();
            Raise(nameof(HasComparison));
            SourceNote = "";
            Caption = "";
            _last = null;
            // Source.Selected stays as the user left it: it is a viewing preference, like the
            // map's scale. Only the availability is withdrawn, because there is no file any more.
            Source.IsEnabled = false;
            Verdict = "";
            Advice = "";
            IsRawFlat = true;
            // HistogramGuide.IsExpanded stays as the user left it: it is a viewing preference,
            // not a result - the same as the map guide.
        }
    }
}
