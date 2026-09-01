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

// Thresholds.cs - toate pragurile de verdict, in procente.
//
// Sunt reguli empirice, nu constante fizice: un punct de plecare, de calibrat pe un
// setup despre care esti sigur. Din faza 8 sunt TOATE aici - inainte, --light avea
// 2.0/5.0 inline si --expo benzile ca locale, ceea ce facea imposibil de vazut
// intr-un singur loc dupa ce se ia o decizie.

namespace FlatCheck
{
    static class Th
    {
        // --- structura ---
        // Scara de reziduu, folosita de toate trei metricile de abatere de la simetria
        // radiala: imprastierea colturilor, asimetria azimutala si reziduul dupa model.
        // Toate trei masoara aceeasi abatere din unghiuri diferite, deci impart scara:
        //     sub  2 %   calitate perfecta
        //      2 - 10 %  utilizabil; background extraction scoate ce ramane
        //     10 - 20 %  utilizabil, dar confirma cu --light inainte de un stretch dur
        //     peste 20 % refa flat-urile
        public const double ResidPerfect = 2.0;
        public const double ResidUsable = 10.0;
        public const double ResidCheck = 20.0;

        // Offsetul centrului de iluminare NU e o metrica de reziduu - masoara o pozitie,
        // nu o amplitudine - deci ramane pe scara OK/SUSPECT/PROBLEMA.
        public const double CentroidOk = 4.0, CentroidBad = 10.0;

        // --- rotatie 180 ---
        public const double RatioOk = 1.0, RatioBad = 3.0;

        // --- end-to-end ---
        public const double LightRadOk = 2.0, LightRadBad = 5.0;

        // --- expunere, pe fereastra centrala 20% a canalului celui mai luminos ---
        public const double ExpoClip = 0.05;        // % pixeli saturati = clipping
        public const double ExpoTooLow = 30.0;      // sub asta masterul iese zgomotos
        public const double ExpoLo = 50.0;          // tinta pt panou
        public const double ExpoHi = 65.0;
        public const double ExpoTooHigh = 80.0;
        public const double ExpoTarget = 57.0;      // mijlocul tintei, pt sugestia in stopuri

        // --- expunere pe cer (blue hour) ---
        public const double ExpoSkyLo = 15.0, ExpoSkyHi = 50.0;
        public const double ExpoSkyTooHigh = 60.0;
        public const double ExpoSkyTarget = 30.0;
    }
}
