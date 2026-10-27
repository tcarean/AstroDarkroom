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

// What is known about the shot - focal length, aperture, the camera's crop, the lens - gathered
// from the reference frame (a FITS master, or a raw given to the console) and from a raw beside
// it (one of flats/, in the window), with the SOURCE of each value, and the catalogue's lenses
// the raw points at by itself.
//
// It exists because this rule lived in a view-model (the prefilling of the Synthetic tab) and
// half in SyntheticRun.Complete, while the vignetting comparison of the Structure tab needed
// exactly the same identification. Two copies would have chosen two different lenses for the
// same folder. Now the console, the Synthetic tab and the Structure tab start from here.
//
// The precedence, the same as before: what the person gives beats what is read from the files;
// for the focal length the reference frame beats the raw beside it (FOCALLEN is what Siril wrote
// for this very master); the aperture is given only by a raw (Siril does not write the f-number);
// the crop comes from the lensfun camera table, by INSTRUME or by what the raw says. Nothing is
// guessed: a missing value stays null, with the reason in its source, and whoever asks for it
// refuses or explains. No pixel is read: both frames can come from ImageLoader.LoadInfo.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NLog;
using static FlatCheck.Ui;

namespace FlatCheck
{
    /// <summary>The shot's identity: the values, their sources, and the candidate lenses.</summary>
    internal sealed class ShotIdentity
    {
        internal double? FocalMm;
        /// <summary>
        /// "--focal", "FOCALLEN", "raw (libraw)" for a reference raw, "raw (DSC_0001.NEF)" for
        /// one beside it.
        /// </summary>
        internal string FocalSource;
        internal double? FStop;
        internal string FStopSource;
        internal double? Crop;
        /// <summary>"--crop", "baza lensfun, Nikon D3300", or why it is not known.</summary>
        internal string CropSource;
        /// <summary>True when the crop came out of the camera table, not from the person.</summary>
        internal bool CropFromTable;
        /// <summary>
        /// The camera's name, from the reference or from the raw; null when neither says it.
        /// </summary>
        internal string Camera;
        /// <summary>
        /// The raw the lens is read from (the reference itself, or the one beside it), or null.
        /// </summary>
        internal LoadedFrame Raw;
        /// <summary>
        /// The name of the raw file beside the reference, for the notes; null when there is none.
        /// </summary>
        internal string RawName;
        /// <summary>
        /// The lenses the raw points at: its lens ID and its lens name, narrowed by range and
        /// mount (R-018). Empty without a raw, or with one that gives neither an ID nor a name.
        /// </summary>
        internal IReadOnlyList<LensChoice> Candidates = Array.Empty<LensChoice>();
        /// <summary>
        /// Where each value came from, in words: "focala din FOCALLEN", "diafragma din
        /// DSC_0001.NEF".
        /// </summary>
        internal IReadOnlyList<string> Notes = Array.Empty<string>();

        /// <summary>
        /// True when the candidates do NOT come from the lens ID, but only from the raw's range
        /// (minimum / maximum focal length, the aperture): the mount's lenses whose name says
        /// exactly that range. It is the case of a manual lens - a Samyang 135 f/2 on a D3300 -
        /// for which the camera writes the "lens without CPU" ID and the range from its menu, and
        /// no profile carries that ID. The list can include other lenses with the same range too
        /// (a Nikkor 135 f/2 DC), and the report says so.
        /// </summary>
        internal bool CandidatesByRangeOnly;

        internal bool HasRawLensId => Raw?.LensId != null;

        /// <summary>
        /// True when the raw names its lens (EXIF LensModel: a Canon or a Sony does; a D3300 with
        /// third-party glass and a Pentax K-70 do not).
        /// </summary>
        internal bool HasRawLensName => !string.IsNullOrWhiteSpace(Raw?.LensName);

        /// <summary>True when the raw says which lens it is, by ID or by name (R-108).</summary>
        internal bool RawNamesLens => HasRawLensId || HasRawLensName;

        /// <summary>
        /// True when the raw gives a whole range: the minimum and maximum focal length and the
        /// aperture.
        /// </summary>
        internal bool HasRawLensRange => Raw?.LensMinFocalMm != null && Raw.LensMaxFocalMm != null && Raw.LensMaxAperture != null;

        /// <summary>
        /// True when a raw was read and it says nothing about the lens: neither a lens ID, nor a
        /// lens name, nor a focal range. What a camera on a telescope, or on a lens without
        /// contacts, writes (a Canon EOS 40D: focal length 0, aperture 0, lens ID 0, no name). A
        /// manual lens on a Nikon is not this case: the body writes ID 74 and the range from its
        /// menu. False without a raw.
        /// </summary>
        internal bool RawSaysNoLens => Raw != null && !RawNamesLens && !HasRawLensRange;

        /// <summary>
        /// "the lens ID 139 from the raw (Nikon F, 11-20 mm f/2.8)", "the lens "EF50mm f/1.4 USM"
        /// (ID 198) from the raw (Canon EF, 50 mm f/1.4)", or, when neither the ID nor the name
        /// gave anything, the range alone (R-108).
        /// </summary>
        internal string RawLensLabel => CandidatesByRangeOnly ? SyntheticReport.RawRangeLabel(Raw) : SyntheticReport.RawLensLabel(Raw);
    }

    /// <summary>
    /// Gathers a <see cref="ShotIdentity"/> from the frames, the profiles and the person's values.
    /// </summary>
    internal static class LensIdentification
    {
        /// <summary>
        /// Gathers what is known. <paramref name="reference"/> is the frame being worked for (the
        /// master, or the raw given to the console); <paramref name="raw"/> is a raw beside it,
        /// or null; <paramref name="rawName"/> its name, for the notes.
        /// </summary>
        /// <param name="reference">The frame worked for; null allowed.</param>
        /// <param name="raw">A raw beside it; null when there is none.</param>
        /// <param name="sources">The profiles found, for the camera table and the candidates; null
        /// allowed.</param>
        /// <param name="focal">The focal length given by the person (--focal), in mm; null when not
        /// given.</param>
        /// <param name="fstop">The f-number given by the person (--fstop); null when not
        /// given.</param>
        /// <param name="crop">The crop factor given by the person (--crop); null when not
        /// given.</param>
        /// <param name="rawName">The beside raw's file name, for the notes; null allowed.</param>
        /// <returns>The identity; values that cannot be known are null, with the reason in their
        /// source.</returns>
        internal static ShotIdentity Identify(LoadedFrame reference, LoadedFrame raw, ProfileSources sources,
                                              double? focal = null, double? fstop = null, double? crop = null,
                                              string rawName = null)
        {
            var id = new ShotIdentity();
            var notes = new List<string>();
            // A raw has an EMPTY FITS header (RawReader.NoHeader), a FITS has at least SIMPLE.
            bool referenceIsRaw = reference != null && (reference.Header == null || reference.Header.Count == 0);
            string besideLabel = $"raw ({rawName ?? "libraw"})";

            if (focal.HasValue) { id.FocalMm = focal; id.FocalSource = "--focal"; }
            else if (reference?.FocalMm != null)
            {
                id.FocalMm = reference.FocalMm;
                id.FocalSource = referenceIsRaw ? "raw (libraw)" : "FOCALLEN";
                notes.Add(referenceIsRaw ? T("focal length from the raw", "focala din raw") : T("focal length from FOCALLEN", "focala din FOCALLEN"));
            }
            else if (raw?.FocalMm != null)
            {
                id.FocalMm = raw.FocalMm;
                id.FocalSource = besideLabel;
                notes.Add(T($"focal length from {rawName ?? "raw"}", $"focala din {rawName ?? "raw"}"));
            }

            if (fstop.HasValue) { id.FStop = fstop; id.FStopSource = "--fstop"; }
            else if (reference?.FNumber != null) { id.FStop = reference.FNumber; id.FStopSource = "raw (libraw)"; }
            else if (raw?.FNumber != null)
            {
                id.FStop = raw.FNumber;
                id.FStopSource = besideLabel;
                notes.Add(T($"aperture from {rawName ?? "raw"}", $"diafragma din {rawName ?? "raw"}"));
            }

            id.Camera = reference?.Camera ?? raw?.Camera;
            if (crop.HasValue) { id.Crop = crop; id.CropSource = "--crop"; }
            else
            {
                double? known = id.Camera == null ? null : sources?.Lensfun?.CropOf(id.Camera);
                if (known.HasValue)
                {
                    id.Crop = known;
                    id.CropSource = T($"lensfun database, {id.Camera}", $"baza lensfun, {id.Camera}");
                    id.CropFromTable = true;
                    notes.Add(T($"crop from the lensfun database ({id.Camera.Trim()})", $"crop din baza lensfun ({id.Camera.Trim()})"));
                }
                else id.CropSource = T("unknown - the profile's calibration sensor is taken",
                                       "necunoscut - se ia senzorul de calibrare al profilului");
            }

            // The lens: from the raw beside, or from the reference when it is itself a raw.
            id.Raw = NamesLens(raw) ? raw
                   : NamesLens(reference) ? reference
                   : raw ?? (referenceIsRaw ? reference : null);
            id.RawName = rawName;
            if (id.RawNamesLens && sources != null)
            {
                var hits = CandidatesFromRaw(sources.Catalog, id.Raw);
                // The same lens exists on several mounts (Adobe: one file per mount). When the
                // camera is known, its mount wins: "Nikon D3300" picks [NIKON CORPORATION] over
                // [Canon]. A word of the camera's name, not a table of mounts - nothing is guessed
                // that is not written in the files.
                string maker = id.Camera?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                if (hits.Count > 1 && !string.IsNullOrEmpty(maker))
                {
                    var byMount = hits.Where(h => (h.Mount + " " + h.Maker).Contains(maker, StringComparison.OrdinalIgnoreCase)).ToList();
                    if (byMount.Count > 0) hits = byMount;
                }
                id.Candidates = PreferCamera(hits, id.Camera);
            }
            // The ID and the name gave nothing (or there are none), but the raw gives the range: the lenses of
            // the camera's mount whose name says exactly that range. Only with the camera known -
            // "135 mm f/2.0" on every mount would be a list, not an identification.
            if (id.Candidates.Count == 0 && id.HasRawLensRange && sources != null && !string.IsNullOrEmpty(id.Camera))
            {
                var byRange = CandidatesByRange(sources.Catalog, id.Raw, id.Camera);
                if (byRange.Count > 0)
                {
                    id.Candidates = byRange;
                    id.CandidatesByRangeOnly = true;
                }
            }
            // A raw that says nothing about the lens is said so here, once, so that the Synthetic
            // tab's note and the Structure report's gap tell the same thing (R-106).
            if (id.RawSaysNoLens) notes.Add(SyntheticReport.RawWithoutLensNote(rawName));
            id.Notes = notes;
            if (Log.IsDebugEnabled) Log.Debug("identified " + LogText.Shot(id));
            return id;
        }

        /// <summary>The diagnostic log: what each identification concluded (R-110).</summary>
        static readonly Logger Log = LogManager.GetLogger(nameof(LensIdentification));

        /// <summary>
        /// The lenses on the camera's mount whose name says exactly the raw's range: both ends of
        /// the focal length within LensSpec's tolerance and the aperture within a quarter stop. A
        /// name that does not give the whole range does not match - here equality is asked for,
        /// not just the absence of a contradiction as in Narrow.
        /// </summary>
        /// <param name="cat">The lens catalogue.</param>
        /// <param name="f">The raw with the lens's range; null gives an empty list.</param>
        /// <param name="camera">The camera's name; its first word is taken as the mount's
        /// maker.</param>
        /// <returns>The matching lenses, the camera's own files preferred.</returns>
        internal static IReadOnlyList<LensChoice> CandidatesByRange(LensCatalog cat, LoadedFrame f, string camera)
        {
            string maker = camera?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (f == null || string.IsNullOrEmpty(maker)) return Array.Empty<LensChoice>();
            var hits = cat.Lenses.Where(l =>
            {
                LensSpec spec = LensSpec.Parse(l.Name);
                return spec.MinFocalMm.HasValue && spec.MaxFocalMm.HasValue && spec.MaxAperture.HasValue
                    && !spec.Contradicts(f.LensMinFocalMm, f.LensMaxFocalMm, f.LensMaxAperture)
                    && (l.Mount + " " + l.Maker).Contains(maker, StringComparison.OrdinalIgnoreCase);
            }).ToList();
            return PreferCamera(hits, camera);
        }

        /// <summary>
        /// True when the frame says which lens made it, by ID or by name; false for null.
        /// </summary>
        /// <param name="f">The frame; null allowed.</param>
        /// <returns>Whether it carries a lens ID or a lens name.</returns>
        static bool NamesLens(LoadedFrame f) => f != null && (f.LensId != null || !string.IsNullOrWhiteSpace(f.LensName));

        /// <summary>
        /// The lenses the raw points at by itself (R-018): those whose name is the raw's lens name
        /// letter for letter (<see cref="LensCatalog.ByLensName"/>) when any of them survives the
        /// range the raw gives, otherwise those that carry its lens ID
        /// (<see cref="LensCatalog.ByLensId"/>, in the files of the camera's mount), minus those
        /// whose name contradicts that range. The name
        /// wins because an ID is shared: Canon's 198 is the EF 50mm f/1.4 USM and three Zeiss ZE
        /// lenses whose names ("Milvus 1.4_25 ZE") no range can read, and every RF lens is 61182;
        /// the ID is the key only where the camera writes no name or the name is in no file. When
        /// the raw gives no name - a Nikon, a Pentax, an older Canon - the lensfun lenses join,
        /// since only the Adobe files carry IDs and the user asked where the Tokina from lensfun
        /// had gone: those of the same makers as the hits whose name does not contradict the
        /// range (the raw's or, when the raw gives none, the one every hit's name agrees on,
        /// <see cref="AgreedRange"/>), and those whose name is letter for letter a hit's name or
        /// alias (Adobe's "smc PENTAX-DA 18-135mm ... AL[IF] DC WR" is lensfun's "smc Pentax-DA
        /// 18-135mm ... AL IF DC WR", under the maker Pentax while the Adobe stub's maker is its
        /// first word, "smc"). When the name decided, it already found the lensfun entry, and a
        /// join by range would pull another lens of the same range (the A-mount 16-35 f/2.8
        /// beside the FE). Adobe first, then lensfun, by name. Empty without an ID and a name.
        /// </summary>
        /// <param name="cat">The lens catalogue.</param>
        /// <param name="f">The raw; null allowed.</param>
        /// <returns>The lenses matched by name and ID, then the lensfun lenses that join them.</returns>
        internal static IReadOnlyList<LensChoice> CandidatesFromRaw(LensCatalog cat, LoadedFrame f)
        {
            if (!NamesLens(f)) return Array.Empty<LensChoice>();
            string hint = f.Camera?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            // The name decides only when a hit of it survives the range: a name hit the range
            // contradicts (a body that writes a lens type's name for a lens of another make) must
            // not hide the ID, which may carry the right file in AlternateLensIDs.
            IReadOnlyList<LensChoice> hits = Array.Empty<LensChoice>();
            bool nameDecided = false;
            if (!string.IsNullOrWhiteSpace(f.LensName))
            {
                hits = LensCatalog.Narrow(cat.ByLensName(f.LensName, hint), f.LensMinFocalMm, f.LensMaxFocalMm, f.LensMaxAperture);
                nameDecided = hits.Count > 0;
            }
            if (!nameDecided && f.LensId != null)
                hits = LensCatalog.Narrow(cat.ByLensId(f.LensId.Value, hint), f.LensMinFocalMm, f.LensMaxFocalMm, f.LensMaxAperture);
            if (hits.Count == 0 || nameDecided) return Ordered(hits);

            var makers = new HashSet<string>(hits.Select(l => l.Maker), StringComparer.OrdinalIgnoreCase);
            var names = new HashSet<string>(hits.SelectMany(h => cat.ProfilesOf(h).SelectMany(p => p.Aliases).Append(h.Name)).Select(LensCatalog.Letters));
            bool rawHasRange = f.LensMinFocalMm != null || f.LensMaxFocalMm != null || f.LensMaxAperture != null;
            var (min, max, ap) = rawHasRange ? (f.LensMinFocalMm, f.LensMaxFocalMm, f.LensMaxAperture) : AgreedRange(hits);
            var lensfun = cat.Lenses.Where(l => l.Source == ProfileSource.Lensfun).ToList();
            IEnumerable<LensChoice> byRange = min == null && max == null && ap == null
                ? Array.Empty<LensChoice>()
                : LensCatalog.Narrow(lensfun.Where(l => makers.Contains(l.Maker)).ToList(), min, max, ap);
            IEnumerable<LensChoice> byName = cat.Profiles
                .Where(p => p.Lens.Source == ProfileSource.Lensfun
                            && (names.Contains(LensCatalog.Letters(p.Lens.Name)) || p.Aliases.Any(a => names.Contains(LensCatalog.Letters(a)))))
                .Select(p => p.Lens);
            return Ordered(hits.Concat(byRange).Concat(byName).Distinct().ToList());
        }

        /// <summary>The candidates in the lists' order: Adobe before lensfun, then by name and mount.</summary>
        /// <param name="lenses">The candidates.</param>
        /// <returns>The same lenses, ordered.</returns>
        static IReadOnlyList<LensChoice> Ordered(IEnumerable<LensChoice> lenses)
            => lenses.OrderBy(l => l.Source).ThenBy(l => l.Name).ThenBy(l => l.Mount).ToList();

        /// <summary>
        /// The range every hit's name states, when they all state the same complete one (focal
        /// ends within LensSpec's tolerance, aperture within its quarter stop); three nulls
        /// otherwise. It stands in for a raw that gives no range (a Pentax K-70), so that the
        /// lensfun join by maker stays as narrow as on a raw that does.
        /// </summary>
        /// <param name="hits">The lenses matched by ID or name.</param>
        /// <returns>The agreed range, or three nulls.</returns>
        static (double? Min, double? Max, double? Aperture) AgreedRange(IReadOnlyList<LensChoice> hits)
        {
            var specs = hits.Select(h => LensSpec.Parse(h.Name)).ToList();
            if (specs.Count == 0 || specs.Any(s => !s.MinFocalMm.HasValue || !s.MaxFocalMm.HasValue || !s.MaxAperture.HasValue))
                return (null, null, null);
            LensSpec first = specs[0];
            return specs.All(s => !s.Contradicts(first.MinFocalMm, first.MaxFocalMm, first.MaxAperture))
                ? (first.MinFocalMm, first.MaxFocalMm, first.MaxAperture)
                : (null, null, null);
        }

        /// <summary>
        /// Among the matches, those whose Adobe file is the camera's own ("NIKON D750
        /// (obiectiv)") beat the mount's generic ones ("NIKON CORPORATION (obiectiv)"): Adobe keeps
        /// profiles per body, with the body's SFF, and Lightroom takes that one. Without a camera,
        /// or without such a file, the list stays as it is.
        /// </summary>
        /// <param name="hits">The matches; null allowed.</param>
        /// <param name="camera">The camera's name; null allowed.</param>
        /// <returns>The camera's own matches when there are any, otherwise
        /// <paramref name="hits"/>.</returns>
        internal static IReadOnlyList<LensChoice> PreferCamera(IReadOnlyList<LensChoice> hits, string camera)
        {
            if (hits == null || hits.Count < 2) return hits;
            string wanted = LensCatalog.Letters(camera);
            if (wanted.Length == 0) return hits;
            var own = hits.Where(h => LensCatalog.Letters(h.Mount).Contains(wanted)).ToList();
            return own.Count > 0 ? own : hits;
        }

        /// <summary>
        /// The first raw of a list of frames, or null: the rule "the raw beside the master", once,
        /// for every tab that needs one.
        /// </summary>
        /// <param name="frames">The frames' paths; null allowed.</param>
        /// <returns>The first path with a raw extension, or null.</returns>
        internal static string FirstRaw(IEnumerable<string> frames)
            => frames?.FirstOrDefault(RawReader.IsRawPath);

        /// <summary>The file name for the notes, or null.</summary>
        /// <param name="path">The path; null allowed.</param>
        /// <returns>The file name, or null.</returns>
        internal static string NameOf(string path) => path == null ? null : Path.GetFileName(path);
    }
}
