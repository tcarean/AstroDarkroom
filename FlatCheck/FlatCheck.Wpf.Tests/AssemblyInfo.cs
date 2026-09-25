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

// AssemblyInfo.cs - testele din assembly-ul asta nu ruleaza in paralel.
//
// DE CE. Aproape tot ce se testeaza aici atinge stare GLOBALA de WPF, nu doar obiecte
// proprii: Application.Current, cultura firelor, si un dispecer care apartine unui singur
// fir STA. Colectiile rulate in paralel isi calca in picioare exact aceste lucruri.
//
// S-a si intamplat, si a fost greu de vazut fiindca pica doar cateodata: fixtura STA se
// inchidea la mijlocul unei serii rulate de alta colectie, Application.Current trecea din
// "exista" in "are dispecerul inchis", si randurile din tabel ajungeau parte pe firul de
// interfata, parte pe firul de calcul. ObservableCollection pierde randuri tacut cand i se
// intampla asta - sase cadre analizate, cinci randuri in tabel, o data la cateva rulari.
//
// Codul a fost intarit separat (LightTabViewModel.OnUi tine un singur fir pe toata
// durata unei rulari), dar asta nu face testele deterministe: ele tot ar constata sase
// versus cinci, doar dintr-un motiv mai curat. Determinismul vine de aici.
//
// Costul e zero in practica: suita intreaga ruleaza in vreo doua secunde.

[assembly: CollectionBehavior(DisableTestParallelization = true)]
