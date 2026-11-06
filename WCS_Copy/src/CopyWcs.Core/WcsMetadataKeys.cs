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

/// <summary>Which keys a copy takes from the source: the WCS, the SIP coefficients and the optical metadata (R-007).</summary>
internal static class WcsMetadataKeys
{
    /// <summary>The keys that describe the astrometric solution itself.</summary>
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

    /// <summary>
    /// The optical and observation metadata. Without FOCALLEN and XPIXSZ a later re-solve has
    /// no starting scale, so they are worth copying too.
    /// </summary>
    internal static readonly string[] OpticalKeys =
    {
        "FOCALLEN", "FOCAL", "XPIXSZ", "YPIXSZ", "XBINNING", "YBINNING",
        "OBJCTRA", "OBJCTDEC", "RA", "DEC", "OBJECT",
        "SITELAT", "SITELONG", "SITEELEV", "DATE-OBS", "INSTRUME", "TELESCOP"
    };

    /// <summary>The SIP coefficients have variable names: A_0_2, B_1_1, AP_2_0 and so on.</summary>
    private static readonly Regex SipCoefficient =
        new(@"^(A|B|AP|BP)_\d+_\d+$", RegexOptions.Compiled);

    /// <summary>
    /// The keys to copy: every WCS key, every SIP coefficient the source has, and the optical
    /// keys when asked; only those present in the source, each once, in the order of the lists
    /// (the SIP coefficients in the source's order, between the two).
    /// </summary>
    /// <param name="source">The header the keys come from.</param>
    /// <param name="includeOptical">True to add <see cref="OpticalKeys"/>.</param>
    /// <returns>The keys present in the source, without duplicates.</returns>
    internal static List<string> SelectKeysToCopy(FitsHeader source, bool includeOptical)
    {
        ArgumentNullException.ThrowIfNull(source);

        var wanted = new List<string>(WcsKeys);
        wanted.AddRange(source.Keywords.Where(k => SipCoefficient.IsMatch(k)));
        if (includeOptical)
        {
            wanted.AddRange(OpticalKeys);
        }

        return wanted.Distinct().Where(source.Contains).ToList();
    }

    /// <summary>
    /// The destination's old plate-solve keys a copy drops (R-016): every keyword of the
    /// destination that is one of <see cref="WcsKeys"/> or a SIP coefficient and that the source
    /// does not have, each once, in the order the destination holds them. Never an optical key.
    /// Whether to drop at all, only when the source has <c>CTYPE1</c>, is the run's rule.
    /// </summary>
    /// <param name="source">The header the plate solve comes from; not null.</param>
    /// <param name="dest">The header that receives it; not null.</param>
    /// <returns>The keys to drop; empty when the destination holds no plate-solve key the source lacks.</returns>
    internal static List<string> SelectKeysToDrop(FitsHeader source, FitsHeader dest)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(dest);

        return dest.Keywords
            .Where(k => (WcsKeys.Contains(k) || SipCoefficient.IsMatch(k)) && !source.Contains(k))
            .Distinct()
            .ToList();
    }
}
