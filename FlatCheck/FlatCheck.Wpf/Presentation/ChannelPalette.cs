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

// ChannelPalette.cs - culoarea unui canal CFA pe histograma.
//
// DE CE NU E IN VerdictPalette. Astea NU sunt verdicte si nu trebuie sa treaca niciodata
// prin Stats.Severity: un canal rosu nu e "mai rau" decat unul verde. Doua sisteme de
// culoare pe acelasi ecran e riscant tocmai fiindca seamana, deci merita spus: verdictul
// se vede pe o singura pastila, sus; restul culorilor de pe tab identifica un canal.
//
// DE CE NU E IN Dark.xaml. Alegerea se face dintr-un char care vine din rezultat, adica
// din C#, nu dintr-o legare - iar un dictionar de resurse n-ar putea decide pe el.
//
// Cand fisierul n-a spus ordinea, toate patru canalele primesc culoarea de accent. E
// deliberat: patru curbe colorate R/G/G/B pe un mozaic necunoscut ar arata exact ca o
// masuratoare, si n-ar fi.

using System.Windows.Media;

namespace FlatCheck.Wpf
{
    internal static class ChannelPalette
    {
        internal static readonly Color R = Color.FromRgb(0xE0, 0x6C, 0x75);
        internal static readonly Color G = Color.FromRgb(0x7D, 0xC3, 0x83);
        internal static readonly Color B = Color.FromRgb(0x61, 0xAF, 0xEF);

        /// <summary>Aceeasi cu AccentBr din tema: "nu stiu ce culoare e" arata neutru.</summary>
        internal static readonly Color Unknown = Color.FromRgb(0x4E, 0xA8, 0xDE);

        internal static Color For(char colour) => colour switch
        {
            'R' => R,
            'G' => G,
            'B' => B,
            _ => Unknown,
        };

        internal static Brush BrushFor(char colour)
        {
            var b = new SolidColorBrush(For(colour));
            b.Freeze();   // se leaga din mai multe fire, si inghetat e si mai ieftin de desenat
            return b;
        }
    }
}
