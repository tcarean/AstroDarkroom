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

// AboutViewModel.cs - what the "Despre" window shows. It phrases nothing: the version and the
// copyright from VersionInfo, the warranty from RunReport (the same as at --help), the
// components from ThirdParty.

using System.Collections.Generic;
using System.Linq;

namespace AstroPhotoAnalyzer.Wpf
{
    /// <summary>
    /// The texts of the "Despre" window (R-119): the headings and the lead sentences are its own,
    /// the version, the warranty and the components the core's.
    /// </summary>
    public sealed class AboutViewModel
    {
        /// <summary>Copies the core's component list into <see cref="ComponentRow"/>s.</summary>
        public AboutViewModel()
        {
            Components = ThirdParty.Components
                .Select(c => new ComponentRow { Name = c.Name, Role = c.Role, Licence = c.Licence, Copyright = c.Copyright, Url = c.Url })
                .ToList();
        }

        public string Title => Tr.Pick("About AstroPhotoAnalyzer", "Despre AstroPhotoAnalyzer");
        public string Banner => VersionInfo.Banner;
        public string Copyright => VersionInfo.Copyright;
        public string Tagline => Tr.Pick("Measures a finished astrophoto and says how to send it to print.", "Masoara o astrofoto finita si spune cum sa fie trimisa la print.");
        public string LicenceHeading => Tr.Pick("LICENCE", "LICENTA");

        /// <summary>The GPLv3 section 5(d) notice, in a single paragraph.</summary>
        public string Warranty => string.Join(" ", RunReport.WarrantyLines);
        public string LicenseUrl => RunReport.LicenseUrl;
        public string LicenseLinkLabel => Tr.Pick("The full text of the GPLv3", "Textul complet al GPLv3");
        public string ThirdPartyHeading => Tr.Pick("THIRD-PARTY COMPONENTS", "COMPONENTE TERTE");
        public string ThirdPartyLead => Tr.Pick("The code written by others that the tool links, each with its licence.", "Codul scris de altii pe care il leaga unealta, cu licenta fiecaruia.");
        public IReadOnlyList<ComponentRow> Components { get; }
    }
}
