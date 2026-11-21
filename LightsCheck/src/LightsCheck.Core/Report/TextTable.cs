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

namespace LightsCheck.Core;

/// <summary>One column of a <see cref="TextTable"/>.</summary>
/// <param name="Header">The column's title, in the run's language; not null.</param>
/// <param name="Right">
/// True when the title and the cells are set against the column's right edge (figures), false
/// when against its left (words).
/// </param>
/// <param name="Least">
/// The least width of the column, in characters; it grows to its title and to its widest cell.
/// </param>
internal readonly record struct TextColumn(string Header, bool Right, int Least);

/// <summary>
/// A table of the console: a header and rows written with the same column widths, so that a
/// title sits over its cells in either language and a long cell widens its column for every
/// row instead of pushing its own (R-095). The columns are two spaces apart; the last one,
/// when its cells are words, is not padded.
/// </summary>
internal sealed class TextTable
{
    private const string Gap = "  ";
    private readonly TextColumn[] _columns;
    private readonly List<(string[] Cells, string? After)> _rows = [];

    /// <summary>Starts a table with the given columns and no row.</summary>
    /// <param name="columns">The columns, left to right; at least one.</param>
    internal TextTable(params TextColumn[] columns)
    {
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentOutOfRangeException.ThrowIfZero(columns.Length);
        _columns = columns;
    }

    /// <summary>Adds a row with one cell for each column.</summary>
    /// <param name="cells">The cells, left to right, as many as the columns; none null.</param>
    internal void Add(params string[] cells)
    {
        ArgumentNullException.ThrowIfNull(cells);
        ArgumentOutOfRangeException.ThrowIfNotEqual(cells.Length, _columns.Length);
        _rows.Add((cells, null));
    }

    /// <summary>
    /// Adds a row that has cells for its first columns only and then one text that runs over
    /// the rest, such as the error of a frame that could not be read.
    /// </summary>
    /// <param name="rest">The text written after the cells, not padded; not null.</param>
    /// <param name="cells">The cells of the first columns, left to right; fewer than the columns.</param>
    internal void AddSpanning(string rest, params string[] cells)
    {
        ArgumentNullException.ThrowIfNull(rest);
        ArgumentNullException.ThrowIfNull(cells);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(cells.Length, _columns.Length);
        _rows.Add((cells, rest));
    }

    /// <summary>Writes the header, then the rows in the order they were added.</summary>
    /// <param name="w">Where the lines go; not null.</param>
    /// <param name="indent">The text every line starts with; not null.</param>
    internal void Write(TextWriter w, string indent)
    {
        ArgumentNullException.ThrowIfNull(w);
        ArgumentNullException.ThrowIfNull(indent);
        int[] widths = new int[_columns.Length];
        for (int c = 0; c < widths.Length; c++)
        {
            widths[c] = Math.Max(_columns[c].Least, _columns[c].Header.Length);
            foreach ((string[] cells, _) in _rows)
            {
                if (c < cells.Length)
                {
                    widths[c] = Math.Max(widths[c], cells[c].Length);
                }
            }
        }

        w.WriteLine(indent + Line(_columns.Select(c => c.Header).ToArray(), null, widths));
        foreach ((string[] cells, string? rest) in _rows)
        {
            w.WriteLine(indent + Line(cells, rest, widths));
        }
    }

    /// <summary>One line of the table: the cells padded to their columns, then the spanning text.</summary>
    /// <param name="cells">The cells of the line's first columns.</param>
    /// <param name="rest">The text that runs over the other columns; null for a full row.</param>
    /// <param name="widths">The width of each column, in characters.</param>
    /// <returns>The line without its indent.</returns>
    private string Line(string[] cells, string? rest, int[] widths)
    {
        StringBuilder line = new();
        for (int c = 0; c < cells.Length; c++)
        {
            if (c > 0)
            {
                line.Append(Gap);
            }

            // A last column of words ends the line: padding it would only add blanks.
            bool open = c == _columns.Length - 1 && !_columns[c].Right;
            line.Append(_columns[c].Right ? cells[c].PadLeft(widths[c]) : open ? cells[c] : cells[c].PadRight(widths[c]));
        }

        if (rest is not null)
        {
            line.Append(Gap).Append(rest);
        }

        return line.ToString();
    }
}
