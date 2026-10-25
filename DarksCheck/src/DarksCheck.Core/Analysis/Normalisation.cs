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
/// The two settings of ADR 0004 the window exposes (R-076): how the sensor's gain grows per
/// ISO stop (2 = linear in ISO, the D3300's; the normalisation to ISO 400 divides by this
/// factor raised to the stops above 400) and how many degrees double the dark current
/// (<see cref="Th.DoublingCelsius"/>). The exposure factor is always linear. The console
/// runs with <see cref="Default"/>.
/// </summary>
/// <param name="IsoFactorPerStop">The factor the gain grows by per ISO stop, above 0; 2 means
/// linear in ISO.</param>
/// <param name="DoublingCelsius">The degrees Celsius that double the dark current, above 0.</param>
internal sealed record Normalisation(double IsoFactorPerStop = 2, double DoublingCelsius = Th.DoublingCelsius)
{
    internal static readonly Normalisation Default = new();

    /// <summary>
    /// The factor that turns a frame's ADU into ADU equivalent at ISO 400 and 120 s, or 1 when the
    /// frame does not say its ISO or its exposure.
    /// </summary>
    /// <param name="info">The frame's metadata; not null.</param>
    /// <returns>The factor a frame's ADU are divided by, above 0; 1 also for an ISO or an
    /// exposure that is not positive.</returns>
    internal double NormOf(FrameInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        if (info.Iso is not double iso || info.ExposureSeconds is not double e || iso <= 0 || e <= 0)
        {
            return 1.0;
        }

        double stops = Math.Log2(iso / Th.NormIso);
        double gain = IsoFactorPerStop == 2 ? iso / Th.NormIso : Math.Pow(IsoFactorPerStop, stops);
        return gain * (e / Th.NormExposureSeconds);
    }

    /// <summary>
    /// The temperature offset the doubling rule reads from an amplitude ratio: D x log2(ratio); NaN
    /// for a ratio that is not positive.
    /// </summary>
    /// <param name="ratio">A ratio of two dark-current amplitudes.</param>
    /// <returns>The offset in degrees Celsius, negative for a ratio below 1, or NaN.</returns>
    internal double Degrees(double ratio) => ratio > 0 ? DoublingCelsius * Math.Log2(ratio) : double.NaN;
}
