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

// StructureRun.cs - modul 1 cap-coada: fisier -> cifre + text + harta.

using System.IO;

namespace FlatCheck
{
    internal static class StructureRun
    {
        /// <param name="report">
        /// Unde se scrie raportul. Consola da stdout, fereastra da un StringWriter din
        /// care apoi salveaza "Rezultat testare.txt" - acelasi text, prin constructie.
        /// </param>
        internal static RunOutcome<StructureResult> Execute(string path, TextWriter report)
        {
            var r = StructureAnalyzer.Analyze(ImageLoader.LoadImage(path));
            StructureReport.Write(r, path, report);
            return new RunOutcome<StructureResult>(r, PngWriter.EncodeResidualMap(r.Residual));
        }
    }
}
