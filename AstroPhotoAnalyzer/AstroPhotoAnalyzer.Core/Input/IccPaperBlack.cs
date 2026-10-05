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

// IccPaperBlack.cs - Dmax-ul unei hartii pe o imprimanta, citit din profilul ICC al
// perechii: negrul dispozitivului (toate canalele la capatul intunecat) trecut prin tabela
// colorimetrica a profilului, relativ la albul hartiei (ADR 0028, ADR 0030).
//
// DE CE COLORIMETRIC RELATIV SI NIMIC ALTCEVA. Tabela colorimetrica e masuratoarea: ce
// culoare iese pe hartie. Cea perceptuala e o conventie - pune negrul oricarui profil pe
// mediul de referinta ICC, L* 3.14 (Dmax 2.46), fie ca hartia e baryta sau canvas - iar
// intentia scrisa in antetul profilului e de obicei tocmai cea perceptuala. Aici intentia
// se cere explicit; un test pe un profil cu negrul slab pica daca cineva o schimba.
//
// DE CE RELATIV LA ALBUL HARTIEI. Albul imaginii se tipareste ca albul hartiei, deci
// intrebarea "ce L* din imagine cade sub negru" se pune pe scara aceea, nu pe cea absoluta
// (care ar da cu ~0.03 mai mult pe o baryta).
//
// Tabelele le evalueaza Wacton.Unicolour (v2 lut8/lut16, v4 lutAToB); numele profilului il
// da IccProfileInfo, din tagul 'desc'. Pachetul nu arunca pe un fisier care nu e profil:
// pune o eroare in configuratie si calculeaza mai departe "naiv" (L* 100) - de aceea antetul
// se verifica aici, inainte, si eroarea lui dupa.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Wacton.Unicolour;
using Wacton.Unicolour.Icc;

namespace AstroPhotoAnalyzer
{
    /// <summary>Ce spune un profil ICC despre negrul hartiei, sau de ce nu poate spune.</summary>
    internal sealed record IccBlack
    {
        internal string Path { get; init; }

        /// <summary>Numele din tagul 'desc'; numele fisierului cand profilul n-are unul.</summary>
        internal string Description { get; init; }

        /// <summary>De ce nu s-a putut citi negrul, pentru om; null cand s-a putut.</summary>
        internal string Problem { get; init; }
        internal bool Readable => Problem == null;

        /// <summary>O rezerva despre cifra, pentru om (profilele CMYK); null cand nu e niciuna.</summary>
        internal string Note { get; init; }

        /// <summary>Negrul dispozitivului, relativ la albul hartiei.</summary>
        internal double BlackLstar { get; init; }

        /// <summary>-log10(Y) al aceluiasi negru, rotunjit la cele doua zecimale cu care se afiseaza un Dmax.</summary>
        internal double Dmax { get; init; }
    }

    internal static class IccPaperBlack
    {
        /// <summary>Folderul in care Windows tine profilele instalate; nu exista pe alt sistem.</summary>
        internal static string InstalledFolder
            => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "spool", "drivers", "color");

        static readonly string[] Extensions = { ".icc", ".icm" };

        internal static IccBlack Read(string path)
        {
            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(path);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                return new IccBlack { Path = path, Description = System.IO.Path.GetFileName(path), Problem = "fisierul nu se poate citi: " + e.Message };
            }
            return Read(bytes, path);
        }

        internal static IccBlack Read(byte[] bytes, string path)
        {
            string file = System.IO.Path.GetFileName(path);
            var result = new IccBlack { Path = path, Description = IccProfileInfo.Describe(bytes) ?? file };
            if (!IsProfile(bytes)) return result with { Problem = "nu e un profil ICC" };

            string deviceClass = Signature(bytes, 12), space = Signature(bytes, 16);
            if (deviceClass != "prtr")
                return result with { Problem = $"nu e un profil de imprimanta (clasa '{Printable(deviceClass)}'); negrul hartiei e in profilul hartiei pe imprimanta ei" };

            // Negrul dispozitivului: fara lumina pe RGB, toate cernelurile pe CMYK.
            double[] black;
            string note = null;
            switch (space)
            {
                case "RGB ": black = new double[3]; break;
                case "CMYK":
                    black = new[] { 1.0, 1.0, 1.0, 1.0 };
                    note = "profil CMYK: negrul e cu toate cernelurile la maximum; o imprimanta reala pune mai putina cerneala, deci Dmax-ul ei e ceva mai mic";
                    break;
                default:
                    return result with { Problem = $"profil de imprimanta '{Printable(space)}': se citesc doar profilele RGB si CMYK" };
            }

            try
            {
                var icc = new IccConfiguration(new Profile(bytes, file), Intent.RelativeColorimetric, file);
                if (!string.IsNullOrEmpty(icc.Error)) return result with { Problem = "profilul nu se poate citi: " + icc.Error };
                var colour = new Unicolour(new Configuration(xyzConfig: XyzConfiguration.D50, iccConfig: icc), new Channels(black));
                double y = colour.Xyz.Y;
                // Pachetul inghite o tabela stricata si intoarce un Y oarecare; un negru care nu e
                // intre 0 si alb, sau un Dmax pe care --dmax l-ar refuza, e semnul ei.
                if (double.IsNaN(y) || y <= 0 || y >= 1)
                    return result with { Problem = "tabela colorimetrica a profilului nu da un negru intre 0 si albul hartiei; profilul e probabil stricat" };
                double dmax = Math.Round(-Math.Log10(y), 2);
                if (dmax <= 0 || dmax > Thresholds.DmaxMax)
                    return result with { Problem = $"profilul da Dmax {Fmt.Dmax(dmax)}, in afara intervalului (0, {Fmt.F0(Thresholds.DmaxMax)}]; profilul e probabil stricat" };
                return result with { BlackLstar = ColorMath.LstarOfY(y), Dmax = dmax, Note = note };
            }
            catch (Exception e)
            {
                // Pachetul arunca ArgumentException cu cauza inauntru pe un profil stricat.
                return result with { Problem = "profilul nu se poate citi: " + (e.InnerException ?? e).Message };
            }
        }

        /// <summary>
        /// Profilele de imprimanta din <paramref name="folder"/> (implicit cel al Windows-ului),
        /// dupa nume. Celelalte clase (monitor, spatiu de lucru) si fisierele care nu sunt
        /// profile se sar; un profil de imprimanta care nu se poate citi ramane in lista, cu
        /// motivul lui. Un folder absent da o lista goala.
        /// </summary>
        internal static IReadOnlyList<IccBlack> Installed(string folder = null)
        {
            folder ??= InstalledFolder;
            if (!Directory.Exists(folder)) return Array.Empty<IccBlack>();
            string[] files;
            try
            {
                files = Directory.GetFiles(folder);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // Un folder care nu se lasa listat e ca unul gol: fereastra spune "niciun profil" si ramane "Alege fisier...".
                return Array.Empty<IccBlack>();
            }
            var found = new List<IccBlack>();
            foreach (string path in files.Where(f => Extensions.Contains(System.IO.Path.GetExtension(f).ToLowerInvariant())))
            {
                byte[] bytes;
                try { bytes = File.ReadAllBytes(path); }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { continue; }
                if (!IsProfile(bytes) || Signature(bytes, 12) != "prtr") continue;
                found.Add(Read(bytes, path));
            }
            return found.OrderBy(p => p.Description, StringComparer.OrdinalIgnoreCase).ThenBy(p => p.Path, StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>
        /// Propozitia despre un profil, pentru lista ferestrei: "Dmax 2.41 (negrul la L* 3.5
        /// fata de albul hartiei)", sau de ce nu s-a putut citi.
        /// </summary>
        internal static string Describe(IccBlack b)
            => b.Readable ? $"Dmax {Fmt.Dmax(b.Dmax)} (negrul la L* {Fmt.F1(b.BlackLstar)} fata de albul hartiei)" : b.Problem;

        static bool IsProfile(byte[] b) => b != null && b.Length >= 132 && Signature(b, 36) == "acsp";

        static string Signature(byte[] b, int at) => Encoding.ASCII.GetString(b, at, 4);

        /// <summary>O semnatura de 4 octeti, de aratat omului: fara spatiile de umplere si fara octetii care nu sunt litere.</summary>
        static string Printable(string signature) => new string(signature.Where(c => c > ' ' && c < 127).ToArray());
    }
}
