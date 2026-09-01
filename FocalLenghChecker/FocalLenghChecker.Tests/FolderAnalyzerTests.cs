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

namespace FocalLenghChecker.Tests;

public class FolderAnalyzerTests
{
    private const double Crop = 1.5;

    private sealed class FakeFinder(params string[] files) : INefFileFinder
    {
        public string? FolderAskedFor { get; private set; }

        public IEnumerable<string> Find(string folder)
        {
            FolderAskedFor = folder;
            return files;
        }
    }

    private sealed class FakeReader(Dictionary<string, FocalLengthReadResult> results) : IFocalLengthReader
    {
        public List<string> PathsRead { get; } = new();

        public FocalLengthReadResult Read(string path)
        {
            PathsRead.Add(path);

            return results.TryGetValue(path, out var result)
                ? result
                : FocalLengthReadResult.Failure("Not set up in this test.");
        }
    }

    private static FolderAnalyzer AnalyzerFor(params (string Path, double? FocalLength)[] files)
    {
        var results = files.ToDictionary(
            f => f.Path,
            f => f.FocalLength is { } mm
                ? FocalLengthReadResult.Success(mm)
                : FocalLengthReadResult.Failure("No EXIF focal length was recorded."));

        return new FolderAnalyzer(new FakeFinder(files.Select(f => f.Path).ToArray()), new FakeReader(results));
    }

    private static FolderAnalysis Run(FolderAnalyzer analyzer, bool fullFrame = false) =>
        analyzer.Analyze(@"E:\Photos", fullFrame, FocalLengthRanges.Default, Crop);

    [Fact]
    public void An_empty_folder_produces_an_empty_analysis()
    {
        var analysis = Run(AnalyzerFor());

        Assert.Equal(0, analysis.FilesFound);
        Assert.Empty(analysis.Unreadable);
        Assert.Equal(0, analysis.Result.FilesWithFocalLength);
    }

    [Fact]
    public void Every_file_found_is_read()
    {
        var reader = new FakeReader(new Dictionary<string, FocalLengthReadResult>
        {
            ["a.NEF"] = FocalLengthReadResult.Success(18),
            ["b.NEF"] = FocalLengthReadResult.Success(35),
        });

        new FolderAnalyzer(new FakeFinder("a.NEF", "b.NEF"), reader).Analyze(
            @"E:\Photos", false, FocalLengthRanges.Default, Crop);

        Assert.Equal(new[] { "a.NEF", "b.NEF" }, reader.PathsRead);
    }

    [Fact]
    public void The_folder_asked_for_is_the_one_passed_in()
    {
        var finder = new FakeFinder();

        new FolderAnalyzer(finder, new FakeReader(new())).Analyze(
            @"E:\Photos", false, FocalLengthRanges.Default, Crop);

        Assert.Equal(@"E:\Photos", finder.FolderAskedFor);
    }

    [Fact]
    public void Readable_files_are_tallied_and_bucketed()
    {
        var analysis = Run(AnalyzerFor(
            ("a.NEF", 18), ("b.NEF", 18), ("c.NEF", 35), ("d.NEF", 100)));

        Assert.Equal(4, analysis.FilesFound);
        Assert.Equal(4, analysis.Result.FilesWithFocalLength);
        Assert.Equal(2, analysis.Result.Ranges[1].Count);   // FF 27
        Assert.Equal(1, analysis.Result.Ranges[2].Count);   // FF 52.5
        Assert.Equal(1, analysis.Result.Ranges[3].Count);   // FF 150
    }

    [Fact]
    public void Unreadable_files_are_listed_with_their_reason_and_kept_out_of_the_tally()
    {
        var analysis = Run(AnalyzerFor(("good.NEF", 18), ("bad.NEF", null), ("also-bad.NEF", null)));

        Assert.Equal(3, analysis.FilesFound);
        Assert.Equal(1, analysis.Result.FilesWithFocalLength);
        Assert.Equal(2, analysis.Result.FilesWithoutFocalLength);
        Assert.Equal(new[] { "bad.NEF", "also-bad.NEF" }, analysis.Unreadable.Select(u => u.Path));
        Assert.All(analysis.Unreadable, u => Assert.False(string.IsNullOrWhiteSpace(u.Error)));
    }

    [Fact]
    public void One_unreadable_file_does_not_stop_the_rest_being_read()
    {
        var analysis = Run(AnalyzerFor(("bad.NEF", null), ("good.NEF", 18)));

        Assert.Equal(1, analysis.Result.Ranges[1].Count);
    }

    [Fact]
    public void The_ff_switch_is_passed_through_to_the_bucketing()
    {
        var analyzer = AnalyzerFor(("a.NEF", 18));

        Assert.Equal(1, Run(analyzer, fullFrame: false).Result.Ranges[1].Count);   // 18 DX -> FF 27

        Assert.Equal(1, Run(AnalyzerFor(("a.NEF", 18)), fullFrame: true).Result.Ranges[0].Count);
    }

    [Fact]
    public void Progress_is_reported_once_per_file_with_a_running_total()
    {
        var seen = new List<(int Read, int Total)>();

        AnalyzerFor(("a.NEF", 18), ("b.NEF", 35), ("c.NEF", null)).Analyze(
            @"E:\Photos", false, FocalLengthRanges.Default, Crop, (read, total) => seen.Add((read, total)));

        Assert.Equal(new[] { (1, 3), (2, 3), (3, 3) }, seen);
    }

    [Fact]
    public void Progress_is_optional()
    {
        var analysis = Run(AnalyzerFor(("a.NEF", 18)));

        Assert.Equal(1, analysis.FilesFound);
    }

    [Fact]
    public void A_missing_finder_or_reader_is_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => new FolderAnalyzer(null!, new FakeReader(new())));
        Assert.Throws<ArgumentNullException>(() => new FolderAnalyzer(new FakeFinder(), null!));
    }
}
