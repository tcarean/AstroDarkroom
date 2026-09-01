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

using System.Globalization;
using System.Text;

namespace FocalLenghChecker;

/// <summary>
/// Renders an <see cref="AnalysisResult"/> as the text written to the console.
/// Pure, and invariant-culture throughout so the output does not change with the
/// machine's regional settings.
/// </summary>
public static class ReportFormatter
{
    private const string Nl = "\n";
    private const string Gap = "   ";
    private const string OutsideLabel = "Outside ranges";
    private const string TotalLabel = "Total";

    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    public static string Format(AnalysisResult result, string sourceFolder)
    {
        ArgumentNullException.ThrowIfNull(result);

        var report = new StringBuilder();

        AppendHeader(report, result, sourceFolder);
        report.Append(Nl);
        AppendRangeTable(report, result);
        report.Append(Nl);
        AppendFocalLengthTable(report, result);
        AppendFooter(report, result);

        return report.ToString();
    }

    private static void AppendHeader(StringBuilder report, AnalysisResult result, string sourceFolder)
    {
        var files = result.FilesScanned == 1 ? "file" : "files";

        report.Append(Culture, $"Scanned {Count(result.FilesScanned)} .NEF {files} in {sourceFolder}{Nl}");

        report.Append(result.SourceIsFullFrame
            ? $"Recorded focal lengths treated as full-frame (--ff).{Nl}"
            : $"Recorded focal lengths treated as DX; full-frame equivalent is {Mm(result.CropFactor)}x the DX value.{Nl}");
    }

    private static void AppendRangeTable(StringBuilder report, AnalysisResult result)
    {
        var rows = result.Ranges
            .Select(r => (Ff: r.Range.FfLabel, Dx: r.Range.DxLabel(result.CropFactor), r.Count, r.Percentage))
            .ToList();

        rows.Add((OutsideLabel, string.Empty, result.OutsideRangesCount, result.OutsideRangesPercentage));

        var totalPercentage = result.FilesWithFocalLength == 0 ? 0 : 100.0;

        var ffWidth = Widest(rows.Select(r => r.Ff).Append("Range (FF)").Append(TotalLabel));
        var dxWidth = Widest(rows.Select(r => r.Dx).Append("Range (DX)"));
        var countWidth = Widest(rows.Select(r => Count(r.Count)).Append(Count(result.FilesWithFocalLength)).Append("Count"));
        var pctWidth = Widest(rows.Select(r => Percent(r.Percentage)).Append(Percent(totalPercentage)).Append("%"));

        report.Append(Culture,
            $"{"Range (FF)".PadRight(ffWidth)}{Gap}{"Range (DX)".PadRight(dxWidth)}{Gap}{"Count".PadLeft(countWidth)}{Gap}{"%".PadLeft(pctWidth)}{Nl}");

        foreach (var row in rows)
            report.Append(Culture,
                $"{row.Ff.PadRight(ffWidth)}{Gap}{row.Dx.PadRight(dxWidth)}{Gap}{Count(row.Count).PadLeft(countWidth)}{Gap}{Percent(row.Percentage).PadLeft(pctWidth)}{Nl}");

        report.Append(Culture,
            $"{new string(' ', ffWidth)}{Gap}{new string(' ', dxWidth)}{Gap}{new string('-', countWidth)}{Gap}{new string('-', pctWidth)}{Nl}");

        report.Append(Culture,
            $"{TotalLabel.PadRight(ffWidth)}{Gap}{new string(' ', dxWidth)}{Gap}{Count(result.FilesWithFocalLength).PadLeft(countWidth)}{Gap}{Percent(totalPercentage).PadLeft(pctWidth)}{Nl}");
    }

    private static void AppendFocalLengthTable(StringBuilder report, AnalysisResult result)
    {
        report.Append(Culture, $"Individual focal lengths{Nl}");

        if (result.FocalLengths.Count == 0)
        {
            report.Append(Culture, $"  (none){Nl}");
            return;
        }

        var labels = result.FocalLengths
            .Select(f => $"DX {Mm(f.DxMm)} mm  (FF {Mm(f.FfMm)} mm)")
            .ToList();

        var labelWidth = Widest(labels);
        var countWidth = Widest(result.FocalLengths.Select(f => Count(f.Count)));
        var pctWidth = Widest(result.FocalLengths.Select(f => Percent(f.Percentage)));

        for (var i = 0; i < labels.Count; i++)
            report.Append(Culture,
                $"  {labels[i].PadRight(labelWidth)}{Gap}{Count(result.FocalLengths[i].Count).PadLeft(countWidth)}{Gap}{Percent(result.FocalLengths[i].Percentage).PadLeft(pctWidth)}{Nl}");
    }

    private static void AppendFooter(StringBuilder report, AnalysisResult result)
    {
        if (result.FilesWithoutFocalLength == 0)
            return;

        var files = result.FilesWithoutFocalLength == 1 ? "file" : "files";

        report.Append(Nl);
        report.Append(Culture,
            $"{Count(result.FilesWithoutFocalLength)} {files} had no readable focal length.{Nl}");
    }

    private static int Widest(IEnumerable<string> values) => values.Max(v => v.Length);

    private static string Count(int count) => count.ToString("N0", Culture);

    private static string Percent(double percentage) => percentage.ToString("0.0", Culture) + "%";

    private static string Mm(double millimetres) => millimetres.ToString("0.0", Culture);
}
