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

// Stats.cs - mediana, percentile, medie, deviatie, si un generator de numere aleatoare
// cu samanta fixa.
//
// PERCENTILA E CEA A LUI NUMPY (interpolare liniara intre ranguri, metoda implicita
// 'linear'). Cifrele de acceptanta din docs/validation.md au fost masurate cu
// numpy, si o percentila definita altfel (nearest, lower) ar fi dat diferente mici pe
// care nimeni nu le-ar fi putut explica.
//
// GENERATORUL E AL NOSTRU, nu System.Random: dalele de zgomot se aleg aleator, si
// rezultatul trebuie sa fie acelasi pe orice runtime si in orice an. System.Random cu
// samanta e determinist doar in cadrul aceleiasi versiuni de .NET.

using System;

namespace AstroPhotoAnalyzer
{
    internal static class Stats
    {
        /// <summary>Percentila p (0..100) pe un vector DEJA SORTAT, ca np.percentile.</summary>
        internal static double PercentileSorted(float[] sorted, double p)
        {
            if (sorted.Length == 0) throw new ArgumentException("percentila pe un vector gol");
            if (sorted.Length == 1) return sorted[0];
            double rank = p / 100.0 * (sorted.Length - 1);
            int lo = (int)Math.Floor(rank);
            int hi = Math.Min(lo + 1, sorted.Length - 1);
            double frac = rank - lo;
            return sorted[lo] + (sorted[hi] - sorted[lo]) * frac;
        }

        /// <summary>Copiaza, sorteaza, si intoarce vectorul sortat - pentru cine vrea mai multe percentile.</summary>
        internal static float[] Sorted(float[] values)
        {
            var copy = (float[])values.Clone();
            Array.Sort(copy);
            return copy;
        }

        internal static double Median(float[] values) => PercentileSorted(Sorted(values), 50);

        /// <summary>
        /// Sigma robusta: 1.4826 x mediana abaterilor absolute de la mediana (MAD), egala cu
        /// deviatia standard pe o gaussiana si insensibila la coada. Da si mediana.
        /// </summary>
        internal static double RobustSigma(float[] values, out double median)
        {
            median = Median(values);
            double m = median;
            var dev = new float[values.Length];
            for (int i = 0; i < values.Length; i++) dev[i] = (float)Math.Abs(values[i] - m);
            return 1.4826 * Median(dev);
        }

        /// <summary>
        /// Pragul Otsu pe un vector DEJA SORTAT: valoarea la care impartirea in doua clase are
        /// varianta intre clase maxima; cele la sau sub prag sunt clasa de jos. NaN sub doua
        /// valori; cu toate valorile egale, cea mai mica (si nimic nu e peste ea).
        /// </summary>
        internal static double OtsuThreshold(float[] sorted)
        {
            if (sorted.Length < 2) return double.NaN;
            double total = 0;
            foreach (float v in sorted) total += v;
            double best = -1, threshold = sorted[0], sumBelow = 0;
            for (int i = 0; i < sorted.Length - 1; i++)
            {
                sumBelow += sorted[i];
                int nBelow = i + 1, nAbove = sorted.Length - nBelow;
                double meanBelow = sumBelow / nBelow, meanAbove = (total - sumBelow) / nAbove;
                double between = (double)nBelow * nAbove * (meanBelow - meanAbove) * (meanBelow - meanAbove);
                if (between > best) { best = between; threshold = sorted[i]; }
            }
            return threshold;
        }

        internal static double Mean(float[] values)
        {
            double s = 0;
            for (int i = 0; i < values.Length; i++) s += values[i];
            return s / values.Length;
        }

        /// <summary>Deviatia standard a populatiei (impartire la N), ca np.std implicit.</summary>
        internal static double StdDev(float[] values)
        {
            double m = Mean(values), s = 0;
            for (int i = 0; i < values.Length; i++) { double d = values[i] - m; s += d * d; }
            return Math.Sqrt(s / values.Length);
        }

        internal static float Min(float[] values)
        {
            float m = float.PositiveInfinity;
            for (int i = 0; i < values.Length; i++) if (values[i] < m) m = values[i];
            return m;
        }

        internal static float Max(float[] values)
        {
            float m = float.NegativeInfinity;
            for (int i = 0; i < values.Length; i++) if (values[i] > m) m = values[i];
            return m;
        }
    }

    /// <summary>
    /// xorshift64*, cu samanta fixa. Suficient de bun pentru a alege dale si a genera
    /// zgomot de test; important e ca da ACELEASI numere oriunde ruleaza.
    /// </summary>
    internal sealed class SeededRandom
    {
        ulong _s;

        internal SeededRandom(int seed)
        {
            // Samanta 0 ar bloca xorshift-ul pe zero; se amesteca cu o constanta.
            _s = 0x9E3779B97F4A7C15UL ^ (ulong)(uint)seed;
            if (_s == 0) _s = 1;
        }

        internal ulong NextUInt64()
        {
            _s ^= _s >> 12;
            _s ^= _s << 25;
            _s ^= _s >> 27;
            return _s * 0x2545F4914F6CDD1DUL;
        }

        /// <summary>Intreg uniform in [0, maxExclusive).</summary>
        internal int Next(int maxExclusive) => (int)(NextUInt64() % (ulong)maxExclusive);

        /// <summary>Real uniform in [0, 1).</summary>
        internal double NextDouble() => (NextUInt64() >> 11) * (1.0 / (1UL << 53));

        /// <summary>Normal standard, prin Box-Muller.</summary>
        internal double NextGaussian()
        {
            double u1 = 1.0 - NextDouble(), u2 = NextDouble();
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
        }
    }
}
