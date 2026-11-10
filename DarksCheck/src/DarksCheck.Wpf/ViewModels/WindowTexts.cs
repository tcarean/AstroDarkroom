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
        "The Siril working folder, the one that holds darks\\ and, if you have them, biases\\ and lights\\, or a folder with "
        + "the darks directly in it (a library). The analysis starts by itself once you have chosen it: the program reads and "
        + "measures the biases, the darks and the lights, then works out the index and the label.",
        "Folderul de lucru din Siril, cel care contine darks\\ si, daca le ai, biases\\ si lights\\, sau un folder cu "
        + "dark-urile direct in el (o biblioteca). Analiza porneste singura dupa ce l-ai ales: programul citeste si masoara "
        + "bias-urile, dark-urile si lights-urile, apoi socoteste indexul si eticheta.");

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

    internal static string ChooseLightsTitle => T(
        "Choose the folder of lights (a session, or a folder with the lights in it)",
        "Alege folderul de lights (o sesiune, sau un folder cu lights-urile in el)");

    internal static string NoLightsFrom => T(
        "(none: the open folder's lights)",
        "(niciunul: lights-urile folderului deschis)");

    internal static string LightsFromChanged => T(
        "The lights have changed: press \"Analyse again\" to compare them with the darks.",
        "Lights-urile s-au schimbat: apasa \"Analizeaza din nou\" ca sa le compari cu dark-urile.");

    /// <summary>The sentence of the Lights tab's card (R-122): what choosing another folder's lights does.</summary>
    internal static string LightsFromLead => T(
        "Do these darks fit the lights of another session? Choose that session (its lights\\ is taken) or a folder with the "
        + "lights in it: the program compares them with the open darks, as if they sat in this folder's lights\\, and the "
        + "folder's own lights are left out. The choice holds for this folder only and is not kept.",
        "Se potrivesc dark-urile astea cu lights-urile altei sesiuni? Alege sesiunea aceea (se ia lights\\ al ei) sau un folder cu "
        + "lights-urile in el: programul le compara cu dark-urile deschise, ca si cum ar sta in lights\\ al folderului asta, iar "
        + "lights-urile proprii ale folderului raman deoparte. Alegerea tine doar pentru folderul asta si nu se pastreaza.");

    /// <summary>The text under the label of a folder with lights and no darks of its own (R-066).</summary>
    internal static string IndexLeadLights => T(
        "This folder has no darks of its own, so its label is the lights': what dark they are like, read through the partner's "
        + "darks (the lights row under INDEX). The warmer the sensor was when the lights were shot, the higher the number. "
        + "The partner's own index and label stand on its row. You copy the label and rename the folder yourself; the program "
        + "writes and renames nothing in the photo folders.",
        "Folderul asta nu are dark-uri ale lui, asa ca eticheta e a lights-urilor: cu ce dark seamana, citit prin dark-urile "
        + "partenerului (randul lights de sub INDEX). Cu cat senzorul a fost mai cald cand s-au facut lights-urile, cu atat numarul e mai mare. "
        + "Indexul si eticheta partenerului stau pe randul lui. Eticheta o copiezi si redenumesti tu folderul; programul "
        + "nu scrie si nu redenumeste nimic in folderele cu poze.");

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
        $"How many frames of a group are held in memory (48 MB each at 24 MP); if there are more, only some are read, evenly spaced (one in two, for example). The lights are all measured. Default {Th.MaxFramesInMemory.ToString(CultureInfo.InvariantCulture)}.",
        $"Cate cadre dintr-un grup se tin in memorie (48 MB fiecare la 24 MP); daca sunt mai multe, se citesc doar unele, la distante egale (de exemplu unul din doua). Lights-urile se masoara toate. Implicit {Th.MaxFramesInMemory.ToString(CultureInfo.InvariantCulture)}.");

    internal static string IsoFactorTip => T(
        $"How many times the signal grows at every ISO stop; with it every frame is worked out at ISO {RunReport.F(Th.NormIso, 0)}, so that they can be compared. {RunReport.F(Normalisation.Default.IsoFactorPerStop, 0)} = the signal doubles at every stop (D3300)",
        $"De cate ori creste semnalul la fiecare treapta de ISO; cu el, toate cadrele se socotesc la ISO {RunReport.F(Th.NormIso, 0)}, ca sa poata fi comparate. {RunReport.F(Normalisation.Default.IsoFactorPerStop, 0)} = semnalul se dubleaza la fiecare treapta (D3300)");

    internal static string DoublingTip => T(
        $"How many degrees warmer it takes for the dark current (what the sensor gathers from its own heat) to double; the degrees in the report are worked out from it. {RunReport.F(Th.DoublingCelsius, 0)} on the D3300",
        $"La cate grade in plus se dubleaza curentul de intuneric (ce aduna senzorul din caldura lui); din el se socotesc gradele din raport. {RunReport.F(Th.DoublingCelsius, 0)} la D3300");

    internal static string MapsLead => T(
        $"The three maps show the frame in squares of {(Th.BinPx * 2).ToString(CultureInfo.InvariantCulture)} px: the dark master (the mean of the frames), the bias master "
        + "and their difference, that is what the sensor gathers from its own heat (the dark current). Each map has its own contrast: the darkest "
        + "1 % of the squares is black, the brightest 1 % white, so that small differences show too. A brighter edge or corner on dark minus bias "
        + "is light that got into the camera, or glow; one that also shows in the bias comes from the sensor's readout, not from light.",
        $"Cele trei harti arata cadrul in patrate de {(Th.BinPx * 2).ToString(CultureInfo.InvariantCulture)} px: masterul dark (media cadrelor), masterul bias "
        + "si diferenta lor, adica ce aduna senzorul din caldura lui (curentul de intuneric). Fiecare harta are contrastul ei: cel mai intunecat "
        + "1 % din patrate e negru, cel mai luminos 1 % e alb, ca sa se vada si diferentele mici. O margine sau un colt mai luminos pe dark minus bias "
        + "e lumina intrata in aparat sau glow; unul care apare si in bias vine din citirea senzorului, nu e lumina.");

    /// <summary>
    /// The sentence above the lights' chart: what a point is and what each colour means. The green
    /// band is the one the core found for the open lights (R-067): 0.9 .. 1.1, or wider while what a
    /// plain subtraction leaves stays under the noise of a light, and then the sentence says so.
    /// </summary>
    /// <param name="band">The traces of the open result, whose band the sentence names;
    /// <see cref="TraceFigures.None"/> for the plain band.</param>
    /// <returns>The sentence, in the window's language.</returns>
    internal static string LightsChartLead(TraceFigures band) => band.Widened
        ? T(
            "Each point is a light: how hot the master's hot pixels are in it, against the master (1 = the same), once ISO and exposure are taken into account. "
            + $"Green between {RunReport.F(band.DirectLow, 2)} and {RunReport.F(band.DirectHigh, 2)}: the dark can be subtracted as it is ({RunReport.F(Th.MatchDirectLow, 1)} .. {RunReport.F(Th.MatchDirectHigh, 1)}, widened to where what the subtraction leaves reaches the noise of a light). "
            + $"Yellow from there to {RunReport.F(Th.MatchOptLow, 1)} and {RunReport.F(Th.MatchOptHigh, 1)}: subtract the dark with -opt (Siril multiplies it so that it matches the lights); "
            + $"yellow too between {RunReport.F(Th.MatchCosmeticLow, 1)} and {RunReport.F(Th.MatchOptLow, 1)}: use the dark for the cosmetic correction only. "
            + "Red beyond: they do not match. The first lights of the night usually sit lower, because the camera is still cold.",
            "Fiecare punct e un light: cat de fierbinti sunt in el pixelii fierbinti ai masterului, fata de master (1 = la fel), dupa ce se tine cont de ISO si de expunere. "
            + $"Verde intre {RunReport.F(band.DirectLow, 2)} si {RunReport.F(band.DirectHigh, 2)}: dark-ul se poate scadea asa cum e ({RunReport.F(Th.MatchDirectLow, 1)} .. {RunReport.F(Th.MatchDirectHigh, 1)}, largit pana unde ce ramane dupa scadere ajunge cat zgomotul unui light). "
            + $"Galben de acolo pana la {RunReport.F(Th.MatchOptLow, 1)} si {RunReport.F(Th.MatchOptHigh, 1)}: scade dark-ul cu -opt (Siril il inmulteste cat sa se potriveasca cu lights-urile); "
            + $"tot galben intre {RunReport.F(Th.MatchCosmeticLow, 1)} si {RunReport.F(Th.MatchOptLow, 1)}: foloseste dark-ul doar pentru corectia cosmetica. "
            + "Rosu mai departe: nu se potrivesc. Primele lights ale noptii ies de obicei mai jos, pentru ca aparatul e inca rece.")
        : PlainLightsChartLead;

    /// <summary>The sentence above the lights' chart with the plain green band, 0.9 .. 1.1.</summary>
    private static string PlainLightsChartLead => T(
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
