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

// The reader of the lensfun database (data/db/*.xml, <lensdatabase version="2">). The <lens>
// elements with <calibration><vignetting model="pa" .../> are taken, the <camera> elements, for
// the crop factor and the mount, and the <mount> definitions, for which mounts a body takes
// (R-116). A lens can have several <mount>s; the canonical name is the <model> without lang, and
// those with lang are search aliases. Nothing else of the database is read (distortion, TCA).
//
// The database is CC BY-SA 3.0 and is shipped unchanged in the lensfun/ folder beside the exe -
// see THIRD-PARTY-NOTICES.md.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NLog;

namespace FlatCheck
{
    /// <summary>A camera body of the lensfun database.</summary>
    /// <param name="Maker">The canonical maker's name.</param>
    /// <param name="Model">The canonical model name.</param>
    /// <param name="Aliases">The model's names in other languages, for searching.</param>
    /// <param name="Mount">The camera's mount; empty when the database gives none.</param>
    /// <param name="CropFactor">The sensor's crop factor against full frame.</param>
    internal sealed record LensfunCamera(string Maker, string Model, IReadOnlyList<string> Aliases, string Mount, double CropFactor);

    /// <summary>
    /// A mount definition of the lensfun database: what the <c>&lt;mount&gt;</c> element at the
    /// root of a file says a body of that mount also takes (the F body's lists the older F
    /// mounts and the adapter mounts M42, T2, DKL; the Z body's lists every F mount).
    /// </summary>
    /// <param name="Name">The mount's name, as the cameras and lenses spell it.</param>
    /// <param name="Compat">The mounts whose lenses fit a body of this mount, the mount itself
    /// not among them.</param>
    internal sealed record LensfunMount(string Name, IReadOnlyList<string> Compat);

    /// <summary>
    /// What was read from the lensfun database: the vignetting profiles, the cameras and the
    /// mount definitions.
    /// </summary>
    internal sealed class LensfunData
    {
        internal readonly IReadOnlyList<LensProfile> Profiles;
        internal readonly IReadOnlyList<LensfunCamera> Cameras;
        internal readonly IReadOnlyList<LensfunMount> Mounts;

        /// <summary>The database's content.</summary>
        /// <param name="profiles">The vignetting profiles, one per calibration entry.</param>
        /// <param name="cameras">The camera bodies with a crop factor.</param>
        /// <param name="mounts">The mount definitions, with their compatible mounts.</param>
        internal LensfunData(IReadOnlyList<LensProfile> profiles, IReadOnlyList<LensfunCamera> cameras, IReadOnlyList<LensfunMount> mounts)
        {
            Profiles = profiles;
            Cameras = cameras;
            Mounts = mounts;
        }

        internal static readonly LensfunData Empty = new LensfunData(Array.Empty<LensProfile>(), Array.Empty<LensfunCamera>(), Array.Empty<LensfunMount>());

        /// <summary>
        /// The crop of the camera of that name (<see cref="CameraOf"/>, R-133) - "NIKON D3300"
        /// from the EXIF finds "Nikon D3300". Null when there is none.
        /// </summary>
        /// <param name="cameraName">The camera's name from the file; null or blank allowed.</param>
        /// <returns>The camera's crop factor, or null.</returns>
        internal double? CropOf(string cameraName) => CameraOf(cameraName)?.CropFactor;

        /// <summary>
        /// The mounts whose lenses fit the camera (R-116): the mount the camera table gives it,
        /// plus those its <c>&lt;mount&gt;</c> definition lists as compatible - for a D3300
        /// "Nikon F AF" with "Nikon F", "Nikon F AI", "Nikon F AI-S", "M42", "M39/1", "DKL",
        /// "T2" and "Generic", never "Nikon Z". A mount without a definition is itself alone.
        /// Null when the camera is not in the table or has no mount there: the caller falls back
        /// to the camera's first word (ADR 0076).
        /// </summary>
        /// <param name="cameraName">The camera's name from the file; null or blank allowed.</param>
        /// <returns>The mount names, compared without case; null when the camera's mount is
        /// not known.</returns>
        internal IReadOnlySet<string> MountsOf(string cameraName)
        {
            string mount = CameraOf(cameraName)?.Mount;
            if (string.IsNullOrEmpty(mount)) return null;
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { mount };
            LensfunMount def = Mounts.FirstOrDefault(m => string.Equals(m.Name, mount, StringComparison.OrdinalIgnoreCase));
            if (def != null) set.UnionWith(def.Compat);
            return set;
        }

        /// <summary>
        /// The camera of that name (R-133, ADR 0101), the names compared as words
        /// (<see cref="Words"/>): the first entry whose model or alias has exactly the words of
        /// <paramref name="cameraName"/>; when there is none, the entry one of whose names
        /// stands whole in it, word for word and side by side ("Nikon D3300" in "Nikon
        /// Corporation NIKON D3300"), the name with the most letters winning and, of two as
        /// long, the first. A table name that only contains the file's name is no match: "Nikon
        /// D7500" is not the camera of "Nikon D750".
        /// </summary>
        /// <param name="cameraName">The camera's name from the file; null or blank allowed.</param>
        /// <returns>The camera, or null when none matches or the name has no letter or
        /// digit.</returns>
        internal LensfunCamera CameraOf(string cameraName)
        {
            string[] wanted = Words(cameraName);
            if (wanted.Length == 0) return null;
            LensfunCamera inside = null;
            int insideLetters = 0;
            foreach (LensfunCamera c in Cameras)
                foreach (string name in new[] { c.Model }.Concat(c.Aliases))
                {
                    string[] words = Words(name);
                    if (words.Length == 0) continue;
                    if (words.AsSpan().SequenceEqual(wanted)) return c;
                    int letters = words.Sum(w => w.Length);
                    if (letters > insideLetters && StandsIn(wanted, words)) { inside = c; insideLetters = letters; }
                }
            return inside;
        }

        /// <summary>A name's words for matching: its runs of letters and digits, upper-cased, so
        /// "PENTAX K-70" is PENTAX, K, 70 and "Nikon Z fc" is NIKON, Z, FC.</summary>
        /// <param name="s">The name; null allowed.</param>
        /// <returns>The words in order; none for a name without a letter or a digit.</returns>
        static string[] Words(string s)
        {
            var words = new List<string>();
            int start = -1;
            string text = s ?? "";
            for (int i = 0; i <= text.Length; i++)
            {
                bool inWord = i < text.Length && char.IsLetterOrDigit(text[i]);
                if (inWord && start < 0) start = i;
                else if (!inWord && start >= 0)
                {
                    words.Add(text.Substring(start, i - start).ToUpperInvariant());
                    start = -1;
                }
            }
            return words.ToArray();
        }

        /// <summary>Whether a run of words stands in another, in order and side by side.</summary>
        /// <param name="all">The words searched in.</param>
        /// <param name="run">The words searched for; not empty.</param>
        /// <returns>True when <paramref name="run"/> is a contiguous part of
        /// <paramref name="all"/>.</returns>
        static bool StandsIn(string[] all, string[] run)
        {
            for (int at = 0; at + run.Length <= all.Length; at++)
                if (all.AsSpan(at, run.Length).SequenceEqual(run)) return true;
            return false;
        }
    }

    /// <summary>Reads the lensfun database's XML files.</summary>
    internal static class LensfunReader
    {
        /// <summary>The diagnostic log: the files that could not be read.</summary>
        static readonly Logger Log = LogManager.GetLogger(nameof(LensfunReader));

        /// <summary>
        /// Every .xml file of a folder, in name order; a file that does not parse is skipped.
        /// </summary>
        /// <param name="dir">The folder of the database's XML files.</param>
        /// <returns>What the files hold together; <see cref="LensfunData.Empty"/> for a missing
        /// folder.</returns>
        internal static LensfunData ReadDirectory(string dir)
        {
            var profiles = new List<LensProfile>();
            var cameras = new List<LensfunCamera>();
            var mounts = new List<LensfunMount>();
            if (!Directory.Exists(dir)) return LensfunData.Empty;
            foreach (string f in Directory.EnumerateFiles(dir, "*.xml").OrderBy(f => f, StringComparer.Ordinal))
            {
                LensfunData d;
                try { d = ReadXml(File.ReadAllText(f), f); }
                catch (Exception e)
                {
                    // A broken file does not stop the list; it is someone else's. The log says which.
                    Log.Warn(e, "the lensfun file " + f + " could not be read; its lenses are left out");
                    continue;
                }
                profiles.AddRange(d.Profiles);
                cameras.AddRange(d.Cameras);
                mounts.AddRange(d.Mounts);
            }
            return new LensfunData(profiles, cameras, mounts);
        }

        /// <summary>
        /// The mount definitions, the cameras with a crop factor and the lenses' "pa" vignetting
        /// entries of one document, the copied-distance pairs collapsed. Throws on XML that does
        /// not parse.
        /// </summary>
        /// <param name="xml">The document's text.</param>
        /// <param name="file">The path recorded as each profile's source.</param>
        /// <returns>The document's content.</returns>
        internal static LensfunData ReadXml(string xml, string file)
        {
            var doc = XDocument.Parse(xml);
            XElement root = doc.Root;
            var profiles = new List<LensProfile>();
            var cameras = new List<LensfunCamera>();
            var mountDefs = new List<LensfunMount>();
            if (root == null) return LensfunData.Empty;

            foreach (XElement mount in root.Elements("mount"))
            {
                string name = mount.Element("name")?.Value?.Trim();
                if (string.IsNullOrEmpty(name)) continue;
                mountDefs.Add(new LensfunMount(name,
                    mount.Elements("compat").Select(c => c.Value.Trim()).Where(c => c.Length > 0).Distinct().ToList()));
            }

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
                // The calibration crop is part of the identity: the same lens calibrated on two
                // formats is in the database twice, and they are two different profiles
                // (LensChoice).
                var choice = new LensChoice(ProfileSource.Lensfun, maker, model, mount, LensChoice.CalibrationLabel(crop.Value));
                var own = new List<LensProfile>();
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
                    own.Add(new LensProfile(choice, aliases, null, focal.Value, aperture.Value,
                        Num((string)v.Attribute("distance")) ?? double.NaN, m, file));
                }
                profiles.AddRange(CollapseCopiedDistances(own));
            }
            return new LensfunData(profiles, cameras, mountDefs);
        }

        /// <summary>
        /// The same (focal length, aperture) at two distances with EXACTLY the same coefficients
        /// is lensfun's convention for "measured at a single distance, not written down": their
        /// calibration script copies the values to 10 and to 1000 m. Two real measurements do not
        /// coincide to four decimals. The pair becomes a single profile with an unknown distance
        /// (NaN), which the report writes "distanta nespecificata" instead of "infinit" - on the
        /// Nikkor 35/1.8G DX the "APS-C" entry said "infinit" and looked like a close wall (the
        /// plateau of 0.16-0.17 stops from f/4 to f/22 is that of the Adobe profile at 0.36 m, not
        /// that at 1.5 m). A single entry at a declared distance keeps its distance.
        /// </summary>
        /// <param name="own">One lens's profiles, as read.</param>
        /// <returns>The profiles with each copied pair (or group) collapsed into one.</returns>
        internal static IReadOnlyList<LensProfile> CollapseCopiedDistances(IReadOnlyList<LensProfile> own)
        {
            var result = new List<LensProfile>();
            foreach (var g in own.GroupBy(p => (p.FocalMm, p.FStop)))
            {
                var list = g.ToList();
                bool copies = list.Count > 1 && list.All(p => SameCoefficients(p.Model, list[0].Model));
                if (copies)
                {
                    LensProfile first = list[0];
                    result.Add(new LensProfile(first.Lens, first.Aliases, first.LensIds, first.FocalMm, first.FStop, double.NaN, first.Model, first.File));
                }
                else result.AddRange(list);
            }
            return result;
        }

        /// <summary>Whether two models carry the same polynomial coefficients, exactly.</summary>
        /// <param name="a">One model.</param>
        /// <param name="b">The other.</param>
        /// <returns>True when both have coefficients and they are equal.</returns>
        static bool SameCoefficients(VignettingModel a, VignettingModel b)
            => a.Coefficients != null && b.Coefficients != null && a.Coefficients.SequenceEqual(b.Coefficients);

        /// <summary>
        /// The element without a lang attribute - the canonical name; otherwise the first.
        /// </summary>
        /// <param name="e">The camera or lens element.</param>
        /// <param name="name">The child element's name ("model", "maker").</param>
        /// <returns>Its trimmed text, or null when absent or empty.</returns>
        static string Canonical(XElement e, string name)
        {
            var all = e.Elements(name).ToList();
            XElement pick = all.FirstOrDefault(x => x.Attribute("lang") == null) ?? all.FirstOrDefault();
            string v = pick?.Value?.Trim();
            return string.IsNullOrEmpty(v) ? null : v;
        }

        /// <summary>
        /// The names in other languages: the child elements with a lang attribute.
        /// </summary>
        /// <param name="e">The camera or lens element.</param>
        /// <param name="name">The child element's name.</param>
        /// <returns>The distinct non-empty aliases.</returns>
        static IReadOnlyList<string> Aliases(XElement e, string name)
            => e.Elements(name).Where(x => x.Attribute("lang") != null).Select(x => x.Value.Trim())
                .Where(v => v.Length > 0).Distinct().ToList();

        /// <summary>A number in the invariant culture, or null.</summary>
        /// <param name="s">The text; null allowed.</param>
        /// <returns>The number, or null when the text is null or not a number.</returns>
        static double? Num(string s)
            => s != null && double.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : null;
    }
}
