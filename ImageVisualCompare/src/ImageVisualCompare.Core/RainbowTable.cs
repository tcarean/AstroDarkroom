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

namespace ImageVisualCompare.Core;

/// <summary>
/// Siril 1.4.4's rainbow colour map, the "color-map" toggle of its display: a hue from 300°
/// at level 0 to 0° at level 255, saturation and value 1, through its <c>hsv_to_rgb</c>
/// (<c>make_index_for_rainbow</c> in <c>src/gui/image_display.c</c>, <c>hsv_to_rgb</c> in
/// <c>src/algos/colors.c</c>; R-112, ADR 0040). Level 0 is magenta, not black.
/// </summary>
public static class RainbowTable
{
    /// <summary>Red, green and blue of each level, three bytes a level, indexed by level × 3.</summary>
    private static readonly byte[] Rgb = Build();

    /// <summary>
    /// The level of each colour, looked up by the colour packed as red, green, blue; the table
    /// is one to one, so every colour in it has one level.
    /// </summary>
    private static readonly Dictionary<int, byte> Levels = Invert();

    /// <summary>Gives the colour of a level.</summary>
    /// <param name="level">The equalised level, 0 to 255.</param>
    /// <returns>Red, green and blue, each 0 to 255.</returns>
    public static (byte R, byte G, byte B) Colour(byte level) =>
        (Rgb[level * 3], Rgb[(level * 3) + 1], Rgb[(level * 3) + 2]);

    /// <summary>
    /// Writes the colour of a level into a Bgra32 pixel, blue first, and alpha 255.
    /// </summary>
    /// <param name="level">The equalised level, 0 to 255.</param>
    /// <param name="bgra">Receives the four bytes of the pixel.</param>
    public static void WriteBgra(byte level, Span<byte> bgra)
    {
        int at = level * 3;
        bgra[0] = Rgb[at + 2];
        bgra[1] = Rgb[at + 1];
        bgra[2] = Rgb[at];
        bgra[3] = 255;
    }

    /// <summary>
    /// Gives the level a colour of the table stands for, for the readout of R-114.
    /// </summary>
    /// <param name="r">Red, 0 to 255.</param>
    /// <param name="g">Green, 0 to 255.</param>
    /// <param name="b">Blue, 0 to 255.</param>
    /// <returns>The level, or null when the colour is not in the table.</returns>
    public static byte? LevelOf(byte r, byte g, byte b) =>
        Levels.TryGetValue(Pack(r, g, b), out byte level) ? level : null;

    /// <summary>Packs a colour into one int, for <see cref="Levels"/>.</summary>
    /// <param name="r">Red, 0 to 255.</param>
    /// <param name="g">Green, 0 to 255.</param>
    /// <param name="b">Blue, 0 to 255.</param>
    /// <returns>Red in the high byte, blue in the low one.</returns>
    private static int Pack(byte r, byte g, byte b) => (r << 16) | (g << 8) | b;

    /// <summary>Builds the 256 colours as Siril does.</summary>
    /// <returns>Red, green and blue of each level, three bytes a level.</returns>
    private static byte[] Build()
    {
        var rgb = new byte[256 * 3];

        // Siril: "Arbitrary: we want h from 300 to 0 deg", the hue a fraction of a turn.
        const double offset = 300.0 / 360.0;
        for (int level = 0; level < 256; level++)
        {
            double hue = offset - (level * (offset / 255.0));
            (double r, double g, double b) = HsvToRgb(hue, 1.0, 1.0);
            rgb[level * 3] = LevelLookup.RoundToByte(r * 255.0);
            rgb[(level * 3) + 1] = LevelLookup.RoundToByte(g * 255.0);
            rgb[(level * 3) + 2] = LevelLookup.RoundToByte(b * 255.0);
        }

        return rgb;
    }

    /// <summary>Builds <see cref="Levels"/> from <see cref="Rgb"/>.</summary>
    /// <returns>The level of each colour of the table.</returns>
    private static Dictionary<int, byte> Invert()
    {
        var levels = new Dictionary<int, byte>(256);
        for (int level = 0; level < 256; level++)
        {
            levels[Pack(Rgb[level * 3], Rgb[(level * 3) + 1], Rgb[(level * 3) + 2])] = (byte)level;
        }

        return levels;
    }

    /// <summary>
    /// Siril's <c>hsv_to_rgb</c>, every value a fraction between 0 and 1, the hue a fraction of
    /// a turn; its integer sectors are kept, since they decide the colour of every level.
    /// </summary>
    /// <param name="h">The hue, 0 to 1 for 0° to 360°.</param>
    /// <param name="s">The saturation, 0 to 1.</param>
    /// <param name="v">The value, 0 to 1.</param>
    /// <returns>Red, green and blue, each 0 to 1.</returns>
    private static (double R, double G, double B) HsvToRgb(double h, double s, double v)
    {
        if (h >= 1.0)
        {
            h -= 1.0;
        }

        h *= 6.0;
        int sector = (int)h;
        double f = h - sector;
        double p = v * (1.0 - s);
        double q = v * (1.0 - (s * f));
        double t = v * (1.0 - (s * (1.0 - f)));

        return sector switch
        {
            0 => (v, t, p),
            1 => (q, v, p),
            2 => (p, v, t),
            3 => (p, q, v),
            4 => (t, p, v),
            _ => (v, p, q),
        };
    }
}
