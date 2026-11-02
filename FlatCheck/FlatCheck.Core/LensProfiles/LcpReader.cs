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

// The reader of the Adobe profiles (.lcp): XMP / RDF, one file per lens and mount, with a list
// of sub-profiles (focal length x aperture x distance). The format has two looks, and both occur
// in the installed files: the properties as ATTRIBUTES on an rdf:Description (the current files)
// or as child ELEMENTS under an rdf:li with parseType="Resource" (Appendix A of Adobe's
// document). Both are read. The vignetting model can be an element with attributes or, when it
// also has the "piecewise" table, a nested rdf:Description.
//
// What is NOT read: the distortion, the chromatic aberration, the fisheye profiles without a
// VignetteModel. A broken file stops nothing - the catalog parses it to no profile, with a
// line in the log, so that the list is made from what works; it is someone else's file.
//
// The aperture is in APEX (ApertureValue): f = 2^(Av/2); 2.970854 -> 2.80. The focal length and
// the SFF are the profile's, not those of the user's EXIF.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace FlatCheck
{
    /// <summary>Reads the vignetting sub-profiles of an Adobe .lcp file.</summary>
    internal static class LcpReader
    {
        static readonly XNamespace Rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
        static readonly XNamespace St = "http://ns.adobe.com/photoshop/1.0/camera-profile";
        static readonly XNamespace Ps = "http://ns.adobe.com/photoshop/1.0/";

        /// <summary>The sub-profiles of an .lcp file on disk; throws on unreadable XML.</summary>
        /// <param name="path">The .lcp file.</param>
        /// <returns>One profile per sub-profile that has a vignetting model.</returns>
        internal static IReadOnlyList<LensProfile> Read(string path)
            => ReadXml(File.ReadAllText(path), path);

        /// <summary>
        /// The sub-profiles of an .lcp document, in either of its two looks. A sub-profile without
        /// a VignetteModel, without a focal length above 0 or without an aperture is skipped.
        /// Throws on XML that does not parse.
        /// </summary>
        /// <param name="xml">The document's text.</param>
        /// <param name="file">The path recorded as each profile's source.</param>
        /// <returns>One profile per usable sub-profile, in document order.</returns>
        internal static IReadOnlyList<LensProfile> ReadXml(string xml, string file)
        {
            var doc = XDocument.Parse(xml);
            var result = new List<LensProfile>();
            foreach (XElement li in doc.Descendants(Ps + "CameraProfiles").Descendants(Rdf + "li"))
            {
                // The properties sit either as attributes on the child rdf:Description, or as
                // elements directly under rdf:li. Both places are searched; the first that has
                // something wins.
                XElement desc = li.Element(Rdf + "Description");
                if (desc == null && li.Element(St + "Make") == null && li.Element(St + "LensPrettyName") == null) continue;
                XElement holder = desc ?? li;
                string Prop(string name) => holder.Attribute(St + name)?.Value ?? holder.Element(St + name)?.Value?.Trim();

                XElement vig = holder.Descendants(St + "VignetteModel").FirstOrDefault();
                if (vig == null) continue;
                XElement vigHolder = vig.Element(Rdf + "Description") ?? vig;
                string VProp(string name) => vigHolder.Attribute(St + name)?.Value ?? vigHolder.Element(St + name)?.Value?.Trim();

                double? a1 = Num(VProp("VignetteModelParam1"));
                double[] coeffs = null;
                if (a1.HasValue)
                    coeffs = new[] { a1.Value, Num(VProp("VignetteModelParam2")) ?? 0, Num(VProp("VignetteModelParam3")) ?? 0 };

                var table = new List<(double R, double V)>();
                XElement piece = vig.Descendants(St + "VignetteModelPiecewiseParam").FirstOrDefault();
                if (piece != null)
                    foreach (XElement pt in piece.Descendants(Rdf + "li"))
                    {
                        string[] parts = pt.Value.Split(',');
                        if (parts.Length == 2 && Num(parts[0]) is double r && Num(parts[1]) is double v)
                            table.Add((r, v));
                    }
                if (coeffs == null && table.Count < 2) continue;

                double? focal = Num(Prop("FocalLength"));
                double? av = Num(Prop("ApertureValue"));
                double sff = Num(Prop("SensorFormatFactor")) ?? 1.0;
                if (!focal.HasValue || focal.Value <= 0 || !av.HasValue) continue;

                string make = Prop("Make") ?? Prop("CameraPrettyName") ?? "?";
                string lensName = Prop("LensPrettyName") ?? Prop("Lens") ?? "?";
                string mount = Prop("CameraPrettyName") ?? make;
                var aliases = holder.Descendants(St + "AlternateLensNames").Descendants(Rdf + "li").Select(e => e.Value.Trim()).ToList();
                // The pretty name and stCamera:Lens both go in the aliases: a lazily parsed file is
                // listed under the name its FILE name spells, so the name read from the raw is set
                // against the aliases (LensCatalog.ByLensName), and LensPrettyName may differ from
                // the file name in more than letters.
                string lens = Prop("Lens");
                if (lensName != "?") aliases.Add(lensName);
                if (lens != null) aliases.Add(lens);
                var ids = new List<long>();
                long? ownId = ParseLensId(Prop("LensID"));
                if (ownId is long id0) ids.Add(id0);
                foreach (XElement e in holder.Descendants(St + "AlternateLensIDs").Descendants(Rdf + "li"))
                    if (ParseLensId(e.Value) is long id) ids.Add(id);

                var model = new VignettingModel(VignettingModelKind.Adobe, coeffs, table.Count >= 2 ? table : null,
                    focal.Value, sff,
                    Num(VProp("FocalLengthX")), Num(VProp("FocalLengthY")),
                    Num(VProp("ImageXCenter")) ?? 0.5, Num(VProp("ImageYCenter")) ?? 0.5);

                double fStop = Math.Pow(2, av.Value / 2);
                double distance = Num(Prop("FocusDistance")) ?? double.NaN;
                result.Add(new LensProfile(new LensChoice(ProfileSource.Adobe, LensMaker(lensName, make), lensName, mount),
                    aliases.Distinct().ToList(), ids.Distinct().ToList(), focal.Value, fStop, distance, model, file, ownId));
            }
            return result;
        }

        /// <summary>
        /// A <c>stCamera:LensID</c> or <c>AlternateLensIDs</c> entry as one number: one integer as
        /// it is (Canon's 198, Nikon's 139); two integers of a byte each, Pentax's form ("8 215"),
        /// as a*256+b, the number libraw builds from the same two bytes (2263, measured on a K-70,
        /// R-108). Anything else is null, so an entry this reader does not understand drops out
        /// instead of matching a wrong lens.
        /// </summary>
        /// <param name="text">The entry's text; null or blank gives null.</param>
        /// <returns>The lens ID, or null.</returns>
        internal static long? ParseLensId(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            string[] parts = text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1)
                return long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out long one) ? one : null;
            if (parts.Length == 2
                && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int a) && a >= 0 && a <= 255
                && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int b) && b >= 0 && b <= 255)
                return a * 256 + b;
            return null;
        }

        /// <summary>
        /// The first word of the lens's name - "Tokina", "Nikon" - so that the list can be
        /// grouped.
        /// </summary>
        /// <param name="lensName">The lens's pretty name.</param>
        /// <param name="make">The camera make, used when the name is blank.</param>
        /// <returns>The lens's maker.</returns>
        static string LensMaker(string lensName, string make)
        {
            string[] parts = lensName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 0 ? parts[0] : make;
        }

        /// <summary>A number in the invariant culture, or null.</summary>
        /// <param name="s">The text; null allowed.</param>
        /// <returns>The number, or null when the text is null or not a number.</returns>
        static double? Num(string s)
            => s != null && double.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : null;
    }
}
