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

// A selectable vignetting profile - a combination (lens, focal length, aperture, distance) from
// one of the two sources - and the catalogue that holds them all: search by name, grouping by
// lens, choosing the profile nearest to what was asked for.
//
// The choice is "nearest, reported", not interpolation: first the nearest focal length, then the
// nearest aperture in stops, then the largest distance (the flats are at infinity). The report
// always says which profile was used, so that the user sees whether 18 mm stood in for 20.
//
// The Adobe profiles are read LAZILY. Measured on the author's machine: 3531 .lcp files, 455 MB,
// 36 seconds to parse them all - unacceptable for a list. The file name, however, says all that
// is needed for the list and for searching:
//   <Maker>/<Mount>/<Camera> (<Lens>) - RAW.lcp
// so the catalogue keeps only these "stubs" for Adobe and parses a file only when someone asks
// for its profiles (the chosen lens) or looks for a lens ID, and then only in the files of the
// camera's mount. What is lost: the aliases do not enter the search until parsing. The parsed
// files are remembered.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static FlatCheck.Ui;

namespace FlatCheck
{
    /// <summary>Which database a profile comes from.</summary>
    internal enum ProfileSource { Adobe, Lensfun }

    /// <summary>
    /// A lens's identity in a source: what is chosen from the list. <paramref name="Calibration"/>
    /// is the format of the sensor the profile was calibrated on ("APS-C 1.528", "full frame 1"),
    /// and it is part of the identity: the lensfun database holds the same lens, on the same
    /// mount, twice when two people calibrated it on bodies of different formats - 20 of 692 in
    /// the shipped copy, among them the Nikkor 35 f/1.8G DX, whose "media APS-C" entry gives 0.19
    /// stops in the DX corner at f/2.8 and the D780 entry 0.51, next to Adobe's 0.61. With the key
    /// without the crop the two merged, Nearest took the first in the file, and the good
    /// calibration never reached the screen. The label is put on ALL the lensfun lenses, not only
    /// on the doubled ones: a name that would change when the database gets a second entry would
    /// break the text given to --lens. For Adobe it stays empty: the calibration body is already
    /// in the mount ("NIKON D90"), and the SFF sits in the file, not in its name, so it could not
    /// enter the lazily loaded list.
    /// </summary>
    /// <param name="Source">The database.</param>
    /// <param name="Maker">The lens's maker, for grouping.</param>
    /// <param name="Name">The lens's name.</param>
    /// <param name="Mount">The mount (Adobe: the camera or mount of the file).</param>
    /// <param name="Calibration">The calibration format (lensfun); empty for Adobe.</param>
    internal sealed record LensChoice(ProfileSource Source, string Maker, string Name, string Mount, string Calibration = "")
    {
        internal string Key => $"{Source}|{Maker}|{Name}|{Mount}" + (HasCalibration ? $"|{Calibration}" : "");

        internal string SourceLabel => Source == ProfileSource.Adobe ? "Adobe" : "lensfun";

        internal bool HasCalibration => !string.IsNullOrEmpty(Calibration);

        /// <summary>
        /// The mount, plus the calibration when known: "Nikon F AF, calibrat pe full frame 1".
        /// </summary>
        internal string Where => HasCalibration ? T($"{Mount}, calibrated on {Calibration}", $"{Mount}, calibrat pe {Calibration}") : Mount;

        /// <summary>
        /// The name, plus the calibration when known - for sentences that name the chosen profile.
        /// </summary>
        internal string Title => HasCalibration ? T($"{Name} (calibrated on {Calibration})", $"{Name} (calibrat pe {Calibration})") : Name;

        /// <summary>
        /// "APS-C 1.528", "full frame 1", "format neobisnuit 1.37": the nominal format and the
        /// exact crop.
        /// </summary>
        /// <param name="crop">The calibration crop factor.</param>
        /// <returns>The label.</returns>
        internal static string CalibrationLabel(double crop)
            => $"{SensorFormats.LabelOf(crop)} {crop.ToString("0.###", CultureInfo.InvariantCulture)}";

        /// <summary>The lens as the lists show it: name, where, source.</summary>
        /// <returns>"name [where] (source)".</returns>
        public override string ToString() => $"{Name} [{Where}] ({SourceLabel})";
    }

    /// <summary>
    /// A lens's range read from its name - "11-20mm F2.8", "18-200mm f3.5-5.6G", "135mm f/2.0" -
    /// so that it can be set against what a raw's MakerNote says. It exists because a Nikon lens ID
    /// is not unique: 139 is the Tokina 11-20 f/2.8, and the Nikkor 18-200 f/3.5-5.6, and the
    /// Sigma 18-35 f/1.8, and the first choice by ID took the first name in alphabetical order - a
    /// Nikkor - for a raw made with the Tokina. Null for any part the name does not say ("Zeiss
    /// Batis 2/25"); nothing is guessed, and a name that cannot be read contradicts nothing.
    /// </summary>
    /// <param name="MinFocalMm">The shortest focal length, in mm; null when the name does not
    /// say.</param>
    /// <param name="MaxFocalMm">The longest focal length, in mm (the same for a prime); null when
    /// the
    /// name does not say.</param>
    /// <param name="MaxAperture">The widest f-number, at the shortest focal length; null when the
    /// name does not say.</param>
    internal readonly record struct LensSpec(double? MinFocalMm, double? MaxFocalMm, double? MaxAperture)
    {
        // "11-20mm", "18-200 mm", "35mm"; the first occurrence wins.
        static readonly Regex FocalRx = new(@"(\d+(?:\.\d+)?)\s*(?:-\s*(\d+(?:\.\d+)?))?\s*mm\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        // "f/2.8", "F2.8", "f3.5-5.6G" (the aperture at the shortest focal length is taken); not
        // "AF-S", "EF".
        static readonly Regex ApertureRx = new(@"(?<![A-Za-z])[fF]/?\s?(\d+(?:\.\d+)?)", RegexOptions.Compiled);

        /// <summary>
        /// The focal tolerance: 1 mm or 5 %, whichever is larger - names round. The same as
        /// Th.ProfileFocalTol*, a single definition.
        /// </summary>
        internal const double FocalToleranceMm = Th.ProfileFocalTolMm, FocalToleranceFraction = Th.ProfileFocalTolFrac;
        /// <summary>
        /// The aperture tolerance, in stops: the Nikon codes go in sixths of a stop.
        /// </summary>
        internal const double ApertureToleranceStops = 0.25;

        /// <summary>Reads the range from a lens's name.</summary>
        /// <param name="name">The name; null or empty gives an all-null spec.</param>
        /// <returns>The range, each part null when the name does not say it.</returns>
        internal static LensSpec Parse(string name)
        {
            if (string.IsNullOrEmpty(name)) return default;
            double? min = null, max = null, ap = null;
            Match f = FocalRx.Match(name);
            if (f.Success)
            {
                min = double.Parse(f.Groups[1].Value, CultureInfo.InvariantCulture);
                max = f.Groups[2].Success ? double.Parse(f.Groups[2].Value, CultureInfo.InvariantCulture) : min;
            }
            Match a = ApertureRx.Match(name);
            if (a.Success) ap = double.Parse(a.Groups[1].Value, CultureInfo.InvariantCulture);
            return new LensSpec(min, max, ap);
        }

        /// <summary>
        /// True when the name says one range and the raw says another. A part missing on either
        /// side does not contradict.
        /// </summary>
        /// <param name="minFocal">The raw's shortest focal length, in mm; null when
        /// unknown.</param>
        /// <param name="maxFocal">The raw's longest focal length, in mm; null when unknown.</param>
        /// <param name="maxAperture">The raw's widest f-number; null when unknown.</param>
        /// <returns>True on a contradiction beyond the tolerances.</returns>
        internal bool Contradicts(double? minFocal, double? maxFocal, double? maxAperture)
        {
            if (minFocal.HasValue && MinFocalMm.HasValue && !Close(MinFocalMm.Value, minFocal.Value)) return true;
            if (maxFocal.HasValue && MaxFocalMm.HasValue && !Close(MaxFocalMm.Value, maxFocal.Value)) return true;
            if (maxAperture.HasValue && MaxAperture.HasValue
                && Math.Abs(2 * Math.Log2(MaxAperture.Value / maxAperture.Value)) > ApertureToleranceStops) return true;
            return false;
        }

        /// <summary>Whether two focal lengths agree within the focal tolerance.</summary>
        /// <param name="a">One focal length, in mm.</param>
        /// <param name="b">The other, in mm.</param>
        /// <returns>True within 1 mm or 5 % of the larger, whichever is larger.</returns>
        internal static bool Close(double a, double b) => Math.Abs(a - b) <= Math.Max(FocalToleranceMm, FocalToleranceFraction * Math.Max(a, b));

        /// <summary>
        /// "11-20 mm f/2.8", "135 mm f/2.0", "11-20 mm", "" when nothing is known.
        /// </summary>
        /// <param name="minFocal">The shortest focal length, in mm; null allowed.</param>
        /// <param name="maxFocal">The longest focal length, in mm; null allowed.</param>
        /// <param name="maxAperture">The widest f-number; null allowed.</param>
        /// <returns>The range in words; empty when every part is null.</returns>
        internal static string Describe(double? minFocal, double? maxFocal, double? maxAperture)
        {
            string focal = "";
            if (minFocal.HasValue && maxFocal.HasValue)
                focal = Math.Abs(minFocal.Value - maxFocal.Value) < 0.5
                    ? $"{minFocal.Value.ToString("0.#", CultureInfo.InvariantCulture)} mm"
                    : $"{minFocal.Value.ToString("0.#", CultureInfo.InvariantCulture)}-{maxFocal.Value.ToString("0.#", CultureInfo.InvariantCulture)} mm";
            else if (minFocal.HasValue || maxFocal.HasValue)
                focal = $"{(minFocal ?? maxFocal).Value.ToString("0.#", CultureInfo.InvariantCulture)} mm";
            string ap = maxAperture.HasValue ? $"f/{maxAperture.Value.ToString("0.0", CultureInfo.InvariantCulture)}" : "";
            return focal.Length > 0 && ap.Length > 0 ? focal + " " + ap : focal + ap;
        }
    }

    /// <summary>
    /// One vignetting profile: a lens at one focal length, aperture and distance, with its model.
    /// </summary>
    internal sealed class LensProfile
    {
        internal readonly LensChoice Lens;
        internal readonly IReadOnlyList<string> Aliases;
        internal readonly IReadOnlyList<long> LensIds;
        internal readonly double FocalMm;
        internal readonly double FStop;
        /// <summary>
        /// The focus distance in metres; NaN when the profile does not give it - an Adobe one
        /// without FocusDistance, or a lensfun 10 / 1000 m pair with the same coefficients
        /// (LensfunReader.CollapseCopiedDistances), which is "one measurement, distance not
        /// written down".
        /// </summary>
        internal readonly double DistanceM;
        internal readonly VignettingModel Model;
        internal readonly string File;

        /// <summary>A profile.</summary>
        /// <param name="lens">The lens it belongs to.</param>
        /// <param name="aliases">The lens's other names; null for none.</param>
        /// <param name="lensIds">The Nikon lens IDs it carries (Adobe); null for none.</param>
        /// <param name="focalMm">The focal length, in mm.</param>
        /// <param name="fStop">The f-number.</param>
        /// <param name="distanceM">The focus distance in metres; NaN when not given.</param>
        /// <param name="model">The vignetting model.</param>
        /// <param name="file">The file it was read from.</param>
        internal LensProfile(LensChoice lens, IReadOnlyList<string> aliases, IReadOnlyList<long> lensIds,
                             double focalMm, double fStop, double distanceM, VignettingModel model, string file)
        {
            Lens = lens;
            Aliases = aliases ?? Array.Empty<string>();
            LensIds = lensIds ?? Array.Empty<long>();
            FocalMm = focalMm;
            FStop = fStop;
            DistanceM = distanceM;
            Model = model;
            File = file;
        }

        /// <summary>The same profile under another lens identity.</summary>
        /// <param name="lens">The identity to carry.</param>
        /// <returns>A copy with <paramref name="lens"/>.</returns>
        internal LensProfile WithLens(LensChoice lens)
            => new LensProfile(lens, Aliases, LensIds, FocalMm, FStop, DistanceM, Model, File);

        /// <summary>The distance of a profile that does not say it.</summary>
        internal static string DistanceUnspecified => T("unspecified", "nespecificata");

        /// <summary>The distance of a profile at 1000 m and over.</summary>
        internal static string DistanceInfinity => T("infinity", "infinit");

        /// <summary>
        /// "0.36 m" / "infinit" (1000 m and over) / "nespecificata" (NaN) - a single way of
        /// writing the distance, for Settings and for the list.
        /// </summary>
        /// <param name="distanceM">The distance in metres; NaN when unknown.</param>
        /// <returns>The distance in words.</returns>
        internal static string DistanceLabel(double distanceM)
            => double.IsNaN(distanceM) ? DistanceUnspecified
             : distanceM >= 1000 ? DistanceInfinity
             : $"{distanceM.ToString("0.##", CultureInfo.InvariantCulture)} m";

        /// <summary>
        /// "20 mm, f/2.8, infinit" / ", 1.51 m" / ", distanta nespecificata" (NaN: the profile
        /// does not say).
        /// </summary>
        internal string Settings
            => $"{FocalMm.ToString("0.#", CultureInfo.InvariantCulture)} mm, f/{FStop.ToString("0.0", CultureInfo.InvariantCulture)}, "
             + (double.IsNaN(DistanceM) ? T("distance ", "distanta ") : "") + DistanceLabel(DistanceM);
    }

    /// <summary>An Adobe file not parsed yet: what is known from its name and its folder.</summary>
    internal sealed class AdobeProfileFile
    {
        internal readonly string Path;
        internal readonly LensChoice Lens;

        /// <summary>A stub.</summary>
        /// <param name="path">The .lcp file.</param>
        /// <param name="lens">The lens its name says.</param>
        internal AdobeProfileFile(string path, LensChoice lens)
        {
            Path = path;
            Lens = lens;
        }

        /// <summary>
        /// "NIKON CORPORATION (Tokina AT-X 11-20mm F2.8 PRO DX) - RAW.lcp" in the folder
        /// Tokina/Nikon: the camera before the first parenthesis, the lens between the first
        /// parenthesis and its pair (it can contain parentheses: "(IF)"), the maker is the
        /// grandparent folder. A name without parentheses is a camera with a fixed lens, and stays
        /// a
        /// stub with its name.
        /// </summary>
        /// <param name="path">The .lcp file's path.</param>
        /// <returns>The stub; never null.</returns>
        internal static AdobeProfileFile FromName(string path)
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            int open = name.IndexOf(" (", StringComparison.Ordinal);
            int depth = 0, close = -1;
            for (int i = open + 1; open > 0 && i < name.Length; i++)
            {
                if (name[i] == '(') depth++;
                else if (name[i] == ')' && --depth == 0) { close = i; break; }
            }
            if (open <= 0 || close < 0)
            {
                // Phones and compacts with a fixed lens: "Apple/iPhone 6.lcp", "Canon/Canon
                // PowerShot G12 - RAW.lcp". The name is the camera, the maker is the folder; there
                // is no mount.
                string folder = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(path) ?? "") ?? "";
                int dash = name.LastIndexOf(" - ", StringComparison.Ordinal);
                string camera = (dash > 0 ? name.Substring(0, dash) : name).Trim();
                return new AdobeProfileFile(path, new LensChoice(ProfileSource.Adobe, folder.Length > 0 ? folder : camera, camera, ""));
            }
            string make = name.Substring(0, open).Trim();
            string lens = name.Substring(open + 2, close - open - 2).Trim();
            string kind = name.Substring(close + 1).Trim().TrimStart('-').Trim();
            string mount = make + (kind.Length == 0 || kind == "RAW" ? "" : $", {kind}");
            string[] words = lens.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string maker = words.Length > 0 ? words[0] : make;
            return new AdobeProfileFile(path, new LensChoice(ProfileSource.Adobe, maker, lens, mount));
        }
    }

    /// <summary>All the profiles found, from anywhere, and the questions put to the list.</summary>
    internal sealed class LensCatalog
    {
        /// <summary>
        /// The parsed profiles: all the lensfun ones, plus the Adobe ones given already parsed
        /// (tests).
        /// </summary>
        internal readonly IReadOnlyList<LensProfile> Profiles;

        /// <summary>The unparsed Adobe files, known by name.</summary>
        internal readonly IReadOnlyList<AdobeProfileFile> AdobeFiles;

        readonly ConcurrentDictionary<string, IReadOnlyList<LensProfile>> _parsed = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>A catalogue.</summary>
        /// <param name="profiles">The parsed profiles; null for none.</param>
        /// <param name="adobeFiles">The Adobe stubs, parsed on demand; null for none.</param>
        internal LensCatalog(IReadOnlyList<LensProfile> profiles, IReadOnlyList<AdobeProfileFile> adobeFiles = null)
        {
            Profiles = profiles ?? Array.Empty<LensProfile>();
            AdobeFiles = adobeFiles ?? Array.Empty<AdobeProfileFile>();
        }

        internal static readonly LensCatalog Empty = new LensCatalog(Array.Empty<LensProfile>());

        internal IReadOnlyList<LensChoice> Lenses
            => Profiles.Select(p => p.Lens).Concat(AdobeFiles.Select(f => f.Lens)).Distinct()
                .OrderBy(l => l.Source).ThenBy(l => l.Maker).ThenBy(l => l.Name).ThenBy(l => l.Mount).ToList();

        /// <summary>
        /// The lenses whose name, maker, mount or calibration contains ALL the words searched (and
        /// the aliases, for the profiles already parsed), case-insensitively; an empty text gives
        /// them all. Optionally from one source only. The calibration is in the search so that
        /// "Nikkor 35mm full" picks one of the two lensfun entries of the same lens.
        /// </summary>
        /// <param name="text">The words, separated by spaces; null or empty matches all.</param>
        /// <param name="source">The one source to search; null for both.</param>
        /// <returns>The matching lenses, in the catalogue's order.</returns>
        internal IReadOnlyList<LensChoice> Search(string text, ProfileSource? source = null)
        {
            string[] words = (text ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var hits = new List<LensChoice>();
            foreach (LensChoice lens in Lenses)
            {
                if (source.HasValue && lens.Source != source.Value) continue;
                string haystack = lens.Name + " " + lens.Maker + " " + lens.Mount + " " + lens.Calibration + " "
                                + string.Join(" ", Profiles.Where(p => p.Lens == lens).SelectMany(p => p.Aliases).Distinct());
                if (words.All(w => haystack.Contains(w, StringComparison.OrdinalIgnoreCase)))
                    hits.Add(lens);
            }
            return hits;
        }

        /// <summary>A lens's profiles; an Adobe file is parsed now, once.</summary>
        /// <param name="lens">The lens.</param>
        /// <returns>Its profiles by focal length, f-number and distance.</returns>
        internal IReadOnlyList<LensProfile> ProfilesOf(LensChoice lens)
        {
            IEnumerable<LensProfile> eager = Profiles.Where(p => p.Lens == lens);
            IEnumerable<LensProfile> lazy = AdobeFiles.Where(f => f.Lens == lens).SelectMany(Parse);
            return eager.Concat(lazy).OrderBy(p => p.FocalMm).ThenBy(p => p.FStop).ThenBy(p => p.DistanceM).ToList();
        }

        /// <summary>
        /// An Adobe stub's profiles, parsed once and remembered; none for a broken file.
        /// </summary>
        /// <param name="f">The stub.</param>
        /// <returns>Its profiles under the stub's lens identity.</returns>
        IReadOnlyList<LensProfile> Parse(AdobeProfileFile f)
            => _parsed.GetOrAdd(f.Path, path =>
            {
                try { return LcpReader.Read(path).Select(p => p.WithLens(f.Lens)).ToList(); }
                catch (Exception) { return Array.Empty<LensProfile>(); }   // a broken file is not ours; the list goes on
            });

        /// <summary>
        /// The lenses that carry the given lens ID (LensID or AlternateLensIDs; the Adobe files are
        /// the ones that have IDs). It parses the files whose mount or camera contains
        /// <paramref name="makerHint"/> (the Nikon ones for a D3300, the Canon ones for an EOS), in
        /// parallel, or all of them when the hint is missing.
        /// </summary>
        /// <param name="id">The lens ID as the raw gives it (R-108).</param>
        /// <param name="makerHint">A word of the camera's name; null or blank for every
        /// file.</param>
        /// <returns>The lenses with the ID, by name and mount.</returns>
        internal IReadOnlyList<LensChoice> ByLensId(long id, string makerHint = null)
        {
            var hits = new List<LensChoice>(Profiles.Where(p => p.LensIds.Contains(id)).Select(p => p.Lens));
            IEnumerable<AdobeProfileFile> files = AdobeFiles;
            if (!string.IsNullOrWhiteSpace(makerHint))
                files = files.Where(f => (f.Lens.Mount + " " + f.Lens.Maker).Contains(makerHint, StringComparison.OrdinalIgnoreCase));
            var found = new ConcurrentBag<LensChoice>();
            Parallel.ForEach(files, f =>
            {
                if (Parse(f).Any(p => p.LensIds.Contains(id))) found.Add(f.Lens);
            });
            hits.AddRange(found);
            return hits.Distinct().OrderBy(l => l.Name).ThenBy(l => l.Mount).ToList();
        }

        /// <summary>A name with only its letters and digits, upper-cased, for matching.</summary>
        /// <param name="s">The name; null gives an empty string.</param>
        /// <returns>The normalised name.</returns>
        internal static string Letters(string s) => new string((s ?? "").Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

        /// <summary>
        /// The lenses whose name, or one of whose aliases, is <paramref name="name"/> letter for
        /// letter (<see cref="Letters"/>: case and punctuation aside), the lens's maker allowed in
        /// front: the camera's "EF50mm f/1.4 USM" is Adobe's <c>stCamera:Lens</c> alias as it
        /// stands and lensfun's "Canon EF 50mm f/1.4 USM" with the maker; Sony's "FE 16-35mm F2.8
        /// GM" is lensfun's name as it stands and Adobe's with the maker. The Adobe files whose
        /// mount or maker contains <paramref name="makerHint"/> are parsed, in parallel, or all of
        /// them without a hint, as in <see cref="ByLensId"/>; a stub whose file name already spells
        /// the name matches without a parse. Nothing for a blank name (R-018).
        /// </summary>
        /// <param name="name">The lens's name as the raw wrote it; null or blank gives an empty
        /// list.</param>
        /// <param name="makerHint">A word of the camera's name; null or blank for every file.</param>
        /// <returns>The lenses of that name, by name and mount.</returns>
        internal IReadOnlyList<LensChoice> ByLensName(string name, string makerHint = null)
        {
            string want = Letters(name);
            if (want.Length == 0) return Array.Empty<LensChoice>();
            bool Same(LensChoice lens, string candidate)
            {
                string got = Letters(candidate);
                return got == want || got == Letters(lens.Maker) + want;
            }
            bool Matches(LensChoice lens, IEnumerable<string> aliases) => Same(lens, lens.Name) || aliases.Any(a => Same(lens, a));

            var hits = new List<LensChoice>();
            foreach (var group in Profiles.GroupBy(p => p.Lens))
                if (Matches(group.Key, group.SelectMany(p => p.Aliases))) hits.Add(group.Key);
            IEnumerable<AdobeProfileFile> files = AdobeFiles;
            if (!string.IsNullOrWhiteSpace(makerHint))
                files = files.Where(f => (f.Lens.Mount + " " + f.Lens.Maker).Contains(makerHint, StringComparison.OrdinalIgnoreCase));
            var found = new ConcurrentBag<LensChoice>();
            Parallel.ForEach(files, f =>
            {
                if (Same(f.Lens, f.Lens.Name) || Matches(f.Lens, Parse(f).SelectMany(p => p.Aliases))) found.Add(f.Lens);
            });
            hits.AddRange(found);
            return hits.Distinct().OrderBy(l => l.Name).ThenBy(l => l.Mount).ToList();
        }

        /// <summary>
        /// Of the matches by ID or name, only those whose name does NOT contradict the range in the raw
        /// (minimum / maximum focal length, maximum aperture): a name that cannot be read stays.
        /// Without any range, the list stays whole.
        /// </summary>
        /// <param name="hits">The matches.</param>
        /// <param name="minFocal">The raw's shortest focal length, in mm; null when
        /// unknown.</param>
        /// <param name="maxFocal">The raw's longest focal length, in mm; null when unknown.</param>
        /// <param name="maxAperture">The raw's widest f-number; null when unknown.</param>
        /// <returns>The matches that do not contradict the raw.</returns>
        internal static IReadOnlyList<LensChoice> Narrow(IReadOnlyList<LensChoice> hits, double? minFocal, double? maxFocal, double? maxAperture)
            => hits.Where(h => !LensSpec.Parse(h.Name).Contradicts(minFocal, maxFocal, maxAperture)).ToList();

        /// <summary>
        /// The profile nearest to (focal length, aperture) among a lens's: the nearest focal
        /// length, then the nearest aperture in stops, then the largest distance. Null when the
        /// lens has no profile.
        /// </summary>
        /// <param name="profiles">One lens's profiles; null allowed.</param>
        /// <param name="focalMm">The focal length asked for, in mm.</param>
        /// <param name="fStop">The f-number asked for.</param>
        /// <returns>The nearest profile, or null.</returns>
        internal static LensProfile Nearest(IReadOnlyList<LensProfile> profiles, double focalMm, double fStop)
        {
            if (profiles == null || profiles.Count == 0) return null;
            double bestFocal = profiles.Min(p => Math.Abs(p.FocalMm - focalMm));
            var atFocal = profiles.Where(p => Math.Abs(Math.Abs(p.FocalMm - focalMm) - bestFocal) < 1e-9).ToList();
            double bestStops = atFocal.Min(p => Math.Abs(Stops(p.FStop) - Stops(fStop)));
            var atStop = atFocal.Where(p => Math.Abs(Math.Abs(Stops(p.FStop) - Stops(fStop)) - bestStops) < 1e-9).ToList();
            return atStop.OrderByDescending(p => double.IsNaN(p.DistanceM) ? double.NegativeInfinity : p.DistanceM).First();
        }

        /// <summary>
        /// The aperture in stops (log2 f^2), so that f/2.8 and f/4 are one unit apart.
        /// </summary>
        /// <param name="fStop">The f-number.</param>
        /// <returns>The stops.</returns>
        internal static double Stops(double fStop) => 2 * Math.Log2(fStop);
    }
}
