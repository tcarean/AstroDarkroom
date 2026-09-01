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

/// <summary>
/// Turns a tally of focal lengths into the counts and percentages for each
/// configured range. Pure: no files, no console, no clock.
/// </summary>
public static class RangeAnalyzer
{
    public static AnalysisResult Analyze(
        FocalLengthTally tally,
        bool sourceIsFullFrame,
        IReadOnlyList<FocalLengthRange> ranges,
        double cropFactor,
        int filesWithoutFocalLength = 0)
    {
        ArgumentNullException.ThrowIfNull(tally);
        FocalLengthRanges.Validate(ranges);
        ArgumentOutOfRangeException.ThrowIfNegative(filesWithoutFocalLength);

        var total = tally.Total;
        var rangeCounts = new int[ranges.Count];
        var outside = 0;
        var focalLengths = new List<FocalLengthCount>(tally.Counts.Count);

        foreach (var (recordedMm, count) in tally.Counts)
        {
            var ff = FocalLengthConverter.ToFullFrame(recordedMm, sourceIsFullFrame, cropFactor);
            var dx = FocalLengthConverter.ToDx(recordedMm, sourceIsFullFrame, cropFactor);

            focalLengths.Add(new FocalLengthCount(dx, ff, count, Percentage(count, total)));

            var matched = false;

            for (var i = 0; i < ranges.Count; i++)
            {
                if (!ranges[i].Contains(ff))
                    continue;

                rangeCounts[i] += count;
                matched = true;
                break;
            }

            if (!matched)
                outside += count;
        }

        var rows = ranges
            .Select((range, i) => new RangeCount(range, rangeCounts[i], Percentage(rangeCounts[i], total)))
            .ToList();

        // Most-used focal length first; ties broken by the shorter focal length.
        focalLengths.Sort(static (a, b) =>
            b.Count != a.Count ? b.Count.CompareTo(a.Count) : a.FfMm.CompareTo(b.FfMm));

        return new AnalysisResult(
            sourceIsFullFrame,
            cropFactor,
            rows,
            outside,
            Percentage(outside, total),
            focalLengths,
            total,
            filesWithoutFocalLength);
    }

    private static double Percentage(int count, int total) =>
        total == 0 ? 0 : count * 100.0 / total;
}
