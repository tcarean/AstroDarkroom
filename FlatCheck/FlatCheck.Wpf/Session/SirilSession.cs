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

// SirilSession.cs - what "a session's folder" means on disk.
//
// The console application receives files; this one receives ONE folder and deduces the rest
// through the convention of Siril's scripts (flats/ darks/ biases/ lights/ process/).
// Everything to do with that deduction sits here, and not in the windows, for a practical
// reason: it is the only part of the WPF application with real rules - which file is the
// master, which files are candidates for the report, which are raws - and the only one that
// can be tested without opening a window.
//
// The class touches nothing and does not throw at construction: it asks the disk only when it
// is asked. So a folder chosen wrong becomes a state of the interface, not an exception.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FlatCheck.Wpf
{
    /// <summary>Why the window cannot start on this folder. <see cref="None"/> = it
    /// can.</summary>
    internal enum SessionProblem
    {
        None,
        NoFolder,        // the path does not exist at all
        NoProcessDir,    // no preprocessing script has been run
        NoFlatMaster,    // process/ exists, but the flat master does not
    }

    /// <summary>
    /// What kind of row it is in the histogram tab's list. The master is the stack of the
    /// others; the smoothed flat is the master without the texture, written by the Netezire
    /// tab; a frame is a frame. A bool cannot say three things.
    /// </summary>
    internal enum HistEqKind { Master, Smoothed, Frame }

    /// <summary>
    /// A file the histogram tab can show: the master from masters/ or process/, the smoothed
    /// flat beside it when it exists, or a frame from flats/.
    /// </summary>
    /// <param name="Path">The file's full path.</param>
    /// <param name="Kind">Which kind of row it is.</param>
    internal readonly record struct HistEqCandidate(string Path, HistEqKind Kind);

    /// <summary>
    /// The result of the search for the master: the path chosen (null = it is nowhere) and,
    /// when it exists in both places, the path of the older one, which nobody measures.
    /// </summary>
    /// <param name="Path">The master to measure, the newer when there are two; null when
    /// there is none.</param>
    /// <param name="Other">The older master when both places hold one, otherwise
    /// null.</param>
    internal readonly record struct FlatMasterLookup(string Path, string Other)
    {
        internal bool Ambiguous => Other != null;
    }

    /// <summary>
    /// One Siril session folder and the rules of what is in it: where the master is, which
    /// files each tab lists, where the window writes; asks the disk only when asked.
    /// </summary>
    internal sealed class SirilSession
    {
        /// <summary>The name the OSC_Preprocessing.ssf script gives the flat master.</summary>
        internal const string FlatMasterName = "pp_flat_stacked.fit";

        /// <summary>
        /// The preprocessing script named in the messages and proposed by default by the run
        /// button: the one the author uses (a camera without darks), from Siril's scripts
        /// repository, not from the installation - see SirilInstall for where the file is. It
        /// leaves the master in process/; the standard script of the 1.4 installation leaves it
        /// in masters/, and both places are accepted - see
        /// <see cref="FlatMasterCandidates"/>.
        /// </summary>
        internal const string PreprocessingScript = "OSC_Preprocessing_WithoutDark.ssf";

        /// <summary>The structure report, saved beside the session's data.</summary>
        internal const string ResultFileName = "Test result.txt";

        internal string Root { get; }

        /// <summary>Wraps a folder; nothing is read yet. Throws an
        /// <see cref="ArgumentNullException"/> for a null root.</summary>
        /// <param name="root">The session folder, existing or not.</param>
        internal SirilSession(string root)
        {
            Root = root ?? throw new ArgumentNullException(nameof(root));
        }

        internal const string ProcessFolder = "process";
        internal const string MastersFolder = "masters";
        /// <summary>The folders Siril WRITES; a split never copies them from the original
        /// (ADR 0054).</summary>
        internal static readonly IReadOnlyList<string> OutputFolders = new[] { ProcessFolder, MastersFolder };

        internal string ProcessDir => Path.Combine(Root, ProcessFolder);
        internal string MastersDir => Path.Combine(Root, MastersFolder);
        internal string FlatsDir => Path.Combine(Root, "flats");

        /// <summary>
        /// Where the flat master can sit, in the order it is looked for. Two Siril conventions
        /// coexist: the repository's scripts (WithoutDark and its siblings) leave it in
        /// process/, beside the calibrated frames; the scripts of the 1.4 installation
        /// (OSC_Preprocessing.ssf) write it in masters/. Both exist in the real archive, so
        /// both are accepted.
        /// </summary>
        internal IReadOnlyList<string> FlatMasterCandidates => new[]
        {
            Path.Combine(MastersDir, FlatMasterName),
            Path.Combine(ProcessDir, FlatMasterName),
        };

        /// <summary>
        /// Mode 1's input: the master found in one of the two places, or null. When it exists
        /// in both, THE NEWEST is taken - it is what the last preprocessing produced - and
        /// <see cref="FlatMasterLookup.Other"/> names the other, so the tab can warn: two
        /// masters in the same folder mean two runs, and the user must know which one is
        /// measured.
        /// </summary>
        /// <returns>The master chosen and the older one, each null when absent.</returns>
        internal FlatMasterLookup FindFlatMaster()
        {
            var existing = FlatMasterCandidates.Where(File.Exists).ToList();
            if (existing.Count == 0) return new FlatMasterLookup(null, null);
            if (existing.Count == 1) return new FlatMasterLookup(existing[0], null);

            var byAge = existing.OrderByDescending(File.GetLastWriteTimeUtc).ToList();
            return new FlatMasterLookup(byAge[0], byAge[1]);
        }

        /// <summary>The path of the master found, or null. See
        /// <see cref="FindFlatMaster"/>.</summary>
        internal string FlatMasterPath => FindFlatMaster().Path;

        /// <summary>
        /// The smoothed flat's suffix: "pp_flat_stacked_netezit.fit", beside the master it was
        /// made from. The name is NOT one of <see cref="FlatMasterCandidates"/> - exact paths,
        /// not patterns - so the session's master stays the one photographed and the Structure
        /// tab still shows the texture; the smoothed file must not hide the problem it was made
        /// for.
        /// </summary>
        internal const string SmoothedSuffix = "_smoothed";

        /// <summary>Where the smoothing tab writes, or null when there is no master.</summary>
        internal string SmoothedMasterPath
        {
            get
            {
                string master = FlatMasterPath;
                return master == null ? null : SmoothedPathBeside(master);
            }
        }

        /// <summary>The name of the smoothed flat made from <paramref name="master"/>, in the
        /// same folder.</summary>
        /// <param name="master">A master's full path.</param>
        /// <returns>The path with <see cref="SmoothedSuffix"/> before the extension.</returns>
        internal static string SmoothedPathBeside(string master)
            => Path.Combine(Path.GetDirectoryName(master),
                Path.GetFileNameWithoutExtension(master) + SmoothedSuffix + Path.GetExtension(master));

        /// <summary>
        /// The SYNTHETIC flat's suffix (the Sintetic tab): pp_flat_stacked_sintetic.fit, beside
        /// the master, for the same reasons as the smoothed one - it does not become the
        /// session's master, but it is what --against expects.
        /// </summary>
        internal const string SyntheticSuffix = "_synthetic";

        /// <summary>Where the Sintetic tab writes, or null when there is no master.</summary>
        internal string SyntheticMasterPath
        {
            get
            {
                string master = FlatMasterPath;
                return master == null ? null : SyntheticPathBeside(master);
            }
        }

        /// <summary>The name of the synthetic flat made for <paramref name="master"/>, in the
        /// same folder.</summary>
        /// <param name="master">A master's full path.</param>
        /// <returns>The path with <see cref="SyntheticSuffix"/> before the extension.</returns>
        internal static string SyntheticPathBeside(string master)
            => Path.Combine(Path.GetDirectoryName(master),
                Path.GetFileNameWithoutExtension(master) + SyntheticSuffix + Path.GetExtension(master));

        /// <summary>Where the structure report is written: in the folder given, not in
        /// process/.</summary>
        internal string ResultTextPath => Path.Combine(Root, ResultFileName);

        /// <summary>The residual map, beside the report and with the same name as the
        /// console's.</summary>
        internal string ResidualPngPath => Path.Combine(Root, "flatcheck.png");

        /// <summary>The two switches the window remembers for this folder (R-119), beside the
        /// report: <see cref="SessionSettings.FileName"/> in the session root.</summary>
        internal string SettingsPath => Path.Combine(Root, SessionSettings.FileName);

        /// <summary>The Split tab's report and mosaic, in the session's root, beside the
        /// structure's (ADR 0054).</summary>
        internal const string SplitResultFileName = "Split result.txt";
        internal string SplitResultTextPath => Path.Combine(Root, SplitResultFileName);
        internal string SplitPngPath => Path.Combine(Root, "flatcheck-split.png");

        /// <summary>
        /// The first check, before any computation. The order matters: a wrong folder chosen,
        /// one on which no script has run yet and one on which the script ran but left no
        /// master are three different mistakes, and deserve three messages.
        ///
        /// The only thing required is THE MASTER, wherever it is of the two places. process/ is
        /// not a condition: a folder with masters/ and without process/ - the frames deleted to
        /// make room, the master kept - runs the structure, and the end-to-end tab says by
        /// itself that it has no series.
        /// </summary>
        /// <returns>The first problem found, or <see cref="SessionProblem.None"/>.</returns>
        internal SessionProblem Check()
        {
            if (!Directory.Exists(Root)) return SessionProblem.NoFolder;
            if (!Directory.Exists(ProcessDir) && !Directory.Exists(MastersDir)) return SessionProblem.NoProcessDir;
            if (FlatMasterPath == null) return SessionProblem.NoFlatMaster;
            return SessionProblem.None;
        }

        /// <summary>
        /// Of the subfolders a script enters (SirilScript.RequiredFolders), those that are
        /// missing or empty - that is exactly those at which Siril would stop with a failed
        /// "cd" or with a conversion without files. Empty means it can start.
        /// </summary>
        /// <param name="required">The folder names the script enters.</param>
        /// <returns>The names missing or without a file, in the given order.</returns>
        internal IReadOnlyList<string> MissingInputFolders(IEnumerable<string> required)
        {
            var missing = new List<string>();
            foreach (string name in required)
            {
                string dir = Path.Combine(Root, name);
                bool present = Directory.Exists(dir) && Directory.EnumerateFiles(dir).Any();
                if (!present) missing.Add(name);
            }
            return missing;
        }

        /// <summary>
        /// The calibrated frames for the end-to-end test. The same default pattern as the
        /// console's, so also the same exclusion of the registered frames (r_pp_light_*) - see
        /// SeriesFinder.DefaultPattern for why it matters.
        /// </summary>
        /// <returns>The lights of process/, ordinal by name; empty when process/ does not
        /// exist.</returns>
        internal IReadOnlyList<string> LightSeries()
            => Directory.Exists(ProcessDir)
                ? SeriesFinder.Find(ProcessDir, SeriesFinder.DefaultPattern)
                : Array.Empty<string>();

        /// <summary>
        /// The raw flats for --expo: camera raws only.
        ///
        /// The mode needs the CFA mosaic intact and the original ADU scale, and a raw carries
        /// both, exactly, in the file. The extensions come from RawReader, not from a list
        /// written here, so that no camera appears that the reader supports and the list does
        /// not show.
        /// </summary>
        /// <returns>The raws of flats/, ordinal by name; empty when flats/ does not
        /// exist.</returns>
        internal IReadOnlyList<string> RawFlats()
        {
            if (!Directory.Exists(FlatsDir)) return Array.Empty<string>();

            return Directory.EnumerateFiles(FlatsDir)
                .Where(RawReader.IsRawPath)
                .OrderBy(Path.GetFileName, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// The frames of flats/ the loader can read: camera raws AND FITS. For the histogram
        /// equalisation a flat gone through Conversion is as good as the NEF; the reason the
        /// exposure tab accepts only raws (exact white and black) does not apply here. Both
        /// extension lists come from the readers.
        /// </summary>
        /// <returns>The frames of flats/, ordinal by name; empty when flats/ does not
        /// exist.</returns>
        internal IReadOnlyList<string> FlatFrames()
        {
            if (!Directory.Exists(FlatsDir)) return Array.Empty<string>();

            return Directory.EnumerateFiles(FlatsDir)
                .Where(p => RawReader.IsRawPath(p) || FitsReader.IsFitsPath(p))
                .OrderBy(Path.GetFileName, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// The raw from beside the master the aperture and the lens are read from - the first
        /// in flats/, or null when there is none. A single rule for the Sintetic tab (the
        /// prefill) and for the Structure tab (the comparison with the profile), so both speak
        /// about the same file; the rule itself is in the core.
        /// </summary>
        internal string ReferenceRawPath => LensIdentification.FirstRaw(FlatFrames());

        /// <summary>
        /// What the histogram tab lists: the master first, ONLY if it exists; after it the
        /// smoothed flat, ONLY if it exists - on the exact path the Netezire tab writes,
        /// <see cref="SmoothedMasterPath"/>, not by pattern; then the frames of flats/. In the
        /// window the master always exists - Check() refuses the folder without it - but this
        /// class is tested on its own and never throws.
        ///
        /// Only the master by convention - the one Structure measures too, so the same one
        /// when there are two - not any stack in process/: a second stack in a folder is not
        /// the session's master. For the same reason a smoothed file beside the OLDER master,
        /// when there are two, does not appear: it is the smoothing of another master than the
        /// one on screen. And nothing is de-duplicated by content - a master copied into
        /// flats/ appears twice, under two paths, which is the truth.
        /// </summary>
        /// <returns>The rows in display order; empty when there is neither master nor
        /// frame.</returns>
        internal IReadOnlyList<HistEqCandidate> HistEqCandidates()
        {
            var list = new List<HistEqCandidate>();
            string master = FlatMasterPath;
            if (master != null)
            {
                list.Add(new HistEqCandidate(master, HistEqKind.Master));
                string smoothed = SmoothedPathBeside(master);
                if (File.Exists(smoothed)) list.Add(new HistEqCandidate(smoothed, HistEqKind.Smoothed));
            }
            list.AddRange(FlatFrames().Select(p => new HistEqCandidate(p, HistEqKind.Frame)));
            return list;
        }
    }
}
