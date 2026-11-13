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

// OutputFile.cs - how every file written for the user reaches the disk (R-138, ADR 0109).

using System;
using System.IO;
using System.Text;
using static FlatCheck.Ui;

namespace FlatCheck
{
    /// <summary>
    /// Writes a file for the user so that the name always holds a whole file: the bytes go to
    /// a temporary file beside the target, which replaces the target in one move once the
    /// write has ended. The existing file is never opened for writing, so a write that fails
    /// half way (a full disk, a drive gone, the process ended) leaves it as it was. Both
    /// applications write their maps, reports and synthetic flats through here.
    /// </summary>
    internal static class OutputFile
    {
        /// <summary>What the temporary file's name adds to the target's: <c>map.png.tmp</c>
        /// beside <c>map.png</c>. One fixed name per target, so a file left by a process
        /// that ended mid-write is overwritten by the next write instead of piling up.</summary>
        internal const string TemporarySuffix = ".tmp";

        /// <summary>
        /// Writes a file through a stream. Throws what <paramref name="write"/> throws, what
        /// creating the temporary file throws (the folder missing or not writable, the name
        /// taken by another write in flight), or an <see cref="IOException"/> naming
        /// <paramref name="path"/> when the finished file cannot replace the target; in each
        /// case the target is as it was and the temporary file is deleted when it can be.
        /// </summary>
        /// <param name="path">The file to write; replaced when it exists. Its folder must
        /// exist. A device (<c>NUL</c>, <c>/dev/null</c>) is written into directly: there is
        /// no earlier file to keep, and a device is not replaced by a move. A folder is
        /// refused with an <see cref="IOException"/> before anything is written.</param>
        /// <param name="write">Writes the whole content into the stream it is given (seekable
        /// for a file); it need not close it.</param>
        internal static void Write(string path, Action<Stream> write)
        {
            if (Directory.Exists(path)) throw new IOException(IsAFolder(path));
            if (IsDevice(path))
            {
                using var device = new FileStream(path, FileMode.Create, FileAccess.Write);
                write(device);
                return;
            }

            string temp = path + TemporarySuffix;
            try
            {
                using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
                    write(stream);
            }
            catch
            {
                DeleteQuietly(temp);
                throw;
            }

            try { File.Move(temp, path, overwrite: true); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                // Windows refuses to replace a file another program holds open, or a
                // read-only one, with "Access to the path is denied.", which names neither
                // the file nor the cause.
                DeleteQuietly(temp);
                throw new IOException(CannotReplace(path, e.Message), e);
            }
        }

        /// <summary>Writes a file from bytes, as <see cref="Write"/> does.</summary>
        /// <param name="path">The file to write; replaced when it exists.</param>
        /// <param name="bytes">The whole content.</param>
        internal static void WriteAllBytes(string path, byte[] bytes)
            => Write(path, stream => stream.Write(bytes, 0, bytes.Length));

        /// <summary>Writes a text file, as <see cref="Write"/> does: UTF-8 without a byte
        /// order mark, what <c>File.WriteAllText</c> writes.</summary>
        /// <param name="path">The file to write; replaced when it exists.</param>
        /// <param name="text">The whole text.</param>
        internal static void WriteAllText(string path, string text)
            => WriteAllBytes(path, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(text));

        /// <summary>The failure of the last step: the new file is whole, but the target
        /// could not be replaced by it.</summary>
        /// <param name="path">The target.</param>
        /// <param name="reason">The system's message for the failed move.</param>
        /// <returns>The message, in the current language.</returns>
        internal static string CannotReplace(string path, string reason)
            => T($"Could not replace {path}: the file is open in another program or is read-only ({reason}).",
                 $"Nu am putut inlocui {path}: fisierul e deschis in alt program sau e doar pentru citire ({reason}).");

        /// <summary>The refusal of an output path that is a folder.</summary>
        /// <param name="path">The path given for the output.</param>
        /// <returns>The message, in the current language.</returns>
        internal static string IsAFolder(string path)
            => T($"{path} is a folder; the output needs a file name.",
                 $"{path} e un folder; iesirea are nevoie de un nume de fisier.");

        /// <summary>
        /// Whether a path names a device and not a file: on Windows a path of the device
        /// namespace, which is what the full path of <c>NUL</c> or <c>CON</c> is
        /// (<c>\\.\NUL</c>); elsewhere a path under <c>/dev/</c>. A file under
        /// <c>/dev/shm</c> counts too, and is then simply written in place.
        /// </summary>
        /// <param name="path">The output path, relative or full.</param>
        /// <returns>True for a device; false for anything else, a path that cannot be
        /// made full included.</returns>
        static bool IsDevice(string path)
        {
            string full;
            try { full = Path.GetFullPath(path); }
            catch (Exception e) when (e is ArgumentException || e is NotSupportedException || e is PathTooLongException) { return false; }
            return OperatingSystem.IsWindows()
                ? full.StartsWith(@"\\.\", StringComparison.Ordinal)
                : full.StartsWith("/dev/", StringComparison.Ordinal);
        }

        /// <summary>Deletes the temporary file of a write that did not end; a failure is
        /// ignored, since the next write overwrites the file anyway.</summary>
        /// <param name="temp">The temporary file; may not exist.</param>
        static void DeleteQuietly(string temp)
        {
            try { File.Delete(temp); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { }
        }
    }
}
