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

namespace MindfulnessReminder.Core;

/// <summary>
/// The decision log as a file: <see cref="FileName"/> in the folder it is given, one stamped
/// line per event, rotated once to <see cref="RotatedFileName"/> past
/// <see cref="DecisionLog.MaxBytes"/> (R-110; ADR 0037). Every failure is swallowed: a log must
/// never take the tray down. App names the folder, <c>%LOCALAPPDATA%\MindfulnessReminder</c>;
/// the rules live here so the tests can run them against a folder of their own.
/// </summary>
public sealed class FileDecisionLog : IDecisionLog
{
    /// <summary>The file being written.</summary>
    public const string FileName = "log.txt";

    /// <summary>Where the file goes when it is rotated, replacing the previous one.</summary>
    public const string RotatedFileName = "log.1.txt";

    private readonly string _folder;
    private readonly string _path;
    private readonly string _rotatedPath;

    /// <summary>A log writing into <paramref name="folder"/>, created on the first write.</summary>
    /// <param name="folder">
    /// The folder of the two files; not null. Empty means there is nowhere to write (Windows
    /// could not name the profile folder), and every write is dropped.
    /// </param>
    public FileDecisionLog(string folder)
    {
        ArgumentNullException.ThrowIfNull(folder);

        _folder = folder;
        _path = Path.Combine(folder, FileName);
        _rotatedPath = Path.Combine(folder, RotatedFileName);
    }

    /// <inheritdoc/>
    public void Write(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (_folder.Length == 0)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(_folder);
            Rotate();

            // Stamped with the local clock, read directly: the time is for the owner's eyes,
            // in the owner's time, and no rule depends on it (ADR 0037).
            File.AppendAllText(_path, DecisionLog.FormatLine(DateTimeOffset.Now, text) + Environment.NewLine);
        }
        catch (Exception)
        {
            // A folder that cannot be created, a locked file, a full disk: none of them is a
            // reason to show a dialog or to stop. The line is lost, the application goes on.
        }
    }

    /// <summary>
    /// Moves the file to <see cref="RotatedFileName"/> once it is past the cap, replacing the
    /// previous one. A rotation that fails (the old file held open by an editor or a scanner)
    /// is let go on its own, so the line is still appended and the file grows past the cap
    /// until the next write can rotate it; a log that went silent would be the worse failure.
    /// </summary>
    private void Rotate()
    {
        try
        {
            if (File.Exists(_path) && DecisionLog.ShouldRotate(new FileInfo(_path).Length))
            {
                File.Move(_path, _rotatedPath, overwrite: true);
            }
        }
        catch (Exception)
        {
        }
    }
}
