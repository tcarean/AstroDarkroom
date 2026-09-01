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

using System.Drawing;
using System.Windows.Forms;
using MindfulnessReminder.Core;
using WpfApplication = System.Windows.Application;

namespace MindfulnessReminder.App;

/// <summary>
/// The tray icon and its menu: a checkbox that mirrors the auto-start state, About, and Exit.
///
/// The checked state is refreshed every time the menu opens rather than cached, because the
/// user can change auto-start behind our back from Task Manager's Startup tab.
/// </summary>
internal sealed class TrayIconHost : IDisposable
{
    private const string IconResourceUri = "pack://application:,,,/Assets/app.ico";

    private readonly IAutoStartManager _autoStart;
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _autoStartItem;
    private readonly Icon _icon;

    private bool _disposed;

    public TrayIconHost(IAutoStartManager autoStart)
    {
        ArgumentNullException.ThrowIfNull(autoStart);
        _autoStart = autoStart;

        _autoStartItem = new ToolStripMenuItem("Run at Windows startup")
        {
            CheckOnClick = false,
        };
        _autoStartItem.Click += OnAutoStartClick;

        // The app's only window is the reminder, and putting a licence notice on that would
        // defeat its purpose, so About is where the copyright and the warranty disclaimer are
        // displayed. It sits with Exit rather than with the setting: neither is a setting.
        var aboutItem = new ToolStripMenuItem("About");
        aboutItem.Click += (_, _) => ShowAbout();

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);

        var menu = new ContextMenuStrip();
        menu.Items.Add(_autoStartItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(aboutItem);
        menu.Items.Add(exitItem);
        menu.Opening += (_, _) => RefreshAutoStartItem();

        _icon = LoadTrayIcon();

        _notifyIcon = new NotifyIcon
        {
            Icon = _icon,
            // The tooltip carries the version too: hovering the icon is the cheapest way
            // to find out which build is actually running, without opening anything.
            Text = VersionInfo.Banner,
            ContextMenuStrip = menu,
            Visible = true,
        };

        RefreshAutoStartItem();
    }

    public event EventHandler? ExitRequested;

    public void ShowBalloon(string title, string text)
    {
        _notifyIcon.BalloonTipTitle = title;
        _notifyIcon.BalloonTipText = text;
        _notifyIcon.BalloonTipIcon = ToolTipIcon.Info;
        _notifyIcon.ShowBalloonTip(5000);
    }

    private static void ShowAbout()
    {
        MessageBox.Show(
            LicenseInfo.AboutText,
            "About " + VersionInfo.Name,
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void OnAutoStartClick(object? sender, EventArgs e)
    {
        try
        {
            // Anything other than a working entry -- absent, or vetoed in Task Manager --
            // turns into "enable", so one click always restores the intended state.
            if (_autoStart.GetStatus() == AutoStartStatus.Enabled)
            {
                _autoStart.Disable();
            }
            else
            {
                _autoStart.Enable();
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException)
        {
            ShowBalloon(
                VersionInfo.Name,
                "Windows refused the startup setting change. Group policy may be managing it.");
        }

        RefreshAutoStartItem();
    }

    private void RefreshAutoStartItem()
    {
        var status = _autoStart.GetStatus();

        _autoStartItem.Checked = status == AutoStartStatus.Enabled;
        _autoStartItem.Text = status == AutoStartStatus.BlockedByWindows
            ? "Run at Windows startup (disabled in Task Manager)"
            : "Run at Windows startup";
    }

    private static Icon LoadTrayIcon()
    {
        var resource = WpfApplication.GetResourceStream(new Uri(IconResourceUri))
            ?? throw new InvalidOperationException($"Missing embedded resource: {IconResourceUri}");

        using var stream = resource.Stream;

        // Asking for the shell's small icon size picks the matching frame out of the
        // multi-resolution .ico, which keeps the tray crisp at every DPI.
        return new Icon(stream, SystemInformation.SmallIconSize);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        // Hiding first: a NotifyIcon disposed while visible can leave a ghost icon in the
        // tray until the user hovers over it.
        _notifyIcon.Visible = false;
        _notifyIcon.ContextMenuStrip?.Dispose();
        _notifyIcon.Dispose();
        _icon.Dispose();

        _disposed = true;
    }
}
