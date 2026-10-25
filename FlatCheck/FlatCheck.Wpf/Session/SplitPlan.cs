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

// SplitPlan.cs - what "split the session into N parts" means on disk.
//
// The Split tab's rules of form (ADR 0054), with no run and no window, so they can be tested
// on their own: which files of flats/ are frames (raw or FITS, in name order; a readme.txt is
// not a frame), whether their count divides exactly, what each temp session gets and where it
// sits in TEMP.
//
// WHAT IS COPIED is decided by the script, not a fixed list: the folders the script enters
// with "cd" (SirilScript.RequiredFolders - biases/, darks/, whatever) are copied whole,
// lights/ gets at most MaxLights frames (the first by name: enough for the script to reach the
// end, not a hundred lights copied four times), and flats/ gets the part's slice. A fixed list
// lets the OSC_Preprocessing.ssf script die at "cd darks" on part 1, after gigabytes had been
// copied - exactly what the check before starting exists to prevent (ADR 0035).
//
// The original session is only read: there is no move and no deletion here, and Delete()
// deletes only the temp root this class built.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FlatCheck.Wpf
{
    /// <summary>
    /// The disk rule of the Split tab (ADR 0054): which flats are frames, whether they divide,
    /// what each temp part session gets and where; reads the original session only.
    /// </summary>
    internal sealed class SplitPlan
    {
        /// <summary>How many files of lights/ go into each part, at most.</summary>
        internal const int MaxLights = 2;

        /// <summary>Into how many parts it can be split: 2 (0/180) or 4
        /// (0/90/180/270).</summary>
        internal static readonly IReadOnlyList<int> Choices = new[] { 2, 4 };

        internal const string PartPrefix = "part-";

        internal const string FlatsFolder = "flats";
        internal const string LightsFolder = "lights";

        readonly SirilSession _session;

        /// <summary>
        /// Plans a split of a session: lists its flat frames and decides which folders are
        /// copied whole. Throws an <see cref="ArgumentNullException"/> for a null session or
        /// temp root, and an <see cref="ArgumentOutOfRangeException"/> for a part count not in
        /// <see cref="Choices"/>.
        /// </summary>
        /// <param name="session">The session the parts are cut from: its flats split between them,
        /// at most <see cref="MaxLights"/> lights when the script enters lights/, and every other
        /// folder the script enters, but Siril's output folders, copied whole; never null.</param>
        /// <param name="parts">How many parts: one of <see cref="Choices"/>.</param>
        /// <param name="tempRoot">The folder the part sessions are built under; never null.</param>
        /// <param name="requiredFolders">The folders the script enters
        /// (SirilScript.RequiredFolders); flats/ is copied anyway.</param>
        internal SplitPlan(SirilSession session, int parts, string tempRoot, IEnumerable<string> requiredFolders)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            if (!Choices.Contains(parts)) throw new ArgumentOutOfRangeException(nameof(parts));
            Parts = parts;
            TempRoot = tempRoot ?? throw new ArgumentNullException(nameof(tempRoot));
            Frames = session.FlatFrames();
            // process/ and masters/ are what Siril writes, not inputs: a script that enters them
            // with "cd process" does not get the original's 17 GB, nor its master
            WholeFolders = (requiredFolders ?? Array.Empty<string>())
                .Where(f => !Is(f, FlatsFolder) && !Is(f, LightsFolder)
                            && !SirilSession.OutputFolders.Any(o => Is(f, o)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            CopiesLights = (requiredFolders ?? Array.Empty<string>()).Any(f => Is(f, LightsFolder));
        }

        /// <summary>Compares two folder names ignoring case.</summary>
        /// <param name="folder">A folder name from the script.</param>
        /// <param name="name">A known folder name.</param>
        /// <returns>True when they are the same name.</returns>
        static bool Is(string folder, string name) => string.Equals(folder, name, StringComparison.OrdinalIgnoreCase);

        internal int Parts { get; }
        internal string TempRoot { get; }

        /// <summary>The folders copied whole into each part: those the script enters, apart
        /// from flats/ and lights/.</summary>
        internal IReadOnlyList<string> WholeFolders { get; }

        /// <summary>True when the script enters lights/, so each part gets at most MaxLights
        /// frames from there.</summary>
        internal bool CopiesLights { get; }

        /// <summary>The frames of flats/, in name order: raw or FITS, nothing else.</summary>
        internal IReadOnlyList<string> Frames { get; }

        internal bool Divides => Frames.Count > 0 && Frames.Count % Parts == 0;

        internal int FramesPerPart => Divides ? Frames.Count / Parts : 0;

        /// <summary>The root of one run: %TEMP%\FlatCheck\split-yyyyMMdd-HHmmss.</summary>
        /// <param name="when">The run's start, local time.</param>
        /// <returns>The path; nothing is created.</returns>
        internal static string NewTempRoot(DateTime when)
            => Path.Combine(Path.GetTempPath(), "FlatCheck", "split-" + when.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture));

        /// <summary>The folder of part <paramref name="part"/> (from 1).</summary>
        /// <param name="part">The part's number, 1 to <see cref="Parts"/>.</param>
        /// <returns>The part session's root under <see cref="TempRoot"/>.</returns>
        internal string PartRoot(int part) => Path.Combine(TempRoot, PartPrefix + part);

        /// <summary>The slice of frames of part <paramref name="part"/> (from 1): consecutive
        /// blocks.</summary>
        /// <param name="part">The part's number, 1 to <see cref="Parts"/>.</param>
        /// <returns>The part's flats, or empty when the count does not divide.</returns>
        internal IReadOnlyList<string> PartFrames(int part)
        {
            if (!Divides) return Array.Empty<string>();
            int n = FramesPerPart;
            return Frames.Skip((part - 1) * n).Take(n).ToList();
        }

        /// <summary>The frames of lights/ that go into each part: the first MaxLights frames
        /// (raw or FITS) by name.</summary>
        /// <returns>The lights to copy, or empty when the script does not enter
        /// lights/.</returns>
        internal IReadOnlyList<string> LightsToCopy()
            => CopiesLights
                ? AllFiles(Path.Combine(_session.Root, LightsFolder)).Where(IsFrame).Take(MaxLights).ToList()
                : Array.Empty<string>();

        /// <summary>Whether a file is a frame: a raw or a FITS by its extension.</summary>
        /// <param name="path">A file path.</param>
        /// <returns>True for a frame.</returns>
        static bool IsFrame(string path) => RawReader.IsRawPath(path) || FitsReader.IsFitsPath(path);

        /// <summary>Everything copied for one part, as (source, destination) pairs, in the
        /// order of copying.</summary>
        /// <param name="part">The part's number, 1 to <see cref="Parts"/>.</param>
        /// <returns>The whole folders, then the lights, then the part's flats.</returns>
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
        /// How many bytes are copied in total, over all parts: the whole folders and the lights
        /// Parts times, each flat once. Each folder is enumerated a single time - it is called
        /// at every change of chip or script, on the UI thread.
        /// </summary>
        /// <returns>The total in bytes; a file that cannot be sized counts 0.</returns>
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

        /// <summary>A file's size.</summary>
        /// <param name="path">A file path.</param>
        /// <returns>Its length in bytes, or 0 when it cannot be read.</returns>
        static long SizeOf(string path)
        {
            try { return new FileInfo(path).Length; }
            catch (IOException) { return 0; }
            catch (UnauthorizedAccessException) { return 0; }
        }

        /// <summary>How many frames (raw or FITS) each part has in the folders copied whole; a
        /// readme or an .xmp is not a frame.</summary>
        /// <returns>The frame count over every whole folder.</returns>
        internal int WholeFrameCount() => WholeFolders.Sum(f => AllFiles(Path.Combine(_session.Root, f)).Count(IsFrame));

        /// <summary>The part's temp session, through the same rules as the original's (where
        /// the master is).</summary>
        /// <param name="part">The part's number, 1 to <see cref="Parts"/>.</param>
        /// <returns>A session over the part's folder.</returns>
        internal SirilSession PartSession(int part) => new(PartRoot(part));

        /// <summary>The files of a folder, not its subfolders, ordinal by name.</summary>
        /// <param name="dir">The folder.</param>
        /// <returns>The full paths, or empty when the folder does not exist.</returns>
        static IReadOnlyList<string> AllFiles(string dir)
            => Directory.Exists(dir)
                ? Directory.EnumerateFiles(dir).OrderBy(Path.GetFileName, StringComparer.Ordinal).ToList()
                : Array.Empty<string>();

        /// <summary>
        /// Deletes the temp root, with everything in it. A raw copied from a card is often
        /// read-only and File.Copy keeps the attribute, so first it is lifted on everything
        /// there. A disk error is not an error of the run: it returns false.
        /// </summary>
        /// <returns>True when the root is gone (or never existed), false on a disk
        /// error.</returns>
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
