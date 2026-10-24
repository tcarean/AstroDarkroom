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
// primeste fiecare sesiune temporara si unde sta in TEMP.
//
// CE SE COPIAZA decide scriptul, nu o lista fixa: folderele in care scriptul intra cu
// "cd" (SirilScript.RequiredFolders - biases/, darks/, ce-o fi) se copiaza intregi,
// lights/ primeste cel mult MaxLights cadre (primele dupa nume: destule ca scriptul sa
// ajunga la capat, nu o suta de lights copiate de patru ori), iar flats/ primeste felia
// partii. O lista fixa lasa scriptul OSC_Preprocessing.ssf sa moara la "cd darks" pe
// partea 1, dupa ce s-au copiat gigaocteti - exact ce verificarea de dinainte de
// pornire exista sa previna (ADR 0035).
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

        internal const string FlatsFolder = "flats";
        internal const string LightsFolder = "lights";

        readonly SirilSession _session;

        /// <param name="session">The session whose flats (and lights, if the script needs them)
        /// the parts are cut from; never null.</param>
        /// <param name="parts">How many parts: one of <see cref="Choices"/>.</param>
        /// <param name="tempRoot">The folder the part sessions are built under; never null.</param>
        /// <param name="requiredFolders">Folderele in care intra scriptul (SirilScript.RequiredFolders); flats/ se copiaza oricum.</param>
        internal SplitPlan(SirilSession session, int parts, string tempRoot, IEnumerable<string> requiredFolders)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            if (!Choices.Contains(parts)) throw new ArgumentOutOfRangeException(nameof(parts));
            Parts = parts;
            TempRoot = tempRoot ?? throw new ArgumentNullException(nameof(tempRoot));
            Frames = session.FlatFrames();
            // process/ si masters/ sunt ce scrie Siril, nu intrari: un script care intra in ele
            // cu "cd process" nu primeste cei 17 GB ai originalului, si nici masterul lui
            WholeFolders = (requiredFolders ?? Array.Empty<string>())
                .Where(f => !Is(f, FlatsFolder) && !Is(f, LightsFolder)
                            && !SirilSession.OutputFolders.Any(o => Is(f, o)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            CopiesLights = (requiredFolders ?? Array.Empty<string>()).Any(f => Is(f, LightsFolder));
        }

        static bool Is(string folder, string name) => string.Equals(folder, name, StringComparison.OrdinalIgnoreCase);

        internal int Parts { get; }
        internal string TempRoot { get; }

        /// <summary>Folderele copiate intregi in fiecare parte: cele in care scriptul intra, in afara de flats/ si lights/.</summary>
        internal IReadOnlyList<string> WholeFolders { get; }

        /// <summary>Adevarat cand scriptul intra in lights/, deci fiecare parte primeste cel mult MaxLights cadre de acolo.</summary>
        internal bool CopiesLights { get; }

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

        /// <summary>Cadrele din lights/ care merg in fiecare parte: primele MaxLights cadre (raw sau FITS) dupa nume.</summary>
        internal IReadOnlyList<string> LightsToCopy()
            => CopiesLights
                ? AllFiles(Path.Combine(_session.Root, LightsFolder)).Where(IsFrame).Take(MaxLights).ToList()
                : Array.Empty<string>();

        static bool IsFrame(string path) => RawReader.IsRawPath(path) || FitsReader.IsFitsPath(path);

        /// <summary>Tot ce se copiaza pentru o parte, ca perechi (sursa, destinatie), in ordinea copierii.</summary>
        internal IReadOnlyList<(string From, string To)> CopyList(int part)
        {
            string root = PartRoot(part);
            var list = new List<(string, string)>();
            foreach (string folder in WholeFolders)
                foreach (string f in AllFiles(Path.Combine(_session.Root, folder)))
                    list.Add((f, Path.Combine(root, folder, Path.GetFileName(f))));
            foreach (string f in LightsToCopy()) list.Add((f, Path.Combine(root, LightsFolder, Path.GetFileName(f))));
            foreach (string f in PartFrames(part)) list.Add((f, Path.Combine(root, FlatsFolder, Path.GetFileName(f))));
            return list;
        }

        /// <summary>
        /// Cati octeti se copiaza in total, pe toate partile: folderele intregi si lights-urile
        /// de Parts ori, fiecare flat o data. Fiecare folder e enumerat o singura data - se
        /// cheama la fiecare schimbare de chip sau de script, pe firul de interfata.
        /// </summary>
        internal long CopiedBytes()
        {
            long perPart = 0;
            foreach (string folder in WholeFolders)
                foreach (string f in AllFiles(Path.Combine(_session.Root, folder)))
                    perPart += SizeOf(f);
            foreach (string f in LightsToCopy()) perPart += SizeOf(f);
            long flats = 0;
            foreach (string f in Frames) flats += SizeOf(f);
            return perPart * Parts + flats;
        }

        static long SizeOf(string path)
        {
            try { return new FileInfo(path).Length; }
            catch (IOException) { return 0; }
            catch (UnauthorizedAccessException) { return 0; }
        }

        /// <summary>Cate cadre (raw sau FITS) are fiecare parte in folderele copiate intregi; un readme sau un .xmp nu e cadru.</summary>
        internal int WholeFrameCount() => WholeFolders.Sum(f => AllFiles(Path.Combine(_session.Root, f)).Count(IsFrame));

        /// <summary>Sesiunea temporara a partii, prin aceleasi reguli ca a originalului (unde e masterul).</summary>
        internal SirilSession PartSession(int part) => new(PartRoot(part));

        static IReadOnlyList<string> AllFiles(string dir)
            => Directory.Exists(dir)
                ? Directory.EnumerateFiles(dir).OrderBy(Path.GetFileName, StringComparer.Ordinal).ToList()
                : Array.Empty<string>();

        /// <summary>
        /// Sterge radacina temporara, cu tot ce e in ea. Un raw copiat de pe card e adesea
        /// read-only si File.Copy pastreaza atributul, deci intai se ridica pe tot ce e
        /// acolo. O eroare de disc nu e o eroare a rularii: intoarce false.
        /// </summary>
        internal bool Delete()
        {
            try
            {
                if (!Directory.Exists(TempRoot)) return true;
                foreach (string f in Directory.EnumerateFiles(TempRoot, "*", SearchOption.AllDirectories))
                    File.SetAttributes(f, FileAttributes.Normal);
                Directory.Delete(TempRoot, recursive: true);
                return true;
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }
    }
}
