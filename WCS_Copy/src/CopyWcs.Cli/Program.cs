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

namespace CopyWcs;

/// <summary>
/// Copiaza metadatele WCS (plate solving) si datele optice dintr-un FITS in altul.
/// Fara dependinte externe: headerul FITS e ASCII simplu, carduri de 80 de bytes
/// grupate in blocuri de 2880.
/// </summary>
internal static class Program
{
    private static int Main(string[] args) => CopyWcsCli.Run(args, Console.Out, Console.Error);
}
