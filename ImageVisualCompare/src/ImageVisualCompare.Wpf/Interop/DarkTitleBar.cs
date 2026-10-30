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
using ImageVisualCompare.Wpf.Logging;
using NLog;

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

    /// <summary>The dark caption asked of the desktop window manager, in the diagnostic log (R-121).</summary>
    private static readonly Logger Log = LogManager.GetLogger(Loggers.Chrome);

    /// <summary>
    /// Applies the dark caption as soon as the window has a handle, which is before it is
    /// first rendered — applying any later shows a white title bar for one frame.
    /// </summary>
    /// <param name="window">
    /// The window, with or without a handle yet; every window of the application passes itself
    /// from its constructor.
    /// </param>
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

    /// <summary>
    /// Turns the dark caption on for a window, and on Windows 11 sets the caption and border to
    /// the chrome colour too; does nothing before build 17763, which has no dark caption.
    /// </summary>
    /// <param name="handle">The window's handle; nothing is done for a zero handle.</param>
    private static void ApplyTo(IntPtr handle)
    {
        int build = Environment.OSVersion.Version.Build;

        if (handle == IntPtr.Zero || build < FirstDarkModeBuild)
        {
            Log.Debug("dark caption: none, " + (handle == IntPtr.Zero ? "the window has no handle" : "build " + LogText.Int(build) + " has no dark caption"));
            return;
        }

        int enabled = 1;
        int attribute = build >= FirstBuildUsingAttribute20 ? UseImmersiveDarkMode : UseImmersiveDarkModeBefore20H1;
        int dark = Set(handle, attribute, ref enabled);

        // Windows 11 lets the caption be an exact colour, so it can match the toolbar below it
        // rather than being the generic Windows dark. Windows 10 has no such attribute and
        // gets the generic one, so the colour is set only from build 22000. No result of
        // DwmSetWindowAttribute is acted on, here or for the dark mode above: they are logged,
        // for a report of a white caption (R-121).
        string colours = "not on this build";
        if (build >= FirstWindows11Build)
        {
            int colour = ChromeColorRef;
            int caption = Set(handle, CaptionColor, ref colour);
            int border = Set(handle, BorderColor, ref colour);
            colours = LogText.Hex(caption) + " and " + LogText.Hex(border);
        }

        Log.Debug("dark caption on build " + LogText.Int(build) + ": attribute " + LogText.Int(attribute) + " -> "
            + LogText.Hex(dark) + ", caption and border colours -> " + colours);
    }

    /// <summary>Sets one DWM attribute of a window to a 32-bit value.</summary>
    /// <param name="handle">The window whose attribute is set.</param>
    /// <param name="attribute">The attribute's number (DWMWINDOWATTRIBUTE).</param>
    /// <param name="value">The value: 1 to turn a flag on, or a COLORREF.</param>
    /// <returns>The call's HRESULT, 0 on success; only logged.</returns>
    private static int Set(IntPtr handle, int attribute, ref int value) =>
        DwmSetWindowAttribute(handle, attribute, ref value, sizeof(int));

    /// <summary>The desktop window manager's call that sets one attribute of a window.</summary>
    /// <param name="hwnd">The window whose attribute is set.</param>
    /// <param name="attribute">The attribute's number (DWMWINDOWATTRIBUTE).</param>
    /// <param name="value">
    /// The attribute's new value, by reference as the API takes it: 1 to turn a flag on, or a
    /// COLORREF.
    /// </param>
    /// <param name="size">The value's size, in bytes.</param>
    /// <returns>An HRESULT, 0 on success.</returns>
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
