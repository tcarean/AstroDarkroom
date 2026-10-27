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

// AboutViewModel.cs - what the "Despre" window shows.
//
// It decides and words nothing: the version and the copyright come from VersionInfo, the warranty
// notice from RunReport (the same one the console prints for --help), the component list from
// ThirdParty, and the headings from SessionMessages. All that happens here is the passage from
// the core's internal types to public properties the XAML can bind - and the joining of the
// warranty lines, broken at ~70 columns for the console, into one paragraph the window wraps by
// itself.
//
// Every property is constant, so there is no need for ObservableObject. They are still instance
// properties: {Binding} resolves through the DataContext and does not see a static one.

using System.Collections.Generic;
using System.Linq;

namespace FlatCheck.Wpf
{
    /// <summary>The "Despre" window's content: banner, copyright, licence notice and the
    /// third-party components, as public properties for the binding.</summary>
    public sealed class AboutViewModel
    {
        /// <summary>Copies the core's component list into public rows.</summary>
        public AboutViewModel()
        {
            Components = ThirdParty.Components
                .Select(c => new ComponentRow
                {
                    Name = c.Name,
                    Role = c.Role,
                    Licence = c.Licence,
                    Copyright = c.Copyright,
                    Url = c.Url,
                })
                .ToList();
        }

        public string Title => SessionMessages.AboutTitle();

        /// <summary>The name and version, exactly as they appear on a report's first
        /// line.</summary>
        public string Banner => VersionInfo.Banner;

        public string Copyright => VersionInfo.Copyright;

        public string Tagline => SessionMessages.AboutTagline();

        public string LicenceHeading => SessionMessages.AboutLicenceHeading();

        /// <summary>The GPLv3 section 5(d) notice, in a single paragraph.</summary>
        public string Warranty => string.Join(" ", RunReport.WarrantyLines);

        public string LicenseUrl => RunReport.LicenseUrl;

        public string LicenseLinkLabel => SessionMessages.AboutLicenseLinkLabel();

        public string ThirdPartyHeading => SessionMessages.AboutThirdPartyHeading();

        /// <summary>The lead counts the linked components from the core's list, so it cannot
        /// stay behind the list; the database is named apart.</summary>
        public string ThirdPartyLead => SessionMessages.AboutThirdPartyLead(ThirdParty.Components.Count(c => !c.IsData));

        public IReadOnlyList<ComponentRow> Components { get; }
    }
}
