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

namespace MindfulnessReminder.App;

/// <summary>
/// Asks the desktop window manager to draw a window's title bar dark.
///
/// The title bar is not WPF's to paint -- it belongs to the non-client area, so no amount of
/// styling inside the window reaches it, and a settings window themed to the caption and no
/// further looks like a bug rather than a theme. The alternative is WindowStyle="None" with
/// the caption redrawn by hand, which means reimplementing dragging, the close button, snap
/// and the system menu for a cosmetic gain.
///
/// The attribute number moved: it is 20 from Windows 10 build 18985 onwards and 19 on
/// 17763..18984. Rather than read the build number and pick, this asks for 20 and falls back
/// to 19 when the call is refused. A build that knows neither simply keeps a light caption:
/// failure here is a window that looks wrong, never a window that does not open, so nothing
/// is thrown and nothing is reported.
/// </summary>
internal static class ImmersiveDarkMode
{
    private const int UseImmersiveDarkMode = 20;

    private const int UseImmersiveDarkModeBefore20H1 = 19;

    /// <summary>
    /// Asks for a dark title bar on <paramref name="window"/>.
    ///
    /// Call once the window has an HWND -- SourceInitialized is the earliest such moment,
    /// and it is before the first paint, so the caption is never seen light first.
    /// </summary>
    /// <param name="window">The window to darken; nothing happens while it has no handle.</param>
    public static void Apply(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var enabled = 1;

        if (DwmSetWindowAttribute(handle, UseImmersiveDarkMode, ref enabled, sizeof(int)) != 0)
        {
            _ = DwmSetWindowAttribute(
                handle, UseImmersiveDarkModeBefore20H1, ref enabled, sizeof(int));
        }
    }

    /// <summary>
    /// dwmapi's DwmSetWindowAttribute: sets one attribute of how the desktop window manager
    /// draws a window.
    /// </summary>
    /// <param name="hwnd">The window's handle.</param>
    /// <param name="attribute">The attribute's number.</param>
    /// <param name="value">The value to set; 1 switches the dark title bar on.</param>
    /// <param name="size">The size of <paramref name="value"/>, in bytes.</param>
    /// <returns>An HRESULT: 0 when the attribute was set.</returns>
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd, int attribute, ref int value, int size);
}
