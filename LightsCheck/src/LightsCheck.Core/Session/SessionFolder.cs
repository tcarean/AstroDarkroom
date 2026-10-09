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

namespace LightsCheck.Core;

/// <summary>
/// A Siril session folder as LightsCheck reads it (R-002, ADR 0004): the lights in
/// <c>lights/</c>, or in the folder itself when it holds frames directly; the biases in
/// <c>biases/</c>, optional. Nothing is ever written into it.
/// </summary>
internal sealed class SessionFolder
{
    internal const string LightsName = "lights";
    internal const string BiasesName = "biases";

    internal string Root { get; }
    internal string LightsDir { get; }
    internal string? BiasesDir { get; }
    internal IReadOnlyList<string> Lights { get; }
    internal IReadOnlyList<string> Biases { get; }

    private SessionFolder(string root, string lightsDir, string? biasesDir, IReadOnlyList<string> lights, IReadOnlyList<string> biases)
    {
        Root = root;
        LightsDir = lightsDir;
        BiasesDir = biasesDir;
        Lights = lights;
        Biases = biases;
    }

    /// <summary>
    /// Opens the folder. When <c>lights/</c> exists it is the lights folder, empty or not: a
    /// session root also holds the stacked results (<c>result.fit</c>), which must never pass
    /// for lights. Without <c>lights/</c> the folder itself must hold the frames. A missing
    /// folder throws <see cref="DirectoryNotFoundException"/>, a folder without frames
    /// <see cref="InvalidOperationException"/>, both with the path in the message.
    /// </summary>
    internal static SessionFolder Open(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string root = Path.GetFullPath(path);
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"nu gasesc folderul {root}");
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
                $"niciun cadru in {lightsDir} (raw: " + string.Join(", ", RawReader.RawExtensions)
                + "; FITS: .fit, .fits, .fts)");
        }

        string biasesDir = Path.Combine(root, BiasesName);
        IReadOnlyList<string> biases = Directory.Exists(biasesDir) ? FramesIn(biasesDir) : [];
        return new SessionFolder(root, lightsDir, biases.Count > 0 ? biasesDir : null, lights, biases);
    }

    /// <summary>
    /// The frames of one folder, sorted by file name (ordinal, case-insensitive, so DSC_0010
    /// follows DSC_0009). Camera raws when there are any; otherwise FITS files. The two are
    /// not mixed: a folder with both holds converted copies beside the originals.
    /// </summary>
    internal static IReadOnlyList<string> FramesIn(string dir)
    {
        string[] files = Directory.GetFiles(dir);
        List<string> raws = files.Where(RawReader.IsRawPath).ToList();
        List<string> chosen = raws.Count > 0 ? raws : files.Where(FitsReader.IsFitsPath).ToList();
        chosen.Sort((a, b) => string.Compare(Path.GetFileName(a), Path.GetFileName(b), StringComparison.OrdinalIgnoreCase));
        return chosen;
    }
}
