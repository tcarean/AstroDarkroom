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

using static AstroDarkroom.Core.Ui;

namespace AstroDarkroom.Core;

/// <summary>
/// The five applications of the suite, compiled in, in the order of the workflow: the
/// calibration frames first (darks, flats), then the lights, then the processed result and
/// the print (R-001). SuiteTests keeps the list in step with the components kit.json bundles
/// and with the executable names their project files build (the rule "The catalogue and
/// kit.json agree"). Each description is written in both languages where it stands, and
/// <see cref="Apps"/> gives the wording of the current language (R-006, ADR 0006).
/// </summary>
public static class AppCatalogue
{
    /// <summary>The apps, in workflow order, described in the language of <see cref="Ui.Current"/>.</summary>
    public static IReadOnlyList<SuiteApp> Apps => Current == UiLanguage.Romanian ? Romanian : English;

    private static readonly IReadOnlyList<SuiteApp> English = Build(UiLanguage.English);

    private static readonly IReadOnlyList<SuiteApp> Romanian = Build(UiLanguage.Romanian);

    /// <summary>The list in one language, built once per language when the type loads.</summary>
    /// <param name="language">The language of the descriptions.</param>
    /// <returns>The five apps, described in that language.</returns>
    private static SuiteApp[] Build(UiLanguage language)
    {
        using IDisposable scope = Use(language);
        return
        [
            new("DarksCheck", "DarksCheck", T("Checks the darks of a session or of a library", "Verifica dark-urile unei sesiuni sau ale unei biblioteci"), "DarksCheck.Wpf.exe"),
            new("FlatCheck", "FlatCheck", T("Checks the flats of a lens", "Verifica flat-urile unui obiectiv"), "FlatCheck.Wpf.exe"),
            new("LightsCheck", "LightsCheck", T("Checks the lights of a session: focus, trailing, dithering, drift", "Verifica light-urile unei sesiuni: focus, trailing, dithering, drift"), "LightsCheck.Wpf.exe"),
            new("ImageVisualCompare", "ImageVisualCompare", T("Compares two versions of the same processing, pixel by pixel", "Compara doua variante ale aceleiasi procesari, pixel cu pixel"), "ImageVisualCompare.Wpf.exe"),
            new("AstroPhotoAnalyzer", "AstroPhotoAnalyzer", T("Checks a final image before printing", "Verifica o imagine finala inainte de print"), "AstroPhotoAnalyzer.Wpf.exe"),
        ];
    }
}
