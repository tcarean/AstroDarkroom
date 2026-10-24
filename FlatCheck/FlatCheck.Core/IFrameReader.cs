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

// IFrameReader.cs - the contract every input format implements.
//
// The implementations are the only ones that know a given format. ImageLoader chooses between
// them through this contract and knows none of them by name, so a new format is added without
// touching the dispatcher - and, more importantly, without any difference in behaviour between
// formats being able to slip into it.

namespace FlatCheck
{
    /// <summary>
    /// One input format: claims its files by extension, then reads a frame with or without its
    /// pixels.
    /// </summary>
    internal interface IFrameReader
    {
        /// <summary>
        /// Does this reader claim the file? The decision is on the EXTENSION, not on the
        /// content. If the bytes were sniffed, a raw that libraw does not know could fall
        /// silently onto the FITS reader and come out of it as noise, instead of an error.
        /// </summary>
        /// <param name="path">The file's path; only its extension is looked at.</param>
        /// <returns>True when this reader is the one for the file.</returns>
        bool Handles(string path);

        /// <summary>
        /// Reads the frame: its pixels and what the file says about it.
        /// </summary>
        /// <param name="path">A file this reader claims (<see cref="Handles"/>).</param>
        /// <returns>The frame, with <see cref="LoadedFrame.Image"/> filled.</returns>
        LoadedFrame Load(string path);

        /// <summary>
        /// The same frame, but WITHOUT pixels: <see cref="LoadedFrame.Image"/> stays null and
        /// only what the file says about it is filled in - the header, the focal length, the
        /// aperture, the camera, the lens. It exists because identifying the lens and prefilling
        /// the Synthetic tab need metadata, and a 24 MP decode for an aperture is a waste that
        /// makes the opening chain wait.
        /// </summary>
        /// <param name="path">A file this reader claims (<see cref="Handles"/>).</param>
        /// <returns>The frame's metadata, its image null.</returns>
        LoadedFrame LoadInfo(string path);
    }
}
