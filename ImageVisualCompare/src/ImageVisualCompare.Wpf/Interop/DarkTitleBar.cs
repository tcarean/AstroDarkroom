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

using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ImageVisualCompare.Wpf.Interop;

/// <summary>
/// Makes a window's title bar dark.
///
/// WPF does not follow the system app theme, and it offers no way to reach the caption at
/// all: the frame belongs to the desktop window manager, not to the application. So the
/// window has to opt in through DWM, one attribute per window. Without this the tool paints
/// a dark chrome under a white title bar, which is exactly the bright edge around the image
/// the palette exists to avoid.
///
/// The choice is deliberately not "follow the system": this window is dark whatever the user
/// runs their desktop as, for the same reason the rest of the palette is.
/// </summary>
internal static class DarkTitleBar
{
    /// <summary>Dark caption. 20 is the documented attribute; 19 is what builds before 20H1 used.</summary>
    private const int UseImmersiveDarkMode = 20;

    private const int UseImmersiveDarkModeBefore20H1 = 19;

    /// <summary>Caption and border colours. Windows 11 only — earlier builds ignore them.</summary>
    private const int CaptionColor = 35;

    private const int BorderColor = 34;

    /// <summary>The chrome colour from Theme.xaml, as a COLORREF (0x00BBGGRR).</summary>
    private const int ChromeColorRef = 0x00261F1B;

    private const int FirstDarkModeBuild = 17763;

    private const int FirstBuildUsingAttribute20 = 19041;

    private const int FirstWindows11Build = 22000;

    /// <summary>
    /// Applies the dark caption as soon as the window has a handle, which is before it is
    /// first rendered — applying any later shows a white title bar for one frame.
    /// </summary>
    public static void Apply(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        IntPtr handle = new WindowInteropHelper(window).Handle;
        if (handle != IntPtr.Zero)
        {
            ApplyTo(handle);
            return;
        }

        window.SourceInitialized += OnSourceInitialized;

        void OnSourceInitialized(object? sender, EventArgs e)
        {
            window.SourceInitialized -= OnSourceInitialized;
            ApplyTo(new WindowInteropHelper(window).Handle);
        }
    }

    private static void ApplyTo(IntPtr handle)
    {
        int build = Environment.OSVersion.Version.Build;

        if (handle == IntPtr.Zero || build < FirstDarkModeBuild)
        {
            return;
        }

        int enabled = 1;
        Set(handle, build >= FirstBuildUsingAttribute20 ? UseImmersiveDarkMode : UseImmersiveDarkModeBefore20H1, ref enabled);

        // Windows 11 lets the caption be an exact colour, so it can match the toolbar below it
        // rather than being the generic Windows dark. Windows 10 has no such attribute and
        // gets the generic one, so the colour is set only from build 22000. No result of
        // DwmSetWindowAttribute is checked, here or for the dark mode above.
        if (build >= FirstWindows11Build)
        {
            int colour = ChromeColorRef;
            Set(handle, CaptionColor, ref colour);
            Set(handle, BorderColor, ref colour);
        }
    }

    private static void Set(IntPtr handle, int attribute, ref int value) =>
        DwmSetWindowAttribute(handle, attribute, ref value, sizeof(int));

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
