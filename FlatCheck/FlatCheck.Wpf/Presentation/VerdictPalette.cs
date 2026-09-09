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

// VerdictPalette.cs - ce culoare are un verdict.
//
// Culoarea se deriva din Stats.Severity, NU dintr-o lista proprie de siruri. Nucleul
// stie deja sa ordoneze verdictele din amandoua vocabularele (OK/SUSPECT/PROBLEMA si
// PERFECT/BUN/ACCEPTABIL/REFA), inclusiv regula "orice necunoscut e tratat ca cel mai
// rau". O a doua lista aici ar fi insemnat ca un verdict nou adaugat in nucleu apare in
// fereastra colorat gresit - adica verde - ceea ce e cea mai proasta directie posibila
// pentru o greseala din asta.
//
// Singura exceptie e NEDETERMINAT, care in Stats sta la severitate 2 fiindca acolo
// conteaza doar codul de iesire ("nu stiu" cere aceeasi reactie ca "suspect"). Vizual
// insa nu e o avertizare, e o absenta: se coloreaza neutru, ca sa nu para un rezultat
// prost cand de fapt nu e niciun rezultat.

using System.Windows.Media;

namespace FlatCheck.Wpf
{
    internal static class VerdictPalette
    {
        internal static readonly Color Perfect = Color.FromRgb(0x3F, 0xB9, 0x50);
        internal static readonly Color Good = Color.FromRgb(0x9C, 0xCC, 0x65);
        internal static readonly Color Warn = Color.FromRgb(0xE3, 0xB3, 0x41);
        internal static readonly Color Bad = Color.FromRgb(0xF8, 0x51, 0x49);
        internal static readonly Color Neutral = Color.FromRgb(0x8B, 0x94, 0x9E);

        internal static Color For(string verdict)
        {
            if (IsInconclusive(verdict)) return Neutral;

            return Stats.Severity(verdict) switch
            {
                0 => Perfect,
                1 => Good,
                2 => Warn,
                _ => Bad,
            };
        }

        internal static Brush BrushFor(string verdict)
        {
            var b = new SolidColorBrush(For(verdict));
            b.Freeze();   // se leaga din mai multe fire; inghetat e si mai ieftin de desenat
            return b;
        }

        /// <summary>
        /// "A rulat dar nu concluzioneaza" (serie neconcludenta) si "nu se aplica" (camp
        /// prea larg) nu sunt calitati proaste, sunt lipsa unui raspuns.
        /// </summary>
        internal static bool IsInconclusive(string verdict)
            => verdict == LightSeriesAnalyzer.Undetermined
            || verdict == Stats.NotApplicable;
    }
}
