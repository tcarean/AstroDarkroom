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

// RunOutcome.cs - ce intoarce o rulare completa a unui mod.
//
// DE CE EXISTA STRATUL Runs/. Pana la 2.0.0 lantul complet al unui mod - incarca
// fisierul, cheama analizorul, cheama raportul, encodeaza harta - traia in
// FlatCheckCli, adica in aplicatia de consola. Cand a aparut a doua aplicatie peste
// acelasi nucleu, lantul ala ar fi trebuit scris a doua oara, si de acolo pana la doua
// comportamente usor diferite era o singura scapare de intretinere.
//
// Deci lantul s-a mutat aici, iar consola si fereastra au ramas cu ce le deosebeste
// cu adevarat: consola scrie textul la stdout si harta pe disc si intoarce un cod de
// iesire; fereastra leaga rezultatul de niste controale si arata harta din memorie.
//
// DOUA DECIZII DE FORMA, amandoua ca sa nu se duplice nimic:
//
//   - Textul NU se intoarce ca sir, se scrie intr-un TextWriter primit. Asa consola isi
//     pastreaza streamingul (o serie de 32 de cadre isi scrie randurile pe masura ce
//     sunt gata, nu dupa doua minute de tacere), iar cine vrea sirul ii da un
//     StringWriter si-l citeste inapoi. Un camp "Report" ar fi fortat acumularea peste
//     tot, deci ar fi rupt exact purtarea buna a consolei.
//
//   - Harta se intoarce ca octeti PNG, nu se scrie pe disc. Modul nu stie unde vrea
//     apelantul fisierul, iar fereastra nu vrea deloc un fisier - vrea sa-l arate.
//     Scrierea si anuntul raman ale apelantului (vezi RunReport.WriteMapSaved).

namespace FlatCheck
{
    /// <summary>
    /// Rezultatul unui mod care produce mereu un raspuns: structura, raportul A/B,
    /// expunerea. Modurile astea nu pot refuza intrarea - cel mult dau un verdict prost,
    /// care e tot un raspuns.
    /// </summary>
    internal sealed class RunOutcome<TResult>
    {
        /// <summary>Record-ul analizorului, cu toate cifrele. Niciodata null aici.</summary>
        internal readonly TResult Result;

        /// <summary>
        /// Harta reziduala deja encodata PNG, sau null cand modul nu produce harta
        /// (--expo masoara un nivel, n-are ce desena).
        /// </summary>
        internal readonly byte[] Png;

        /// <summary>
        /// A doua harta, cand modul produce doua, altfel null. Numai modul de atribuire:
        /// el desparte masterul in doua componente, iar fiecare are alta forma de aratat -
        /// reziduul partii fixe pe aparat (care se potriveste cu verdictul) si raportul
        /// A/B (care arata forma gradientului extern). Fereastra le comuta; consola le
        /// scrie pe amandoua.
        /// </summary>
        internal readonly byte[] SecondaryPng;

        internal RunOutcome(TResult result, byte[] png, byte[] secondaryPng = null)
        {
            Result = result;
            Png = png;
            SecondaryPng = secondaryPng;
        }
    }
}
