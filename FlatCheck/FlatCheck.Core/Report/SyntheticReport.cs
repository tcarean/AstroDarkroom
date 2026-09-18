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

// Raportul flatului sintetic: ce profil s-a folosit (sursa, obiectivul, focala si
// diafragma CERUTE si cele GASITE - alegerea e "cel mai apropiat", deci se spune), cat de
// adanca e vignetarea scrisa, in stopuri, in punctele in care s-au comparat masterele reale
// cu bazele, si ce NU contine fisierul. Plus lista de obiective pentru --lenses si textele
// panoului din fereastra (unde sunt profilele Adobe, cum se actualizeaza lensfun), ca ambele
// aplicatii sa le spuna la fel.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace FlatCheck
{
    internal static class SyntheticReport
    {
        internal static void Write(SyntheticFlatResult r, SyntheticRequest req, string referencePath, TextWriter w)
        {
            LensProfile p = r.Profile;
            w.WriteLine($"\n=== FLAT SINTETIC din profilul obiectivului ===");
            w.WriteLine($"    cadru de referinta : {referencePath}  ({r.Width} x {r.Height})");
            w.WriteLine($"    obiectiv           : {p.Lens.Name}  [{p.Lens.Where}]  sursa: {p.Lens.SourceLabel}");
            w.WriteLine($"    profil folosit     : {p.Settings}   (cerut: {Requested(req)})");
            string mismatch = ProfileMismatch(p, req.FocalMm, req.FStop, r.LensProfiles);
            if (mismatch != null) Indent("ATENTIE: " + mismatch, w);
            w.WriteLine($"    model              : {p.Model.Describe()}");
            w.WriteLine($"    fisier profil      : {Path.GetFileName(p.File)}");
            w.WriteLine("    crop aparat        : " + CropLine(r, req));
            w.WriteLine("    format             : " + FormatMatch(p.Model, r.CameraCrop, r.Width, r.Height));

            w.WriteLine("\n  ADANCIMEA VIGNETARII SCRISE (fata de axa)");
            foreach (string line in DepthLines(r)) w.WriteLine("    " + line);

            w.WriteLine();
            Indent("INFORMATIV: " + Caveat(), w);
            Indent(SirilNote(), w);
        }

        /// <summary>
        /// Crop-ul folosit si de unde vine; la lensfun si extrapolarea, la Adobe cand profilul
        /// a fost calibrat pe alt format decat al tau, si "necunoscut" cand s-a luat SFF-ul.
        /// </summary>
        internal static string CropLine(SyntheticFlatResult r, SyntheticRequest req)
            => r.CameraCrop <= 0
                ? $"necunoscut  ({req.CropSource}; SFF {Fmt(r.Profile.Model.CropFactor, "0.###")})"
                : $"{Fmt(r.CameraCrop, "0.###")}  ({req.CropSource})";

        /// <summary>Peste 3 % intre crop si formatul de calibrare: alt format de senzor, nu rotunjirea nominala (1.5 fata de 1.523).</summary>
        internal const double SameFormatTolerance = 0.03;

        /// <summary>
        /// Profilul fata de senzorul tau, pe un rand: pe ce format a fost calibrat, ce format ai,
        /// si ce urmeaza din diferenta - raza recalculata prin mm cand formatele difera, si cat
        /// de departe cade coltul tau fata de coltul de calibrare: inauntru, extrapolare mica
        /// (sub 10 %: D3300 pe profilul lensfun calibrat la 1.61 da 1.06) sau MARE (un full
        /// frame pe un profil DX, 1.5: polinomul de gradul sase nu mai spune nimic acolo).
        /// Consola o tipareste la "format", fereastra o arata la alegerea obiectivului si sub
        /// adancimile scrise; aceeasi propozitie.
        /// </summary>
        internal static string FormatMatch(VignettingModel m, double cameraCrop, int w, int h)
            => FormatMatch(m, cameraCrop, cameraCrop > 0 ? m.Extrapolation(w, h, cameraCrop) : double.NaN);

        /// <summary>Aceeasi propozitie, cu extrapolarea deja calculata (SyntheticFlatResult.Extrapolation).</summary>
        internal static string FormatMatch(VignettingModel m, double cameraCrop, double extrapolation)
        {
            string calib = $"{SensorFormats.LabelOf(m.CropFactor)} (crop {Fmt(m.CropFactor, "0.###")})";
            if (!(cameraCrop > 0))
                return $"profil calibrat pe {calib}; crop-ul tau nu se stie, se ia senzorul de calibrare";
            string yours = $"{SensorFormats.LabelOf(cameraCrop)} ({Fmt(cameraCrop, "0.###")})";
            bool same = Math.Abs(cameraCrop - m.CropFactor) / m.CropFactor <= SameFormatTolerance;
            string head = same
                ? $"profil calibrat pe {calib}; senzorul tau {yours}: acelasi format"
                : $"profil calibrat pe {calib}; senzorul tau {yours}: alt format, raza se recalculeaza prin mm";
            return head + ExtrapolationTail(extrapolation);
        }

        /// <summary>Adevarat cand propozitia de mai sus e un avertisment: coltul tau cade mult dincolo de calibrare.</summary>
        internal static bool FormatMatchIsWarning(VignettingModel m, double cameraCrop, int w, int h)
            => cameraCrop > 0 && m.Extrapolation(w, h, cameraCrop) > SyntheticFlatResult.SmallExtrapolation;

        /// <summary>Ce inseamna coltul tau fata de coltul de calibrare; gol cand coincid.</summary>
        internal static string ExtrapolationTail(double e)
        {
            string es = Fmt(e, "0.00");
            if (!(e > 1.0))
                return e < 0.97 ? $"; coltul tau cade la {es} din coltul de calibrare, in interiorul profilului" : "";
            return e <= SyntheticFlatResult.SmallExtrapolation
                ? $"; coltul tau cade la {es} x coltul de calibrare: extrapolare mica"
                : $"; coltul tau cade la {es} x coltul de calibrare: extrapolare MARE, polinomul nu e de incredere dincolo de ce a fost calibrat - ia un profil calibrat pe formatul tau";
        }

        /// <summary>Coada de extrapolare a unui flat scris; gol cand coltul e in interiorul profilului.</summary>
        internal static string ExtrapolationNote(SyntheticFlatResult r) => ExtrapolationTail(r.Extrapolation);

        internal static string Requested(SyntheticRequest req)
            => $"{Fmt(req.FocalMm, "0.#")} mm din {req.FocalSource}, f/{Fmt(req.FStop, "0.0")} din {req.FStopSource}";

        /// <summary>
        /// Cand profilul folosit e departe de ce s-a cerut: focala la mai mult de
        /// Th.ProfileFocalTolMm / TolFrac, diafragma la mai mult de Th.ProfileStopsTol stopuri.
        /// Nearest nu interpoleaza si nu refuza, deci 500 mm f/1.0 pe Nikkor 35 f/1.8 ajungea
        /// tacut la 35 mm f/1.8, cu singurul semn in "cerut". Propozitia spune ce s-a cerut, ce
        /// s-a luat, ce are obiectivul, si ca adancimea scrisa e a profilului. Null cand totul e
        /// in toleranta. <paramref name="all"/> sunt profilele obiectivului (pentru gama).
        /// </summary>
        internal static string ProfileMismatch(LensProfile used, double focalMm, double fStop, IReadOnlyList<LensProfile> all)
        {
            all ??= new[] { used };
            var parts = new List<string>();
            if (!LensSpec.Close(used.FocalMm, focalMm))
            {
                var focals = all.Select(p => p.FocalMm).Distinct().OrderBy(f => f).Select(f => Fmt(f, "0.#")).ToList();
                parts.Add($"focala ceruta {Fmt(focalMm, "0.#")} mm n-are profil: s-a luat {Fmt(used.FocalMm, "0.#")} mm"
                        + $" (obiectivul are profile la {string.Join(", ", focals)} mm)");
            }
            double stops = Math.Abs(LensCatalog.Stops(used.FStop) - LensCatalog.Stops(fStop));
            if (stops > Th.ProfileStopsTol)
            {
                double lo = all.Min(p => p.FStop), hi = all.Max(p => p.FStop);
                string range = Math.Abs(lo - hi) < 1e-9 ? $"un singur profil, f/{Fmt(lo, "0.0")}" : $"profile de la f/{Fmt(lo, "0.0")} la f/{Fmt(hi, "0.0")}";
                parts.Add($"diafragma ceruta f/{Fmt(fStop, "0.0")} e la {Fmt(stops, "0.0")} stopuri de cel mai apropiat profil, f/{Fmt(used.FStop, "0.0")} ({range})");
            }
            if (parts.Count == 0) return null;
            return string.Join("; ", parts) + ". Adancimea scrisa e a profilului folosit, nu a valorilor cerute.";
        }

        internal static bool ProfileMismatches(LensProfile used, double focalMm, double fStop)
            => ProfileMismatch(used, focalMm, fStop, null) != null;

        static string Fmt(double v, string f) => v.ToString(f, CultureInfo.InvariantCulture);

        internal static string[] DepthLines(SyntheticFlatResult r)
            => DepthRows(r).Select(d => $"{d.Label,-28}: {d.Value}").ToArray();

        /// <summary>
        /// Cele patru puncte ale adancimii scrise, cu nota fiecaruia - ce punct e si cu ce
        /// se compara. Consola tipareste doar eticheta si valoarea; fereastra si nota.
        /// </summary>
        internal static (string Label, string Value, string Note)[] DepthRows(SyntheticFlatResult r) => new[]
        {
            ("colt (cutia de 5 %)", Depth(r.CornerStops),
             "centrul cutiei de 5 % din colt pe care o masoara si testul de colturi, deci direct comparabil cu \"vignetare centru -> colt\" al masterului; e punctul cel mai adanc al flatului"),
            ("marginea lunga (st/dr)", Depth(r.LongEdgeStops),
             "mijlocul laturii din stanga / dreapta (95 % din semilatime, pe axa orizontala); mai putin adanc decat coltul fiindca e mai aproape de axa"),
            ("marginea scurta (sus/jos)", Depth(r.ShortEdgeStops),
             "mijlocul laturii de sus / jos (95 % din semiinaltime, pe axa verticala); cel mai putin adanc dintre cele trei puncte de margine, fiind cel mai aproape de axa"),
            ("jumatate de semidiagonala", Depth(r.HalfDiagonalStops),
             "la jumatatea drumului dintre centru si colt, pe diagonala: mijlocul campului, unde sta cea mai mare parte a obiectului; spune cat din cadere e deja acolo"),
        };

        internal static string Depth(double stops)
            => $"{100 * Math.Pow(2, -stops),5:F1} % din axa  ({stops,5:F2} stopuri)";

        internal static string Caveat()
            => "flatul sintetic e MODELUL obiectivului, nu o masuratoare a exemplarului tau. Are\n"
             + "doar vignetarea, radiala si acromatica (profilele publicate au un singur set de\n"
             + "parametri pentru toate canalele): nu contine partea cromatica a caderii, umbrirea\n"
             + "specifica body-ului, praful, nici vreo structura neradiala. Cele doua baze difera\n"
             + "intre ele cu ~0.2 stopuri in colt pe acelasi obiectiv; atata precizie sa-i ceri.\n"
             + "Ce ii lipseste fata de un flat real - si ce are in plus: nicio sursa de lumina,\n"
             + "deci nici curbura cerului, nici caderea unui panou. Verifica un flat real fata de\n"
             + "el cu --against.";

        internal static string SirilNote()
            => "Scripturile Siril numesc pp_flat_stacked explicit, deci nu iau flatul sintetic singure:\n"
             + "redenumeste-l in locul masterului (pastreaza originalul) sau da-l la calibrate cu -flat=.";

        internal static string SavedLine(string path) => $"\n    -> flat sintetic scris: {path}";

        // ---------------------------------------------------------------- refuzuri

        /// <summary>"ID-ul Nikon 139 din raw (11-20 mm f/2.8)" - ce a spus raw-ul despre obiectiv.</summary>
        internal static string RawLensLabel(LoadedFrame f)
        {
            if (f?.NikonLensId == null) return "(ID obiectiv)";
            string range = LensSpec.Describe(f.LensMinFocalMm, f.LensMaxFocalMm, f.LensMaxAperture);
            return $"ID-ul Nikon {f.NikonLensId.Value} din raw" + (range.Length > 0 ? $" ({range})" : "");
        }

        /// <summary>"gama 135 mm f/2.0 din raw (ID-ul Nikon 74 nu se potriveste cu niciun profil)" - candidatii dupa gama singura.</summary>
        internal static string RawRangeLabel(LoadedFrame f)
        {
            string range = LensSpec.Describe(f?.LensMinFocalMm, f?.LensMaxFocalMm, f?.LensMaxAperture);
            string id = f?.NikonLensId != null ? $" (ID-ul Nikon {f.NikonLensId.Value} nu se potriveste cu niciun profil)" : " (fara ID de obiectiv)";
            return $"gama {range} din raw" + id;
        }

        internal static string NoLensGiven()
            => "FLAT SINTETIC: spune obiectivul cu --lens <text> (cauta in nume; vezi --lenses pentru lista).";

        internal static string NoMatch(string text, ProfileSources sources)
            => $"FLAT SINTETIC: niciun obiectiv nu se potriveste cu \"{text}\".\n" + SourcesSummary(sources)
             + "\nIncearca alt text, sau --lenses <text> ca sa vezi ce e in lista.";

        internal static string Ambiguous(string text, IReadOnlyList<LensChoice> hits)
            => $"FLAT SINTETIC: \"{text}\" se potriveste cu {hits.Count} obiective; alege unul cu un text mai lung"
             + " (sau --source adobe|lensfun):\n" + string.Join("\n", hits.Take(20).Select(h => "    " + h))
             + (hits.Count > 20 ? $"\n    ... si inca {hits.Count - 20}" : "");

        internal static string NoFocal()
            => "FLAT SINTETIC: nu stiu focala - cadrul de referinta n-are FOCALLEN si nu e un raw; da --focal <mm>.";

        internal static string NoFStop()
            => "FLAT SINTETIC: nu stiu diafragma - un FITS n-o poarta; da --fstop <f> (sau un raw ca referinta).";

        internal static string NoCrop(string camera)
            => "FLAT SINTETIC: profilul lensfun are nevoie de crop factor-ul aparatului, si "
             + (camera == null ? "cadrul de referinta nu spune ce aparat e" : $"\"{camera}\" nu e in baza lensfun")
             + $". Da --crop <factor>: {SensorFormats.Describe()}.";

        internal static string RefusalCube(string path, int planes)
            => $"FLAT SINTETIC: {path} are {planes} planuri de culoare (debayerizat); da un cadru CFA sau mono ca referinta.";

        internal static string SourcesSummary(ProfileSources s)
            => $"    Adobe  : {s.AdobeLenses} obiective din {s.AdobeFiles} fisiere"
             + (s.AdobeFilesSkipped > 0 ? $" ({s.AdobeFilesSkipped} sarite)" : "")
             + (s.AdobeFiles == 0 ? " - niciun profil gasit in " + string.Join(" / ", s.AdobeDirectories) : "") + "\n"
             + $"    lensfun: {s.LensfunLenses} obiective din {s.LensfunDirectory}"
             + (s.LensfunPresent ? "" : " - folderul lipseste sau e gol");

        // ---------------------------------------------------------------- --lenses

        internal const int ListDetailsMax = 40;

        internal static void WriteList(IReadOnlyList<LensChoice> lenses, LensCatalog catalog, string text, ProfileSources sources, TextWriter w)
        {
            w.WriteLine($"\n=== OBIECTIVE CU PROFIL DE VIGNETARE" + (string.IsNullOrWhiteSpace(text) ? "" : $" care contin \"{text}\"") + " ===");
            w.WriteLine(SourcesSummary(sources));
            w.WriteLine();
            if (lenses.Count == 0) { w.WriteLine("    (niciunul)"); return; }
            // Focalele si diafragmele cer parsarea fisierului Adobe; pe o lista lunga (fara
            // filtru, mii de obiective) s-ar aduna la zeci de secunde, deci doar pe una scurta.
            bool details = lenses.Count <= ListDetailsMax;
            foreach (LensChoice l in lenses)
            {
                w.WriteLine($"    {l.SourceLabel,-7} {l.Name}  [{l.Where}]");
                if (details) w.WriteLine("            " + LensDetails(l, catalog));
            }
            if (!details) w.WriteLine($"\n    ({ListDetailsHidden(lenses.Count)} - da un text la --lens)");
        }

        /// <summary>
        /// Ce are un obiectiv: "focale: 18, 20 mm; diafragme: f/2.8, f/4.0; distante: 0.36, 1.51 m,
        /// infinit; 3 profile". Distantele crescator, apoi infinit, apoi nespecificata (perechea
        /// lensfun copiata). Un fisier Adobe se parseaza aici, deci consola si fereastra o cer
        /// numai pe o lista de cel mult <see cref="ListDetailsMax"/>. Aceeasi propozitie in
        /// amandoua: consola o tipareste sub rand, fereastra o pune pe a doua linie a randului.
        /// </summary>
        internal static string LensDetails(LensChoice lens, LensCatalog catalog)
        {
            var ps = catalog.ProfilesOf(lens);
            if (ps.Count == 0) return "";
            var focals = ps.Select(p => p.FocalMm).Distinct().OrderBy(f => f).ToList();
            var stops = ps.Select(p => p.FStop).Distinct().OrderBy(f => f).ToList();
            var metres = ps.Select(p => p.DistanceM).Where(d => !double.IsNaN(d) && d < 1000).Distinct().OrderBy(d => d)
                           .Select(d => d.ToString("0.##", CultureInfo.InvariantCulture)).ToList();
            var distances = new List<string>();
            if (metres.Count > 0) distances.Add(string.Join(", ", metres) + " m");
            if (ps.Any(p => p.DistanceM >= 1000)) distances.Add(LensProfile.DistanceInfinity);
            if (ps.Any(p => double.IsNaN(p.DistanceM))) distances.Add(LensProfile.DistanceUnspecified);
            return $"focale: {string.Join(", ", focals.Select(f => f.ToString("0.#", CultureInfo.InvariantCulture)))} mm;"
                 + $" diafragme: f/{string.Join(", f/", stops.Select(f => f.ToString("0.0", CultureInfo.InvariantCulture)))};"
                 + $" distante: {string.Join(", ", distances)}; {ps.Count} profile";
        }

        /// <summary>De ce lipsesc detaliile pe o lista lunga; consola si fereastra adauga fiecare cum se scurteaza lista.</summary>
        internal static string ListDetailsHidden(int count)
            => $"{count} obiective; focalele, diafragmele si distantele se arata cand lista are cel mult {ListDetailsMax}";

        // ---------------------------------------------------------------- panoul din fereastra

        internal static string AdobeWhere(ProfileSources s)
            => "Profilele Adobe (.lcp) le instaleaza Lightroom, Camera Raw sau DNG Converter (gratuit) in:\n"
             + string.Join("\n", s.AdobeDirectories.Select(d => "  " + d)) + "\n"
             + "Se citesc de acolo la pornire; nu se copiaza si nu se redistribuie. Fara Adobe, jumatatea asta a listei e goala.";

        internal static string LensfunWhere(ProfileSources s)
            => "Baza lensfun (CC BY-SA 3.0) e livrata cu unealta in folderul:\n  " + s.LensfunDirectory + "\n"
             + (s.LensfunVersion != null ? s.LensfunVersion + "\n" : "")
             + "Actualizare: descarca fisierele XML din https://github.com/lensfun/lensfun/tree/master/data/db,\n"
             + "inlocuieste-le pe cele din folder si reporneste. Versiunea copiei e in " + ProfileLocator.LensfunVersionFile + ".";

        static void Indent(string text, TextWriter w)
        {
            foreach (string line in text.Split('\n')) w.WriteLine("    " + line);
        }
    }
}
