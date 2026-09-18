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

// Un profil de vignetare selectabil - o combinatie (obiectiv, focala, diafragma, distanta)
// dintr-una din cele doua surse - si catalogul care le tine pe toate: cautare dupa nume,
// gruparea pe obiectiv, alegerea celui mai apropiat profil de ce s-a cerut.
//
// Alegerea e "cel mai apropiat, raportat", nu interpolare: intai focala cea mai apropiata,
// apoi diafragma cea mai apropiata in stopuri, apoi distanta cea mai mare (flat-urile sunt
// la infinit). Raportul spune mereu ce profil s-a folosit, ca utilizatorul sa vada daca
// 18 mm a stat in loc de 20.
//
// Profilele Adobe se citesc LENES. Masurat pe masina autorului: 3531 de fisiere .lcp,
// 455 MB, 36 de secunde de parsat pe toate - de neacceptat pentru o lista. Numele
// fisierului spune insa tot ce trebuie pentru lista si pentru cautare:
//   <Producator>/<Montura>/<Aparat> (<Obiectiv>) - RAW.lcp
// deci catalogul tine pentru Adobe doar aceste "cioturi" si parseaza un fisier abia cand
// cineva ii cere profilele (obiectivul ales) sau cand cauta un ID de obiectiv, si atunci
// numai in fisierele monturii aparatului. Ce se pierde: aliasurile nu intra in cautare
// pana la parsare. Fisierele parsate se tin minte.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FlatCheck
{
    internal enum ProfileSource { Adobe, Lensfun }

    /// <summary>
    /// Identitatea unui obiectiv intr-o sursa: ce se alege din lista. <paramref name="Calibration"/>
    /// e formatul senzorului pe care a fost calibrat profilul ("APS-C 1.528", "full frame 1"),
    /// si face parte din identitate: baza lensfun tine acelasi obiectiv, pe aceeasi montura, de
    /// doua ori cand doi oameni l-au calibrat pe corpuri de format diferit - 20 din 692 in copia
    /// livrata, printre ele Nikkor 35 f/1.8G DX, a carui intrare "media APS-C" da 0.19 stopuri in
    /// coltul DX la f/2.8 si intrarea D780 0.51, langa Adobe 0.61. Cu cheia fara crop cele doua
    /// se contopeau, Nearest lua prima din fisier, si calibrarea buna nu ajungea niciodata pe
    /// ecran. Eticheta se pune pe TOATE obiectivele lensfun, nu doar pe cele dublate: un nume
    /// care s-ar schimba cand baza primeste a doua intrare ar strica textul din --lens. La
    /// Adobe ramane goala: corpul de calibrare e deja in montura ("NIKON D90"), iar SFF-ul sta
    /// in fisier, nu in numele lui, deci n-ar putea intra in lista incarcata lenes.
    /// </summary>
    internal sealed record LensChoice(ProfileSource Source, string Maker, string Name, string Mount, string Calibration = "")
    {
        internal string Key => $"{Source}|{Maker}|{Name}|{Mount}" + (HasCalibration ? $"|{Calibration}" : "");

        internal string SourceLabel => Source == ProfileSource.Adobe ? "Adobe" : "lensfun";

        internal bool HasCalibration => !string.IsNullOrEmpty(Calibration);

        /// <summary>Montura, plus calibrarea cand se stie: "Nikon F AF, calibrat pe full frame 1".</summary>
        internal string Where => HasCalibration ? $"{Mount}, calibrat pe {Calibration}" : Mount;

        /// <summary>Numele, plus calibrarea cand se stie - pentru propozitii care numesc profilul ales.</summary>
        internal string Title => HasCalibration ? $"{Name} (calibrat pe {Calibration})" : Name;

        /// <summary>"APS-C 1.528", "full frame 1", "format neobisnuit 1.37": formatul nominal si crop-ul exact.</summary>
        internal static string CalibrationLabel(double crop)
            => $"{SensorFormats.LabelOf(crop)} {crop.ToString("0.###", CultureInfo.InvariantCulture)}";

        public override string ToString() => $"{Name} [{Where}] ({SourceLabel})";
    }

    /// <summary>
    /// Gama unui obiectiv citita din numele lui - "11-20mm F2.8", "18-200mm f3.5-5.6G",
    /// "135mm f/2.0" - ca sa poata fi pusa fata in fata cu ce spune MakerNote-ul unui raw.
    /// Exista fiindca un ID de obiectiv Nikon nu e unic: 139 e si Tokina 11-20 f/2.8, si
    /// Nikkor 18-200 f/3.5-5.6, si Sigma 18-35 f/1.8, iar prima alegere dupa ID lua primul
    /// nume in ordine alfabetica - un Nikkor - pentru un raw facut cu Tokina. Null la orice
    /// parte pe care numele n-o spune ("Zeiss Batis 2/25"); nimic nu se ghiceste, si un nume
    /// care nu se citeste nu contrazice nimic.
    /// </summary>
    internal readonly record struct LensSpec(double? MinFocalMm, double? MaxFocalMm, double? MaxAperture)
    {
        // "11-20mm", "18-200 mm", "35mm"; prima aparitie castiga.
        static readonly Regex FocalRx = new(@"(\d+(?:\.\d+)?)\s*(?:-\s*(\d+(?:\.\d+)?))?\s*mm\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        // "f/2.8", "F2.8", "f3.5-5.6G" (se ia deschiderea la focala minima); nu "AF-S", "EF".
        static readonly Regex ApertureRx = new(@"(?<![A-Za-z])[fF]/?\s?(\d+(?:\.\d+)?)", RegexOptions.Compiled);

        /// <summary>Toleranta la focala: 1 mm sau 5 %, care e mai mare - numele rotunjesc.</summary>
        internal const double FocalToleranceMm = 1.0, FocalToleranceFraction = 0.05;
        /// <summary>Toleranta la deschidere, in stopuri: codurile Nikon merg in sesimi de stop.</summary>
        internal const double ApertureToleranceStops = 0.25;

        internal static LensSpec Parse(string name)
        {
            if (string.IsNullOrEmpty(name)) return default;
            double? min = null, max = null, ap = null;
            Match f = FocalRx.Match(name);
            if (f.Success)
            {
                min = double.Parse(f.Groups[1].Value, CultureInfo.InvariantCulture);
                max = f.Groups[2].Success ? double.Parse(f.Groups[2].Value, CultureInfo.InvariantCulture) : min;
            }
            Match a = ApertureRx.Match(name);
            if (a.Success) ap = double.Parse(a.Groups[1].Value, CultureInfo.InvariantCulture);
            return new LensSpec(min, max, ap);
        }

        /// <summary>
        /// Adevarat cand numele spune o gama si raw-ul spune alta. O parte lipsa de oricare
        /// parte nu contrazice.
        /// </summary>
        internal bool Contradicts(double? minFocal, double? maxFocal, double? maxAperture)
        {
            if (minFocal.HasValue && MinFocalMm.HasValue && !Close(MinFocalMm.Value, minFocal.Value)) return true;
            if (maxFocal.HasValue && MaxFocalMm.HasValue && !Close(MaxFocalMm.Value, maxFocal.Value)) return true;
            if (maxAperture.HasValue && MaxAperture.HasValue
                && Math.Abs(2 * Math.Log2(MaxAperture.Value / maxAperture.Value)) > ApertureToleranceStops) return true;
            return false;
        }

        static bool Close(double a, double b) => Math.Abs(a - b) <= Math.Max(FocalToleranceMm, FocalToleranceFraction * Math.Max(a, b));

        /// <summary>"11-20 mm f/2.8", "135 mm f/2.0", "11-20 mm", "" cand nu se stie nimic.</summary>
        internal static string Describe(double? minFocal, double? maxFocal, double? maxAperture)
        {
            string focal = "";
            if (minFocal.HasValue && maxFocal.HasValue)
                focal = Math.Abs(minFocal.Value - maxFocal.Value) < 0.5
                    ? $"{minFocal.Value.ToString("0.#", CultureInfo.InvariantCulture)} mm"
                    : $"{minFocal.Value.ToString("0.#", CultureInfo.InvariantCulture)}-{maxFocal.Value.ToString("0.#", CultureInfo.InvariantCulture)} mm";
            else if (minFocal.HasValue || maxFocal.HasValue)
                focal = $"{(minFocal ?? maxFocal).Value.ToString("0.#", CultureInfo.InvariantCulture)} mm";
            string ap = maxAperture.HasValue ? $"f/{maxAperture.Value.ToString("0.0", CultureInfo.InvariantCulture)}" : "";
            return focal.Length > 0 && ap.Length > 0 ? focal + " " + ap : focal + ap;
        }
    }

    internal sealed class LensProfile
    {
        internal readonly LensChoice Lens;
        internal readonly IReadOnlyList<string> Aliases;
        internal readonly IReadOnlyList<long> LensIds;
        internal readonly double FocalMm;
        internal readonly double FStop;
        /// <summary>
        /// Distanta de focalizare in metri; NaN cand profilul n-o da - un Adobe fara
        /// FocusDistance, sau o pereche lensfun 10 / 1000 m cu aceiasi coeficienti
        /// (LensfunReader.CollapseCopiedDistances), care e "o masuratoare, distanta nescrisa".
        /// </summary>
        internal readonly double DistanceM;
        internal readonly VignettingModel Model;
        internal readonly string File;

        internal LensProfile(LensChoice lens, IReadOnlyList<string> aliases, IReadOnlyList<long> lensIds,
                             double focalMm, double fStop, double distanceM, VignettingModel model, string file)
        {
            Lens = lens;
            Aliases = aliases ?? Array.Empty<string>();
            LensIds = lensIds ?? Array.Empty<long>();
            FocalMm = focalMm;
            FStop = fStop;
            DistanceM = distanceM;
            Model = model;
            File = file;
        }

        internal LensProfile WithLens(LensChoice lens)
            => new LensProfile(lens, Aliases, LensIds, FocalMm, FStop, DistanceM, Model, File);

        internal const string DistanceUnspecified = "nespecificata", DistanceInfinity = "infinit";

        /// <summary>"0.36 m" / "infinit" (1000 m si peste) / "nespecificata" (NaN) - o singura scriere a distantei, pentru Settings si pentru lista.</summary>
        internal static string DistanceLabel(double distanceM)
            => double.IsNaN(distanceM) ? DistanceUnspecified
             : distanceM >= 1000 ? DistanceInfinity
             : $"{distanceM.ToString("0.##", CultureInfo.InvariantCulture)} m";

        /// <summary>"20 mm, f/2.8, infinit" / ", 1.51 m" / ", distanta nespecificata" (NaN: profilul n-o spune).</summary>
        internal string Settings
            => $"{FocalMm.ToString("0.#", CultureInfo.InvariantCulture)} mm, f/{FStop.ToString("0.0", CultureInfo.InvariantCulture)}, "
             + (double.IsNaN(DistanceM) ? "distanta " : "") + DistanceLabel(DistanceM);
    }

    /// <summary>Un fisier Adobe neparsat inca: ce se stie din numele si folderul lui.</summary>
    internal sealed class AdobeProfileFile
    {
        internal readonly string Path;
        internal readonly LensChoice Lens;

        internal AdobeProfileFile(string path, LensChoice lens)
        {
            Path = path;
            Lens = lens;
        }

        /// <summary>
        /// "NIKON CORPORATION (Tokina AT-X 11-20mm F2.8 PRO DX) - RAW.lcp" in folderul
        /// Tokina/Nikon: aparatul inainte de prima paranteza, obiectivul intre prima paranteza
        /// si perechea ei (poate contine paranteze: "(IF)"), producatorul e folderul bunic.
        /// Un nume fara paranteze e un aparat cu obiectiv fix, si ramane ciot cu numele lui.
        /// </summary>
        internal static AdobeProfileFile FromName(string path)
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            int open = name.IndexOf(" (", StringComparison.Ordinal);
            int depth = 0, close = -1;
            for (int i = open + 1; open > 0 && i < name.Length; i++)
            {
                if (name[i] == '(') depth++;
                else if (name[i] == ')' && --depth == 0) { close = i; break; }
            }
            if (open <= 0 || close < 0)
            {
                // Telefoane si compacte cu obiectiv fix: "Apple/iPhone 6.lcp", "Canon/Canon PowerShot
                // G12 - RAW.lcp". Numele e aparatul, producatorul e folderul; montura nu exista.
                string folder = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(path) ?? "") ?? "";
                int dash = name.LastIndexOf(" - ", StringComparison.Ordinal);
                string camera = (dash > 0 ? name.Substring(0, dash) : name).Trim();
                return new AdobeProfileFile(path, new LensChoice(ProfileSource.Adobe, folder.Length > 0 ? folder : camera, camera, ""));
            }
            string make = name.Substring(0, open).Trim();
            string lens = name.Substring(open + 2, close - open - 2).Trim();
            string kind = name.Substring(close + 1).Trim().TrimStart('-').Trim();
            string mount = make + (kind.Length == 0 || kind == "RAW" ? "" : $", {kind}");
            string[] words = lens.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string maker = words.Length > 0 ? words[0] : make;
            return new AdobeProfileFile(path, new LensChoice(ProfileSource.Adobe, maker, lens, mount));
        }
    }

    /// <summary>Toate profilele gasite, de oriunde, si intrebarile care se pun listei.</summary>
    internal sealed class LensCatalog
    {
        /// <summary>Profilele parsate: toate cele lensfun, plus cele Adobe date gata parsate (teste).</summary>
        internal readonly IReadOnlyList<LensProfile> Profiles;

        /// <summary>Fisierele Adobe neparsate, cunoscute din nume.</summary>
        internal readonly IReadOnlyList<AdobeProfileFile> AdobeFiles;

        readonly ConcurrentDictionary<string, IReadOnlyList<LensProfile>> _parsed = new(StringComparer.OrdinalIgnoreCase);

        internal LensCatalog(IReadOnlyList<LensProfile> profiles, IReadOnlyList<AdobeProfileFile> adobeFiles = null)
        {
            Profiles = profiles ?? Array.Empty<LensProfile>();
            AdobeFiles = adobeFiles ?? Array.Empty<AdobeProfileFile>();
        }

        internal static readonly LensCatalog Empty = new LensCatalog(Array.Empty<LensProfile>());

        internal IReadOnlyList<LensChoice> Lenses
            => Profiles.Select(p => p.Lens).Concat(AdobeFiles.Select(f => f.Lens)).Distinct()
                .OrderBy(l => l.Source).ThenBy(l => l.Maker).ThenBy(l => l.Name).ThenBy(l => l.Mount).ToList();

        /// <summary>
        /// Obiectivele al caror nume, producator, montura sau calibrare contine TOATE cuvintele
        /// cautate (si aliasurile, pentru profilele deja parsate), fara sa conteze majusculele;
        /// textul gol le da pe toate. Optional doar dintr-o sursa. Calibrarea e in cautare ca
        /// "Nikkor 35mm full" sa aleaga una din cele doua intrari lensfun ale aceluiasi obiectiv.
        /// </summary>
        internal IReadOnlyList<LensChoice> Search(string text, ProfileSource? source = null)
        {
            string[] words = (text ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var hits = new List<LensChoice>();
            foreach (LensChoice lens in Lenses)
            {
                if (source.HasValue && lens.Source != source.Value) continue;
                string haystack = lens.Name + " " + lens.Maker + " " + lens.Mount + " " + lens.Calibration + " "
                                + string.Join(" ", Profiles.Where(p => p.Lens == lens).SelectMany(p => p.Aliases).Distinct());
                if (words.All(w => haystack.Contains(w, StringComparison.OrdinalIgnoreCase)))
                    hits.Add(lens);
            }
            return hits;
        }

        /// <summary>Profilele unui obiectiv; un fisier Adobe se parseaza acum, o singura data.</summary>
        internal IReadOnlyList<LensProfile> ProfilesOf(LensChoice lens)
        {
            IEnumerable<LensProfile> eager = Profiles.Where(p => p.Lens == lens);
            IEnumerable<LensProfile> lazy = AdobeFiles.Where(f => f.Lens == lens).SelectMany(Parse);
            return eager.Concat(lazy).OrderBy(p => p.FocalMm).ThenBy(p => p.FStop).ThenBy(p => p.DistanceM).ToList();
        }

        IReadOnlyList<LensProfile> Parse(AdobeProfileFile f)
            => _parsed.GetOrAdd(f.Path, path =>
            {
                try { return LcpReader.Read(path).Select(p => p.WithLens(f.Lens)).ToList(); }
                catch (Exception) { return Array.Empty<LensProfile>(); }   // un fisier stricat nu e al nostru; lista merge mai departe
            });

        /// <summary>
        /// Obiectivele Adobe care poarta ID-ul de obiectiv Nikon dat (LensID sau AlternateLensIDs).
        /// Parseaza fisierele a caror montura sau aparat contine <paramref name="makerHint"/>
        /// (cele Nikon pentru un D3300), in paralel, sau pe toate cand indiciul lipseste.
        /// </summary>
        internal IReadOnlyList<LensChoice> ByLensId(long id, string makerHint = null)
        {
            var hits = new List<LensChoice>(Profiles.Where(p => p.LensIds.Contains(id)).Select(p => p.Lens));
            IEnumerable<AdobeProfileFile> files = AdobeFiles;
            if (!string.IsNullOrWhiteSpace(makerHint))
                files = files.Where(f => (f.Lens.Mount + " " + f.Lens.Maker).Contains(makerHint, StringComparison.OrdinalIgnoreCase));
            var found = new ConcurrentBag<LensChoice>();
            Parallel.ForEach(files, f =>
            {
                if (Parse(f).Any(p => p.LensIds.Contains(id))) found.Add(f.Lens);
            });
            hits.AddRange(found);
            return hits.Distinct().OrderBy(l => l.Name).ThenBy(l => l.Mount).ToList();
        }

        /// <summary>
        /// Din potrivirile dupa ID, doar cele al caror nume NU contrazice gama din raw
        /// (focala minima / maxima, deschiderea maxima): un nume care nu se citeste ramane.
        /// Fara nicio gama, lista ramane intreaga.
        /// </summary>
        internal static IReadOnlyList<LensChoice> Narrow(IReadOnlyList<LensChoice> hits, double? minFocal, double? maxFocal, double? maxAperture)
            => hits.Where(h => !LensSpec.Parse(h.Name).Contradicts(minFocal, maxFocal, maxAperture)).ToList();

        /// <summary>
        /// Profilul cel mai apropiat de (focala, diafragma) dintre cele ale unui obiectiv:
        /// focala cea mai apropiata, apoi diafragma cea mai apropiata in stopuri, apoi
        /// distanta cea mai mare. Null cand obiectivul n-are niciun profil.
        /// </summary>
        internal static LensProfile Nearest(IReadOnlyList<LensProfile> profiles, double focalMm, double fStop)
        {
            if (profiles == null || profiles.Count == 0) return null;
            double bestFocal = profiles.Min(p => Math.Abs(p.FocalMm - focalMm));
            var atFocal = profiles.Where(p => Math.Abs(Math.Abs(p.FocalMm - focalMm) - bestFocal) < 1e-9).ToList();
            double bestStops = atFocal.Min(p => Math.Abs(Stops(p.FStop) - Stops(fStop)));
            var atStop = atFocal.Where(p => Math.Abs(Math.Abs(Stops(p.FStop) - Stops(fStop)) - bestStops) < 1e-9).ToList();
            return atStop.OrderByDescending(p => double.IsNaN(p.DistanceM) ? double.NegativeInfinity : p.DistanceM).First();
        }

        /// <summary>Diafragma in stopuri (log2 f^2), ca f/2.8 si f/4 sa fie la o unitate.</summary>
        internal static double Stops(double fStop) => 2 * Math.Log2(fStop);
    }
}
