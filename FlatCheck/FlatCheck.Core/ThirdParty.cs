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

// ThirdParty.cs - componentele scrise de altii pe care le leaga unealta, ca valoare.
//
// DE CE SE AFISEAZA, NU DOAR SE LIVREAZA. Motivele de compatibilitate cu GPLv3 stau in
// THIRD-PARTY-NOTICES.md, dar acel fisier nu calatoreste garantat cu binarul: promisiunea
// uneltei e "un singur exe copiabil", si cine ia doar exe-ul nu primeste niciun fisier de
// notite. Iar LGPL-2.1 sectiunea 6 (LibRaw) cere explicit ca, daca programul afiseaza
// notite de copyright la rulare - si o face, pe linia a doua a fiecarui raport si in bara
// ferestrei - notita bibliotecii sa apara printre ele, cu o trimitere la licenta. Deci
// lista asta e ce arata fereastra "Despre", si singurul loc din care poate sa o arate.
//
// O DEPENDINTA NOUA INSEAMNA O INTRARE AICI, pe langa cea din THIRD-PARTY-NOTICES.md.
// ThirdPartyTests leaga lista de PackageReference-urile din FlatCheck.Core.csproj in
// ambele sensuri, si fiecare nume de un heading din fisierul de notite - un pachet
// adaugat fara intrare pica testul, nu ramane nescris pe ecran.
//
// Textul oglindeste tabelul din README si fisierul de notite DE MANA; niciun test nu
// compara formularile, doar prezenta.

using System.Collections.Generic;

namespace FlatCheck
{
    /// <summary>
    /// O componenta terta: cum se cheama, ce face aici, sub ce licenta e luata, a cui e,
    /// unde e proiectul ei, si care pachete NuGet din csproj o aduc.
    /// </summary>
    internal sealed record ThirdPartyComponent(
        string Name,
        string Role,
        string Licence,
        string Copyright,
        string Url,
        string[] Packages)
    {
        /// <summary>
        /// O componenta de DATE, nu de cod: nu vine printr-un pachet NuGet, ci ca fisiere
        /// livrate langa exe (baza lensfun). Lista de pachete e goala si testul care leaga
        /// componentele de csproj o stie.
        /// </summary>
        internal bool IsData => Packages.Length == 0;
    }

    internal static class ThirdParty
    {
        internal static readonly IReadOnlyList<ThirdPartyComponent> Components = new[]
        {
            new ThirdPartyComponent(
                Name: "Sdcb.LibRaw",
                Role: "ambalajul C# peste libraw",
                Licence: "MIT",
                Copyright: "Copyright (c) Zhou Jie (sdcb)",
                Url: "https://github.com/sdcb/Sdcb.LibRaw",
                Packages: new[] { "Sdcb.LibRaw" }),

            // Dubla licenta, si se alege LGPL: CDDL nu e compatibila cu GPL. Vezi
            // THIRD-PARTY-NOTICES.md pentru de ce; aici doar se spune ce s-a ales.
            // Notita de copyright e cea din fisierul COPYRIGHT al LibRaw 0.21.1, versiunea
            // pe care o aduc pachetele runtime.
            new ThirdPartyComponent(
                Name: "LibRaw",
                Role: "citirea fisierelor raw de aparat (bibliotecile native)",
                Licence: "LGPL-2.1 (optiunea aleasa din dubla LGPL-2.1 / CDDL-1.0)",
                Copyright: "Copyright (C) 2008-2021 LibRaw LLC (info@libraw.org)",
                Url: "https://www.libraw.org/",
                Packages: new[] { "Sdcb.LibRaw.runtime.win64", "Sdcb.LibRaw.runtime.linux64" }),

            new ThirdPartyComponent(
                Name: "SixLabors.ImageSharp",
                Role: "decodarea previzualizarii JPEG din raw",
                Licence: "Six Labors Split License 1.0 (termenii Apache 2.0 pentru software open source)",
                Copyright: "Copyright (c) Six Labors",
                Url: "https://github.com/SixLabors/ImageSharp",
                Packages: new[] { "SixLabors.ImageSharp" }),

            // Date, nu cod: baza de profile de obiective, copiata neschimbata in folderul
            // lensfun/ de langa exe, pentru flatul sintetic. CC BY-SA cere atribuirea, si
            // lista asta e locul in care programul isi arata notitele - de aceea e aici.
            new ThirdPartyComponent(
                Name: "lensfun (baza de date)",
                Role: "profilele de vignetare ale obiectivelor, pentru flatul sintetic (fisierele din lensfun/)",
                Licence: "CC BY-SA 3.0 (doar datele; biblioteca lensfun nu e folosita)",
                Copyright: "Copyright (C) the lensfun contributors",
                Url: "https://github.com/lensfun/lensfun",
                Packages: System.Array.Empty<string>()),
        };
    }
}
