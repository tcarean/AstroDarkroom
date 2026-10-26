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

// Thresholds.cs - every verdict threshold, in percent.
//
// They are empirical rules, not physical constants: a starting point, to be calibrated on a
// setup you are sure of. Since phase 8 the verdict thresholds are ALL here - before, --light had
// 2.0/5.0 inline and --expo its bands as locals, which made it impossible to see in a single
// place what a decision rests on. A few tolerances still sit beside their code:
// Mosaic.CfaSpreadMin (does a frame look CFA), LensSpec.ApertureToleranceStops and
// SensorFormats.Tolerance (lens and format matching), SyntheticFlatBuilder.SmallExtrapolation
// (when an extrapolation is a large one).

namespace FlatCheck
{
    /// <summary>
    /// The verdict thresholds, as named constants: the residual scale, the smoothing,
    /// --light and its series, the profile tolerances and the exposure bands. Percent
    /// unless the name says otherwise.
    /// </summary>
    static class Th
    {
        // --- structure ---
        // The residual scale, used by all four metrics of deviation from radial symmetry: the
        // spread of the corners, the azimuthal asymmetry, the residual after the model and the
        // tilt. All four measure the same deviation from different angles, and all four are
        // fractional amplitudes in percent, so they share the scale:
        //     under  2 %   perfect quality
        //      2 - 10 %    usable; background extraction removes what is left
        //     10 - 20 %    usable, but confirm with --light before a hard stretch
        //     over  20 %   redo the flats
        public const double ResidPerfect = 2.0;
        public const double ResidUsable = 10.0;
        public const double ResidCheck = 20.0;

        // The threshold from which "local spots" (Metrics.LocalSpots) prints the warning. It is
        // not a verdict: a compact spot fixed on the camera - dust, condensation, a small leak -
        // is right for the session whose lights have it too, and wrong for a library flat. The
        // answer depends on what you do with the flat, not on the flat, so it warns, it does not
        // vote. The same 2% as the PERFECT band: below that neither the shape nor what is fixed
        // on the camera matters.
        public const double LocalSpotWarn = ResidPerfect;

        // The offset of the illumination centre no longer has thresholds, because it is no
        // longer a test. Until 1.2.0 it had 4%/10% on the OK/SUSPECT/PROBLEMA scale, and that was
        // wrong: the offset is ~ -b/(2d), the ratio between gradient and curvature, so it is NOT
        // a fraction and NOT scale-invariant. On a lens with little vignetting it exploded
        // (measured: 31% on a Samyang 135 f/2.8 at 0.15 stops, where the real error was 11%), and
        // when the vertex came to leave the frame it fell to 0.00% = "OK" - so it also became
        // non-monotonic, giving a false negative exactly on the flattest lenses. What is measured
        // now is Th.Resid* on the "tilt": the same gradient, taken as an amplitude.

        // --- smoothing ---
        // The threshold from which the smoothing report warns that more than the texture was
        // removed: the most deviant tile of the removed component (original / smoothed), on the
        // analysis raster. It is not a verdict - the file is written anyway - but the signal that
        // the smoothing took something of large scale too, which the person has to see in the
        // map. Measured at the default scale of 300 px on five real masters: 0.40-0.71 % on the
        // four clean ones, 4.2 % on the one with the fabric texture - so the same 2 % as the
        // PERFECT band leaves a 3x margin in both directions.
        public const double SmoothKeptWarn = ResidPerfect;

        // The scale proposal (SmoothScaleAdvisor), on Metrics.FineTexture - the structured part
        // of the fine component under ~128 px on the sensor, in percent, on the analysis raster.
        // Measured in C# on five real masters (2026-09-16):
        //   input: 0.04-0.06 % on the four without texture, 0.69 % on the one with the diffuser
        //   on a monitor (cells of ~300 px). The 0.20 threshold leaves 3x in both directions.
        //   after smoothing, on the textured one: 0.09 % at 150 px, 0.02 % at 300, 0.01 % at
        //   600; on the clean ones 0.01-0.02 % at any scale. The 0.05 threshold says "it is gone"
        //   from 300 up, with 1.8x against 150 and 2.5x against what is left at 300.
        // Five masters are enough to see that the idea holds, not to calibrate it: a master with
        // a weak texture, of 0.15-0.25 %, falls next to the first threshold.
        public const double SmoothTextureMin = 0.20;    // below this, nothing to smooth
        public const double SmoothTextureGone = 0.05;   // below this, the texture is gone

        // The "scale too large" guard: how much the azimuthal asymmetry and the tilt may DROP,
        // in points, between the master and the smoothed file. They are the two tests the
        // texture does not touch (large sectors, and a plane), so a drop is real structure
        // leaving. Measured: at 300 px on the textured master they move 0.00 and -0.01; at
        // 600 px the real ring of a Nikkor 35 DX drops the asymmetry by 0.79 and the monitor's
        // spots on the Tokina by 0.80. The corners, the residual and the maximum spot can NOT be
        // the guard: they drop when the texture leaves (1.47 points on the maximum spot).
        public const double SmoothScaleLoss = 0.5;

        // --- end-to-end ---
        public const double LightRadOk = 2.0, LightRadBad = 5.0;

        // The suitability guard, on the DIAGONAL of the field covered, in degrees.
        //
        // The test splits the background into "light pollution plane" + "radial terms = flat
        // error". The split holds only as long as the pollution dome is quasi-linear over the
        // frame. On a wide field it CURVES inside the frame, and the radially symmetric part of
        // that curvature has exactly the shape of vignetting - so the fit has no way left to tell
        // them apart, however well it is written.
        //
        // It is not a quality threshold, it is a validity limit: above LightFovMax the mode
        // refuses to answer, it does not give a bad answer.
        public const double LightFovOk = 20.0;      // below this, the split is clean
        public const double LightFovMax = 40.0;     // above this, the test does not apply

        // The same when the header gives only FOCALLEN, without the pixel size: the real field
        // cannot be computed, so the largest plausible sensor is assumed (full-frame, diagonal
        // 43.3 mm) and the focal length is compared directly. Conservative by construction - on
        // a smaller sensor the field is narrower than assumed here, so a good frame can come out
        // "marginal". For that case there is --fov.
        public const double LightFocalOk = 120.0;   // ~20.5 degrees diagonal on full-frame
        public const double LightFocalMin = 60.0;   // ~39.6 degrees diagonal on full-frame
        public const double FullFrameDiagMm = 43.27;

        // The sky gradient (the plane of the fit) above which a warning is printed. It is only
        // INFORMATIVE, not a gate: the slope is an indirect proxy - the real cause of a failed
        // split is the curvature, and that one is covered by the field guard above.
        public const double LightPlaneWarn = 40.0;

        // --- end-to-end over a series of frames ---
        // The mean over the series is a better measurement than any single frame, but only if
        // the SKY's radial term averages to zero over the session. The three conditions below
        // check exactly that; if any fails, the verdict is NEDETERMINAT.
        //
        // These thresholds are the least calibrated in the file: they come from a single real
        // session (2026-08-12, which fails both). Recalibrate them when you have a session you
        // are sure of.
        public const int LightSeriesMinFrames = 5;      // below this a mean means nothing

        // The error of the mean (std/sqrt(n)). If it exceeds even the OK threshold, the series
        // does not resolve the flat to the precision the thresholds speak at - so it does not
        // speak.
        public const double LightSeriesSemMax = LightRadOk;

        // The Pearson correlation between the radial component and the sky gradient, over the
        // series. The flat is the same on every frame by construction: if the "flat error"
        // follows the sky, what is measured is the sky.
        public const double LightSeriesSkyCorr = 0.70;

        // ...but the correlation makes sense only if the gradient really varies over the series.
        // On a clean session where it stays constant, the correlation is noise and would reject
        // good data, so the correlation test applies only above this span (points of %).
        public const double LightSeriesSkySpanMin = 10.0;

        // --- the vignetting profile against what was asked for (synthetic, structure with --raw)
        // ---
        // The profile is chosen "nearest", without interpolation; above these thresholds the
        // report warns that the depth is the profile's, not the requested values'. Only a
        // warning: the exit code does not change. Focal length: 1 mm or 5 %, whichever is larger
        // - the same tolerance with which LensSpec compares a lens's name with the range in the
        // raw (names round). Aperture: half a stop - the Adobe grid is at whole stops, so any
        // request inside the range falls under the threshold, and the warning catches exactly
        // the requests outside it (f/1.0 on a lens with profiles from f/1.8).
        public const double ProfileFocalTolMm = 1.0;
        public const double ProfileFocalTolFrac = 0.05;
        public const double ProfileStopsTol = 0.5;

        // How much the two profile databases differ from each other, on the same lens, at the
        // same aperture, in the corner of the frame: 0.16-0.25 stops on the Tokina 11-20 and the
        // Nikkor 35 DX (docs/design/lens-profiles.md, section 8). It is the uncertainty of the
        // REFERENCE of the depth verdict, and the reason that verdict sits on the residual scale
        // (10 % = BUN) and not on that of the --light mode (5 % = PROBLEMA), although it measures
        // the same quantity: 0.2 stops = 15 % in the corner, three times the PROBLEMA line. It is
        // not a threshold: the report quotes it to say how fine the verdict can be, not to decide
        // anything.
        public const double ProfileDatabaseSpreadStops = 0.2;

        // --- exposure, on the central 20% window of the brightest channel ---
        public const double ExpoClip = 0.05;        // % of pixels saturated = clipping
        // a pixel at or above this fraction of white counts as saturated
        public const double ExpoClipWhite = 0.98;
        public const double ExpoTooLow = 30.0;      // below this the master comes out noisy
        public const double ExpoLo = 50.0;          // target for a panel
        public const double ExpoHi = 65.0;
        public const double ExpoTooHigh = 80.0;
        public const double ExpoTarget = 57.0;      // middle of the target, for the suggestion in stops

        // --- exposure on the sky (blue hour) ---
        public const double ExpoSkyLo = 15.0, ExpoSkyHi = 50.0;
        public const double ExpoSkyTooHigh = 60.0;
        public const double ExpoSkyTarget = 30.0;
    }
}
