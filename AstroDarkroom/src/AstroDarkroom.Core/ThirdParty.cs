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

using static AstroDarkroom.Core.Ui;

namespace AstroDarkroom.Core;

/// <summary>
/// A component written by others that the launcher ships: its name, its role here, the licence it
/// is taken under, its copyright line, where its project lives, and the NuGet packages that bring
/// it into the build.
/// </summary>
/// <param name="Name">The component's name, as "Despre" shows it.</param>
/// <param name="Role">What it does in the launcher, in the language of the moment the list is read.</param>
/// <param name="Licence">The licence's SPDX identifier, the same in both languages.</param>
/// <param name="Copyright">Its copyright line, as its licence file states it.</param>
/// <param name="Url">Its project's web address.</param>
/// <param name="Packages">The NuGet package IDs that bring it into the build.</param>
public sealed record ThirdPartyComponent(
    string Name,
    string Role,
    string Licence,
    string Copyright,
    string Url,
    IReadOnlyList<string> Packages);

/// <summary>
/// The components written by others, as "Despre" lists them (R-015): NLog's BSD-3-Clause licence
/// asks for its notice in the materials that go with the program. The whole texts, those of
/// every component the suite folder ships, are in THIRD-PARTY-NOTICES.md, which the launcher
/// publishes beside the executables as THIRD-PARTY-NOTICES.txt (R-017). A new package needs an
/// entry here and a section there; ThirdPartyTests binds the two to the project files (ADR 0013,
/// ADR 0014).
/// </summary>
public static class ThirdParty
{
    /// <summary>The name of the file beside the program that holds each licence's whole text (R-017); the launcher's project publishes it under this name.</summary>
    public const string NoticesFile = "THIRD-PARTY-NOTICES.txt";

    /// <summary>The line "Despre" shows above the list, saying where the whole texts are, in the language of the moment it is read.</summary>
    public static string Lead => T(
        $"Each licence's whole text is in {NoticesFile}, beside the program.",
        $"Textul intreg al fiecarei licente e in {NoticesFile}, langa program.");

    /// <summary>The components, worded in the language of the moment they are read.</summary>
    public static IReadOnlyList<ThirdPartyComponent> Components =>
    [
        // One component, two packages: NLog.Targets.AtomicFile is NLog's own file target with
        // atomic append, built from the same commit (d9ebf82) under the same licence. The copyright
        // line is the first line of LICENSE.txt at that commit, as FlatCheck read it for the same
        // packages (its ADR 0070).
        new ThirdPartyComponent(
            Name: "NLog",
            Role: T("the diagnostic log beside the program (the log folder)",
                    "jurnalul de diagnostic langa program (folderul log)"),
            Licence: "BSD-3-Clause",
            Copyright: "Copyright (c) 2004-2024 Jaroslaw Kowalski <jaak@jkowalski.net>, Kim Christensen, Julian Verdurmen",
            Url: "https://nlog-project.org/",
            Packages: ["NLog", "NLog.Targets.AtomicFile"]),
    ];
}
