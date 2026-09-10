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


using System;

namespace FlatCheck
{
    static class Program
    {
        internal static int Main(string[] args)
            => FlatCheckCli.Run(args, Console.Out, Console.Error);
    }
}
