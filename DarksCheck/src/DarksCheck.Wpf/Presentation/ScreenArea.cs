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

// DarksCheck's own, not a LightsCheck copy: the launcher's ScreenArea (AstroDarkroom ADR 0013)
// with AstroPhotoAnalyzer's TopInside beside it (its ADR 0070), for About's fit (ADR 0027).

using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using NLog;

namespace DarksCheck.Wpf;

/// <summary>
/// The work area of the screen a window is on, and where a window's top goes to stay inside it
/// (R-121). WPF knows the primary screen's area only (<see cref="SystemParameters.WorkArea"/>),
/// so Windows is asked for the screen nearest the window and its area without the taskbar, in
/// the pixels Windows gives this process, turned into the window's device-independent pixels by
/// the window's DPI scale.
/// </summary>
internal static partial class ScreenArea
{
    /// <summary>The diagnostic log: a screen's work area that could not be read (R-101).</summary>
    private static readonly Logger Log = LogManager.GetLogger(Loggers.Window);

    /// <summary>MONITOR_DEFAULTTONEAREST: the screen nearest the window, even when it is on none.</summary>
    private const uint MonitorDefaultToNearest = 2;

    /// <summary>The screen nearest a window.</summary>
    /// <param name="hwnd">The window's handle.</param>
    /// <param name="flags">What to return for a window on no screen: <see cref="MonitorDefaultToNearest"/>.</param>
    /// <returns>The screen's handle; zero when there is none.</returns>
    [LibraryImport("user32.dll")]
    private static partial IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

    /// <summary>What Windows says of one screen: its rectangle and its work area.</summary>
    /// <param name="monitor">The screen's handle.</param>
    /// <param name="info">Filled in; its <see cref="MonitorInfo.Size"/> must be set first.</param>
    /// <returns>True when the information was given.</returns>
    [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    /// <summary>
    /// The work area of the screen <paramref name="window"/> is on, in its device-independent
    /// pixels: the screen nearest it, without the taskbar.
    /// </summary>
    /// <param name="window">A window, normally shown, so that it has a handle and a screen; not null.</param>
    /// <returns>The area; the primary screen's when the window has no handle or Windows gives no answer.</returns>
    internal static Rect WorkAreaOf(Window window)
    {
        IntPtr hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return SystemParameters.WorkArea;
        }

        try
        {
            IntPtr monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
            MonitorInfo info = new() { Size = Marshal.SizeOf<MonitorInfo>() };
            if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info))
            {
                Log.Debug("the work area of the window's screen could not be read; the primary screen's is used");
                return SystemParameters.WorkArea;
            }

            DpiScale dpi = VisualTreeHelper.GetDpi(window);
            return new Rect(
                info.Work.Left / dpi.DpiScaleX,
                info.Work.Top / dpi.DpiScaleY,
                (info.Work.Right - info.Work.Left) / dpi.DpiScaleX,
                (info.Work.Bottom - info.Work.Top) / dpi.DpiScaleY);
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException)
        {
            Log.Debug(e, "the work area of the window's screen could not be read; the primary screen's is used");
            return SystemParameters.WorkArea;
        }
    }

    /// <summary>
    /// The top of a window of the given height kept inside a work area: its own top when it fits
    /// there, moved down to the area's top or up until its bottom is the area's when it runs past
    /// one edge, and the area's top when it is as tall as the area or taller, so its title bar
    /// stays on the screen. The launcher's <c>WindowFit.Top</c>, as AstroPhotoAnalyzer keeps it.
    /// </summary>
    /// <param name="top">Where the window's top is before the fit, in device-independent pixels.</param>
    /// <param name="height">The window's height, title bar included; not negative.</param>
    /// <param name="areaTop">The work area's top.</param>
    /// <param name="areaHeight">The work area's height; not negative.</param>
    /// <returns>The window's top inside the area.</returns>
    internal static double TopInside(double top, double height, double areaTop, double areaHeight)
        => height >= areaHeight ? areaTop : Math.Clamp(top, areaTop, areaTop + areaHeight - height);

    /// <summary>A screen rectangle in the pixels Windows gives this process (RECT).</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        /// <summary>The left edge.</summary>
        public int Left;

        /// <summary>The top edge.</summary>
        public int Top;

        /// <summary>The right edge, outside the rectangle.</summary>
        public int Right;

        /// <summary>The bottom edge, outside the rectangle.</summary>
        public int Bottom;
    }

    /// <summary>What Windows says of one screen (MONITORINFO).</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        /// <summary>The structure's size in bytes, which the caller sets.</summary>
        public int Size;

        /// <summary>The whole screen.</summary>
        public NativeRect Monitor;

        /// <summary>The screen without the taskbar and the docked bars.</summary>
        public NativeRect Work;

        /// <summary>1 for the primary screen.</summary>
        public uint Flags;
    }
}
