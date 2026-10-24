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

// ExposureAnalyzer.cs - modul 4: e flat-ul BRUT expus corect?
//
// Ruleaza pe un SINGUR flat brut necalibrat, cu mozaicul CFA intact si scala ADU
// originala. Metrica de decizie e mediana unei ferestre CENTRALE de 20%x20% din canalul
// cel mai luminos, NU mediana globala: un flat are prin definitie o distributie larga
// (asta E vignetarea), iar mediana globala e trasa in jos de colturi si te-ar pacali sa
// supraexpui pana clipeaza centrul.
//
// Pe langa cifra care decide, modul strange si DISTRIBUTIA fiecarui canal - vezi
// LevelHistogram mai jos. O mediana spune unde sta un canal; nu spune daca varful mai are
// loc pana la saturatie, cat de departe cad colturile sub centru, sau daca o coada atinge
// deja albul. Histogramele nu voteaza si nu schimba niciun prag: sunt aceleasi date, vazute.

using System;
using System.Collections.Generic;
using System.Linq;

namespace FlatCheck
{
    internal enum ExposureVerdict
    {
        NotRaw,          // gate-ul de sanitate a taiat verdictul
        TooLow,
        AcceptableLow,   // doar pe panou; sky flat n-are banda asta
        Ok,
        AcceptableHigh,
        TooHigh,
    }

    /// <summary>
    /// Scala ADU citita DIN fisier, cand fisierul o stie: un raw de aparat poarta white
    /// si black exacte in metadate. Pe FITS nu exista asa ceva - stacking-ul si
    /// conversia le pierd - deci acolo scala ramane ghicita din maxim (vezi AutoWhite).
    /// </summary>
    internal sealed class ExposureScale
    {
        internal readonly double White, Black;
        internal readonly string Source;

        internal ExposureScale(double white, double black, string source)
        {
            White = white; Black = black; Source = source;
        }
    }

    /// <summary>
    /// Distributia unui canal pe axa PROCENTUALA a verdictului: 200 de cosuri de 0.5 %
    /// intre 0 si 100 % din (white - black).
    ///
    /// Axa e aleasa dinadins sa fie aceeasi pe care stau si cifrele, si pragurile din
    /// <see cref="ExposureBands"/>: asa benzile devin direct intervale pe desen, iar doua
    /// fisiere sau doua canale se pot compara fara nicio conversie. Nivelurile de sub
    /// black si cele la sau peste white se string in cosul de la capat, deci o coada
    /// saturata se vede ca masa in ultimul cos in loc sa dispara din grafic.
    /// </summary>
    internal sealed class LevelHistogram
    {
        internal const int Bins = 200;

        internal readonly int[] Counts = new int[Bins];
        internal int Total;

        /// <summary>Cosul unui nivel; sub black -> primul, la sau peste white -> ultimul.</summary>
        internal static int BinOf(double pct)
        {
            int b = (int)(pct * Bins / 100.0);
            if (b < 0) return 0;
            return b >= Bins ? Bins - 1 : b;
        }

        internal void Add(double pct)
        {
            Counts[BinOf(pct)]++;
            Total++;
        }
    }

    /// <summary>
    /// Un canal cu tot ce se poate spune despre nivelul lui. Pe un cadru CFA sunt patru,
    /// cate unul pe pozitie Bayer; pe unul mono/procesat e unul singur, pe tot cadrul.
    /// Toate procentele sunt din (white - black), ca restul modului.
    /// </summary>
    internal sealed class ChannelLevels
    {
        /// <summary>0..3 = CFA[y,x] in ordinea (y&amp;1)*2 + (x&amp;1); 0 pe mono.</summary>
        internal int Position;

        /// <summary>"R", "G1", "G2", "B" cand se stie ordinea; altfel "CFA[0,1]". "mono" pe mono.</summary>
        internal string Label;

        /// <summary>'R', 'G' sau 'B'; '\0' cand fisierul n-a spus ordinea.</summary>
        internal char Colour;

        /// <summary>Mediana canalului INTREG - trasa in jos de colturi, deci informativa.</summary>
        internal double MedianPct;

        /// <summary>Coada de sus a canalului intreg si cat din el e deja saturat.</summary>
        internal double P99Pct, ClipPct;

        /// <summary>Aceeasi fereastra centrala de 20 % ca la decizie, dar pe canalul asta.</summary>
        internal double CentreMedianPct, CentreP1Pct, CentreP99Pct;

        internal bool IsBrightest;

        internal LevelHistogram Whole, Centre;
    }

    /// <summary>
    /// Nivelurile din previzualizarea aparatului: trei canale, fara mozaic si fara
    /// verdict. Aceleasi cosuri ca la raw, ca cele doua sa se poata citi una langa alta.
    /// </summary>
    internal sealed class PreviewLevels
    {
        /// <summary>Exact 3: R, G, B, in ordinea asta.</summary>
        internal ChannelLevels[] Channels;

        internal string Source;
    }

    internal sealed class ExposureResult
    {
        internal double White, Black, Range;
        internal string ScaleSource;            // "--sat=..." sau "auto-detectat din max=..."
        internal double[] ChannelMedians;       // 4 pozitii Bayer, in ADU brut
        internal double CfaSpread;              // max/min peste cele 4
        internal bool IsCfa;
        internal int BrightestChannel;
        internal double CentreMedianPct;        // ASTA decide expunerea
        internal double GlobalMedianPct;        // informativ
        internal double CentreClipPct;
        internal bool LevelTooLow;              // < 1% -> calibrat/normalizat
        internal bool LooksDebayered;           // spread CFA < 1.02
        internal bool IsSky;
        internal ExposureVerdict Verdict;

        /// <summary>Ordinea culorilor citita din fisier, sau null cand fisierul n-o spune.</summary>
        internal CfaPattern Cfa;

        /// <summary>4 canale pe un cadru CFA, 1 pe unul mono. Niciodata null.</summary>
        internal ChannelLevels[] Channels;

        /// <summary>
        /// Benzile pe care s-a dat verdictul. Stau in rezultat, nu se realeg de apelant:
        /// fereastra deseneaza exact banda care a votat.
        /// </summary>
        internal ExposureBands Bands;

        /// <summary>
        /// Ce a aratat aparatul pe ecran, sau null cand fisierul n-a purtat o
        /// previzualizare sau nimeni n-a cerut-o. Nu intra in niciun verdict.
        /// </summary>
        internal PreviewLevels Preview;

        internal bool IsRaw => !(LevelTooLow || LooksDebayered);

        /// <summary>
        /// Numele unei pozitii Bayer: culoarea cand fisierul a spus ordinea, pozitia cand
        /// nu. Nu se ghiceste - ordinea difera de la aparat la aparat.
        /// </summary>
        internal string ChannelLabel(int i) => Mosaic.Label(Cfa, i);
    }

    internal static class ExposureAnalyzer
    {
        /// <summary>Scala se ghiceste din maxim; daca greseste, --sat / --black.</summary>
        internal static double AutoWhite(double max)
        {
            if (max <= 1.5) return 1.0;
            if (max <= 4096 * 1.05) return 4095.0;
            if (max <= 16384 * 1.05) return 16383.0;
            return 65535.0;
        }

        /// <param name="data">The raw flat's pixels, in the file's own levels (ADU).</param>
        /// <param name="satOverride">The white level given with --sat, or null to take it from
        /// the file or guess it.</param>
        /// <param name="blackOverride">The black level given with --black, or null to take it
        /// from the file (0 when the file carries none).</param>
        /// <param name="sky">True for a sky flat, whose acceptable bands differ from a panel's.</param>
        /// <param name="fileScale">
        /// Scala citita din fisier, sau null daca fisierul n-o poarta (FITS). Ordinea de
        /// precedenta e cea din flatcheck.py: --sat bate fisierul, fisierul bate ghicitul.
        /// </param>
        /// <param name="cfa">
        /// Ordinea culorilor citita din fisier, sau null cand fisierul n-o spune. Nu
        /// schimba nicio cifra - doar cum se numesc canalele in raport si in fereastra.
        /// </param>
        /// <param name="preview">
        /// Previzualizarea aparatului, decodata, sau null. Produce doar
        /// <see cref="ExposureResult.Preview"/>; nimic din verdict nu o atinge.
        /// </param>
        internal static ExposureResult Analyze(Img data, double? satOverride, double? blackOverride,
                                               bool sky, ExposureScale fileScale = null,
                                               CfaPattern cfa = null, PreviewImage preview = null)
        {
            double mx = data.D.Max();
            double white;
            string src;
            if (satOverride.HasValue) { white = satOverride.Value; src = $"--sat={white}"; }
            else if (fileScale != null) { white = fileScale.White; src = fileScale.Source; }
            else
            {
                white = AutoWhite(mx);
                src = $"auto-detectat din max={mx:F0} (foloseste --sat/--black daca e gresit)";
            }
            double black = blackOverride ?? fileScale?.Black ?? 0.0;
            double rng = white - black;

            // cele 4 pozitii Bayer. Canalul cel mai luminos clipeaza primul, deci e cel limitant.
            // Histogramele se umplu in ACEEASI trecere: pixelii sunt oricum vizitati o data.
            var ch = new List<double>[4];
            var wholeHist = new LevelHistogram[4];
            for (int i = 0; i < 4; i++) { ch[i] = new List<double>(); wholeHist[i] = new LevelHistogram(); }
            for (int y = 0; y < data.H; y++)
                for (int x = 0; x < data.W; x++)
                {
                    int c = Mosaic.PositionOf(y, x);
                    double v = data.At(y, x);
                    ch[c].Add(v);
                    wholeHist[c].Add((v - black) / rng * 100);
                }
            var meds = ch.Select(c => Stats.Percentile(c, 50)).ToArray();
            double mmax = meds.Max(), mmin = meds.Min();
            double cfaSpread = mmax / Math.Max(mmin, 1e-9);
            bool isCfa = Mosaic.LooksCfa(cfaSpread);
            int bright = Array.IndexOf(meds, mmax);

            // fereastra centrala 20%x20%, pe FIECARE pozitie Bayer refacuta ca sub-imagine.
            // Decizia ramane a canalului celui mai luminos - cenVals de mai jos e chiar
            // lista lui - dar celelalte trei se string fiindca histograma le arata pe toate,
            // si trei ferestre de 4 % din cadru nu costa nimic.
            int chH = data.H / 2, chW = data.W / 2;
            int cy0 = (int)(chH * 0.4), cy1 = (int)(chH * 0.6);
            int cx0 = (int)(chW * 0.4), cx1 = (int)(chW * 0.6);
            var cenPer = new List<double>[4];
            var centreHist = new LevelHistogram[4];
            for (int i = 0; i < 4; i++)
            {
                cenPer[i] = new List<double>();
                centreHist[i] = new LevelHistogram();
                int oy = i / 2, ox = i % 2;
                for (int y = cy0; y < cy1; y++)
                    for (int x = cx0; x < cx1; x++)
                    {
                        double v = data.At(y * 2 + oy, x * 2 + ox);
                        cenPer[i].Add(v);
                        centreHist[i].Add((v - black) / rng * 100);
                    }
            }
            var cenVals = cenPer[bright];
            double cenMed = (Stats.Percentile(cenVals, 50) - black) / rng * 100;
            double globPct = (mmax - black) / rng * 100;
            double cenClip = cenVals.Count(v => v >= 0.98 * white) / (double)cenVals.Count * 100;

            if (!isCfa)
            {
                // mono/procesat: centrul se ia pe intreaga imagine, nu pe sub-mozaic.
                // Lista noua, nu cenVals.Clear(): cenVals arata spre cenPer[bright], iar
                // golirea ei ar sterge tacut datele unui canal.
                cenVals = new List<double>();
                int my0 = (int)(data.H * 0.4), my1 = (int)(data.H * 0.6);
                int mx0 = (int)(data.W * 0.4), mx1 = (int)(data.W * 0.6);
                for (int y = my0; y < my1; y++)
                    for (int x = mx0; x < mx1; x++)
                        cenVals.Add(data.At(y, x));
                cenMed = (Stats.Percentile(cenVals, 50) - black) / rng * 100;
                globPct = (Stats.Percentile(data.D, 50) - black) / rng * 100;
                cenClip = cenVals.Count(v => v >= 0.98 * white) / (double)cenVals.Count * 100;
            }

            var r = new ExposureResult
            {
                White = white,
                Black = black,
                Range = rng,
                ScaleSource = src,
                ChannelMedians = meds,
                CfaSpread = cfaSpread,
                IsCfa = isCfa,
                BrightestChannel = bright,
                CentreMedianPct = cenMed,
                GlobalMedianPct = globPct,
                CentreClipPct = cenClip,
                LevelTooLow = cenMed < 1.0,
                LooksDebayered = cfaSpread < 1.02,
                IsSky = sky,
                Cfa = cfa,
                Bands = ExposureBands.For(sky),
            };
            r.Channels = isCfa
                ? CfaChannels(r, ch, wholeHist, cenPer, centreHist)
                : MonoChannel(r, data, cenVals);
            r.Preview = preview != null ? AnalyzePreview(preview) : null;
            r.Verdict = Classify(r);
            return r;
        }

        /// <summary>
        /// Histogramele previzualizarii, pe axa 0..100 % din 255. NU primeste praguri si NU
        /// produce verdict: ce se vede aici a trecut prin balans de alb, gama si curba de
        /// ton, deci nu mai are nicio legatura cu cata lumina a cazut pe senzor. Exista ca
        /// sa fie pusa langa nivelul real - diferenta e cea care deruteaza.
        ///
        /// Fereastra centrala e aceeasi fractie ca la raw, 0.4..0.6 pe ambele axe. E
        /// invarianta la orientarile pe care le poate avea o previzualizare - o rotatie de
        /// 180 de grade o duce in ea insasi, una de 90 schimba W cu H dar nu fractiile - si
        /// nu-i pasa ca previzualizarea e cu cativa pixeli mai ingusta decat zona vizibila
        /// a raw-ului (6000 fata de 6016 pe D3300). Amandoua arata ca niste bug-uri cui nu
        /// le-a masurat; nu sunt.
        /// </summary>
        internal static PreviewLevels AnalyzePreview(PreviewImage p)
        {
            const double White = 255.0;
            int cy0 = (int)(p.H * 0.4), cy1 = (int)(p.H * 0.6);
            int cx0 = (int)(p.W * 0.4), cx1 = (int)(p.W * 0.6);

            var channels = new ChannelLevels[3];
            double bestMedian = double.NegativeInfinity;
            int best = 0;

            for (int c = 0; c < 3; c++)
            {
                var whole = new LevelHistogram();
                var centre = new LevelHistogram();
                var all = new double[p.W * p.H];
                var mid = new List<double>(Math.Max(0, (cy1 - cy0) * (cx1 - cx0)));
                int clipped = 0;

                for (int y = 0; y < p.H; y++)
                    for (int x = 0; x < p.W; x++)
                    {
                        double v = p.At(y, x, c);
                        double pct = v / White * 100;
                        all[y * p.W + x] = v;
                        whole.Add(pct);
                        if (v >= 0.98 * White) clipped++;
                        if (y >= cy0 && y < cy1 && x >= cx0 && x < cx1)
                        {
                            mid.Add(v);
                            centre.Add(pct);
                        }
                    }

                double[] sortedAll = Stats.Sorted(all);
                double[] sortedMid = Stats.Sorted(mid.ToArray());
                double median = Stats.OfSorted(sortedAll, 50) / White * 100;

                channels[c] = new ChannelLevels
                {
                    Position = c,
                    Label = "RGB"[c].ToString(),
                    Colour = "RGB"[c],
                    MedianPct = median,
                    P99Pct = Stats.OfSorted(sortedAll, 99) / White * 100,
                    ClipPct = all.Length == 0 ? 0 : clipped / (double)all.Length * 100,
                    CentreMedianPct = Stats.OfSorted(sortedMid, 50) / White * 100,
                    CentreP1Pct = Stats.OfSorted(sortedMid, 1) / White * 100,
                    CentreP99Pct = Stats.OfSorted(sortedMid, 99) / White * 100,
                    Whole = whole,
                    Centre = centre,
                };

                if (median > bestMedian) { bestMedian = median; best = c; }
            }

            // "Cel mai luminos" doar ca sa fie consecvent cu randurile raw; pe partea asta
            // nu decide nimic, si fereastra nu-l scoate in evidenta.
            channels[best].IsBrightest = true;

            return new PreviewLevels { Channels = channels, Source = p.Source };
        }

        /// <summary>Un procent din (white - black), scara pe care sta tot modul.</summary>
        static double Pct(ExposureResult r, double adu) => (adu - r.Black) / r.Range * 100;

        static double ClipShare(IReadOnlyList<double> vals, double white)
        {
            if (vals.Count == 0) return 0;
            int n = 0;
            for (int i = 0; i < vals.Count; i++) if (vals[i] >= 0.98 * white) n++;
            return n / (double)vals.Count * 100;
        }

        static ChannelLevels[] CfaChannels(ExposureResult r, List<double>[] ch,
            LevelHistogram[] whole, List<double>[] cen, LevelHistogram[] centre)
        {
            var o = new ChannelLevels[4];
            for (int i = 0; i < 4; i++)
            {
                // O singura sortare per set, apoi trei percentile din ea.
                double[] all = Stats.Sorted(ch[i].ToArray());
                double[] mid = Stats.Sorted(cen[i].ToArray());

                o[i] = new ChannelLevels
                {
                    Position = i,
                    Label = r.ChannelLabel(i),
                    Colour = r.Cfa != null ? r.Cfa.Colours[i] : '\0',
                    MedianPct = Pct(r, r.ChannelMedians[i]),
                    P99Pct = Pct(r, Stats.OfSorted(all, 99)),
                    ClipPct = ClipShare(ch[i], r.White),
                    CentreMedianPct = Pct(r, Stats.OfSorted(mid, 50)),
                    CentreP1Pct = Pct(r, Stats.OfSorted(mid, 1)),
                    CentreP99Pct = Pct(r, Stats.OfSorted(mid, 99)),
                    IsBrightest = i == r.BrightestChannel,
                    Whole = whole[i],
                    Centre = centre[i],
                };
            }
            return o;
        }

        /// <summary>
        /// Cadru mono sau deja procesat: un singur canal, pe tot cadrul. Nu se numeste cu
        /// o culoare, fiindca nu mai exista mozaic din care sa se citeasca una.
        /// </summary>
        static ChannelLevels[] MonoChannel(ExposureResult r, Img data, List<double> cen)
        {
            var whole = new LevelHistogram();
            foreach (double v in data.D) whole.Add(Pct(r, v));

            var centre = new LevelHistogram();
            foreach (double v in cen) centre.Add(Pct(r, v));

            double[] all = Stats.Sorted(data.D);
            double[] mid = Stats.Sorted(cen.ToArray());

            return new[]
            {
                new ChannelLevels
                {
                    Position = 0,
                    Label = "mono",
                    Colour = '\0',
                    MedianPct = r.GlobalMedianPct,
                    P99Pct = Pct(r, Stats.OfSorted(all, 99)),
                    ClipPct = ClipShare(data.D, r.White),
                    CentreMedianPct = r.CentreMedianPct,
                    CentreP1Pct = Pct(r, Stats.OfSorted(mid, 1)),
                    CentreP99Pct = Pct(r, Stats.OfSorted(mid, 99)),
                    IsBrightest = true,
                    Whole = whole,
                    Centre = centre,
                },
            };
        }

        /// <summary>
        /// O singura scara, citita din <see cref="ExposureBands"/>. Ramura separata pentru
        /// cer nu mai e nevoie: acolo tinta incepe chiar la pragul de prea-jos
        /// (TooLow == Lo), deci banda "acceptabil, dar cam jos" iese goala de la sine - un
        /// sky flat TREBUIE sa stea modest, fiindca cerul se stinge continuu si e plin de
        /// stele slabe, iar SNR-ul masterului vine din NUMARUL de cadre.
        /// </summary>
        static ExposureVerdict Classify(ExposureResult r)
        {
            if (!r.IsRaw) return ExposureVerdict.NotRaw;

            ExposureBands b = r.Bands;
            bool clipping = r.CentreClipPct > Th.ExpoClip;
            double cen = r.CentreMedianPct;

            if (clipping || cen > b.TooHigh) return ExposureVerdict.TooHigh;
            if (cen < b.TooLow) return ExposureVerdict.TooLow;
            if (cen < b.Lo) return ExposureVerdict.AcceptableLow;
            if (cen <= b.Hi) return ExposureVerdict.Ok;
            return ExposureVerdict.AcceptableHigh;
        }
    }
}
