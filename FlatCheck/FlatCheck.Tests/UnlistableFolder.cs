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

using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace FlatCheck.Tests;

/// <summary>
/// A folder the current user may not list, for the tests of what an unreadable folder does:
/// a deny rule on <c>ListDirectory</c>, removed again on <see cref="Dispose"/> so the folder
/// can be deleted. Windows only; a test on another OS returns before calling it (linked into
/// FlatCheck.Wpf.Tests too).
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class UnlistableFolder : IDisposable
{
    readonly DirectoryInfo info;
    readonly FileSystemAccessRule deny;

    UnlistableFolder(DirectoryInfo info, FileSystemAccessRule deny)
    {
        this.info = info;
        this.deny = deny;
    }

    /// <summary>Denies the current user the listing of <paramref name="dir"/>, which must
    /// exist, and checks that the denial holds.</summary>
    /// <param name="dir">The folder.</param>
    /// <returns>The rule to dispose.</returns>
    internal static UnlistableFolder Deny(string dir)
    {
        var info = new DirectoryInfo(dir);
        var user = WindowsIdentity.GetCurrent().User!;
        var deny = new FileSystemAccessRule(user, FileSystemRights.ListDirectory, AccessControlType.Deny);
        var acl = info.GetAccessControl();
        acl.AddAccessRule(deny);
        info.SetAccessControl(acl);
        Assert.Throws<UnauthorizedAccessException>(() => Directory.EnumerateFiles(dir).ToList());   // the setup holds
        return new UnlistableFolder(info, deny);
    }

    /// <summary>Removes the rule.</summary>
    public void Dispose()
    {
        var acl = info.GetAccessControl();
        acl.RemoveAccessRule(deny);
        info.SetAccessControl(acl);
    }
}
