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

using System.Text;
using MindfulnessReminder.Core;

namespace MindfulnessReminder.Tests;

/// <summary>
/// The file half of R-110, on a folder of its own under the temp directory: the line written,
/// the rotation at the cap, and the failures the writer swallows.
/// </summary>
public sealed class FileDecisionLogTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "MindfulnessReminder.Tests", Guid.NewGuid().ToString("N"));

    private string LogPath => Path.Combine(_folder, FileDecisionLog.FileName);

    private string RotatedPath => Path.Combine(_folder, FileDecisionLog.RotatedFileName);

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void R110_The_first_write_creates_the_folder_and_one_stamped_line()
    {
        new FileDecisionLog(_folder).Write("Workstation locked");

        var lines = File.ReadAllLines(LogPath);
        Assert.Single(lines);
        Assert.Matches(@"^\d{4}-\d\d-\d\d \d\d:\d\d:\d\d  Workstation locked$", lines[0]);
    }

    [Fact]
    public void R110_Lines_end_in_the_platform_newline_and_the_file_has_no_byte_order_mark()
    {
        var log = new FileDecisionLog(_folder);
        log.Write("first\nsecond");
        log.Write("third");

        var bytes = File.ReadAllBytes(LogPath);
        var text = Encoding.UTF8.GetString(bytes);

        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF, "a byte order mark was written");
        Assert.EndsWith("  first | second" + Environment.NewLine, text.Split(Environment.NewLine)[0] + Environment.NewLine);
        Assert.Equal(2, text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Length);
        Assert.EndsWith(Environment.NewLine, text);
    }

    [Fact]
    public void R110_A_file_at_the_cap_is_not_rotated_and_one_past_it_is()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllBytes(LogPath, new byte[DecisionLog.MaxBytes]);

        new FileDecisionLog(_folder).Write("at the cap");

        Assert.False(File.Exists(RotatedPath));
        Assert.True(new FileInfo(LogPath).Length > DecisionLog.MaxBytes);

        new FileDecisionLog(_folder).Write("past the cap");

        Assert.True(File.Exists(RotatedPath));
        Assert.True(new FileInfo(RotatedPath).Length > DecisionLog.MaxBytes);
        Assert.Single(File.ReadAllLines(LogPath));
        Assert.EndsWith("  past the cap", File.ReadAllLines(LogPath)[0]);
    }

    [Fact]
    public void R110_A_second_rotation_replaces_the_previous_rotated_file()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(RotatedPath, "older");
        File.WriteAllBytes(LogPath, new byte[DecisionLog.MaxBytes + 1]);

        new FileDecisionLog(_folder).Write("rotated again");

        Assert.Equal(DecisionLog.MaxBytes + 1, new FileInfo(RotatedPath).Length);
        Assert.Single(File.ReadAllLines(LogPath));
    }

    /// <summary>
    /// The rotated file held open by something else (an editor, a scanner): the move fails,
    /// the line is still written, and the file grows past the cap until a later write can
    /// rotate it. A log that fell silent instead would be the worse failure.
    /// </summary>
    [Fact]
    public void R110_A_rotation_that_fails_does_not_lose_the_line()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllBytes(LogPath, new byte[DecisionLog.MaxBytes + 1]);
        using var held = new FileStream(RotatedPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);

        new FileDecisionLog(_folder).Write("still written");

        Assert.True(new FileInfo(LogPath).Length > DecisionLog.MaxBytes + 1);
        Assert.EndsWith("  still written", File.ReadAllLines(LogPath)[^1]);
    }

    /// <summary>A folder that cannot be created: a path below an existing file.</summary>
    [Fact]
    public void R110_A_folder_that_cannot_be_created_loses_the_line_and_throws_nothing()
    {
        Directory.CreateDirectory(_folder);
        var file = Path.Combine(_folder, "a-file");
        File.WriteAllText(file, "x");

        var log = new FileDecisionLog(Path.Combine(file, "below-a-file"));
        log.Write("lost");

        Assert.Equal("x", File.ReadAllText(file));
    }

    [Fact]
    public void R110_An_empty_folder_means_nowhere_to_write_and_nothing_is_written()
    {
        var before = Directory.GetCurrentDirectory();

        new FileDecisionLog(string.Empty).Write("nowhere");

        Assert.False(File.Exists(Path.Combine(before, FileDecisionLog.FileName)));
        Assert.False(File.Exists(FileDecisionLog.FileName));
    }

    [Fact]
    public void R110_A_null_folder_or_text_is_refused()
    {
        Assert.Throws<ArgumentNullException>(() => new FileDecisionLog(null!));
        Assert.Throws<ArgumentNullException>(() => new FileDecisionLog(_folder).Write(null!));
    }
}
