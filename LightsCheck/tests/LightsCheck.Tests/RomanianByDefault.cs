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
using LightsCheck.Core;

namespace LightsCheck.Tests;

/// <summary>
/// Sets the language outside any scope to Romanian before any test runs (ADR 0022), so every
/// test written while the interface was Romanian only keeps asserting the Romanian wording
/// unchanged and needs no <c>--lang</c>. The programs start in English (<see cref="Ui.StartupDefault"/>);
/// a test that wants English opens <see cref="Ui.Use"/> or passes <c>--lang en</c>. Linked into
/// the window's test project too.
/// </summary>
internal static class RomanianByDefault
{
    [ModuleInitializer]
    internal static void Set() => Ui.Default = UiLanguage.Romanian;
}
