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

// After DarksCheck's AboutViewModel (which copied LightsCheck's), with the third-party list
// since the tool links NLog (ADR 0009).

using static CopyWcs.Ui;

namespace CopyWcs.Wpf;

/// <summary>A third-party component as the About window lists it.</summary>
/// <param name="Name">The component's name.</param>
/// <param name="Role">What it does in the program, in the run's language.</param>
/// <param name="Licence">Its licence.</param>
/// <param name="Copyright">Its copyright line.</param>
/// <param name="Url">Where it comes from.</param>
public sealed record ComponentRow(string Name, string Role, string Licence, string Copyright, string Url);

/// <summary>
/// The About window's texts: the banner, the copyright, the GPLv3 notice and the third-party
/// list from the core, the headings as pairs (R-035).
/// </summary>
// CA1822: XAML binds these, and a binding needs instance members; statics would not bind.
#pragma warning disable CA1822
public sealed class AboutViewModel
{
    /// <summary>
    /// The window's texts, with the third-party list taken from the core's
    /// <see cref="ThirdParty"/> in the language of the moment it is made.
    /// </summary>
    public AboutViewModel()
    {
        Components = ThirdParty.Components.Select(c => new ComponentRow(c.Name, c.Role, c.Licence, c.Copyright, c.Url)).ToList();
    }

    public string Title => T("About CopyWcs", "Despre CopyWcs");

    public string Banner => VersionInfo.Banner;

    public string Copyright => LicenseInfo.Copyright;

    public string Tagline => T("Copies the plate solve from one FITS into another.", "Copiaza plate solve-ul dintr-un FITS in altul.");

    public string LicenceHeading => T("LICENCE", "LICENTA");

    /// <summary>GPLv3 section 5(d): the console's warranty lines joined into one paragraph.</summary>
    public string Warranty => LicenseInfo.Warranty;

    public string LicenseUrl => LicenseInfo.LicenseUrl;

    public string LicenseLinkLabel => T("The full text of the licence (GPLv3)", "Textul complet al licentei (GPLv3)");

    public string ThirdPartyHeading => T("THIRD-PARTY COMPONENTS", "COMPONENTE TERTE");

    public string ThirdPartyLead => T(
        "The tool links one component written by others, compatible with GPLv3: NLog, which writes the "
        + "diagnostic log. The FITS header is read and written by hand. The licence's full text, and why "
        + "it is compatible, are in THIRD-PARTY-NOTICES.md, beside the source.",
        "Unealta leaga o componenta scrisa de altii, compatibila cu GPLv3: NLog, care scrie jurnalul de "
        + "diagnostic. Header-ul FITS e citit si scris de mana. Textul complet al licentei si motivul pentru "
        + "care e compatibila sunt in THIRD-PARTY-NOTICES.md, langa sursa.");

    /// <summary>The components the core links, one row each (R-035).</summary>
    public IReadOnlyList<ComponentRow> Components { get; }
}
#pragma warning restore CA1822
