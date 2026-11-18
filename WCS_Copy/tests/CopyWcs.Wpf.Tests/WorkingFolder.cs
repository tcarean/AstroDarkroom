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

namespace CopyWcs.Wpf.Tests;

/// <summary>
/// The process's working folder moved for one test and put back at Dispose (R-051). The folder
/// is the whole process's, so this is safe only because the assembly runs one test at a time
/// (<c>AssemblyInfo</c>).
/// </summary>
internal sealed class WorkingFolder : IDisposable
{
    private readonly string _before = Environment.CurrentDirectory;

    /// <summary>Moves the working folder to a folder, until Dispose.</summary>
    /// <param name="folder">An existing folder.</param>
    internal WorkingFolder(string folder) => Environment.CurrentDirectory = folder;

    /// <summary>Moves it again, as a file dialog, or anything else in the process, might.</summary>
    /// <param name="folder">An existing folder.</param>
    internal static void MoveTo(string folder) => Environment.CurrentDirectory = folder;

    public void Dispose() => Environment.CurrentDirectory = _before;
}
