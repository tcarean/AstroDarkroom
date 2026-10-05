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
using Microsoft.Win32;

namespace ImageVisualCompare.Wpf.Services;

public interface IFileDialogService
{
    /// <summary>
    /// Asks for the two images in turn. Returns null if the user cancels either dialog.
    /// </summary>
    ImagePairSelection? PickImagePair(string? initialDirectory = null);
}

public sealed record ImagePairSelection(string BeforePath, string AfterPath);

public sealed class FileDialogService : IFileDialogService
{
    // TIFF comes free with the WIC codec set and Siril exports it, so there is no reason to
    // turn those files away. FITS is what Siril works in; the project's own reader opens it.
    private const string Filter =
        "Images (*.png;*.tif;*.tiff;*.fit;*.fits;*.fts)|*.png;*.tif;*.tiff;*.fit;*.fits;*.fts|" +
        "PNG (*.png)|*.png|" +
        "TIFF (*.tif;*.tiff)|*.tif;*.tiff|" +
        "FITS (*.fit;*.fits;*.fts)|*.fit;*.fits;*.fts|" +
        "All files (*.*)|*.*";

    public ImagePairSelection? PickImagePair(string? initialDirectory = null)
    {
        string? before = Pick("Select image A  —  shown left of the divider", initialDirectory);
        if (before is null)
        {
            return null;
        }

        // The second image almost always sits beside the first, so start there rather than
        // making the user navigate the same path twice.
        string? after = Pick("Select image B  —  shown right of the divider", Path.GetDirectoryName(before));
        if (after is null)
        {
            return null;
        }

        return new ImagePairSelection(before, after);
    }

    private static string? Pick(string title, string? initialDirectory)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = Filter,
            CheckFileExists = true,
            Multiselect = false,
        };

        if (!string.IsNullOrEmpty(initialDirectory) && Directory.Exists(initialDirectory))
        {
            dialog.InitialDirectory = initialDirectory;
        }

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
