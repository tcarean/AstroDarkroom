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

using System.Diagnostics;

namespace ImageVisualCompare.Wpf.Imaging;

/// <param name="ManagedBytes">Managed heap in use.</param>
/// <param name="PrivateBytes">Private working set of the whole process.</param>
public readonly record struct MemorySnapshot(long ManagedBytes, long PrivateBytes)
{
    public string Describe() =>
        $"managed {Megabytes(ManagedBytes)}, process {Megabytes(PrivateBytes)}";

    private static string Megabytes(long bytes) =>
        $"{bytes / (1024.0 * 1024.0):F0} MB";
}

public static class MemoryHousekeeping
{
    /// <summary>
    /// Reclaims the previous pair after it has been dropped.
    ///
    /// WIC keeps decoded pixels in unmanaged memory: measured on a 5794x3637 16-bit pair, the
    /// managed heap never rises above 1 MB while the process holds 173 MB. That rules out
    /// large object heap compaction as a remedy — there is nothing on the large object heap to
    /// compact. What actually returns the memory is the finaliser pass, which is what releases
    /// the unmanaged buffer behind a discarded bitmap, so the collection either side of it is
    /// there to make that pass happen promptly rather than whenever the collector next feels
    /// like it. Measured effect: 173 MB back down to 12 MB.
    /// </summary>
    public static void ReleaseImageBuffers()
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
    }

    public static MemorySnapshot Snapshot()
    {
        using var process = Process.GetCurrentProcess();
        process.Refresh();
        return new MemorySnapshot(GC.GetTotalMemory(forceFullCollection: false), process.PrivateMemorySize64);
    }
}
