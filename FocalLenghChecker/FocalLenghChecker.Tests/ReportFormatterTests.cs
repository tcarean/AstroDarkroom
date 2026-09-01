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

namespace FocalLenghChecker.Tests;

public class ReportFormatterTests
{
    private const double Crop = 1.5;
    private const string Folder = @"E:\Photos";

    private static AnalysisResult Analyze(bool fullFrame, int unreadable, params double[] focalLengths)
    {
        var tally = new FocalLengthTally();

        foreach (var focalLength in focalLengths)
            tally.Add(focalLength);

        return RangeAnalyzer.Analyze(tally, fullFrame, FocalLengthRanges.Default, Crop, unreadable);
    }

    private static string Report(bool fullFrame = false, int unreadable = 0, params double[] focalLengths) =>
        ReportFormatter.Format(Analyze(fullFrame, unreadable, focalLengths), Folder);

    private static string[] Lines(string report) =>
        report.Split('\n', StringSplitOptions.RemoveEmptyEntries);

    [Fact]
    public void The_folder_that_was_scanned_is_named()
    {
        Assert.Contains(Folder, Report(focalLengths: 18));
    }

    [Fact]
    public void The_file_count_is_reported()
    {
        Assert.Contains("Scanned 3 .NEF files", Report(focalLengths: new[] { 18.0, 18.0, 35.0 }));
    }

    [Fact]
    public void A_single_file_is_described_in_the_singular()
    {
        Assert.Contains("Scanned 1 .NEF file in", Report(focalLengths: 18));
    }

    [Fact]
    public void Unreadable_files_are_counted_in_the_files_scanned()
    {
        Assert.Contains("Scanned 5 .NEF files", Report(unreadable: 3, focalLengths: new[] { 18.0, 35.0 }));
    }

    [Fact]
    public void Large_counts_get_thousands_separators()
    {
        var many = Enumerable.Repeat(18.0, 1500).ToArray();

        Assert.Contains("Scanned 1,500 .NEF files", Report(focalLengths: many));
    }

    [Fact]
    public void DX_mode_says_so_and_names_the_crop_factor()
    {
        var report = Report(fullFrame: false, focalLengths: 18);

        Assert.Contains("treated as DX", report);
        Assert.Contains("1.5x", report);
        Assert.DoesNotContain("--ff", report);
    }

    [Fact]
    public void FF_mode_says_so()
    {
        var report = Report(fullFrame: true, focalLengths: 18);

        Assert.Contains("treated as full-frame (--ff)", report);
        Assert.DoesNotContain("treated as DX", report);
    }

    [Fact]
    public void Every_configured_range_gets_a_row_showing_both_FF_and_DX()
    {
        var report = Report(focalLengths: 18);

        foreach (var range in FocalLengthRanges.Default)
        {
            var row = Assert.Single(Lines(report), line => line.StartsWith(range.FfLabel));

            Assert.Contains(range.DxLabel(Crop), row);
        }
    }

    [Fact]
    public void A_range_row_carries_its_count_and_percentage()
    {
        var report = Report(focalLengths: new[] { 18.0, 18.0, 18.0, 35.0 });

        var row = Assert.Single(Lines(report), line => line.StartsWith("24 - 27 mm"));

        Assert.Contains("3", row);
        Assert.Contains("75.0%", row);
    }

    [Fact]
    public void The_outside_bucket_is_shown_even_when_it_is_empty()
    {
        Assert.Contains("Outside ranges", Report(focalLengths: 18));
    }

    [Fact]
    public void The_total_row_accounts_for_every_file_that_had_a_focal_length()
    {
        var report = Report(unreadable: 2, focalLengths: new[] { 18.0, 35.0, 100.0 });

        var row = Assert.Single(Lines(report), line => line.StartsWith("Total"));

        Assert.Contains("3", row);
        Assert.Contains("100.0%", row);
    }

    [Fact]
    public void The_total_row_reads_zero_percent_when_nothing_could_be_read()
    {
        var row = Assert.Single(Lines(Report(unreadable: 4)), line => line.StartsWith("Total"));

        Assert.Contains("0.0%", row);
        Assert.DoesNotContain("100.0%", row);
    }

    [Fact]
    public void Each_distinct_focal_length_is_listed_with_both_values()
    {
        var report = Report(focalLengths: new[] { 18.0, 18.0, 35.0 });

        Assert.Contains("DX 18.0 mm  (FF 27.0 mm)", report);
        Assert.Contains("DX 35.0 mm  (FF 52.5 mm)", report);
    }

    [Fact]
    public void The_individual_focal_lengths_of_a_full_frame_run_show_the_derived_DX_value()
    {
        Assert.Contains("DX 18.0 mm  (FF 27.0 mm)", Report(fullFrame: true, focalLengths: 27));
    }

    [Fact]
    public void The_individual_list_says_so_when_there_is_nothing_in_it()
    {
        var report = Report(unreadable: 2);

        Assert.Contains("Individual focal lengths", report);
        Assert.Contains("(none)", report);
    }

    [Fact]
    public void Unreadable_files_are_reported_at_the_end()
    {
        Assert.Contains("3 files had no readable focal length.", Report(unreadable: 3, focalLengths: 18));
    }

    [Fact]
    public void One_unreadable_file_is_described_in_the_singular()
    {
        Assert.Contains("1 file had no readable focal length.", Report(unreadable: 1, focalLengths: 18));
    }

    [Fact]
    public void Nothing_is_said_about_unreadable_files_when_there_are_none()
    {
        Assert.DoesNotContain("no readable focal length", Report(focalLengths: 18));
    }

    [Fact]
    public void The_report_ends_with_a_newline()
    {
        Assert.EndsWith("\n", Report(focalLengths: 18));
    }

    [Fact]
    public void A_null_result_is_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => ReportFormatter.Format(null!, Folder));
    }

    [Fact]
    public void The_report_does_not_change_with_the_machine_regional_settings()
    {
        // de-DE writes 1.500 for a thousand and 52,5 for a decimal; the report must not.
        var original = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            var report = Report(focalLengths: Enumerable.Repeat(35.0, 1500).ToArray());

            Assert.Contains("Scanned 1,500 .NEF files", report);
            Assert.Contains("DX 35.0 mm  (FF 52.5 mm)", report);
            Assert.Contains("100.0%", report);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
