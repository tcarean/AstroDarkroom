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

// Copied from LightsCheck (which copied FlatCheck); a fix goes there first (ADR 0009).

using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using NLog;

namespace DarksCheck.Wpf;

/// <summary>
/// Asks the window manager for a dark title bar; silently nothing on a Windows without the
/// attribute, and a Debug line in the diagnostic log when the call itself cannot be made (no
/// <c>dwmapi.dll</c>, or no such entry point in it).
/// </summary>
internal static partial class DarkTitleBar
{
    /// <summary>The diagnostic log: a window manager that cannot be called, so the title bar stays light (R-101).</summary>
    private static readonly Logger Log = LogManager.GetLogger(Loggers.Window);

    private const int UseImmersiveDarkModeBefore20H1 = 19;
    private const int UseImmersiveDarkMode = 20;

    /// <summary>DWM's DwmSetWindowAttribute, source-generated.</summary>
    /// <param name="hwnd">The window's handle.</param>
    /// <param name="attr">The attribute's number (19 or 20 here, the dark-mode attribute).</param>
    /// <param name="value">The attribute's value; 1 = on.</param>
    /// <param name="size">The size of <paramref name="value"/> in bytes.</param>
    /// <returns>An HRESULT: 0 on success, non-zero when the attribute is refused.</returns>
    [LibraryImport("dwmapi.dll")]
    private static partial int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    /// <summary>
    /// Called once the window has a handle (SourceInitialized or later): tries the attribute of
    /// Windows 10 20H1 and later, then the older number; does nothing without a handle.
    /// </summary>
    /// <param name="window">The window whose title bar turns dark; not null.</param>
    internal static void Apply(Window window)
    {
        IntPtr hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        int on = 1;
        try
        {
            if (DwmSetWindowAttribute(hwnd, UseImmersiveDarkMode, ref on, sizeof(int)) != 0)
            {
                DwmSetWindowAttribute(hwnd, UseImmersiveDarkModeBefore20H1, ref on, sizeof(int));
            }
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException)
        {
            // No compositor: the bar stays light, the application still starts.
            Log.Debug(e, "the title bar cannot be darkened on this system");
        }
    }
}
