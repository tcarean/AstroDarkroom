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

namespace AstroPhotoAnalyzer.Tests;

/// <summary>
/// Scenele intregi pe care ruleaza lantul: cer + stele + zgomot, cu sau fara defecte.
/// Toate deterministe (samanta fixa), ca testele golden sa compare acelasi text de
/// fiecare data.
/// </summary>
internal static class Scenes
{
    /// <summary>
    /// Dmax-ul hartiei pentru care au fost construite scenele: baryta de 2.0 de dinainte de
    /// ADR 0028, cu negrul la L* 9.0. Un cer la 0.08 e sub negrul acela si deasupra celui de
    /// azi (2.4, L* 3.6), asa ca testele despre "fundal sub negrul hartiei" isi dau hartia
    /// explicit, prin <see cref="OnBuiltForPaper"/> sau --dmax 2.0, in loc sa atarne de implicit.
    /// </summary>
    internal const double BuiltForDmax = 2.0;

    /// <summary>Aceiasi parametri, pe hartia pentru care au fost construite scenele.</summary>
    internal static Parameters OnBuiltForPaper(Parameters? p = null)
        => (p ?? new Parameters()) with { DmaxOverride = BuiltForDmax };

    /// <summary>Stele de FWHM 3 pe o grila, la 55 px, peste orice raster.</summary>
    internal static Raster WithStars(Raster r, int count = 64, double fwhm = 3.0, float amplitude = 0.3f)
    {
        int placed = 0;
        for (int j = 0; j < 8 && placed < count; j++)
            for (int i = 0; i < 8 && placed < count; i++, placed++)
            {
                double cx = 40 + i * 55, cy = 40 + j * 55;
                if (cx < r.W - 20 && cy < r.H - 20) r = SyntheticRasters.AddStar(r, cx, cy, fwhm, amplitude);
            }
        return r;
    }

    /// <summary>
    /// Imaginea "cu probleme": cer prea jos pentru hartie (0.08), tenta verde de 2
    /// niveluri, colt de orizont cald in dreapta-jos, denoise apasat (0.3 LSB), si o
    /// nebuloasa in centru (gaussiana larga) ca tonurile sa fie continue - fara ea,
    /// testul de stres ar gasi goluri si in datele de 16 biti, cum nu se intampla pe o
    /// imagine reala. Toate recomandarile de pe imaginea reala, in mic.
    /// </summary>
    internal static Raster DarkSky()
    {
        const float sky = 0.08f;
        Raster r = SyntheticRasters.From(512, 512, 3, (x, y, p) =>
        {
            float v = p == 1 ? sky + 2f / 255 : sky;
            double d2 = (x - 256) * (x - 256) + (y - 256) * (y - 256);
            v += (float)(0.4 * Math.Exp(-d2 / (2 * 60.0 * 60.0)));
            // O banda de praf: tonurile coboara continuu pana spre 0.02, ca pe un cer
            // real - acolo musca cel mai tare un shadow-lift din 8 biti.
            double l2 = (x - 256) * (x - 256) + (y - 100) * (y - 100);
            v -= (float)(0.06 * Math.Exp(-l2 / (2 * 40.0 * 40.0)));
            if (y >= 435 && x >= 410) v += p == 0 ? 10f / 255 : p == 2 ? -8f / 255 : 0f;
            return v;
        });
        r = WithStars(r);
        r = SyntheticRasters.AddNoise(r, 0.3 / 255, 21);
        r.Source = new SourceInfo { Format = "memorie", BitDepth = 16, IsFloat = false, PlanesInFile = 3, Icc = new byte[1], IccDescription = "sRGB test" };
        return r;
    }

    /// <summary>Imaginea "curata": fundal print-safe (0.25), neutru, zgomot bun (1.0 LSB), profil sRGB.</summary>
    internal static Raster CleanSky()
    {
        Raster r = WithStars(SyntheticRasters.Flat(512, 512, 0.25f));
        r = SyntheticRasters.AddNoise(r, 1.0 / 255, 22);
        r.Source = new SourceInfo { Format = "memorie", BitDepth = 16, IsFloat = false, PlanesInFile = 3, Icc = new byte[1], IccDescription = "sRGB IEC61966-2.1" };
        return r;
    }

    /// <summary>Mono, 8 biti, fara profil.</summary>
    internal static Raster MonoSky()
    {
        Raster r = WithStars(SyntheticRasters.Flat(300, 300, 0.25f, planes: 1), count: 25);
        return SyntheticRasters.AddNoise(r, 1.0 / 255, 23);
    }
}
