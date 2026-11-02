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

// The master's vignetting against the lens's published profiles - the sixth test of the
// structure mode, of another kind than the other five and on another scale.
//
// WHY IT EXISTS. The five tests measure the departure from radial symmetry, and are blind
// by construction to a RADIAL excess: the tablet masters (2026-09-18, Tokina 20 mm
// f/2.8, four orientations 90 degrees apart) came out PERFECT on all five with 1.26 stops
// of vignetting, when the two databases give 0.67-0.92 for this lens - the panel's angular
// falloff, made radial by the rotation. More rotations do not help either: everything that
// rotates with the camera or is symmetrised by the rotation escapes them. The only external
// reference the tool has is the lens's model, and that is the comparison.
//
// HOW IT IS MEASURED. Not by evaluating the polynomial at the corner point, but by building the
// model's raster at the size of the analysis raster (SyntheticFlatBuilder) and running EXACTLY
// Metrics.CornerStats on it - the same operator as on the master, so the difference between "the
// median of a 5 % box" and "the value at a point" does not exist, and the half-pixel artefact
// (known issue 5) is the same on both sides. The cost is ~1.5 M evaluations, tens of ms.
//
// THE VERDICT IS AN ADOBE ROW'S WHEN THE LENS WAS NAMED, THE NEAREST ROW'S OTHERWISE, never the
// worst one's. The two databases differ from each other by ~0.2 stops on the same lens
// (docs/design/lens-profiles.md, section 8). When the raw's ID or name, or --lens, found the
// lens and an Adobe file is among the rows, an Adobe row decides and the lensfun rows are for
// comparison (R-128, ADR 0090): on the owner's Tokina both lensfun entries came out deeper than
// every panel flat allows - the five panel flats read +0.10..+0.19 stops against Adobe and
// -0.15..+0.00 against lensfun, a sign no panel can give - so the nearest let the panel's own
// falloff pass as the lens's. Of two Adobe names of one optic, the file whose own ID is the
// raw's decides. By range alone the Adobe row may be another lens of the range, a chosen lens is
// a single row, and without an Adobe row there is nothing to prefer: there the nearest decides,
// since a master that matches one database is not wrong (ADR 0031). The thresholds are not
// new: the difference in stops goes through Stats.DepthErrorPct (2^|D| - 1) onto the residual
// scale, exactly like the depth of --against. The sign is the diagnosis: plus = the master falls
// more than the model (the source falls with the angle: a panel without a real diffuser, too
// close), minus = less (the source fills the corners: the sky's dome, ambient light, a
// reflection).
//
// WHAT IT CANNOT KNOW, and the report says it: the profile is the lens's model, not the copy;
// it is achromatic, and the master is a 2x2 mean of the mosaic (weighted toward green); a radial
// excess shows neither in the five tests nor in the map. Without a raw beside the master there
// is no lens ID, without the aperture the profile cannot be chosen: then it does not compare,
// it explains why, and the verdict stays the five tests' - "Unknown is not a refusal",
// the rule of --light.

using System;
using System.Collections.Generic;
using System.Linq;

namespace FlatCheck
{
    /// <summary>Why the comparison could not be made. None when it was made.</summary>
    internal enum ComparisonGap
    {
        None,
        /// <summary>No profile on the machine: neither Adobe nor lensfun/.</summary>
        NoProfiles,
        /// <summary>
        /// No raw beside the master (or as a reference), so no lens ID - and no --lens.
        /// </summary>
        NoRaw,
        /// <summary>
        /// A raw was read, but it says nothing about the lens: neither a lens ID nor a focal
        /// range, what a camera on a telescope or on a lens without contacts writes (R-106).
        /// </summary>
        RawWithoutLens,
        /// <summary>The raw has an ID, but no lens in the catalog matches it.</summary>
        NoLensMatch,
        /// <summary>
        /// The focal length is not known (neither FOCALLEN, nor a raw, nor --focal).
        /// </summary>
        NoFocal,
        /// <summary>
        /// The aperture is not known: Siril does not write it, and there was no raw or --fstop.
        /// </summary>
        NoFStop,
        /// <summary>Only lensfun profiles, and the camera's crop is not known.</summary>
        NoCrop,
    }

    /// <summary>A profile set face to face with the master.</summary>
    internal sealed class ProfileDepth
    {
        internal readonly LensProfile Profile;
        /// <summary>
        /// The model's depth at the corner, measured with CornerStats on its raster, in stops.
        /// </summary>
        internal readonly double CornerStops;
        /// <summary>
        /// master - profile, in stops; positive = the master falls more toward the corner than the
        /// model.
        /// </summary>
        internal readonly double DiffStops;
        /// <summary>
        /// 2^|DiffStops| - 1, in percent: the error left in the corner of a light calibrated with
        /// the master, if the model is the truth.
        /// </summary>
        internal readonly double ErrorPct;
        internal readonly string Verdict;
        /// <summary>
        /// The crop the model was built with (0 = the profile's calibration sensor).
        /// </summary>
        internal readonly double CameraCrop;
        /// <summary>
        /// The master's corner against the profile's calibration corner
        /// (SyntheticFlatResult.Extrapolation).
        /// </summary>
        internal readonly double Extrapolation;
        /// <summary>
        /// All the lens's profiles, for the warning "the profile used is far from what the raw
        /// said".
        /// </summary>
        internal readonly IReadOnlyList<LensProfile> LensProfiles;

        /// <summary>
        /// One profile's row: the difference and its verdict through
        /// <see cref="Stats.DepthErrorPct"/> and <see cref="Stats.DepthVerdict"/>.
        /// </summary>
        /// <param name="profile">The profile compared.</param>
        /// <param name="cornerStops">The model's depth at the corner, stops.</param>
        /// <param name="masterStops">The master's depth at the corner, stops.</param>
        /// <param name="cameraCrop">The crop the model was built with; 0 for the calibration
        /// sensor.</param>
        /// <param name="extrapolation">The master's corner against the calibration corner.</param>
        /// <param name="lensProfiles">All the lens's profiles; null for just
        /// <paramref name="profile"/>.</param>
        internal ProfileDepth(LensProfile profile, double cornerStops, double masterStops, double cameraCrop, double extrapolation,
                              IReadOnlyList<LensProfile> lensProfiles = null)
        {
            Profile = profile;
            LensProfiles = lensProfiles ?? new[] { profile };
            CornerStops = cornerStops;
            DiffStops = masterStops - cornerStops;
            ErrorPct = Stats.DepthErrorPct(DiffStops);
            Verdict = Stats.DepthVerdict(DiffStops);
            CameraCrop = cameraCrop;
            Extrapolation = extrapolation;
        }
    }

    /// <summary>
    /// The sixth structure test: the master's corner depth against every candidate lens's
    /// profile, the verdict of the row that decides (R-128), or the gap that kept it from being
    /// made.
    /// </summary>
    internal sealed class VignettingComparison
    {
        internal readonly double MasterStops;
        internal readonly ShotIdentity Shot;
        /// <summary>
        /// One row per candidate lens, in catalog order; empty when it could not compare.
        /// </summary>
        internal readonly IReadOnlyList<ProfileDepth> Profiles;
        /// <summary>
        /// The row that gives the verdict; null when it did not compare. When
        /// <see cref="AdobeDecides"/>, the nearest of the Adobe rows whose own lens ID is the
        /// raw's, or of every Adobe row when none has it; otherwise the row with the smallest
        /// difference in absolute value.
        /// </summary>
        internal readonly ProfileDepth Deciding;
        /// <summary>
        /// True when an Adobe row decides (R-128): there is one, the lenses were not found by
        /// the raw's range alone, and none was chosen. False when the nearest row of all decides.
        /// </summary>
        internal readonly bool AdobeDecides;
        /// <summary>
        /// The comparison's verdict = the deciding row's; null when it did not compare.
        /// </summary>
        internal readonly string Verdict;
        /// <summary>
        /// How much the profiles differ among themselves at the corner, in stops (max - min); 0
        /// with a single profile.
        /// </summary>
        internal readonly double SpreadStops;
        internal readonly ComparisonGap Gap;
        /// <summary>
        /// The lensfun lenses skipped because the crop is not known; usually empty.
        /// </summary>
        internal readonly IReadOnlyList<LensChoice> SkippedForCrop;

        internal bool Compared => Verdict != null;

        /// <summary>
        /// A comparison: picks the row that decides (R-128; a NaN difference never wins) and the
        /// spread between the profiles.
        /// </summary>
        /// <param name="masterStops">The master's depth at the corner, stops.</param>
        /// <param name="shot">What is known of the shot; null when nothing.</param>
        /// <param name="profiles">One row per lens compared; null or empty when none.</param>
        /// <param name="gap">Why nothing was compared, or None.</param>
        /// <param name="skippedForCrop">The lensfun lenses skipped for want of a crop; null for
        /// none.</param>
        internal VignettingComparison(double masterStops, ShotIdentity shot, IReadOnlyList<ProfileDepth> profiles,
                                      ComparisonGap gap, IReadOnlyList<LensChoice> skippedForCrop)
        {
            MasterStops = masterStops;
            Shot = shot;
            Profiles = profiles ?? Array.Empty<ProfileDepth>();
            Gap = gap;
            SkippedForCrop = skippedForCrop ?? Array.Empty<LensChoice>();
            if (Profiles.Count > 0)
            {
                // By range alone the Adobe row may be another lens of the range, and a chosen lens
                // is a single row: there, and without an Adobe row, the nearest decides. A
                // degenerate Adobe row (a NaN difference) is no Adobe row: NaN never decides.
                var adobe = Profiles.Where(p => p.Profile.Lens.Source == ProfileSource.Adobe && !double.IsNaN(p.DiffStops)).ToList();
                AdobeDecides = adobe.Count > 0 && shot?.CandidatesByRangeOnly != true && shot?.ChosenLens == null;
                if (AdobeDecides)
                {
                    // Two Adobe names of one optic carry the same figures (the Tokina's AT-X PRO
                    // DX and atx-i CF): the file whose own ID is the raw's names the lens on it.
                    long? rawId = shot?.Raw?.LensId;
                    var own = rawId == null ? adobe : adobe.Where(p => p.Profile.OwnLensId == rawId).ToList();
                    Deciding = NearestOf(own.Count > 0 ? own : adobe);
                }
                else Deciding = NearestOf(Profiles);
                Verdict = Deciding.Verdict;
                SpreadStops = Profiles.Max(p => p.CornerStops) - Profiles.Min(p => p.CornerStops);
            }
            else SpreadStops = double.NaN;
        }

        /// <summary>
        /// The row with the smallest difference in absolute value; a NaN difference (a
        /// degenerate profile) never wins, which Min on a NaN key could let it.
        /// </summary>
        /// <param name="rows">The rows to choose from; not empty.</param>
        /// <returns>The nearest row, the first in order on a tie.</returns>
        static ProfileDepth NearestOf(IEnumerable<ProfileDepth> rows)
            => rows.OrderBy(p => double.IsNaN(p.DiffStops) ? double.PositiveInfinity : Math.Abs(p.DiffStops)).First();
    }

    /// <summary>
    /// Makes the <see cref="VignettingComparison"/> of a master with the profiles of its lens.
    /// </summary>
    internal static class VignettingComparer
    {
        /// <summary>
        /// Compares the master with the profiles of the lenses in <paramref name="lenses"/> (by
        /// default the identity's candidates: what the raw indicated), each through the profile
        /// nearest to the focal length and the aperture. The gap is the first that applies, in
        /// this order: an empty <paramref name="catalog"/> gives NoProfiles; no lens to compare
        /// gives NoLensMatch when the raw had a lens ID, RawWithoutLens when a raw was read but
        /// says nothing about the lens, NoRaw otherwise; a null catalog then gives NoProfiles;
        /// then NoFocal, NoFStop, and NoCrop or NoLensMatch when no row was made.
        /// </summary>
        /// <param name="master">The master's structure result; its VignetteStops is
        /// compared.</param>
        /// <param name="shot">The shot's identity (lens, focal length, aperture, crop); null when
        /// nothing is known.</param>
        /// <param name="catalog">The profiles on the machine; null when none was loaded.</param>
        /// <param name="lenses">The lenses to compare with; null for the identity's
        /// candidates.</param>
        /// <returns>The comparison, or one carrying the <see cref="ComparisonGap"/> that kept it
        /// from being made; never null.</returns>
        internal static VignettingComparison Analyze(StructureResult master, ShotIdentity shot, LensCatalog catalog,
                                                     IReadOnlyList<LensChoice> lenses = null)
        {
            lenses ??= shot?.Candidates ?? Array.Empty<LensChoice>();
            double masterStops = master.VignetteStops;
            // An empty catalog is the deepest reason; an unknown one (null) is not a reason, so
            // without a raw it says "no raw", which is what the person can fix. A raw that was
            // read and carries no lens is not "no raw": the person would look for a file that is
            // there (the Canon EOS 40D session of 2026-10-26, 17 raws in flats/).
            if (catalog != null && catalog.Lenses.Count == 0)
                return new VignettingComparison(masterStops, shot, null, ComparisonGap.NoProfiles, null);
            if (lenses.Count == 0)
                return new VignettingComparison(masterStops, shot, null,
                    shot != null && shot.RawNamesLens ? ComparisonGap.NoLensMatch
                    : shot != null && shot.RawSaysNoLens ? ComparisonGap.RawWithoutLens
                    : ComparisonGap.NoRaw, null);
            if (catalog == null)
                return new VignettingComparison(masterStops, shot, null, ComparisonGap.NoProfiles, null);
            if (shot?.FocalMm == null)
                return new VignettingComparison(masterStops, shot, null, ComparisonGap.NoFocal, null);
            if (shot.FStop == null)
                return new VignettingComparison(masterStops, shot, null, ComparisonGap.NoFStop, null);

            double crop = shot.Crop ?? 0;
            var rows = new List<ProfileDepth>();
            var skipped = new List<LensChoice>();
            foreach (LensChoice lens in lenses)
            {
                // lensfun normalises the radius to the calibration sensor's half-diagonal, so
                // without the camera's crop it does not know where your corner is; Adobe has the
                // SFF as a fallback.
                if (lens.Source == ProfileSource.Lensfun && !(crop > 0)) { skipped.Add(lens); continue; }
                IReadOnlyList<LensProfile> all = catalog.ProfilesOf(lens);
                LensProfile p = LensCatalog.Nearest(all, shot.FocalMm.Value, shot.FStop.Value);
                if (p == null) continue;
                rows.Add(Measure(p, master, crop, all));
            }
            ComparisonGap gap = rows.Count > 0 ? ComparisonGap.None : skipped.Count > 0 ? ComparisonGap.NoCrop : ComparisonGap.NoLensMatch;
            return new VignettingComparison(masterStops, shot, rows, gap, skipped);
        }

        /// <summary>
        /// A profile's depth at the corner, with the master's operator: the model's raster at
        /// the size of the analysis raster, then CornerStats on it.
        /// </summary>
        /// <param name="p">The profile.</param>
        /// <param name="master">The master's structure result, for the raster size and its
        /// depth.</param>
        /// <param name="crop">The camera's crop factor; 0 for none (Adobe only).</param>
        /// <param name="all">All the lens's profiles, for the mismatch warning; null for just
        /// <paramref name="p"/>.</param>
        /// <returns>The profile's row against the master.</returns>
        internal static ProfileDepth Measure(LensProfile p, StructureResult master, double crop, IReadOnlyList<LensProfile> all = null)
        {
            SyntheticFlatResult built = SyntheticFlatBuilder.Build(p, master.Width, master.Height, crop);
            var (corners, centre) = Metrics.CornerStats(built.Flat);
            double stops = Math.Log2(centre / corners.Values.Average());
            return new ProfileDepth(p, stops, master.VignetteStops, crop, built.Extrapolation, all);
        }
    }
}
