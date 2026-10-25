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

namespace FlatCheck.Wpf.Tests;

/// <summary>
/// The language of this test assembly: Romanian, set once before any test runs. The tests
/// written before the English interface (ADR 0062) assert the Romanian wordings, and keep
/// doing so unchanged; a test of English opens its own scope (<see cref="Ui.Use"/>, or
/// <c>--lang en</c> on the console), which cannot leak into the tests running beside it.
/// </summary>
static class RomanianByDefault
{
    [ModuleInitializer]
    internal static void Set() => Ui.Default = UiLanguage.Romanian;
}
