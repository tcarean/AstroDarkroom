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

using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;

namespace DarksCheck.Core;

/// <summary>
/// Whether a folder is another folder or lies inside it, told by the folders themselves and not
/// by how their paths are spelled (R-026, ADR 0069): the output folder is refused inside the
/// session whichever name of the session it was reached through. After CopyWcs's
/// <c>FileIdentity</c>, for folders.
/// </summary>
internal static partial class FolderIdentity
{
    /// <summary>The number of 32-bit fields of Windows' <c>BY_HANDLE_FILE_INFORMATION</c>.</summary>
    private const int InfoFields = 13;

    /// <summary>The field of <c>BY_HANDLE_FILE_INFORMATION</c> that holds the volume's serial number.</summary>
    private const int VolumeSerialField = 7;

    /// <summary>The field that holds the high 32 bits of the file index; the low 32 bits follow it.</summary>
    private const int IndexHighField = 11;

    /// <summary><c>FILE_READ_ATTRIBUTES</c>: the only access the identity of a folder needs.</summary>
    private const uint ReadAttributes = 0x80;

    /// <summary><c>FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE</c>: a folder another program holds is still told.</summary>
    private const uint ShareAll = 0x7;

    /// <summary><c>OPEN_EXISTING</c>: the folder is opened, never created.</summary>
    private const uint OpenExisting = 3;

    /// <summary><c>FILE_FLAG_BACKUP_SEMANTICS</c>: what Windows asks for to open a folder and not a file.</summary>
    private const uint BackupSemantics = 0x02000000;

    /// <summary>
    /// Whether <paramref name="dir"/> is the folder <paramref name="root"/> or lies inside it.
    /// First by the full paths, letter case ignored on Windows, a <c>\\?\</c> prefix before a
    /// drive letter taken off, which also answers for a folder that does not exist yet. Then, on
    /// Windows, by the folders themselves: <paramref name="dir"/>, or a folder above it, is the
    /// folder <paramref name="root"/> is when the system gives both the same volume serial number
    /// and file index, so a junction or a link to the folder, a <c>subst</c> drive on it and a
    /// short 8.3 name of it are all that folder.
    /// </summary>
    /// <param name="dir">The folder asked about, absolute or relative to the current directory; it
    /// need not exist. Not null or blank.</param>
    /// <param name="root">The folder it is held against; not null or blank.</param>
    /// <returns>True when <paramref name="dir"/> is <paramref name="root"/> or under it; false
    /// when it is not, or when the system cannot tell beyond the paths (a root that does not
    /// exist or cannot be opened).</returns>
    internal static bool IsOrInside(string dir, string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dir);
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        StringComparison cmp = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        string full = Full(dir), fullRoot = Full(root);
        string prefix = Path.EndsInDirectorySeparator(fullRoot) ? fullRoot : fullRoot + Path.DirectorySeparatorChar;
        if (full.Equals(fullRoot, cmp) || full.StartsWith(prefix, cmp))
        {
            return true;
        }

        if (!OperatingSystem.IsWindows() || Id(fullRoot) is not { } rootId)
        {
            return false;
        }

        // the folder itself and every folder above it: the first that exists settles nothing,
        // since a folder above it may still be the root under another name
        for (string? p = full; !string.IsNullOrEmpty(p); p = Path.GetDirectoryName(p))
        {
            if (Id(p) == rootId)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// A folder's full path for comparing: relative to the current directory when not rooted,
    /// without a trailing separator (a drive's root keeps its own), and without the <c>\\?\</c>
    /// prefix when a drive letter follows it, which <see cref="Path.GetFullPath(string)"/> would
    /// leave as it is, <c>..</c> included.
    /// </summary>
    /// <param name="path">The folder; not null or blank.</param>
    /// <returns>The full path.</returns>
    internal static string Full(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        const string Device = @"\\?\";
        string p = path.StartsWith(Device, StringComparison.Ordinal) && path.Length >= Device.Length + 2 && char.IsAsciiLetter(path[Device.Length]) && path[Device.Length + 1] == ':'
            ? path[Device.Length..]
            : path;
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(p));
    }

    /// <summary>The volume serial number and the file index Windows gives a folder.</summary>
    /// <param name="path">The folder's path.</param>
    /// <returns>The two numbers; null when the folder does not exist or cannot be opened, or when
    /// the file system gives the index 0, which would make every folder of it the same one.</returns>
    [SupportedOSPlatform("windows")]
    private static (uint Volume, ulong Index)? Id(string path)
    {
        using SafeFileHandle folder = CreateFileW(path, ReadAttributes, ShareAll, IntPtr.Zero, OpenExisting, BackupSemantics, IntPtr.Zero);
        uint[] info = new uint[InfoFields];
        if (folder.IsInvalid || !GetFileInformationByHandle(folder, info))
        {
            return null;
        }

        ulong index = ((ulong)info[IndexHighField] << 32) | info[IndexHighField + 1];
        return index == 0 ? null : (info[VolumeSerialField], index);
    }

    /// <summary>
    /// Windows' <c>CreateFileW</c>, used only to open an existing folder for its identity:
    /// nothing is created, read or written through the handle.
    /// </summary>
    /// <param name="path">The folder's path.</param>
    /// <param name="access">The access asked for: <see cref="ReadAttributes"/>.</param>
    /// <param name="share">What others may do meanwhile: <see cref="ShareAll"/>.</param>
    /// <param name="security">No security attributes: <see cref="IntPtr.Zero"/>.</param>
    /// <param name="disposition"><see cref="OpenExisting"/>.</param>
    /// <param name="flags"><see cref="BackupSemantics"/>, which opens a folder.</param>
    /// <param name="template">No template: <see cref="IntPtr.Zero"/>.</param>
    /// <returns>The handle, invalid when the folder could not be opened; the caller disposes
    /// it.</returns>
    [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [SupportedOSPlatform("windows")]
    private static partial SafeFileHandle CreateFileW(string path, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);

    /// <summary>
    /// Windows' <c>GetFileInformationByHandle</c>: the attributes, the times, the volume serial
    /// number, the size, the link count and the file index of an open file or folder, as 13
    /// 32-bit fields.
    /// </summary>
    /// <param name="file">An open file or folder.</param>
    /// <param name="info">Receives <c>BY_HANDLE_FILE_INFORMATION</c>'s 13 fields in their order; 13 elements.</param>
    /// <returns>True when the fields were filled; false when the system could not tell.</returns>
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    [SupportedOSPlatform("windows")]
    private static partial bool GetFileInformationByHandle(SafeFileHandle file, [Out] uint[] info);
}
