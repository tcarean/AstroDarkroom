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

// ExposureBands.cs - benzile verdictului de expunere, ca valoare.
//
// DE CE EXISTA. Pana acum Classify alegea inline intre sase constante din Th, dupa
// steagul --sky. Cand a aparut histograma din fereastra, care deseneaza aceleasi benzi
// peste curbe, alegerea aia ar fi trebuit facuta a doua oara - si o a doua alegere intre
// aceleasi constante ar fi fost o a doua definitie a verdictului, care se poate desincroniza
// tacut. Asa, banda verde de pe desen E prin constructie banda care voteaza.
//
// Pe cer nu exista banda "acceptabil, dar cam jos": tinta incepe direct de la pragul de
// prea-jos, deci TooLow == Lo. Nu e o scapare, e chiar forma scarii - un sky flat TREBUIE
// sa stea modest, iar "prea jos" si "sub tinta" sunt acolo acelasi lucru.

namespace FlatCheck
{
    /// <summary>
    /// Pragurile pe care se citeste mediana ferestrei centrale, in procente din
    /// (white - black). Aceeasi scara pe care sunt toate cifrele modului.
    /// </summary>
    internal readonly struct ExposureBands
    {
        /// <summary>Praguri pentru flat pe cer, nu pe panou.</summary>
        internal readonly bool Sky;

        /// <summary>Sub asta, PREA JOS.</summary>
        internal readonly double TooLow;

        /// <summary>Banda tinta, capete inclusive la dreapta (<c>cen &lt;= Hi</c>).</summary>
        internal readonly double Lo, Hi;

        /// <summary>Peste asta, PREA SUS.</summary>
        internal readonly double TooHigh;

        /// <summary>Mijlocul tintei, pentru sugestia in stopuri.</summary>
        internal readonly double Target;

        ExposureBands(bool sky, double tooLow, double lo, double hi, double tooHigh, double target)
        {
            Sky = sky; TooLow = tooLow; Lo = lo; Hi = hi; TooHigh = tooHigh; Target = target;
        }

        internal static ExposureBands For(bool sky) => sky
            ? new ExposureBands(true, Th.ExpoSkyLo, Th.ExpoSkyLo, Th.ExpoSkyHi, Th.ExpoSkyTooHigh, Th.ExpoSkyTarget)
            : new ExposureBands(false, Th.ExpoTooLow, Th.ExpoLo, Th.ExpoHi, Th.ExpoTooHigh, Th.ExpoTarget);

        /// <summary>
        /// true cand scara are o banda "acceptabil, dar cam jos" intre prea-jos si tinta.
        /// Pe cer nu are, si desenul nu trebuie sa inventeze una lata de zero.
        /// </summary>
        internal bool HasLowShoulder => Lo > TooLow;
    }
}
