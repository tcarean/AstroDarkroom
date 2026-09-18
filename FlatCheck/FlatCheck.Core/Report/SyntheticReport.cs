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
            w.WriteLine($"    obiectiv           : {p.Lens.Name}  [{p.Lens.Mount}]  sursa: {p.Lens.SourceLabel}");
            w.WriteLine($"    profil folosit     : {p.Settings}   (cerut: {Requested(req)})");
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
        {
            string calib = $"{SensorFormats.LabelOf(m.CropFactor)} (crop {Fmt(m.CropFactor, "0.###")})";
            if (!(cameraCrop > 0))
                return $"profil calibrat pe {calib}; crop-ul tau nu se stie, se ia senzorul de calibrare";
            string yours = $"{SensorFormats.LabelOf(cameraCrop)} ({Fmt(cameraCrop, "0.###")})";
            bool same = Math.Abs(cameraCrop - m.CropFactor) / m.CropFactor <= SameFormatTolerance;
            string head = same
                ? $"profil calibrat pe {calib}; senzorul tau {yours}: acelasi format"
                : $"profil calibrat pe {calib}; senzorul tau {yours}: alt format, raza se recalculeaza prin mm";
            return head + ExtrapolationTail(m.Extrapolation(w, h, cameraCrop));
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
                w.WriteLine($"    {l.SourceLabel,-7} {l.Name}  [{l.Mount}]");
                if (!details) continue;
                var ps = catalog.ProfilesOf(l);
                var focals = ps.Select(p => p.FocalMm).Distinct().OrderBy(f => f).ToList();
                var stops = ps.Select(p => p.FStop).Distinct().OrderBy(f => f).ToList();
                w.WriteLine($"            focale: {string.Join(", ", focals.Select(f => f.ToString("0.#", CultureInfo.InvariantCulture)))} mm;"
                          + $" diafragme: f/{string.Join(", f/", stops.Select(f => f.ToString("0.0", CultureInfo.InvariantCulture)))}; {ps.Count} profile");
            }
            if (!details) w.WriteLine($"\n    ({lenses.Count} obiective; focalele si diafragmele se arata cand lista are cel mult {ListDetailsMax} - da un text la --lens)");
        }

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
