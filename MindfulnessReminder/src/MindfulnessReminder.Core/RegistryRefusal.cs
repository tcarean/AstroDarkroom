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
/// Which exceptions mean that Windows refused this account a registry access (a policy,
/// security software), so that every reader and writer of the registry treats the same ones
/// as a refusal rather than an error.
/// </summary>
public static class RegistryRefusal
{
    /// <summary>Whether a registry read failed because this account may not read the key.</summary>
    /// <param name="exception">What the read threw.</param>
    /// <returns>
    /// <c>true</c> for a <see cref="System.Security.SecurityException"/>, an
    /// <see cref="UnauthorizedAccessException"/> or an <see cref="IOException"/>.
    /// </returns>
    public static bool IsReadRefusal(Exception exception) =>
        exception is System.Security.SecurityException or UnauthorizedAccessException or IOException;

    /// <summary>Whether a registry write failed because this account may not write the key.</summary>
    /// <param name="exception">What the write threw.</param>
    /// <returns>
    /// <c>true</c> for an <see cref="UnauthorizedAccessException"/> or a
    /// <see cref="System.Security.SecurityException"/>.
    /// </returns>
    public static bool IsWriteRefusal(Exception exception) =>
        exception is UnauthorizedAccessException or System.Security.SecurityException;
}
