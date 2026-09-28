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

// SplitPlan.cs - ce inseamna, pe disc, "impart sesiunea in N parti".
//
// Regulile de forma ale tabului Split (ADR 0054), fara nicio rulare si fara fereastra,
// ca sa se poata testa singure: care fisiere din flats/ sunt cadre (raw sau FITS, in
// ordinea numelor; un readme.txt nu e cadru), daca numarul lor se imparte exact, ce
// primeste fiecare sesiune temporara - toate fisierele din biases/, cel mult MaxLights
// din lights/ (primele dupa nume: destule ca scriptul sa ajunga la capat, nu o suta de
// lights copiate de patru ori), si felia ei de flats/ - si unde sta in TEMP.
//
// Din sesiunea originala se citeste doar: aici nu exista nicio mutare si nicio stergere,
// iar Delete() sterge doar radacina temporara pe care a construit-o clasa asta.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FlatCheck.Wpf
{
    internal sealed class SplitPlan
    {
        /// <summary>Cate fisiere din lights/ merg in fiecare parte, cel mult.</summary>
        internal const int MaxLights = 2;

        /// <summary>In cate parti se poate imparti: 2 (0/180) sau 4 (0/90/180/270).</summary>
        internal static readonly IReadOnlyList<int> Choices = new[] { 2, 4 };

        internal const string PartPrefix = "parte-";

        readonly SirilSession _session;

        internal SplitPlan(SirilSession session, int parts, string tempRoot)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            if (!Choices.Contains(parts)) throw new ArgumentOutOfRangeException(nameof(parts));
            Parts = parts;
            TempRoot = tempRoot ?? throw new ArgumentNullException(nameof(tempRoot));
            Frames = session.FlatFrames();
        }

        internal int Parts { get; }
        internal string TempRoot { get; }

        /// <summary>Cadrele din flats/, in ordinea numelor: raw sau FITS, nimic altceva.</summary>
        internal IReadOnlyList<string> Frames { get; }

        internal bool Divides => Frames.Count > 0 && Frames.Count % Parts == 0;

        internal int FramesPerPart => Divides ? Frames.Count / Parts : 0;

        /// <summary>Radacina unei rulari: %TEMP%\FlatCheck\split-yyyyMMdd-HHmmss.</summary>
        internal static string NewTempRoot(DateTime when)
            => Path.Combine(Path.GetTempPath(), "FlatCheck", "split-" + when.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture));

        /// <summary>Folderul partii <paramref name="part"/> (de la 1).</summary>
        internal string PartRoot(int part) => Path.Combine(TempRoot, PartPrefix + part);

        /// <summary>Felia de cadre a partii <paramref name="part"/> (de la 1): blocuri consecutive.</summary>
        internal IReadOnlyList<string> PartFrames(int part)
        {
            if (!Divides) return Array.Empty<string>();
            int n = FramesPerPart;
            return Frames.Skip((part - 1) * n).Take(n).ToList();
        }

        /// <summary>Fisierele din lights/ care merg in fiecare parte: primele MaxLights dupa nume.</summary>
        internal IReadOnlyList<string> LightsToCopy()
            => AllFiles(Path.Combine(_session.Root, "lights")).Take(MaxLights).ToList();

        internal IReadOnlyList<string> BiasesToCopy()
            => AllFiles(Path.Combine(_session.Root, "biases"));

        /// <summary>Tot ce se copiaza pentru o parte, ca perechi (sursa, destinatie), in ordinea copierii.</summary>
        internal IReadOnlyList<(string From, string To)> CopyList(int part)
        {
            string root = PartRoot(part);
            var list = new List<(string, string)>();
            foreach (string f in BiasesToCopy()) list.Add((f, Path.Combine(root, "biases", Path.GetFileName(f))));
            foreach (string f in LightsToCopy()) list.Add((f, Path.Combine(root, "lights", Path.GetFileName(f))));
            foreach (string f in PartFrames(part)) list.Add((f, Path.Combine(root, "flats", Path.GetFileName(f))));
            return list;
        }

        /// <summary>Sesiunea temporara a partii, prin aceleasi reguli ca a originalului (unde e masterul).</summary>
        internal SirilSession PartSession(int part) => new(PartRoot(part));

        static IReadOnlyList<string> AllFiles(string dir)
            => Directory.Exists(dir)
                ? Directory.EnumerateFiles(dir).OrderBy(Path.GetFileName, StringComparer.Ordinal).ToList()
                : Array.Empty<string>();

        /// <summary>Sterge radacina temporara, cu tot ce e in ea. O eroare de disc nu e o eroare a rularii: intoarce false.</summary>
        internal bool Delete()
        {
            try
            {
                if (Directory.Exists(TempRoot)) Directory.Delete(TempRoot, recursive: true);
                return true;
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }
    }
}
