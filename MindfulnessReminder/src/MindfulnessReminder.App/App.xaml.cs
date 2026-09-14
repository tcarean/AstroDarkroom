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

using System.Collections.Generic;
using System.Globalization;
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
    /// machine. No tray icon, no registry access, no session hook. An optional argument
    /// after it is the message to show, so a text can be tried against the window before
    /// it is saved -- which is the only way to see what the length cap looks like without
    /// the registry being involved. Without one it shows the default text.
    /// </summary>
    private const string PreviewSwitch = "--preview";

    /// <summary>
    /// Reports what the activity monitor can see and quits. The two pause settings cannot be
    /// exercised without a real call or a real game, so this is the only way to tell a signal
    /// that is not arriving from a setting that is switched off.
    ///
    /// It takes an optional number of seconds to watch for. That is not a convenience: a
    /// single reading taken as the switch runs is no use for the full-screen check, because
    /// getting to a terminal to run it means leaving full screen and destroying the state
    /// being measured. With a duration you start it, go and do the thing, and read the
    /// timeline when you come back.
    /// </summary>
    private const string DetectSwitch = "--detect";

    /// <summary>Ten minutes. Long enough for any test, short enough that a typo ends.</summary>
    private static readonly TimeSpan MaxDetectDuration = TimeSpan.FromMinutes(10);

    private Mutex? _singleInstanceMutex;
    private TrayIconHost? _tray;
    private ReminderCoordinator? _coordinator;
    private ISettingsStore? _settingsStore;
    private SettingsWindow? _settingsWindow;
    private AboutWindow? _aboutWindow;

    /// <summary>
    /// The tooltip text last written to the tray. The countdown ticks every second but only
    /// changes once a minute, and every write is a Shell_NotifyIcon call, so the string is
    /// compared before it is set.
    /// </summary>
    private string? _tooltip;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (PreviewMessage(e.Args) is { } previewMessage)
        {
            ShowPreview(previewMessage);
            return;
        }

        if (DetectDuration(e.Args) is { } duration)
        {
            ShowDetection(duration);
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
        _tray.AboutRequested += (_, _) => ShowAbout();

        _coordinator = new ReminderCoordinator(
            new SystemEventsSessionMonitor(Dispatcher),
            new WpfReminderPresenter(Dispatcher),
            new DispatcherPeriodicTimer(Dispatcher, ReminderCoordinator.DefaultTickInterval),
            _settingsStore.Load(),
            new WindowsActivityMonitor());
        _coordinator.CountdownChanged += (_, _) => UpdateTooltip();
        _coordinator.Start();

        if (new StartupRegistrar(registry, autoStart).EnsureRegisteredOnFirstRun())
        {
            _tray.ShowBalloon(
                VersionInfo.Name,
                "Running in the tray. It now starts with Windows -- untick that in the tray menu to stop it.");
        }
    }

    /// <summary>
    /// The message <c>--preview</c> should show, or null when the switch was not given. The
    /// argument after the switch is the text; it goes through the same normalization the
    /// saved setting does, so the preview shows what the app would actually show, and an
    /// empty or missing one is the default message.
    /// </summary>
    private static string? PreviewMessage(string[] args)
    {
        var index = Array.FindIndex(
            args,
            argument => argument.Equals(PreviewSwitch, StringComparison.OrdinalIgnoreCase));

        if (index < 0)
        {
            return null;
        }

        return ReminderSettings.NormalizeMessage(index + 1 < args.Length ? args[index + 1] : null);
    }

    private void ShowPreview(string message)
    {
        var window = new BreakWindow(message);
        window.Closed += (_, _) => Shutdown();
        window.Show();
    }

    /// <summary>
    /// How long <c>--detect</c> should watch for, or null when the switch was not given.
    /// Zero means the one-shot reading; "--detect 30" and "--detect=30" both mean thirty
    /// seconds. Anything unparseable is a zero rather than a complaint: a diagnostic that
    /// refuses to run because its argument was mistyped is no help to anybody.
    /// </summary>
    private static TimeSpan? DetectDuration(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            var argument = args[i];

            if (argument.StartsWith(DetectSwitch + "=", StringComparison.OrdinalIgnoreCase))
            {
                return Seconds(argument[(DetectSwitch.Length + 1)..]);
            }

            if (!argument.Equals(DetectSwitch, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return i + 1 < args.Length ? Seconds(args[i + 1]) : TimeSpan.Zero;
        }

        return null;

        static TimeSpan Seconds(string text)
        {
            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                || value <= 0)
            {
                return TimeSpan.Zero;
            }

            var wanted = TimeSpan.FromSeconds(value);
            return wanted > MaxDetectDuration ? MaxDetectDuration : wanted;
        }
    }

    /// <summary>
    /// The diagnostic behind <see cref="DetectSwitch"/>. A WinExe has no console to print on,
    /// so it goes in a message box -- the same reasoning that puts the version in the crash
    /// dialog. The raw shell state is shown alongside the reading taken from it, because the
    /// number is what tells a state Windows has stopped reporting from one this build does
    /// not know about.
    ///
    /// Neither this nor --preview touches the single-instance mutex, which is claimed further
    /// down OnStartup. That is deliberate here: the point is to watch what the copy already
    /// sitting in the tray would be seeing, so it has to run alongside it.
    /// </summary>
    private void ShowDetection(TimeSpan duration)
    {
        var monitor = new WindowsActivityMonitor();
        var settings = new RegistrySettingsStore(new WindowsRegistryStore()).Load();

        if (duration <= TimeSpan.Zero)
        {
            Report(
                monitor.Describe()
                + "\nWith the current settings: "
                + SuppressionPolicy.Evaluate(settings, monitor.Read()));

            return;
        }

        // One reading a second, the same cadence the app itself ticks at. The message box
        // waits until the run is over, so it cannot disturb what is being measured -- which
        // is the whole reason the duration exists.
        var samples = new List<DetectionSample>();
        var started = DateTimeOffset.UtcNow;
        var timer = new DispatcherPeriodicTimer(Dispatcher, ReminderCoordinator.DefaultTickInterval);

        timer.Tick += (_, _) =>
        {
            var elapsed = DateTimeOffset.UtcNow - started;
            var snapshot = monitor.Read();

            samples.Add(new DetectionSample(
                elapsed,
                snapshot,
                SuppressionPolicy.Evaluate(settings, snapshot)));

            if (elapsed < duration)
            {
                return;
            }

            timer.Dispose();
            Report(
                "Watched for " + duration.TotalSeconds + " seconds.\n\n"
                + DetectionLog.Render(samples));
        };

        timer.Start();
    }

    private void Report(string body)
    {
        MessageBox.Show(
            body + "\n\n" + VersionInfo.Banner,
            VersionInfo.Name + " detection",
            MessageBoxButton.OK,
            MessageBoxImage.Information);

        Shutdown();
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
    /// Opens About, or raises the one already open. It was a MessageBox until the dark theme
    /// arrived: a Win32 dialog cannot be themed, and a white About box opening out of a dark
    /// tray menu reads as a different program. Modeless for the same reason Settings is -- a
    /// modal dialog would disable every other window, and a reminder falling due behind it
    /// would appear with a dead Close button.
    /// </summary>
    private void ShowAbout()
    {
        if (_aboutWindow is { } existing)
        {
            if (existing.WindowState == WindowState.Minimized)
            {
                existing.WindowState = WindowState.Normal;
            }

            existing.Activate();
            return;
        }

        var window = new AboutWindow();
        window.Closed += (_, _) => _aboutWindow = null;

        _aboutWindow = window;
        window.Show();
        window.Activate();
    }

    /// <summary>
    /// Pushes the countdown into the tray tooltip, and only when the visible text actually
    /// changes -- which, with a per-minute countdown off a per-second tick, is 1 write in 60.
    /// </summary>
    private void UpdateTooltip()
    {
        var text = TrayTooltip.Format(
            VersionInfo.Banner,
            _coordinator?.Remaining,
            _coordinator?.Suppression ?? ReminderSuppression.None);

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
