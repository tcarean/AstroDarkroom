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

namespace LightsCheck.Wpf;

/// <summary>
/// The window's own sentences (Romanian), in one place; no figure is written here.
/// </summary>
internal static class WindowTexts
{
    internal const string AppName = "LightsCheck";

    internal static string AboutTitle => "Despre LightsCheck";

    internal static string AboutTagline => "Verificare de calitate pentru light frames de astrofoto.";

    internal static string AboutLicenceHeading => "LICENTA";

    internal static string AboutLicenseLinkLabel => "Textul complet al GNU General Public License v3";

    internal static string AboutThirdPartyHeading => "COMPONENTE TERTE";

    internal static string AboutThirdPartyLead =>
        "Unealta leaga doua componente scrise de altii, amandoua compatibile cu GPLv3. Cititorul FITS, "
        + "encoderul PNG, toate calculele si toate pragurile sunt scrise de mana. Motivele pentru care fiecare "
        + "licenta e compatibila sunt in THIRD-PARTY-NOTICES.md.";

    internal static string ChooseTitle => "Alege folderul sesiunii Siril (cel cu lights\\ si biases\\)";

    internal static string EmptyTitle => "Alege folderul unei sesiuni";

    internal static string EmptyLead =>
        "Folderul de lucru din Siril, cel care contine lights\\ (sau cadrele raw direct) si, optional, biases\\. "
        + "Analiza porneste singura dupa ce l-ai ales: fiecare cadru e decodat si masurat, apoi seria.";

    internal static string ProblemTitle => "Folderul nu se poate analiza";

    /// <summary>The wait screen's line while the frames are measured.</summary>
    /// <param name="done">
    /// Frames measured so far; 0 before the first, while the session opens.
    /// </param>
    /// <param name="total">Frames to measure.</param>
    /// <returns>
    /// The opening line when <paramref name="done"/> is 0, else the count of frames measured.
    /// </returns>
    internal static string Busy(int done, int total) => done == 0 ? "Se deschide sesiunea..." : $"Se masoara cadrele: {done} / {total}";

    internal static string BusyRegistering => "Se inregistreaza cadrele si se analizeaza seria...";

    internal static string Cancelled => "Analiza a fost oprita.";

    internal static string ExportTitle => "Alege folderul in care se scriu raportul, CSV-ul si lista de respins";

    /// <summary>The status line after an export.</summary>
    /// <param name="paths">The files written, as the core returned them.</param>
    /// <returns>"Scris:" followed by one path per line.</returns>
    internal static string Written(IReadOnlyList<string> paths) => "Scris:\n" + string.Join("\n", paths);

    internal static string CropsBusy => "Se decodeaza cadrul pentru decupaje...";

    internal static string NoCrops => "Niciun decupaj: cadrul nu are stele masurate sau nu s-a putut citi.";

    /// <summary>The step chart's guide label at the move threshold.</summary>
    /// <param name="px">The threshold in px, formatted by the core.</param>
    /// <returns>`miscare >= 5 px`.</returns>
    internal static string MoveGuide(string px) => $"miscare >= {px} px";

    /// <summary>The step chart's guide label at the coverage radius of walking noise.</summary>
    /// <param name="px">The radius in px, formatted by the core.</param>
    /// <returns>`10 px (walking noise)`.</returns>
    internal static string WalkingNoiseGuide(string px) => $"{px} px (walking noise)";

    /// <summary>The note on the dither map's x axis label: the grid spacing.</summary>
    /// <param name="px">The spacing in px, formatted whole.</param>
    /// <returns>`grila la 20 px`.</returns>
    internal static string GridNote(string px) => $"grila la {px} px";
}
