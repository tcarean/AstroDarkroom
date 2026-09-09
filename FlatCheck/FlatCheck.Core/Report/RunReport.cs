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

// RunReport.cs - textul care incadreaza raportul oricarui mod: antetul (versiune +
// copyright), linia care anunta harta salvata, si formularile care insotesc harta.
//
// Stau aici, nu in FlatCheckCli, fiindca nu mai sunt ale consolei. Cerinta pentru
// aplicatia WPF e ca fisierul "Rezultat testare.txt" sa contina EXACT ce ar fi scris
// consola - antet inclus, ca un raport copiat undeva sa spuna singur ce versiune si a
// cui unealta l-a produs. Doua implementari ale acelorasi doua linii ar fi divergat la
// prima reformulare, si divergenta s-ar fi vazut abia intr-un raport lipit pe un forum.
//
// Eticheta de scara si legenda hartii n-au apelant in consola azi - le foloseste numai
// fereastra. Stau totusi aici, fiindca regula e ca nicio propozitie pentru utilizator
// nu se scrie intr-un ViewModel: daca maine consola primeste un --scala, ia acelasi
// text, nu unul rescris. Ambele sunt functii pure, nu scriu in TextWriter, exact ca
// StructureReport.Advice - o valoare pe care consola o formateaza in coloane si
// fereastra o pune intr-o eticheta.

using System.IO;

namespace FlatCheck
{
    internal static class RunReport
    {
        /// <summary>
        /// Primele doua linii ale oricarei rulari: versiunea si notita de copyright.
        ///
        /// Banner-ul pe prima linie inseamna ca un raport copiat spune singur ce
        /// versiune l-a produs - de aceea apare si la --help, si la rulari esuate.
        /// Copyright-ul pe a doua e cerut de GPLv3 (vezi LICENSE).
        /// </summary>
        internal static void WriteBanner(TextWriter w)
        {
            w.WriteLine(VersionInfo.Banner);
            w.WriteLine(VersionInfo.Copyright);
        }

        /// <summary>
        /// Anuntul hartii reziduale. Calea o stie apelantul - el a ales unde scrie -
        /// deci nu se poate genera in interiorul modului; dar formularea e aceeasi
        /// peste tot, si de asta sta aici.
        /// </summary>
        internal static void WriteMapSaved(string path, TextWriter w)
            => w.WriteLine($"\n    -> harta salvata: {path}");

        /// <summary>
        /// Eticheta unei scari de harta: "auto", sau plafonul cand scara e fixa.
        /// <paramref name="capPct"/> null inseamna scara automata.
        /// </summary>
        internal static string MapScaleLabel(double? capPct)
            => capPct is null ? "auto" : $"+/-{capPct.Value.ToString("0.#")} %";

        /// <summary>
        /// Legenda de sub harta: ce inseamna capetele culorilor, in procente fata de
        /// model. Se da intervalul CHIAR AFISAT, fiindca pe scara automata capetele
        /// difera de la o imagine la alta - si tocmai asta era de nespus pe ecran.
        /// </summary>
        internal static string MapLegend(PngWriter.MapRange r)
        {
            string lo = ((r.Lo - PngWriter.Mid) * 100).ToString("+0.0;-0.0");
            string hi = ((r.Hi - PngWriter.Mid) * 100).ToString("+0.0;-0.0");
            return $"albastru = {lo} %   alb = 0 (exact pe model)   rosu = {hi} %";
        }
    }
}
