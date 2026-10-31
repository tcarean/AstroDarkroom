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
/// What the suite needs on the machine, told to the user before the tools are opened (R-009,
/// ADR 0007): a Siril session folder, Siril 1.4.4 or newer installed, Microsoft's Visual C++
/// runtime for the tools that read camera raw files and, optional for FlatCheck, Adobe's lens
/// profiles. The launcher checks the runtime only (<see cref="VcRuntime"/>, R-018, ADR 0016);
/// the texts are here, in both languages, so a test guards them (ADR 0006), and the windows show
/// them.
/// </summary>
public static class Prerequisites
{
    /// <summary>One line for the main window, under its subtitle, in the current language.</summary>
    public static string Summary => T(
        "The tools read a Siril session folder and expect Siril 1.4.4 or newer; details under About.",
        "Uneltele citesc folderul unei sesiuni Siril si cer Siril 1.4.4 sau mai nou; detalii la Despre.");

    /// <summary>
    /// The four paragraphs of the "Despre" window, in the current language: the session folder,
    /// the Siril version, the Visual C++ runtime with Microsoft's link, the optional Adobe lens
    /// profiles.
    /// </summary>
    public static IReadOnlyList<string> Details =>
    [
        T("A Siril session folder: the folder of one night with lights/, darks/, flats/ and biases/ as "
          + "Siril lays them out, and the results of its scripts in process/ (or masters/). DarksCheck, "
          + "FlatCheck and LightsCheck open that folder; ImageVisualCompare, CopyWcs and AstroPhotoAnalyzer "
          + "open the files exported from Siril.",
          "Folderul unei sesiuni Siril: folderul unei nopti cu lights/, darks/, flats/ si biases/ cum le "
          + "aseaza Siril, si rezultatele scripturilor lui in process/ (sau masters/). DarksCheck, FlatCheck "
          + "si LightsCheck deschid acel folder; ImageVisualCompare, CopyWcs si AstroPhotoAnalyzer deschid "
          + "fisierele exportate din Siril."),
        T("Siril 1.4.4 or newer installed (siril.org): the masters and the stacks the tools check come "
          + "from its scripts, and FlatCheck runs the preprocessing script through siril-cli from your "
          + "installation.",
          "Siril 1.4.4 sau mai nou instalat (siril.org): masterele si stack-urile pe care le verifica "
          + "uneltele ies din scripturile lui, iar FlatCheck ruleaza scriptul de preprocesare prin siril-cli "
          + "din instalarea ta."),
        T("Microsoft's Visual C++ 2015-2022 runtime (x64), which most computers already have: DarksCheck, "
          + "FlatCheck and LightsCheck need it to read camera raw files, and the launcher says when it is "
          + "missing. It installs from " + VcRuntime.DownloadUrl + ".",
          "Runtime-ul Microsoft Visual C++ 2015-2022 (x64), pe care il au deja cele mai multe calculatoare: "
          + "DarksCheck, FlatCheck si LightsCheck au nevoie de el ca sa citeasca fisiere raw de aparat, iar "
          + "lansatorul spune cand lipseste. Se instaleaza de la " + VcRuntime.DownloadUrl + "."),
        T("Optional, for FlatCheck: Adobe's lens profiles, read from this computer when an Adobe program "
          + "installed them: Lightroom, Photoshop with Camera Raw, or the free Adobe DNG Converter. Without "
          + "them FlatCheck uses the lensfun database it ships with.",
          "Optional, pentru FlatCheck: profilele de obiective Adobe, citite de pe acest calculator cand le-a "
          + "instalat un program Adobe: Lightroom, Photoshop cu Camera Raw, sau Adobe DNG Converter, gratuit. "
          + "Fara ele FlatCheck foloseste baza lensfun livrata cu el."),
    ];
}
