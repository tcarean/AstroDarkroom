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

// FileIdentity.cs - whether two paths name one file, told by the file each path opens and not by
// its spelling (R-019, ADR 0018): the copy run refuses a source that is the destination and a
// new file that is either of them.

using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;

namespace CopyWcs;

/// <summary>Whether two paths name one file, whatever their spelling (R-019).</summary>
internal static partial class FileIdentity
{
    /// <summary>The number of 32-bit fields of Windows' <c>BY_HANDLE_FILE_INFORMATION</c>.</summary>
    private const int InfoFields = 13;

    /// <summary>The field of <c>BY_HANDLE_FILE_INFORMATION</c> that holds the volume's serial number.</summary>
    private const int VolumeSerialField = 7;

    /// <summary>The field that holds the high 32 bits of the file index; the low 32 bits follow it.</summary>
    private const int IndexHighField = 11;

    /// <summary>
    /// Whether two paths name the same existing file. On Windows two open files are one when the
    /// system gives both the same volume serial number and file index, so another letter case, a
    /// <c>..</c>, a <c>\\?\</c> prefix, a short 8.3 name or a link names the file it leads to;
    /// elsewhere, or when the system gives no index, the two full paths are compared, letter case
    /// ignored on Windows.
    /// </summary>
    /// <param name="a">A path as given: relative, missing, empty or a folder are all allowed.</param>
    /// <param name="b">Another path, taken the same way.</param>
    /// <returns>True when both name one existing file; false when they name two files, or when either cannot be opened (missing, a folder, an empty path, no access): such a path is no file a run could overwrite, and its read or its write says why it fails.</returns>
    internal static bool Same(string a, string b)
    {
        try
        {
            using SafeFileHandle first = Open(a);
            using SafeFileHandle second = Open(b);
            if (OperatingSystem.IsWindows() && Id(first) is { } x && Id(second) is { } y)
            {
                return x == y;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return false;
        }

        return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    /// <summary>Opens a file for its identity alone, every sharing allowed, so a file another program holds open is still told.</summary>
    /// <param name="path">The path as given.</param>
    /// <returns>The open handle; the caller disposes it.</returns>
    /// <exception cref="IOException">The file does not exist or cannot be opened.</exception>
    /// <exception cref="UnauthorizedAccessException">The path is a folder, or this user may not read the file.</exception>
    /// <exception cref="ArgumentException">The path is empty or blank.</exception>
    private static SafeFileHandle Open(string path) =>
        File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

    /// <summary>The volume serial number and the file index Windows gives an open file.</summary>
    /// <param name="file">An open file.</param>
    /// <returns>The two numbers; null when the call fails, or when the file system gives the index 0, which would make every file of it the same one.</returns>
    [SupportedOSPlatform("windows")]
    private static (uint Volume, ulong Index)? Id(SafeFileHandle file)
    {
        var info = new uint[InfoFields];
        if (!GetFileInformationByHandle(file, info))
        {
            return null;
        }

        ulong index = ((ulong)info[IndexHighField] << 32) | info[IndexHighField + 1];
        return index == 0 ? null : (info[VolumeSerialField], index);
    }

    /// <summary>
    /// Windows' <c>GetFileInformationByHandle</c>: the attributes, the times, the volume serial
    /// number, the size, the link count and the file index of an open file, as 13 32-bit fields.
    /// </summary>
    /// <param name="file">An open file.</param>
    /// <param name="info">Receives <c>BY_HANDLE_FILE_INFORMATION</c>'s 13 fields in their order; 13 elements.</param>
    /// <returns>True when the fields were filled; false when the system could not tell.</returns>
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    [SupportedOSPlatform("windows")]
    private static partial bool GetFileInformationByHandle(SafeFileHandle file, [Out] uint[] info);
}
