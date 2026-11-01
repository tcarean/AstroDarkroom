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
/// The six applications of the suite, compiled in, in the order of the workflow: the
/// calibration frames first (darks, flats), then the lights, then the processed result, the
/// plate solve put back into it, and the print (R-001, ADR 0010). SuiteTests keeps the list
/// in step with the components kit.json bundles and with the executable names their project
/// files build (the rule "The catalogue and kit.json agree"), and each app's icon with the copy
/// of the component's own in Icon/ (R-010). Each description is written in
/// both languages where it stands, and <see cref="Apps"/> gives the wording of the current
/// language (R-006, ADR 0006).
/// </summary>
public static class AppCatalogue
{
    /// <summary>The apps, in workflow order, described in the language of <see cref="Ui.Current"/>.</summary>
    public static IReadOnlyList<SuiteApp> Apps => Current == UiLanguage.Romanian ? Romanian : English;

    private static readonly IReadOnlyList<SuiteApp> English = Build(UiLanguage.English);

    private static readonly IReadOnlyList<SuiteApp> Romanian = Build(UiLanguage.Romanian);

    /// <summary>The list in one language, built once per language when the type loads.</summary>
    /// <param name="language">The language of the descriptions.</param>
    /// <returns>The six apps, described in that language.</returns>
    private static SuiteApp[] Build(UiLanguage language)
    {
        using IDisposable scope = Use(language);
        return
        [
            new("DarksCheck", "DarksCheck", T("Checks the darks of a session or a library: light leaks, thermal drift, noise, hot and dead pixels, and whether their temperature matches the lights.", "Verifica dark-urile unei sesiuni sau biblioteci: scurgeri de lumina, deriva termica, zgomot, pixeli fierbinti si morti, si daca au temperatura light-urilor."), "DarksCheck.Wpf.exe", "darkscheck.ico"),
            new("FlatCheck", "FlatCheck", T("Checks the flats of a lens: exposure, symmetry, vignetting against the lens profile, and whether they correct a real light; builds a flat from the profile.", "Verifica flat-urile unui obiectiv: expunerea, simetria, vinietarea fata de profilul obiectivului si daca corecteaza un light real; face un flat din profil."), "FlatCheck.Wpf.exe", "flatcheck.ico"),
            new("LightsCheck", "LightsCheck", T("Checks the lights of a night: which frames to leave out of the stack and why, mount drift, dithering, polar alignment, what to change next time.", "Verifica light-urile unei nopti: ce cadre sa lasi afara din stack si de ce, deriva monturii, dithering-ul, alinierea polara, ce sa schimbi data viitoare."), "LightsCheck.Wpf.exe", "lightscheck.ico"),
            new("ImageVisualCompare", "ImageVisualCompare", T("Compares two versions of one image with a before/after slider (two stretches, with and without stars), pixel by pixel; reveals faint gradients.", "Compara doua variante ale unei imagini cu un slider inainte/dupa (doua stretch-uri, cu si fara stele), pixel cu pixel; arata gradientii slabi."), "ImageVisualCompare.Wpf.exe", "ImageVisualCompare.ico"),
            new("WCS_Copy", "CopyWcs", T("Copies the plate solve of a FITS back into its processed version, which lost it on the way, so that annotations and catalogues find the sky again.", "Copiaza plate solve-ul unui FITS inapoi in varianta lui procesata, care l-a pierdut pe drum, ca adnotarile si cataloagele sa regaseasca cerul."), "CopyWcs.Wpf.exe", "copywcs.ico"),
            new("AstroPhotoAnalyzer", "AstroPhotoAnalyzer", T("Prepares an image for print: simulates the paper, checks the blacks against its limits, helps edit the curves and soft-proofs with the paper's ICC profile.", "Pregateste o imagine pentru print: simuleaza hartia, verifica negrurile fata de limitele ei, ajuta la editarea curbelor si face soft proofing cu profilul ICC."), "AstroPhotoAnalyzer.Wpf.exe", "AstroPhotoAnalyzer.ico"),
        ];
    }
}
