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

using static DarksCheck.Core.Ui;

namespace DarksCheck.Core;

/// <summary>
/// A third-party component the tool links: its name, its role here, the licence it is taken
/// under, whose it is, where its project lives, and which NuGet packages bring it.
/// </summary>
/// <param name="Name">The component's name, as the About window lists it.</param>
/// <param name="Role">What it does in the tool, in the run's language (the window shows it).</param>
/// <param name="Licence">The licence it is taken under, as the window shows it (in the run's
/// language when a choice is named).</param>
/// <param name="Copyright">Its copyright line, as its own files state it.</param>
/// <param name="Url">The address of its project.</param>
/// <param name="Packages">The NuGet package IDs that bring it; ThirdPartyTests matches them to the
/// Core csproj.</param>
internal sealed record ThirdPartyComponent(
    string Name,
    string Role,
    string Licence,
    string Copyright,
    string Url,
    IReadOnlyList<string> Packages);

/// <summary>
/// The components written by others, as a value: what the About window shows, under its lead.
/// LGPL-2.1 §6 asks that LibRaw's notice appear where the program displays its own copyright,
/// with a reference to the licence's copy: the lead points to the suite's notices, which hold
/// every licence's whole text, while THIRD-PARTY-NOTICES.md here argues each one's
/// compatibility. A new package needs an entry here and one in the notices file;
/// ThirdPartyTests binds the two to the Core csproj in both directions (ADR 0003). Each DLL of
/// LibRaw's Windows package is a component of its own, the three libraries LibRaw calls listed
/// apart from it, as the Astro Darkroom suite's notices list them for DarksCheck (R-120, ADR
/// 0027).
/// </summary>
internal static class ThirdParty
{
    /// <summary>
    /// The lead above the list in About, in the language of the moment it is read (R-120): how
    /// many components the program ships, as a word that ThirdPartyTests holds to the list's
    /// length, what is written by hand, and where each licence's whole text is, in words true
    /// from the suite folder and from a build of the source alike. LightsCheck's wording (its ADR
    /// 0030), as the window reading it is LightsCheck's (ADR 0027).
    /// </summary>
    internal static string Lead => T(
        "The tool ships six components written by others, all compatible with the GPLv3. The FITS reader, the PNG encoder, "
        + "every calculation and every threshold are written by hand. Each licence's whole text, and why it is compatible, is "
        + "in THIRD-PARTY-NOTICES.txt beside the program in the Astro Darkroom suite folder, and in the source in "
        + "AstroDarkroom/THIRD-PARTY-NOTICES.md.",
        "Unealta livreaza sase componente scrise de altii, toate compatibile cu GPLv3. Cititorul FITS, encoderul PNG, toate "
        + "calculele si toate pragurile sunt scrise de mana. Textul intreg al fiecarei licente, si motivul pentru care e "
        + "compatibila, sunt in THIRD-PARTY-NOTICES.txt, langa program in folderul suitei Astro Darkroom, si in sursa, in "
        + "AstroDarkroom/THIRD-PARTY-NOTICES.md.");

    /// <summary>The components, their words picked in the run's language when read.</summary>
    internal static IReadOnlyList<ThirdPartyComponent> Components =>
    [
        new ThirdPartyComponent(
            Name: "Sdcb.LibRaw",
            Role: T("the C# wrapper over libraw", "ambalajul C# peste libraw"),
            Licence: "MIT",
            Copyright: "Copyright (c) Zhou Jie (sdcb)",
            Url: "https://github.com/sdcb/Sdcb.LibRaw",
            Packages: ["Sdcb.LibRaw"]),

        // Dual licence, and LGPL is chosen: CDDL is not GPL-compatible. The copyright line is
        // the one in LibRaw 0.21.1's COPYRIGHT file, the version the runtime packages carry.
        new ThirdPartyComponent(
            Name: "LibRaw",
            Role: T("reading camera raw files (the native library, raw_r.dll)", "citirea fisierelor raw de aparat (biblioteca nativa, raw_r.dll)"),
            Licence: T("LGPL-2.1 (the option chosen from the dual LGPL-2.1 / CDDL-1.0)", "LGPL-2.1 (optiunea aleasa din dubla LGPL-2.1 / CDDL-1.0)"),
            Copyright: "Copyright (C) 2008-2021 LibRaw LLC (info@libraw.org)",
            Url: "https://www.libraw.org/",
            Packages: ["Sdcb.LibRaw.runtime.win64", "Sdcb.LibRaw.runtime.linux64"]),

        // The three libraries LibRaw calls, which its Windows package ships beside raw_r.dll, each
        // a component of its own as in the suite's notices (ADR 0027). libjpeg-turbo 2.1.3: the IJG
        // licence asks a program distributed as executables to state, in its documentation, that
        // it is based in part on the IJG's work; the single file has no documentation but this
        // window, so the licence line quotes it. The copyright lines are the ones its README.ijg
        // and LICENSE.md give, as the suite's notices quote them.
        new ThirdPartyComponent(
            Name: "libjpeg-turbo",
            Role: T("decoding, for LibRaw, the JPEG data some raw files hold (jpeg8.dll)", "decodarea, pentru LibRaw, a datelor JPEG din unele fisiere raw (jpeg8.dll)"),
            Licence: T("IJG, BSD-3-Clause and zlib; \"this software is based in part on the work of the Independent JPEG Group\"",
                       "IJG, BSD-3-Clause si zlib; \"this software is based in part on the work of the Independent JPEG Group\""),
            Copyright: "Copyright (C) 1991-2020 Thomas G. Lane, Guido Vollbeding; (C) 2009-2022 D. R. Commander; (C) 2015 Viktor Szathmáry",
            Url: "https://libjpeg-turbo.org/",
            Packages: ["Sdcb.LibRaw.runtime.win64"]),

        // Little CMS 2.12, its COPYING file's copyright line.
        new ThirdPartyComponent(
            Name: "Little CMS",
            Role: T("the colour management inside LibRaw, its ICC profile transforms (lcms2.dll)", "managementul culorii din LibRaw, transformarile de profil ICC (lcms2.dll)"),
            Licence: "MIT",
            Copyright: "Copyright (c) 1998-2020 Marti Maria Saguer",
            Url: "https://www.littlecms.com/",
            Packages: ["Sdcb.LibRaw.runtime.win64"]),

        // zlib 1.2.11, the copyright line of its zlib.h.
        new ThirdPartyComponent(
            Name: "zlib",
            Role: T("decompressing, for LibRaw, the Deflate data some raw files hold (zlib1.dll)", "decomprimarea, pentru LibRaw, a datelor Deflate din unele fisiere raw (zlib1.dll)"),
            Licence: "zlib",
            Copyright: "Copyright (C) 1995-2017 Jean-loup Gailly and Mark Adler",
            Url: "https://zlib.net/",
            Packages: ["Sdcb.LibRaw.runtime.win64"]),

        // One component, two packages, as LibRaw above: NLog.Targets.AtomicFile is NLog's own file
        // target with atomic append, built from the same commit (d9ebf82) under the same licence.
        // The copyright notice is the first line of LICENSE.txt at that commit, as FlatCheck read
        // it for the same packages (its ADR 0070); the BSD terms ask for it, the conditions and
        // the disclaimer, which THIRD-PARTY-NOTICES.md reproduces.
        new ThirdPartyComponent(
            Name: "NLog",
            Role: T("writing the diagnostic log beside the program (the log folder), two windows into one file",
                    "scrierea jurnalului de diagnostic langa program (folderul log), doua ferestre in acelasi fisier"),
            Licence: "BSD-3-Clause",
            Copyright: "Copyright (c) 2004-2024 Jaroslaw Kowalski <jaak@jkowalski.net>, Kim Christensen, Julian Verdurmen",
            Url: "https://nlog-project.org/",
            Packages: ["NLog", "NLog.Targets.AtomicFile"]),
    ];
}
