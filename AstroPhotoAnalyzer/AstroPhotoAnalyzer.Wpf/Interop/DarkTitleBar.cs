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
// WPF does not draw the title bar, Windows does, so a window otherwise completely dark keeps a
// white band above it. It is solved by asking DWM to use the dark variant - a single attribute,
// whose number changed between versions of Windows 10 (19 before 20H1, 20 after). Both are
// tried and failure is ignored.

using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using NLog;

namespace AstroPhotoAnalyzer.Wpf
{
    /// <summary>Asks DWM for the dark title bar on a window; best effort, never fails.</summary>
    internal static class DarkTitleBar
    {
        /// <summary>The diagnostic log: a system without the dark title bar (R-171, ADR 0069).</summary>
        static readonly Logger Log = LogManager.GetLogger(Loggers.Window);

        const int UseImmersiveDarkModeBefore20H1 = 19;
        const int UseImmersiveDarkMode = 20;

        /// <summary>DwmSetWindowAttribute of dwmapi.dll, for an int-sized attribute.</summary>
        /// <param name="hwnd">The window's handle.</param>
        /// <param name="attr">The DWMWINDOWATTRIBUTE number.</param>
        /// <param name="value">The attribute's value; 1 turns the dark mode on.</param>
        /// <param name="size">The size of the value in bytes, sizeof(int).</param>
        /// <returns>
        /// An HRESULT: 0 on success, otherwise the failure, not an exception; a missing
        /// dwmapi.dll or entry point still throws, which <see cref="Apply"/> catches.
        /// </returns>
        [DllImport("dwmapi.dll", PreserveSig = true)]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        /// <summary>
        /// Turns the dark title bar on. Called once the window has a handle (SourceInitialized or
        /// later); earlier it does nothing. A missing dwmapi.dll or entry point is ignored.
        /// </summary>
        /// <param name="window">The window; not null.</param>
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
            catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException)
            {
                Log.Debug(e, "the title bar cannot be darkened on this system");
            }
        }
    }
}
