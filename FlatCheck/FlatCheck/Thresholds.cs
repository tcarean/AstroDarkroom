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

        // Garda de aplicabilitate, pe DIAGONALA campului acoperit, in grade.
        //
        // Testul separa fondul in "plan de poluare luminoasa" + "termeni radiali = eroare
        // de flat". Separarea tine doar cat timp cupola de poluare e cvasi-liniara peste
        // cadru. Pe un camp larg ea se CURBEAZA in interiorul cadrului, iar partea
        // radial simetrica a acelei curburi are exact forma vignetarii - deci fitul nu
        // mai are cum sa le deosebeasca, indiferent cat de bine e scris.
        //
        // Nu e un prag de calitate, e o limita de valabilitate: peste LightFovMax
        // modul refuza sa raspunda, nu da un raspuns prost.
        public const double LightFovOk = 20.0;      // sub asta, separarea e curata
        public const double LightFovMax = 40.0;     // peste asta, testul nu se aplica

        // Acelasi lucru cand headerul da doar FOCALLEN, fara marimea pixelului: nu se
        // poate calcula campul real, deci se presupune cel mai mare senzor plauzibil
        // (full-frame, diagonala 43.3 mm) si se compara focala direct. Conservator prin
        // constructie - pe un senzor mai mic campul e mai ingust decat se presupune aici,
        // deci un cadru bun poate iesi "marginal". Pentru cazul asta exista --fov.
        public const double LightFocalOk = 120.0;   // ~20.5 grade diagonala pe full-frame
        public const double LightFocalMin = 60.0;   // ~39.6 grade diagonala pe full-frame
        public const double FullFrameDiagMm = 43.27;

        // Gradient de cer (planul din fit) peste care se tipareste un avertisment. E doar
        // INFORMATIV, nu o poarta: panta e un proxy indirect - cauza reala a nesepararii e
        // curbura, iar pe aia o acopera garda de camp de mai sus.
        public const double LightPlaneWarn = 40.0;

        // --- end-to-end pe o serie de cadre ---
        // Media pe serie e o masuratoare mai buna decat orice cadru singur, dar numai daca
        // termenul radial al CERULUI se mediaza la zero peste sesiune. Cele trei conditii
        // de mai jos verifica exact asta; daca vreuna pica, verdictul e NEDETERMINAT.
        //
        // Pragurile astea sunt cele mai putin calibrate din fisier: vin dintr-o singura
        // sesiune reala (2026-08-12, care le pica pe amandoua). Recalibreaza-le cand ai
        // o sesiune despre care esti sigur.
        public const int LightSeriesMinFrames = 5;      // sub asta o medie nu inseamna nimic

        // Eroarea mediei (std/sqrt(n)). Daca depaseste chiar pragul de OK, seria nu
        // rezolva flat-ul la precizia la care se pronunta pragurile - deci nu se pronunta.
        public const double LightSeriesSemMax = LightRadOk;

        // Corelatia Pearson intre componenta radiala si gradientul de cer, peste serie.
        // Flat-ul e acelasi pe toate cadrele prin constructie: daca "eroarea de flat"
        // urmareste cerul, ce se masoara e cerul.
        public const double LightSeriesSkyCorr = 0.70;

        // ...dar corelatia are sens doar daca gradientul chiar variaza peste serie. Pe o
        // sesiune curata unde sta constant, corelatia e zgomot si ar respinge date bune,
        // deci testul de corelatie se aplica numai peste intervalul asta (puncte de %).
        public const double LightSeriesSkySpanMin = 10.0;

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
