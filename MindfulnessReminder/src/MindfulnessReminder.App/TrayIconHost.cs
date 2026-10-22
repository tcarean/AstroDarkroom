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

using MindfulnessReminder.Core;
using WpfApplication = System.Windows.Application;

namespace MindfulnessReminder.App;

/// <summary>
/// The tray icon and its menu: Settings, a checkbox that mirrors the auto-start state,
/// About, and Exit.
///
/// The checked state is refreshed every time the menu opens rather than cached, because the
/// user can change auto-start behind our back from Task Manager's Startup tab.
///
/// The tooltip is not static any more -- it carries the countdown to the next reminder, so
/// the App sets it through <see cref="SetTooltip"/> whenever the coordinator's countdown
/// changes.
/// </summary>
internal sealed class TrayIconHost : IDisposable
{
    private const string IconResourceUri = "pack://application:,,,/Assets/app.ico";

    private readonly IAutoStartManager _autoStart;
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _autoStartItem;
    private readonly Icon _icon;

    private bool _disposed;

    /// <summary>Puts the icon and its menu in the tray, visible at once.</summary>
    /// <param name="autoStart">What the "Run at Windows startup" item reads and sets.</param>
    public TrayIconHost(IAutoStartManager autoStart)
    {
        ArgumentNullException.ThrowIfNull(autoStart);
        _autoStart = autoStart;

        _autoStartItem = new ToolStripMenuItem("Run at Windows startup")
        {
            CheckOnClick = false,
        };
        _autoStartItem.Click += OnAutoStartClick;

        var settingsItem = new ToolStripMenuItem("Settings...");
        settingsItem.Click += (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty);

        // The reminder is no place for a licence notice -- it would defeat the point of it --
        // so About is where the copyright and the warranty disclaimer are displayed. It sits
        // with Exit rather than with the settings: neither is a setting.
        //
        // Like Settings, it is raised as an event rather than opened here. Both are WPF
        // windows whose lifetime App owns, and this class knows nothing about either.
        var aboutItem = new ToolStripMenuItem("About");
        aboutItem.Click += (_, _) => AboutRequested?.Invoke(this, EventArgs.Empty);

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);

        var menu = new ContextMenuStrip();
        menu.Items.Add(settingsItem);
        menu.Items.Add(_autoStartItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(aboutItem);
        menu.Items.Add(exitItem);
        menu.Opening += (_, _) => RefreshAutoStartItem();

        // Painted to match the Settings window. The menu is Windows Forms, so the WPF theme
        // does not reach it and the colours are repeated in the renderer; see DarkMenuRenderer.
        DarkMenuRenderer.Apply(menu);

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

    public event EventHandler? SettingsRequested;

    public event EventHandler? AboutRequested;

    /// <summary>
    /// Replaces the hover text. Truncated rather than trusted: NotifyIcon.Text throws above
    /// 127 characters, and a tray icon lost to an exception is a poor trade for a countdown.
    /// <see cref="TrayTooltip"/> already keeps within the limit; this is the belt to its braces.
    /// </summary>
    /// <param name="text">
    /// The new hover text; cut to <see cref="TrayTooltip.MaxLength"/> characters.
    /// </param>
    public void SetTooltip(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        _notifyIcon.Text = text.Length <= TrayTooltip.MaxLength
            ? text
            : text[..TrayTooltip.MaxLength];
    }

    /// <summary>Shows a notification from the tray icon.</summary>
    /// <param name="title">The notification's title.</param>
    /// <param name="text">The notification's text.</param>
    public void ShowBalloon(string title, string text)
    {
        _notifyIcon.BalloonTipTitle = title;
        _notifyIcon.BalloonTipText = text;
        _notifyIcon.BalloonTipIcon = ToolTipIcon.Info;
        _notifyIcon.ShowBalloonTip(5000);
    }

    /// <summary>
    /// Switches auto-start off when it works and on otherwise, then refreshes the item. A
    /// change Windows refuses becomes a notification rather than an error.
    /// </summary>
    /// <param name="sender">The menu item; not read.</param>
    /// <param name="e">The click; not read.</param>
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
        catch (Exception ex) when (RegistryRefusal.IsWriteRefusal(ex))
        {
            ShowBalloon(
                VersionInfo.Name,
                "Windows refused the startup setting change. Group policy may be managing it.");
        }

        RefreshAutoStartItem();
    }

    /// <summary>Sets the item's tick and text from the auto-start status as it is now.</summary>
    private void RefreshAutoStartItem()
    {
        var status = _autoStart.GetStatus();

        _autoStartItem.Checked = status == AutoStartStatus.Enabled;
        _autoStartItem.Text = status == AutoStartStatus.BlockedByWindows
            ? "Run at Windows startup (disabled in Task Manager)"
            : "Run at Windows startup";
    }

    /// <summary>
    /// Loads the app icon at the shell's small icon size. Throws
    /// <see cref="System.IO.IOException"/> when the icon is not among the assembly's
    /// resources: WPF throws it before the null check below can.
    /// </summary>
    /// <returns>The icon, which the caller disposes.</returns>
    private static Icon LoadTrayIcon()
    {
        var resource = WpfApplication.GetResourceStream(new Uri(IconResourceUri))
            ?? throw new InvalidOperationException($"Missing embedded resource: {IconResourceUri}");

        using var stream = resource.Stream;

        // Asking for the shell's small icon size picks the matching frame out of the
        // multi-resolution .ico, which keeps the tray crisp at every DPI.
        return new Icon(stream, SystemInformation.SmallIconSize);
    }

    /// <summary>
    /// Takes the icon out of the tray and disposes it, its menu and the icon image. Calling it
    /// again does nothing.
    /// </summary>
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
