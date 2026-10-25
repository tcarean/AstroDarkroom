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

using ImageVisualCompare.Core;

namespace ImageVisualCompare.Tests;

/// <summary>
/// The collection of the tests that switch the interface to Romanian. <see cref="UiText.Current"/>
/// is one value for the whole process, so these run alone, after the parallel ones (ADR 0038).
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class UiLanguageCollection
{
    public const string Name = "UI language";
}

/// <summary>Switches the interface to Romanian until disposed, then back to what it was.</summary>
internal sealed class InRomanian : IDisposable
{
    private readonly UiText _previous = UiText.Current;

    public InRomanian() => UiText.Current = UiText.Romanian;

    public void Dispose() => UiText.Current = _previous;
}
