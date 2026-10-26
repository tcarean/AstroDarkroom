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

// After DarksCheck's AboutViewModel (which copied LightsCheck's), without the third-party
// list: this project has no package (ADR 0007).

using static CopyWcs.Ui;

namespace CopyWcs.Wpf;

/// <summary>
/// The About window's texts: the banner, the copyright and the GPLv3 notice from the core, the
/// headings as pairs (R-035).
/// </summary>
// CA1822: XAML binds these, and a binding needs instance members; statics would not bind.
#pragma warning disable CA1822
public sealed class AboutViewModel
{
    public string Title => T("About CopyWcs", "Despre CopyWcs");

    public string Banner => VersionInfo.Banner;

    public string Copyright => LicenseInfo.Copyright;

    public string Tagline => T("Copies the plate solve from one FITS into another.", "Copiaza plate solve-ul dintr-un FITS in altul.");

    public string LicenceHeading => T("LICENCE", "LICENTA");

    /// <summary>GPLv3 section 5(d): the console's warranty lines joined into one paragraph.</summary>
    public string Warranty => LicenseInfo.Warranty;

    public string LicenseUrl => LicenseInfo.LicenseUrl;

    public string LicenseLinkLabel => T("The full text of the licence (GPLv3)", "Textul complet al licentei (GPLv3)");
}
#pragma warning restore CA1822
