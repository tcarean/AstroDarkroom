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
        // Scara de reziduu, folosita de toate patru metricile de abatere de la simetria
        // radiala: imprastierea colturilor, asimetria azimutala, reziduul dupa model si
        // inclinarea. Toate patru masoara aceeasi abatere din unghiuri diferite, si toate
        // patru sunt amplitudini fractionare in procente, deci impart scara:
        //     sub  2 %   calitate perfecta
        //      2 - 10 %  utilizabil; background extraction scoate ce ramane
        //     10 - 20 %  utilizabil, dar confirma cu --light inainte de un stretch dur
        //     peste 20 % refa flat-urile
        public const double ResidPerfect = 2.0;
        public const double ResidUsable = 10.0;
        public const double ResidCheck = 20.0;

        // Pragul de la care "pete locale" (Metrics.LocalSpots) tipareste atentionarea.
        // Nu e un verdict: o pata compacta fixa pe aparat - praf, condens, o scurgere
        // mica - e corecta pentru sesiunea ale carei lights o au si ele, si gresita
        // pentru un flat de biblioteca. Raspunsul depinde de ce faci cu flat-ul, nu de
        // flat, deci se atentioneaza, nu se voteaza. Acelasi 2% ca banda PERFECT: sub
        // atat nu conteaza nici forma, nici ce e fix pe aparat.
        public const double LocalSpotWarn = ResidPerfect;

        // Offsetul centrului de iluminare nu mai are praguri, fiindca nu mai e un test.
        // Pana in 1.2.0 avea 4%/10% pe scara OK/SUSPECT/PROBLEMA, si era gresit: offsetul
        // e ~ -b/(2d), adica raportul dintre gradient si curbura, deci NU e o fractie si
        // nu e scale-invariant. Pe un obiectiv cu vignetare mica exploda (masurat: 31% pe
        // un Samyang 135 f/2.8 la 0.15 stopuri, unde eroarea reala era de 11%), iar cand
        // varful ajungea sa iasa din cadru cadea la 0.00% = "OK" - adica devenea si
        // nemonoton, dand un fals negativ exact pe obiectivele cele mai plate. Ce se
        // masoara acum e Th.Resid* pe "inclinare": acelasi gradient, luat ca amplitudine.

        // --- netezire ---
        // Pragul de la care raportul de netezire atentioneaza ca s-a scos mai mult decat
        // textura: cea mai abatuta dala a componentei scoase (original / netezit), pe
        // rasterul de analiza. Nu e un verdict - fisierul se scrie oricum - ci semnalul
        // ca netezirea a luat si ceva de scara mare, pe care omul trebuie sa-l vada in
        // harta. Masurat la scara implicita de 300 px pe cinci mastere reale: 0.40-0.71 %
        // pe cele patru curate, 4.2 % pe cel cu textura de tesatura - deci acelasi 2 % ca
        // banda PERFECT lasa o marja de 3x in ambele directii.
        public const double SmoothKeptWarn = ResidPerfect;

        // Propunerea de scara (SmoothScaleAdvisor), pe Metrics.FineTexture - partea
        // structurata a componentei fine sub ~128 px pe senzor, in procente, pe rasterul
        // de analiza. Masurat in C# pe cinci mastere reale (2026-09-16):
        //   intrare: 0.04-0.06 % pe cele patru fara textura, 0.69 % pe cel cu difuzorul pe
        //   monitor (celule de ~300 px). Pragul de 0.20 lasa 3x in ambele directii.
        //   dupa netezire, pe cel cu textura: 0.09 % la 150 px, 0.02 % la 300, 0.01 % la
        //   600; pe cele curate 0.01-0.02 % la orice scara. Pragul de 0.05 spune "a plecat"
        //   de la 300 in sus, cu 1.8x fata de 150 si 2.5x fata de ce ramane la 300.
        // Cinci mastere ajung ca sa se vada ca ideea tine, nu ca sa fie calibrat: un master
        // cu textura slaba, de 0.15-0.25 %, cade langa primul prag.
        public const double SmoothTextureMin = 0.20;    // sub atat, nimic de netezit
        public const double SmoothTextureGone = 0.05;   // sub atat, textura a plecat

        // Garda "scara prea mare": cat au voie sa SCADA asimetria azimutala si inclinarea,
        // in puncte, intre master si fisierul netezit. Sunt cele doua teste pe care textura
        // nu le atinge (sectoare mari, respectiv un plan), deci o scadere e structura reala
        // care pleaca. Masurat: la 300 px pe masterul cu textura se misca 0.00 si -0.01;
        // la 600 px inelul real al unui Nikkor 35 DX scade asimetria cu 0.79 si petele
        // monitorului pe Tokina cu 0.80. Colturile, reziduul si pata maxima NU pot fi
        // garda: scad cand pleaca textura (1.47 puncte la pata maxima).
        public const double SmoothScaleLoss = 0.5;

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

        // --- profilul de vignetare fata de ce s-a cerut (sintetic, structura cu --raw) ---
        // Profilul se alege "cel mai apropiat", fara interpolare; peste pragurile astea
        // raportul avertizeaza ca adancimea e a profilului, nu a valorilor cerute. Doar un
        // avertisment: exit-ul nu se schimba. Focala: 1 mm sau 5 %, care e mai mare -
        // aceeasi toleranta cu care LensSpec compara numele unui obiectiv cu gama din raw
        // (numele rotunjesc). Diafragma: o jumatate de stop - grila Adobe e la stopuri
        // intregi, deci orice cerere din interiorul gamei cade sub prag, si avertismentul
        // prinde exact cererile din afara ei (f/1.0 pe un obiectiv cu profile de la f/1.8).
        public const double ProfileFocalTolMm = 1.0;
        public const double ProfileFocalTolFrac = 0.05;
        public const double ProfileStopsTol = 0.5;

        // Cat difera cele doua baze de profile intre ele, pe acelasi obiectiv, la aceeasi
        // diafragma, in coltul cadrului: 0.16-0.25 stopuri pe Tokina 11-20 si Nikkor 35 DX
        // (docs/design/lens-profiles.md, sectiunea 8). E incertitudinea REFERINTEI verdictului de
        // adancime, si motivul pentru care acel verdict sta pe scara de reziduu (10 % = BUN)
        // si nu pe cea a modului --light (5 % = PROBLEMA), desi masoara aceeasi marime:
        // 0.2 stopuri = 15 % in colt, de trei ori linia de PROBLEMA. Nu e un prag: raportul
        // il citeaza ca sa spuna cat de fin poate fi verdictul, nu ca sa decida ceva.
        public const double ProfileDatabaseSpreadStops = 0.2;

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
