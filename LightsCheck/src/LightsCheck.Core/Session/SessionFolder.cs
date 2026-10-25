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

using static LightsCheck.Core.Ui;

namespace LightsCheck.Core;

/// <summary>
/// A Siril session folder as LightsCheck reads it (R-002, ADR 0004): the lights in
/// <c>lights/</c>, or in the folder itself when it holds frames directly; the biases in
/// <c>biases/</c>, optional; the darks in <c>darks/</c>, optional and only counted, never read
/// (R-042). Nothing is ever written into it.
/// </summary>
internal sealed class SessionFolder
{
    internal const string LightsName = "lights";
    internal const string BiasesName = "biases";
    internal const string DarksName = "darks";

    internal string Root { get; }
    internal string LightsDir { get; }
    internal string? BiasesDir { get; }
    internal string? DarksDir { get; }
    internal IReadOnlyList<string> Lights { get; }
    internal IReadOnlyList<string> Biases { get; }

    /// <summary>
    /// The frames of darks/, listed so the report can say whether Siril will subtract the sensor's
    /// pattern; none is ever read.
    /// </summary>
    internal IReadOnlyList<string> Darks { get; }

    /// <summary>Holds what <see cref="Open"/> found; only <see cref="Open"/> builds one.</summary>
    /// <param name="root">The session folder, as a full path.</param>
    /// <param name="lightsDir">
    /// The folder the lights were taken from: <c>lights/</c>, or the root.
    /// </param>
    /// <param name="biasesDir">
    /// The <c>biases/</c> folder; null when it is missing or holds no frame.
    /// </param>
    /// <param name="darksDir">
    /// The <c>darks/</c> folder; null when it is missing or holds no frame.
    /// </param>
    /// <param name="lights">The lights, full paths sorted by file name; never empty.</param>
    /// <param name="biases">
    /// The biases, full paths sorted by file name; empty when there are none.
    /// </param>
    /// <param name="darks">
    /// The darks, full paths sorted by file name; empty when there are none.
    /// </param>
    private SessionFolder(string root, string lightsDir, string? biasesDir, string? darksDir, IReadOnlyList<string> lights, IReadOnlyList<string> biases, IReadOnlyList<string> darks)
    {
        Root = root;
        LightsDir = lightsDir;
        BiasesDir = biasesDir;
        DarksDir = darksDir;
        Lights = lights;
        Biases = biases;
        Darks = darks;
    }

    /// <summary>
    /// Opens the folder. When <c>lights/</c> exists it is the lights folder, empty or not: a
    /// session root also holds the stacked results (<c>result.fit</c>), which must never pass
    /// for lights. Without <c>lights/</c> the folder itself must hold the frames. A missing
    /// folder throws <see cref="DirectoryNotFoundException"/>, a folder without frames
    /// <see cref="InvalidOperationException"/>, both with the path in the message.
    /// </summary>
    /// <param name="path">
    /// The session folder, absolute or relative to the current directory; not blank.
    /// </param>
    /// <returns>The session's folders and frame lists.</returns>
    internal static SessionFolder Open(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string root = Path.GetFullPath(path);
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException(T($"cannot find the folder {root}", $"nu gasesc folderul {root}"));
        }

        string lightsDir = Path.Combine(root, LightsName);
        if (!Directory.Exists(lightsDir))
        {
            lightsDir = root;
        }

        IReadOnlyList<string> lights = FramesIn(lightsDir);
        if (lights.Count == 0)
        {
            throw new InvalidOperationException(
                T($"no frame in {lightsDir} (raw: ", $"niciun cadru in {lightsDir} (raw: ") + string.Join(", ", RawReader.RawExtensions)
                + "; FITS: .fit, .fits, .fts)");
        }

        string biasesDir = Path.Combine(root, BiasesName);
        IReadOnlyList<string> biases = Directory.Exists(biasesDir) ? FramesIn(biasesDir) : [];
        string darksDir = Path.Combine(root, DarksName);
        IReadOnlyList<string> darks = Directory.Exists(darksDir) ? FramesIn(darksDir) : [];
        return new SessionFolder(root, lightsDir, biases.Count > 0 ? biasesDir : null, darks.Count > 0 ? darksDir : null, lights, biases, darks);
    }

    /// <summary>
    /// The frames of one folder, sorted by file name (ordinal, case-insensitive, so DSC_0010
    /// follows DSC_0009). Camera raws when there are any; otherwise FITS files. The two are
    /// not mixed: a folder with both holds converted copies beside the originals.
    /// </summary>
    /// <param name="dir">An existing folder; its subfolders are not searched.</param>
    /// <returns>
    /// The frames' paths, each <paramref name="dir"/> joined with a file name; possibly none.
    /// </returns>
    internal static IReadOnlyList<string> FramesIn(string dir)
    {
        string[] files = Directory.GetFiles(dir);
        List<string> raws = files.Where(RawReader.IsRawPath).ToList();
        List<string> chosen = raws.Count > 0 ? raws : files.Where(FitsReader.IsFitsPath).ToList();
        chosen.Sort((a, b) => string.Compare(Path.GetFileName(a), Path.GetFileName(b), StringComparison.OrdinalIgnoreCase));
        return chosen;
    }
}
