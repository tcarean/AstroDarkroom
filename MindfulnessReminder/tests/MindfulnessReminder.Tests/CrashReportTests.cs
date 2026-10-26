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

using MindfulnessReminder.Core;

namespace MindfulnessReminder.Tests;

public class CrashReportTests
{
    private const string Banner = "Mindfulness Reminder 1.4.0";

    [Fact]
    public void R101_The_report_is_the_type_name_and_the_message_then_a_blank_line_and_the_banner()
    {
        var text = CrashReport.Format(new InvalidOperationException("Boom"), Banner);

        Assert.Equal("System.InvalidOperationException: Boom\n\n" + Banner, text);
    }

    /// <summary>
    /// What the runtime hands out once its own resource library is gone (2026-10-26): the
    /// message is empty, and the old dialog showed nothing above the banner.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void R101_A_blank_message_leaves_the_type_name_on_its_own(string message)
    {
        var text = CrashReport.Format(new InvalidOperationException(message), Banner);

        Assert.Equal("System.InvalidOperationException\n\n" + Banner, text);
    }

    /// <summary>The example of R-101, word for word.</summary>
    [Fact]
    public void R101_A_missing_assembly_is_named_on_a_file_line()
    {
        const string name = "UIAutomationTypes, Version=8.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35";
        var exception = new FileNotFoundException(
            "Could not load file or assembly '" + name + "'. The system cannot find the file specified.",
            name);

        var text = CrashReport.Format(exception, Banner);

        Assert.Equal(
            "System.IO.FileNotFoundException: Could not load file or assembly '" + name
            + "'. The system cannot find the file specified.\n"
            + "File: " + name + "\n\n" + Banner,
            text);
    }

    [Fact]
    public void R101_A_file_load_error_and_a_bad_image_name_their_file_too()
    {
        Assert.Contains(
            "\nFile: System.Diagnostics.Tracing\n",
            CrashReport.Format(new FileLoadException("m", "System.Diagnostics.Tracing"), Banner));
        Assert.Contains(
            "\nFile: C:\\x\\bad.dll\n",
            CrashReport.Format(new BadImageFormatException("m", "C:\\x\\bad.dll"), Banner));
    }

    [Fact]
    public void R101_A_file_error_without_a_file_name_has_no_file_line()
    {
        var text = CrashReport.Format(new FileNotFoundException("m"), Banner);

        Assert.Equal("System.IO.FileNotFoundException: m\n\n" + Banner, text);
    }

    /// <summary>
    /// A type initializer that failed wraps the real error; the dialog has to show both, the
    /// inner one with its file line, or a screenshot says nothing about the cause.
    /// </summary>
    [Fact]
    public void R101_Inner_exceptions_follow_as_caused_by_lines_down_to_the_innermost()
    {
        var inner = new FileNotFoundException("Could not load file or assembly 'X'.", "X");
        var middle = new TypeInitializationException("Some.Type", inner);
        var outer = new InvalidOperationException("Window failed", middle);

        var text = CrashReport.Format(outer, Banner);

        Assert.Equal(
            "System.InvalidOperationException: Window failed\n"
            + "Caused by: System.TypeInitializationException: " + middle.Message + "\n"
            + "Caused by: System.IO.FileNotFoundException: Could not load file or assembly 'X'.\n"
            + "File: X\n\n" + Banner,
            text);
    }

    [Fact]
    public void R101_A_null_exception_or_banner_is_refused()
    {
        Assert.Throws<ArgumentNullException>(() => CrashReport.Format(null!, Banner));
        Assert.Throws<ArgumentNullException>(() => CrashReport.Format(new Exception("x"), null!));
    }
}
