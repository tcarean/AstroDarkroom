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

// ThirdParty.cs - the components written by others that the tool links, as a value, after
// DarksCheck's. They are shown, not only shipped: THIRD-PARTY-NOTICES.md stays beside the source,
// and the executable, copied alone or bundled into the Astro Darkroom suite's one folder, carries
// only the About window (ADR 0009). A new package means an entry here and a heading in the notices
// file; ThirdPartyTests ties the list to the core's csproj in both directions.

using static CopyWcs.Ui;

namespace CopyWcs;

/// <summary>
/// A third-party component the tool links: its name, its role here, the licence it is taken
/// under, whose it is, where its project lives, and which NuGet packages bring it.
/// </summary>
/// <param name="Name">The component's name, as the About window lists it and the notices file's heading spells it.</param>
/// <param name="Role">What it does in the tool, in the run's language (the window shows it).</param>
/// <param name="Licence">The licence it is taken under, as the window shows it.</param>
/// <param name="Copyright">Its copyright line, as its own files state it.</param>
/// <param name="Url">The address of its project.</param>
/// <param name="Packages">The NuGet package IDs that bring it; ThirdPartyTests matches them to the core's csproj.</param>
internal sealed record ThirdPartyComponent(
    string Name,
    string Role,
    string Licence,
    string Copyright,
    string Url,
    IReadOnlyList<string> Packages);

/// <summary>The components the tool links, for the notices the About window shows (R-035).</summary>
internal static class ThirdParty
{
    /// <summary>The components, their words picked in the run's language when read.</summary>
    internal static IReadOnlyList<ThirdPartyComponent> Components =>
    [
        // One component, two packages: NLog.Targets.AtomicFile is NLog's own file target with
        // the atomic append, built from the same commit (d9ebf82) under the same licence. The
        // copyright line is the first line of LICENSE.txt at that commit, as FlatCheck's list has
        // it; the BSD terms ask for it, the conditions and the disclaimer, which
        // THIRD-PARTY-NOTICES.md reproduces.
        new ThirdPartyComponent(
            Name: "NLog",
            Role: T("writing the diagnostic log beside the program (the log folder)",
                    "scrierea jurnalului de diagnostic langa program (folderul log)"),
            Licence: "BSD-3-Clause",
            Copyright: "Copyright (c) 2004-2024 Jaroslaw Kowalski <jaak@jkowalski.net>, Kim Christensen, Julian Verdurmen",
            Url: "https://nlog-project.org/",
            Packages: ["NLog", "NLog.Targets.AtomicFile"]),
    ];
}
