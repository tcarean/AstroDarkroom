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

using LightsCheck.Core;

namespace LightsCheck.Tests;

/// <summary>
/// The console's table writer (R-095): a header and rows with the same column widths, and the
/// check the two reports' tests use to see that every title sits over its cells.
/// </summary>
public class TextTableTests
{
    /// <summary>
    /// Fails unless every row of a printed table has, under each title of its header, a cell
    /// set to the same side: ending where the title ends for a column of figures, starting
    /// where it starts for a column of words.
    /// </summary>
    /// <param name="lines">The header line, then the rows; a row may end before its last columns.</param>
    /// <param name="columns">The titles in the header's order, each with whether its column is set to the right.</param>
    internal static void AssertUnderHeader(IReadOnlyList<string> lines, params (string Title, bool Right)[] columns)
    {
        string header = lines[0];
        List<(int Start, int End, bool Right, string Title)> spans = [];
        int from = 0;
        foreach ((string title, bool right) in columns)
        {
            int at = header.IndexOf(title, from, StringComparison.Ordinal);
            Assert.True(at >= 0, $"the header lacks '{title}' after column {from}: {header}");
            spans.Add((at, at + title.Length - 1, right, title));
            from = at + title.Length;
        }

        foreach (string row in lines.Skip(1))
        {
            foreach ((int start, int end, bool right, string title) in spans)
            {
                if (right)
                {
                    Assert.True(row.Length > end && row[end] != ' ', $"no cell ends under the end of '{title}' (column {end}):\n{header}\n{row}");
                    Assert.True(row.Length == end + 1 || row[end + 1] == ' ', $"the cell under '{title}' runs past its title (column {end}):\n{header}\n{row}");
                }
                else if (row.Length > start)
                {
                    Assert.True(row[start] != ' ', $"no cell starts under the start of '{title}' (column {start}):\n{header}\n{row}");
                    Assert.True(row[start - 1] == ' ', $"the cell under '{title}' starts before its title (column {start}):\n{header}\n{row}");
                }
            }
        }
    }

    private static string[] Written(TextTable table)
    {
        using StringWriter w = new();
        table.Write(w, "    ");
        return w.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>
    /// A title sits over its cells, figures to the right and words to the left, and a cell
    /// wider than its column's least width widens the column for the header and every row.
    /// </summary>
    [Fact]
    public void R095_ATitleSitsOverItsCellsAndALongCellWidensItsColumn()
    {
        TextTable table = new(new("nr", true, 3), new("clasa", false, 8), new("pauza", true, 5), new("motive", false, 0));
        table.Add("1", "bun", "1 s", "");
        table.Add("2", "la limita", "1500 s", "stele late");

        string[] lines = Written(table);

        Assert.Equal(
            [
                "     nr  clasa       pauza  motive",
                "      1  bun           1 s  ",
                "      2  la limita  1500 s  stele late",
            ],
            lines);
        AssertUnderHeader(lines, ("nr", true), ("clasa", false), ("pauza", true), ("motive", false));
    }

    /// <summary>Without a long cell the columns keep their least widths, and a title longer than that widens its own.</summary>
    [Fact]
    public void R095_AColumnIsAsWideAsItsLeastWidthItsTitleAndItsCells()
    {
        TextTable table = new(new("no", true, 3), new("exposure", true, 4), new("class", false, 8), new("pause", true, 6));
        table.Add("12", "120.0 s", "good", "-");

        Assert.Equal(["     no  exposure  class      pause", "     12   120.0 s  good           -"], Written(table));
    }

    /// <summary>A row may hold its first cells and then one text that runs over the other columns, unpadded.</summary>
    [Fact]
    public void R095_ARowCanHoldItsFirstCellsAndThenOneText()
    {
        TextTable table = new(new("no", true, 3), new("file", false, 12), new("ISO", true, 5));
        table.Add("1", "DSC_0001.NEF", "1600");
        table.AddSpanning("error: cannot be read", "2", "DSC_2.NEF");

        Assert.Equal(["     no  file            ISO", "      1  DSC_0001.NEF   1600", "      2  DSC_2.NEF     error: cannot be read"], Written(table));
    }

    /// <summary>A row with the wrong number of cells is refused, where it is added.</summary>
    [Fact]
    public void R095_ARowWithTheWrongNumberOfCellsIsRefused()
    {
        TextTable table = new(new("a", true, 1), new("b", false, 1));

        Assert.Throws<ArgumentOutOfRangeException>(() => table.Add("1"));
        Assert.Throws<ArgumentOutOfRangeException>(() => table.AddSpanning("rest", "1", "2"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TextTable());
    }
}
