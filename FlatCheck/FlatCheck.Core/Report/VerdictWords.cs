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

// VerdictWords.cs - the word a verdict is printed as. The analysis hands out verdicts as
// keys, the Romanian words they always were ("REFA", "INAPLICABIL"), and everything that
// decides on them - the exit code, the severity, the colour, the advice - compares those keys.
// Only the printing goes through here, so a translation can never move an exit code (ADR 0062).

using static FlatCheck.Ui;

namespace FlatCheck
{
    /// <summary>The printed word of a verdict key, in the run's language.</summary>
    internal static class VerdictWords
    {
        /// <summary>
        /// The word for a verdict key of either scale, <see cref="LightSeriesAnalyzer.Undetermined"/>
        /// or <see cref="Stats.NotApplicable"/>.
        /// </summary>
        /// <param name="key">The verdict as the analysis gave it; null allowed.</param>
        /// <returns>The word to print; a key that is no verdict (or null) comes back as it
        /// is.</returns>
        internal static string Of(string key) => key switch
        {
            "OK" => "OK",
            "SUSPECT" => "SUSPECT",
            "PROBLEMA" => T("PROBLEM", "PROBLEMA"),
            "PERFECT" => "PERFECT",
            "BUN" => T("GOOD", "BUN"),
            "ACCEPTABIL" => T("ACCEPTABLE", "ACCEPTABIL"),
            "REFA" => T("REDO", "REFA"),
            LightSeriesAnalyzer.Undetermined => T("UNDETERMINED", "NEDETERMINAT"),
            Stats.NotApplicable => T("NOT APPLICABLE", "INAPLICABIL"),
            _ => key,
        };
    }
}
