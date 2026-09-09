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

// SirilSession.cs - ce inseamna, pe disc, "folderul unei sesiuni".
//
// Aplicatia de consola primeste fisiere; asta primeste UN folder si deduce restul prin
// conventia scripturilor Siril (flats/ darks/ biases/ lights/ process/). Tot ce tine de
// deducerea aia sta aici, si nu in ferestre, dintr-un motiv practic: e singura parte a
// aplicatiei WPF care are reguli adevarate - ce fisier e masterul, care fisiere sunt
// candidati pentru raport, care sunt raw-uri - si e singura care se poate testa fara sa
// deschizi o fereastra.
//
// Clasa nu atinge nimic si nu arunca la constructie: intreaba discul abia cand e
// intrebata. Asa un folder ales gresit devine o stare a interfetei, nu o exceptie.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FlatCheck.Wpf
{
    /// <summary>De ce nu se poate porni pe folderul asta. <see cref="None"/> = se poate.</summary>
    internal enum SessionProblem
    {
        None,
        NoFolder,        // calea nu exista deloc
        NoProcessDir,    // nu s-a rulat niciun script de preprocesare
        NoFlatMaster,    // process/ exista, dar masterul de flat nu
    }

    internal sealed class SirilSession
    {
        /// <summary>Numele pe care il da scriptul OSC_Preprocessing.ssf masterului de flat.</summary>
        internal const string FlatMasterName = "pp_flat_stacked.fit";

        /// <summary>Scriptul Siril care produce continutul lui process/.</summary>
        internal const string PreprocessingScript = "OSC_Preprocessing.ssf";

        /// <summary>Tiparul candidatilor pentru testul de rotatie 180.</summary>
        internal const string RatioPattern = "pp_flat*.fit";

        /// <summary>Raportul de structura, salvat langa datele sesiunii.</summary>
        internal const string ResultFileName = "Rezultat testare.txt";

        internal string Root { get; }

        internal SirilSession(string root)
        {
            Root = root ?? throw new ArgumentNullException(nameof(root));
        }

        internal string ProcessDir => Path.Combine(Root, "process");
        internal string FlatsDir => Path.Combine(Root, "flats");

        /// <summary>Intrarea modului 1, prin conventie. Vezi cerinta 4.</summary>
        internal string FlatMasterPath => Path.Combine(ProcessDir, FlatMasterName);

        /// <summary>Unde se scrie raportul de structura: in folderul dat, nu in process/.</summary>
        internal string ResultTextPath => Path.Combine(Root, ResultFileName);

        /// <summary>Harta reziduala, langa raport si cu acelasi nume ca la consola.</summary>
        internal string ResidualPngPath => Path.Combine(Root, "flatcheck.png");

        /// <summary>
        /// Prima verificare, inainte de orice calcul. Ordinea conteaza: un folder gresit
        /// ales si un folder corect pe care nu s-a rulat inca scriptul sunt doua greseli
        /// diferite, si merita doua mesaje diferite.
        /// </summary>
        internal SessionProblem Check()
        {
            if (!Directory.Exists(Root)) return SessionProblem.NoFolder;
            if (!Directory.Exists(ProcessDir)) return SessionProblem.NoProcessDir;
            if (!File.Exists(FlatMasterPath)) return SessionProblem.NoFlatMaster;
            return SessionProblem.None;
        }

        /// <summary>
        /// Cadrele calibrate pentru testul end-to-end. Acelasi tipar implicit ca la
        /// consola, deci si aceeasi excludere a cadrelor registrate (r_pp_light_*) - vezi
        /// SeriesFinder.DefaultPattern pentru de ce conteaza.
        /// </summary>
        internal IReadOnlyList<string> LightSeries()
            => Directory.Exists(ProcessDir)
                ? SeriesFinder.Find(ProcessDir, SeriesFinder.DefaultPattern)
                : Array.Empty<string>();

        /// <summary>
        /// Candidatii pentru --ratio: masterele de flat din process/. Aceeasi potrivire
        /// in proces ca la consola, deci "pp_flat*.fit" chiar inseamna ".fit", nu si
        /// ".fits" prin mostenirea 8.3 din Win32.
        /// </summary>
        internal IReadOnlyList<string> RatioCandidates()
            => Directory.Exists(ProcessDir)
                ? SeriesFinder.Find(ProcessDir, RatioPattern)
                : Array.Empty<string>();

        /// <summary>
        /// Un fisier e acceptat de tabul Ratio doar daca e un master de flat. Verificarea
        /// e pe NUME, nu pe cale, ca sa mearga si pentru un fisier ales de mana din alt
        /// folder - cerinta 6 spune sa explicam, nu sa refuzam tacut.
        /// </summary>
        internal static bool IsFlatMasterName(string path)
            => !string.IsNullOrEmpty(path)
               && SeriesFinder.GlobMatch(Path.GetFileName(path), RatioPattern);

        /// <summary>
        /// Flat-urile brute pentru --expo: doar raw de aparat.
        ///
        /// Modul are nevoie de mozaicul CFA intact si de scala ADU originala, iar un raw
        /// le duce pe amandoua, exact, in fisier. Extensiile vin din RawReader, nu dintr-o
        /// lista scrisa aici, ca sa nu apara un aparat suportat de cititor si invizibil
        /// in lista.
        /// </summary>
        internal IReadOnlyList<string> RawFlats()
        {
            if (!Directory.Exists(FlatsDir)) return Array.Empty<string>();

            return Directory.EnumerateFiles(FlatsDir)
                .Where(RawReader.IsRawPath)
                .OrderBy(Path.GetFileName, StringComparer.Ordinal)
                .ToList();
        }
    }
}
