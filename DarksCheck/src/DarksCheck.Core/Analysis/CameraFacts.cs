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

using static DarksCheck.Core.Ui;

namespace DarksCheck.Core;

/// <summary>
/// Published figures of a camera's sensor (R-080, ADR 0015): the gain in electrons per ADU at
/// each nominal ISO, the full well, the read noise, the quantum efficiency and the ISO of unity
/// gain. The tool measures none of them; they turn ADU into electrons in the scale section and
/// are always printed with their provenance. One camera so far, the owner's Nikon D3300, from
/// photonstophotos.net (Bill Claff), "DxOMark Derived Sensor Characteristics" and "DxOMark
/// Derived Gain versus Measured ISO", read on 2026-10-20 from the pages' own data; the site's
/// caveat is that the method derives the gain from DxOMark's SNR curves and overestimates it
/// on small, noisy sensors.
/// </summary>
/// <param name="Model">The camera's name, as the report prints it.</param>
/// <param name="Gain">The gain at each nominal ISO, in electrons per ADU, in increasing ISO
/// order.</param>
/// <param name="FullWellElectrons">The full well, in electrons.</param>
/// <param name="ReadNoiseElectrons">The read noise, in electrons.</param>
/// <param name="QuantumEfficiencyPercent">The quantum efficiency, in percent.</param>
/// <param name="UnityIso">The measured ISO at which one electron makes one ADU.</param>
internal sealed record CameraFacts(string Model, IReadOnlyList<(double Iso, double ElectronsPerAdu)> Gain, double FullWellElectrons, double ReadNoiseElectrons, double QuantumEfficiencyPercent, double UnityIso)
{
    /// <summary>The provenance, as the report prints it, in the run's language.</summary>
    internal static string SourceWords => T(
        "photonstophotos.net from the DxOMark curves, read 2026-10-20; the method overestimates the gain on small sensors",
        "photonstophotos.net din curbele DxOMark, citit 2026-10-20; metoda supraestimeaza castigul pe senzorii mici");

    /// <summary>
    /// The D3300 on 12 bits: 2^y of the gain chart's y at nominal ISO 100 .. 25600 (3.136, 2.096,
    /// 1.048, 0.074, -0.928, -1.921, -2.942, -3.893, -4.922), full well 34,446 e-, read noise 2.0
    /// e-, QE 56 %, unity gain at measured ISO 841.
    /// </summary>
    private static readonly CameraFacts D3300 = new(
        "Nikon D3300",
        [(100, 8.79), (200, 4.28), (400, 2.07), (800, 1.05), (1600, 0.526), (3200, 0.264), (6400, 0.130), (12800, 0.0673), (25600, 0.0330)],
        34446,
        2.0,
        56,
        841);

    /// <summary>
    /// The facts of the camera a frame names, or null for a camera the table does not know.
    /// </summary>
    /// <param name="camera">The camera name from a frame's metadata, null when unknown; matched
    /// on "D3300", ignoring case.</param>
    /// <returns>The camera's facts, or null.</returns>
    internal static CameraFacts? For(string? camera) =>
        camera is not null && camera.Contains("D3300", StringComparison.OrdinalIgnoreCase) ? D3300 : null;

    /// <summary>
    /// The gain at a nominal ISO, in electrons per ADU: the table's value at a listed ISO,
    /// interpolated on the log2 scale between two, null outside the table.
    /// </summary>
    /// <param name="iso">The nominal ISO.</param>
    /// <returns>The gain in electrons per ADU, or null for an ISO that is not positive or lies
    /// outside the table.</returns>
    internal double? GainAt(double iso)
    {
        if (!(iso > 0) || Gain.Count == 0 || iso < Gain[0].Iso || iso > Gain[^1].Iso)
        {
            return null;
        }

        for (int i = 0; i < Gain.Count; i++)
        {
            if (Math.Abs(Gain[i].Iso - iso) < 1e-9)
            {
                return Gain[i].ElectronsPerAdu;
            }

            if (i + 1 < Gain.Count && iso > Gain[i].Iso && iso < Gain[i + 1].Iso)
            {
                double t = Math.Log2(iso / Gain[i].Iso) / Math.Log2(Gain[i + 1].Iso / Gain[i].Iso);
                return Math.Pow(2, Math.Log2(Gain[i].ElectronsPerAdu) + t * (Math.Log2(Gain[i + 1].ElectronsPerAdu) - Math.Log2(Gain[i].ElectronsPerAdu)));
            }
        }

        return null;
    }
}
