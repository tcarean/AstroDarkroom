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

// Raster.cs - imaginea in memorie: 1 sau 3 planuri de float, valori 0..1.
//
// DE CE FLOAT SI NU DOUBLE: imaginea tipica are 23 MP pe 3 canale. In double ar fi 554
// MB doar pixelii; in float, 277. Precizia float (24 de biti de mantisa) acopera cu
// rezerva cele 16 ale sursei - round(v * 65535) intoarce exact nivelul din fisier, si
// pe asta se sprijina masuratoarea de biti reali.
//
// DE CE PLANURI SEPARATE SI NU INTRETESUT: aproape toate masuratorile lucreaza pe un
// singur canal (verdele, sau mono) sau pe fiecare canal in parte; un plan contiguu se
// parcurge fara stride si se subesantioneaza cu o singura bucla.
//
// Rasterul nu stie de unde a venit - de asta e SourceInfo, un record separat pe care
// cititorul il completeaza. Analizoarele primesc un Raster si atat.

namespace AstroPhotoAnalyzer
{
    internal sealed class Raster
    {
        internal readonly int H, W;

        /// <summary>1 plan (mono) sau 3 (R, G, B). Fiecare e H*W, row-major, randul 0 SUS.</summary>
        internal readonly float[][] Planes;

        /// <summary>Ce spune fisierul despre el. Null pe un raster construit in memorie (teste).</summary>
        internal SourceInfo Source;

        internal Raster(int h, int w, int planes)
        {
            if (planes != 1 && planes != 3)
                throw new System.ArgumentException("un raster are 1 sau 3 planuri, nu " + planes);
            H = h; W = w;
            Planes = new float[planes][];
            for (int p = 0; p < planes; p++) Planes[p] = new float[h * w];
        }

        internal bool IsColor => Planes.Length == 3;

        internal float At(int plane, int y, int x) => Planes[plane][y * W + x];
        internal void Set(int plane, int y, int x, float v) => Planes[plane][y * W + x] = v;

        /// <summary>
        /// Canalul pe care se fac masuratorile mono-canal: verdele pe color (cel mai
        /// aproape de luminanta si cel cu cei mai multi fotoni pe un senzor Bayer), sau
        /// singurul plan pe mono. Acelasi canal pe care s-au masurat cifrele de acceptanta.
        /// </summary>
        internal float[] Reference => IsColor ? Planes[1] : Planes[0];
    }

    /// <summary>
    /// Ce stie fisierul despre el insusi. Campurile care lipsesc nu sunt erori, sunt
    /// raspunsul corect pentru formatul respectiv: un FITS n-are compresie si n-are
    /// profil ICC, un PNG n-are BITPIX.
    /// </summary>
    internal sealed record SourceInfo
    {
        internal string Path { get; init; }

        /// <summary>"TIFF", "PNG", "JPEG", "FITS".</summary>
        internal string Format { get; init; }

        /// <summary>Bitii pe esantion din FISIER: 8, 16 sau 32. Pe FITS float, 32 cu IsFloat.</summary>
        internal int BitDepth { get; init; }

        /// <summary>FITS cu BITPIX negativ: valori reale, fara "niveluri" de numarat.</summary>
        internal bool IsFloat { get; init; }

        /// <summary>Planurile din fisier (1 sau 3); rasterul le pastreaza pe toate.</summary>
        internal int PlanesInFile { get; init; }

        /// <summary>Compresia, cum o numeste formatul ("LZW", "Deflate", "necomprimat"); null unde nu exista notiunea.</summary>
        internal string Compression { get; init; }

        /// <summary>Profilul ICC incorporat, brut; null daca fisierul n-are.</summary>
        internal byte[] Icc { get; init; }

        /// <summary>Numele din tagul 'desc' al profilului; null fara profil sau cu profil fara nume.</summary>
        internal string IccDescription { get; init; }

        internal long FileBytes { get; init; }

        /// <summary>
        /// WCS-ul din antetul FITS (R-018), sau cel imprumutat prin Parameters.WcsPath (R-027);
        /// null cand fisierul n-are unul: atunci praful si cerul se masoara fara catalog.
        /// </summary>
        internal Wcs Wcs { get; init; }

        /// <summary>
        /// O observatie a cititorului pe care raportul o arata ca atare: de exemplu ca
        /// valorile float au fost taiate la [0, 1], sau ca randurile au fost rasturnate
        /// dupa ROWORDER. Null cand n-a fost nimic de spus.
        /// </summary>
        internal string Note { get; init; }
    }
}
