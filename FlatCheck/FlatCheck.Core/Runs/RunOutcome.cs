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

// RunOutcome.cs - what a complete run of a mode returns.
//
// WHY THE Runs/ LAYER EXISTS. Until 2.0.0 the whole chain of a mode - load the file, call the
// analyzer, call the report, encode the map - lived in FlatCheckCli, that is in the console
// application. When a second application appeared over the same core, that chain would have
// had to be written a second time, and from there to two slightly different behaviours was a
// single maintenance slip.
//
// So the chain moved here, and the console and the window kept only what truly sets them
// apart: the console writes the text to stdout and the map to disk and returns an exit code;
// the window binds the result to some controls and shows the map from memory.
//
// TWO DECISIONS OF SHAPE, both so that nothing is duplicated:
//
//   - The text is NOT returned as a string, it is written into a TextWriter received. So the
//     console keeps its streaming (a series of 32 frames writes its rows as they are ready,
//     not after two minutes of silence), and whoever wants the string hands in a StringWriter
//     and reads it back. A "Report" field would have forced accumulation everywhere, so it
//     would have broken exactly the console's good behaviour.
//
//   - The map is returned as PNG bytes, not written to disk. The mode does not know where the
//     caller wants the file, and the window does not want a file at all - it wants to show
//     it. The writing and the announcement stay the caller's (see RunReport.WriteMapSaved).

namespace FlatCheck
{
    /// <summary>
    /// The result of a mode that always produces an answer: structure, the A/B ratio,
    /// exposure. These modes cannot refuse their input - at most they give a bad verdict,
    /// which is still an answer.
    /// </summary>
    /// <typeparam name="TResult">The analyzer's result record (for instance
    /// <see cref="StructureResult"/>).</typeparam>
    internal sealed class RunOutcome<TResult>
    {
        /// <summary>The analyzer's record, with every figure. Never null here.</summary>
        internal readonly TResult Result;

        /// <summary>
        /// The residual map already encoded as PNG, or null when the mode produces no map
        /// (--expo measures a level, it has nothing to draw).
        /// </summary>
        internal readonly byte[] Png;

        /// <summary>
        /// A second map, when the mode produces two, null otherwise. Only the attribution mode:
        /// it splits the master into two components, and each has a different shape to show -
        /// the residual of the camera-fixed part (which matches the verdict) and the A/B ratio
        /// (which shows the shape of the external gradient). The window toggles between them;
        /// the console writes both.
        /// </summary>
        internal readonly byte[] SecondaryPng;

        /// <summary>Bundles a run's result with its encoded maps.</summary>
        /// <param name="result">The analyzer's record; not null.</param>
        /// <param name="png">The residual map as PNG bytes, or null when the mode draws
        /// none.</param>
        /// <param name="secondaryPng">The second map as PNG bytes (the ratio mode's A/B), or
        /// null.</param>
        internal RunOutcome(TResult result, byte[] png, byte[] secondaryPng = null)
        {
            Result = result;
            Png = png;
            SecondaryPng = secondaryPng;
        }
    }
}
