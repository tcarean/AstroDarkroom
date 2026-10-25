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

namespace LightsCheck.Wpf;

/// <summary>
/// Asks the window manager for a dark title bar; silently nothing on a Windows without the
/// attribute.
/// </summary>
internal static partial class DarkTitleBar
{
    private const int UseImmersiveDarkModeBefore20H1 = 19;
    private const int UseImmersiveDarkMode = 20;

    /// <summary>The DWM call that sets one attribute of a window.</summary>
    /// <param name="hwnd">The window's handle.</param>
    /// <param name="attr">The attribute's number (19 or 20 for the dark mode).</param>
    /// <param name="value">The attribute's value; 1 turns the dark mode on.</param>
    /// <param name="size">The size of <paramref name="value"/> in bytes.</param>
    /// <returns>0 (S_OK) on success, an HRESULT otherwise.</returns>
    [LibraryImport("dwmapi.dll")]
    private static partial int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    /// <summary>
    /// Called once the window has a handle (SourceInitialized or later); before that, nothing.
    /// It tries attribute 20, then 19 for the Windows 10 builds before 20H1.
    /// </summary>
    /// <param name="window">The window whose title bar turns dark.</param>
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
            // No compositor: the bar stays light, the application still starts.
        }
        catch (EntryPointNotFoundException)
        {
        }
    }
}
