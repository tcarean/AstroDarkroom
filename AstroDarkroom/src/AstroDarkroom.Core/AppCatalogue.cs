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

namespace AstroDarkroom.Core;

/// <summary>
/// The five applications of the suite, compiled in, in the order of the workflow: the
/// calibration frames first (darks, flats), then the lights, then the processed result and
/// the print (R-001). SuiteTests keeps the list in step with the components kit.json bundles
/// and with the executable names their project files build (the rule "The catalogue and
/// kit.json agree"). The texts are Romanian without diacritics (ADR 0003).
/// </summary>
public static class AppCatalogue
{
    /// <summary>The apps, in workflow order.</summary>
    public static IReadOnlyList<SuiteApp> Apps { get; } =
    [
        new("DarksCheck", "DarksCheck", "Verifica dark-urile unei sesiuni sau ale unei biblioteci", "DarksCheck.Wpf.exe"),
        new("FlatCheck", "FlatCheck", "Verifica flat-urile unui obiectiv", "FlatCheck.Wpf.exe"),
        new("LightsCheck", "LightsCheck", "Verifica light-urile unei sesiuni: focus, trailing, dithering, drift", "LightsCheck.Wpf.exe"),
        new("ImageVisualCompare", "ImageVisualCompare", "Compara doua variante ale aceleiasi procesari, pixel cu pixel", "ImageVisualCompare.Wpf.exe"),
        new("AstroPhotoAnalyzer", "AstroPhotoAnalyzer", "Verifica o imagine finala inainte de print", "AstroPhotoAnalyzer.Wpf.exe"),
    ];
}
