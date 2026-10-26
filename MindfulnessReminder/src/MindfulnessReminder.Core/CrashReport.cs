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

using System.Text;

namespace MindfulnessReminder.Core;

/// <summary>
/// The text of the crash dialog (R-076, R-101): what a screenshot of it has to say for the
/// cause to be found. The dialog that only showed <see cref="Exception.Message"/> said nothing
/// on 2026-10-26, when the runtime's own resources were gone and the message was empty
/// (ADR 0036).
/// </summary>
public static class CrashReport
{
    /// <summary>What an inner exception's line starts with.</summary>
    private const string CausedBy = "Caused by: ";

    /// <summary>What the line naming a missing or unloadable file starts with.</summary>
    private const string FilePrefix = "File: ";

    /// <summary>
    /// Renders an exception for the crash dialog: one line per exception, the outermost
    /// first, each the type's full name and, when not blank, <c>: </c> and the message; a
    /// <c>File: </c> line after an exception that names a file it could not load; inner
    /// exceptions prefixed <c>Caused by: </c>; then a blank line and the banner.
    /// </summary>
    /// <param name="exception">What went wrong; not null.</param>
    /// <param name="banner">The name and version line that closes the text; not null.</param>
    /// <returns>The text, its lines joined with <c>\n</c>.</returns>
    public static string Format(Exception exception, string banner)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(banner);

        var text = new StringBuilder();

        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (!ReferenceEquals(current, exception))
            {
                text.Append(CausedBy);
            }

            text.Append(current.GetType().FullName);

            if (!string.IsNullOrWhiteSpace(current.Message))
            {
                text.Append(": ").Append(current.Message);
            }

            text.Append('\n');

            if (FileName(current) is { } file)
            {
                text.Append(FilePrefix).Append(file).Append('\n');
            }
        }

        text.Append('\n').Append(banner);
        return text.ToString();
    }

    /// <summary>
    /// The file an exception names, for the three exception types that carry one: a missing
    /// assembly is the case that mattered on 2026-10-26.
    /// </summary>
    /// <param name="exception">The exception to look at.</param>
    /// <returns>Its <c>FileName</c>, or <c>null</c> when it has none or it is blank.</returns>
    private static string? FileName(Exception exception)
    {
        var name = exception switch
        {
            FileNotFoundException e => e.FileName,
            FileLoadException e => e.FileName,
            BadImageFormatException e => e.FileName,
            _ => null,
        };

        return string.IsNullOrWhiteSpace(name) ? null : name;
    }
}
