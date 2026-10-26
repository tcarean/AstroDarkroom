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

// The synthetic flat's chain: the reference frame (size, cards, what it knows about the shot)
// -> the completed request (focal length, aperture, crop, each with its source) -> the choice
// of the lens and of the profile -> raster -> report. The file is written by the caller,
// through WriteFits, after it has answered the overwrite question - the same reason as for
// --smooth. The refusals are text, as in the other runs, and do not throw.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static FlatCheck.Ui;

namespace FlatCheck
{
    /// <summary>What was asked for, with what was completed from the reference frame and from
    /// where.</summary>
    internal sealed class SyntheticRequest
    {
        internal string LensText;
        internal ProfileSource? Source;
        internal LensChoice Lens;
        internal double FocalMm;
        internal string FocalSource;
        internal double FStop;
        internal string FStopSource;
        internal double CameraCrop;
        internal string CropSource;
    }

    /// <summary>
    /// The result of a <c>--synthetic</c> run: the raster built, the completed request and
    /// the reference frame, or the refusal.
    /// </summary>
    internal sealed class SyntheticRunOutcome
    {
        internal readonly SyntheticFlatResult Result;
        internal readonly SyntheticRequest Request;
        internal readonly LoadedFrame Frame;
        internal readonly string Refusal;
        /// <summary>The refusal is about the CHOICE of the lens (none / too many): "read the
        /// list", code 2, not 1.</summary>
        internal readonly bool RefusedChoice;
        internal bool Refused => Result == null;

        /// <summary>Bundles a run's parts, or a refusal.</summary>
        /// <param name="r">The synthetic flat, or null on a refusal.</param>
        /// <param name="req">The request, completed as far as it got.</param>
        /// <param name="f">The reference frame, as loaded.</param>
        /// <param name="refusal">The refusal's text, or null.</param>
        /// <param name="refusedChoice">True when the refusal is about choosing the
        /// lens.</param>
        internal SyntheticRunOutcome(SyntheticFlatResult r, SyntheticRequest req, LoadedFrame f, string refusal, bool refusedChoice = false)
        {
            Result = r;
            Request = req;
            Frame = f;
            Refusal = refusal;
            RefusedChoice = refusedChoice;
        }
    }

    /// <summary>
    /// Mode 7, <c>--synthetic</c>: builds a radial flat from the lens's Adobe or lensfun
    /// profile, at the size of a reference frame, and writes it on request.
    /// </summary>
    internal static class SyntheticRun
    {
        /// <summary>
        /// Completes the request from the shot's identity: each value with its source, so the
        /// report says where it came from. Null when everything could be completed; otherwise
        /// the refusal. The identification itself is in LensIdentification, shared with the
        /// window.
        /// </summary>
        /// <param name="req">The request to complete; its focal length, aperture and crop
        /// fields are written.</param>
        /// <param name="id">What the frame, the raw and the options say about the
        /// shot.</param>
        /// <param name="lensfunNeedsCrop">True when the chosen lens is a lensfun one, whose
        /// profile needs the camera's crop factor.</param>
        /// <returns>Null when complete, otherwise the refusal's text (no focal length, no
        /// aperture, or no crop for lensfun).</returns>
        internal static string Complete(SyntheticRequest req, ShotIdentity id, bool lensfunNeedsCrop)
        {
            if (!id.FocalMm.HasValue) return SyntheticReport.NoFocal();
            req.FocalMm = id.FocalMm.Value;
            req.FocalSource = id.FocalSource;

            if (!id.FStop.HasValue) return SyntheticReport.NoFStop();
            req.FStop = id.FStop.Value;
            req.FStopSource = id.FStopSource;

            if (id.Crop.HasValue) { req.CameraCrop = id.Crop.Value; req.CropSource = id.CropSource; }
            else if (lensfunNeedsCrop) return SyntheticReport.NoCrop(id.Camera);
            else { req.CameraCrop = 0; req.CropSource = id.CropSource; }
            return null;
        }

        /// <summary>
        /// Chooses the lens from the text: exactly one hit, otherwise a refusal with the list.
        /// Without a text, the candidates the raw pointed to (the identity), narrowed by the
        /// source asked for. Sets <c>req.Lens</c> on success.
        /// </summary>
        /// <param name="req">The request: its lens text and source are read, its lens is
        /// written.</param>
        /// <param name="sources">The profile catalog.</param>
        /// <param name="f">The reference frame; not read by this overload, the identity
        /// carries what it knows.</param>
        /// <param name="id">The shot's identity, with the candidates and the camera.</param>
        /// <returns>Null when exactly one lens was chosen, otherwise the refusal's text (none
        /// given, no match, or ambiguous).</returns>
        internal static string ChooseLens(SyntheticRequest req, ProfileSources sources, LoadedFrame f, ShotIdentity id)
        {
            LensCatalog cat = sources.Catalog;
            IReadOnlyList<LensChoice> hits;
            if (string.IsNullOrWhiteSpace(req.LensText))
            {
                hits = id.Candidates;
                if (req.Source.HasValue) hits = hits.Where(h => h.Source == req.Source.Value).ToList();
                if (hits.Count == 0) return SyntheticReport.NoLensGiven();
            }
            else
            {
                hits = MatchLenses(req.LensText, req.Source, sources, id);
                if (hits.Count == 0) return SyntheticReport.NoMatch(req.LensText, sources);
            }
            if (hits.Count > 1) return SyntheticReport.Ambiguous(req.LensText != null ? $"\"{req.LensText}\"" : id.RawLensLabel, hits);
            req.Lens = hits[0];
            return null;
        }

        /// <summary>
        /// The lenses that match a text, narrowed the way the choice narrows them: an exact
        /// match on the name beats the partial ones ("Tokina AT-X 11-20mm F2.8 PRO DX" is also
        /// contained in the name of the "II" variant), the camera's mount when known, then the
        /// body's own file. Without the "exactly one" rule: --synthetic asks for it, the
        /// comparison of the structure mode compares with all that remain.
        /// </summary>
        /// <param name="text">The lens text (<c>--lens</c>); not blank.</param>
        /// <param name="source">The profile source to keep, or null for both.</param>
        /// <param name="sources">The profile catalog.</param>
        /// <param name="id">The shot's identity, for the camera; null allowed.</param>
        /// <returns>The lenses left, possibly empty.</returns>
        internal static IReadOnlyList<LensChoice> MatchLenses(string text, ProfileSource? source, ProfileSources sources, ShotIdentity id)
        {
            IReadOnlyList<LensChoice> hits = sources.Catalog.Search(text, source);
            if (hits.Count == 0) return hits;
            var exact = hits.Where(h => string.Equals(h.Name, text.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
            if (exact.Count > 0) hits = exact;
            string makerWord = id?.Camera?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (hits.Count > 1 && !string.IsNullOrEmpty(makerWord))
            {
                var byMount = hits.Where(h => (h.Mount + " " + h.Maker).Contains(makerWord, StringComparison.OrdinalIgnoreCase)).ToList();
                if (byMount.Count > 0) hits = byMount;
            }
            return LensIdentification.PreferCamera(hits, id?.Camera);
        }

        /// <summary>For the tests: identifies first, then chooses.</summary>
        /// <param name="req">The request: its lens is written on success.</param>
        /// <param name="sources">The profile catalog.</param>
        /// <param name="f">The reference frame, identified here.</param>
        /// <returns>Null when exactly one lens was chosen, otherwise the refusal's
        /// text.</returns>
        internal static string ChooseLens(SyntheticRequest req, ProfileSources sources, LoadedFrame f)
            => ChooseLens(req, sources, f, LensIdentification.Identify(f, null, sources));

        /// <summary>
        /// Loads the reference frame and runs the chain on it. Throws what
        /// <see cref="ImageLoader.Load"/> throws; the refusals are returned, not thrown.
        /// </summary>
        /// <param name="referencePath">The frame whose size and cards the synthetic flat takes
        /// (normally the master).</param>
        /// <param name="req">The request: a lens text, or a lens already chosen.</param>
        /// <param name="sources">The profile catalog.</param>
        /// <param name="focal">The focal length in mm given by hand, or null.</param>
        /// <param name="fstop">The f-number given by hand, or null.</param>
        /// <param name="crop">The crop factor given by hand, or null.</param>
        /// <param name="report">Where the report's text is written.</param>
        /// <returns>The synthetic flat with its request, or the refusal.</returns>
        internal static SyntheticRunOutcome Execute(string referencePath, SyntheticRequest req, ProfileSources sources,
                                                    double? focal, double? fstop, double? crop, TextWriter report)
        {
            LoadedFrame f = ImageLoader.Load(referencePath);
            return Execute(f, referencePath, req, sources, focal, fstop, crop, report);
        }

        /// <summary>
        /// The chain on a frame already loaded: refuses a cube, identifies the shot once,
        /// chooses the lens when the request has none, completes the request, picks the
        /// nearest profile and builds the raster; writes the report or the refusal.
        /// </summary>
        /// <param name="f">The reference frame, as loaded.</param>
        /// <param name="referencePath">Its path, for the report.</param>
        /// <param name="req">The request: a lens text, or a lens already chosen (the
        /// window's list).</param>
        /// <param name="sources">The profile catalog.</param>
        /// <param name="focal">The focal length in mm given by hand, or null.</param>
        /// <param name="fstop">The f-number given by hand, or null.</param>
        /// <param name="crop">The crop factor given by hand, or null.</param>
        /// <param name="report">Where the report's text is written.</param>
        /// <returns>The synthetic flat with its request, or the refusal.</returns>
        internal static SyntheticRunOutcome Execute(LoadedFrame f, string referencePath, SyntheticRequest req, ProfileSources sources,
                                                    double? focal, double? fstop, double? crop, TextWriter report)
        {
            string refusal = f.Planes > 1 ? SyntheticReport.RefusalCube(referencePath, f.Planes) : null;
            bool choice = false;
            // A single identification for the choice and the completion: ByLensId parses the
            // mount's Adobe files, and that is not something to do twice.
            ShotIdentity id = LensIdentification.Identify(f, null, sources, focal, fstop, crop);
            // The window gives the lens already chosen from the list (req.Lens); the console
            // looks it up from the text.
            if (refusal == null && req.Lens == null)
            {
                refusal = ChooseLens(req, sources, f, id);
                choice = refusal != null;
            }
            refusal ??= Complete(req, id, req.Lens.Source == ProfileSource.Lensfun);
            if (refusal != null)
            {
                report.WriteLine();
                report.WriteLine(refusal);
                return new SyntheticRunOutcome(null, req, f, refusal, choice);
            }
            IReadOnlyList<LensProfile> all = sources.Catalog.ProfilesOf(req.Lens);
            LensProfile profile = LensCatalog.Nearest(all, req.FocalMm, req.FStop);
            var r = SyntheticFlatBuilder.Build(profile, f.Image.W, f.Image.H, req.CameraCrop);
            r.LensProfiles = all;
            SyntheticReport.Write(r, req, referencePath, report);
            return new SyntheticRunOutcome(r, req, f, null);
        }

        /// <summary>The HISTORY lines: the version, the lens and profile, the model, the
        /// reference file.</summary>
        /// <param name="o">A run that was not refused.</param>
        /// <param name="referencePath">The reference frame; only its file name is
        /// written.</param>
        /// <returns>Four lines, in the run's language.</returns>
        internal static IReadOnlyList<string> History(SyntheticRunOutcome o, string referencePath)
        {
            LensProfile p = o.Result.Profile;
            return new[]
            {
                T($"{VersionInfo.Banner} --synthetic: synthetic flat from the vignetting profile, source {p.Lens.SourceLabel}",
                  $"{VersionInfo.Banner} --synthetic: flat sintetic din profilul de vignetare, sursa {p.Lens.SourceLabel}"),
                T($"lens: {p.Lens.Name} [{p.Lens.Where}]; profile: {p.Settings}; requested: {SyntheticReport.Requested(o.Request)}",
                  $"obiectiv: {p.Lens.Name} [{p.Lens.Where}]; profil: {p.Settings}; cerut: {SyntheticReport.Requested(o.Request)}"),
                $"model: {p.Model.Describe()}",
                T($"geometry and cards from: {Path.GetFileName(referencePath)}", $"geometrie si carduri din: {Path.GetFileName(referencePath)}"),
            };
        }

        /// <summary>
        /// Writes the synthetic flat, with the reference's cards and the HISTORY lines. Always
        /// float: the flat is a factor around 1, not ADU.
        /// </summary>
        /// <param name="o">A run that was not refused.</param>
        /// <param name="referencePath">The reference frame, named in the HISTORY.</param>
        /// <param name="s">The stream to write the FITS into; flushed, not closed.</param>
        internal static void WriteFits(SyntheticRunOutcome o, string referencePath, Stream s)
            => FitsWriter.Write(s, o.Result.Flat, -32, o.Frame.Cards, History(o, referencePath));
    }
}
