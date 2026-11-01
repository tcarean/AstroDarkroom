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

    /// <summary>
    /// The lead above the third-party list: how many components there are, in words, counted from
    /// the core's list itself, and where their licence texts are, which holds in the Astro
    /// Darkroom folder and for the single file alike (R-120, ADR 0027).
    /// </summary>
    internal static string AboutThirdPartyLead => T(
        $"The program uses {CountWord(ThirdParty.Components.Count)} components written by others, all compatible with the GPLv3. "
        + "The FITS reader, the PNG encoder, every computation and every threshold are written by hand. Their licence texts, "
        + "whole, and why each is compatible are in THIRD-PARTY-NOTICES.txt, beside the program in the Astro Darkroom folder, "
        + "and in AstroDarkroom/THIRD-PARTY-NOTICES.md in the source repository, https://github.com/tcarean/AstroDarkroom.",
        $"Programul foloseste {CountWord(ThirdParty.Components.Count)} componente scrise de altii, toate compatibile cu GPLv3. "
        + "Cititorul FITS, encoderul PNG, toate calculele si toate pragurile sunt scrise de mana. Textele licentelor lor, "
        + "intregi, si motivele pentru care fiecare e compatibila sunt in THIRD-PARTY-NOTICES.txt, langa program in folderul "
        + "Astro Darkroom, si in AstroDarkroom/THIRD-PARTY-NOTICES.md din depozitul sursei, https://github.com/tcarean/AstroDarkroom.");

    /// <summary>
    /// A count as a word, for a sentence: the words up to ten in the run's language, the digits past
    /// them; FlatCheck's <c>CountWord</c>, to ten. The Romanian words are the feminine ones,
    /// <c>componente</c> being feminine.
    /// </summary>
    /// <param name="n">The count, 1 or more.</param>
    /// <returns>The word, or the digits.</returns>
    internal static string CountWord(int n) => n switch
    {
        1 => T("one", "una"),
        2 => T("two", "doua"),
        3 => T("three", "trei"),
        4 => T("four", "patru"),
        5 => T("five", "cinci"),
        6 => T("six", "sase"),
        7 => T("seven", "sapte"),
        8 => T("eight", "opt"),
        9 => T("nine", "noua"),
        10 => T("ten", "zece"),
        _ => n.ToString(CultureInfo.InvariantCulture),
    };

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
        "The index is a number for how warm the sensor ran: the median amplitude of the master's pixels at ranks "
        + $"{RunReport.F(Th.IndexRankLowPerMpx, 0)}-{RunReport.F(Th.IndexRankHighPerMpx, 0)} per megapixel, normalised to "
        + $"ISO {RunReport.F(Th.NormIso, 0)} and {RunReport.F(Th.NormExposureSeconds, 0)} s. The label is the folder's name plus idx<NN>: you copy it "
        + "and rename the folder yourself; the program writes and renames nothing in the photo folders.",
        "Indexul e un numar pentru cat de cald a rulat senzorul: mediana amplitudinii pixelilor de pe rangurile "
        + $"{RunReport.F(Th.IndexRankLowPerMpx, 0)}-{RunReport.F(Th.IndexRankHighPerMpx, 0)} pe megapixel ai masterului, normalizata la "
        + $"ISO {RunReport.F(Th.NormIso, 0)} si {RunReport.F(Th.NormExposureSeconds, 0)} s. Eticheta e numele folderului plus idx<NN>: o copiezi "
        + "si redenumesti folderul tu; programul nu scrie si nu redenumeste nimic in folderele cu poze.");

    internal static string NoMap => T(
        $"The map has no blocks (frames smaller than {(Th.BinPx * 2).ToString(CultureInfo.InvariantCulture)} px) or the group is missing.",
        $"Harta nu are blocuri (cadre mai mici de {(Th.BinPx * 2).ToString(CultureInfo.InvariantCulture)} px) sau grupul lipseste.");

    internal static string HotHeader => ">" + RunReport.F(Th.HotAdu, 0);

    internal static string HotHeaderTip => T(
        $"pixels above {RunReport.F(Th.HotAdu, 0)} ADU-eq on the reference plane",
        $"pixeli peste {RunReport.F(Th.HotAdu, 0)} ADU-eq pe planul de referinta");

    internal static string HotTabLead => T(
        $"The hot pixels of the median master at five levels, on planes at {RunReport.F(Th.HotAdu, 0)} ADU-eq, with their amplitudes and how stable "
        + "they are from one frame to the next; the RTS candidates are the pixels whose value jumps between frames. Informative: how many the sensor has, not a verdict.",
        $"Pixelii fierbinti ai masterului median la cinci niveluri, pe planuri la {RunReport.F(Th.HotAdu, 0)} ADU-eq, cu amplitudinile lor si cat de stabili "
        + "sunt de la un cadru la altul; candidatii RTS sunt pixelii a caror valoare sare intre cadre. Informativ: cati are senzorul, nu un verdict.");

    internal static string CandidatesTip => T(
        $"pixels above {RunReport.F(Th.LightHotSigmas, 0)} local sigma, below saturation",
        $"pixeli peste {RunReport.F(Th.LightHotSigmas, 0)} sigma locale, sub saturatie");

    internal static string IsolatedTip => T(
        $"candidates with no neighbour above {RunReport.F(Th.LightIsolationSigmas, 0)} sigma: hot pixels, not stars",
        $"candidati fara niciun vecin peste {RunReport.F(Th.LightIsolationSigmas, 0)} sigma: pixeli fierbinti, nu stele");

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
        $"The maps of the reference plane in blocks of {(Th.BinPx * 2).ToString(CultureInfo.InvariantCulture)} px, each stretched between its own 1st and 99th percentiles: "
        + "the dark master (the mean of the frames), the bias master and their difference, the dark current. A brighter edge or corner "
        + "on dark - bias is a light leak or glow; one that also shows in the bias is the readout, not light.",
        $"Hartile planului de referinta in blocuri de {(Th.BinPx * 2).ToString(CultureInfo.InvariantCulture)} px, fiecare intinsa intre percentilele 1 si 99 ale ei: "
        + "masterul dark (media cadrelor), masterul bias si diferenta lor, curentul de intuneric. O margine sau un colt "
        + "mai luminos pe dark - bias e o scurgere de lumina sau glow; unul care apare si in bias e citire, nu lumina.");

    internal static string LightsChartLead => T(
        "The amplitude of the master's hot pixels in each light, against the master, normalised for ISO and exposure. "
        + $"Green between {RunReport.F(Th.MatchDirectLow, 1)} and {RunReport.F(Th.MatchDirectHigh, 1)} (direct subtraction), yellow out to "
        + $"{RunReport.F(Th.MatchOptLow, 1)} and {RunReport.F(Th.MatchOptHigh, 1)} (-opt) and down to {RunReport.F(Th.MatchCosmeticLow, 1)} (cosmetic correction only), "
        + "red beyond; the first lights of a night are cold.",
        "Amplitudinea pixelilor fierbinti ai masterului in fiecare light, fata de master, normalizata la ISO si expunere. "
        + $"Verde intre {RunReport.F(Th.MatchDirectLow, 1)} si {RunReport.F(Th.MatchDirectHigh, 1)} (scadere directa), galben pana la "
        + $"{RunReport.F(Th.MatchOptLow, 1)} si {RunReport.F(Th.MatchOptHigh, 1)} (-opt) si pana la {RunReport.F(Th.MatchCosmeticLow, 1)} (doar corectie cosmetica), "
        + "rosu dincolo; primele lights ale unei nopti sunt reci.");
}
