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

// Program.cs - punctul de intrare. Main e un shim de o linie peste FlatCheckCli.Run,
// care e si locul unde se poate testa tot, fiindca primeste scriitorii de iesire.
//
// DOCUMENTATIA DE UTILIZATOR E IN README.md, si numai acolo. Cele patru moduri, cand se
// poate si cand NU se poate folosi --light, scara de reziduu, pragurile, codurile de
// iesire, formatele acceptate si capcanele de workflow Siril - toate sunt explicate
// acolo. Nu le duplica aici: fisierul asta a purtdwat odata o copie a lor, cele doua au
// divergat, si copia de aici era cea gresita - mai zicea "trei moduri" si "C# nu citeste
// NEF" mult dupa ce nu mai era adevarat niciunul.

using System;

namespace FlatCheck
{
    static class Program
    {
        internal static int Main(string[] args)
            => FlatCheckCli.Run(args, Console.Out, Console.Error);
    }
}
