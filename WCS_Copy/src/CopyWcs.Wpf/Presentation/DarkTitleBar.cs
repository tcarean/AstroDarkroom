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

// Copied from AstroDarkroom (which copied LightsCheck); a fix goes there first (ADR 0007).

using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace CopyWcs.Wpf;

/// <summary>
/// Asks the window manager for a dark title bar, so the bar matches the dark windows (R-037);
/// silently nothing on a Windows without the attribute.
/// </summary>
internal static partial class DarkTitleBar
{
    private const int UseImmersiveDarkModeBefore20H1 = 19;
    private const int UseImmersiveDarkMode = 20;

    /// <summary>Sets one attribute of a window's frame, through DWM.</summary>
    /// <param name="hwnd">The window's handle.</param>
    /// <param name="attr">The attribute's number (DWMWINDOWATTRIBUTE).</param>
    /// <param name="value">The attribute's value; 1 turns a boolean attribute on.</param>
    /// <param name="size">The size of the value in bytes.</param>
    /// <returns>An HRESULT: 0 when the attribute was set, non-zero when this Windows refused it.</returns>
    [LibraryImport("dwmapi.dll")]
    private static partial int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    /// <summary>
    /// Darkens the window's title bar: the attribute of Windows 10 20H1 and later, else the one
    /// of the builds before it.
    /// </summary>
    /// <param name="window">The window; it must have a handle (SourceInitialized or later), or nothing happens.</param>
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
        catch (DllNotFoundException)
        {
            // No compositor: the bar stays light, the window still opens.
        }
        catch (EntryPointNotFoundException)
        {
        }
    }
}
