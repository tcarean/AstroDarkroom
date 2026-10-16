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

using System.Runtime.CompilerServices;

namespace DarksCheck.Wpf.Tests;

/// <summary>
/// Points the window's settings file at a file of this test run's own before any test runs,
/// so no view-model built by a test reads or writes the developer's
/// <c>%LOCALAPPDATA%\DarksCheck\settings.json</c> (R-075); every test then starts from the
/// defaults unless it sets <see cref="UserSettings.Path"/> itself.
/// </summary>
internal static class TestSettings
{
    [ModuleInitializer]
    internal static void Isolate()
    {
        UserSettings.Path = Path.Combine(Path.GetTempPath(), "darkscheck-tests", "settings-shared.json");
        try
        {
            File.Delete(UserSettings.Path);
        }
        catch (IOException)
        {
        }
    }

    /// <summary>A fresh settings file for one test, so what it stores is seen by nothing else; restored on dispose.</summary>
    internal sealed class Own : IDisposable
    {
        private readonly string? _before = UserSettings.Path;
        private readonly string _file;

        internal Own()
        {
            _file = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "darkscheck-tests", $"settings-{Guid.NewGuid():N}.json");
            UserSettings.Path = _file;
        }

        internal string FilePath => _file;

        public void Dispose()
        {
            try
            {
                File.Delete(_file);
            }
            catch (IOException)
            {
            }

            UserSettings.Path = _before;
        }
    }
}
