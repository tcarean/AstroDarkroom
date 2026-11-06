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

/// <summary>
/// Which keys a copy takes from the source (the WCS, the SIP coefficients and the optical
/// metadata, R-007), which old plate-solve keys it drops from the destination (R-016), and which
/// way a plate solve faces, to tell a mirrored destination (R-017).
/// </summary>
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
    /// The orientation of a header's plate solve (R-017): the sign of the determinant of its
    /// linear part, read as Siril 1.4.4 reads it (measured, ADR 0011): the CD matrix when the
    /// header holds any <c>CDi_j</c> card, a missing one being 0 as the WCS standard says; else
    /// <c>CDELT1</c> and <c>CDELT2</c> (1 when missing) times the PC matrix (the identity where a
    /// <c>PC</c> card is missing). A <c>CROTA</c> turn never mirrors, so it is not read. Two
    /// plate solves of opposite orientation are mirror images of each other.
    /// </summary>
    /// <param name="header">The header; not null.</param>
    /// <returns>+1 or -1; null when the header has no <c>CTYPE1</c>, a value read is not a number, or the determinant is 0.</returns>
    internal static int? Orientation(FitsHeader header)
        => Determinant(header) is { } det && det != 0 ? Math.Sign(det) : null;

    /// <summary>The determinant whose sign <see cref="Orientation"/> is, read the same way; the log writes it beside the comparison (R-042).</summary>
    /// <param name="header">The header; not null.</param>
    /// <returns>The determinant, in square degrees per square pixel; null when the header has no <c>CTYPE1</c>, no linear part (neither a <c>CDi_j</c> nor a <c>CDELTi</c> card: the standard's defaults alone are no plate solve), or a value read is not a number.</returns>
    internal static double? Determinant(FitsHeader header)
    {
        ArgumentNullException.ThrowIfNull(header);
        string[] cd = ["CD1_1", "CD1_2", "CD2_1", "CD2_2"];
        bool hasCd = cd.Any(header.Contains);
        if (!header.Contains("CTYPE1") || (!hasCd && !header.Contains("CDELT1") && !header.Contains("CDELT2")))
        {
            return null;
        }

        double? det = hasCd
            ? Value(header, "CD1_1", 0) * Value(header, "CD2_2", 0) - Value(header, "CD1_2", 0) * Value(header, "CD2_1", 0)
            : Value(header, "CDELT1", 1) * Value(header, "CDELT2", 1)
              * (Value(header, "PC1_1", 1) * Value(header, "PC2_2", 1) - Value(header, "PC1_2", 0) * Value(header, "PC2_1", 0));
        return det is { } d && double.IsFinite(d) ? d : null;
    }

    /// <summary>
    /// Whether the destination's own plate solve is the mirror image of the source's (R-017):
    /// both orientations known and opposite. They are compared only when both <c>CTYPE1</c> name
    /// the same first axis (<c>RA</c> in <c>'RA---TAN-SIP'</c>): the same solve written
    /// declination first has the opposite determinant without being a mirror, and a
    /// <c>LINEAR</c> one is no sky solve to set against an RA one.
    /// </summary>
    /// <param name="source">The header the plate solve comes from; not null.</param>
    /// <param name="dest">The header that receives it; not null.</param>
    /// <returns>True for a mirror, false for the same orientation; null when the two cannot be compared (no first axis, different ones, or an orientation unknown).</returns>
    internal static bool? Mirrored(FitsHeader source, FitsHeader dest)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(dest);

        if (FirstAxis(source) is not { } axis || !string.Equals(axis, FirstAxis(dest), StringComparison.Ordinal))
        {
            return null;
        }

        return Orientation(source) is { } s && Orientation(dest) is { } d ? s != d : null;
    }

    /// <summary>The first axis a plate solve names: the text of <c>CTYPE1</c> up to its first '-' (<c>RA</c>, <c>DEC</c>, <c>GLON</c>, or the whole <c>LINEAR</c>).</summary>
    /// <param name="header">The header; not null.</param>
    /// <returns>The axis name; null when the header has no string <c>CTYPE1</c>.</returns>
    private static string? FirstAxis(FitsHeader header)
        => header.GetString("CTYPE1") is { } type ? type.Split('-')[0].Trim(' ') : null;

    /// <summary>A card's number for <see cref="Orientation"/>, or the standard's default when the card is missing.</summary>
    /// <param name="header">The header; not null.</param>
    /// <param name="key">The keyword.</param>
    /// <param name="missing">The value of a missing card: 1 for a scale or a diagonal term, 0 for an off-diagonal one.</param>
    /// <returns>The value; null when the card is there but its value is not a number, so that the determinant is null too.</returns>
    private static double? Value(FitsHeader header, string key, double missing)
        => header.Contains(key) ? header.GetDouble(key) : missing;

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
