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

// AboutViewModel.cs - ce arata fereastra "Despre".
//
// Nu decide si nu formuleaza nimic: versiunea si copyright-ul vin din VersionInfo, notita
// de garantie din RunReport (aceeasi pe care consola o tipareste la --help), lista
// componentelor din ThirdParty, iar titlurile din SessionMessages. Aici se face doar
// trecerea de la tipurile internal ale nucleului la proprietati publice pe care XAML-ul
// le poate lega - si lipirea liniilor de garantie, rupte pentru consola la ~70 de coloane,
// intr-un paragraf pe care fereastra il impacheteaza singura.
//
// Toate proprietatile sunt constante, deci nu e nevoie de ObservableObject. Sunt totusi
// proprietati de instanta: {Binding} rezolva prin DataContext si nu vede una statica.

using System.Collections.Generic;
using System.Linq;

namespace FlatCheck.Wpf
{
    public sealed class AboutViewModel
    {
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

        /// <summary>Numele si versiunea, exact cum apar pe prima linie a unui raport.</summary>
        public string Banner => VersionInfo.Banner;

        public string Copyright => VersionInfo.Copyright;

        public string Tagline => SessionMessages.AboutTagline();

        public string LicenceHeading => SessionMessages.AboutLicenceHeading();

        /// <summary>Notita GPLv3 sectiunea 5(d), intr-un singur paragraf.</summary>
        public string Warranty => string.Join(" ", RunReport.WarrantyLines);

        public string LicenseUrl => RunReport.LicenseUrl;

        public string LicenseLinkLabel => SessionMessages.AboutLicenseLinkLabel();

        public string ThirdPartyHeading => SessionMessages.AboutThirdPartyHeading();

        public string ThirdPartyLead => SessionMessages.AboutThirdPartyLead();

        public IReadOnlyList<ComponentRow> Components { get; }
    }
}
