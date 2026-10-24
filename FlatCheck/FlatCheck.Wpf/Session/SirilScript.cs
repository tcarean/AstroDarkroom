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

// SirilScript.cs - what can be read from an .ssf without running it.
//
// Two things, both pure: the commands (a non-empty line that does not start with #), so the
// window can say "command 7 of 20" and show the whole command when Siril prints only the verb;
// and the input folders, so the button can refuse IN ADVANCE a run that would die at line
// three with "cd biases" on a folder without biases/.
//
// The folders are deduced from the "cd <name>" commands with a plain name - no "..", no "/" -
// that is exactly how the preprocessing scripts are written: "cd biases", "cd flats",
// "cd lights". "cd ../process" is the way out, not an input, and does not count. It is a rule
// of form, not an understanding of the script, and it is enough for every script in the Siril
// repository; a script that enters folders otherwise is simply not checked in advance, and
// Siril says itself what is missing.

using System;
using System.Collections.Generic;
using System.Linq;

namespace FlatCheck.Wpf
{
    /// <summary>Reads a Siril script's commands and the session folders it enters, without
    /// running it.</summary>
    internal static class SirilScript
    {
        /// <summary>The lines that are commands, in the file's order, without comments and
        /// blanks.</summary>
        /// <param name="scriptText">The .ssf's text; null allowed.</param>
        /// <returns>The trimmed command lines; empty for null.</returns>
        internal static IReadOnlyList<string> Commands(string scriptText)
        {
            if (scriptText == null) return Array.Empty<string>();
            return scriptText.Split('\n')
                .Select(l => l.Trim())
                .Where(l => l.Length > 0 && l[0] != '#')
                .ToList();
        }

        /// <summary>
        /// The subfolders the script enters directly from the session folder, in the order of
        /// first appearance. See the note in the header for what "directly" means.
        /// </summary>
        /// <param name="scriptText">The .ssf's text; null allowed.</param>
        /// <returns>The folder names as written, each once (ignoring case); empty for
        /// null.</returns>
        internal static IReadOnlyList<string> RequiredFolders(string scriptText)
        {
            var found = new List<string>();
            foreach (string cmd in Commands(scriptText))
            {
                string[] parts = cmd.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 2 || parts[0] != "cd") continue;

                string target = parts[1];
                if (target.Contains('/') || target.Contains('\\') || target.Contains("..")) continue;
                if (!found.Contains(target, StringComparer.OrdinalIgnoreCase)) found.Add(target);
            }
            return found;
        }
    }
}
