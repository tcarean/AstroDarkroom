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

namespace ImageVisualCompare.Core;

/// <summary>
/// Turns rows of sixteen-bit samples into Bgra32 rows under a <see cref="DisplayMode"/>
/// (ADR 0040). A row holds its planes one after another, the file's order red, green, blue, or
/// the single plane of a gray image; the width in samples each. <see cref="DisplayMode.Linear"/>
/// renders at once; the other modes count every row first, then <see cref="Prepare"/> builds
/// the lookups, then every row is rendered.
/// </summary>
public sealed class DisplayRenderer
{
    private readonly int _width;
    private readonly int _planes;
    private readonly SampleHistogram[] _histograms;
    private readonly LevelLookup[] _lookups;
    private readonly ushort[] _layer;

    /// <summary>Builds a renderer for images of one size and plane count.</summary>
    /// <param name="mode">The display mode.</param>
    /// <param name="width">The width of a row, in samples a plane, at least 1.</param>
    /// <param name="planes">1 for a gray image, 3 for red, green and blue.</param>
    public DisplayRenderer(DisplayMode mode, int width, int planes)
    {
        if (planes != 1 && planes != 3)
        {
            throw new ArgumentOutOfRangeException(nameof(planes), planes, "A row has one or three planes.");
        }

        Mode = mode;
        _width = width;
        _planes = planes;

        // The histogram mode counts each plane; the false colours count one layer, the mean of
        // the planes; the linear mode counts nothing and looks every plane up the same way.
        int layers = mode == DisplayMode.Histogram ? planes : 1;
        _histograms = new SampleHistogram[mode == DisplayMode.Linear ? 0 : layers];
        for (int i = 0; i < _histograms.Length; i++)
        {
            _histograms[i] = new SampleHistogram();
        }

        _lookups = new LevelLookup[layers];
        Array.Fill(_lookups, LevelLookup.Linear);
        _layer = mode == DisplayMode.FalseColours && planes == 3 ? new ushort[width] : [];
    }

    /// <summary>The mode the rows are rendered in.</summary>
    public DisplayMode Mode { get; }

    /// <summary>True when every row has to be counted before any is rendered.</summary>
    public bool NeedsCount => Mode != DisplayMode.Linear;

    /// <summary>Counts one row towards the histograms; does nothing in the linear mode.</summary>
    /// <param name="row">The row's planes one after another, <c>planes × width</c> samples.</param>
    public void Count(ReadOnlySpan<ushort> row)
    {
        switch (Mode)
        {
            case DisplayMode.Histogram:
                for (int plane = 0; plane < _planes; plane++)
                {
                    _histograms[plane].Count(row.Slice(plane * _width, _width));
                }

                break;

            case DisplayMode.FalseColours:
                _histograms[0].Count(Layer(row));
                break;
        }
    }

    /// <summary>
    /// Forgets the counts, for a second counting pass when the file turns out to count to
    /// 65 535 (R-016).
    /// </summary>
    public void ClearCounts()
    {
        foreach (SampleHistogram histogram in _histograms)
        {
            histogram.Clear();
        }
    }

    /// <summary>
    /// Builds the lookups from the counts; needed once, after the last <see cref="Count"/> and
    /// before the first <see cref="Render"/>. Does nothing in the linear mode.
    /// </summary>
    public void Prepare()
    {
        for (int i = 0; i < _histograms.Length; i++)
        {
            _lookups[i] = LevelLookup.Equalising(_histograms[i]);
        }
    }

    /// <summary>Renders one row.</summary>
    /// <param name="row">The row's planes one after another, <c>planes × width</c> samples.</param>
    /// <param name="bgra">
    /// Receives the row: <c>width</c> pixels of blue, green, red and alpha, with alpha 255.
    /// </param>
    public void Render(ReadOnlySpan<ushort> row, Span<byte> bgra)
    {
        int width = _width;

        if (Mode == DisplayMode.FalseColours)
        {
            ReadOnlySpan<ushort> layer = Layer(row);
            byte[] table = _lookups[0].Table;
            for (int x = 0; x < width; x++)
            {
                RainbowTable.WriteBgra(table[layer[x]], bgra.Slice(x * 4, 4));
            }

            return;
        }

        if (Mode == DisplayMode.Linear)
        {
            RenderLinear(row, bgra);
            return;
        }

        if (_planes == 1)
        {
            byte[] table = _lookups[0].Table;
            for (int x = 0; x < width; x++)
            {
                byte level = table[row[x]];
                bgra[x * 4] = level;
                bgra[(x * 4) + 1] = level;
                bgra[(x * 4) + 2] = level;
                bgra[(x * 4) + 3] = 255;
            }

            return;
        }

        for (int plane = 0; plane < 3; plane++)
        {
            // Red, green, blue in the row; blue, green, red in the bitmap.
            byte[] table = _lookups[plane].Table;
            ReadOnlySpan<ushort> samples = row.Slice(plane * width, width);
            int channel = 2 - plane;
            for (int x = 0; x < width; x++)
            {
                bgra[(x * 4) + channel] = table[samples[x]];
            }
        }

        for (int x = 0; x < width; x++)
        {
            bgra[(x * 4) + 3] = 255;
        }
    }

    /// <summary>
    /// The linear mode's row: the high byte of each sample, which is what
    /// <see cref="LevelLookup.Linear"/> holds, taken by a shift rather than through the table.
    /// The 64 KB table does not fit the first-level cache, and on a 23 megapixel file the
    /// difference between the two was measured (ADR 0040).
    /// </summary>
    /// <param name="row">The row's planes one after another.</param>
    /// <param name="bgra">Receives the row, four bytes a pixel, alpha 255.</param>
    private void RenderLinear(ReadOnlySpan<ushort> row, Span<byte> bgra)
    {
        int width = _width;
        if (_planes == 1)
        {
            for (int x = 0; x < width; x++)
            {
                byte level = (byte)(row[x] >> 8);
                bgra[x * 4] = level;
                bgra[(x * 4) + 1] = level;
                bgra[(x * 4) + 2] = level;
                bgra[(x * 4) + 3] = 255;
            }

            return;
        }

        ReadOnlySpan<ushort> red = row[..width];
        ReadOnlySpan<ushort> green = row.Slice(width, width);
        ReadOnlySpan<ushort> blue = row.Slice(2 * width, width);
        for (int x = 0; x < width; x++)
        {
            Span<byte> pixel = bgra.Slice(x * 4, 4);
            pixel[0] = (byte)(blue[x] >> 8);
            pixel[1] = (byte)(green[x] >> 8);
            pixel[2] = (byte)(red[x] >> 8);
            pixel[3] = 255;
        }
    }

    /// <summary>
    /// The one layer the false colours show: the plane of a gray image, or the mean of the
    /// three planes rounded to the nearest (R-112).
    /// </summary>
    /// <param name="row">The row's planes one after another.</param>
    /// <returns>The layer, <c>width</c> samples.</returns>
    private ReadOnlySpan<ushort> Layer(ReadOnlySpan<ushort> row)
    {
        if (_planes == 1)
        {
            return row[.._width];
        }

        int width = _width;
        ushort[] layer = _layer;
        for (int x = 0; x < width; x++)
        {
            layer[x] = (ushort)((row[x] + row[width + x] + row[(2 * width) + x] + 1) / 3);
        }

        return layer;
    }
}
