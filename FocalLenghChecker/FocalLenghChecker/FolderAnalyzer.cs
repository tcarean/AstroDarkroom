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

namespace FocalLenghChecker;

/// <summary>A file whose focal length could not be read, and why.</summary>
public sealed record UnreadableFile(string Path, string Error);

/// <summary>The outcome of scanning a folder.</summary>
public sealed record FolderAnalysis(
    AnalysisResult Result,
    IReadOnlyList<UnreadableFile> Unreadable,
    int FilesFound);

/// <summary>
/// Walks a folder, reads each .NEF and hands the tally to <see cref="RangeAnalyzer"/>.
/// The file system and the metadata reader both come in through interfaces, so the
/// whole flow can be exercised without touching a disk.
/// </summary>
public sealed class FolderAnalyzer(INefFileFinder finder, IFocalLengthReader reader)
{
    private readonly INefFileFinder _finder = finder ?? throw new ArgumentNullException(nameof(finder));
    private readonly IFocalLengthReader _reader = reader ?? throw new ArgumentNullException(nameof(reader));

    /// <param name="onProgress">Called with (filesRead, filesFound) after each file.</param>
    public FolderAnalysis Analyze(
        string folder,
        bool sourceIsFullFrame,
        IReadOnlyList<FocalLengthRange> ranges,
        double cropFactor,
        Action<int, int>? onProgress = null)
    {
        var files = _finder.Find(folder).ToList();
        var tally = new FocalLengthTally();
        var unreadable = new List<UnreadableFile>();

        for (var i = 0; i < files.Count; i++)
        {
            var result = _reader.Read(files[i]);

            if (result.FocalLengthMm is { } millimetres)
                tally.Add(millimetres);
            else
                unreadable.Add(new UnreadableFile(files[i], result.Error ?? "Unknown error."));

            onProgress?.Invoke(i + 1, files.Count);
        }

        var analysis = RangeAnalyzer.Analyze(tally, sourceIsFullFrame, ranges, cropFactor, unreadable.Count);

        return new FolderAnalysis(analysis, unreadable, files.Count);
    }
}
