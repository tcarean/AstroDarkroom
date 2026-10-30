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

// The only file in LensProfiles/ that looks at the machine: where the Adobe profiles are (Camera
// Raw and the free DNG Converter put them in the CameraRaw folders; Lightroom Classic and
// Lightroom keep their own copy inside their program folder, ADR 0066; a file present in two
// folders is read once, from the first) and where the copy of the lensfun database is (the
// lensfun/ folder beside the exe, shipped with the tool). The reading itself is in LcpReader /
// LensfunReader, pure, so that the tests run on XML they write.
//
// The Adobe profiles are not redistributed: they are read from where Adobe put them. Their
// absence is not an error, it is an empty half of the list, and ProfileSources says why.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NLog;

namespace FlatCheck
{
    /// <summary>What was found and where, for the report and for the window's panel.</summary>
    internal sealed class ProfileSources
    {
        internal readonly IReadOnlyList<string> AdobeDirectories;
        internal readonly int AdobeFiles;
        internal readonly int AdobeFilesDuplicate;
        internal readonly string LensfunDirectory;
        internal readonly bool LensfunPresent;
        internal readonly string LensfunVersion;
        internal readonly LensCatalog Catalog;
        internal readonly LensfunData Lensfun;

        /// <summary>The result of a search for profiles.</summary>
        /// <param name="adobeDirs">The folders searched for .lcp files.</param>
        /// <param name="adobeFiles">How many .lcp files were kept, each relative name once.</param>
        /// <param name="adobeDuplicate">How many .lcp files were skipped because an earlier folder
        /// held the same relative name.</param>
        /// <param name="lensfunDir">The lensfun folder looked at.</param>
        /// <param name="lensfunPresent">Whether it holds any .xml file.</param>
        /// <param name="lensfunVersion">The text of its version file; null when there is
        /// none.</param>
        /// <param name="catalog">Every lens found, from both sources.</param>
        /// <param name="lensfun">The lensfun database read, cameras included.</param>
        internal ProfileSources(IReadOnlyList<string> adobeDirs, int adobeFiles, int adobeDuplicate,
                                string lensfunDir, bool lensfunPresent, string lensfunVersion,
                                LensCatalog catalog, LensfunData lensfun)
        {
            AdobeDirectories = adobeDirs;
            AdobeFiles = adobeFiles;
            AdobeFilesDuplicate = adobeDuplicate;
            LensfunDirectory = lensfunDir;
            LensfunPresent = lensfunPresent;
            LensfunVersion = lensfunVersion;
            Catalog = catalog;
            Lensfun = lensfun;
        }

        internal int AdobeLenses => Catalog.Lenses.Count(l => l.Source == ProfileSource.Adobe);
        internal int LensfunLenses => Catalog.Lenses.Count(l => l.Source == ProfileSource.Lensfun);
    }

    /// <summary>Finds the Adobe profiles and the lensfun database on this machine.</summary>
    internal static class ProfileLocator
    {
        internal const string LensfunFolderName = "lensfun";
        internal const string LensfunVersionFile = "LENSFUN-VERSION.txt";

        /// <summary>
        /// The folders where Adobe puts its profiles on Windows, in the order they are searched:
        /// the Camera Raw folder (Camera Raw and the DNG Converter write it), the user's own, then
        /// the profile folder of every Adobe application under Program Files that carries one
        /// (Lightroom Classic, Lightroom).
        /// </summary>
        /// <returns>The two Camera Raw folders, whether they exist or not, then the application
        /// folders that exist.</returns>
        internal static IReadOnlyList<string> DefaultAdobeDirectories()
        {
            var dirs = new List<string>();
            string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            if (!string.IsNullOrEmpty(programData))
                dirs.Add(Path.Combine(programData, "Adobe", "CameraRaw", "LensProfiles", "1.0"));
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (!string.IsNullOrEmpty(appData))
                dirs.Add(Path.Combine(appData, "Adobe", "CameraRaw", "LensProfiles"));
            // Lightroom is 64-bit only, so Program Files (x86) is not searched (ADR 0066); in the
            // win-x64 build ProgramFiles is the 64-bit folder (a 32-bit process would get the x86 one).
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if (!string.IsNullOrEmpty(programFiles))
                dirs.AddRange(AdobeAppDirectories(Path.Combine(programFiles, "Adobe")));
            return dirs;
        }

        /// <summary>
        /// The profile folder of every Adobe application installed under
        /// <paramref name="adobeRoot"/> that has one: Lightroom Classic and Lightroom keep their
        /// own copy in <c>Resources\LensProfiles\1.0</c> inside their program folder (measured
        /// 2026-10-26, ADR 0066).
        /// </summary>
        /// <param name="adobeRoot">The folder holding the Adobe applications, normally
        /// <c>C:\Program Files\Adobe</c>; null, missing or unreadable gives none.</param>
        /// <returns>The profile folders that exist, in ordinal order of the application folder;
        /// empty when there are none.</returns>
        internal static IReadOnlyList<string> AdobeAppDirectories(string adobeRoot)
        {
            if (string.IsNullOrEmpty(adobeRoot) || !Directory.Exists(adobeRoot)) return Array.Empty<string>();
            try
            {
                return Directory.EnumerateDirectories(adobeRoot)
                    .OrderBy(d => d, StringComparer.Ordinal)
                    .Select(d => Path.Combine(d, "Resources", "LensProfiles", "1.0"))
                    .Where(Directory.Exists)
                    .ToList();
            }
            catch (Exception e)
            {
                // A folder we may not list is a folder without profiles; the log says which.
                Log.Warn(e, "the Adobe applications under " + adobeRoot + " could not be listed; their profiles are left out");
                return Array.Empty<string>();
            }
        }

        /// <summary>The diagnostic log: what was searched and found, and what could not be
        /// read (R-110).</summary>
        static readonly Logger Log = LogManager.GetLogger(nameof(ProfileLocator));

        /// <summary>The lensfun/ folder beside the running exe.</summary>
        /// <returns>Its path, whether it exists or not.</returns>
        internal static string DefaultLensfunDirectory()
            => Path.Combine(AppContext.BaseDirectory, LensfunFolderName);

        /// <summary>Searches the default places.</summary>
        /// <returns>What was found.</returns>
        internal static ProfileSources Locate()
            => Locate(DefaultAdobeDirectories(), DefaultLensfunDirectory());

        /// <summary>
        /// Searches the given places in order: every .lcp under the Adobe folders, by file name
        /// only (the file is read when its lens is asked for; <see cref="LensCatalog"/> logs
        /// one it cannot read), and the lensfun folder's XML. A missing folder is skipped, and
        /// a file whose path relative to its folder was already found under an earlier folder is
        /// skipped and counted as a duplicate.
        /// </summary>
        /// <param name="adobeDirs">The folders to search for .lcp files, recursively, first
        /// wins.</param>
        /// <param name="lensfunDir">The folder of the lensfun XML files.</param>
        /// <returns>What was found.</returns>
        internal static ProfileSources Locate(IReadOnlyList<string> adobeDirs, string lensfunDir)
        {
            var clock = Stopwatch.StartNew();
            // Adobe: only the file names - 3531 on the author's machine, 36 s to parse them all.
            // FromName reads any name (a stub, never null). Lightroom's folder repeats the DNG
            // Converter's files under the same relative names (ADR 0066): the catalogue would
            // list and parse each twice otherwise.
            var profiles = new List<LensProfile>();
            var stubs = new List<AdobeProfileFile>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int files = 0, duplicates = 0;
            foreach (string dir in adobeDirs)
            {
                if (!Directory.Exists(dir)) continue;
                List<string> found;
                // A subfolder the user may not list is skipped by the enumeration itself
                // (IgnoreInaccessible), so the rest of the root is still read; AttributesToSkip
                // is cleared because the default would leave out hidden and system files, which
                // the SearchOption overload never did. IgnoreInaccessible would also skip the
                // root itself in silence (measured 2026-10-30: files=0 and no line), so the root
                // is opened once without it first: a root the user may not list, one that
                // vanished or an I/O error throws there, the whole root is left out with a
                // line, and the next folder is searched.
                var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = 0 };
                try
                {
                    Directory.EnumerateFileSystemEntries(dir).Any();
                    found = Directory.EnumerateFiles(dir, "*.lcp", options).OrderBy(f => f, StringComparer.Ordinal).ToList();
                }
                catch (Exception e)
                {
                    Log.Warn(e, "the Adobe folder " + dir + " could not be listed; its profiles are left out");
                    continue;
                }
                foreach (string f in found)
                {
                    if (!seen.Add(Path.GetRelativePath(dir, f))) { duplicates++; continue; }
                    files++;
                    stubs.Add(AdobeProfileFile.FromName(f));
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
                catch (Exception e)
                {
                    Log.Warn(e, "the lensfun version file " + vf + " could not be read");
                    version = null;
                }
            }
            var sources = new ProfileSources(adobeDirs, files, duplicates, lensfunDir, lfPresent, version, new LensCatalog(profiles, stubs), lf);
            if (Log.IsInfoEnabled)
                Log.Info("profiles: adobe folders " + LogText.List(adobeDirs.Select(d => d + (Directory.Exists(d) ? "" : " (missing)")))
                         + " files=" + LogText.Int(files) + " duplicate=" + LogText.Int(duplicates)
                         + "; lensfun " + (lfPresent ? "present" : "absent") + " version=" + LogText.Str(version?.Split('\n')[0].Trim())
                         + " at " + lensfunDir + "; lenses adobe=" + LogText.Int(sources.AdobeLenses) + " lensfun=" + LogText.Int(sources.LensfunLenses)
                         + " in " + LogText.Ms(clock.ElapsedMilliseconds));
            return sources;
        }
    }
}
