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
/// The settings neither of two compared sets gives (ADR 0060): they are taken as the same, and a
/// row of the report says so.
/// </summary>
[Flags]
internal enum UnsaidSettings
{
    /// <summary>Each setting is given by at least one of the two sets.</summary>
    None = 0,

    /// <summary>Neither set gives the ISO.</summary>
    Iso = 1,

    /// <summary>Neither set gives the exposure.</summary>
    Exposure = 2,
}

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
        if (!Says(info))
        {
            return 1.0;
        }

        double iso = info.Iso!.Value, e = info.ExposureSeconds!.Value;
        double stops = Math.Log2(iso / Th.NormIso);
        double gain = IsoFactorPerStop == 2 ? iso / Th.NormIso : Math.Pow(IsoFactorPerStop, stops);
        return gain * (e / Th.NormExposureSeconds);
    }

    /// <summary>
    /// Whether a frame says the settings its factor is read from: an ISO and an exposure, both
    /// above 0. A frame that does not has the factor 1 of <see cref="NormOf"/>, which stands for
    /// "unknown", not for ISO 400 and 120 s: a set of such frames is compared with another set on
    /// the settings both give, the others taken from that set (<see cref="Relative"/>, ADR 0053,
    /// ADR 0060).
    /// </summary>
    /// <param name="info">The frame's metadata; not null.</param>
    /// <returns>True when both are known and positive.</returns>
    internal static bool Says(FrameInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        return info.Iso is double iso && info.ExposureSeconds is double e && iso > 0 && e > 0;
    }

    /// <summary>Whether a frame gives its ISO, above 0.</summary>
    /// <param name="info">The frame's metadata; not null.</param>
    /// <returns>True when the ISO is known and positive.</returns>
    internal static bool GivesIso(FrameInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        return info.Iso is double iso && iso > 0;
    }

    /// <summary>Whether a frame gives its exposure, above 0.</summary>
    /// <param name="info">The frame's metadata; not null.</param>
    /// <returns>True when the exposure is known and positive.</returns>
    internal static bool GivesExposure(FrameInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        return info.ExposureSeconds is double e && e > 0;
    }

    /// <summary>
    /// Whether a frame lacks a setting another frame gives (ADR 0060): that setting is then taken
    /// from the other frame when the two are compared.
    /// </summary>
    /// <param name="set">The frame that may lack a setting; not null.</param>
    /// <param name="other">The frame it is compared with; not null.</param>
    /// <returns>True when <paramref name="other"/> gives an ISO or an exposure that
    /// <paramref name="set"/> does not.</returns>
    internal static bool Lacks(FrameInfo set, FrameInfo other) =>
        (!GivesIso(set) && GivesIso(other)) || (!GivesExposure(set) && GivesExposure(other));

    /// <summary>The settings neither of two frames gives (ADR 0060).</summary>
    /// <param name="a">One frame; not null.</param>
    /// <param name="b">The other; not null.</param>
    /// <returns>The settings both lack.</returns>
    internal static UnsaidSettings UnsaidBy(FrameInfo a, FrameInfo b) =>
        (GivesIso(a) || GivesIso(b) ? UnsaidSettings.None : UnsaidSettings.Iso)
        | (GivesExposure(a) || GivesExposure(b) ? UnsaidSettings.None : UnsaidSettings.Exposure);

    /// <summary>
    /// A frame's settings with those it does not give taken from another frame (ADR 0060): where
    /// the frame stands when the two are compared. A setting neither gives stays unknown.
    /// </summary>
    /// <param name="set">The frame; not null.</param>
    /// <param name="from">The frame it is compared with; not null.</param>
    /// <returns><paramref name="set"/> with its missing ISO and exposure from
    /// <paramref name="from"/>.</returns>
    internal static FrameInfo TakenFrom(FrameInfo set, FrameInfo from)
    {
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(from);
        return set with
        {
            Iso = GivesIso(set) ? set.Iso : from.Iso,
            ExposureSeconds = GivesExposure(set) ? set.ExposureSeconds : from.ExposureSeconds,
        };
    }

    /// <summary>
    /// The factor of one frame's ADU over another's, from the settings both give (ADR 0060): the
    /// gain per ISO stop raised to the stops between the two ISOs, times the ratio of the two
    /// exposures. A setting either frame does not give counts as the same on both and gives 1.
    /// For two frames that give both settings it is <see cref="NormOf"/> of the one over
    /// <see cref="NormOf"/> of the other.
    /// </summary>
    /// <param name="set">The frame whose ADU are to be brought; not null.</param>
    /// <param name="reference">The frame they are brought to; null for none, which gives 1.</param>
    /// <returns>The factor <paramref name="set"/>'s ADU are divided by to stand beside
    /// <paramref name="reference"/>'s, above 0.</returns>
    internal double Relative(FrameInfo set, FrameInfo? reference)
    {
        ArgumentNullException.ThrowIfNull(set);
        if (reference is null)
        {
            return 1.0;
        }

        double gain = 1.0;
        if (GivesIso(set) && GivesIso(reference))
        {
            double ratio = set.Iso!.Value / reference.Iso!.Value;
            gain = IsoFactorPerStop == 2 ? ratio : Math.Pow(IsoFactorPerStop, Math.Log2(ratio));
        }

        double exposure = GivesExposure(set) && GivesExposure(reference) ? set.ExposureSeconds!.Value / reference.ExposureSeconds!.Value : 1.0;
        return gain * exposure;
    }

    /// <summary>
    /// The temperature offset the doubling rule reads from an amplitude ratio: D x log2(ratio); NaN
    /// for a ratio that is not positive.
    /// </summary>
    /// <param name="ratio">A ratio of two dark-current amplitudes.</param>
    /// <returns>The offset in degrees Celsius, negative for a ratio below 1, or NaN.</returns>
    internal double Degrees(double ratio) => ratio > 0 ? DoublingCelsius * Math.Log2(ratio) : double.NaN;
}
