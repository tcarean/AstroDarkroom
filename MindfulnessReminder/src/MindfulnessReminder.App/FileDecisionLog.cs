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
using MindfulnessReminder.Core;

namespace MindfulnessReminder.App;

/// <summary>
/// The decision log as a file: <c>log.txt</c> in <c>%LOCALAPPDATA%\MindfulnessReminder\</c>,
/// one stamped line per event, rotated once to <c>log.1.txt</c> past
/// <see cref="DecisionLog.MaxBytes"/> (R-110; ADR 0037). Every failure is swallowed: a log must
/// never take the tray down.
/// </summary>
internal sealed class FileDecisionLog : IDecisionLog
{
    /// <summary>The folder under the local application data, the one the README names.</summary>
    private const string FolderName = "MindfulnessReminder";

    /// <summary>The file being written.</summary>
    private const string FileName = "log.txt";

    /// <summary>Where the file goes when it is rotated, replacing the previous one.</summary>
    private const string RotatedFileName = "log.1.txt";

    private readonly string _folder;
    private readonly string _path;
    private readonly string _rotatedPath;

    /// <summary>
    /// A log writing under the current user's local application data. When Windows cannot name
    /// that folder, the log writes nowhere rather than into the working directory.
    /// </summary>
    public FileDecisionLog()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        _folder = localAppData.Length == 0 ? string.Empty : Path.Combine(localAppData, FolderName);
        _path = Path.Combine(_folder, FileName);
        _rotatedPath = Path.Combine(_folder, RotatedFileName);
    }

    /// <inheritdoc/>
    public void Write(string text)
    {
        if (_folder.Length == 0)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(_folder);
            Rotate();
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
