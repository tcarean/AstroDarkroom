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

// LogText.cs - how a value is written in the diagnostic log: English, invariant numbers whatever
// the system's culture, a missing value as "none", sizes in MiB. Every log line is composed from
// these pieces, never through UiText: the log reads the same whatever --lang says (R-121).
// Nothing here decides anything.

using System.Globalization;
using System.IO;
using ImageVisualCompare.Core;

namespace ImageVisualCompare.Wpf.Logging;

/// <summary>The diagnostic log's way of writing values.</summary>
internal static class LogText
{
    /// <summary>The invariant culture, for every number in the log.</summary>
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Writes a number with six significant digits, invariant.</summary>
    /// <param name="value">The number; NaN and the infinities are written as .NET names them.</param>
    /// <returns>The text: <c>0.123457</c>, <c>32768</c>, <c>1.25</c>.</returns>
    internal static string Num(double value) => value.ToString("G6", Inv);

    /// <summary>
    /// Writes a number exactly, as the shortest text that reads back to the same value, invariant:
    /// for a value a reader must see whole, such as a FITS's <c>BZERO</c>.
    /// </summary>
    /// <param name="value">The number.</param>
    /// <returns>The text: <c>2147483648</c>, <c>1.0000001</c>, <c>0</c>.</returns>
    internal static string Exact(double value) => value.ToString("R", Inv);

    /// <summary>Writes an integer, invariant.</summary>
    /// <param name="value">The integer.</param>
    /// <returns>The text, without separators: <c>-32</c>, <c>2880</c>.</returns>
    internal static string Int(long value) => value.ToString(Inv);

    /// <summary>Writes an elapsed time in milliseconds, the log's short form of a time.</summary>
    /// <param name="milliseconds">The time, in whole milliseconds.</param>
    /// <returns>The text: <c>1141 ms</c>.</returns>
    internal static string Ms(long milliseconds) => Int(milliseconds) + " ms";

    /// <summary>
    /// Writes an elapsed time in seconds with one decimal, the log's long form of a time, never
    /// whole seconds, so that <c>0.8 s</c> does not read as nothing.
    /// </summary>
    /// <param name="elapsed">The time.</param>
    /// <returns>The text: <c>12.9 s</c>.</returns>
    internal static string Seconds(TimeSpan elapsed) => elapsed.TotalSeconds.ToString("F1", Inv) + " s";

    /// <summary>Writes a text that may be missing or empty.</summary>
    /// <param name="text">The text; null or empty when there is none.</param>
    /// <returns>The text itself, or <c>none</c>.</returns>
    internal static string Str(string? text) => string.IsNullOrEmpty(text) ? "none" : text;

    /// <summary>Writes a yes or no.</summary>
    /// <param name="value">The value.</param>
    /// <returns><c>yes</c> or <c>no</c>.</returns>
    internal static string Yes(bool value) => value ? "yes" : "no";

    /// <summary>Writes a size in mebibytes with one decimal.</summary>
    /// <param name="bytes">The size, in bytes.</param>
    /// <returns>The text: <c>92.2 MiB</c>.</returns>
    internal static string MiB(long bytes) => (bytes / (1024.0 * 1024.0)).ToString("F1", Inv) + " MiB";

    /// <summary>
    /// Writes a file's size in mebibytes, or a question mark when it cannot be read: the log
    /// never throws for a size.
    /// </summary>
    /// <param name="path">The file.</param>
    /// <returns>The text: <c>92.2 MiB</c>, or <c>?</c>.</returns>
    internal static string FileMiB(string path)
    {
        try
        {
            return MiB(new FileInfo(path).Length);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return "?";
        }
    }

    /// <summary>Writes a width and a height in pixels.</summary>
    /// <param name="width">The width, in pixels.</param>
    /// <param name="height">The height, in pixels.</param>
    /// <returns>The text: <c>5938x3888</c>.</returns>
    internal static string Size(long width, long height) => Int(width) + "x" + Int(height);

    /// <summary>Writes a zoom scale as a percentage with at most one decimal.</summary>
    /// <param name="scale">The scale, 1 for one image pixel per physical pixel.</param>
    /// <returns>The text: <c>152.3%</c>, <c>100%</c>.</returns>
    internal static string Percent(double scale) => (scale * 100).ToString("0.#", Inv) + "%";

    /// <summary>
    /// Writes what a probe found a file to be, in the log's own words, never
    /// <see cref="ImageInfo.FormatDescription"/>, which is the interface's.
    /// </summary>
    /// <param name="info">What the file turned out to be.</param>
    /// <returns>
    /// The text: <c>5938x3888 bits=16 channels=Rgb float=no asStored=no</c>.
    /// </returns>
    internal static string Image(ImageInfo info) =>
        Size(info.PixelWidth, info.PixelHeight)
        + " bits=" + Int(info.BitsPerChannel)
        + " channels=" + info.Channels
        + " float=" + Yes(info.IsFloat)
        + " asStored=" + Yes(info.ShownAsStored);

    /// <summary>Writes what only a decode finds out about a file, and the mode it was drawn in.</summary>
    /// <param name="info">What the file turned out to be, after its decode.</param>
    /// <param name="mode">The mode its pixels were rendered in.</param>
    /// <returns>The text: <c>range=0..65535 mode=Histogram</c>, <c>range=none mode=Linear</c>.</returns>
    internal static string Decoded(ImageInfo info, DisplayMode mode) => "range=" + Str(info.ValueRange) + " mode=" + mode;

    /// <summary>
    /// Writes the decision a float file's samples led to, a FITS's or a float TIFF's: whether they
    /// count in 0 to 1 or in 0 to 65535, and the count it was made on (<see cref="FitsScale.IsRescaled"/>).
    /// </summary>
    /// <param name="above">How many samples are finite and above <see cref="FitsScale.RescaleThreshold"/>.</param>
    /// <param name="samples">How many samples the image has, all planes.</param>
    /// <param name="rescaled">True when the samples were read as 0 to 65535.</param>
    /// <returns>The text: <c>float samples counted in 0..65535: 51200 of 76800 above 2</c>.</returns>
    internal static string FloatRange(long above, long samples, bool rescaled) =>
        "float samples counted in " + (rescaled ? FitsScale.SixteenBitRange : "0..1") + ": "
        + Int(above) + " of " + Int(samples) + " above " + Num(FitsScale.RescaleThreshold);

    /// <summary>Writes a text between double quotes, so that spaces at its ends can be seen.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The text in quotes: <c>"de"</c>.</returns>
    internal static string Quoted(string text) => "\"" + text + "\"";

    /// <summary>
    /// Writes a text of several lines, a message the user was shown, on one line of the log, so
    /// that the message stays on the line that begins with its time; only an exception's text
    /// goes on the lines below.
    /// </summary>
    /// <param name="text">The text, its lines ended by <c>\r\n</c> or <c>\n</c>.</param>
    /// <returns>The same text with each line break written as <c> | </c>.</returns>
    internal static string OneLine(string text) => text.Replace("\r\n", " | ").Replace("\n", " | ");

    /// <summary>Writes a Windows result code, an HRESULT, as Windows documents them.</summary>
    /// <param name="result">The code; 0 for success.</param>
    /// <returns>The text: <c>0x00000000</c>, <c>0x80070057</c>.</returns>
    internal static string Hex(int result) => "0x" + result.ToString("X8", Inv);
}
