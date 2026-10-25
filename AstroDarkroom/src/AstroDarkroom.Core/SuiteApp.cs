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
/// One application of the suite, as the catalogue describes it (R-001). The suite ships the
/// windows only; a component's console stays in that component's own release (ADR 0002).
/// </summary>
/// <param name="Folder">The component's project folder in the repository, which is also its name in release.json ("DarksCheck").</param>
/// <param name="Title">The title the launcher shows, Romanian without diacritics.</param>
/// <param name="Description">One line, Romanian without diacritics, saying what the app checks.</param>
/// <param name="WindowExe">The file name of the window executable in the suite folder ("DarksCheck.Wpf.exe").</param>
public sealed record SuiteApp(string Folder, string Title, string Description, string WindowExe);
