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

// DarkTitleBar.cs - the dark title bar.
//
// WPF does not draw the title bar, Windows does, so a window otherwise wholly dark keeps a
// white band on top. It is solved by asking DWM to use the dark variant - a single attribute.
//
// The attribute changed its number between versions of Windows 10: 19 on the builds before
// 20H1, 20 after. Both are tried and the failure is ignored - on a system too old the bar stays
// light, which is ugly, not serious. That is why nothing here throws.

using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace FlatCheck.Wpf
{
    /// <summary>Asks Windows to draw a window's title bar dark; never throws.</summary>
    internal static class DarkTitleBar
    {
        const int UseImmersiveDarkModeBefore20H1 = 19;
        const int UseImmersiveDarkMode = 20;

        /// <summary>The DWM call that sets one attribute of a window.</summary>
        /// <param name="hwnd">The window's handle.</param>
        /// <param name="attr">The attribute's number.</param>
        /// <param name="value">The attribute's value (1 = on).</param>
        /// <param name="size">The value's size in bytes.</param>
        /// <returns>0 (S_OK) on success, an HRESULT otherwise.</returns>
        [DllImport("dwmapi.dll", PreserveSig = true)]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        /// <summary>
        /// Called after the window has a handle (SourceInitialized or later); before that the
        /// HWND is zero and the call has nothing to colour.
        /// </summary>
        /// <param name="window">The window whose title bar turns dark.</param>
        internal static void Apply(Window window)
        {
            IntPtr hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;

            int on = 1;
            try
            {
                if (DwmSetWindowAttribute(hwnd, UseImmersiveDarkMode, ref on, sizeof(int)) != 0)
                    DwmSetWindowAttribute(hwnd, UseImmersiveDarkModeBefore20H1, ref on, sizeof(int));
            }
            catch (DllNotFoundException)
            {
                // dwmapi is missing (Windows without a compositor). The bar stays light; that
                // is no reason for the application not to start.
            }
            catch (EntryPointNotFoundException)
            {
            }
        }
    }
}
