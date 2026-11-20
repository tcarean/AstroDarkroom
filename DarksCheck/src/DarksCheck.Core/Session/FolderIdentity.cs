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
    /// Windows, by the folders themselves: the deepest folder of <paramref name="dir"/>'s path
    /// that exists is taken by the path the system resolves it to, with no link left in it
    /// (<see cref="Resolved"/>), and that folder, or a folder above it, is the folder
    /// <paramref name="root"/> is when the system gives both the same volume serial number and
    /// file index. So a junction or a link to the folder or to a folder inside it, a
    /// <c>subst</c> drive on either and a short 8.3 name are all told. The typed path's own
    /// folders are not enough: the folder above a link is not above the place the link leads to.
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

        // the folder as the system resolves it and every folder above that: a link on the typed
        // path may lead into the root from a folder that is not under it
        for (string? p = Resolved(full); !string.IsNullOrEmpty(p); p = Path.GetDirectoryName(p))
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

    /// <summary>
    /// The folder a path really names, as a path to show and to take the folder around it from: on
    /// Windows the path the system resolves an existing folder to, links, <c>subst</c> drives and
    /// short names followed, without the <c>\\?\</c> prefix; the full path as typed for a
    /// folder that does not exist or cannot be opened, and elsewhere.
    /// </summary>
    /// <param name="path">The folder; not null or blank.</param>
    /// <returns>The path, without a trailing separator (a drive's root keeps its own).</returns>
    internal static string Real(string path)
    {
        string full = Full(path);
        if (!OperatingSystem.IsWindows() || FinalPath(full) is not string final)
        {
            return full;
        }

        const string Unc = @"\\?\UNC\", Device = @"\\?\";
        string plain = final.StartsWith(Unc, StringComparison.OrdinalIgnoreCase) ? @"\\" + final[Unc.Length..]
            : final.StartsWith(Device, StringComparison.Ordinal) ? final[Device.Length..]
            : final;
        return Path.TrimEndingDirectorySeparator(plain);
    }

    /// <summary>
    /// A full path with the links on it followed: the deepest folder of the path that exists, by
    /// the path the system resolves it to, then the rest of the path as typed (a folder that does
    /// not exist yet is no link).
    /// </summary>
    /// <param name="full">A full path, as <see cref="Full"/> gives it.</param>
    /// <returns>The resolved path, in the system's <c>\\?\</c> form from its first folder that
    /// exists; the path as given when none of its folders can be opened.</returns>
    [SupportedOSPlatform("windows")]
    private static string Resolved(string full)
    {
        string rest = string.Empty;
        for (string? p = full; !string.IsNullOrEmpty(p); p = Path.GetDirectoryName(p))
        {
            if (FinalPath(p) is string final)
            {
                return rest.Length == 0 ? final : Path.Combine(final, rest);
            }

            // p does not exist (or cannot be opened): its name goes back on once a folder above resolves
            string name = Path.GetFileName(p);
            rest = rest.Length == 0 ? name : Path.Combine(name, rest);
        }

        return full;
    }

    /// <summary>
    /// The length from which a folder's path is opened in the <c>\\?\</c> form: 248, the length
    /// from which Windows with its long paths off no longer makes a folder by its plain path
    /// (260 less the 12 characters of a short file name). A folder that exists opens plainly up
    /// to 259 characters; the form does no harm below that.
    /// </summary>
    private const int LongPath = 248;

    /// <summary>
    /// Opens an existing folder for its identity alone: nothing is created, read or written
    /// through the handle. A long path is opened in the form that takes one
    /// (<see cref="Extended"/>): opened as typed it would fail where Windows' long paths are off,
    /// and a link that far down a path would pass for a folder that does not exist yet (CR-080).
    /// </summary>
    /// <param name="path">The folder's full path.</param>
    /// <returns>The handle, invalid when the folder does not exist or cannot be opened; the caller
    /// disposes it.</returns>
    [SupportedOSPlatform("windows")]
    private static SafeFileHandle Open(string path) =>
        CreateFileW(Extended(path), ReadAttributes, ShareAll, IntPtr.Zero, OpenExisting, BackupSemantics, IntPtr.Zero);

    /// <summary>
    /// A full path in the form Windows takes at any length: <c>\\?\C:\...</c> for a drive's,
    /// <c>\\?\UNC\server\share\...</c> for a share's. A path under <see cref="LongPath"/>
    /// characters, one already in a device form, and one that is neither a drive's nor a share's
    /// are returned as they are.
    /// </summary>
    /// <param name="path">A full path, as <see cref="Full"/> or the system gives it.</param>
    /// <returns>The path to hand to <c>CreateFileW</c>.</returns>
    internal static string Extended(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (path.Length < LongPath || path.StartsWith(@"\\?\", StringComparison.Ordinal) || path.StartsWith(@"\\.\", StringComparison.Ordinal))
        {
            return path;
        }

        if (path.StartsWith(@"\\", StringComparison.Ordinal))
        {
            return @"\\?\UNC\" + path[2..];
        }

        return path.Length >= 2 && char.IsAsciiLetter(path[0]) && path[1] == ':' ? @"\\?\" + path : path;
    }

    /// <summary>The volume serial number and the file index Windows gives a folder.</summary>
    /// <param name="path">The folder's path.</param>
    /// <returns>The two numbers; null when the folder does not exist or cannot be opened, or when
    /// the file system gives the index 0, which would make every folder of it the same one.</returns>
    [SupportedOSPlatform("windows")]
    private static (uint Volume, ulong Index)? Id(string path)
    {
        using SafeFileHandle folder = Open(path);
        uint[] info = new uint[InfoFields];
        if (folder.IsInvalid || !GetFileInformationByHandle(folder, info))
        {
            return null;
        }

        ulong index = ((ulong)info[IndexHighField] << 32) | info[IndexHighField + 1];
        return index == 0 ? null : (info[VolumeSerialField], index);
    }

    /// <summary>
    /// The path Windows resolves an existing folder to: every link, <c>subst</c> drive and short
    /// name on the way followed, the volume by its drive letter (a share as
    /// <c>\\?\UNC\server\share</c>), in the <c>\\?\</c> form.
    /// </summary>
    /// <param name="path">The folder's path.</param>
    /// <returns>The final path; null when the folder does not exist or cannot be opened, or when
    /// the system cannot name it.</returns>
    [SupportedOSPlatform("windows")]
    private static string? FinalPath(string path)
    {
        using SafeFileHandle folder = Open(path);
        if (folder.IsInvalid)
        {
            return null;
        }

        char[] buffer = new char[512];
        for (int attempt = 0; attempt < 2; attempt++)
        {
            // the length without the ending zero when it fits; the size needed, with it, when not; 0 on failure
            uint length = GetFinalPathNameByHandleW(folder, buffer, (uint)buffer.Length, 0);
            if (length == 0)
            {
                return null;
            }

            if (length < buffer.Length)
            {
                return new string(buffer, 0, (int)length);
            }

            buffer = new char[length + 1];
        }

        return null;
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
    /// Windows' <c>GetFinalPathNameByHandleW</c>: the path of an open file or folder with every
    /// link followed; with the flags 0, the normalized name and the volume by its drive letter.
    /// </summary>
    /// <param name="file">An open file or folder.</param>
    /// <param name="path">Receives the path, zero-terminated when it fits.</param>
    /// <param name="length">The buffer's size, in characters.</param>
    /// <param name="flags">0: <c>FILE_NAME_NORMALIZED | VOLUME_NAME_DOS</c>.</param>
    /// <returns>The path's length without the ending zero when it fits; the size needed, with the
    /// zero, when the buffer is too small; 0 when the system cannot tell.</returns>
    [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [SupportedOSPlatform("windows")]
    private static partial uint GetFinalPathNameByHandleW(SafeFileHandle file, [Out] char[] path, uint length, uint flags);

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
