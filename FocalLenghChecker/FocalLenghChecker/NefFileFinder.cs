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

namespace FocalLenghChecker;

public interface INefFileFinder
{
    /// <summary>Finds every .NEF file beneath <paramref name="folder"/>, recursively.</summary>
    IEnumerable<string> Find(string folder);
}

public sealed class NefFileFinder : INefFileFinder
{
    public const string NefExtension = ".nef";

    public IEnumerable<string> Find(string folder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            MatchCasing = MatchCasing.CaseInsensitive,
        };

        // Enumerated with "*" and filtered by extension rather than matched with
        // "*.nef", because on Windows the latter also matches 8.3 short names and
        // longer extensions such as ".nef_backup".
        return Directory.EnumerateFiles(folder, "*", options)
            .Where(IsNef)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);
    }

    public static bool IsNef(string path) =>
        string.Equals(Path.GetExtension(path), NefExtension, StringComparison.OrdinalIgnoreCase);
}
