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

using System.Globalization;
using DarksCheck.Core;
using static DarksCheck.Core.Ui;

namespace DarksCheck.Wpf;

/// <summary>
/// The window's own sentences, in one place, each an English and a Romanian wording picked in the
/// run's language (ADR 0021); no figure is decided here.
/// </summary>
internal static class WindowTexts
{
    internal const string AppName = "DarksCheck";

    internal static string AboutTitle => T("About DarksCheck", "Despre DarksCheck");

    internal static string AboutTagline => T(
        "Quality check for astrophotography dark frames and their match with a session.",
        "Verificare de calitate pentru dark frames de astrofoto si potrivirea lor cu o sesiune.");

    internal static string AboutLicenceHeading => T("LICENCE", "LICENTA");

    internal static string AboutLicenseLinkLabel => T("The full text of the GNU General Public License v3", "Textul complet al GNU General Public License v3");

    internal static string AboutThirdPartyHeading => T("THIRD-PARTY COMPONENTS", "COMPONENTE TERTE");

    internal static string ChooseTitle => T(
        "Choose the Siril session folder (the one with darks\\) or the folder of darks",
        "Alege folderul sesiunii Siril (cel cu darks\\) sau folderul de dark-uri");

    internal static string ChoosePartnerTitle => T(
        "Choose the partner folder of darks (a library or another session)",
        "Alege folderul partener de dark-uri (o biblioteca sau alta sesiune)");

    internal static string EmptyTitle => T(
        "Choose the folder of a session or of a library of darks",
        "Alege folderul unei sesiuni sau al unei biblioteci de dark-uri");

    internal static string EmptyLead => T(
        "The Siril working folder, the one that holds darks\\ and, optionally, biases\\ and lights\\, or a folder with "
        + "the darks directly in it (a library). The analysis starts by itself once you have chosen it: the biases, the darks "
        + "and the lights are decoded and measured, then come the index and the label.",
        "Folderul de lucru din Siril, cel care contine darks\\ si, optional, biases\\ si lights\\, sau un folder cu "
        + "dark-urile direct in el (o biblioteca). Analiza porneste singura dupa ce l-ai ales: bias-urile, dark-urile "
        + "si lights-urile sunt decodate si masurate, apoi vine indexul si eticheta.");

    internal static string ProblemTitle => T("The folder cannot be analysed", "Folderul nu se poate analiza");

    /// <summary>
    /// The wait screen's line: the phase and how far it got, or the opening message before any
    /// count.
    /// </summary>
    /// <param name="phase">The core's phase word, already in the run's language.</param>
    /// <param name="done">The items done so far.</param>
    /// <param name="total">The items of the phase; 0 while the session is still being
    /// opened.</param>
    /// <returns>The line, in the run's language.</returns>
    internal static string Busy(string phase, int done, int total) =>
        total == 0 ? T("Opening the session...", "Se deschide sesiunea...") : $"{phase}: {done.ToString(CultureInfo.InvariantCulture)} / {total.ToString(CultureInfo.InvariantCulture)}";

    internal static string Cancelled => T("The analysis was stopped.", "Analiza a fost oprita.");

    internal static string ExportTitle => T(
        "Choose the folder the report, the CSVs and the maps are written into",
        "Alege folderul in care se scriu raportul, CSV-urile si hartile");

    /// <summary>The status after an export: the written files, one per line.</summary>
    /// <param name="paths">The paths written.</param>
    /// <returns>The status text, in the run's language.</returns>
    internal static string Written(IReadOnlyList<string> paths) => T("Written:\n", "Scris:\n") + string.Join("\n", paths);

    internal static string NoPartner => T(
        "(none: choose a library or another session to compare the darks with)",
        "(niciunul: alege o biblioteca sau alta sesiune ca sa compari dark-urile)");

    internal static string PartnerChanged => T(
        "The partner has changed: press \"Analyse again\" to compare it with the session.",
        "Partenerul s-a schimbat: apasa \"Analizeaza din nou\" ca sa-l compari cu sesiunea.");

    internal static string LabelCopied => T(
        "The label is in the clipboard; rename the folder with it if you wish.",
        "Eticheta e in clipboard; redenumeste folderul cu ea daca vrei.");

    internal static string IndexLead => T(
        $"The index is a number for how warm the sensor was: the average value of the master's hot pixels at places {RunReport.F(Th.IndexRankLowPerMpx, 0)}-{RunReport.F(Th.IndexRankHighPerMpx, 0)} "
        + $"per megapixel, counting from the hottest, worked out for ISO {RunReport.F(Th.NormIso, 0)} and {RunReport.F(Th.NormExposureSeconds, 0)} s. The warmer the sensor, the higher the index. "
        + "The label is the folder's name plus idx and the rounded index (idx- when the index cannot be measured): you copy it and rename the folder yourself; the program writes and renames nothing in the photo folders.",
        $"Indexul e un numar pentru cat de cald a fost senzorul: valoarea medie a pixelilor fierbinti din master de pe locurile {RunReport.F(Th.IndexRankLowPerMpx, 0)}-{RunReport.F(Th.IndexRankHighPerMpx, 0)} "
        + $"pe fiecare megapixel, numarand de la cel mai fierbinte, socotita pentru ISO {RunReport.F(Th.NormIso, 0)} si {RunReport.F(Th.NormExposureSeconds, 0)} s. Cu cat senzorul a fost mai cald, cu atat indexul e mai mare. "
        + "Eticheta e numele folderului plus idx si indexul rotunjit (idx- cand indexul nu se poate masura): o copiezi si redenumesti tu folderul; programul nu scrie si nu redenumeste nimic in folderele cu poze.");

    internal static string NoDarkMap => T(
        $"No map: the frames are smaller than {(Th.BinPx * 2).ToString(CultureInfo.InvariantCulture)} px.",
        $"Nu e harta: cadrele sunt mai mici de {(Th.BinPx * 2).ToString(CultureInfo.InvariantCulture)} px.");

    internal static string NoBiasMap => T(
        $"No map: the session has no biases that can be used, or the frames are smaller than {(Th.BinPx * 2).ToString(CultureInfo.InvariantCulture)} px.",
        $"Nu e harta: sesiunea nu are bias-uri care sa se poata folosi, sau cadrele sunt mai mici de {(Th.BinPx * 2).ToString(CultureInfo.InvariantCulture)} px.");

    internal static string HotHeader => ">" + RunReport.F(Th.HotAdu, 0);

    internal static string HotTabLead => T(
        $"The hot pixels of the master: how many are above each of the five thresholds (from {RunReport.F(Th.HotLevelsAdu[0], 0)} to {RunReport.F(Th.HotLevelsAdu[^1], 0)} ADU-eq), "
        + $"how many of each colour are above {RunReport.F(Th.HotAdu, 0)}, how hot they are, whether they show in every frame, and how many jump between two levels "
        + "from one frame to the next (RTS). For information only: this check never warns.",
        $"Pixelii fierbinti din master: cati sunt peste fiecare din cele cinci praguri (de la {RunReport.F(Th.HotLevelsAdu[0], 0)} la {RunReport.F(Th.HotLevelsAdu[^1], 0)} ADU-eq), "
        + $"cati sunt peste {RunReport.F(Th.HotAdu, 0)} pe fiecare culoare, cat de fierbinti sunt, daca se vad in toate cadrele si cati sar intre doua niveluri "
        + "de la un cadru la altul (RTS). Doar de informare: verificarea asta nu da niciodata avertizare.");

    internal static string CandidatesTip => T(
        $"how many pixels are brighter than their neighbours by more than {RunReport.F(Th.LightHotSigmas, 0)} times the noise, without being saturated: stars and hot pixels",
        $"cati pixeli sunt mai luminosi decat vecinii lor cu peste {RoCount(RunReport.F(Th.LightHotSigmas, 0))}ori zgomotul, fara sa fie saturati: stele si pixeli fierbinti");

    internal static string IsolatedTip => T(
        $"how many of them have no neighbour that also stands out by more than {RunReport.F(Th.LightIsolationSigmas, 0)} times the noise: hot pixels, not stars",
        $"cati dintre ei nu au niciun vecin care sa iasa si el peste {RoCount(RunReport.F(Th.LightIsolationSigmas, 0))}ori zgomotul: pixeli fierbinti, nu stele");

    internal static string MaxFramesTip => T(
        $"How many frames are held in memory (48 MB each at 24 MP) and how many lights are measured; above that, one in k is taken. Default {Th.MaxFramesInMemory.ToString(CultureInfo.InvariantCulture)}.",
        $"Cate cadre se tin in memorie (48 MB fiecare la 24 MP) si cate lights se masoara; peste, se ia unul din k. Implicit {Th.MaxFramesInMemory.ToString(CultureInfo.InvariantCulture)}.");

    internal static string IsoFactorTip => T(
        $"How many times the signal grows per ISO stop, for the normalisation to ISO {RunReport.F(Th.NormIso, 0)}; {RunReport.F(Normalisation.Default.IsoFactorPerStop, 0)} = linear in ISO (D3300)",
        $"De cate ori creste semnalul la o treapta de ISO, pentru normalizarea la ISO {RunReport.F(Th.NormIso, 0)}; {RunReport.F(Normalisation.Default.IsoFactorPerStop, 0)} = liniar in ISO (D3300)");

    internal static string DoublingTip => T(
        $"How many degrees double the dark current, for the degrees in the report; {RunReport.F(Th.DoublingCelsius, 0)} on the D3300",
        $"La cate grade se dubleaza curentul de intuneric, pentru gradele din raport; {RunReport.F(Th.DoublingCelsius, 0)} la D3300");

    internal static string MapsLead => T(
        $"The three maps show the frame in squares of {(Th.BinPx * 2).ToString(CultureInfo.InvariantCulture)} px: the dark master (the mean of the frames), the bias master "
        + "and their difference, that is what the sensor gathers from its own heat (the dark current). Each map has its own contrast: the darkest "
        + "1 % of the squares is black, the brightest 1 % white, so that small differences show too. A brighter edge or corner on dark minus bias "
        + "is light that got into the camera, or glow; one that also shows in the bias comes from the sensor's readout, not from light.",
        $"Cele trei harti arata cadrul in patrate de {(Th.BinPx * 2).ToString(CultureInfo.InvariantCulture)} px: masterul dark (media cadrelor), masterul bias "
        + "si diferenta lor, adica ce aduna senzorul din caldura lui (curentul de intuneric). Fiecare harta are contrastul ei: cel mai intunecat "
        + "1 % din patrate e negru, cel mai luminos 1 % e alb, ca sa se vada si diferentele mici. O margine sau un colt mai luminos pe dark minus bias "
        + "e lumina intrata in aparat sau glow; unul care apare si in bias vine din citirea senzorului, nu e lumina.");

    internal static string LightsChartLead => T(
        "Each point is a light: how hot the master's hot pixels are in it, against the master (1 = the same), once ISO and exposure are taken into account. "
        + $"Green between {RunReport.F(Th.MatchDirectLow, 1)} and {RunReport.F(Th.MatchDirectHigh, 1)}: the dark can be subtracted as it is. "
        + $"Yellow between {RunReport.F(Th.MatchOptLow, 1)} and {RunReport.F(Th.MatchOptHigh, 1)}: subtract the dark with -opt (Siril multiplies it so that it matches the lights); "
        + $"yellow too between {RunReport.F(Th.MatchCosmeticLow, 1)} and {RunReport.F(Th.MatchOptLow, 1)}: use the dark for the cosmetic correction only. "
        + "Red beyond: they do not match. The first lights of the night usually sit lower, because the camera is still cold.",
        "Fiecare punct e un light: cat de fierbinti sunt in el pixelii fierbinti ai masterului, fata de master (1 = la fel), dupa ce se tine cont de ISO si de expunere. "
        + $"Verde intre {RunReport.F(Th.MatchDirectLow, 1)} si {RunReport.F(Th.MatchDirectHigh, 1)}: dark-ul se poate scadea asa cum e. "
        + $"Galben intre {RunReport.F(Th.MatchOptLow, 1)} si {RunReport.F(Th.MatchOptHigh, 1)}: scade dark-ul cu -opt (Siril il inmulteste cat sa se potriveasca cu lights-urile); "
        + $"tot galben intre {RunReport.F(Th.MatchCosmeticLow, 1)} si {RunReport.F(Th.MatchOptLow, 1)}: foloseste dark-ul doar pentru corectia cosmetica. "
        + "Rosu mai departe: nu se potrivesc. Primele lights ale noptii ies de obicei mai jos, pentru ca aparatul e inca rece.");
}
