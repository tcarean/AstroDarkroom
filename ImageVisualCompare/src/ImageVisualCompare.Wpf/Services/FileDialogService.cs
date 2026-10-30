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
using ImageVisualCompare.Core;
using Microsoft.Win32;

namespace ImageVisualCompare.Wpf.Services;

/// <summary>Asks the user for the two files of a pair.</summary>
public interface IFileDialogService
{
    /// <summary>Asks for the two images in turn.</summary>
    /// <param name="initialDirectory">
    /// The folder the first dialog opens in; null, an empty string or a folder that is gone
    /// leaves the choice to Windows.
    /// </param>
    /// <returns>The two files, or null if the user cancels either dialog.</returns>
    ImagePairSelection? PickImagePair(string? initialDirectory = null);

    /// <summary>Asks for one image, the one a change replaces (R-065).</summary>
    /// <param name="side">The image asked for, which titles the dialog as R-001 does.</param>
    /// <param name="initialDirectory">
    /// The folder the dialog opens in; null, an empty string or a folder that is gone leaves the
    /// choice to Windows.
    /// </param>
    /// <returns>The file, or null if the user cancels the dialog.</returns>
    string? PickImage(ImageSide side, string? initialDirectory = null);
}

/// <summary>
/// The two files of a pair, as the user picked them or named them on the command line.
/// </summary>
/// <param name="BeforePath">Image A's file, shown left of the divider.</param>
/// <param name="AfterPath">Image B's file, shown right of the divider.</param>
public sealed record ImagePairSelection(string BeforePath, string AfterPath);

/// <summary>
/// Asks through the Windows common open dialogs, the one part of the application that follows
/// the system theme rather than its own.
/// </summary>
public sealed class FileDialogService : IFileDialogService
{
    /// <inheritdoc/>
    public ImagePairSelection? PickImagePair(string? initialDirectory = null)
    {
        string? before = PickImage(ImageSide.A, initialDirectory);
        if (before is null)
        {
            return null;
        }

        // The second image almost always sits beside the first, so start there rather than
        // making the user navigate the same path twice.
        string? after = PickImage(ImageSide.B, Path.GetDirectoryName(before));
        if (after is null)
        {
            return null;
        }

        return new ImagePairSelection(before, after);
    }

    /// <inheritdoc/>
    public string? PickImage(ImageSide side, string? initialDirectory = null) =>
        Pick(UiText.Current.SelectImage(side), initialDirectory);

    /// <summary>Shows one open dialog for an existing file, with the image formats first.</summary>
    /// <param name="title">The dialog's title, which says which image is being picked.</param>
    /// <param name="initialDirectory">
    /// The folder to open in; not used when null, empty or gone.
    /// </param>
    /// <returns>The picked file, or null when the dialog was cancelled.</returns>
    private static string? Pick(string title, string? initialDirectory)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            // TIFF comes free with the WIC codec set and Siril exports it, so there is no reason
            // to turn those files away. FITS is what Siril works in; the project's own reader
            // opens it.
            Filter = UiText.Current.FileFilter,
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
