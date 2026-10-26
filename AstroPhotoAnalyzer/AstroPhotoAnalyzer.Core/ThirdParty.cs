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

// ThirdParty.cs - the components written by others that the tool links, as a value.
//
// WHY IT IS SHOWN, NOT ONLY SHIPPED. The reasons for the compatibility with GPLv3 are in
// THIRD-PARTY-NOTICES.md, but that file is not guaranteed to travel with the binary: whoever
// takes only the exe gets no notices file. This list is what the window's "Despre" shows, and
// the only place it can show it from.
//
// A NEW DEPENDENCY MEANS AN ENTRY HERE, besides the one in THIRD-PARTY-NOTICES.md.
// ThirdPartyTests binds the list to the PackageReferences of AstroPhotoAnalyzer.Core.csproj in
// both directions, and each name to a heading of the notices file - a package added without an
// entry fails the test, rather than stay unwritten on the screen.

using System.Collections.Generic;

namespace AstroPhotoAnalyzer
{
    /// <summary>
    /// A third-party component: what it is called, what it does here, under which licence it is
    /// taken, whose it is, where its project is, and which NuGet packages of the csproj bring it -
    /// or, for embedded data, which embedded resources (EmbeddedResource) bring it.
    /// </summary>
    /// <param name="Name">
    /// The component's name, which a heading of THIRD-PARTY-NOTICES.md contains.
    /// </param>
    /// <param name="Role">What it does in this tool, in the interface language, as "Despre" shows it.</param>
    /// <param name="Licence">The licence it is taken under, as "Despre" shows it.</param>
    /// <param name="Copyright">
    /// Its copyright line, or for data its authors and distributors.
    /// </param>
    /// <param name="Url">The project's or the catalogue's web page.</param>
    /// <param name="Packages">
    /// The core's PackageReference names that bring it; empty, never null, for embedded data.
    /// </param>
    /// <param name="Resources">
    /// The logical names of the embedded resources that bring it; null reads as empty.
    /// </param>
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

    /// <summary>
    /// The third-party components the tool links, in the order "Despre" lists them.
    /// </summary>
    internal static class ThirdParty
    {
        // A property, so that the roles and the licences are in the interface language in use
        // when they are shown (ADR 0048).
        internal static IReadOnlyList<ThirdPartyComponent> Components => new[]
        {
            new ThirdPartyComponent(
                Name: "SixLabors.ImageSharp",
                Role: Tr.Pick("reading TIFF/PNG/JPEG, and encoding the JPEGs and the 8-bit LZW TIFF of the compression table",
                             "citirea TIFF/PNG/JPEG si encodarea JPEG si a TIFF-ului 8-bit LZW pentru tabelul de compresie"),
                Licence: Tr.Pick("Six Labors Split License 1.0 (the Apache 2.0 terms for open-source software)",
                                "Six Labors Split License 1.0 (termenii Apache 2.0 pentru software open source)"),
                Copyright: "Copyright (c) Six Labors",
                Url: "https://github.com/SixLabors/ImageSharp",
                Packages: new[] { "SixLabors.ImageSharp" }),
            new ThirdPartyComponent(
                Name: "Wacton.Unicolour",
                Role: Tr.Pick("evaluating the tables of a printer ICC profile, for the paper's Dmax and for the share outside the paper gamut in the Curves tab",
                             "evaluarea tabelelor unui profil ICC de imprimanta, pentru Dmax-ul hartiei si pentru partea din afara gamutului hartiei din tabul Curbe"),
                Licence: "MIT",
                Copyright: "Copyright (c) 2022-2026 William Acton",
                Url: "https://github.com/waacton/Unicolour",
                Packages: new[] { "Wacton.Unicolour" }),
            new ThirdPartyComponent(
                Name: "Lynds' Catalogue of Dark Nebulae",
                Role: Tr.Pick("the catalogue of dust clouds (LDN, 1962), by which the clouds of a plate-solved FITS are recognized",
                             "catalogul norilor de praf (LDN, 1962), dupa care se recunosc norii dintr-un FITS plate-solved"),
                Licence: Tr.Pick("published data (Lynds 1962, ApJS 7, 1), the ADC/NASA version of 1979 distributed by CDS/VizieR as VII/7A; citing the source is the only condition",
                                "date publicate (Lynds 1962, ApJS 7, 1), versiunea ADC/NASA din 1979 distribuita de CDS/VizieR ca VII/7A; citarea sursei e singura conditie"),
                Copyright: Tr.Pick("Lynds B.T., 1962; machine-readable version: NASA/ADC, 1979; distributed by CDS, Strasbourg",
                                  "Lynds B.T., 1962; versiunea citibila: NASA/ADC, 1979; distribuita de CDS, Strasbourg"),
                Url: "https://cdsarc.cds.unistra.fr/viz-bin/cat/VII/7A",
                Packages: System.Array.Empty<string>(),
                Resources: new[] { DarkNebulaCatalogue.ResourceName }),
        };
    }
}
