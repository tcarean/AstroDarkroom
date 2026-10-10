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

namespace LightsCheck.Wpf.Tests;

/// <summary>
/// Points the window's settings file at a file of this test run's own before any test runs,
/// so no view-model built by a test reads or writes the developer's
/// <c>%LOCALAPPDATA%\LightsCheck\settings.json</c> (R-041); every test then starts from the
/// defaults unless it sets <see cref="UserSettings.Path"/> itself.
/// </summary>
internal static class TestSettings
{
    [ModuleInitializer]
    internal static void Isolate()
        => UserSettings.Path = Path.Combine(Path.GetTempPath(), "lightscheck-tests", $"settings-{Environment.ProcessId}.json");
}
