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

// Singurul fisier din LensProfiles/ care se uita pe masina: unde sunt profilele Adobe (le
// instaleaza Lightroom, Camera Raw si DNG Converter, care e gratuit) si unde e copia bazei
// lensfun (folderul lensfun/ de langa exe, livrat cu unealta). Cititul propriu-zis e in
// LcpReader / LensfunReader, pure, ca testele sa mearga pe XML scris de ele.
//
// Profilele Adobe nu se redistribuie: se citesc de unde le-a pus Adobe. Lipsa lor nu e o
// eroare, e o jumatate goala a listei, si ProfileSources spune de ce.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FlatCheck
{
    /// <summary>Ce s-a gasit si unde, pentru raport si pentru panoul din fereastra.</summary>
    internal sealed class ProfileSources
    {
        internal readonly IReadOnlyList<string> AdobeDirectories;
        internal readonly int AdobeFiles;
        internal readonly int AdobeFilesSkipped;
        internal readonly string LensfunDirectory;
        internal readonly bool LensfunPresent;
        internal readonly string LensfunVersion;
        internal readonly LensCatalog Catalog;
        internal readonly LensfunData Lensfun;

        internal ProfileSources(IReadOnlyList<string> adobeDirs, int adobeFiles, int adobeSkipped,
                                string lensfunDir, bool lensfunPresent, string lensfunVersion,
                                LensCatalog catalog, LensfunData lensfun)
        {
            AdobeDirectories = adobeDirs;
            AdobeFiles = adobeFiles;
            AdobeFilesSkipped = adobeSkipped;
            LensfunDirectory = lensfunDir;
            LensfunPresent = lensfunPresent;
            LensfunVersion = lensfunVersion;
            Catalog = catalog;
            Lensfun = lensfun;
        }

        internal int AdobeLenses => Catalog.Lenses.Count(l => l.Source == ProfileSource.Adobe);
        internal int LensfunLenses => Catalog.Lenses.Count(l => l.Source == ProfileSource.Lensfun);
    }

    internal static class ProfileLocator
    {
        internal const string LensfunFolderName = "lensfun";
        internal const string LensfunVersionFile = "LENSFUN-VERSION.txt";

        /// <summary>Folderele in care Adobe isi pune profilele pe Windows: cele instalate si cele ale utilizatorului.</summary>
        internal static IReadOnlyList<string> DefaultAdobeDirectories()
        {
            var dirs = new List<string>();
            string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            if (!string.IsNullOrEmpty(programData))
                dirs.Add(Path.Combine(programData, "Adobe", "CameraRaw", "LensProfiles", "1.0"));
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (!string.IsNullOrEmpty(appData))
                dirs.Add(Path.Combine(appData, "Adobe", "CameraRaw", "LensProfiles"));
            return dirs;
        }

        /// <summary>Folderul lensfun/ de langa exe-ul care ruleaza.</summary>
        internal static string DefaultLensfunDirectory()
            => Path.Combine(AppContext.BaseDirectory, LensfunFolderName);

        internal static ProfileSources Locate()
            => Locate(DefaultAdobeDirectories(), DefaultLensfunDirectory());

        internal static ProfileSources Locate(IReadOnlyList<string> adobeDirs, string lensfunDir)
        {
            // Adobe: doar numele fisierelor - 3531 pe masina autorului, 36 s de parsat pe
            // toate. Un nume care nu are forma asteptata se parseaza pe loc, ca sa nu lipseasca.
            var profiles = new List<LensProfile>();
            var stubs = new List<AdobeProfileFile>();
            int files = 0, skipped = 0;
            foreach (string dir in adobeDirs)
            {
                if (!Directory.Exists(dir)) continue;
                foreach (string f in Directory.EnumerateFiles(dir, "*.lcp", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
                {
                    files++;
                    AdobeProfileFile stub = AdobeProfileFile.FromName(f);
                    if (stub != null) { stubs.Add(stub); continue; }
                    try { profiles.AddRange(LcpReader.Read(f)); }
                    catch (Exception) { skipped++; }
                }
            }
            bool lfPresent = Directory.Exists(lensfunDir) && Directory.EnumerateFiles(lensfunDir, "*.xml").Any();
            LensfunData lf = lfPresent ? LensfunReader.ReadDirectory(lensfunDir) : LensfunData.Empty;
            profiles.AddRange(lf.Profiles);
            string version = null;
            string vf = Path.Combine(lensfunDir, LensfunVersionFile);
            if (File.Exists(vf))
            {
                try { version = File.ReadAllText(vf).Trim(); }
                catch (Exception) { version = null; }
            }
            return new ProfileSources(adobeDirs, files, skipped, lensfunDir, lfPresent, version, new LensCatalog(profiles, stubs), lf);
        }
    }
}
