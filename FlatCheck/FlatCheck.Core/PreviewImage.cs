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

// PreviewImage.cs - previzualizarea developata de aparat, in cele doua forme ale ei.
//
// Un raw poarta in el imaginea pe care aparatul a aratat-o pe ecran: debayerizata, cu
// balans de alb, cu gama si cu curba de ton din Picture Control. Aia e imaginea din care
// aparatul si-a calculat histograma - si diferenta fata de nivelul real al senzorului e
// de zeci de puncte, pe un flat mai mare decat oriunde. Tipurile de aici exista ca cele
// doua sa poata fi puse una langa alta.
//
// NICIUNA din ele nu are voie sa decida ceva. Previzualizarea nu e o masuratoare a
// senzorului; e o ilustratie a ce a vazut omul. Verdictul de expunere ramane pe raw.
//
// Doua forme, fiindca au costuri diferite: EmbeddedPreview e ce se copiaza cat timp
// fisierul e oricum deschis (octeti comprimati, ieftin), PreviewImage e ce iese dupa
// decodare (pixeli, scump) - si decodarea se face numai la cerere, vezi ExposureRun.

namespace FlatCheck
{
    /// <summary>
    /// Previzualizarea asa cum sta in fisier: ori octeti JPEG, ori un bitmap RGB pe care
    /// libraw l-a decodat deja. Se captureaza cat timp fisierul e deschis si se decodeaza
    /// numai la cerere.
    /// </summary>
    internal sealed class EmbeddedPreview
    {
        internal readonly bool IsJpeg;

        /// <summary>JPEG comprimat, sau RGB pe 8 biti intretesut cand <see cref="IsJpeg"/> e false.</summary>
        internal readonly byte[] Data;

        /// <summary>Din metadate; pentru JPEG, dimensiunea anuntata inainte de decodare.</summary>
        internal readonly int W, H;

        /// <summary>Orientarea raportata de libraw. Informativa: fereastra centrala nu depinde de ea.</summary>
        internal readonly int Flip;

        /// <summary>Pentru raport: "previzualizare JPEG 1620x1080 din fisier".</summary>
        internal readonly string Source;

        internal EmbeddedPreview(bool isJpeg, byte[] data, int w, int h, int flip, string source)
        {
            IsJpeg = isJpeg; Data = data; W = w; H = h; Flip = flip; Source = source;
        }
    }

    /// <summary>
    /// Previzualizarea ca pixeli. Un tip al nucleului fara nimic tert in el, ca nimic din
    /// aval sa nu stie de unde au venit pixelii - aceeasi disciplina ca la <see cref="Img"/>.
    /// </summary>
    internal sealed class PreviewImage
    {
        internal readonly int H, W;

        /// <summary>H*W*3, intretesut R,G,B, 0..255.</summary>
        internal readonly byte[] Rgb;

        internal readonly string Source;

        internal PreviewImage(int h, int w, byte[] rgb, string source)
        {
            H = h; W = w; Rgb = rgb; Source = source;
        }

        internal byte At(int y, int x, int c) => Rgb[(y * W + x) * 3 + c];
    }
}
