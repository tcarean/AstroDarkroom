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
// WHY THEY ARE SHOWN, NOT ONLY SHIPPED. The GPLv3 compatibility arguments sit in
// THIRD-PARTY-NOTICES.md, but that file is not guaranteed to travel with the binary: the tool's
// promise is "a single copyable exe", and whoever takes only the exe gets no notices file. And
// LGPL-2.1 section 6 (LibRaw) asks explicitly that, if the program shows copyright notices at
// run time - and it does, on the second line of every report and in the window's bar - the
// library's notice appear among them, with a reference to the licence. So this list is what the
// "Despre" window shows, and the only place it can show it from.
//
// A NEW DEPENDENCY MEANS AN ENTRY HERE, beside the one in THIRD-PARTY-NOTICES.md.
// ThirdPartyTests ties the list to the PackageReferences of FlatCheck.Core.csproj in both
// directions, the DLLs of LibRaw's win-x64 runtime package to the components that name them
// (LibRaw and the three libraries its DLL calls, each a component of its own, as the Astro
// Darkroom folder's notices list them), and each name to a heading of the notices file - a
// package or a DLL added without an entry fails the test, it does not stay unwritten on screen.
//
// WHERE THE WHOLE TEXTS ARE. The lead above the list says it (R-095): in THIRD-PARTY-NOTICES.txt
// beside the program in the Astro Darkroom folder, and at NoticesUrl, the same file in the source
// repository, a link under the lead. FlatCheck's own release ships no notices file, so the address is what is true from
// both places the tool runs.
//
// The text mirrors the table of the README and the notices file BY HAND; no test compares the
// wording, only the presence.

using System.Collections.Generic;
using static FlatCheck.Ui;

namespace FlatCheck
{
    /// <summary>
    /// A third-party component: what it is called, what it does here, under which licence it is
    /// taken, whose it is, where its project is, and which NuGet packages of the csproj bring it.
    /// </summary>
    /// <param name="Name">The component's name, as the notices file's heading spells it.</param>
    /// <param name="Role">What it does in the tool, in the run's language, as the window shows it.</param>
    /// <param name="Licence">The licence it is taken under.</param>
    /// <param name="Copyright">Its copyright notice.</param>
    /// <param name="Url">Its project's home page.</param>
    /// <param name="Packages">The NuGet packages that bring it; empty for data.</param>
    internal sealed record ThirdPartyComponent(
        string Name,
        string Role,
        string Licence,
        string Copyright,
        string Url,
        string[] Packages)
    {
        /// <summary>
        /// A DATA component, not a code one: it does not come through a NuGet package, but as
        /// files shipped beside the exe (the lensfun database). The package list is empty and the
        /// test that ties the components to the csproj knows it.
        /// </summary>
        internal bool IsData => Packages.Length == 0;

        /// <summary>
        /// The native DLLs of the win-x64 runtime that are this component: raw_r.dll for LibRaw,
        /// jpeg8.dll for libjpeg-turbo, and so on. Empty for a managed component and for data. A
        /// package that brings several components is divided among them by these files (R-095).
        /// </summary>
        internal string[] NativeFiles { get; init; } = System.Array.Empty<string>();
    }

    /// <summary>The components the tool links or ships, for the notices it shows.</summary>
    internal static class ThirdParty
    {
        /// <summary>
        /// Where each licence's whole text is online: the Astro Darkroom folder's
        /// THIRD-PARTY-NOTICES.txt as the source repository holds it. The window shows it as a
        /// link under the lead, above the list, since FlatCheck's own release carries no notices
        /// file (R-095).
        /// </summary>
        internal const string NoticesUrl = "https://github.com/tcarean/AstroDarkroom/blob/main/AstroDarkroom/THIRD-PARTY-NOTICES.md";

        /// <summary>The components, in the order the window lists them: each package's code,
        /// LibRaw's libraries after LibRaw, the database last.</summary>
        internal static IReadOnlyList<ThirdPartyComponent> Components => new[]
        {
            new ThirdPartyComponent(
                Name: "Sdcb.LibRaw",
                Role: T("the C# wrapper over libraw", "ambalajul C# peste libraw"),
                Licence: "MIT",
                Copyright: "Copyright (c) Zhou Jie (sdcb)",
                Url: "https://github.com/sdcb/Sdcb.LibRaw",
                Packages: new[] { "Sdcb.LibRaw" }),

            // Dual licence, and LGPL is chosen: CDDL is not compatible with the GPL. See
            // THIRD-PARTY-NOTICES.md for why; here it only says what was chosen. The copyright
            // notice is the one in the COPYRIGHT file of LibRaw 0.21.1, the version the runtime
            // packages bring.
            new ThirdPartyComponent(
                Name: "LibRaw",
                Role: T("reading camera raw files (raw_r.dll)", "citirea fisierelor raw de aparat (raw_r.dll)"),
                Licence: T("LGPL-2.1 (the option chosen of the dual LGPL-2.1 / CDDL-1.0)", "LGPL-2.1 (optiunea aleasa din dubla LGPL-2.1 / CDDL-1.0)"),
                Copyright: "Copyright (C) 2008-2021 LibRaw LLC (info@libraw.org)",
                Url: "https://www.libraw.org/",
                Packages: new[] { "Sdcb.LibRaw.runtime.win64", "Sdcb.LibRaw.runtime.linux64" })
            { NativeFiles = new[] { "raw_r.dll" } },

            // The three libraries raw_r.dll calls, which the win-x64 runtime package ships beside it
            // as DLLs of their own (R-095), each under its own licence: the versions they report
            // and the copyright lines are those of the Astro Darkroom folder's THIRD-PARTY-NOTICES,
            // read on 2026-10-31 (libjpeg-turbo 2.1.3, Little CMS 2.12, zlib 1.2.11). The IJG
            // licence asks the documentation of a binary to say the software is based in part on
            // the IJG's work, so the sentence ends the copyright line, which no language changes.
            new ThirdPartyComponent(
                Name: "libjpeg-turbo",
                Role: T("decoding, for LibRaw, the JPEG-compressed data some raw files hold (jpeg8.dll)",
                        "decodarea, pentru LibRaw, a datelor comprimate JPEG din unele fisiere raw (jpeg8.dll)"),
                Licence: T("IJG, BSD-3-Clause and zlib", "IJG, BSD-3-Clause si zlib"),
                Copyright: "Copyright (C) 1991-2020 Thomas G. Lane, Guido Vollbeding; Copyright (C) 2009-2022 D. R. Commander; "
                         + "Copyright (C) 2015 Viktor Szathmáry. This software is based in part on the work of the Independent JPEG Group.",
                Url: "https://libjpeg-turbo.org/",
                Packages: new[] { "Sdcb.LibRaw.runtime.win64" })
            { NativeFiles = new[] { "jpeg8.dll" } },

            new ThirdPartyComponent(
                Name: "Little CMS",
                Role: T("the colour management inside LibRaw, its ICC profile transforms (lcms2.dll)",
                        "gestiunea culorii din LibRaw, transformarile ei de profil ICC (lcms2.dll)"),
                Licence: "MIT",
                Copyright: "Copyright (c) 1998-2020 Marti Maria Saguer",
                Url: "https://www.littlecms.com/",
                Packages: new[] { "Sdcb.LibRaw.runtime.win64" })
            { NativeFiles = new[] { "lcms2.dll" } },

            new ThirdPartyComponent(
                Name: "zlib",
                Role: T("decompressing, for LibRaw, the Deflate-compressed data some raw files hold (zlib1.dll)",
                        "decomprimarea, pentru LibRaw, a datelor comprimate Deflate din unele fisiere raw (zlib1.dll)"),
                Licence: "zlib",
                Copyright: "Copyright (C) 1995-2017 Jean-loup Gailly and Mark Adler",
                Url: "https://zlib.net/",
                Packages: new[] { "Sdcb.LibRaw.runtime.win64" })
            { NativeFiles = new[] { "zlib1.dll" } },

            // The Split License grants the Apache License 2.0 to software under an open-source
            // licence, which FlatCheck is, and asks all documentation to name the granted licence
            // only; THIRD-PARTY-NOTICES.md argues the grant (ADR 0010, ADR 0082).
            new ThirdPartyComponent(
                Name: "SixLabors.ImageSharp",
                Role: T("decoding the JPEG preview in a raw", "decodarea previzualizarii JPEG din raw"),
                Licence: "Apache-2.0",
                Copyright: "Copyright (c) Six Labors",
                Url: "https://github.com/SixLabors/ImageSharp",
                Packages: new[] { "SixLabors.ImageSharp" }),

            // One component, two packages, as LibRaw above: NLog.Targets.AtomicFile is NLog's own
            // file target with atomic append, built from the same commit (d9ebf82) under the same
            // licence. The copyright notice is the first line of LICENSE.txt at that commit; the
            // BSD terms ask for it, the conditions and the disclaimer, which
            // THIRD-PARTY-NOTICES.md reproduces.
            new ThirdPartyComponent(
                Name: "NLog",
                Role: T("writing the diagnostic log beside the program (the log folder), two windows into one file",
                        "scrierea jurnalului de diagnostic langa program (folderul log), doua ferestre in acelasi fisier"),
                Licence: "BSD-3-Clause",
                Copyright: "Copyright (c) 2004-2024 Jaroslaw Kowalski <jaak@jkowalski.net>, Kim Christensen, Julian Verdurmen",
                Url: "https://nlog-project.org/",
                Packages: new[] { "NLog", "NLog.Targets.AtomicFile" }),

            // Data, not code: the database of lens profiles, copied unchanged into the lensfun/
            // folder beside the exe, for the synthetic flat. CC BY-SA asks for attribution, and
            // this list is the place where the program shows its notices - which is why it is here.
            new ThirdPartyComponent(
                Name: T("lensfun (database)", "lensfun (baza de date)"),
                Role: T("the lenses' vignetting profiles, for the synthetic flat (the files in lensfun/)",
                        "profilele de vignetare ale obiectivelor, pentru flatul sintetic (fisierele din lensfun/)"),
                Licence: T("CC BY-SA 3.0 (the data only; the lensfun library is not used)",
                           "CC BY-SA 3.0 (doar datele; biblioteca lensfun nu e folosita)"),
                Copyright: "Copyright (C) the lensfun contributors",
                Url: "https://github.com/lensfun/lensfun",
                Packages: System.Array.Empty<string>()),
        };
    }
}
