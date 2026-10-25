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

// RunReport.cs - the text that frames the report: the banner (version + copyright) and the
// warranty notice.
//
// It lives here, not in the console, because it is not the console's. The requirement for the
// WPF application is that the "Raport.txt" file it saves holds EXACTLY what the console would
// have written - banner included, so that a report copied somewhere says by itself which version
// and whose tool produced it. Two implementations of the same two lines would have diverged at
// the first rewording.

using System.IO;

namespace AstroPhotoAnalyzer
{
    /// <summary>
    /// The banner every run starts with and the warranty notice, shared by the console and the
    /// window.
    /// </summary>
    internal static class RunReport
    {
        /// <summary>
        /// The first two lines of any run: the version and the copyright notice.
        ///
        /// The banner on the first line means that a copied report says by itself which version
        /// produced it - that is why it also appears with --help and on failed runs. The
        /// copyright on the second is required by GPLv3 (see LICENSE).
        /// </summary>
        /// <param name="w">The writer the two lines go to.</param>
        internal static void WriteBanner(TextWriter w)
        {
            w.WriteLine(VersionInfo.Banner);
            w.WriteLine(VersionInfo.Copyright);
        }

        /// <summary>
        /// The warranty and licence notice - GPLv3 section 5(d). The console prints it with
        /// --help (the banner carries only the copyright; five more lines on every report would
        /// drown the figures), the window shows it in "Despre". One text for both, so that they
        /// cannot say two different things about the same licence.
        ///
        /// The lines are broken at ~70 columns for the console; the window joins them with a
        /// space and wraps them itself. A property, so that the lines are in the interface
        /// language in use when they are shown (ADR 0048).
        /// </summary>
        internal static string[] WarrantyLines => Tr.Current == UiLanguage.English
            ? new[]
            {
                "This program comes with ABSOLUTELY NO WARRANTY, not even the implied",
                "warranty of merchantability or fitness for a particular purpose. It is",
                "free software: you can redistribute it and modify it under the terms",
                "of the GPLv3 (or, at your option, any later version). The full text is",
                "in the LICENSE file, or at <" + LicenseUrl + ">.",
            }
            : new[]
            {
                "Acest program vine FARA NICIO GARANTIE, nici macar cea implicita de",
                "vandabilitate sau potrivire pentru un scop anume. E software liber:",
                "il poti redistribui si modifica in termenii GPLv3 (sau, la alegerea",
                "ta, orice versiune ulterioara). Textul complet e in fisierul LICENSE,",
                "sau la <" + LicenseUrl + ">.",
            };

        /// <summary>
        /// Where the licence's full text is, for whoever does not have the LICENSE file at hand.
        /// </summary>
        internal const string LicenseUrl = "https://www.gnu.org/licenses/gpl-3.0.html";
    }
}
