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

using System.Windows.Media.Imaging;

namespace AstroDarkroom.Wpf;

/// <summary>
/// The icons built into the assembly as resources from Icon/: the launcher's own, for the two
/// logos, and the copy of each component's, for its row (R-010, ADR 0011). Each is decoded
/// once and kept: the rows are rebuilt at every change of language. Used on the UI thread only.
/// </summary>
internal static class Icons
{
    /// <summary>The launcher's own icon, the same resource as Icon= in the windows' XAML and the executable's.</summary>
    internal const string Launcher = "astrodarkroom.ico";

    private static readonly Dictionary<string, BitmapSource> Frames = new(StringComparer.Ordinal);

    /// <summary>The pack URI of a file of Icon/, built into the assembly as a resource; the windows' Icon= names the same path in XAML.</summary>
    /// <param name="fileName">The file's name in Icon/ ("darkscheck.ico").</param>
    /// <returns>The resource's URI.</returns>
    private static Uri Source(string fileName) => new("pack://application:,,,/AstroDarkroom;component/Icon/" + fileName);

    /// <summary>
    /// The icon's frame with the most pixels, which WPF scales down to where it is shown; the
    /// first frame of an .ico is not reliably the largest.
    /// </summary>
    /// <param name="fileName">The file's name in Icon/ ("darkscheck.ico").</param>
    /// <returns>The widest frame, frozen; the same instance at every call for the same file.</returns>
    internal static BitmapSource LargestFrame(string fileName)
    {
        if (!Frames.TryGetValue(fileName, out BitmapSource? frame))
        {
            BitmapDecoder decoder = BitmapDecoder.Create(Source(fileName), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            frame = decoder.Frames.OrderByDescending(f => f.PixelWidth).First();
            if (frame.CanFreeze)
            {
                frame.Freeze();
            }

            Frames[fileName] = frame;
        }

        return frame;
    }
}
