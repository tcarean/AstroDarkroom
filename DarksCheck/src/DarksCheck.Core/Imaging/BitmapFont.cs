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

#nullable disable // copied from FlatCheck 2.3.0 unchanged, which compiles without nullable annotations (ADR 0003)

// BitmapFont.cs - un font bitmap 5x7, de mana, pentru etichetele din PNG-ul de split.
//
// Scriitorul PNG e scris de mana (ADR 0008) si nu deseneaza text; o eticheta ca
// "PARTEA 1 DIN 4" sau titlul cu versiunea si data nu justifica un pachet de fonturi
// (ADR 0054). Cifre, majuscule si cateva semne; literele mici se deseneaza ca majuscule,
// orice altceva ramane gol dar avanseaza, ca sirul sa nu se stranga.

namespace DarksCheck.Core
{
    internal static class BitmapFont
    {
        internal const int GlyphW = 5;
        internal const int GlyphH = 7;
        /// <summary>Avansul de la o litera la urmatoarea, in pixeli de glifa: latimea plus o coloana goala.</summary>
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

        static char Key(char c) => char.ToUpperInvariant(c);

        /// <summary>Adevarat cand caracterul are glifa (literele mici prin majuscula lor).</summary>
        internal static bool Has(char c) => Glyphs.ContainsKey(Key(c));

        /// <summary>Latimea textului in pixeli, la scara data; fara coloana goala dupa ultima litera.</summary>
        internal static int Width(string text, int scale)
            => text.Length == 0 ? 0 : (text.Length * Advance - 1) * scale;

        internal static int Height(int scale) => GlyphH * scale;

        /// <summary>
        /// Deseneaza textul in rasterul RGB, cu coltul din stanga-sus la (x, y). Ce iese
        /// din raster se taie; nimic nu arunca.
        /// </summary>
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
