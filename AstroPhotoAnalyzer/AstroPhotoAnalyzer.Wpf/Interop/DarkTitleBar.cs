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

// DarkTitleBar.cs - bara de titlu intunecata.
//
// WPF nu deseneaza bara de titlu, o deseneaza Windows, deci o fereastra altfel complet
// intunecata ramane cu o banda alba deasupra. Se rezolva cerandu-i lui DWM sa foloseasca
// varianta intunecata - un singur atribut, al carui numar s-a schimbat intre versiuni de
// Windows 10 (19 inainte de 20H1, 20 dupa). Se incearca amandoua si se ignora esecul.

using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace AstroPhotoAnalyzer.Wpf
{
    internal static class DarkTitleBar
    {
        const int UseImmersiveDarkModeBefore20H1 = 19;
        const int UseImmersiveDarkMode = 20;

        [DllImport("dwmapi.dll", PreserveSig = true)]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        /// <summary>Se cheama dupa ce fereastra are un handle (SourceInitialized sau mai tarziu).</summary>
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
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
        }
    }
}
