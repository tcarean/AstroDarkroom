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

// AtomicFile.cs - a file written whole or not at all, the one way the tool writes its two
// files: the report (R-005, R-111) and the FITS with a curve applied (R-145). The content goes
// into a temporary file beside the target and is moved over it once everything is written, so
// that a write that fails midway (a full disk, a share that drops) leaves the old file or none,
// never a truncated one, and no temporary file behind. The temporary name and the delete policy
// live here once, so that the two writers cannot drift apart.

using System;
using System.IO;

namespace AstroPhotoAnalyzer
{
    /// <summary>
    /// Writes a file whole or not at all: into a temporary file beside the target, moved over it
    /// at the end, deleted when the write or the move fails (R-005, R-145).
    /// </summary>
    internal static class AtomicFile
    {
        /// <summary>
        /// Writes <paramref name="target"/> through a temporary file beside it. A target that
        /// exists is opened for writing first, before anything is written, so that one another
        /// program holds open or one that refuses writing is reported with the system's message
        /// for the target itself ("being used by another process", "access is denied"), not
        /// with the final move's, which only says that access is denied. A folder that does
        /// not allow a file to be created beside the target is reported with the target's name
        /// and the folder's, the temporary name being nobody's choice.
        /// </summary>
        /// <param name="target">
        /// The file to write, replaced when it exists; its folder must exist and allow a file to
        /// be created. A malformed path throws ArgumentException (Path.GetFullPath).
        /// </param>
        /// <param name="write">
        /// Writes the whole content to the stream it is given, a new file opened for writing
        /// with a 64 KB buffer; the stream is closed afterwards whether or not it closes it.
        /// Whatever it throws passes on, after the temporary file is deleted.
        /// </param>
        internal static void Write(string target, Action<Stream> write)
        {
            string full = Path.GetFullPath(target);
            if (File.Exists(full))
                using (new FileStream(full, FileMode.Open, FileAccess.Write, FileShare.None)) { }
            string temp = full + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                FileStream o;
                try
                {
                    o = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1 << 16);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    throw new IOException(
                        $"nu se poate crea un fisier in folderul {Path.GetDirectoryName(full)}, langa {Path.GetFileName(full)} "
                        + $"(fisierul se scrie intreg intr-un fisier temporar, mutat peste nume la sfarsit): {e.Message}", e);
                }
                using (o) write(o);
                File.Move(temp, full, overwrite: true);
            }
            finally
            {
                // After a failed write or a refused move the temporary file must not outlive the
                // attempt; a delete refused in its turn changes nothing the caller has to know,
                // the original exception being what matters.
                if (File.Exists(temp))
                {
                    try { File.Delete(temp); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }
        }
    }
}
