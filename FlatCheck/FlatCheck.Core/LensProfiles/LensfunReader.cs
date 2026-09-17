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

// Cititorul bazei lensfun (data/db/*.xml, <lensdatabase version="2">). Se iau <lens>-urile
// cu <calibration><vignetting model="pa" .../> si <camera>-ele, pentru crop factor. Un
// obiectiv poate avea mai multe <mount>-uri; numele canonic e <model> fara lang, iar cele
// cu lang sunt aliasuri de cautare. Nimic altceva din baza nu se citeste (distorsiune, TCA).
//
// Baza e CC BY-SA 3.0 si e livrata neschimbata in folderul lensfun/ de langa exe - vezi
// THIRD-PARTY-NOTICES.md.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace FlatCheck
{
    internal sealed record LensfunCamera(string Maker, string Model, IReadOnlyList<string> Aliases, string Mount, double CropFactor);

    internal sealed class LensfunData
    {
        internal readonly IReadOnlyList<LensProfile> Profiles;
        internal readonly IReadOnlyList<LensfunCamera> Cameras;

        internal LensfunData(IReadOnlyList<LensProfile> profiles, IReadOnlyList<LensfunCamera> cameras)
        {
            Profiles = profiles;
            Cameras = cameras;
        }

        internal static readonly LensfunData Empty = new LensfunData(Array.Empty<LensProfile>(), Array.Empty<LensfunCamera>());

        /// <summary>
        /// Crop-ul aparatului al carui nume (model sau alias) apare in <paramref name="cameraName"/>
        /// sau il contine - "NIKON D3300" din EXIF gaseste "Nikon D3300" / "D3300". Null cand nu e.
        /// </summary>
        internal double? CropOf(string cameraName)
        {
            if (string.IsNullOrWhiteSpace(cameraName)) return null;
            string wanted = Normalise(cameraName);
            LensfunCamera best = null;
            int bestLen = 0;
            foreach (LensfunCamera c in Cameras)
                foreach (string name in new[] { c.Model }.Concat(c.Aliases))
                {
                    string n = Normalise(name);
                    if (n.Length == 0) continue;
                    // cel mai lung nume care se potriveste castiga: "D3300" bate "D3"
                    if ((wanted.Contains(n) || n.Contains(wanted)) && n.Length > bestLen) { best = c; bestLen = n.Length; }
                }
            return best?.CropFactor;
        }

        static string Normalise(string s) => new string(s.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
    }

    internal static class LensfunReader
    {
        internal static LensfunData ReadDirectory(string dir)
        {
            var profiles = new List<LensProfile>();
            var cameras = new List<LensfunCamera>();
            if (!Directory.Exists(dir)) return LensfunData.Empty;
            foreach (string f in Directory.EnumerateFiles(dir, "*.xml").OrderBy(f => f, StringComparer.Ordinal))
            {
                LensfunData d;
                try { d = ReadXml(File.ReadAllText(f), f); }
                catch (Exception) { continue; }   // un fisier stricat nu opreste lista; e al altcuiva
                profiles.AddRange(d.Profiles);
                cameras.AddRange(d.Cameras);
            }
            return new LensfunData(profiles, cameras);
        }

        internal static LensfunData ReadXml(string xml, string file)
        {
            var doc = XDocument.Parse(xml);
            XElement root = doc.Root;
            var profiles = new List<LensProfile>();
            var cameras = new List<LensfunCamera>();
            if (root == null) return LensfunData.Empty;

            foreach (XElement cam in root.Elements("camera"))
            {
                string model = Canonical(cam, "model");
                double? crop = Num(cam.Element("cropfactor")?.Value);
                if (model == null || !crop.HasValue) continue;
                cameras.Add(new LensfunCamera(Canonical(cam, "maker") ?? "?", model, Aliases(cam, "model"),
                    cam.Element("mount")?.Value?.Trim() ?? "", crop.Value));
            }

            foreach (XElement lens in root.Elements("lens"))
            {
                string model = Canonical(lens, "model");
                double? crop = Num(lens.Element("cropfactor")?.Value);
                XElement cal = lens.Element("calibration");
                if (model == null || !crop.HasValue || cal == null) continue;
                string maker = Canonical(lens, "maker") ?? "?";
                var mounts = lens.Elements("mount").Select(m => m.Value.Trim()).Where(m => m.Length > 0).ToList();
                string mount = mounts.Count > 0 ? string.Join(" / ", mounts) : "";
                var aliases = Aliases(lens, "model");
                var choice = new LensChoice(ProfileSource.Lensfun, maker, model, mount);
                foreach (XElement v in cal.Elements("vignetting"))
                {
                    if ((string)v.Attribute("model") != "pa") continue;
                    double? focal = Num((string)v.Attribute("focal"));
                    double? aperture = Num((string)v.Attribute("aperture"));
                    double? k1 = Num((string)v.Attribute("k1"));
                    if (!focal.HasValue || focal.Value <= 0 || !aperture.HasValue || !k1.HasValue) continue;
                    var m = new VignettingModel(VignettingModelKind.Lensfun,
                        new[] { k1.Value, Num((string)v.Attribute("k2")) ?? 0, Num((string)v.Attribute("k3")) ?? 0 },
                        null, focal.Value, crop.Value);
                    profiles.Add(new LensProfile(choice, aliases, null, focal.Value, aperture.Value,
                        Num((string)v.Attribute("distance")) ?? double.NaN, m, file));
                }
            }
            return new LensfunData(profiles, cameras);
        }

        /// <summary>Elementul fara atribut lang - numele canonic; altfel primul.</summary>
        static string Canonical(XElement e, string name)
        {
            var all = e.Elements(name).ToList();
            XElement pick = all.FirstOrDefault(x => x.Attribute("lang") == null) ?? all.FirstOrDefault();
            string v = pick?.Value?.Trim();
            return string.IsNullOrEmpty(v) ? null : v;
        }

        static IReadOnlyList<string> Aliases(XElement e, string name)
            => e.Elements(name).Where(x => x.Attribute("lang") != null).Select(x => x.Value.Trim())
                .Where(v => v.Length > 0).Distinct().ToList();

        static double? Num(string s)
            => s != null && double.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : null;
    }
}
