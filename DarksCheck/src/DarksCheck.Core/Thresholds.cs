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

namespace DarksCheck.Core;

/// <summary>
/// Every constant a verdict depends on, in one place, so a change is one edit and the design
/// notes can point at it. Each constant carries the one-line fact that set its value; the
/// measurements behind the facts are in <c>docs/validation/</c> and the ADRs.
/// </summary>
internal static class Th
{
    /// <summary>
    /// A pause between the end of one exposure and the start of the next longer than this is
    /// a gap in the series (R-006): back-to-back frames on a DSLR pause under a second; a
    /// longer one means Live View, a menu, or the camera left alone, and the sensor cooled.
    /// </summary>
    internal const double PauseSeconds = 5;

    /// <summary>
    /// Two exposures within this fraction of each other count as the same (R-007): the
    /// D3300 writes 119.2 s for a 120 s setting and 118.5 s once in a while.
    /// </summary>
    internal const double ExposureTolerance = 0.02;

    /// <summary>A bias longer than this is not a bias (R-007); the dark current in it would show.</summary>
    internal const double BiasMaxExposureSeconds = 1.0;

    // --- normalisation (ADR 0004) ---

    /// <summary>ADU are normalised to this ISO and this exposure: the Smida darks' settings, so their figures stay the raw ones.</summary>
    internal const double NormIso = 400;

    internal const double NormExposureSeconds = 120;

    /// <summary>Dark current doubles about every 6 °C: the rule that turns an amplitude ratio into a temperature offset (ADR 0004).</summary>
    internal const double DoublingCelsius = 6;

    // --- the stack (ADR 0007) ---

    /// <summary>
    /// Frames held in memory for the median master, 48 MB each at 24 MP: 60 is 2.9 GB, a
    /// tenth of the owner's 32 GB. Above it every k-th frame is decoded.
    /// </summary>
    internal const int MaxFramesInMemory = 60;

    // --- hot pixels (R-030) ---

    /// <summary>The levels, in ADU equivalent above black, at which pixels are counted: 10 is five quantisation steps of the D3300, 500 the strongest on the Smida master.</summary>
    internal static readonly IReadOnlyList<double> HotLevelsAdu = [10, 20, 50, 100, 500];

    /// <summary>A pixel above this, in ADU equivalent, on the median master is a hot pixel: 13 sigma of the Smida master, 443 pixels in 24 MP (above 19.9 ADU at 119.2 s).</summary>
    internal const double HotAdu = 20;

    /// <summary>The position of <see cref="HotAdu"/> in <see cref="HotLevelsAdu"/>, so the report's per-plane counts follow the threshold.</summary>
    internal static int HotLevelIndex => Enumerable.Range(0, HotLevelsAdu.Count).First(i => HotLevelsAdu[i] == HotAdu);

    /// <summary>
    /// A level in raw ADU never goes under this, whatever the normalisation: at ISO 100 and
    /// 30 s the factor is 1/16 and 20 ADU-eq would be 1.25 ADU, under one quantisation step
    /// of the D3300 (2) and its 1.5 ADU read noise; 5 ADU is above both.
    /// </summary>
    internal const double MinHotAdu = 5;

    /// <summary>A hot pixel still above half its threshold in a frame counts as hot there (the stability histogram).</summary>
    internal const double HotKeepFraction = 0.5;

    /// <summary>A pixel whose value over the frames varies by more than this many quiet sigmas is a random-telegraph candidate.</summary>
    internal const double RtsSigmaFactor = 6;

    /// <summary>A hot pixel above this fraction of the white level is too close to saturation to measure an amplitude ratio (3000 of 4095 on the D3300).</summary>
    internal const double AmplitudeMaxFraction = 0.73;

    /// <summary>A pixel below black minus this, in raw ADU on the master, is dead (the D3300's black is 150; nothing honest sits 50 under it). Raw, not equivalent: a dead pixel does not respond, so it does not scale with exposure or ISO.</summary>
    internal const double DeadBelowAdu = 50;

    // --- maps and banding (R-020, R-040) ---

    /// <summary>Block side, in plane pixels, of the binned maps: 16 (32 full-resolution pixels) gives 125 x 188 blocks on a 24 MP sensor.</summary>
    internal const int BinPx = 16;

    /// <summary>The running median that separates structured banding from the profile's slow curve: 101 rows or columns of the plane.</summary>
    internal const int BandingWindow = 101;

    /// <summary>The central crop, full-resolution pixels, on which the noise of the mean of n frames is measured.</summary>
    internal const int NoiseCropPx = 2048;

    /// <summary>How many frames are averaged for each point of the noise curve; each needs twice as many frames.</summary>
    internal static readonly IReadOnlyList<int> NoiseCurveCounts = [1, 2, 4, 5, 10];

    // --- verdicts (R-020..R-049, ADR 0007) ---

    /// <summary>An edge or corner that departs from the centre by more than this, in ADU equivalent on the dark-minus-bias map, is a leak or a glow when brighter and a readout or bias offset when darker; the Smida map departs by 0.09 and the library's edges by 0.22.</summary>
    internal const double LeakAdu = 1.0;

    /// <summary>A series that warms or cools by more than this many degrees, read from its hot pixels, is flagged; the two measured series moved 2.2 and 3.4 °C.</summary>
    internal const double DriftCelsius = 1.0;

    /// <summary>A frame whose residual noise exceeds the series' median by this factor is an outlier (the Smida frames span 1.17).</summary>
    internal const double NoiseOutlierFactor = 1.5;

    /// <summary>Structured banding (row or column profile minus its running median) above this peak to peak, in ADU equivalent, is a pattern worth a word; the Smida master has 0.39 on the rows and 0.17 on the columns.</summary>
    internal const double BandingAdu = 2.0;

    /// <summary>The noise of the median master is low enough when it is under this fraction of the quantisation step; it adds in quadrature to every calibrated light, so under the step is as low as the file format resolves.</summary>
    internal const double MasterNoiseVsStep = 1.0;

    /// <summary>Fewer darks than this make a poor median whatever the noise figure says: with 10 the median is within 25 % of a mean's efficiency and one odd frame cannot tilt it.</summary>
    internal const int MinDarks = 10;

    /// <summary>The noise of a median over many frames against the mean's: sqrt(pi / 2), the asymptotic value for Gaussian noise (measured 1.40 at 10 frames on Smida, quantisation included).</summary>
    internal const double MedianNoiseFactor = 1.2533;
}
