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

// AboutViewModel.cs - ce arata fereastra "Despre". Nu formuleaza nimic: versiunea si
// copyright-ul din VersionInfo, garantia din RunReport (aceeasi ca la --help),
// componentele din ThirdParty.

using System.Collections.Generic;
using System.Linq;

namespace AstroPhotoAnalyzer.Wpf
{
    public sealed class AboutViewModel
    {
        public AboutViewModel()
        {
            Components = ThirdParty.Components
                .Select(c => new ComponentRow { Name = c.Name, Role = c.Role, Licence = c.Licence, Copyright = c.Copyright, Url = c.Url })
                .ToList();
        }

        public string Title => "Despre AstroPhotoAnalyzer";
        public string Banner => VersionInfo.Banner;
        public string Copyright => VersionInfo.Copyright;
        public string Tagline => "Masoara o astrofoto finita si spune cum sa fie trimisa la print.";
        public string LicenceHeading => "LICENTA";

        /// <summary>Notita GPLv3 sectiunea 5(d), intr-un singur paragraf.</summary>
        public string Warranty => string.Join(" ", RunReport.WarrantyLines);
        public string LicenseUrl => RunReport.LicenseUrl;
        public string LicenseLinkLabel => "Textul complet al GPLv3";
        public string ThirdPartyHeading => "COMPONENTE TERTE";
        public string ThirdPartyLead => "Codul scris de altii pe care il leaga unealta, cu licenta fiecaruia.";
        public IReadOnlyList<ComponentRow> Components { get; }
    }
}
