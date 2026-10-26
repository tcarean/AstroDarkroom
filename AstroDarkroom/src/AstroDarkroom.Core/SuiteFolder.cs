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

namespace AstroDarkroom.Core;

/// <summary>
/// Where an app is in the suite folder (R-002).
/// </summary>
/// <param name="Path">The full path of the app's window executable, whether or not it exists.</param>
/// <param name="IsAvailable">True when the executable exists there.</param>
/// <param name="Reason">Why the app cannot start, in the current language ("DarksCheck.Wpf.exe is missing"); null when it is available.</param>
public sealed record AppLocation(string Path, bool IsAvailable, string? Reason);

/// <summary>
/// The suite folder: the flat folder the release publishes, with every component's executables
/// beside the launcher's own. The launcher constructs it with its base directory; the tests
/// with a temp folder holding empty files named like the executables.
/// </summary>
/// <param name="baseDirectory">The folder the executables are looked up in; it need not exist.</param>
public sealed class SuiteFolder(string baseDirectory)
{
    /// <summary>The folder the executables are looked up in, as given.</summary>
    public string BaseDirectory { get; } = baseDirectory;

    /// <summary>The release.json the release writes beside the executables, whether or not it exists.</summary>
    public string ManifestPath => Path.Combine(BaseDirectory, "release.json");

    /// <summary>
    /// Says whether an app's window executable is in the folder (R-002). Only the window counts:
    /// the launcher starts windows, and a component without a console still has one.
    /// </summary>
    /// <param name="app">The app to look up; not null.</param>
    /// <returns>The path of the window executable and whether it exists, with the reason when it does not.</returns>
    public AppLocation Locate(SuiteApp app)
    {
        ArgumentNullException.ThrowIfNull(app);
        string path = Path.Combine(BaseDirectory, app.WindowExe);
        return File.Exists(path)
            ? new AppLocation(path, true, null)
            : new AppLocation(path, false, Ui.T(app.WindowExe + " is missing", "Lipseste " + app.WindowExe));
    }
}
