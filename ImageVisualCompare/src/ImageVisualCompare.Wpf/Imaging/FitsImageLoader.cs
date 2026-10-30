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

using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using ImageVisualCompare.Core;
using ImageVisualCompare.Wpf.Logging;
using NLog;

namespace ImageVisualCompare.Wpf.Imaging;

/// <summary>
/// Reads FITS, which WIC has no codec for, with the project's own reader in Core, and arrives
/// where <see cref="WicImageLoader"/> does: a frozen Bgra32 bitmap and what the file was. The
/// values are shown as they are stored, with no stretch (ADR 0029), or through the display mode
/// asked for (ADR 0040).
/// </summary>
public sealed class FitsImageLoader : IImageLoader
{
    /// <summary>The most header bytes <see cref="LogCards"/> reads, 1 MiB: 13 107 cards.</summary>
    private const int MaxHeaderBytes = 1024 * 1024;

    /// <summary>The size of one FITS card, in bytes.</summary>
    private const int CardBytes = 80;

    /// <summary>What the FITS reader made of a file, in the diagnostic log (R-121).</summary>
    private static readonly Logger Log = LogManager.GetLogger(Loggers.Fits);

    /// <summary>
    /// Reads only the FITS header, to check that a pair matches before either is decoded. A
    /// file this program cannot show throws as <see cref="FitsHeader.ReadLayout"/> does. At the
    /// log's <c>Trace</c> level the headers are read once more, for their cards
    /// (<see cref="LogCards"/>), which changes nothing the probe returns or throws.
    /// </summary>
    /// <param name="path">The file.</param>
    /// <returns>
    /// The image's size, depth and planes, with no value range: whether a float file counts in
    /// 0 to 1 or in 0 to 65535 shows only in its samples.
    /// </returns>
    public ImageInfo Probe(string path)
    {
        try
        {
            using FileStream stream = OpenRead(path);
            FitsImageLayout layout = FitsHeader.ReadLayout(stream);
            LogLayout("probed", path, layout);
            return layout.Describe(path);
        }
        finally
        {
            // At Trace the header's cards, of a file the reader refused too: its header is what
            // says why (R-122). The pair loader probes every file before it decodes one.
            if (Log.IsTraceEnabled)
            {
                LogCards(path);
            }
        }
    }

    /// <summary>Decodes a FITS on a background thread, as <see cref="Load"/> does.</summary>
    /// <param name="path">The file.</param>
    /// <param name="mode">How the samples become pixels; every mode is honoured.</param>
    /// <param name="cancellationToken">
    /// Looked at before the decode starts and before each row; a stopped load throws
    /// <see cref="OperationCanceledException"/>.
    /// </param>
    /// <returns>
    /// The bitmap, and what the file was, with the range its values were read in.
    /// </returns>
    public Task<LoadedImage> LoadAsync(
        string path, DisplayMode mode = DisplayMode.Linear, CancellationToken cancellationToken = default) =>
        Task.Run(() => Load(path, mode, cancellationToken), cancellationToken);

    /// <summary>
    /// Decodes a FITS on the calling thread into a frozen Bgra32 bitmap, the values as stored
    /// and rendered in the mode asked for. A file this program cannot show throws as
    /// <see cref="FitsHeader.ReadLayout"/> does, and so does an image too large for a bitmap.
    /// </summary>
    /// <param name="path">The file.</param>
    /// <param name="mode">How the samples become pixels; every mode is honoured.</param>
    /// <param name="cancellationToken">
    /// Looked at before each row; a stopped load throws <see cref="OperationCanceledException"/>.
    /// </param>
    /// <returns>
    /// The bitmap, and what the file was, with the range its values were read in.
    /// </returns>
    public LoadedImage Load(string path, DisplayMode mode = DisplayMode.Linear, CancellationToken cancellationToken = default)
    {
        using FileStream stream = OpenRead(path);
        FitsImageLayout layout = FitsHeader.ReadLayout(stream);
        LogLayout("decoding", path, layout);

        WriteableBitmap bitmap = CreateBitmap(layout);
        var renderer = new DisplayRenderer(mode, layout.Width, layout.Planes);
        double fullScale = FitsScale.FullScale(layout.BitPix);
        long samples = (long)layout.Width * layout.Height * layout.Planes;
        string? valueRange = null;

        // Whether a float file counts in 0 to 1 or in 0 to 65535 is known only once every
        // sample has been seen. The rare file that turns out to be the second pays for a
        // second pass; holding the floats to avoid it would cost every file 277 MB. A mode
        // that equalises needs every sample before any pixel too, so it counts first and
        // renders after, and the rescaled file counts twice.
        long above;
        if (renderer.NeedsCount)
        {
            above = Pass(layout, stream, renderer, fullScale, bitmap: null, cancellationToken);
            if (FitsScale.IsRescaled(layout.BitPix, above, samples))
            {
                fullScale = FitsScale.FullScale(layout.BitPix, rescaled: true);
                valueRange = FitsScale.SixteenBitRange;
                renderer.ClearCounts();
                Pass(layout, stream, renderer, fullScale, bitmap: null, cancellationToken);
            }

            renderer.Prepare();
            Pass(layout, stream, renderer, fullScale, bitmap, cancellationToken);
        }
        else
        {
            above = Pass(layout, stream, renderer, fullScale, bitmap, cancellationToken);
            if (FitsScale.IsRescaled(layout.BitPix, above, samples))
            {
                Pass(layout, stream, renderer, FitsScale.FullScale(layout.BitPix, rescaled: true), bitmap, cancellationToken);
                valueRange = FitsScale.SixteenBitRange;
            }
        }

        // An integer file has no such decision to make: its range is its BITPIX's.
        if (layout.IsFloat)
        {
            Log.Debug(path + ": " + LogText.FloatRange(above, samples, rescaled: valueRange is not null));
        }

        bitmap.Freeze();
        return new LoadedImage(layout.Describe(path, valueRange), bitmap, mode);
    }

    /// <summary>
    /// Writes a FITS's layout, at <c>Debug</c>: everything <see cref="FitsHeader.ReadLayout"/>
    /// found, which decides how the samples are read.
    /// </summary>
    /// <param name="stage">What is being done to the file: <c>probed</c> or <c>decoding</c>.</param>
    /// <param name="path">The file.</param>
    /// <param name="layout">Its layout.</param>
    private static void LogLayout(string stage, string path, FitsImageLayout layout)
    {
        if (Log.IsDebugEnabled)
        {
            // BZERO and BSCALE exactly: six digits would write an unsigned 32-bit file's
            // 2147483648 as 2.14748E+09.
            Log.Debug(stage + " " + path + ": " + LogText.Size(layout.Width, layout.Height)
                + " planes=" + LogText.Int(layout.Planes) + " bitpix=" + LogText.Int(layout.BitPix)
                + " bzero=" + LogText.Exact(layout.BZero) + " bscale=" + LogText.Exact(layout.BScale)
                + " data at " + LogText.Int(layout.DataOffset));
        }
    }

    /// <summary>
    /// Writes the headers' cards, at <c>Trace</c>, one line each (R-122), read from the file on a
    /// stream of their own, without the reader: a header the reader refuses is written too, which
    /// is when its cards matter most. Each header is walked card by card to its <c>END</c>; the
    /// next one is the first 2880-byte block after it that starts with <c>XTENSION</c>, past the
    /// data of the unit before, as the reader steps over a table or random groups to the image.
    /// A byte that is not printable ASCII, a tab or a UTF-8 letter in a value, is written as
    /// <c>?</c>; a card that is mostly such bytes is data, not a header, and ends the walk, as
    /// does the end of <see cref="MaxHeaderBytes"/>. A blank card is left out. A file that cannot
    /// be read is a line at <c>Debug</c>: the log never changes what the probe does.
    /// </summary>
    /// <param name="path">The file.</param>
    private static void LogCards(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            byte[] bytes = new byte[(int)Math.Min(stream.Length, MaxHeaderBytes)];
            int read = stream.ReadAtLeast(bytes, bytes.Length, throwOnEndOfStream: false);
            int at = 0;
            while (at + CardBytes <= read)
            {
                ReadOnlySpan<byte> card = bytes.AsSpan(at, CardBytes);
                if (card.Length - card.Count((byte)' ') - CountPrintable(card) > CardBytes / 2)
                {
                    return;
                }

                if (card.IndexOfAnyExcept((byte)' ') >= 0)
                {
                    Log.Trace("card " + Printable(card));
                }

                at += CardBytes;
                if (card[..8].SequenceEqual("END     "u8))
                {
                    // The header ends with its 2880-byte block. The next header, an extension's,
                    // starts a later block, after the data of this unit when it has any.
                    at = (at + FitsHeader.BlockSize - 1) / FitsHeader.BlockSize * FitsHeader.BlockSize;
                    while (at + 8 <= read && !bytes.AsSpan(at, 8).SequenceEqual("XTENSION"u8))
                    {
                        at += FitsHeader.BlockSize;
                    }
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            Log.Debug(ex, "the header cards of " + path + " could not be read");
        }
    }

    /// <summary>Counts the bytes of a card that are printable ASCII, the space aside.</summary>
    /// <param name="card">The card's 80 bytes.</param>
    /// <returns>How many of its bytes are in 0x21 to 0x7E.</returns>
    private static int CountPrintable(ReadOnlySpan<byte> card)
    {
        int count = 0;
        foreach (byte b in card)
        {
            if (b is > 0x20 and <= 0x7E)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Writes a card for the log: each byte that is not printable ASCII as <c>?</c>, so that no
    /// control byte reaches the file, and the spaces at its end left out.
    /// </summary>
    /// <param name="card">The card's 80 bytes.</param>
    /// <returns>The card's text.</returns>
    private static string Printable(ReadOnlySpan<byte> card)
    {
        Span<char> text = stackalloc char[CardBytes];
        for (int i = 0; i < card.Length; i++)
        {
            text[i] = card[i] is >= 0x20 and <= 0x7E ? (char)card[i] : '?';
        }

        return new string(text[..card.Length]).TrimEnd();
    }

    /// <summary>
    /// Creates the bitmap the rows are written into, through <see cref="Bitmaps.Create"/>: an
    /// image too large for one is the FITS reader's too-large message.
    /// </summary>
    /// <param name="layout">The image whose size the bitmap takes.</param>
    /// <returns>An empty Bgra32 bitmap of the image's size, at 96 dpi.</returns>
    private static WriteableBitmap CreateBitmap(FitsImageLayout layout) =>
        Bitmaps.Create(layout.Width, layout.Height, fits: true);

    /// <summary>
    /// One pass over every row, in the order of the file: each row read as sixteen-bit samples
    /// and either counted towards the renderer's histograms or rendered into the bitmap, where
    /// it belongs in the image.
    /// </summary>
    /// <param name="layout">Where the image's samples are and how they are stored.</param>
    /// <param name="stream">The open file; each row is read from where it is.</param>
    /// <param name="renderer">Counts or renders the rows, in the mode asked for.</param>
    /// <param name="fullScale">
    /// The value shown as white (<see cref="FitsScale.FullScale"/>).
    /// </param>
    /// <param name="bitmap">
    /// Receives the rows, the image's size, Bgra32, not yet frozen; null for a pass that only
    /// counts.
    /// </param>
    /// <param name="cancellationToken">
    /// Looked at before each row; a stopped pass throws <see cref="OperationCanceledException"/>.
    /// </param>
    /// <returns>
    /// How many float samples were above <see cref="FitsScale.RescaleThreshold"/>; 0 for an
    /// integer file.
    /// </returns>
    private static long Pass(
        FitsImageLayout layout,
        Stream stream,
        DisplayRenderer renderer,
        double fullScale,
        WriteableBitmap? bitmap,
        CancellationToken cancellationToken)
    {
        var decoder = new FitsRowDecoder(layout, stream, fullScale);
        ushort[] planesRow = new ushort[decoder.RowSamples];
        int stride = layout.Width * 4;
        byte[] row = new byte[stride];

        // In the order of the file, whichever way up the image is, so the disk is read forwards.
        for (int fileRow = 0; fileRow < layout.Height; fileRow++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            decoder.ReadFileRow(fileRow, planesRow);
            if (bitmap is null)
            {
                renderer.Count(planesRow);
            }
            else
            {
                renderer.Render(planesRow, row);
                bitmap.WritePixels(new Int32Rect(0, layout.DisplayRow(fileRow), layout.Width, 1), row, stride, 0);
            }
        }

        return decoder.SamplesAboveThreshold;
    }

    // No buffer of the stream's own: the decoder reads one whole row at each of three places
    // in turn, and a buffer would be filled past the row and thrown away at every seek.
    /// <summary>
    /// Opens a file for reads here and there in it, sharing it with other readers.
    /// </summary>
    /// <param name="path">The file.</param>
    /// <returns>The open stream, with no buffer of its own.</returns>
    private static FileStream OpenRead(string path) =>
        new(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 1, FileOptions.RandomAccess);
}
