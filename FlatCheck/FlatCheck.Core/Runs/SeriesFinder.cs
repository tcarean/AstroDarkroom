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

// SeriesFinder.cs - which files go into a series run.
//
// It stands apart from LightSeriesRun because the list is also asked for BEFORE the run: the
// window wants to show how many frames it found and to say "none" as a state of the interface,
// not as an exception caught in the middle of a computation.

using System.Collections.Generic;
using System.IO;

namespace FlatCheck
{
    /// <summary>
    /// Lists the calibrated lights of a folder for the series mode of <c>--light</c>, by a
    /// glob of its own rather than the operating system's.
    /// </summary>
    internal static class SeriesFinder
    {
        /// <summary>
        /// The default pattern: Siril's convention for frames CALIBRATED but NOT REGISTERED.
        ///
        /// "r_pp_light_*" does not match, because the pattern is anchored at the start, and
        /// that is right, not accidental: registration transforms the frame geometrically, so
        /// it moves the flat error - which is fixed on the sensor - relative to the centre
        /// around which the radial model is fitted. Registered frames would corrupt the
        /// measurement silently.
        /// </summary>
        internal const string DefaultPattern = "pp_light_*.fit";

        /// <summary>
        /// The files of the folder that match the pattern, sorted.
        ///
        /// A full enumeration + matching in process, NOT Directory.GetFiles(dir, pattern):
        /// Win32 matching also catches longer extensions, from the 8.3 inheritance, so
        /// "pp_light_*.fit" would swallow ".fits" too. Here the pattern means the same thing on
        /// any system, and the same thing as in flatcheck.py.
        ///
        /// The sort is ordinal on the file name, so it does not depend on the machine's culture:
        /// the console runs without culture data, the window with it, and both list the series
        /// in the same order. The verdict does not depend on the order, only the table's rows
        /// do (ADR 0021).
        /// </summary>
        /// <param name="dir">The folder to search, not its subfolders; must exist.</param>
        /// <param name="pattern">The file-name pattern, with <c>*</c> and <c>?</c>
        /// (<see cref="GlobMatch"/>).</param>
        /// <returns>The full paths that match, ordinal by file name; empty when none
        /// does.</returns>
        internal static List<string> Find(string dir, string pattern)
        {
            var hits = new List<string>();
            foreach (string p in Directory.EnumerateFiles(dir))
                if (GlobMatch(Path.GetFileName(p), pattern)) hits.Add(p);
            hits.Sort((a, b) => string.CompareOrdinal(Path.GetFileName(a), Path.GetFileName(b)));
            return hits;
        }

        /// <summary>
        /// Pattern matching with '*' and '?', case-insensitive.
        /// Character classes ('[abc]') are NOT supported - they are taken literally, in both
        /// implementations, so that no silent divergence appears.
        /// </summary>
        /// <param name="name">The file name, without its folder.</param>
        /// <param name="pattern">The pattern; <c>*</c> matches any run, <c>?</c> one
        /// character.</param>
        /// <returns>True when the whole name matches the whole pattern.</returns>
        internal static bool GlobMatch(string name, string pattern)
        {
            int n = 0, p = 0, starP = -1, starN = 0;
            while (n < name.Length)
            {
                if (p < pattern.Length && (pattern[p] == '?' || Eq(pattern[p], name[n])))
                { p++; n++; }
                else if (p < pattern.Length && pattern[p] == '*')
                { starP = p++; starN = n; }
                else if (starP >= 0)
                { p = starP + 1; n = ++starN; }
                else return false;
            }
            while (p < pattern.Length && pattern[p] == '*') p++;
            return p == pattern.Length;
        }

        /// <summary>Compares two characters ignoring case, culture-invariantly.</summary>
        /// <param name="a">The pattern's character.</param>
        /// <param name="b">The name's character.</param>
        /// <returns>True when they are equal ignoring case.</returns>
        static bool Eq(char a, char b) => char.ToLowerInvariant(a) == char.ToLowerInvariant(b);
    }
}
