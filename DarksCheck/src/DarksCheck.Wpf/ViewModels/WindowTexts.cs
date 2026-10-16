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

namespace DarksCheck.Wpf;

/// <summary>The window's own sentences (Romanian), in one place; no figure is decided here.</summary>
internal static class WindowTexts
{
    internal const string AppName = "DarksCheck";

    internal static string AboutTitle => "Despre DarksCheck";

    internal static string AboutTagline => "Verificare de calitate pentru dark frames de astrofoto si potrivirea lor cu o sesiune.";

    internal static string AboutLicenceHeading => "LICENTA";

    internal static string AboutLicenseLinkLabel => "Textul complet al GNU General Public License v3";

    internal static string AboutThirdPartyHeading => "COMPONENTE TERTE";

    internal static string AboutThirdPartyLead =>
        "Unealta leaga doua componente scrise de altii, amandoua compatibile cu GPLv3. Cititorul FITS, "
        + "encoderul PNG, toate calculele si toate pragurile sunt scrise de mana. Motivele pentru care fiecare "
        + "licenta e compatibila sunt in THIRD-PARTY-NOTICES.md.";

    internal static string ChooseTitle => "Alege folderul sesiunii Siril (cel cu darks\\) sau folderul de dark-uri";

    internal static string ChoosePartnerTitle => "Alege folderul partener de dark-uri (o biblioteca sau alta sesiune)";

    internal static string EmptyTitle => "Alege folderul unei sesiuni sau al unei biblioteci de dark-uri";

    internal static string EmptyLead =>
        "Folderul de lucru din Siril, cel care contine darks\\ si, optional, biases\\ si lights\\, sau un folder cu "
        + "dark-urile direct in el (o biblioteca). Analiza porneste singura dupa ce l-ai ales: bias-urile, dark-urile "
        + "si lights-urile sunt decodate si masurate, apoi vine indexul si eticheta.";

    internal static string ProblemTitle => "Folderul nu se poate analiza";

    internal static string Busy(string phase, int done, int total) =>
        total == 0 ? "Se deschide sesiunea..." : $"{phase}: {done.ToString(CultureInfo.InvariantCulture)} / {total.ToString(CultureInfo.InvariantCulture)}";

    internal static string Cancelled => "Analiza a fost oprita.";

    internal static string ExportTitle => "Alege folderul in care se scriu raportul, CSV-urile si hartile";

    internal static string Written(IReadOnlyList<string> paths) => "Scris:\n" + string.Join("\n", paths);

    internal static string NoPartner => "(niciunul: alege o biblioteca sau alta sesiune ca sa compari dark-urile)";

    internal static string PartnerChanged => "Partenerul s-a schimbat: apasa \"Analizeaza din nou\" ca sa-l compari cu sesiunea.";

    internal static string LabelCopied => "Eticheta e in clipboard; redenumeste folderul cu ea daca vrei.";

    internal static string IndexLead =>
        "Indexul e un numar pentru cat de cald a rulat senzorul: mediana amplitudinii pixelilor de pe rangurile "
        + $"{RunReport.F(Th.IndexRankLowPerMpx, 0)}-{RunReport.F(Th.IndexRankHighPerMpx, 0)} pe megapixel ai masterului, normalizata la "
        + $"ISO {RunReport.F(Th.NormIso, 0)} si {RunReport.F(Th.NormExposureSeconds, 0)} s. Eticheta e numele folderului plus idx<NN>: o copiezi "
        + "si redenumesti folderul tu; programul nu scrie si nu redenumeste nimic in folderele cu poze.";

    internal static string NoMap => $"Harta nu are blocuri (cadre mai mici de {(Th.BinPx * 2).ToString(CultureInfo.InvariantCulture)} px) sau grupul lipseste.";

    internal static string HotHeader => ">" + RunReport.F(Th.HotAdu, 0);

    internal static string HotHeaderTip => $"pixeli peste {RunReport.F(Th.HotAdu, 0)} ADU-eq pe planul de referinta";

    internal static string HotTabLead =>
        $"Pixelii fierbinti ai masterului median la cinci niveluri, pe planuri la {RunReport.F(Th.HotAdu, 0)} ADU-eq, cu amplitudinile lor si cat de stabili "
        + "sunt de la un cadru la altul; candidatii RTS sunt pixelii a caror valoare sare intre cadre. Informativ: cati are senzorul, nu un verdict.";

    internal static string CandidatesTip => $"pixeli peste {RunReport.F(Th.LightHotSigmas, 0)} sigma locale, sub saturatie";

    internal static string IsolatedTip => $"candidati fara niciun vecin peste {RunReport.F(Th.LightIsolationSigmas, 0)} sigma: pixeli fierbinti, nu stele";

    internal static string MaxFramesTip =>
        $"Cate cadre se tin in memorie (48 MB fiecare la 24 MP) si cate lights se masoara; peste, se ia unul din k. Implicit {Th.MaxFramesInMemory.ToString(CultureInfo.InvariantCulture)}.";

    internal static string IsoFactorTip =>
        $"De cate ori creste semnalul la o treapta de ISO, pentru normalizarea la ISO {RunReport.F(Th.NormIso, 0)}; {RunReport.F(Normalisation.Default.IsoFactorPerStop, 0)} = liniar in ISO (D3300)";

    internal static string DoublingTip =>
        $"La cate grade se dubleaza curentul de intuneric, pentru gradele din raport; {RunReport.F(Th.DoublingCelsius, 0)} la D3300";

    internal static string MapsLead =>
        $"Hartile planului de referinta in blocuri de {(Th.BinPx * 2).ToString(CultureInfo.InvariantCulture)} px, fiecare intinsa intre percentilele 1 si 99 ale ei: "
        + "masterul dark (media cadrelor), masterul bias si diferenta lor, curentul de intuneric. O margine sau un colt "
        + "mai luminos pe dark - bias e o scurgere de lumina sau glow; unul care apare si in bias e citire, nu lumina.";

    internal static string LightsChartLead =>
        "Amplitudinea pixelilor fierbinti ai masterului in fiecare light, fata de master, normalizata la ISO si expunere. "
        + $"Verde intre {RunReport.F(Th.MatchDirectLow, 1)} si {RunReport.F(Th.MatchDirectHigh, 1)} (scadere directa), galben pana la "
        + $"{RunReport.F(Th.MatchOptLow, 1)} si {RunReport.F(Th.MatchOptHigh, 1)} (-opt) si pana la {RunReport.F(Th.MatchCosmeticLow, 1)} (doar corectie cosmetica), "
        + "rosu dincolo; primele lights ale unei nopti sunt reci.";
}
