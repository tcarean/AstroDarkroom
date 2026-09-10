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

// ExposureReport.cs - ExposureResult -> textul din consola. Doar formatare.
//
// Corectiile sugerate sunt log2(tinta/centru) stopuri, unde tinta e mijlocul benzii -
// 57 pe panou, 30 pe cer. Nu se mai alege aici intre cele doua: vine din r.Bands, adica
// din exact scara pe care s-a dat verdictul.
//
// Trei bucati de aici NU se tiparesc in consola - HistogramReading, ChannelNote si
// BandLabel. Sunt textul care insoteste histogramele din fereastra, si stau aici din
// acelasi motiv ca RunReport.MapLegend: formularea are un singur loc, chiar daca azi are
// un singur cititor. In consola n-ar avea ce cauta - un paragraf despre forma unei curbe
// pe care consola n-o deseneaza.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FlatCheck
{
    internal static class ExposureReport
    {
        internal static void Write(ExposureResult r, string path, TextWriter w)
        {
            w.WriteLine($"\n=== EXPUNERE: {path} ===");
            w.WriteLine($"    saturatie (white) : {r.White:F0}   black : {r.Black:F0}   sursa: {r.ScaleSource}");

            if (r.IsCfa)
            {
                // Culorile se scriu doar cand fisierul chiar a spus ordinea. Fara card,
                // linia ramane exact cea de dinainte - pozitiile anonime - fiindca ordinea
                // difera de la aparat la aparat si o eticheta ghicita ar parea masurata.
                w.WriteLine(r.Cfa != null
                    ? $"    nivel median GLOBAL per canal CFA (ordinea R/G/B din {r.Cfa.Source}):"
                    : "    nivel median GLOBAL per canal CFA (informativ; nu stiu care e R/G/B):");
                for (int i = 0; i < 4; i++)
                {
                    string mk = i == r.BrightestChannel ? "  <- cel mai luminos" : "";
                    w.WriteLine($"        {ChannelColumn(r, i)}: {(r.ChannelMedians[i] - r.Black) / r.Range * 100,5:F1}%{mk}");
                }
            }
            else
            {
                w.WriteLine("    imagine mono/procesata (fara CFA)");
            }

            w.WriteLine($"    canal cel mai luminos, mediana GLOBALA : {r.GlobalMedianPct,5:F1}%  (trasa in jos de colturi)");
            w.WriteLine($"    canal cel mai luminos, CENTRU 20%      : {r.CentreMedianPct,5:F1}%  <- ASTA decide expunerea");
            w.WriteLine($"    clipping in centru (>=98% white)       : {r.CentreClipPct:F3}%");

            if (!r.IsRaw)
            {
                WriteNotRaw(r, w);
                return;
            }

            w.WriteLine();

            if (r.IsSky) WriteSky(r, w);
            else WritePanel(r, w);
        }

        static void WriteNotRaw(ExposureResult r, TextWriter w)
        {
            w.WriteLine();
            w.WriteLine("    !! FISIERUL NU PARE UN FLAT BRUT !!");
            if (r.LevelTooLow)
            {
                w.WriteLine($"    Nivelul e ~{r.CentreMedianPct:F1}% (langa zero/negativ) -> flat-dark deja scazut,");
                w.WriteLine("    sau imagine normalizata. --expo are nevoie de nivelul ADU original.");
            }
            if (r.LooksDebayered)
            {
                w.WriteLine($"    Cele 4 canale CFA sunt quasi-identice (spread {r.CfaSpread:F3}) -> fisierul");
                w.WriteLine("    e DEBAYERIZAT. --expo are nevoie de mozaicul Bayer intact.");
            }
            w.WriteLine();
            w.WriteLine("    Cauza tipica: ai deschis NEF-ul in Siril si ai dat Save (asta debayerizeaza).");
            w.WriteLine("    SOLUTII:");
            w.WriteLine("      - ruleaza direct pe NEF (flatcheck citeste brutul exact):");
            w.WriteLine("          flatcheck flat.NEF --expo");
            w.WriteLine("      - SAU produ un FITS brut cu tab-ul Conversion din Siril (NU Open+Save),");
            w.WriteLine("        care pastreaza mozaicul CFA nedebayerizat.");
        }

        static void WriteSky(ExposureResult r, TextWriter w)
        {
            bool clipping = r.CentreClipPct > Th.ExpoClip;
            ExposureBands b = r.Bands;
            w.WriteLine("    [mod --sky: praguri pt flats pe cer, nu pe panou]");
            switch (r.Verdict)
            {
                case ExposureVerdict.TooHigh:
                    w.WriteLine("    VERDICT: PREA SUS" + (clipping ? " (CLIPPING in centru!)" : " (risc stele in cadru)"));
                    w.WriteLine("    Scade expunerea.");
                    break;
                case ExposureVerdict.TooLow:
                    w.WriteLine("    VERDICT: PREA JOS pentru sky flat");
                    w.WriteLine($"    Creste cu ~{Math.Log2(b.Target / r.CentreMedianPct):F1} stopuri (dar repede, cerul se stinge).");
                    break;
                case ExposureVerdict.Ok:
                    w.WriteLine($"    VERDICT: OK pentru sky flat  (centru {b.Lo:F0}-{b.Hi:F0}%)");
                    w.WriteLine("    La sky flat nivelul modest e corect. SNR-ul vine din numarul de cadre,");
                    w.WriteLine("    nu din a urca expunerea (ai risca stele). NU forta mai sus.");
                    break;
                default:
                    w.WriteLine("    VERDICT: cam sus, dar acceptabil");
                    w.WriteLine("    Sub 60% e ok; peste, risti stele. Nu urca mai mult.");
                    break;
            }
        }

        static void WritePanel(ExposureResult r, TextWriter w)
        {
            bool clipping = r.CentreClipPct > Th.ExpoClip;
            ExposureBands b = r.Bands;
            double cen = r.CentreMedianPct;
            switch (r.Verdict)
            {
                case ExposureVerdict.TooHigh:
                    {
                        double stops = cen > 0 ? Math.Log2(b.Target / cen) : -1;
                        w.WriteLine("    VERDICT: PREA SUS" + (clipping ? " (CLIPPING in centru!)" : ""));
                        w.WriteLine($"    Scade expunerea cu ~{-stops:F1} stopuri.");
                        if (clipping)
                            w.WriteLine("    Centrul clipeaza -> pierzi info de vignetare exact unde semnalul e maxim.");
                        break;
                    }
                case ExposureVerdict.TooLow:
                    w.WriteLine("    VERDICT: PREA JOS");
                    w.WriteLine($"    Creste expunerea cu ~{Math.Log2(b.Target / cen):F1} stopuri.");
                    w.WriteLine("    Sub ~30% in centru masterul iese zgomotos (zgomot fixed-pattern).");
                    break;
                case ExposureVerdict.AcceptableLow:
                    w.WriteLine("    VERDICT: ACCEPTABIL, dar cam jos");
                    w.WriteLine($"    Ai putea creste cu ~{Math.Log2(b.Target / cen):F1} stopuri.");
                    break;
                case ExposureVerdict.Ok:
                    w.WriteLine($"    VERDICT: OK  (centrul in tinta {b.Lo:F0}-{b.Hi:F0}%)");
                    break;
                default:
                    w.WriteLine("    VERDICT: ACCEPTABIL, dar cam sus");
                    w.WriteLine($"    Ai putea scadea cu ~{-Math.Log2(b.Target / cen):F1} stopuri.");
                    break;
            }
        }

        // =============================================================== pentru fereastra
        //
        // Ce urmeaza nu se tipareste nicaieri in consola. Vezi nota din capul fisierului.

        /// <summary>
        /// Coloana din stanga a unui canal: numele culorii cand se stie, plus pozitia -
        /// care ramane vizibila oricum, ca linia sa se poata lega de mozaic.
        /// </summary>
        static string ChannelColumn(ExposureResult r, int i)
            => r.Cfa != null
                ? $"{r.ChannelLabel(i),-2} (CFA[{i / 2},{i % 2}])"
                : r.ChannelLabel(i);

        /// <summary>Eticheta benzii tinta, pentru desen: "tinta 50-65 %".</summary>
        internal static string BandLabel(ExposureBands b)
            => b.Sky ? $"tinta sky {b.Lo:F0}-{b.Hi:F0} %" : $"tinta {b.Lo:F0}-{b.Hi:F0} %";

        /// <summary>
        /// Nota scurta de sub eticheta unui canal, sau "" cand n-are nimic de spus.
        /// </summary>
        internal static string ChannelNote(ExposureResult r, ChannelLevels c)
        {
            var parts = new List<string>();
            if (c.IsBrightest && r.IsCfa) parts.Add("cel mai luminos - decide expunerea");
            if (c.ClipPct > Th.ExpoClip) parts.Add($"clipping {c.ClipPct:F2} % pe tot canalul");
            return string.Join("; ", parts);
        }

        /// <summary>
        /// Cum se citeste expunerea fisierului asta, in cuvinte. Nu adauga nicio masuratoare
        /// si nu schimba niciun verdict: pune in propozitii cifrele care sunt deja pe ecran,
        /// fiindca legatura dintre ele - de ce curba lata NU inseamna subexpunere, cat mai e
        /// pana la saturatie - nu se vede din desen singur.
        /// </summary>
        internal static string HistogramReading(ExposureResult r)
        {
            if (!r.IsRaw)
                return "Fisierul nu e un flat brut - e deja calibrat sau debayerizat - deci ce se "
                     + "vede mai jos e distributia lui de niveluri, nu o expunere. Cifrele n-au "
                     + "cum sa insemne ceva pana nu rulezi pe cadrul brut de la aparat.";

            ChannelLevels c = r.Channels[r.IsCfa ? r.BrightestChannel : 0];
            ExposureBands b = r.Bands;
            var s = new List<string>();

            // 1. cine decide
            if (!r.IsCfa)
                s.Add("Cadrul e mono sau deja procesat, deci se masoara pe tot, nu pe canale.");
            else if (c.Colour != '\0')
                s.Add($"Canalul cel mai luminos e {c.Label}, deci el decide: el clipeaza primul.");
            else
                s.Add($"Canalul cel mai luminos e {c.Label} - nu stiu ce culoare are - deci el "
                    + "decide: el clipeaza primul.");

            // 2. unde sta varful fata de banda
            double cen = r.CentreMedianPct;
            if (cen < b.Lo)
                s.Add($"Varful ferestrei centrale e la {cen:F1} %, cu {b.Lo - cen:F1} puncte sub "
                    + $"{BandLabel(b)}.");
            else if (cen > b.Hi)
                s.Add($"Varful ferestrei centrale e la {cen:F1} %, cu {cen - b.Hi:F1} puncte peste "
                    + $"{BandLabel(b)}.");
            else
                s.Add($"Varful ferestrei centrale e la {cen:F1} %, in {BandLabel(b)}.");

            // 3. cat mai e pana sus - sau ca nu mai e deloc
            if (r.CentreClipPct > Th.ExpoClip)
                s.Add($"Centrul atinge deja saturatia ({r.CentreClipPct:F3} % din pixelii lui), "
                    + "deci PREA SUS oricare ar fi mediana - acolo se pierde exact informatia "
                    + "de vignetare, unde semnalul e maxim.");
            else if (c.CentreP99Pct > 0)
                s.Add($"Cel mai luminos 1 % din centru ajunge la {c.CentreP99Pct:F1} % din white, "
                    + $"deci mai sunt ~{Math.Log2(100.0 / c.CentreP99Pct):F1} stopuri pana la saturatie.");

            // 4. de ce curba din fundal e lata
            double drop = cen - c.MedianPct;
            if (drop > 0.5)
                s.Add($"Mediana intregului canal e cu {drop:F1} puncte sub centru - aia e "
                    + "vignetarea, nu subexpunere, si curba lata din fundal o arata. Daca "
                    + "expui dupa ea, clipezi centrul.");

            // 5. daca mai clipeaza si altcineva
            if (r.IsCfa)
            {
                var others = r.Channels
                    .Where(x => x.Position != c.Position && x.ClipPct > Th.ExpoClip)
                    .Select(x => x.Label)
                    .ToList();
                if (others.Count > 0)
                    s.Add($"Are pixeli saturati si pe tot cadrul, nu doar in centru: "
                        + $"{string.Join(", ", others)}.");
            }

            return string.Join(" ", s);
        }

        // ================================================= previzualizarea aparatului
        //
        // Tot pentru fereastra, tot netiparit in consola. Aici se pun cap la cap cele doua
        // cifre - ce a masurat senzorul si ce a aratat ecranul - si se spune de ce difera.
        // Nimic de aici nu voteaza.

        /// <summary>
        /// Perechea raw -> ecran pe fiecare canal, CENTRU cu CENTRU: mediana ferestrei de
        /// 20 % din raw langa mediana aceleiasi ferestre din previzualizare. Nu se compara
        /// medianele intregului cadru - cea din raw e trasa in jos de vignetare intr-un fel
        /// in care cea din previzualizare nu e, si diferenta ar fi in parte obiectivul.
        ///
        /// Imperecherea e dupa CULOAREA canalului raw, nu dupa indexul lui si nu dupa
        /// mozaicul din header: un raw judecat mono are un singur canal fara culoare, chiar
        /// daca fisierul a spus ordinea. Cei doi verzi se mediaza fata de singurul G.
        /// Fara nicio culoare pe partea raw, o singura pereche: cel mai luminos cu cel mai
        /// luminos, si eticheta o spune.
        /// </summary>
        internal static IReadOnlyList<(string Channel, char Colour, double RawPct, double ScreenPct)>
            ScreenComparison(ExposureResult r)
        {
            var rows = new List<(string, char, double, double)>();
            if (r.Preview == null) return rows;

            bool anyColour = r.Channels.Any(c => c.Colour != '\0');
            if (anyColour)
            {
                foreach (ChannelLevels p in r.Preview.Channels)
                {
                    var raws = r.Channels.Where(c => c.Colour == p.Colour).ToList();
                    if (raws.Count == 0) continue;
                    string label = raws.Count == 1
                        ? p.Label
                        : $"{p.Label} (media {string.Join(", ", raws.Select(c => c.Label))})";
                    rows.Add((label, p.Colour, raws.Average(c => c.CentreMedianPct), p.CentreMedianPct));
                }
                return rows;
            }

            ChannelLevels rawBest = r.Channels[r.IsCfa ? r.BrightestChannel : 0];
            ChannelLevels prevBest = r.Preview.Channels.OrderByDescending(c => c.MedianPct).First();
            rows.Add(($"cel mai luminos: {rawBest.Label} in raw, {prevBest.Label} pe ecran",
                      '\0', rawBest.CentreMedianPct, prevBest.CentreMedianPct));
            return rows;
        }

        /// <summary>
        /// De ce difera atat de mult cele doua, si la ce mai e bun ecranul. Spune doar ce
        /// sustine rezultatul: diferenta masurata pe canalul care decide, mecanismul (gama
        /// plus curba de ton, pe care nicio setare nu le scoate), de ce un flat e cazul cel
        /// mai nefavorabil, si regula utila - ecranul se satureaza inaintea senzorului cand
        /// balansul de alb amplifica si curba ajunge la 255, deci e o alarma intr-un singur
        /// sens, nu un instrument de masura. Formulata ca mecanism si observatie, nu ca lege.
        /// </summary>
        internal static string ScreenReading(ExposureResult r)
        {
            if (r.Preview == null) return "";

            var s = new List<string>();
            if (!r.IsRaw)
                s.Add("Fisierul nu e un flat brut - e deja calibrat sau debayerizat - deci niciuna "
                    + "din cele doua curbe nu e o expunere. Comparatia de mai jos ramane adevarata "
                    + "despre cele doua developari, atat.");

            ChannelLevels raw = r.Channels[r.IsCfa ? r.BrightestChannel : 0];
            ChannelLevels screen = raw.Colour != '\0'
                ? r.Preview.Channels.FirstOrDefault(c => c.Colour == raw.Colour)
                : null;
            screen ??= r.Preview.Channels.OrderByDescending(c => c.MedianPct).First();

            double gap = screen.CentreMedianPct - raw.CentreMedianPct;
            s.Add($"Pe canalul care decide, senzorul e la {raw.CentreMedianPct:F1} % si ecranul "
                + $"aparatului arata {screen.CentreMedianPct:F1} %: {gap:+0.0;-0.0} puncte, in "
                + "aceeasi fereastra centrala de 20 %.");

            s.Add("Cea mai mare parte e gama: un JPEG e codificat gamma prin definitie, ca sa "
                + "imparta 256 de trepte dupa cum vede ochiul, nu dupa cat primeste senzorul. "
                + "Nicio setare din aparat n-o scoate - Picture Control Neutral si contrastul "
                + "la minim taie curba SUPLIMENTARA, nu codificarea. Restul e curba de ton si "
                + "balansul de alb, care e diferit pe fiecare canal.");

            s.Add("O curba compresiva intinde cel mai tare nivelurile de jos si de mijloc, "
                + "adica exact unde sta un flat. De aia un flat pare alarmant pe ecran cand nu e, "
                + "si de aia diferenta nu e o constanta de tinut minte: se schimba cu aparatul, "
                + "cu Picture Control si cu lumina.");

            s.Add("La ce mai e bun ecranul: cat timp balansul de alb amplifica un canal si curba "
                + "de ton ajunge la 255, JPEG-ul se satureaza INAINTEA senzorului - pe aparatele "
                + "masurate aici, cu o treime de stop pana la un stop. Deci \"nu e lipit de "
                + "margine\" e o garantie solida ca n-ai ars nimic. \"E lipit\" nu e informatie: de "
                + "acolo incolo ecranul sta pe loc, oricat ai depasi. E o alarma intr-un singur "
                + "sens, nu un instrument de masura.");

            return string.Join(" ", s);
        }

        /// <summary>De ce nu se poate arata comparatia pe fisierul asta.</summary>
        internal static string NoPreviewReason(ExposureResult r)
            => "Fisierul asta nu poarta o previzualizare a aparatului - un FITS n-are niciodata, "
             + "iar unele raw-uri convertite o pierd - sau ea nu s-a putut decoda. Nu am ce "
             + "pune langa nivelul real, deci se arata doar datele brute. Verdictul nu e afectat: "
             + "el se da oricum numai pe ele.";
    }
}
