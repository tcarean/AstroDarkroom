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

// BitmapFont.cs - a hand-made 5x7 bitmap font, for the labels of the split PNG.
//
// The PNG writer is written by hand (ADR 0008) and draws no text; a label like "PARTEA 1 DIN 4"
// or the title with the version and the date does not justify a font package (ADR 0054).
// Digits, capitals and a few signs; lower-case letters are drawn as capitals, anything else
// stays blank but advances, so that the string does not shrink.

using System.Collections.Generic;

namespace FlatCheck
{
    /// <summary>
    /// Draws text into an RGB raster with a built-in 5x7 font, scaled by whole pixels.
    /// </summary>
    internal static class BitmapFont
    {
        internal const int GlyphW = 5;
        internal const int GlyphH = 7;
        /// <summary>
        /// The advance from one letter to the next, in glyph pixels: the width plus one blank
        /// column.
        /// </summary>
        internal const int Advance = GlyphW + 1;

        static readonly Dictionary<char, string[]> Glyphs = new()
        {
            ['0'] = new[] { ".###.", "#...#", "#..##", "#.#.#", "##..#", "#...#", ".###." },
            ['1'] = new[] { "..#..", ".##..", "..#..", "..#..", "..#..", "..#..", ".###." },
            ['2'] = new[] { ".###.", "#...#", "....#", "...#.", "..#..", ".#...", "#####" },
            ['3'] = new[] { "#####", "...#.", "..#..", "...#.", "....#", "#...#", ".###." },
            ['4'] = new[] { "...#.", "..##.", ".#.#.", "#..#.", "#####", "...#.", "...#." },
            ['5'] = new[] { "#####", "#....", "####.", "....#", "....#", "#...#", ".###." },
            ['6'] = new[] { "..##.", ".#...", "#....", "####.", "#...#", "#...#", ".###." },
            ['7'] = new[] { "#####", "....#", "...#.", "..#..", ".#...", ".#...", ".#..." },
            ['8'] = new[] { ".###.", "#...#", "#...#", ".###.", "#...#", "#...#", ".###." },
            ['9'] = new[] { ".###.", "#...#", "#...#", ".####", "....#", "...#.", ".##.." },
            ['A'] = new[] { ".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#" },
            ['B'] = new[] { "####.", "#...#", "#...#", "####.", "#...#", "#...#", "####." },
            ['C'] = new[] { ".###.", "#...#", "#....", "#....", "#....", "#...#", ".###." },
            ['D'] = new[] { "###..", "#..#.", "#...#", "#...#", "#...#", "#..#.", "###.." },
            ['E'] = new[] { "#####", "#....", "#....", "####.", "#....", "#....", "#####" },
            ['F'] = new[] { "#####", "#....", "#....", "####.", "#....", "#....", "#...." },
            ['G'] = new[] { ".###.", "#...#", "#....", "#.###", "#...#", "#...#", ".####" },
            ['H'] = new[] { "#...#", "#...#", "#...#", "#####", "#...#", "#...#", "#...#" },
            ['I'] = new[] { ".###.", "..#..", "..#..", "..#..", "..#..", "..#..", ".###." },
            ['J'] = new[] { "..###", "...#.", "...#.", "...#.", "...#.", "#..#.", ".##.." },
            ['K'] = new[] { "#...#", "#..#.", "#.#..", "##...", "#.#..", "#..#.", "#...#" },
            ['L'] = new[] { "#....", "#....", "#....", "#....", "#....", "#....", "#####" },
            ['M'] = new[] { "#...#", "##.##", "#.#.#", "#.#.#", "#...#", "#...#", "#...#" },
            ['N'] = new[] { "#...#", "#...#", "##..#", "#.#.#", "#..##", "#...#", "#...#" },
            ['O'] = new[] { ".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###." },
            ['P'] = new[] { "####.", "#...#", "#...#", "####.", "#....", "#....", "#...." },
            ['Q'] = new[] { ".###.", "#...#", "#...#", "#...#", "#.#.#", "#..#.", ".##.#" },
            ['R'] = new[] { "####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#" },
            ['S'] = new[] { ".####", "#....", "#....", ".###.", "....#", "....#", "####." },
            ['T'] = new[] { "#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#.." },
            ['U'] = new[] { "#...#", "#...#", "#...#", "#...#", "#...#", "#...#", ".###." },
            ['V'] = new[] { "#...#", "#...#", "#...#", "#...#", "#...#", ".#.#.", "..#.." },
            ['W'] = new[] { "#...#", "#...#", "#...#", "#.#.#", "#.#.#", "##.##", "#...#" },
            ['X'] = new[] { "#...#", "#...#", ".#.#.", "..#..", ".#.#.", "#...#", "#...#" },
            ['Y'] = new[] { "#...#", "#...#", ".#.#.", "..#..", "..#..", "..#..", "..#.." },
            ['Z'] = new[] { "#####", "....#", "...#.", "..#..", ".#...", "#....", "#####" },
            [' '] = new[] { ".....", ".....", ".....", ".....", ".....", ".....", "....." },
            ['.'] = new[] { ".....", ".....", ".....", ".....", ".....", ".##..", ".##.." },
            ['-'] = new[] { ".....", ".....", ".....", "#####", ".....", ".....", "....." },
            ['/'] = new[] { "....#", "....#", "...#.", "..#..", ".#...", "#....", "#...." },
            [':'] = new[] { ".....", ".##..", ".##..", ".....", ".##..", ".##..", "....." },
            ['%'] = new[] { "##..#", "##..#", "...#.", "..#..", ".#...", "#..##", "#..##" },
            ['('] = new[] { "...#.", "..#..", ".#...", ".#...", ".#...", "..#..", "...#." },
            [')'] = new[] { ".#...", "..#..", "...#.", "...#.", "...#.", "..#..", ".#..." },
            ['+'] = new[] { ".....", "..#..", "..#..", "#####", "..#..", "..#..", "....." },
            [','] = new[] { ".....", ".....", ".....", ".....", ".##..", "..#..", ".#..." },
        };

        /// <summary>The glyph table's key for a character: its upper-case form.</summary>
        /// <param name="c">The character.</param>
        /// <returns>The upper-case character.</returns>
        static char Key(char c) => char.ToUpperInvariant(c);

        /// <summary>
        /// True when the character has a glyph (lower-case letters through their capital).
        /// </summary>
        /// <param name="c">The character.</param>
        /// <returns>True when it is drawn, false when it would stay blank.</returns>
        internal static bool Has(char c) => Glyphs.ContainsKey(Key(c));

        /// <summary>
        /// The text's width in pixels, at the given scale; without the blank column after the
        /// last letter.
        /// </summary>
        /// <param name="text">The text; every character counts, drawn or not.</param>
        /// <param name="scale">Screen pixels per glyph pixel, 1 or more.</param>
        /// <returns>The width in pixels; 0 for an empty text.</returns>
        internal static int Width(string text, int scale)
            => text.Length == 0 ? 0 : (text.Length * Advance - 1) * scale;

        /// <summary>The height of a line of text, in pixels.</summary>
        /// <param name="scale">Screen pixels per glyph pixel, 1 or more.</param>
        /// <returns>The height in pixels.</returns>
        internal static int Height(int scale) => GlyphH * scale;

        /// <summary>
        /// Draws the text into the RGB raster, with the top left corner at (x, y). What falls
        /// outside the raster is clipped; nothing throws.
        /// </summary>
        /// <param name="rgb">The raster, w*h*3 bytes, interleaved R,G,B.</param>
        /// <param name="w">The raster's width in pixels.</param>
        /// <param name="h">The raster's height in pixels.</param>
        /// <param name="x">The left edge of the first letter, in pixels.</param>
        /// <param name="y">The top edge of the text, in pixels.</param>
        /// <param name="text">The text.</param>
        /// <param name="scale">Screen pixels per glyph pixel, 1 or more.</param>
        /// <param name="r">The text's red, 0..255.</param>
        /// <param name="g">The text's green, 0..255.</param>
        /// <param name="b">The text's blue, 0..255.</param>
        internal static void Draw(byte[] rgb, int w, int h, int x, int y, string text, int scale, byte r, byte g, byte b)
        {
            int cx = x;
            foreach (char c in text)
            {
                if (Glyphs.TryGetValue(Key(c), out string[] rows))
                    for (int gy = 0; gy < GlyphH; gy++)
                        for (int gx = 0; gx < GlyphW; gx++)
                            if (rows[gy][gx] == '#')
                                Block(rgb, w, h, cx + gx * scale, y + gy * scale, scale, r, g, b);
                cx += Advance * scale;
            }
        }

        /// <summary>Fills a square of the raster with one colour, clipped to the raster.</summary>
        /// <param name="rgb">The raster, w*h*3 bytes, interleaved R,G,B.</param>
        /// <param name="w">The raster's width in pixels.</param>
        /// <param name="h">The raster's height in pixels.</param>
        /// <param name="x0">The square's left edge, in pixels.</param>
        /// <param name="y0">The square's top edge, in pixels.</param>
        /// <param name="size">The square's side, in pixels.</param>
        /// <param name="r">Red, 0..255.</param>
        /// <param name="g">Green, 0..255.</param>
        /// <param name="b">Blue, 0..255.</param>
        static void Block(byte[] rgb, int w, int h, int x0, int y0, int size, byte r, byte g, byte b)
        {
            for (int y = y0; y < y0 + size; y++)
            {
                if (y < 0 || y >= h) continue;
                for (int x = x0; x < x0 + size; x++)
                {
                    if (x < 0 || x >= w) continue;
                    int p = (y * w + x) * 3;
                    rgb[p] = r; rgb[p + 1] = g; rgb[p + 2] = b;
                }
            }
        }
    }
}
