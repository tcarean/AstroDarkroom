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

namespace CopyWcs.Tests;

/// <summary>Director temporar unic per test, sters recursiv la Dispose (xUnit ruleaza claselele in paralel).</summary>
internal sealed class TempWorkspace : IDisposable
{
    public string Dir { get; }

    public TempWorkspace()
    {
        Dir = Path.Combine(Path.GetTempPath(), "CopyWcsTests_" + Path.GetRandomFileName());
        Directory.CreateDirectory(Dir);
    }

    public string FilePath(string fileName) => Path.Combine(Dir, fileName);

    public string WriteFile(string fileName, byte[] content)
    {
        var path = FilePath(fileName);
        File.WriteAllBytes(path, content);
        return path;
    }

    public void Dispose()
    {
        try { Directory.Delete(Dir, recursive: true); } catch { /* best effort */ }
    }
}
