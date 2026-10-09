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

using LightsCheck.Core;

namespace LightsCheck.Wpf;

/// <summary>A third-party component as the "Despre" window lists it.</summary>
public sealed record ComponentRow(string Name, string Role, string Licence, string Copyright, string Url);

/// <summary>The "Despre" window: the banner, the GPLv3 notice and the third-party list, all from the core.</summary>
public sealed class AboutViewModel
{
    public AboutViewModel()
    {
        Components = ThirdParty.Components.Select(c => new ComponentRow(c.Name, c.Role, c.Licence, c.Copyright, c.Url)).ToList();
    }

    public string Title => WindowTexts.AboutTitle;

    public string Banner => VersionInfo.Banner;

    public string Copyright => VersionInfo.Copyright;

    public string Tagline => WindowTexts.AboutTagline;

    public string LicenceHeading => WindowTexts.AboutLicenceHeading;

    /// <summary>GPLv3 §5(d), the console's warranty lines joined into one paragraph.</summary>
    public string Warranty => string.Join(" ", RunReport.WarrantyLines);

    public string LicenseUrl => RunReport.LicenseUrl;

    public string LicenseLinkLabel => WindowTexts.AboutLicenseLinkLabel;

    public string ThirdPartyHeading => WindowTexts.AboutThirdPartyHeading;

    public string ThirdPartyLead => WindowTexts.AboutThirdPartyLead;

    public IReadOnlyList<ComponentRow> Components { get; }
}
