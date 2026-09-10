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

// ExposureTabViewModel.cs - tabul 4, verificarea expunerii unui flat brut.
//
// Lista vine din flats/ si contine DOAR raw-uri de aparat: modul are nevoie de mozaicul
// CFA intact si de scala ADU originala, iar pe un raw white si black se citesc exact din
// fisier in loc sa se ghiceasca din maxim.
//
// Se recalculeaza si la schimbarea fisierului, si la comutarea lui "Sky", fiindca cele
// doua benzi sunt diferite: fara --sky un sky flat la 22 % ar fi raportat gresit ca
// subexpus. Un sky flat TREBUIE sa stea modest - cerul se stinge continuu si e plin de
// stele slabe, deci SNR-ul masterului vine din numarul de cadre, nu din a impinge un
// cadru la 55 %.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FlatCheck.Wpf
{
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
        string _reading = "";
        string _verdict = "";
        string _advice = "";
        bool _isRaw = true;

        /// <summary>Raw-urile din flats/, cu calea completa; se afiseaza doar numele.</summary>
        public ObservableCollection<string> Files { get; } = new();

        /// <summary>
        /// Cate un rand de histograma pe canal: patru pe CFA, unul pe mono.
        ///
        /// Lista se INLOCUIESTE intreaga la fiecare rulare, nu se goleste si se reumple,
        /// deci nu are ce cauta aici un ObservableCollection. Motivul e concret: alegerea
        /// unui fisier porneste o rulare pe care n-o asteapta nimeni, iar o a doua alegere
        /// (sau comutatorul Sky) poate porni una peste ea. Pe proprietati simple castiga
        /// ultima si gata; o colectie golita si reumpluta din doua rulari deodata ajunge
        /// cu randuri duplicate si cu null-uri in ea. Seria end-to-end chiar are nevoie de
        /// ObservableCollection, fiindca isi adauga randurile pe masura ce vin - aici nu.
        /// </summary>
        public IReadOnlyList<ChannelHistogramRow> Histograms
        {
            get => _histograms;
            private set => Set(ref _histograms, value);
        }

        /// <summary>Ghidul de citire al histogramelor. Documentatie, nu diagnostic.</summary>
        public HistogramGuideViewModel HistogramGuide { get; } = new();

        public string Selected
        {
            get => _selected;
            set { if (Set(ref _selected, value)) Recompute(); }
        }

        /// <summary>Cerinta 8: comutatorul schimba pragurile, deci re-calculeaza.</summary>
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
        /// De unde stim (sau nu stim) care pozitie CFA e R, G sau B. Sta in ViewModel si
        /// nu in XAML fiindca are doua variante, iar care dintre ele apare e chiar o
        /// informatie despre fisier.
        /// </summary>
        public string ChannelOrder { get => _channelOrder; private set => Set(ref _channelOrder, value); }

        /// <summary>Cum se citeste expunerea fisierului asta - textul de sub histograme.</summary>
        public string Reading { get => _reading; private set => Set(ref _reading, value); }

        public string Verdict { get => _verdict; private set => Set(ref _verdict, value); }
        public string Advice { get => _advice; private set => Set(ref _advice, value); }

        /// <summary>
        /// false cand gate-ul de sanitate a taiat verdictul: fisierul e deja calibrat sau
        /// debayerizat. Nu e un verdict prost, e alt fisier decat cere modul.
        /// </summary>
        public bool IsRawFlat { get => _isRaw; private set => Set(ref _isRaw, value); }

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

        void Recompute()
        {
            if (_session == null || string.IsNullOrEmpty(Selected)) return;
            _ = RecomputeAsync();
        }

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
                    return ExposureRun.Execute(path, null, null, sky, text);
                }, _ct).ConfigureAwait(true);

                Report = text.ToString();
                Populate(outcome.Result);
                HasResult = true;
            });
        }

        void Populate(ExposureResult r)
        {
            Scale = $"white {r.White:F0}   black {r.Black:F0}   ({r.ScaleSource})";
            Centre = $"{r.CentreMedianPct:F1} %";
            Global = $"{r.GlobalMedianPct:F1} %";
            Clipping = $"{r.CentreClipPct:F3} %";
            Channels = DescribeChannels(r);
            ChannelOrder = DescribeOrder(r);
            Reading = ExposureReport.HistogramReading(r);
            FillHistograms(r);
            IsRawFlat = r.IsRaw;
            Verdict = VerdictText(r);
            Advice = AdviceText(r);
        }

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
        /// Ordinea culorilor se citeste din fisier sau nu se stie deloc - nu se ghiceste.
        /// Cand nu se stie, propozitia e cea dintotdeauna: nici nu conteaza pentru verdict,
        /// fiindca limitantul e canalul cel mai luminos oricare ar fi culoarea lui.
        /// </summary>
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
        /// Un rand de desen pe canal. Normalizarea si formatarea sunt de acelasi fel ca
        /// "44.4 %": pregatesc o cifra ca sa poata fi aratata, nu decid nimic despre ea.
        /// Pragurile vin gata alese in r.Bands, deci banda desenata E banda care a votat.
        /// </summary>
        void FillHistograms(ExposureResult r)
        {
            ExposureBands b = r.Bands;
            string bandLabel = ExposureReport.BandLabel(b);

            Histograms = r.Channels.Select(c => new ChannelHistogramRow
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
                    // Pe cer nu exista umar galben in stanga, deci nici zona de desenat:
                    // NaN spune desenului "sari peste", nu "deseneaza o zona de latime 0".
                    TooLow = b.HasLowShoulder ? b.TooLow : double.NaN,
                    TooHigh = b.TooHigh,
                    BandLabel = bandLabel,
                }).ToList();
        }

        /// <summary>
        /// Cosurile scalate la propriul varf. Fiecare curba pe scara ei fiindca fereastra
        /// centrala are 4 % din pixelii canalului: pe o scara comuna ar fi o linie plata,
        /// si tocmai ea e cea care decide. Ce se compara intre curbe e POZITIA, nu inaltimea.
        /// </summary>
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
        /// Verdictul in cuvinte. Sirurile sunt cele pe care le coloreaza VerdictPalette
        /// prin Stats.Severity, deci raman in acelasi vocabular ca restul aplicatiei.
        /// </summary>
        static string VerdictText(ExposureResult r) => r.Verdict switch
        {
            ExposureVerdict.NotRaw => Stats.NotApplicable,
            ExposureVerdict.Ok => "OK",
            ExposureVerdict.AcceptableLow => "ACCEPTABIL",
            ExposureVerdict.AcceptableHigh => "ACCEPTABIL",
            _ => "PROBLEMA",
        };

        static string AdviceText(ExposureResult r)
        {
            if (!r.IsRaw) return NotRawAdvice(r);

            double cen = r.CentreMedianPct;
            bool clipping = r.CentreClipPct > Th.ExpoClip;
            // Tinta vine din benzile pe care s-a dat verdictul, nu se realege aici.
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
        /// Gate-ul de sanitate. Cauza tipica e "am deschis NEF-ul in Siril si am dat
        /// Save", care debayerizeaza - deci se spune exact asta, nu doar ca fisierul nu e
        /// bun.
        /// </summary>
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
            Verdict = "";
            Advice = "";
            IsRawFlat = true;
            // HistogramGuide.IsExpanded ramane cum l-a lasat omul: e o preferinta de
            // vizualizare, nu un rezultat - la fel ca la ghidul hartii.
        }
    }
}
