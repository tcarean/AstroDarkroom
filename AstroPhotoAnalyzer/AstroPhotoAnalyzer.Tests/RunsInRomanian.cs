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

namespace AstroPhotoAnalyzer.Tests;

/// <summary>
/// The tests run in Romanian unless they choose otherwise (ADR 0048): the tests written before
/// the English interface assert the Romanian texts, and the owner asked that they keep doing so.
/// A console run is the exception, since it sets its own language from --lang, English when
/// missing; the tests that assert its Romanian output pass "--lang ro".
/// </summary>
static class RunsInRomanian
{
    // CA2255 warns a library against a module initializer, because the library's users do not
    // expect code to run on load; a test assembly has no users, and this is the one place that
    // runs before every test.
#pragma warning disable CA2255
    [ModuleInitializer]
    internal static void SetRomanian() => Tr.SetDefault(UiLanguage.Romanian);
#pragma warning restore CA2255
}
