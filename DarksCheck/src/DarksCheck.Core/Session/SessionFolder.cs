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

namespace DarksCheck.Core;

/// <summary>
/// A Siril session folder, or a bare folder of darks, as DarksCheck reads it (R-002, ADR
/// 0002): the darks in <c>darks/</c>, or in the folder itself when it holds frames directly
/// and no session subfolder; the biases in <c>biases/</c> and the lights in <c>lights/</c>,
/// both optional; <c>flats/</c> and everything else untouched. Nothing is ever written into it.
/// </summary>
internal sealed class SessionFolder
{
    internal const string DarksName = "darks";
    internal const string BiasesName = "biases";
    internal const string LightsName = "lights";
    internal const string FlatsName = "flats";

    internal string Root { get; }
    internal string DarksDir { get; }
    internal string? BiasesDir { get; }
    internal string? LightsDir { get; }
    internal IReadOnlyList<string> Darks { get; }
    internal IReadOnlyList<string> Biases { get; }
    internal IReadOnlyList<string> Lights { get; }

    /// <summary>
    /// False for a session with lights and no darks (R-054), opened with <c>allowLightsOnly</c>: a
    /// partner's darks stand in for its own.
    /// </summary>
    internal bool HasDarks => Darks.Count > 0;

    /// <summary>Holds what <see cref="Open"/> found; nothing is checked here.</summary>
    /// <param name="root">The full path of the folder opened.</param>
    /// <param name="darksDir">The folder the darks were looked for in: <c>darks/</c>, or the root
    /// for a library.</param>
    /// <param name="biasesDir">The <c>biases/</c> folder when it holds frames, else null.</param>
    /// <param name="lightsDir">The <c>lights/</c> folder when it holds frames, else null.</param>
    /// <param name="darks">The darks' paths, sorted; empty only for a session without darks
    /// (R-054).</param>
    /// <param name="biases">The biases' paths, sorted; empty when there are none.</param>
    /// <param name="lights">The lights' paths, sorted; empty when there are none.</param>
    private SessionFolder(string root, string darksDir, string? biasesDir, string? lightsDir, IReadOnlyList<string> darks, IReadOnlyList<string> biases, IReadOnlyList<string> lights)
    {
        Root = root;
        DarksDir = darksDir;
        BiasesDir = biasesDir;
        LightsDir = lightsDir;
        Darks = darks;
        Biases = biases;
        Lights = lights;
    }

    /// <summary>
    /// Opens the folder. When <c>darks/</c> exists it is the darks folder, empty or not. A
    /// folder with <c>lights/</c>, <c>biases/</c> or <c>flats/</c> but no <c>darks/</c> is a
    /// session without darks: an error, never a fallback to the root, which holds the stacked
    /// results (<c>result.fit</c>); with <paramref name="allowLightsOnly"/> a <c>lights/</c>
    /// with frames opens as a session whose darks are empty (R-054). Without any session
    /// subfolder the folder itself must hold the frames (a library of darks). A missing folder
    /// throws <see cref="DirectoryNotFoundException"/>, a folder without frames
    /// <see cref="InvalidOperationException"/>, both with the path in the message.
    /// </summary>
    /// <param name="path">The session folder or the library folder; not null or blank, relative
    /// paths allowed.</param>
    /// <param name="allowLightsOnly">True to accept a session with lights and no darks (R-054), as
    /// a run against a partner does.</param>
    /// <returns>The folder with its frames' paths.</returns>
    internal static SessionFolder Open(string path, bool allowLightsOnly = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string root = Path.GetFullPath(path);
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"nu gasesc folderul {root}");
        }

        string darksDir = Path.Combine(root, DarksName);
        bool lightsOnly = false;
        IReadOnlyList<string>? lightsFound = null;
        if (!Directory.Exists(darksDir))
        {
            string[] others = [LightsName, BiasesName, FlatsName];
            string? present = others.FirstOrDefault(n => Directory.Exists(Path.Combine(root, n)));
            if (present == LightsName && allowLightsOnly && (lightsFound = FramesIn(Path.Combine(root, LightsName))).Count > 0)
            {
                lightsOnly = true;
            }
            else if (present is not null)
            {
                throw new InvalidOperationException($"sesiunea {root} are {present}\\ dar nu are darks\\");
            }
            else
            {
                darksDir = root;
            }
        }

        IReadOnlyList<string> darks = lightsOnly ? [] : FramesIn(darksDir);
        if (darks.Count == 0 && !lightsOnly)
        {
            throw new InvalidOperationException(
                $"niciun cadru in {darksDir} (raw: " + string.Join(", ", RawReader.RawExtensions)
                + "; FITS: .fit, .fits, .fts)");
        }

        string biasesDir = Path.Combine(root, BiasesName);
        IReadOnlyList<string> biases = darksDir != root && Directory.Exists(biasesDir) ? FramesIn(biasesDir) : [];
        string lightsDir = Path.Combine(root, LightsName);
        IReadOnlyList<string> lights = lightsFound ?? (darksDir != root && Directory.Exists(lightsDir) ? FramesIn(lightsDir) : []);
        return new SessionFolder(root, darksDir, biases.Count > 0 ? biasesDir : null, lights.Count > 0 ? lightsDir : null, darks, biases, lights);
    }

    /// <summary>
    /// The frames of one folder, sorted by file name (ordinal, case-insensitive, so DSC_0010
    /// follows DSC_0009). Camera raws when there are any; otherwise FITS files. The two are
    /// not mixed: a folder with both holds converted copies beside the originals.
    /// </summary>
    /// <param name="dir">The folder to list; it must exist.</param>
    /// <returns>The full paths of its frames, sorted; empty when it holds none.</returns>
    internal static IReadOnlyList<string> FramesIn(string dir)
    {
        string[] files = Directory.GetFiles(dir);
        List<string> raws = files.Where(RawReader.IsRawPath).ToList();
        List<string> chosen = raws.Count > 0 ? raws : files.Where(FitsReader.IsFitsPath).ToList();
        chosen.Sort((a, b) => string.Compare(Path.GetFileName(a), Path.GetFileName(b), StringComparison.OrdinalIgnoreCase));
        return chosen;
    }
}
