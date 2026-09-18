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

// StructureReport.cs - StructureResult -> textul din consola. Doar formatare.

using System;
using System.Collections.Generic;
using System.IO;

namespace FlatCheck
{
    /// <summary>
    /// Un rand al tabelului "vignetarea fata de profilul obiectivului", gata formatat: sursa
    /// (Adobe / lensfun), obiectivul, montura si calibrarea (Where), profilul folosit, adancimea lui, diferenta fata
    /// de master, eroarea in colt si verdictul; IsNearest pe cel care da verdictul, Note cu
    /// formatul de calibrare fata de senzor (IsWarning cand e o extrapolare mare).
    /// </summary>
    internal sealed record ProfileRow(string Key, string Source, string Lens, string Where, string Profile,
                                      string Depth, string Diff, string Error, string Verdict,
                                      bool IsNearest, string Note, bool IsWarning);

    internal static class StructureReport
    {
        internal static void Write(StructureResult r, string path, TextWriter w)
            => Write(new StructureOutcome(r, null, null, null), path, w);

        /// <summary>Cu comparatia vignetarii, cand rularea a avut una; altfel identic cu varianta de mai sus.</summary>
        internal static void Write(StructureOutcome o, string path, TextWriter w)
        {
            StructureResult r = o.Structure;
            w.WriteLine($"\n=== STRUCTURA: {path} ===");
            w.WriteLine($"    dimensiune analiza : {r.Width} x {r.Height} (dupa 2x2 + downsample)");
            w.WriteLine($"    median / max ADU   : {Levels(r)}");
            WriteBody(r, w, o);
        }

        /// <summary>
        /// "median / max", formatate dupa scara fisierului. Siril scrie masterul ca float
        /// impartit la 65535, deci mediana e ~0.014 - iar cu zero zecimale linia iesea
        /// "0 / 0", ca si cum fisierul ar fi gol. Pragul de 1.5 e acelasi pe care --expo
        /// il foloseste ca sa recunoasca un fisier normalizat. Il arata si fereastra.
        /// </summary>
        internal static string Levels(StructureResult r)
        {
            bool normalised = r.RawMax <= 1.5;
            string fmt = normalised ? "F4" : "F0";
            string line = $"{r.RawMedian.ToString(fmt)} / {r.RawMax.ToString(fmt)}";
            return normalised ? line + " (float normalizat 0..1)" : line;
        }

        /// <summary>
        /// Tot ce urmeaza dupa antet: informativele, cele cinci teste, scara si
        /// verdictul. Scos separat fiindca modul de atribuire ruleaza aceeasi baterie
        /// pe masterul combinat (A+B)/2 si vrea exact acest corp sub antetul lui - dar
        /// nu si randul de ADU, care pe un raster normalizat la mediana 1 n-ar insemna
        /// nimic.
        /// </summary>
        internal static void WriteBody(StructureResult r, TextWriter w) => WriteBody(r, w, null);

        /// <param name="o">
        /// Rularea intreaga, cand a avut o comparatie cu profilele: blocul ei se scrie dupa
        /// scara, iar verdictul si sfatul sunt ale ei (cel mai rau dintre forma si adancime).
        /// Null sau fara comparatie = cele cinci teste si atat.
        /// </param>
        static void WriteBody(StructureResult r, TextWriter w, StructureOutcome o)
        {
            WriteInformative(r, w);

            w.WriteLine("\n  TESTE");
            w.WriteLine($"    imprastiere 4 colturi   : {r.CornerSpreadPct,5:F2} %   [{r.CornerVerdict}]");
            foreach (var kv in r.Corners)
                w.WriteLine($"        {kv.Key,-14}: {kv.Value / r.CornerMean * 100,6:F2} % din media colturilor");
            w.WriteLine($"    asimetrie azimutala max : {r.AzimuthalMaxPct,5:F2} %   [{r.AzimuthalVerdict}]");
            w.WriteLine($"    reziduu dupa model rad. : {r.ResidualPtpPct,5:F2} %   [{r.ResidualVerdict}]  <- CEL MAI IMPORTANT");
            w.WriteLine($"    inclinare (gradient)    : {r.TiltPct,5:F2} %   [{r.TiltVerdict}]");
            w.WriteLine($"    pata maxima (o dala)    : {r.MaxTilePct,5:F2} %   [{r.MaxTileVerdict}]");

            WriteScale(w);

            if (o?.Profiles != null) WriteProfiles(o, w);

            w.WriteLine($"\n  VERDICT: {o?.Verdict ?? r.Verdict}");
            if (o != null && o.Compared)
            {
                Indent(VerdictDetail(o), w);
                Indent(Advice(o), w);
            }
            else WriteAdvice(r.Verdict, w);
        }

        // ------------------------------------------------ vignetarea fata de profilul obiectivului

        /// <summary>Titlul blocului; fereastra il pune pe cardul ei.</summary>
        internal const string ProfilesHeading = "VIGNETAREA fata de profilul obiectivului";

        static void WriteProfiles(StructureOutcome o, TextWriter w)
        {
            VignettingComparison c = o.Profiles;
            w.WriteLine($"\n  {ProfilesHeading}");
            if (!c.Compared)
            {
                Indent("nu se poate compara: " + GapText(o), w);
                Indent(GapMeaning(), w);
                return;
            }
            w.WriteLine($"    master                  : {MasterDepth(c)}");
            foreach (ProfileRow row in ProfileRows(c))
            {
                w.WriteLine($"    {row.Source,-7} {row.Lens}  [{row.Where}]");
                w.WriteLine($"        profil {row.Profile,-24}: {row.Depth}   diferenta {row.Diff} = {row.Error} [{row.Verdict}]"
                          + (row.IsNearest ? "  <- cel mai apropiat, da verdictul" : ""));
                if (row.IsWarning) w.WriteLine("        " + row.Note);
            }
            Indent(ComparisonSummary(c), w);
            w.WriteLine($"    ({AgainstReport.DepthScale()})");
            Indent(DepthScaleNote(), w);
            Indent(ComparisonNote(), w);
        }

        /// <summary>
        /// Scara adancimii ca date, pentru fereastra: aceleasi patru benzi ca scara de reziduu,
        /// cu intervalul in stopuri si eroarea din colt care ii corespunde. Pragurile sunt tot
        /// Th.Resid*, trecute prin Stats.StopsForError - nu e o a doua lista de numere.
        /// </summary>
        internal static IReadOnlyList<(string Band, string Range, string Meaning)> DepthScaleRows()
        {
            string S(double pct) => Stats.StopsForError(pct).ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
            return new[]
            {
                ("PERFECT", $"sub {S(Th.ResidPerfect)} stopuri", $"eroare in colt sub {Th.ResidPerfect:F0} %"),
                ("BUN", $"{S(Th.ResidPerfect)} - {S(Th.ResidUsable)} stopuri", $"eroare in colt {Th.ResidPerfect:F0} - {Th.ResidUsable:F0} %"),
                ("ACCEPTABIL", $"{S(Th.ResidUsable)} - {S(Th.ResidCheck)} stopuri", $"eroare in colt {Th.ResidUsable:F0} - {Th.ResidCheck:F0} %; sub atat nu se deosebeste de incertitudinea modelului"),
                ("REFA", $"peste {S(Th.ResidCheck)} stopuri", $"eroare in colt peste {Th.ResidCheck:F0} %; refa flat-urile"),
            };
        }

        /// <summary>De unde vine scara adancimii: nu e un prag nou, e scara celor cinci teste tradusa in stopuri.</summary>
        internal static string DepthScaleNote()
            => $"Aceeasi scara de reziduu ca cele cinci teste ({Th.ResidPerfect:F0} / {Th.ResidUsable:F0} / {Th.ResidCheck:F0} %), aplicata erorii pe care\n"
             + "diferenta de adancime o lasa in coltul unui light calibrat cu masterul, 2^|stopuri| - 1, si\n"
             + "tradusa in stopuri. Nu e un prag nou.";

        /// <summary>"1.26 stopuri (colt = 42 % din centru)" - adancimea masterului, cum o masoara si randul informativ.</summary>
        internal static string MasterDepth(VignettingComparison c) => Depth(c.MasterStops);

        static string Depth(double stops) => $"{stops:F2} stopuri (colt = {100 * Math.Pow(2, -stops):F0} % din centru)";

        /// <summary>Un rand pe profil, gata formatat, pentru consola si pentru fereastra.</summary>
        internal static IReadOnlyList<ProfileRow> ProfileRows(VignettingComparison c)
        {
            var rows = new List<ProfileRow>();
            foreach (ProfileDepth d in c.Profiles)
            {
                LensProfile p = d.Profile;
                bool warning = d.Extrapolation > SyntheticFlatResult.SmallExtrapolation;
                string note = SyntheticReport.FormatMatch(p.Model, d.CameraCrop, d.Extrapolation);
                rows.Add(new ProfileRow(p.Lens.Key, p.Lens.SourceLabel, p.Lens.Name, p.Lens.Where, p.Settings,
                    Depth(d.CornerStops), $"{d.DiffStops:+0.00;-0.00} stopuri", $"{d.ErrorPct:F1} %", d.Verdict,
                    ReferenceEquals(d, c.Nearest), note, warning));
            }
            return rows;
        }

        /// <summary>
        /// Al cui e verdictul si cat difera profilele intre ele. "Cel mai apropiat", nu "cel
        /// mai rau": cele doua baze difera intre ele cu ~0.2 stopuri pe acelasi obiectiv, si un
        /// master care coincide cu una din ele nu e gresit.
        /// </summary>
        internal static string ComparisonSummary(VignettingComparison c)
        {
            string who = $"{c.Nearest.Profile.Lens.SourceLabel} {c.Nearest.Profile.Lens.Title}";
            string verdict = $"verdictul comparatiei: {c.Verdict} - al celui mai apropiat profil ({who}), "
                           + $"{c.Nearest.DiffStops:+0.00;-0.00} stopuri = {c.Nearest.ErrorPct:F1} % eroare in coltul unui light calibrat cu masterul";
            string spread = c.Profiles.Count > 1
                ? $"\ncele {c.Profiles.Count} profile difera intre ele cu {c.SpreadStops:F2} stopuri in colt; un master care coincide cu unul din ele nu e gresit"
                : "\nun singur profil: fara a doua baza nu se stie cat de incert e modelul (de obicei ~0.2 stopuri)";
            return verdict + spread + RangeOnlyNote(c.Shot);
        }

        /// <summary>
        /// Cand obiectivele au fost gasite doar dupa gama (obiectiv manual, fara ID utilizabil):
        /// lista poate cuprinde si alt obiectiv cu aceeasi gama, si omul trebuie sa stie.
        /// </summary>
        internal static string RangeOnlyNote(ShotIdentity shot)
            => shot != null && shot.CandidatesByRangeOnly
                ? $"\nobiectivele de mai sus sunt cele ale monturii cu {shot.RawLensLabel}: obiectiv manual sau necunoscut bazelor. "
                + "Daca printre ele e si altul cu aceeasi gama, citeste randul obiectivului tau, nu pe cel mai apropiat."
                : "";

        /// <summary>Ce poate si ce nu poate spune comparatia. O arata si fereastra, sub tabel.</summary>
        internal static string ComparisonNote()
            => "Diferenta e RADIALA, deci invizibila celor cinci teste, hartii si modului --ratio: e singurul\n"
             + "loc unde se vede o sursa care cade cu unghiul (plus) sau umple colturile (minus). Profilul e\n"
             + "modelul obiectivului, nu exemplarul tau, si e acromatic; masterul e media 2x2 a mozaicului.";

        /// <summary>De ce nu s-a putut compara, in cuvinte.</summary>
        internal static string GapText(StructureOutcome o)
        {
            VignettingComparison c = o.Profiles;
            ShotIdentity shot = c.Shot;
            switch (c.Gap)
            {
                case ComparisonGap.NoProfiles:
                    return "niciun profil de obiectiv pe masina - nici Adobe (Lightroom / Camera Raw / DNG Converter), nici folderul lensfun/ langa program";
                case ComparisonGap.NoRaw:
                    return o.RawError != null
                        ? $"raw-ul de langa master nu s-a putut citi ({o.RawError}), deci nu se stie obiectivul"
                        : "niciun raw langa master (in flats/), deci niciun ID de obiectiv; un FITS nu spune ce obiectiv l-a facut. Da --lens <text> sau --raw <fisier>";
                case ComparisonGap.NoLensMatch:
                    return shot != null && shot.HasRawLensId
                        ? $"{shot.RawLensLabel}: niciun obiectiv din catalog nu i se potriveste (dupa ID si gama). Da --lens <text>"
                        : "niciun obiectiv nu se potriveste cu textul dat; vezi --lenses";
                case ComparisonGap.NoFocal:
                    return "focala nu se stie - nici FOCALLEN in master, nici raw; da --focal <mm>";
                case ComparisonGap.NoFStop:
                    return "diafragma nu se stie - Siril n-o scrie in master, si n-a fost niciun raw langa el; da --fstop <f> sau --raw <fisier>";
                case ComparisonGap.NoCrop:
                    return $"doar profile lensfun, si crop-ul aparatului nu se stie ({shot?.CropSource}); da --crop <factor>";
                default:
                    return "";
            }
        }

        /// <summary>Ce ar fi aratat comparatia, ca sa se stie ce lipseste.</summary>
        internal static string GapMeaning()
            => "Comparatia ar fi spus daca adancimea vignetarii e cea a obiectivului: un exces radial - caderea\n"
             + "unui panou, curbura cerului - trece neobservat prin cele cinci teste si prin --ratio.";

        /// <summary>Cele doua parti ale verdictului, pe doua randuri, cum le tipareste consola sub VERDICT.</summary>
        internal static string VerdictDetail(StructureOutcome o)
            => $"forma     : {ShapeDetail(o)}\nadancimea : {DepthDetail(o)}\n{WorseOfBoth}";

        /// <summary>"PERFECT - cele cinci teste de abatere de la simetria radiala". Separat, ca fereastra sa-si puna eticheta ei in fata.</summary>
        internal static string ShapeDetail(StructureOutcome o)
            => $"{o.Structure.Verdict} - cele cinci teste de abatere de la simetria radiala";

        /// <summary>"REFA - +0.34 stopuri fata de lensfun Tokina ... = 27.0 % eroare in coltul light-ului calibrat".</summary>
        internal static string DepthDetail(StructureOutcome o)
        {
            ProfileDepth n = o.Profiles.Nearest;
            return $"{o.Profiles.Verdict} - {n.DiffStops:+0.00;-0.00} stopuri fata de {n.Profile.Lens.SourceLabel} {n.Profile.Lens.Title} = {n.ErrorPct:F1} % eroare in coltul light-ului calibrat";
        }

        internal const string WorseOfBoth = "Cel mai rau dintre ele.";

        /// <summary>Sfatul rularii: al adancimii cand ea a inrautatit verdictul, altfel al formei.</summary>
        internal static string Advice(StructureOutcome o)
            => o.DepthCarriesTheVerdict ? DepthAdvice(o.Profiles.Nearest.DiffStops, o.Profiles.Verdict) : Advice(o.Structure.Verdict);

        /// <summary>
        /// Ce sa faca omul cand vignetarea masterului nu e cea a obiectivului. Semnul e
        /// diagnosticul: plus = masterul cade mai mult decat modelul, deci SURSA cade cu unghiul
        /// (masurat pe masterele de tableta: cos^2 al unghiului de camp, +0.57 stopuri, facut
        /// radial de rotatia la 90 de grade); minus = sursa umple colturile (cupola cerului
        /// se lumineaza spre orizont de jur imprejurul zenitului, ambient, o reflexie). Nici
        /// --ratio, nici mai multe rotatii nu ajuta: excesul se roteste cu aparatul sau e
        /// simetrizat de rotatie.
        /// </summary>
        internal static string DepthAdvice(double diffStops, string verdict)
        {
            string cause = diffStops > 0
                ? "Masterul cade spre colt MAI MULT decat modelul obiectivului: sursa de lumina cade cu\n"
                + "unghiul - un panou (monitor, tableta) prea aproape sau fara un difuzor adevarat; foile\n"
                + "mate nu difuzeaza. Ajuta un difuzor lambertian (acril opal, un teanc gros de hartie\n"
                + "alba) sau un perete alb luminat din lateral; nu ajuta mai multe rotatii."
                : "Masterul cade spre colt MAI PUTIN decat modelul obiectivului: sursa umple colturile - o\n"
                + "cupola de cer (se lumineaza spre orizont de jur imprejur), lumina ambianta pe langa\n"
                + "sursa, o reflexie in parasolar. Fereste obiectivul de tot ce nu e sursa; pe cer, fereste\n"
                + "zenitul si asteapta un cer mai uniform.";
            string weight = verdict == "REFA"
                ? "\nExcesul e radial, deci cele cinci teste si --ratio nu-l vad; nu-l va scoate nici background\n"
                + "extraction. Refa flat-urile."
                : "\nExcesul e radial, deci cele cinci teste si --ratio nu-l vad. Sub ACCEPTABIL nu se deosebeste\n"
                + "de incertitudinea modelului (cele doua baze difera intre ele cu ~0.2 stopuri).";
            return cause + weight;
        }

        /// <summary>
        /// Marimile care NU sunt teste. Vignetarea e o proprietate a
        /// obiectivului, nu o eroare. Offsetul centrului de iluminare a fost si el un
        /// test pana in 1.2.0 si nu mai e: fiind ~ -b/(2d), adica raportul dintre
        /// gradient si curbura, nu e scale-invariant - iar peste pragul la care varful
        /// iese din cadru, nici macar monoton. Vezi Metrics.FitCenter.
        /// </summary>
        static void WriteInformative(StructureResult r, TextWriter w)
        {
            w.WriteLine("\n  INFORMATIV (nu sunt teste)");
            w.WriteLine($"    vignetare centru->colt  : {r.VignetteStops:F2} stopuri (colt = {r.CornerMean / r.CentreMedian * 100:F0}% din centru)");
            if (r.CentroidValid)
            {
                w.WriteLine($"    offset centru iluminare : {r.CentroidOffsetPct,5:F2} %");
                w.WriteLine($"        varf iluminare = ({r.FitCx:F0}, {r.FitCy:F0})  vs  centru senzor = ({r.SensorCx:F0}, {r.SensorCy:F0})");
            }
            else
            {
                w.WriteLine("    offset centru iluminare : nedeterminat (varf in afara cadrului sau fit degenerat)");
            }
            w.WriteLine("        E raportul dintre gradient si curbura, deci nu e scale-invariant: pe un");
            w.WriteLine("        obiectiv aproape fara vignetare creste mult fara ca eroarea sa creasca,");
            w.WriteLine("        iar peste un prag cade inapoi spre 0. Amplitudinea o da \"inclinare\".");
            w.WriteLine($"    pete locale (praf etc.) : {LocalSpotValue(r)}");
            foreach (string line in LocalSpotNote().Split('\n')) w.WriteLine("        " + line);
            string warning = LocalSpotWarning(r);
            if (warning != null) Indent(warning, w);
        }

        /// <summary>
        /// Cifra de "pete locale": marimea cu semn, ce inseamna semnul, si pozitia in
        /// rasterul de analiza.
        ///
        /// Semnul e numit chiar pe rand, nu lasat sa fie dedus. Un minus pe o linie care
        /// se cheama "pete locale" arata ca o eroare de calcul daca nu stii ca directia e
        /// tocmai informatia - prima intrebare pusa dupa ce a aparut cifra a fost daca
        /// poate fi negativa.
        /// </summary>
        internal static string LocalSpotValue(StructureResult r)
            => LocalSpotValue(r.LocalSpotPct, r.LocalSpotX, r.LocalSpotY);

        /// <summary>
        /// Aceeasi formulare pentru o pata masurata pe alt raster - modul de netezire o
        /// citeste de pe componenta scoasa, nu de pe reziduul structurii.
        /// </summary>
        internal static string LocalSpotValue(double pct, int x, int y)
        {
            // Ce se rotunjeste la zero SE ARATA ca zero: .NET pastreaza semnul unui zero
            // negativ si "-0.00001" iesea "-+0.00" (sectiunea pozitiva a formatului, cu
            // minusul zeroului negativ in fata). Masurat pe golden-ul modului de netezire.
            double shown = Math.Abs(pct) < 0.005 ? 0.0 : pct;
            return $"{shown:+0.00;-0.00} %{SignWord(shown)} la ({x}, {y})";
        }

        /// <summary>
        /// Eticheta unui test, cu cheia numelui cifrei din <see cref="StructureResult"/>,
        /// ca <see cref="TestNote"/>. E ce tipareste WriteBody in fata cifrei; raportul
        /// de netezire o refoloseste in tabelul lui "inainte / dupa", ca cele doua rapoarte
        /// sa numeasca testele la fel.
        /// </summary>
        internal static string TestLabel(string metric) => metric switch
        {
            nameof(StructureResult.CornerSpreadPct) => "imprastiere 4 colturi",
            nameof(StructureResult.AzimuthalMaxPct) => "asimetrie azimutala max",
            nameof(StructureResult.ResidualPtpPct) => "reziduu dupa model rad.",
            nameof(StructureResult.TiltPct) => "inclinare (gradient)",
            nameof(StructureResult.MaxTilePct) => "pata maxima (o dala)",
            _ => null,
        };

        /// <summary>Cheile celor cinci teste, in ordinea din raport.</summary>
        internal static readonly string[] TestKeys =
        {
            nameof(StructureResult.CornerSpreadPct), nameof(StructureResult.AzimuthalMaxPct),
            nameof(StructureResult.ResidualPtpPct), nameof(StructureResult.TiltPct),
            nameof(StructureResult.MaxTilePct),
        };

        /// <summary>Cifra si verdictul testului <paramref name="metric"/> din <paramref name="r"/>.</summary>
        internal static (double Value, string Verdict) Test(StructureResult r, string metric) => metric switch
        {
            nameof(StructureResult.CornerSpreadPct) => (r.CornerSpreadPct, r.CornerVerdict),
            nameof(StructureResult.AzimuthalMaxPct) => (r.AzimuthalMaxPct, r.AzimuthalVerdict),
            nameof(StructureResult.ResidualPtpPct) => (r.ResidualPtpPct, r.ResidualVerdict),
            nameof(StructureResult.TiltPct) => (r.TiltPct, r.TiltVerdict),
            nameof(StructureResult.MaxTilePct) => (r.MaxTilePct, r.MaxTileVerdict),
            _ => throw new ArgumentException("test necunoscut: " + metric),
        };

        /// <summary>
        /// Directia abaterii, in cuvinte. Gol la exact zero (un reziduu constant), unde
        /// "mai luminos" ar fi o afirmatie despre nimic.
        /// </summary>
        static string SignWord(double pct)
            => pct < 0 ? " (mai intunecat)" : pct > 0 ? " (mai luminos)" : "";

        /// <summary>
        /// Ce e cifra, ce inseamna semnul ei, si de la ce valoare urmeaza o atentionare.
        ///
        /// Pragul se INTERPOLEAZA din <see cref="Th.LocalSpotWarn"/>, nu se scrie ca
        /// literal: altfel o recalibrare a pragului ar lasa textul spunand vechea valoare,
        /// si exact acest text e singurul loc din care utilizatorul afla de unde incolo
        /// conteaza cifra. Pinuit de <c>TheLocalSpotNoteQuotesTheThresholdFromTh</c>.
        ///
        /// Intors ca sir, ca <see cref="Advice"/>: il arata si fereastra, sub rand.
        /// </summary>
        internal static string LocalSpotNote()
            => "Cea mai adanca pata compacta, dupa scoaterea structurii de scara mare.\n"
             + "Semnul spune ce fel: minus = mai intunecat decat imprejurimile (praf,\n"
             + "condens, un obstacol), plus = mai luminos (scurgere de lumina, reflexie).\n"
             + $"Peste {Th.LocalSpotWarn:F0} % urmeaza o atentionare; sub atat cifra e doar informativa.";

        /// <summary>
        /// Atentionarea pentru o pata compacta, sau null cand nu e cazul. Nu e un verdict,
        /// si de aceea nu voteaza: o pata fixa pe aparat e corecta pentru sesiunea ale
        /// carei lights o au si ele, si gresita pentru un flat de biblioteca - raspunsul
        /// depinde de ce faci cu flat-ul, nu de flat. Intors ca sir fiindca il arata si
        /// fereastra, sub verdict.
        /// </summary>
        internal static string LocalSpotWarning(StructureResult r)
            => r.LocalSpotFlagged
                ? $"ATENTIE: pata compacta de {Math.Abs(r.LocalSpotPct):F2} % la ({r.LocalSpotX}, {r.LocalSpotY}), peste {Th.LocalSpotWarn:F0} %.\n"
                + "    E fixa pe aparat - praf pe senzor sau pe filtru, condens, o scurgere mica.\n"
                + "    Corecta pentru sesiunea ale carei lights au aceeasi pata; NU pentru un\n"
                + "    flat de biblioteca. Cauta pozitia de mai sus in harta reziduala."
                : null;

        /// <summary>
        /// Ce masoara fiecare dintre cele cinci teste, intr-o propozitie.
        ///
        /// FARA APELANT IN CONSOLA, ca <see cref="MapScaleLabel"/> si celelalte: raportul
        /// de consola isi tine explicatiile grupate sub scara, iar cinci note intercalate
        /// intre randuri ar ineca tocmai cifrele. Fereastra pune fiecare nota sub randul
        /// ei, unde nu costa nimic.
        ///
        /// Stau totusi aici, si nu in ViewModel, fiindca sunt propozitii de diagnostic:
        /// regula e ca nicio explicatie pentru utilizator nu se scrie intr-un ViewModel,
        /// ca sa nu existe doua formulari ale aceluiasi lucru cand mai apare un consumator.
        /// Doua dintre ele chiar erau scrise inline acolo.
        ///
        /// Cheia e numele cifrei din <see cref="StructureResult"/>, nu un index: o lista
        /// pe pozitii s-ar rupe tacut daca cineva reordoneaza randurile.
        /// </summary>
        internal static string TestNote(string metric) => metric switch
        {
            nameof(StructureResult.CornerSpreadPct) =>
                "cat difera intre ele cele patru cutii de colt; pe un camp pur radial ar fi egale",

            nameof(StructureResult.AzimuthalMaxPct) =>
                "cat difera intre ele cele 12 sectoare ale aceluiasi inel, pe inelul cel mai "
              + "prost; doar in cercul inscris, singurul loc unde toate azimuturile sunt "
              + "esantionate",

            nameof(StructureResult.ResidualPtpPct) =>
                "masoara direct cat din cadru NU se explica printr-un model pur radial",

            nameof(StructureResult.TiltPct) =>
                "amplitudinea varf-la-varf a gradientului liniar peste cadru; prinde si o axa "
              + "optica decentrata, ponderata cu cat costa ea fotometric",

            nameof(StructureResult.MaxTilePct) =>
                "cea mai abatuta dala de pe tot cadrul; prinde un defect compact pe care "
              + "reziduul il taie ca extrema",

            _ => null,
        };

        /// <summary>
        /// Scara comuna a celor cinci teste de abatere de la simetria radiala: numele
        /// benzii, intervalul si intelesul. Nu se aplica offsetului centrului de
        /// iluminare, care masoara o pozitie.
        ///
        /// Se intoarce ca date, nu ca text deja scris, fiindca o citesc doi consumatori:
        /// raportul de mai jos, care o insira in coloane, si aplicatia WPF, care o pune
        /// intr-un tabel cu banda curenta evidentiata. Pragurile raman singurele din Th;
        /// o a doua lista de benzi ar fi divergat la prima recalibrare.
        /// </summary>
        internal static IReadOnlyList<(string Band, string Range, string Meaning)> Scale()
            => new[]
            {
                ("PERFECT", $"sub {Th.ResidPerfect:F0} %",
                            "calitate perfecta"),
                ("BUN", $"{Th.ResidPerfect:F0} - {Th.ResidUsable:F0} %",
                        "utilizabil; background extraction scoate ce ramane"),
                ("ACCEPTABIL", $"{Th.ResidUsable:F0} - {Th.ResidCheck:F0} %",
                               "utilizabil, dar confirma cu --light inainte de un stretch dur"),
                ("REFA", $"peste {Th.ResidCheck:F0} %",
                         "refa flat-urile"),
            };

        /// <summary>
        /// De ce reziduul dupa modelul radial e cel care conteaza. Scos separat din
        /// acelasi motiv ca <see cref="Scale"/>: il arata si fereastra, sub tabel.
        /// </summary>
        internal static string WhyResidualMatters()
            => "Cel mai important e \"reziduu dupa model rad.\": masoara direct cat din\n"
             + "cadru NU se explica printr-un model pur radial. E o LIMITA SUPERIOARA a\n"
             + "erorii de flat, nu eroarea insasi - cuprinde si partea fixa pe aparat\n"
             + "(optica; se imparte corect din lights) si partea fixa in lume (iluminarea;\n"
             + "nu se reproduce). Modul --ratio le desparte. Imprastierea colturilor,\n"
             + "asimetria azimutala si inclinarea sunt vederi partiale ale aceleiasi abateri.\n"
             + "\"Pata maxima\" prinde ce le scapa celorlalte: un defect compact - o scurgere\n"
             + "mica, o pata de condens - pe care reziduul il taie ca extrema.";

        static void WriteScale(TextWriter w)
        {
            w.WriteLine("\n  SCARA celor cinci teste de abatere de mai sus");
            foreach (var (_, range, meaning) in Scale())
                w.WriteLine($"    {range,-13}{meaning}");
            Indent(WhyResidualMatters(), w);
        }

        /// <summary>
        /// Ce sa faca omul cu verdictul asta. Intors ca sir, nu scris direct, fiindca il
        /// arata si fereastra, sub verdictul colorat - iar doua formulari ale aceluiasi
        /// sfat ar diverge exact acolo unde conteaza cel mai mult sa nu divergem.
        /// </summary>
        internal static string Advice(string verdict) => verdict switch
        {
            "PERFECT" =>
                "Abaterea de la simetria radiala e sub zgomot. Flat-ul e curat.",

            "BUN" =>
                "Utilizabil ca atare. Ce ramane sta in intervalul pe care background\n"
              + "extraction il scoate fara efort.",

            "ACCEPTABIL" =>
                "Utilizabil, dar ruleaza modul --light pe un light calibrat inainte de\n"
              + "un stretch dur, ca sa confirmi ca flat-ul chiar corecteaza.",

            // Trimite la --ratio INAINTE de "refa": cifra e o limita superioara, si o
            // parte fixa pe aparat (o optica genuin ne-radiala, o axa decentrata) iese
            // tot aici, fara sa fie o eroare de flat. Masurat: o axa decentrata cu 5% din
            // latime da REFA pe un flat care corecteaza perfect.
            _ =>
                "Abatere prea mare de la simetria radiala => sursa neuniforma, light\n"
              + "leak, camera inclinata, sau ambient. NU e vignetare. Inainte sa refaci,\n"
              + "ruleaza --ratio pe doua stive rotite: daca abaterea e fixa pe aparat,\n"
              + "flat-ul e corect. Altfel refa flat-urile. Vezi harta pentru forma problemei.",
        };

        static void WriteAdvice(string verdict, TextWriter w) => Indent(Advice(verdict), w);

        static void Indent(string text, TextWriter w)
        {
            foreach (string line in text.Split('\n')) w.WriteLine("    " + line);
        }
    }
}
