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

namespace MindfulnessReminder.Core;

/// <summary>
/// Where the application's decisions are written, one text per event (R-110; ADR 0037). Core
/// composes the texts (<see cref="DecisionLog"/>); the one implementation, in App, stamps the
/// time and appends to a file, and never throws.
/// </summary>
public interface IDecisionLog
{
    /// <summary>Records one event.</summary>
    /// <param name="text">
    /// What happened, as <see cref="DecisionLog"/> renders it; line breaks are allowed and are
    /// folded into one line by the writer.
    /// </param>
    void Write(string text);
}

/// <summary>The log of a copy that keeps none: every write is dropped. The default everywhere.</summary>
internal sealed class NullDecisionLog : IDecisionLog
{
    /// <summary>The one instance; the type has no state.</summary>
    public static readonly NullDecisionLog Instance = new();

    /// <summary>Keeps the type to its one instance.</summary>
    private NullDecisionLog()
    {
    }

    /// <inheritdoc/>
    public void Write(string text)
    {
    }
}
