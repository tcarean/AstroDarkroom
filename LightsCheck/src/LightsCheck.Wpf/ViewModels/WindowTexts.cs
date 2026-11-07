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

    internal static string CropsBusy => T("Reading the frame for the cut-outs...", "Se citeste cadrul pentru decupaje...");

    internal static string NoCrops => T("No cut-outs: the frame has no measured stars or could not be read.", "Niciun decupaj: cadrul nu are stele masurate sau nu s-a putut citi.");

    /// <summary>The step chart's guide label at the move threshold.</summary>
    /// <param name="px">The threshold in px, formatted by the core.</param>
    /// <returns>`move >= 5 px` (`miscare >= 5 px`).</returns>
    internal static string MoveGuide(string px) => T($"move >= {px} px", $"miscare >= {px} px");

    /// <summary>
    /// The charts' guide label at the distance beyond which a frame stands in another spot, its
    /// hot pixels no longer on those of the frame it is measured against (R-077).
    /// </summary>
    /// <param name="px">The distance in px, formatted by the core.</param>
    /// <returns>`10 px: another spot` (`10 px: alt loc`).</returns>
    internal static string WalkingNoiseGuide(string px) => T($"{px} px: another spot", $"{px} px: alt loc");

    /// <summary>The note on the dither map's x axis label: the grid spacing.</summary>
    /// <param name="px">The spacing in px, formatted whole.</param>
    /// <returns>`grid at 20 px` (`grila la 20 px`).</returns>
    internal static string GridNote(string px) => T($"grid at {px} px", $"grila la {px} px");

    /// <summary>The tip of the top bar's pixel field (R-083): what to type, what it is for, the default.</summary>
    /// <param name="defaultUm">The last fallback, the D3300's spec-sheet pixel, formatted by the core.</param>
    /// <param name="rawUm">What a D3300's raws give, formatted by the core.</param>
    /// <returns>The tip, in the language of the moment.</returns>
    internal static string PixelTip(string defaultUm, string rawUm) => T(
        $"The size of one camera pixel, in microns (thousandths of a millimetre), with a dot: {defaultUm}. It tells how much sky one pixel covers, in arcseconds. "
        + $"Used only when the files do not say it themselves. Blank (the usual way): worked out from the raw, about {rawUm} for the Nikon D3300; if the raw cannot tell, {defaultUm}, the D3300's.",
        $"Marimea unui pixel al camerei, in microni (miimi de milimetru), cu punct: {defaultUm}. Din ea se afla cat inseamna un pixel pe cer, in secunde de arc. "
        + $"Se foloseste doar cand fisierele nu o spun singure. Gol (de obicei): se afla din raw, cam {rawUm} la Nikon D3300; daca raw-ul nu spune, {defaultUm}, cat are D3300.");

    /// <summary>The tip of the top bar's threads field (R-083): what the count changes and this computer's default.</summary>
    /// <param name="defaultThreads">The frames measured at once when the field is blank, from the core.</param>
    /// <param name="cores">This computer's logical processors, from the core.</param>
    /// <returns>The tip, naming the cores only when there are two or more.</returns>
    internal static string ThreadsTip(int defaultThreads, int cores)
    {
        string lead = T(
            "How many frames are read at the same time. More is faster, but takes more memory and slows the rest of the computer. ",
            "Cate cadre se citesc in acelasi timp. Mai multe merg mai repede, dar iau mai multa memorie si incetinesc restul calculatorului. ");
        return lead + (cores >= 2
            ? T($"Blank: {defaultThreads}, half of this computer's {cores} cores.", $"Gol: {defaultThreads}, jumatate din cele {RoCount(cores)} nuclee ale acestui calculator.")
            : T($"Blank: {defaultThreads}.", $"Gol: {defaultThreads}."));
    }

    /// <summary>The tip of the top bar's east selector (R-083), with the default and the mount and app it is from (ADR 0041).</summary>
    /// <param name="arrow">The default arrow for east, as the core writes it.</param>
    /// <returns>The tip, in the language of the moment.</returns>
    internal static string EastTip(string arrow) => T(
        $"Which arrow of the SynScan app moves the mount east; the other one moves it west. It only names the dither moves on the Frames and Dithering tabs and in the files; the figures do not change. Default: {arrow} east, as on a Star Adventurer GTi with the SynScan Pro app. Kept for the next start.",
        $"Ce sageata din aplicatia SynScan muta montura spre est; cealalta o muta spre vest. Doar numeste miscarile de dithering din filele Cadre si Dithering si din fisiere; cifrele nu se schimba. Implicit: {arrow} est, ca pe un Star Adventurer GTi cu aplicatia SynScan Pro. Ramane aleasa si la urmatoarea pornire.");

    /// <summary>The tip of the top bar's north selector (R-083), with the default and the mount and app it is from (ADR 0041).</summary>
    /// <param name="arrow">The default arrow for north, as the core writes it.</param>
    /// <returns>The tip, in the language of the moment.</returns>
    internal static string NorthTip(string arrow) => T(
        $"Which arrow of the SynScan app moves the mount north; the other one moves it south. It only names the dither moves on the Frames and Dithering tabs and in the files; the figures do not change. Default: {arrow} north, as on a Star Adventurer GTi with the SynScan Pro app. Kept for the next start.",
        $"Ce sageata din aplicatia SynScan muta montura spre nord; cealalta o muta spre sud. Doar numeste miscarile de dithering din filele Cadre si Dithering si din fisiere; cifrele nu se schimba. Implicit: {arrow} nord, ca pe un Star Adventurer GTi cu aplicatia SynScan Pro. Ramane aleasa si la urmatoarea pornire.");
}
