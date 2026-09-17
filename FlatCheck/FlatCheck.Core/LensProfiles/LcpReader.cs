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

// Cititorul profilelor Adobe (.lcp): XMP / RDF, un fisier per obiectiv si montura, cu o
// lista de sub-profile (focala x diafragma x distanta). Formatul are doua infatisari, si
// amandoua apar in fisierele instalate: proprietatile ca ATRIBUTE pe un rdf:Description
// (fisierele actuale) sau ca ELEMENTE copil sub un rdf:li cu parseType="Resource" (Anexa A
// din documentul Adobe). Se citesc amandoua. Modelul de vignetare poate fi un element cu
// atribute sau, cand are si tabelul "piecewise", un rdf:Description imbricat.
//
// Ce NU se citeste: distorsiunea, aberatia cromatica, profilele fisheye fara VignetteModel.
// Un fisier stricat nu opreste nimic - se sare si se numara, ca lista sa se faca din ce
// merge; e fisierul altcuiva.
//
// Diafragma e in APEX (ApertureValue): f = 2^(Av/2); 2.970854 -> 2.80. Focala si SFF sunt
// cele din profil, nu din EXIF-ul utilizatorului.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace FlatCheck
{
    internal static class LcpReader
    {
        static readonly XNamespace Rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
        static readonly XNamespace St = "http://ns.adobe.com/photoshop/1.0/camera-profile";
        static readonly XNamespace Ps = "http://ns.adobe.com/photoshop/1.0/";

        internal static IReadOnlyList<LensProfile> Read(string path)
            => ReadXml(File.ReadAllText(path), path);

        internal static IReadOnlyList<LensProfile> ReadXml(string xml, string file)
        {
            var doc = XDocument.Parse(xml);
            var result = new List<LensProfile>();
            foreach (XElement li in doc.Descendants(Ps + "CameraProfiles").Descendants(Rdf + "li"))
            {
                // Proprietatile stau fie ca atribute pe rdf:Description-ul copil, fie ca elemente
                // direct sub rdf:li. Se cauta in amandoua locurile; primul care are ceva castiga.
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
                string lens = Prop("Lens");
                if (lens != null && lens != lensName) aliases.Add(lens);
                var ids = new List<long>();
                if (long.TryParse(Prop("LensID"), NumberStyles.Integer, CultureInfo.InvariantCulture, out long id0)) ids.Add(id0);
                foreach (XElement e in holder.Descendants(St + "AlternateLensIDs").Descendants(Rdf + "li"))
                    if (long.TryParse(e.Value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long id)) ids.Add(id);

                var model = new VignettingModel(VignettingModelKind.Adobe, coeffs, table.Count >= 2 ? table : null,
                    focal.Value, sff,
                    Num(VProp("FocalLengthX")), Num(VProp("FocalLengthY")),
                    Num(VProp("ImageXCenter")) ?? 0.5, Num(VProp("ImageYCenter")) ?? 0.5);

                double fStop = Math.Pow(2, av.Value / 2);
                double distance = Num(Prop("FocusDistance")) ?? double.NaN;
                result.Add(new LensProfile(new LensChoice(ProfileSource.Adobe, LensMaker(lensName, make), lensName, mount),
                    aliases.Distinct().ToList(), ids.Distinct().ToList(), focal.Value, fStop, distance, model, file));
            }
            return result;
        }

        /// <summary>Primul cuvant al numelui de obiectiv - "Tokina", "Nikon" - ca sa se grupeze lista.</summary>
        static string LensMaker(string lensName, string make)
        {
            string[] parts = lensName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 0 ? parts[0] : make;
        }

        static double? Num(string s)
            => s != null && double.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : null;
    }
}
