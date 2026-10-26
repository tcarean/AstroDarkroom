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

using CopyWcs.Tests;

namespace CopyWcs.Wpf.Tests;

/// <summary>
/// The pair of files every window test opens, written into a temp folder of its own: a
/// plate-solved source and a processed destination of the same size from
/// <see cref="FitsTestData"/>, both with the same deterministic data area.
/// </summary>
internal sealed class WindowFixture : IDisposable
{
    private readonly TempWorkspace _ws = new();

    /// <summary>The pair, the destination with the given width (80 rows); 100 matches the source.</summary>
    /// <param name="destWidth">The destination's NAXIS1; the source is always 100 x 80.</param>
    /// <param name="sourceCards">The source's cards; the default plate-solved header when null.</param>
    internal WindowFixture(int destWidth = 100, List<string>? sourceCards = null)
    {
        byte[] data = FitsTestData.DeterministicData(400);
        Source = _ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(sourceCards ?? FitsTestData.PlateSolvedCards(), data));
        Dest = _ws.WriteFile("dest.fit", FitsTestData.MinimalFitsFile(FitsTestData.ProcessedCards(destWidth), data));
    }

    internal string Source { get; }

    internal string Dest { get; }

    internal string Dir => _ws.Dir;

    /// <summary>A path inside the folder, for an output file or a second copy of the destination.</summary>
    /// <param name="name">The file name.</param>
    /// <returns>The full path; the file need not exist.</returns>
    internal string FilePath(string name) => _ws.FilePath(name);

    public void Dispose() => _ws.Dispose();
}
