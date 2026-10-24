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

// Metrics.cs - geometria si metricile pure. Fiecare functie e o transformare
// deterministica a rasterului; niciuna nu scrie nimic si niciuna nu are stare.
//
// ATENTIE la centrul folosit de apelanti: modelul radial si asimetria se calculeaza
// in jurul centrului SENZORULUI, nu al varfului fitat de FitCenter. Vezi
// docs/design/pipeline.md, "The core idea" (ADR 0013) - centrarea pe varf ar absorbi
// exact gradientul cautat.

using System;
using System.Collections.Generic;
using System.Linq;

namespace FlatCheck
{
    internal static class Metrics
    {
        // ========================================================= geometrie
        /// <summary>
        /// Fitul unui paraboloid pe log(I). Din acelasi fit ies doua lucruri diferite:
        ///
        /// - <b>varful</b> (vertexul), adica centrul de iluminare. E o POZITIE, si de
        ///   aceea NU e scale-invariant: vertexul e ~ -b/(2d), un raport intre gradient
        ///   si curbura. Pe un obiectiv aproape fara vignetare (d mic) o rampa modesta
        ///   il arunca oricat de departe; iar cand ajunge sa iasa din cadru, garda de
        ///   mai jos cade pe centrul senzorului. Deci metrica e si NEMONOTONA: peste un
        ///   prag, cu cat defectul e mai mare, cu atat offsetul raportat e mai mic, pana
        ///   la 0. Masurat: cu acelasi gradient de 11%, offsetul creste de la 2.4% la
        ///   2 stopuri de vignetare pana la 57% la 0.08 stopuri, apoi cade la 0 la 0.05.
        ///   De asta offsetul e raportat doar informativ, si nu mai da verdict.
        ///
        /// - <b>inclinarea</b> (<c>tiltPct</c>), adica amplitudinea
        ///   varf-la-varf a termenului LINIAR peste cadru. E numaratorul de mai sus luat
        ///   singur: o fractie, deci scale-invarianta ca restul metricilor si direct
        ///   comparabila cu scara de reziduu. Un camp radial genuin decentrat produce si
        ///   el un termen liniar (~ offset x curbura), deci inclinarea prinde si
        ///   decentrarea reala - dar ponderata cu cat costa ea fotometric, ceea ce e
        ///   exact ponderarea care lipsea offsetului.
        ///
        /// Termenul incrucisat f (astigmatism / sa) e lasat deliberat in afara
        /// inclinarii: nu e o inclinare, iar reziduul dupa modelul radial il prinde
        /// oricum. Amestecat in aceeasi cifra, ar face-o neinterpretabila.
        /// </summary>
        internal static (double cx, double cy, double tiltPct, bool vertexValid) FitCenter(Img img)
        {
            int h = img.H, w = img.W;
            // normal equations pentru A=[1,xn,yn,xn^2,yn^2,xn*yn], 6 necunoscute
            int step = Math.Max(1, Math.Min(h, w) / 200);
            var AtA = new double[6, 6];
            var Atb = new double[6];
            for (int y = 0; y < h; y += step)
                for (int x = 0; x < w; x += step)
                {
                    double xn = (x - w / 2.0) / (w / 2.0);
                    double yn = (y - h / 2.0) / (h / 2.0);
                    double val = Math.Log(Math.Max(img.At(y, x), 1e-12));
                    double[] r = { 1, xn, yn, xn * xn, yn * yn, xn * yn };
                    for (int i = 0; i < 6; i++)
                    {
                        Atb[i] += r[i] * val;
                        for (int j = 0; j < 6; j++) AtA[i, j] += r[i] * r[j];
                    }
                }
            double[] coef = Stats.Solve(AtA, Atb, 6);
            // Sistemul normal depinde doar de coordonate, nu de valori, deci e singular
            // doar pe un raster degenerat. Atunci nici inclinarea nu se poate calcula:
            // NaN, care cade in banda cea mai proasta, ca peste tot in tool.
            if (coef == null) return (w / 2.0, h / 2.0, double.NaN, false);
            double b = coef[1], c = coef[2], d = coef[3], e = coef[4], f = coef[5];
            // Amplitudinea varf-la-varf a termenului liniar peste cadru. |b| + |c| e
            // valoarea lui in colt (unde xn = yn = 1), iar sinh face conversia exacta
            // din log in intensitate, in loc de aproximarea liniara 2*(|b| + |c|).
            double tilt = 2 * Math.Sinh(Math.Abs(b) + Math.Abs(c)) * 100;
            // M = [[2d, f],[f, 2e]]; sol = M^-1 * [-b,-c]
            double det = 2 * d * 2 * e - f * f;
            if (Math.Abs(det) < 1e-15) return (w / 2.0, h / 2.0, tilt, false);
            double sx = ((-b) * (2 * e) - f * (-c)) / det;
            double sy = ((2 * d) * (-c) - f * (-b)) / det;
            double cx = sx * (w / 2.0) + w / 2.0;
            double cy = sy * (h / 2.0) + h / 2.0;
            if (!(cx > 0 && cx < w && cy > 0 && cy < h)) return (w / 2.0, h / 2.0, tilt, false);
            return (cx, cy, tilt, true);
        }

        // Profil radial robust + model 2D reconstruit prin interpolare.
        //
        // In cercul inscris profilul e mediana pe inel, ca dintotdeauna: exacta pe un inel
        // intreg si imuna la ce e compact. Dincolo de el un inel e doar arce partiale, la
        // azimuturi fixe (stanga si dreapta, apoi doar colturile), si orice statistica pe
        // arce mosteneste dependenta de azimut a campului. Img.Symmetrise scoate exact partea
        // IMPARA (gradientul) inainte; partea PARA neradiala - o sa (xy) sau o cadere mai
        // abrupta pe o axa decat pe cealalta (x^2 - y^2) - trece prin simetrizare neatinsa,
        // si mediana pe arcele din stanga/dreapta o absorbea in profil: panta modelului sarea
        // de la 0.21 la 0.71 % per bin chiar pe cercul inscris, iar harta desena o creasta pe
        // el si doi lobi cu margini dure. Masurat pe masterul de cer al Tokinei 20 (15 + 15
        // cadre rotite la 180 de grade; saua cerului, -6 % la colturi, e para si rotatia nu o
        // poate anula) si reprodus pe un sintetic neted cu aceiasi coeficienti: eroarea hartii
        // fata de reziduul adevarat, in afara cercului, 8.9 % cu mediana si 0.5 % cu corectia;
        // cea mai abrupta treapta intre dale de 8 px 3.58 -> 0.23 %.
        //
        // CORECTIA, pe inelele partiale: profilul e mediana lui
        //     v / (1 + r^2 (q2c cos 2t + q2s sin 2t + q4c cos 4t + q4s sin 4t)),
        // cu q amplitudinile RELATIVE per r^2 (raport, ca orice metrica din tool: o sa a
        // cerului inmulteste, nu aduna, si creste cu r^2). q se estimeaza pe fiecare inel
        // identificabil prin RingFit - pe MEDIANELE sectoarelor de 15 grade, cele mai mici
        // patrate cu taiere la 2.5 MAD - si se ia O SINGURA mediana peste inelele de la
        // RingQFrom * rIns in afara, intregi si partiale: un q constant pe r^2, deci o corectie
        // neteda in r, care nu poate desena inele; in colturi, unde arcele nu mai pot separa
        // termenii de constanta (criteriu pur geometric, RingFitMinDet pe determinantul Gram
        // al bazei, la varianta unui inel intreg), acelasi q. Inelele intregi intra fiindca pe
        // ele armonicile sunt exact identificabile: numai din arce, impartirea intre 2t si 4t
        // e prost conditionata si extrapolarea spre azimuturile neobservate a pus +1.3 puncte
        // pe dala maxima a masterului Nikkor ISO400 fara motiv (2.9 -> 4.2); ancorate, 3.2.
        // a0 ramane o mediana pe pixeli peste tot, deci zgomotul si robustetea profilului sunt
        // cele de dinainte.
        //
        // De ce si 4t (2026-09-23): pe arce de +-30..60 de grade in jurul axei x, cos 2t si
        // cos 4t sunt aproape coliniare, si un fit numai cu 2t citea un termen de 4t ca 2t.
        // Un master din patru orientari rotite la 90 de grade are, prin constructie, NICIUN
        // 2t al lumii si NUMAI familia 4t (petele lumii inmuiate de patru ori): corectia
        // veche nu avea ce corecta si injecta q2c de +2..+4 % cu semnul sarind de la un inel
        // la altul (LMedS alegea alte triplete), profilul primea o treapta de -1.7 % chiar pe
        // cercul inscris si benzi de +-1 % dincolo, iar harta desena un inel rosu si un cerc
        // dur pe un master ale carui pete sunt netede. Masurat pe sintetice cu adevar
        // cunoscut (eroare model/adevar, rms): sa 2t de 6 % - mediana 0.76 %, 2t 0.03, 2t+4t
        // 0.03; termen 4t de 3 % - mediana 0.17, 2t 1.87 (trepte 2.6 %), 2t+4t 0.03; sa + 4t,
        // gradient, pata compacta, pata + sa, pata + 4t: 2t+4t sub 0.13 %. Pe masterele
        // reale, treapta maxima intre inele a mediei reziduului: master rotit 0.73 -> 0.17 %,
        // monitor 1.01 -> 0.29, cer 0.31 -> 0.31; un singur stack de orientare citea 17.9 %
        // reziduu (continut neradial adevarat 7.6 %) si citeste 7.9. LMedS nu mai e necesar:
        // taierea la 2.5 MAD in trei runde a tinut pata compacta de +10 % afara din estimare
        // (0.08 % eroare). Vezi docs/design/radial-model-even-term.md, sectiunea 7.
        //
        // Ce a fost incercat si respins, in ordine:
        //  - a0 direct din fitul pe inel (LS cu taiere la 3 MAD, apoi L1): o pata compacta de
        //    10 % intr-un colt, simetrizata in doua jumatati antipodale, E un semnal 2t pe
        //    inelele ei, si fitul o absorbea cu a0 mutat cu 2.3 % - un inel fals in harta.
        //  - un fit global patratic al partii pare (astigmatism + sa) impartit inainte de
        //    mediana: aliaza termenii radiali de ordin mai mare intr-un astigmatism fals
        //    (0.3 % pe un camp perfect radial pe 3:2) si muta dala maxima a Nikkor 800 cu
        //    1.3 puncte fara motiv.
        //  - amplitudini absolute tinute de la ultimul inel identificabil: pe o sa
        //    multiplicativa gresesc cu factorul vignetarii (1.4 % in colt), si o pata din
        //    ultimul inel se propaga in toate cele patru colturi.
        //  - termenul 4t cu amplitudinea extrapolata in colturi: exploda (dala maxima
        //    9.8 -> 28 %). Ca amplitudine relativa pe r^2, constanta pe toate inelele, nu.
        //  - q netezit cu o mediana glisanta pe 7 inele (versiunea 2.2): pe masterele cu pete
        //    lasa trepte de 0.7-1 % intre inele; o singura mediana peste inele nu.
        internal const double RingFitMinDet = 0.02;   // sub atat baza 2t+4t nu se mai poate separa pe arcele unui inel (det. Gram, la varianta unui inel intreg)
        internal const int RingSectors = 24;          // sectoare de 15 grade; fitul pe inel se face pe medianele lor
        internal const int RingMinSectors = 6;        // sub atatea sectoare populate inelul nu se estimeaza
        internal const int RingFitRounds = 3;         // runde de cele mai mici patrate cu taiere la 2.5 MAD
        internal const double RingQFrom = 0.75;       // q e mediana peste inelele de la atata din raza inscrisa in afara, intregi si partiale
        internal const double RingFitTie = 1e-9;      // toleranta (relativ la nivel) la alegerea inlierilor, pt scene fara zgomot

        internal static (double[] centers, double[] prof, Img model) RadialModel(Img img, double cx, double cy, int nbins = 80)
        {
            int h = img.H, w = img.W;
            double rmax = 0;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    double r = Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    if (r > rmax) rmax = r;
                }
            var binVals = new List<double>[nbins];
            var binCos = new List<double>[nbins];
            var binSin = new List<double>[nbins];
            var binSector = new List<int>[nbins];
            for (int i = 0; i < nbins; i++)
            {
                binVals[i] = new List<double>();
                binCos[i] = new List<double>();
                binSin[i] = new List<double>();
                binSector[i] = new List<int>();
            }
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    double r = Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    int idx = Math.Min((int)(r / rmax * nbins), nbins - 1);
                    double t = Math.Atan2(y - cy, x - cx);
                    binVals[idx].Add(img.At(y, x));
                    binCos[idx].Add(Math.Cos(2 * t));
                    binSin[idx].Add(Math.Sin(2 * t));
                    binSector[idx].Add(Math.Min((int)((t + Math.PI) / (2 * Math.PI) * RingSectors), RingSectors - 1));
                }
            double rIns = Math.Min(cx, cy);
            var ringBins = new List<int>();
            for (int i = 0; i < nbins; i++)
                if (binVals[i].Count >= 8) ringBins.Add(i);
            // amplitudinile relative per r^2 pe inelele identificabile de la RingQFrom * rIns in
            // afara - intregi (unde armonicile sunt exact identificabile si ancoreaza estimarea)
            // si partiale - apoi mediana lor
            var qRows = new List<double>[4];
            for (int j = 0; j < 4; j++) qRows[j] = new List<double>();
            foreach (int i in ringBins)
            {
                double center = (i + 0.5) / nbins * rmax;
                if (center < RingQFrom * rIns) continue;
                var fit = RingFit(binVals[i], binCos[i], binSin[i], binSector[i]);
                if (fit == null) continue;
                for (int j = 0; j < 4; j++) qRows[j].Add(fit[j + 1] / fit[0] / (center * center));
            }
            double[] q = null;
            if (qRows[0].Count > 0)
            {
                q = new double[4];
                for (int j = 0; j < 4; j++) q[j] = Stats.Percentile(qRows[j], 50);
            }
            var centers = new List<double>();
            var prof = new List<double>();
            foreach (int i in ringBins)
            {
                double center = (i + 0.5) / nbins * rmax;
                centers.Add(center);
                if (center < rIns || q == null)
                {
                    prof.Add(Stats.Percentile(binVals[i], 50));
                    continue;
                }
                var v = binVals[i]; var c = binCos[i]; var s = binSin[i];
                double r2 = center * center;
                var corrected = new List<double>(v.Count);
                for (int p = 0; p < v.Count; p++)
                {
                    double c4 = 2 * c[p] * c[p] - 1, s4 = 2 * s[p] * c[p];
                    corrected.Add(v[p] / (1 + r2 * (q[0] * c[p] + q[1] * s[p] + q[2] * c4 + q[3] * s4)));
                }
                prof.Add(Stats.Percentile(corrected, 50));
            }
            var ca = centers.ToArray();
            var pa = prof.ToArray();
            var model = new Img(h, w);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    double r = Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    model.Set(y, x, Stats.Interp(r, ca, pa));
                }
            return (ca, pa, model);
        }

        /// <summary>
        /// Un inel identificabil: (a0, a2, b2, a4, b4) ale lui
        /// a0 + a2 cos 2t + b2 sin 2t + a4 cos 4t + b4 sin 4t peste medianele sectoarelor,
        /// prin cele mai mici patrate ponderate (ponderea = pixelii sectorului), RingFitRounds
        /// runde, intre runde sectoarele cu reziduul peste 2.5 MAD fiind scoase - o pata
        /// compacta care, simetrizata, ocupa 2-4 sectoare din 24, iese la a doua runda. Null
        /// cand baza nu e identificabila pe arce (determinantul Gram al celor patru coloane
        /// neconstante, la varianta unui inel intreg, sub RingFitMinDet - geometrie pura, in
        /// colturi) sau cand
        /// sunt sub RingMinSectors sectoare populate. Pereche numerica: <c>_ring_fit</c> din
        /// flatcheck.py.
        /// </summary>
        static double[] RingFit(List<double> v, List<double> c, List<double> s, List<int> sector)
        {
            int n = v.Count;
            var med = new List<double>(); var rows = new List<double[]>(); var wt = new List<double>();
            for (int k = 0; k < RingSectors; k++)
            {
                var vals = new List<double>();
                double sumC = 0, sumS = 0, sumC4 = 0, sumS4 = 0;
                for (int i = 0; i < n; i++)
                    if (sector[i] == k)
                    {
                        vals.Add(v[i]);
                        sumC += c[i]; sumS += s[i];
                        sumC4 += 2 * c[i] * c[i] - 1; sumS4 += 2 * s[i] * c[i];
                    }
                if (vals.Count < 8) continue;
                int cnt = vals.Count;
                med.Add(Stats.Percentile(vals, 50));
                rows.Add(new[] { 1.0, sumC / cnt, sumS / cnt, sumC4 / cnt, sumS4 / cnt });
                wt.Add(cnt);
            }
            int m = med.Count;
            if (m < RingMinSectors) return null;

            // identificabilitate: determinantul matricei Gram a coloanelor neconstante centrate,
            // normalizata la 1/2 - varianta unei armonice pe un inel intreg - deci 1 pe un inel
            // intreg si spre 0 pe arce inguste. NU prin corelatie: pe arcele din colturile unui
            // cadru patrat corelatia ar da 1 oricat de inguste ar fi, cu un fit fara sens.
            var mean = new double[4];
            for (int j = 0; j < 4; j++)
            {
                for (int p = 0; p < m; p++) mean[j] += rows[p][j + 1];
                mean[j] /= m;
            }
            var gram = new double[4, 4];
            for (int a = 0; a < 4; a++)
                for (int b = 0; b < 4; b++)
                {
                    double sum = 0;
                    for (int p = 0; p < m; p++) sum += (rows[p][a + 1] - mean[a]) * (rows[p][b + 1] - mean[b]);
                    gram[a, b] = sum / m / 0.5;
                }
            double det = Determinant(gram, 4);
            if (!(det >= RingFitMinDet)) return null;

            var inlier = new bool[m];
            for (int p = 0; p < m; p++) inlier[p] = true;
            double[] sol = null;
            var res = new double[m];
            for (int round = 0; round < RingFitRounds; round++)
            {
                var A = new double[5, 5]; var b = new double[5];
                for (int p = 0; p < m; p++)
                {
                    if (!inlier[p]) continue;
                    for (int a = 0; a < 5; a++)
                    {
                        b[a] += wt[p] * rows[p][a] * med[p];
                        for (int bb = 0; bb < 5; bb++) A[a, bb] += wt[p] * rows[p][a] * rows[p][bb];
                    }
                }
                sol = Stats.Solve(A, b, 5);
                if (sol == null) return null;
                for (int p = 0; p < m; p++)
                {
                    double e = med[p];
                    for (int a = 0; a < 5; a++) e -= rows[p][a] * sol[a];
                    res[p] = e;
                }
                double centre = Stats.Percentile((double[])res.Clone(), 50);
                var dev = new double[m];
                for (int p = 0; p < m; p++) dev[p] = Math.Abs(res[p] - centre);
                double mad = Stats.Percentile(dev, 50) * 1.4826;
                int inliers = 0;
                for (int p = 0; p < m; p++)
                {
                    inlier[p] = Math.Abs(res[p]) <= 2.5 * mad + RingFitTie * Math.Abs(sol[0]);
                    if (inlier[p]) inliers++;
                }
                if (inliers < RingMinSectors)
                    for (int p = 0; p < m; p++) inlier[p] = true;
            }
            return sol;
        }

        /// <summary>Determinantul unei matrice n x n, prin eliminare Gauss cu pivotare partiala.</summary>
        static double Determinant(double[,] src, int n)
        {
            var a = (double[,])src.Clone();
            double det = 1;
            for (int col = 0; col < n; col++)
            {
                int piv = col;
                for (int r = col + 1; r < n; r++)
                    if (Math.Abs(a[r, col]) > Math.Abs(a[piv, col])) piv = r;
                if (a[piv, col] == 0) return 0;
                if (piv != col)
                {
                    for (int k = 0; k < n; k++) { double tmp = a[col, k]; a[col, k] = a[piv, k]; a[piv, k] = tmp; }
                    det = -det;
                }
                det *= a[col, col];
                for (int r = col + 1; r < n; r++)
                {
                    double f = a[r, col] / a[col, col];
                    for (int k = col; k < n; k++) a[r, k] -= f * a[col, k];
                }
            }
            return det;
        }

        // ========================================================= metrici
        /// <summary>
        /// Cata TEXTURA FINA are un raster: partea structurata a componentei fine, in
        /// procente. Componenta fina e <c>raster / neted - 1</c>, unde "neted" e rasterul
        /// trecut de doua ori printr-un filtru cutie de <c>box</c> px (o cutie
        /// aplicata de doua ori e un triunghi - destul de aproape de un gaussian pentru un
        /// DETECTOR, si fara nicio dependinta). Zgomotul de pixel se estimeaza din
        /// diferentele intre vecini orizontali (impartite la radical din 2) si se scade in
        /// cuadratura, ca sa ramana doar ce e corelat spatial: textura difuzorului, nu
        /// granulatia. Bordura de <c>inset</c> px nu intra in statistica, fiindca
        /// acolo cutia e trunchiata.
        ///
        /// Exista pentru propunerea de scara a modului de netezire: pe rasterul de analiza
        /// al unui senzor de 6016 px (1504 px, 4 px de senzor pe pixel) cutia e de 32 px,
        /// adica 128 px pe senzor, iar celulele de ~300 px ale unui difuzor pus pe monitor
        /// ies la 0.69 % fata de 0.04-0.06 % pe un master fara textura. Masurat pe cinci
        /// mastere reale; pragurile in Th.SmoothTexture*.
        ///
        /// Cutia si bordura sunt RELATIVE la latimea rasterului (1/47 si 1/15 din ea), nu
        /// fixe in pixeli: cu 32 px ficsi pe un fixture de 256 px cutia era o optime din
        /// cadru, curbura vignetarii se scurgea in "textura" cu 0.14 % si nimic nu mai
        /// trecea de niciun prag. Pe rasterul real cele doua definitii coincid (32 si 100).
        ///
        /// NU prin BlockMean marit inapoi: grila de blocuri isi pune propriile margini in
        /// componenta fina (masurat: perioade de 32 px care erau blocurile, nu textura).
        /// </summary>
        internal static double FineTexture(Img a)
        {
            int box = Math.Max(4, (int)Math.Round(a.W / 47.0));
            int inset = Math.Max(box, (int)(a.W / 15.0));
            Img smooth = BoxBlur(BoxBlur(a, box), box);
            int iy = Math.Min(inset, a.H / 4), ix = Math.Min(inset, a.W / 4);
            double sum = 0, sumSq = 0, dSumSq = 0;
            long n = 0, dn = 0;
            for (int y = iy; y < a.H - iy; y++)
            {
                double prev = double.NaN;
                for (int x = ix; x < a.W - ix; x++)
                {
                    double s = smooth.At(y, x);
                    double f = s > 0 ? a.At(y, x) / s - 1 : 0;
                    sum += f; sumSq += f * f; n++;
                    if (!double.IsNaN(prev)) { double d = f - prev; dSumSq += d * d; dn++; }
                    prev = f;
                }
            }
            if (n < 2) return 0;
            double mean = sum / n;
            double var = sumSq / n - mean * mean;
            double noiseVar = dn > 0 ? dSumSq / dn / 2 : 0;
            return Math.Sqrt(Math.Max(var - noiseVar, 0)) * 100;
        }

        /// <summary>
        /// Media pe o cutie de <paramref name="box"/> px centrata pe fiecare pixel, prin sume
        /// glisante pe randuri si apoi pe coloane; la margini cutia se trunchiaza si se
        /// imparte la cati pixeli a acoperit. Separabil, deci O(pixeli), oricat de mare e cutia.
        /// </summary>
        internal static Img BoxBlur(Img a, int box)
        {
            int r = box / 2;
            var tmp = new Img(a.H, a.W);
            for (int y = 0; y < a.H; y++)
            {
                double s = 0; int cnt = 0;
                for (int x = 0; x < Math.Min(a.W, r); x++) { s += a.At(y, x); cnt++; }
                for (int x = 0; x < a.W; x++)
                {
                    int add = x + r, drop = x - r - 1;
                    if (add < a.W) { s += a.At(y, add); cnt++; }
                    if (drop >= 0) { s -= a.At(y, drop); cnt--; }
                    tmp.Set(y, x, s / cnt);
                }
            }
            var o = new Img(a.H, a.W);
            var col = new double[a.H];
            for (int x = 0; x < a.W; x++)
            {
                double s = 0; int cnt = 0;
                for (int y = 0; y < Math.Min(a.H, r); y++) { s += tmp.At(y, x); cnt++; }
                for (int y = 0; y < a.H; y++)
                {
                    int add = y + r, drop = y - r - 1;
                    if (add < a.H) { s += tmp.At(add, x); cnt++; }
                    if (drop >= 0) { s -= tmp.At(drop, x); cnt--; }
                    o.Set(y, x, s / cnt);
                }
            }
            return o;
        }

        internal static (Dictionary<string, double> vals, double ctr) CornerStats(Img img, double frac = 0.05)
        {
            int h = img.H, w = img.W;
            int bh = Math.Max(4, (int)(h * frac)), bw = Math.Max(4, (int)(w * frac));
            double Med(int y0, int y1, int x0, int x1)
            {
                var l = new List<double>();
                for (int y = y0; y < y1; y++) for (int x = x0; x < x1; x++) l.Add(img.At(y, x));
                return Stats.Percentile(l, 50);
            }
            var vals = new Dictionary<string, double>
            {
                ["stanga-sus"] = Med(0, bh, 0, bw),
                ["dreapta-sus"] = Med(0, bh, w - bw, w),
                ["stanga-jos"] = Med(h - bh, h, 0, bw),
                ["dreapta-jos"] = Med(h - bh, h, w - bw, w),
            };
            double ctr = Med(h / 2 - bh, h / 2 + bh, w / 2 - bw, w / 2 + bw);
            return (vals, ctr);
        }

        // Ptp intre sectoare, per inel. Doar in raza inscrisa (toate azimuturile esantionate).
        internal static List<(double r, double ptp)> AzimuthalAsymmetry(Img img, double cx, double cy, int nsect = 12, int nrings = 10)
        {
            int h = img.H, w = img.W;
            double rIns = Math.Min(Math.Min(cx, cy), Math.Min(w - cx, h - cy));
            var edges = new double[nrings + 1];
            for (int i = 0; i <= nrings; i++) edges[i] = 0.15 * rIns + (rIns - 0.15 * rIns) * i / nrings;
            var buckets = new List<double>[nrings, nsect];
            for (int i = 0; i < nrings; i++) for (int s = 0; s < nsect; s++) buckets[i, s] = new List<double>();
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    double dx = x - cx, dy = y - cy;
                    double r = Math.Sqrt(dx * dx + dy * dy);
                    if (r < edges[0] || r >= edges[nrings]) continue;
                    double th = Math.Atan2(dy, dx);
                    int s = Math.Min((int)((th + Math.PI) / (2 * Math.PI) * nsect), nsect - 1);
                    int ri = -1;
                    for (int i = 0; i < nrings; i++) if (r >= edges[i] && r < edges[i + 1]) { ri = i; break; }
                    if (ri >= 0) buckets[ri, s].Add(img.At(y, x));
                }
            var outl = new List<(double, double)>();
            for (int i = 0; i < nrings; i++)
            {
                var meds = new List<double>();
                for (int s = 0; s < nsect; s++) if (buckets[i, s].Count >= 8) meds.Add(Stats.Percentile(buckets[i, s], 50));
                if (meds.Count >= nsect - 1)
                {
                    double mn = meds.Min(), mx = meds.Max(), md = Stats.Percentile(meds, 50);
                    outl.Add(((edges[i] + edges[i + 1]) / 2 / rIns, (mx - mn) / md * 100));
                }
            }
            return outl;
        }

        // Structura de scala mare (izolata de zgomot prin mediere pe tile-uri).
        /// <summary>
        /// Latura dalei folosita de metricile de scara mare. Scoasa separat fiindca o
        /// citesc doi consumatori - <see cref="LargeScalePtp"/> si modul de atribuire -
        /// iar doua formule ar fi divergat la prima ajustare a lui ntiles.
        /// </summary>
        internal static int TileSize(Img src, int ntiles = 40)
            => Math.Max(2, (int)(src.W / (double)ntiles));

        /// <summary>
        /// p95 - p5 al dalelor, pe TOT cadrul.
        ///
        /// Initial lua doar dalele din cercul inscris, fiindca dincolo de el
        /// profilul radial venea din arce partiale si nu era de incredere. Simetrizarea
        /// (vezi <see cref="Img.Symmetrise"/>) a rezolvat exact asta, deci restrictia
        /// ramasese fara motiv - si costa mult: pe un cadru 3:2 cercul inscris e 53% din
        /// dale, iar colturile, unde traiesc scurgerile de lumina si unde gradientul e
        /// cel mai mare, nu se vedeau deloc. Tot din cauza ei acelasi gradient citea
        /// 6.5% pe axa lunga si 9.7% pe cea scurta; pe tot cadrul citeste 10.4 / 10.6.
        /// Podeaua pe un camp radial curat a ramas sub 0.1% la rezolutia de analiza.
        /// </summary>
        internal static double LargeScalePtp(Img resid, int ntiles = 40)
        {
            int k = TileSize(resid, ntiles);
            Img t = Img.BlockMean(resid, k);
            if (t.D.Length < 10) return double.NaN;
            return Stats.Percentile(t.D, 95) - Stats.Percentile(t.D, 5);
        }

        /// <summary>
        /// Cea mai mare abatere a unei singure dale fata de model, in valoare absoluta,
        /// pe tot cadrul. Fractie, nu procent.
        ///
        /// Exista fiindca <see cref="LargeScalePtp"/> taie 5% din dale la fiecare capat,
        /// deci un defect COMPACT - o scurgere mica de lumina, o pata de condens - care
        /// acopera sub 5% din cadru dispare din el complet, oricat de adanc ar fi. Masurat:
        /// o pata de +10% cat un deget intr-un colt citea 0.04% la reziduu si verdict
        /// PERFECT, cu pata perfect vizibila in harta. Aici citeste 9.3%.
        ///
        /// E tot o limita superioara, ca celelalte: o umbra de praf adanca (diafragma
        /// inchisa) e fixa pe aparat si se imparte corect din lights-urile aceleiasi
        /// sesiuni, dar tot aici se vede. Pentru asta exista <see cref="LocalSpots"/> si
        /// atentionarea din raport.
        /// </summary>
        internal static double MaxTileDeviation(Img resid, int ntiles = 40)
        {
            Img t = Img.BlockMean(resid, TileSize(resid, ntiles));
            double worst = 0;
            foreach (double v in t.D) worst = Math.Max(worst, Math.Abs(v - 1.0));
            return worst;
        }

        /// <summary>
        /// Cea mai adanca pata COMPACTA: fiecare dala fina minus mediana vecinatatii ei,
        /// si se pastreaza cea mai mare in valoare absoluta, cu semn si cu pozitia (in
        /// pixelii rasterului de analiza). Fractie, nu procent.
        ///
        /// Scaderea medianei locale e ce o deosebeste de <see cref="MaxTileDeviation"/>:
        /// un gradient e neted, deci mediana vecinatatii il urmareste si diferenta iese
        /// ~0 (masurat: 0.46% la un gradient de 12%, 0.68% la 25%), pe cand o umbra de
        /// praf, un inel sau o scurgere mica raman intregi (4.99% pe o umbra de 5%, si
        /// tot 4.71% cand umbra sta sub un gradient de 12% cu zgomot). Raspunde deci la o
        /// intrebare pe care celelalte cifre n-o pot pune: E CEVA MIC SI FIX PE APARAT?
        /// Raspunsul conteaza altfel decat un verdict: o asemenea pata e corecta pentru
        /// sesiunea ale carei lights o au si ele, si gresita pentru un flat de biblioteca.
        /// De aceea nu voteaza - da o atentionare.
        ///
        /// Dalele sunt de doua ori mai fine decat la celelalte doua metrici (80 pe
        /// latime, ~18 px la rezolutia de analiza), ca o umbra de praf de 60 px sa nu se
        /// dilueze; vecinatatea e 7x7 dale, adica ~250 px pe senzor, mai lata decat orice
        /// pata de praf de pe un DSLR.
        ///
        /// Se evalueaza NUMAI dalele cu vecinatatea intreaga, deci fara o bordura de
        /// <paramref name="win"/> dale (~55 px la rezolutia de analiza, ~110 pe senzor).
        /// La margine vecinatatea e taiata pe o parte, mediana ei nu mai urmareste un
        /// gradient, si diferenta iese din gradient, nu dintr-o pata: masurat pe 256 px,
        /// -0.76% in colt pe un camp CURAT si -1.61% sub un gradient de 60% - la un pas de
        /// atentionarea de 2%, pentru un praf care nu exista. O pata chiar pe margine o
        /// prinde oricum <see cref="MaxTileDeviation"/>.
        ///
        /// Podeaua: sub 0.1% pe camp curat, 1.1% la 10% zgomot pe pixelul senzorului;
        /// masterele reale au 0.2-0.3% zgomot. Pe un master real de Nikkor 35 la f/2.8
        /// umbra de praf vizibila in harta are -0.31%, deci sub orice prag - cum spune si
        /// documentatia ca la deschideri mari praful e sub prag.
        /// </summary>
        internal static (double dev, int x, int y) LocalSpots(Img resid, int ntiles = 80, int win = 3)
        {
            int k = TileSize(resid, ntiles);
            Img t = Img.BlockMean(resid, k);
            double worst = 0; int wx = win, wy = win;
            var nb = new List<double>((2 * win + 1) * (2 * win + 1));
            for (int y = win; y < t.H - win; y++)
                for (int x = win; x < t.W - win; x++)
                {
                    nb.Clear();
                    for (int dy = -win; dy <= win; dy++)
                        for (int dx = -win; dx <= win; dx++)
                            nb.Add(t.At(y + dy, x + dx));
                    double hp = t.At(y, x) - Stats.Percentile(nb, 50);
                    // Toleranta la egalitate: pe o scena simetrica doua dale pot iesi egale pana
                    // la ultimul bit, iar C# si numpy aduna in alta ordine, deci ar alege alta
                    // dala. Sub 1e-12 ramane prima in ordinea randurilor, in amandoua.
                    if (Math.Abs(hp) > Math.Abs(worst) + 1e-12) { worst = hp; wx = x; wy = y; }
                }
            return (worst, (int)((wx + 0.5) * k), (int)((wy + 0.5) * k));
        }

        // Fond local per tile (percentila mica = robust la stele/nebulozitate).
        internal static (double[] bg, double[] bx, double[] by, int th, int tw, int k) TileBackground(Img img, int ntiles = 40, double pct = 15)
        {
            int h = img.H, w = img.W;
            int k = Math.Max(4, (int)(w / (double)ntiles));
            int hh = h / k, ww = w / k;
            var bg = new double[hh * ww];
            var bx = new double[hh * ww];
            var by = new double[hh * ww];
            for (int ty = 0; ty < hh; ty++)
                for (int tx = 0; tx < ww; tx++)
                {
                    var vals = new List<double>();
                    for (int dy = 0; dy < k; dy++)
                        for (int dx = 0; dx < k; dx++)
                            vals.Add(img.At(ty * k + dy, tx * k + dx));
                    int idx = ty * ww + tx;
                    bg[idx] = Stats.Percentile(vals, pct);
                    bx[idx] = (tx + 0.5) * k;
                    by[idx] = (ty + 0.5) * k;
                }
            return (bg, bx, by, hh, ww, k);
        }
    }
}
