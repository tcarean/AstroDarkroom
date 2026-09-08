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

using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using MindfulnessReminder.Core;
using WinFormsApplication = System.Windows.Forms.Application;
using MessageBox = System.Windows.MessageBox;
using WpfApplication = System.Windows.Application;

namespace MindfulnessReminder.App;

/// <summary>
/// Composition root. There is no main window: the app lives in the tray, which is why
/// ShutdownMode is OnExplicitShutdown -- otherwise closing the reminder would quit.
/// </summary>
public partial class App : WpfApplication
{
    /// <summary>Per-session, so a second desktop session gets its own instance.</summary>
    private const string SingleInstanceMutexName = @"Local\MindfulnessReminder.SingleInstance";

    /// <summary>Name of the value under the Run key; also what Task Manager displays.</summary>
    private const string AutoStartValueName = "MindfulnessReminder";

    /// <summary>
    /// Shows the reminder once and quits, so the window can be checked without locking the
    /// machine. No tray icon, no registry access, no session hook.
    /// </summary>
    private const string PreviewSwitch = "--preview";

    private Mutex? _singleInstanceMutex;
    private TrayIconHost? _tray;
    private ReminderCoordinator? _coordinator;
    private ISettingsStore? _settingsStore;
    private SettingsWindow? _settingsWindow;

    /// <summary>
    /// The tooltip text last written to the tray. The countdown ticks every second but only
    /// changes once a minute, and every write is a Shell_NotifyIcon call, so the string is
    /// compared before it is set.
    /// </summary>
    private string? _tooltip;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (e.Args.Contains(PreviewSwitch, StringComparer.OrdinalIgnoreCase))
        {
            ShowPreview();
            return;
        }

        _singleInstanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var isFirstInstance);
        if (!isFirstInstance)
        {
            // Windows can hand us a second launch (Run key plus a manual start); the one
            // already in the tray owns the session.
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
            Shutdown();
            return;
        }

        DispatcherUnhandledException += OnDispatcherUnhandledException;

        // Both stacks share one process-wide DPI mode; setting it before any window exists
        // keeps the tray menu and the reminder crisp on mixed-DPI setups.
        WinFormsApplication.SetHighDpiMode(System.Windows.Forms.HighDpiMode.PerMonitorV2);

        // NotifyIcon menu is a Windows Forms control and renders unthemed without this.
        WinFormsApplication.EnableVisualStyles();

        var registry = new WindowsRegistryStore();
        var autoStart = new RegistryAutoStartManager(registry, AutoStartValueName, ResolveExecutablePath());

        _settingsStore = new RegistrySettingsStore(registry);

        _tray = new TrayIconHost(autoStart);
        _tray.ExitRequested += (_, _) => Shutdown();
        _tray.SettingsRequested += (_, _) => ShowSettings();

        _coordinator = new ReminderCoordinator(
            new SystemEventsSessionMonitor(Dispatcher),
            new WpfReminderPresenter(Dispatcher),
            new DispatcherPeriodicTimer(Dispatcher, ReminderCoordinator.DefaultTickInterval),
            _settingsStore.Load());
        _coordinator.CountdownChanged += (_, _) => UpdateTooltip();
        _coordinator.Start();

        if (new StartupRegistrar(registry, autoStart).EnsureRegisteredOnFirstRun())
        {
            _tray.ShowBalloon(
                VersionInfo.Name,
                "Running in the tray. It now starts with Windows -- untick that in the tray menu to stop it.");
        }
    }

    private void ShowPreview()
    {
        var window = new BreakWindow();
        window.Closed += (_, _) => Shutdown();
        window.Show();
    }

    /// <summary>
    /// Opens Settings, or raises the one already open. Modeless and single-instance: two
    /// copies of the window could be saved in either order, and the loser would silently win.
    /// </summary>
    private void ShowSettings()
    {
        if (_coordinator is not { } coordinator)
        {
            return;
        }

        if (_settingsWindow is { } existing)
        {
            if (existing.WindowState == WindowState.Minimized)
            {
                existing.WindowState = WindowState.Normal;
            }

            existing.Activate();
            return;
        }

        var window = new SettingsWindow(coordinator.Settings);

        window.Accepted += (_, settings) =>
        {
            // Saved first: if the write fails, the app has not started behaving in a way the
            // next launch will contradict.
            _settingsStore?.Save(settings);
            coordinator.ApplySettings(settings);
        };

        window.Closed += (_, _) => _settingsWindow = null;

        _settingsWindow = window;
        window.Show();
        window.Activate();
    }

    /// <summary>
    /// Pushes the countdown into the tray tooltip, and only when the visible text actually
    /// changes -- which, with a per-minute countdown off a per-second tick, is 1 write in 60.
    /// </summary>
    private void UpdateTooltip()
    {
        var text = TrayTooltip.Format(VersionInfo.Banner, _coordinator?.Remaining);

        if (text == _tooltip)
        {
            return;
        }

        _tooltip = text;
        _tray?.SetTooltip(text);
    }

    /// <summary>
    /// The path to record for auto-start. <see cref="Environment.ProcessPath"/> is the right
    /// answer for a normal launch (single-file publish included); under "dotnet run" it points
    /// at the shared host instead, so the apphost next to the assembly is used.
    /// </summary>
    private static string ResolveExecutablePath()
    {
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(processPath)
            && !string.Equals(Path.GetFileName(processPath), "dotnet.exe", StringComparison.OrdinalIgnoreCase))
        {
            return processPath;
        }

        return Path.GetFullPath(Path.ChangeExtension(Environment.GetCommandLineArgs()[0], ".exe"));
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // A tray app that dies silently just vanishes; say so before going. The banner goes
        // along: a screenshot of this dialog is often all a bug report has, and it has to say
        // which version broke.
        MessageBox.Show(
            e.Exception.Message + "\n\n" + VersionInfo.Banner,
            VersionInfo.Name + " stopped",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _coordinator?.Dispose();
        _tray?.Dispose();

        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();

        base.OnExit(e);
    }
}
