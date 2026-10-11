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

namespace DarksCheck.Tests;

/// <summary>A throwaway directory for tests that need real files on disk.</summary>
internal sealed class TempWorkspace : IDisposable
{
    internal string Dir { get; }

    internal TempWorkspace()
    {
        Dir = Path.Combine(Path.GetTempPath(), "darkscheck-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Dir);
    }

    /// <summary>Writes bytes into the workspace and returns the full path.</summary>
    internal string WriteFile(string name, byte[] bytes)
    {
        string p = Path.Combine(Dir, name);
        File.WriteAllBytes(p, bytes);
        return p;
    }

    /// <summary>A path inside the workspace, for output files that do not exist yet.</summary>
    internal string PathTo(string name) => Path.Combine(Dir, name);

    public void Dispose()
    {
        try { Directory.Delete(Dir, recursive: true); }
        catch (IOException) { /* a leaked temp dir must never fail a test */ }
        catch (UnauthorizedAccessException) { }
    }
}
