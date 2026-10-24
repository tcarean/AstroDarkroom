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

// PreviewImage.cs - the preview developed by the camera, in its two forms.
//
// A raw carries in it the image the camera showed on its screen: debayered, white-balanced,
// with gamma and with the Picture Control tone curve. That is the image the camera computed its
// histogram from - and the difference from the sensor's real level is tens of points, larger on
// a flat than anywhere. The types here exist so that the two can be put side by side.
//
// NEITHER of them may decide anything. The preview is not a measurement of the sensor; it is an
// illustration of what the person saw. The exposure verdict stays on the raw.
//
// Two forms, because they cost differently: EmbeddedPreview is what is copied while the file is
// open anyway (compressed bytes, cheap), PreviewImage is what comes out after decoding (pixels,
// expensive) - and the decoding is done only on demand, see ExposureRun.

namespace FlatCheck
{
    /// <summary>
    /// The preview as it sits in the file: either JPEG bytes, or an RGB bitmap libraw has
    /// already decoded. Captured while the file is open and decoded only on demand.
    /// </summary>
    internal sealed class EmbeddedPreview
    {
        internal readonly bool IsJpeg;

        /// <summary>
        /// Compressed JPEG, or 8-bit interleaved RGB when <see cref="IsJpeg"/> is false.
        /// </summary>
        internal readonly byte[] Data;

        /// <summary>From the metadata; for a JPEG, the size announced before decoding.</summary>
        internal readonly int W, H;

        /// <summary>
        /// The orientation libraw reports. Informative: the centre window does not depend on it.
        /// </summary>
        internal readonly int Flip;

        /// <summary>For the report: "previzualizare JPEG 1620x1080 din fisier".</summary>
        internal readonly string Source;

        /// <summary>A preview captured from the file.</summary>
        /// <param name="isJpeg">True for a JPEG stream, false for an RGB bitmap.</param>
        /// <param name="data">The bytes, as <see cref="Data"/> describes them.</param>
        /// <param name="w">The width in pixels, from the metadata.</param>
        /// <param name="h">The height in pixels, from the metadata.</param>
        /// <param name="flip">libraw's orientation code.</param>
        /// <param name="source">The report's description of the preview.</param>
        internal EmbeddedPreview(bool isJpeg, byte[] data, int w, int h, int flip, string source)
        {
            IsJpeg = isJpeg; Data = data; W = w; H = h; Flip = flip; Source = source;
        }
    }

    /// <summary>
    /// The preview as pixels. A core type with nothing third-party in it, so that nothing
    /// downstream knows where the pixels came from - the same discipline as for
    /// <see cref="Img"/>.
    /// </summary>
    internal sealed class PreviewImage
    {
        internal readonly int H, W;

        /// <summary>H*W*3, interleaved R,G,B, 0..255.</summary>
        internal readonly byte[] Rgb;

        internal readonly string Source;

        /// <summary>A decoded preview.</summary>
        /// <param name="h">The height in pixels.</param>
        /// <param name="w">The width in pixels.</param>
        /// <param name="rgb">H*W*3 bytes, interleaved R,G,B.</param>
        /// <param name="source">The report's description of the preview.</param>
        internal PreviewImage(int h, int w, byte[] rgb, string source)
        {
            H = h; W = w; Rgb = rgb; Source = source;
        }

        /// <summary>One channel of one pixel.</summary>
        /// <param name="y">Row, 0 at the top.</param>
        /// <param name="x">Column, 0 at the left.</param>
        /// <param name="c">The channel: 0 R, 1 G, 2 B.</param>
        /// <returns>The value, 0..255.</returns>
        internal byte At(int y, int x, int c) => Rgb[(y * W + x) * 3 + c];
    }
}
