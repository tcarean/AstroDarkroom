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
// varianta intunecata - un singur atribut.
//
// Atributul si-a schimbat numarul intre versiuni de Windows 10: 19 pe build-urile de
// dinainte de 20H1, 20 dupa. Se incearca amandoua si se ignora esecul - pe un sistem
// prea vechi bara ramane deschisa la culoare, ceea ce e urat, nu grav. De aceea nimic
// de aici nu arunca.

using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace FlatCheck.Wpf
{
    internal static class DarkTitleBar
    {
        const int UseImmersiveDarkModeBefore20H1 = 19;
        const int UseImmersiveDarkMode = 20;

        [DllImport("dwmapi.dll", PreserveSig = true)]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        /// <summary>
        /// Se cheama dupa ce fereastra are un handle (SourceInitialized sau mai tarziu);
        /// inainte de asta HWND-ul e zero si apelul n-are ce colora.
        /// </summary>
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
                // dwmapi lipseste (Windows fara compozitor). Bara ramane deschisa la
                // culoare; nu e un motiv sa nu porneasca aplicatia.
            }
            catch (EntryPointNotFoundException)
            {
            }
        }
    }
}
