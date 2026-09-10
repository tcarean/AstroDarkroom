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

// ImageLoader.cs - SINGURUL loc care decide daca un fisier e FITS sau raw de aparat.
//
// Asta e toata diferenta dintre cele doua formate, si e important sa ramana asa: de aici
// in jos nimic nu mai stie de unde au venit pixelii. Analizoarele primesc un Img,
// rapoartele primesc un rezultat, iar --expo primeste aceeasi scala indiferent daca a
// citit-o din NEF sau a ghicit-o din FITS. Daca decizia asta se mai imprastie o data prin
// cod, urmatoarea diferenta de comportament intre formate va aparea tacut.
//
// Oglindeste load_image_with_header din flatcheck.py, inclusiv alegerea de a NU valida
// extensia: orice altceva decat cele sase extensii raw merge la cititorul FITS si esueaza
// acolo ca eroare de parsare (vezi "Known issues" in CLAUDE.md).

using System.Collections.Generic;

namespace FlatCheck
{
    /// <summary>
    /// Un cadru incarcat, cu tot ce poate spune fisierul despre el. Campurile care lipsesc
    /// nu sunt erori, sunt raspunsul corect pentru formatul respectiv: un raw n-are header
    /// FITS, iar un FITS n-are scala ADU scrisa de aparat.
    /// </summary>
    internal sealed class LoadedFrame
    {
        internal Img Image;

        /// <summary>Cardurile FITS; GOL (nu null) pentru un raw - vezi LightSuitability.</summary>
        internal IReadOnlyDictionary<string, string> Header;

        /// <summary>Scala ADU exacta din fisier, sau null daca fisierul n-o poarta.</summary>
        internal ExposureScale Scale;

        /// <summary>
        /// Ordinea culorilor in mozaicul CFA, sau null cand fisierul n-o spune. Acelasi
        /// principiu ca la <see cref="Scale"/>: null e raspunsul corect pentru un fisier
        /// care tace, nu o eroare - ordinea difera de la aparat la aparat si nu se ghiceste.
        /// </summary>
        internal CfaPattern Cfa;

        /// <summary>
        /// Previzualizarea inglobata, NEDECODATA, sau null cand formatul n-o poarta (FITS,
        /// mereu). Se copiaza cat timp fisierul e oricum deschis - cativa MB cel mult - si
        /// se decodeaza numai daca cineva o cere; vezi ExposureRun si PreviewReader.
        /// </summary>
        internal EmbeddedPreview Preview;
    }

    internal static class ImageLoader
    {
        // Cititoarele, cate o instanta fara stare, create o data la prima incarcare.
        // ImageLoader nu cunoaste API-ul niciuneia - doar contractul.
        static readonly IFrameReader Fits = new FitsReader();
        static readonly IFrameReader[] Readers = { new RawReader(), Fits };

        internal static LoadedFrame Load(string path)
        {
            foreach (IFrameReader r in Readers)
                if (r.Handles(path)) return r.Load(path);

            // Ce nu revendica nimeni merge tot la FITS. E o decizie, nu o scapare: un .jpg
            // dat din greseala esueaza cu o eroare de parsare FITS, care arata tot spre
            // fisierul vinovat, in loc de "format nerecunoscut" (vezi Known issues in
            // CLAUDE.md). Sta explicit aici, nu ca ultim element din lista, ca o
            // reordonare a listei sa nu-l poata rupe tacut.
            return Fits.Load(path);
        }

        /// <summary>Cand nu-ti trebuie decat pixelii - structura, --ratio, seria de --light.</summary>
        internal static Img LoadImage(string path) => Load(path).Image;
    }
}
