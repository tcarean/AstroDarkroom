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

namespace AstroPhotoAnalyzer.Tests;

/// <summary>Un folder temporar de aruncat, pentru testele care au nevoie de fisiere reale pe disc.</summary>
internal sealed class TempWorkspace : IDisposable
{
    internal string Dir { get; }

    internal TempWorkspace()
    {
        Dir = Path.Combine(Path.GetTempPath(), "astrophotoanalyzer-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Dir);
    }

    /// <summary>Scrie octetii in folder si intoarce calea completa.</summary>
    internal string WriteFile(string name, byte[] bytes)
    {
        string p = Path.Combine(Dir, name);
        File.WriteAllBytes(p, bytes);
        return p;
    }

    /// <summary>O cale din folder, pentru fisiere de iesire care nu exista inca.</summary>
    internal string PathTo(string name) => Path.Combine(Dir, name);

    public void Dispose()
    {
        try { Directory.Delete(Dir, recursive: true); }
        catch (IOException) { /* un folder temporar scapat nu trebuie sa pice un test */ }
        catch (UnauthorizedAccessException) { }
    }
}
