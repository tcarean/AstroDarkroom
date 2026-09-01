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

public class CommandLineParserTests
{
    private const string Cwd = @"C:\current";

    private static CliOptions ParseOk(params string[] args)
    {
        var result = CommandLineParser.Parse(args, Cwd);

        Assert.Null(result.Error);
        Assert.False(result.HelpRequested);

        return Assert.IsType<CliOptions>(result.Options);
    }

    [Fact]
    public void No_arguments_uses_the_current_directory_and_DX()
    {
        var options = ParseOk();

        Assert.Equal(Cwd, options.SourceFolder);
        Assert.False(options.FullFrame);
    }

    [Fact]
    public void A_folder_on_its_own_stays_DX()
    {
        var options = ParseOk(@"E:\Photos");

        Assert.Equal(@"E:\Photos", options.SourceFolder);
        Assert.False(options.FullFrame);
    }

    [Fact]
    public void The_ff_flag_on_its_own_still_uses_the_current_directory()
    {
        var options = ParseOk("--ff");

        Assert.Equal(Cwd, options.SourceFolder);
        Assert.True(options.FullFrame);
    }

    [Theory]
    [InlineData("--ff")]
    [InlineData("-ff")]
    [InlineData("--FF")]
    [InlineData("--Ff")]
    public void The_ff_flag_is_accepted_in_several_spellings(string flag)
    {
        Assert.True(ParseOk(flag).FullFrame);
    }

    [Fact]
    public void The_folder_may_come_before_the_flag()
    {
        var options = ParseOk(@"E:\Photos", "--ff");

        Assert.Equal(@"E:\Photos", options.SourceFolder);
        Assert.True(options.FullFrame);
    }

    [Fact]
    public void The_folder_may_come_after_the_flag()
    {
        var options = ParseOk("--ff", @"E:\Photos");

        Assert.Equal(@"E:\Photos", options.SourceFolder);
        Assert.True(options.FullFrame);
    }

    [Fact]
    public void A_folder_containing_spaces_survives_as_one_argument()
    {
        Assert.Equal(@"E:\My Photos\2026", ParseOk(@"E:\My Photos\2026").SourceFolder);
    }

    [Fact]
    public void Blank_arguments_are_ignored()
    {
        var options = ParseOk("", "   ", @"E:\Photos");

        Assert.Equal(@"E:\Photos", options.SourceFolder);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("-?")]
    [InlineData("--HELP")]
    public void Help_is_recognised(string flag)
    {
        var result = CommandLineParser.Parse(new[] { flag }, Cwd);

        Assert.True(result.HelpRequested);
        Assert.Null(result.Options);
    }

    [Fact]
    public void Help_wins_over_everything_else_on_the_line()
    {
        Assert.True(CommandLineParser.Parse(new[] { @"E:\Photos", "--ff", "--help" }, Cwd).HelpRequested);
    }

    [Fact]
    public void An_unknown_option_is_an_error()
    {
        var result = CommandLineParser.Parse(new[] { "--dx" }, Cwd);

        Assert.Null(result.Options);
        Assert.Contains("--dx", result.Error);
    }

    [Fact]
    public void A_second_folder_is_an_error()
    {
        var result = CommandLineParser.Parse(new[] { @"E:\One", @"E:\Two" }, Cwd);

        Assert.Null(result.Options);
        Assert.Contains(@"E:\Two", result.Error);
    }

    [Fact]
    public void Repeating_the_ff_flag_is_an_error()
    {
        var result = CommandLineParser.Parse(new[] { "--ff", "--ff" }, Cwd);

        Assert.Null(result.Options);
        Assert.Contains("--ff", result.Error);
    }

    [Fact]
    public void Null_arguments_are_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => CommandLineParser.Parse(null!, Cwd));
    }

    [Fact]
    public void The_usage_text_documents_both_the_folder_and_the_flag()
    {
        Assert.Contains("SourceFolderPath", CommandLineParser.Usage);
        Assert.Contains("--ff", CommandLineParser.Usage);
    }
}
