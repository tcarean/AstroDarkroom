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

// SmoothScaleAdvisor.cs - propune scara de netezire. PROPUNE, nu decide: ce e al
// obiectivului si ce e al sursei nu se vede din pixeli, deci omul apasa butonul.
//
// Scara trebuie sa stea intre marimea a ce trebuie scos (celulele difuzorului) si marimea
// celei mai mici structuri reale de pastrat. Amandoua se citesc din master:
//
//   1. Textura intrarii (Metrics.FineTexture pe rasterul de analiza) spune daca exista
//      ceva de netezit. Sub Th.SmoothTextureMin raspunsul e "nimic de netezit", fara
//      nicio rulare: netezirea unui master curat scoate doar praful, si aia e o decizie
//      despre folosirea flat-ului, nu despre flat.
//   2. Altfel, cate o rulare pe fiecare treapta a scarii (cele trei chipuri din fereastra:
//      jumatate, implicit, dublu), ~4-5 s fiecare pe 24 MP, si doua cifre pe treapta:
//      textura ramasa in fisier (sub Th.SmoothTextureGone = a plecat) si cat a SCAZUT
//      bateria de structura - dar numai asimetria azimutala si inclinarea, singurele doua
//      teste pe care textura nu le atinge (colturile, reziduul si pata maxima scad tocmai
//      cand pleaca textura, deci nu pot fi garda). O scadere peste Th.SmoothScaleLoss
//      inseamna ca a plecat structura reala: scara e prea mare.
//   3. Cea mai mica treapta la care textura a plecat si garda tace. Daca nu exista una,
//      NEDETERMINAT - textura si structura reala au aceeasi marime - cum raspunde seria
//      --light cand cerul si flat-ul nu se despart.
//
// Masurat (2026-09-16, docs/design/smooth-validation.md): pe masterul cu difuzorul pe monitor
// propune 300 px (la 150 raman 0.09 % textura, la 300 raman 0.02 % si garda e la 0.00 /
// -0.01); pe celelalte patru "nimic de netezit". La 600 px garda prinde inelul real al
// Nikkor-ului (asimetria -0.79) si petele monitorului pe Tokina (-0.80), pe care cifra
// scoasa la scara mare nu le prindea pe amandoua - de aceea garda e bateria.
//
// Ce NU stie, si spune in raport: ca punctul cald al unui LCD e al sursei; ca o structura
// reala doar in colturi, in afara cercului inscris si care nu e un plan, scapa garzilor.

using System;
using System.Collections.Generic;
using System.Linq;

namespace FlatCheck
{
    internal enum SmoothAdviceKind
    {
        /// <summary>Intrarea n-are textura peste prag: nu e nimic de netezit.</summary>
        NoTexture,
        /// <summary>O treapta a scos textura fara sa ia structura: ScalePx.</summary>
        Proposed,
        /// <summary>Exista textura, dar orice treapta care o scoate ia si structura reala.</summary>
        Undetermined,
    }

    /// <summary>O treapta incercata: ce a ramas, ce a plecat, si de ce a trecut sau nu.</summary>
    internal sealed class SmoothScaleTrial
    {
        internal double ScalePx;
        internal double TextureAfterPct;
        /// <summary>Dupa minus inainte, in puncte; negativ = a scazut.</summary>
        internal double AzimuthalDelta, TiltDelta, CornerDelta;
        internal double RemovedPtpPct, RemovedMaxTilePct;
        internal bool TextureGone;
        internal bool StructureLost;
        internal bool Acceptable => TextureGone && !StructureLost;
    }

    internal sealed class SmoothScaleAdvice
    {
        internal SmoothAdviceKind Kind;
        internal double InputTexturePct;
        /// <summary>Scara propusa; NaN cand Kind nu e Proposed.</summary>
        internal double ScalePx = double.NaN;
        /// <summary>Gol pe NoTexture; altfel o intrare pe treapta, in ordinea scarii.</summary>
        internal IReadOnlyList<SmoothScaleTrial> Trials = Array.Empty<SmoothScaleTrial>();
    }

    internal static class SmoothScaleAdvisor
    {
        /// <summary>Treptele: cele trei chipuri din fereastra, ca sa nu existe o a doua lista de numere.</summary>
        internal static IReadOnlyList<double> Ladder => new[]
        {
            SmoothAnalyzer.DefaultScalePx / 2, SmoothAnalyzer.DefaultScalePx, SmoothAnalyzer.DefaultScalePx * 2,
        };

        /// <param name="ladder">Treptele de incercat, crescator; implicit <see cref="Ladder"/>. Testele dau trepte pe masura fixture-urilor lor.</param>
        internal static SmoothScaleAdvice Advise(Img input, IReadOnlyList<double> ladder = null)
        {
            double texture = Metrics.FineTexture(Img.Prepare(input));
            if (texture < Th.SmoothTextureMin)
                return new SmoothScaleAdvice { Kind = SmoothAdviceKind.NoTexture, InputTexturePct = texture };

            var trials = new List<SmoothScaleTrial>();
            foreach (double scale in ladder ?? Ladder)
            {
                // Rezultatul se arunca dupa ce s-au citit cifrele: rasterul netezit e 190 MB
                // pe 24 MP, si trei deodata n-ar avea ce cauta in memorie.
                SmoothResult r = SmoothAnalyzer.Analyze(input, scale);
                trials.Add(Trial(r));
            }
            return Decide(texture, trials);
        }

        /// <summary>Cifrele unei trepte, dintr-un rezultat de netezire. Pur; testele o folosesc direct.</summary>
        internal static SmoothScaleTrial Trial(SmoothResult r)
        {
            double az = r.After.AzimuthalMaxPct - r.Before.AzimuthalMaxPct;
            double tilt = r.After.TiltPct - r.Before.TiltPct;
            return new SmoothScaleTrial
            {
                ScalePx = r.ScalePx,
                TextureAfterPct = r.OutputTexturePct,
                AzimuthalDelta = az,
                TiltDelta = tilt,
                CornerDelta = r.After.CornerSpreadPct - r.Before.CornerSpreadPct,
                RemovedPtpPct = r.RemovedPtpPct,
                RemovedMaxTilePct = r.RemovedMaxTilePct,
                TextureGone = r.OutputTexturePct < Th.SmoothTextureGone,
                // !(x > -prag), ca NaN sa cada pe partea prudenta - conventia Stats.Verdict
                StructureLost = !(az > -Th.SmoothScaleLoss) || !(tilt > -Th.SmoothScaleLoss),
            };
        }

        /// <summary>Cea mai mica treapta acceptabila, sau NEDETERMINAT. Pur.</summary>
        internal static SmoothScaleAdvice Decide(double inputTexture, IReadOnlyList<SmoothScaleTrial> trials)
        {
            SmoothScaleTrial pick = trials.FirstOrDefault(t => t.Acceptable);
            return new SmoothScaleAdvice
            {
                Kind = pick != null ? SmoothAdviceKind.Proposed : SmoothAdviceKind.Undetermined,
                InputTexturePct = inputTexture,
                ScalePx = pick?.ScalePx ?? double.NaN,
                Trials = trials,
            };
        }
    }
}
