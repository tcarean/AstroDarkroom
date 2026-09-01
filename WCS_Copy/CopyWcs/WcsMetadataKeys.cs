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

using System.Text.RegularExpressions;

namespace CopyWcs;

internal static class WcsMetadataKeys
{
    // Cheile care descriu solutia astrometrica propriu-zisa.
    internal static readonly string[] WcsKeys =
    {
        "WCSAXES", "CTYPE1", "CTYPE2", "CUNIT1", "CUNIT2",
        "CRPIX1", "CRPIX2", "CRVAL1", "CRVAL2",
        "CDELT1", "CDELT2", "CROTA1", "CROTA2",
        "CD1_1", "CD1_2", "CD2_1", "CD2_2",
        "PC1_1", "PC1_2", "PC2_1", "PC2_2",
        "EQUINOX", "RADESYS", "LONPOLE", "LATPOLE",
        "A_ORDER", "B_ORDER", "AP_ORDER", "BP_ORDER",
        "PLTSOLVD"
    };

    // Metadate optice / de observatie. Fara FOCALLEN si XPIXSZ un re-solve
    // ulterior nu are scara de pornire, deci merita copiate si ele.
    internal static readonly string[] OpticalKeys =
    {
        "FOCALLEN", "FOCAL", "XPIXSZ", "YPIXSZ", "XBINNING", "YBINNING",
        "OBJCTRA", "OBJCTDEC", "RA", "DEC", "OBJECT",
        "SITELAT", "SITELONG", "SITEELEV", "DATE-OBS", "INSTRUME", "TELESCOP"
    };

    // Coeficientii SIP au nume variabile: A_0_2, B_1_1, AP_2_0 etc.
    private static readonly Regex SipCoefficient =
        new(@"^(A|B|AP|BP)_\d+_\d+$", RegexOptions.Compiled);

    internal static List<string> SelectKeysToCopy(FitsHeader source, bool includeOptical)
    {
        var wanted = new List<string>(WcsKeys);
        wanted.AddRange(source.Keywords.Where(k => SipCoefficient.IsMatch(k)));
        if (includeOptical) wanted.AddRange(OpticalKeys);

        return wanted.Distinct().Where(source.Contains).ToList();
    }
}
