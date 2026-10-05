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
// THIRD-PARTY-NOTICES.md, dar acel fisier nu calatoreste garantat cu binarul: cine ia
// doar exe-ul nu primeste niciun fisier de notite. Lista asta e ce arata fereastra
// "Despre", si singurul loc din care poate sa o arate.
//
// O DEPENDINTA NOUA INSEAMNA O INTRARE AICI, pe langa cea din THIRD-PARTY-NOTICES.md.
// ThirdPartyTests leaga lista de PackageReference-urile din AstroPhotoAnalyzer.Core.csproj
// in ambele sensuri, si fiecare nume de un heading din fisierul de notite - un pachet
// adaugat fara intrare pica testul, nu ramane nescris pe ecran.

using System.Collections.Generic;

namespace AstroPhotoAnalyzer
{
    /// <summary>
    /// O componenta terta: cum se cheama, ce face aici, sub ce licenta e luata, a cui e,
    /// unde e proiectul ei, si care pachete NuGet din csproj o aduc - sau, pentru date
    /// imbarcate, care resurse incorporate (EmbeddedResource) o aduc.
    /// </summary>
    internal sealed record ThirdPartyComponent(
        string Name,
        string Role,
        string Licence,
        string Copyright,
        string Url,
        string[] Packages,
        string[] Resources = null)
    {
        internal string[] Resources { get; init; } = Resources ?? System.Array.Empty<string>();
    }

    internal static class ThirdParty
    {
        internal static readonly IReadOnlyList<ThirdPartyComponent> Components = new[]
        {
            new ThirdPartyComponent(
                Name: "SixLabors.ImageSharp",
                Role: "citirea TIFF/PNG/JPEG si encodarea JPEG pentru tabelul de compresie",
                Licence: "Six Labors Split License 1.0 (termenii Apache 2.0 pentru software open source)",
                Copyright: "Copyright (c) Six Labors",
                Url: "https://github.com/SixLabors/ImageSharp",
                Packages: new[] { "SixLabors.ImageSharp" }),
            new ThirdPartyComponent(
                Name: "Wacton.Unicolour",
                Role: "evaluarea tabelelor unui profil ICC de imprimanta, din care se citeste Dmax-ul hartiei",
                Licence: "MIT",
                Copyright: "Copyright (c) 2022-2026 William Acton",
                Url: "https://github.com/waacton/Unicolour",
                Packages: new[] { "Wacton.Unicolour" }),
            new ThirdPartyComponent(
                Name: "Lynds' Catalogue of Dark Nebulae",
                Role: "catalogul norilor de praf (LDN, 1962), dupa care se recunosc norii dintr-un FITS plate-solved",
                Licence: "date publicate (Lynds 1962, ApJS 7, 1), versiunea ADC/NASA din 1979 distribuita de CDS/VizieR ca VII/7A; citarea sursei e singura conditie",
                Copyright: "Lynds B.T., 1962; versiunea citibila: NASA/ADC, 1979; distribuita de CDS, Strasbourg",
                Url: "https://cdsarc.cds.unistra.fr/viz-bin/cat/VII/7A",
                Packages: System.Array.Empty<string>(),
                Resources: new[] { DarkNebulaCatalogue.ResourceName }),
        };
    }
}
