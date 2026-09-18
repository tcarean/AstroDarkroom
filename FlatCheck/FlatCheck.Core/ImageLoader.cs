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

        /// <summary>
        /// Aceleasi carduri, dar BRUTE - cele 80 de caractere ale fiecaruia, in ordinea din
        /// fisier, fara END. Gol pentru un raw. Exista pentru modul de netezire, care scrie
        /// un al doilea master si vrea sa-i dea headerul intrarii neatins: dictionarul de
        /// mai sus pierde comentariile si taie sirurile la primul '/', deci nu poate fi
        /// sursa unui header scris inapoi.
        /// </summary>
        internal IReadOnlyList<string> Cards = System.Array.Empty<string>();

        /// <summary>NAXIS3 al HDU-ului citit; 1 pentru o imagine 2D si pentru un raw.</summary>
        internal int Planes = 1;

        /// <summary>BITPIX al HDU-ului citit; 0 pentru un raw, care n-are asa ceva.</summary>
        internal int Bitpix;

        /// <summary>Scala ADU exacta din fisier, sau null daca fisierul n-o poarta.</summary>
        internal ExposureScale Scale;

        /// <summary>
        /// Ce stie fisierul despre fotografiere, pentru flatul sintetic: focala (FOCALLEN
        /// pe FITS, libraw pe raw), diafragma (doar raw - Siril nu scrie f-numarul),
        /// numele aparatului (INSTRUME / libraw) si ID-ul de obiectiv Nikon (doar raw; e
        /// ce poarta si profilele Adobe in LensID / AlternateLensIDs). Null cand fisierul
        /// tace - acelasi principiu ca la Scale.
        /// </summary>
        internal double? FocalMm;
        internal double? FNumber;
        internal string Camera;
        internal long? NikonLensId;

        /// <summary>
        /// Gama obiectivului din MakerNote-ul raw-ului, cum o decodeaza libraw: focala minima
        /// si maxima, si deschiderea maxima la focala minima. Un ID Nikon e purtat de mai
        /// multe obiective (139: Tokina 11-20 f/2.8, Nikkor 18-200 f/3.5-5.6, Sigma 18-35
        /// f/1.8), si gama e ce le desparte. Null cand libraw da zero.
        /// </summary>
        internal double? LensMinFocalMm;
        internal double? LensMaxFocalMm;
        internal double? LensMaxAperture;

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

        /// <summary>
        /// Cand nu-ti trebuie decat metadatele - identificarea obiectivului, preumplerea
        /// tabului Sintetic. Aceeasi dispecerizare ca <see cref="Load"/>, inclusiv caderea
        /// pe FITS, ca cele doua sa nu poata ajunge la cititoare diferite pentru acelasi
        /// fisier. <see cref="LoadedFrame.Image"/> e null.
        /// </summary>
        internal static LoadedFrame LoadInfo(string path)
        {
            foreach (IFrameReader r in Readers)
                if (r.Handles(path)) return r.LoadInfo(path);
            return Fits.LoadInfo(path);
        }
    }
}
