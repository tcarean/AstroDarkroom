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

using static LightsCheck.Core.Ui;

namespace LightsCheck.Wpf;

/// <summary>
/// The window's own sentences, each in both languages (ADR 0022), in one place; no figure is
/// written here.
/// </summary>
internal static class WindowTexts
{
    internal const string AppName = "LightsCheck";

    internal static string AboutTitle => T("About LightsCheck", "Despre LightsCheck");

    internal static string AboutTagline => T("Quality check for astrophotography light frames.", "Verificare de calitate pentru light frames de astrofoto.");

    internal static string AboutLicenceHeading => T("LICENCE", "LICENTA");

    internal static string AboutLicenseLinkLabel => T("The full text of the GNU General Public License v3", "Textul complet al GNU General Public License v3");

    internal static string AboutThirdPartyHeading => T("THIRD-PARTY COMPONENTS", "COMPONENTE TERTE");

    internal static string AboutThirdPartyLead => T(
        "The tool links two components written by others, both compatible with the GPLv3. The FITS reader, "
        + "the PNG encoder, every calculation and every threshold are written by hand. Why each licence is "
        + "compatible is in THIRD-PARTY-NOTICES.md.",
        "Unealta leaga doua componente scrise de altii, amandoua compatibile cu GPLv3. Cititorul FITS, "
        + "encoderul PNG, toate calculele si toate pragurile sunt scrise de mana. Motivele pentru care fiecare "
        + "licenta e compatibila sunt in THIRD-PARTY-NOTICES.md.");

    internal static string ChooseTitle => T("Choose the Siril session folder (the one with lights\\ and biases\\)", "Alege folderul sesiunii Siril (cel cu lights\\ si biases\\)");

    internal static string EmptyTitle => T("Choose a session folder", "Alege folderul unei sesiuni");

    internal static string EmptyLead => T(
        "The Siril working folder, the one that holds lights\\ (or the raw frames themselves) and, optionally, biases\\. "
        + "The analysis starts by itself once you have chosen it: every frame is decoded and measured, then the series.",
        "Folderul de lucru din Siril, cel care contine lights\\ (sau cadrele raw direct) si, optional, biases\\. "
        + "Analiza porneste singura dupa ce l-ai ales: fiecare cadru e decodat si masurat, apoi seria.");

    internal static string ProblemTitle => T("The folder cannot be analysed", "Folderul nu se poate analiza");

    /// <summary>The wait screen's line while the frames are measured.</summary>
    /// <param name="done">
    /// Frames measured so far; 0 before the first, while the session opens.
    /// </param>
    /// <param name="total">Frames to measure.</param>
    /// <returns>
    /// The opening line when <paramref name="done"/> is 0, else the count of frames measured.
    /// </returns>
    internal static string Busy(int done, int total)
        => done == 0 ? T("Opening the session...", "Se deschide sesiunea...") : T($"Measuring the frames: {done} / {total}", $"Se masoara cadrele: {done} / {total}");

    internal static string BusyRegistering => T("Registering the frames and analysing the series...", "Se inregistreaza cadrele si se analizeaza seria...");

    internal static string Cancelled => T("The analysis was stopped.", "Analiza a fost oprita.");

    internal static string ExportTitle => T("Choose the folder to write the report, the CSV and the reject list into", "Alege folderul in care se scriu raportul, CSV-ul si lista de respins");

    /// <summary>The status line after an export.</summary>
    /// <param name="paths">The files written, as the core returned them.</param>
    /// <returns>"Written:" ("Scris:") followed by one path per line.</returns>
    internal static string Written(IReadOnlyList<string> paths) => T("Written:", "Scris:") + "\n" + string.Join("\n", paths);

    internal static string CropsBusy => T("Decoding the frame for the cut-outs...", "Se decodeaza cadrul pentru decupaje...");

    internal static string NoCrops => T("No cut-outs: the frame has no measured stars or could not be read.", "Niciun decupaj: cadrul nu are stele masurate sau nu s-a putut citi.");

    /// <summary>The step chart's guide label at the move threshold.</summary>
    /// <param name="px">The threshold in px, formatted by the core.</param>
    /// <returns>`move >= 5 px` (`miscare >= 5 px`).</returns>
    internal static string MoveGuide(string px) => T($"move >= {px} px", $"miscare >= {px} px");

    /// <summary>The step chart's guide label at the coverage radius of walking noise.</summary>
    /// <param name="px">The radius in px, formatted by the core.</param>
    /// <returns>`10 px (walking noise)`.</returns>
    internal static string WalkingNoiseGuide(string px) => $"{px} px (walking noise)";

    /// <summary>The note on the dither map's x axis label: the grid spacing.</summary>
    /// <param name="px">The spacing in px, formatted whole.</param>
    /// <returns>`grid at 20 px` (`grila la 20 px`).</returns>
    internal static string GridNote(string px) => T($"grid at {px} px", $"grila la {px} px");
}
