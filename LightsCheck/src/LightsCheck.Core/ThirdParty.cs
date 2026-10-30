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

using static LightsCheck.Core.Ui;

namespace LightsCheck.Core;

/// <summary>
/// A third-party component the tool links: its name, its role here, the licence it is taken
/// under, whose it is, where its project lives, and which NuGet packages bring it.
/// </summary>
/// <param name="Name">The component's name, as the About window shows it.</param>
/// <param name="Role">What it does in LightsCheck, in the run's language, for the window.</param>
/// <param name="Licence">The licence it is taken under, in the run's language, for the window.</param>
/// <param name="Copyright">Its copyright line, as its project states it.</param>
/// <param name="Url">Its project's web address.</param>
/// <param name="Packages">The NuGet package IDs that bring it into the build.</param>
internal sealed record ThirdPartyComponent(
    string Name,
    string Role,
    string Licence,
    string Copyright,
    string Url,
    IReadOnlyList<string> Packages);

/// <summary>
/// The components written by others, as a value: what the "Despre" window shows. LGPL-2.1 §6
/// asks that LibRaw's notice appear where the program displays its own copyright, and the
/// reasons for GPLv3 compatibility are in THIRD-PARTY-NOTICES.md, which does not travel with
/// a single executable. A new package needs an entry here and one in the notices file;
/// ThirdPartyTests binds the two to the Core csproj in both directions (ADR 0003).
/// </summary>
internal static class ThirdParty
{
    /// <summary>The components, worded in the language of the moment they are read.</summary>
    internal static IReadOnlyList<ThirdPartyComponent> Components =>
    [
        new ThirdPartyComponent(
            Name: "Sdcb.LibRaw",
            Role: T("the C# wrapper around libraw", "ambalajul C# peste libraw"),
            Licence: "MIT",
            Copyright: "Copyright (c) Zhou Jie (sdcb)",
            Url: "https://github.com/sdcb/Sdcb.LibRaw",
            Packages: ["Sdcb.LibRaw"]),

        // Dual licence, and LGPL is chosen: CDDL is not GPL-compatible. The copyright line is
        // the one in LibRaw 0.21.1's COPYRIGHT file, the version the runtime packages carry.
        new ThirdPartyComponent(
            Name: "LibRaw",
            Role: T("reading the camera raw files (the native libraries)", "citirea fisierelor raw de aparat (bibliotecile native)"),
            Licence: T("LGPL-2.1 (the option chosen from the dual LGPL-2.1 / CDDL-1.0)", "LGPL-2.1 (optiunea aleasa din dubla LGPL-2.1 / CDDL-1.0)"),
            Copyright: "Copyright (C) 2008-2021 LibRaw LLC (info@libraw.org)",
            Url: "https://www.libraw.org/",
            Packages: ["Sdcb.LibRaw.runtime.win64", "Sdcb.LibRaw.runtime.linux64"]),

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
