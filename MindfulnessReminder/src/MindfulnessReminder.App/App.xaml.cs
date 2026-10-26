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

using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
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
    private const string PreviewSwitch = CommandLineSwitches.Preview;

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
    private const string DetectSwitch = CommandLineSwitches.Detect;

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

    /// <summary>
    /// 1 once <see cref="ReportCrash"/> has started. The dialog it shows pumps messages, so a
    /// second error can arrive while it is up; the first one is the one that ends the process.
    /// </summary>
    private int _crashReported;

    /// <summary>The decision log of the tray copy; <c>null</c> in <c>--preview</c> and <c>--detect</c>.</summary>
    private FileDecisionLog? _log;

    /// <summary>
    /// Runs <c>--preview</c> or <c>--detect</c> when one is given; each quits when it is done.
    /// Otherwise claims the single-instance mutex, so that a second copy quits at once, and
    /// starts the tray icon, the reminders and, on the very first run, auto-start.
    /// </summary>
    /// <param name="e">The command line, in <see cref="StartupEventArgs.Args"/>.</param>
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

        // Only the tray copy is worth bringing back: Windows starts it again, with --restarted,
        // after a crash or a hang once it has run for a minute (R-100, ADR 0036).
        var registration = ApplicationRestart.ShouldRegister(e.Args) ? RestartRegistration.Register() : 0;

        // The log exists from here on: --preview and --detect returned above and write nothing
        // (R-110). Its first line says what this copy is and what Windows will do for it.
        _log = new FileDecisionLog();
        _log.Write(DecisionLog.Started(
            VersionInfo.Banner,
            RuntimeInformation.FrameworkDescription,
            e.Args,
            registration));

        DispatcherUnhandledException += OnDispatcherUnhandledException;

        // The tray menu is Windows Forms, whose window procedure catches an error in a menu
        // action and would show its own dialog; this sends it to the same one as the rest.
        WinFormsApplication.ThreadException += (_, e) => ReportCrash(e.Exception);

        // Both stacks share one process-wide DPI mode; setting it before any window exists
        // keeps the tray menu and the reminder crisp on mixed-DPI setups.
        WinFormsApplication.SetHighDpiMode(System.Windows.Forms.HighDpiMode.PerMonitorV2);

        // NotifyIcon menu is a Windows Forms control and renders unthemed without this.
        WinFormsApplication.EnableVisualStyles();

        var registry = new WindowsRegistryStore();
        var autoStart = new RegistryAutoStartManager(registry, AutoStartValueName, ResolveExecutablePath());

        _settingsStore = new RegistrySettingsStore(registry, _log);

        _tray = new TrayIconHost(autoStart);
        _tray.ExitRequested += (_, _) => Shutdown();
        _tray.SettingsRequested += (_, _) => ShowSettings();
        _tray.AboutRequested += (_, _) => ShowAbout();

        _coordinator = new ReminderCoordinator(
            new SystemEventsSessionMonitor(Dispatcher),
            new WpfReminderPresenter(Dispatcher),
            new DispatcherPeriodicTimer(Dispatcher, ReminderCoordinator.DefaultTickInterval),
            _settingsStore.Load(),
            new WindowsActivityMonitor(),
            log: _log);
        _coordinator.CountdownChanged += (_, _) => UpdateTooltip();
        _coordinator.Start();

        if (new StartupRegistrar(registry, autoStart, _log).EnsureRegisteredOnFirstRun())
        {
            _tray.ShowBalloon(
                VersionInfo.Name,
                "Running in the tray. It now starts with Windows -- untick that in the tray menu to stop it.");
        }

        if (ApplicationRestart.IsRestart(e.Args))
        {
            _tray.ShowBalloon(
                VersionInfo.Name,
                "Restarted after an error. The details are in the Windows Application log.");
        }
    }

    /// <summary>
    /// Finds the message <c>--preview</c> should show. The argument after the switch is the
    /// text; it goes through the same normalization the saved setting does, so the preview
    /// shows what the app would actually show, and an empty or missing one is the default
    /// message.
    /// </summary>
    /// <param name="args">The command-line arguments, without the program's name.</param>
    /// <returns>The message, or <c>null</c> when the switch was not given.</returns>
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

    /// <summary>Shows the reminder window, and quits when it is closed.</summary>
    /// <param name="message">What the window says, already normalized.</param>
    private void ShowPreview(string message)
    {
        var window = new BreakWindow(message);
        window.Closed += (_, _) => Shutdown();
        window.Show();
    }

    /// <summary>
    /// Finds how long <c>--detect</c> should watch for: "--detect 30" and "--detect=30" both
    /// mean thirty seconds. Anything unparseable is a zero rather than a complaint: a
    /// diagnostic that refuses to run because its argument was mistyped is no help to anybody.
    /// </summary>
    /// <param name="args">The command-line arguments, without the program's name.</param>
    /// <returns>
    /// The duration, at most <see cref="MaxDetectDuration"/>; zero for the one-shot reading;
    /// <c>null</c> when the switch was not given.
    /// </returns>
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
    /// <param name="duration">
    /// How long to take a reading once a second; zero or less takes a single reading.
    /// </param>
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

    /// <summary>
    /// Shows what <c>--detect</c> found, with the banner, in a message box, then quits.
    /// </summary>
    /// <param name="body">The report to show.</param>
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
    /// Finds the path to record for auto-start. <see cref="Environment.ProcessPath"/> is the
    /// right answer for a normal launch (single-file publish and "dotnet run" included); when
    /// the app is started through the shared host, as "dotnet MindfulnessReminder.dll", it
    /// points at dotnet.exe instead, so the apphost next to the assembly is used.
    /// </summary>
    /// <returns>The full path of the executable to start with Windows.</returns>
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

    /// <summary>
    /// Ends the app through its own crash dialog when an exception escapes on the UI thread.
    /// </summary>
    /// <param name="sender">Not read.</param>
    /// <param name="e">The exception, which is marked handled.</param>
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        ReportCrash(e.Exception);
    }

    /// <summary>
    /// Shows the error in a message box, hides the tray icon, and ends the process with a
    /// fail-fast, which Windows takes as a crash: it logs the text and the stack, writes a dump
    /// where it is set to, and starts the registered copy again (R-076, R-100). Never returns.
    /// </summary>
    /// <param name="exception">What went wrong; rendered by <see cref="CrashReport.Format"/>.</param>
    private void ReportCrash(Exception exception)
    {
        // The dialog below pumps messages, so the timer, the session events and the tray menu
        // all keep running under it; an error from any of them lands here again. The first
        // error is the one reported and the one that ends the process; a second one returns to
        // its handler, which has already marked it handled, and waits for the OK.
        if (Interlocked.Exchange(ref _crashReported, 1) == 1)
        {
            return;
        }

        // A tray app that dies silently just vanishes; say so before going. The banner goes
        // along: a screenshot of this dialog is often all a bug report has, and it has to say
        // which version broke -- and, since 2026-10-26, which file was missing.
        var text = CrashReport.Format(exception, VersionInfo.Banner);

        // Stops the timer and the session hook, so that what failed is not run again under the
        // dialog; nothing in the coordinator's teardown is worth a second failure here.
        try
        {
            _coordinator?.Dispose();
        }
        catch (Exception)
        {
        }

        // Written before the dialog, so the file has it even if nobody reads the dialog; the
        // log swallows its own failures, so this cannot get in the way of the fail-fast (R-114).
        _log?.Write(DecisionLog.Crashed(text));

        MessageBox.Show(
            text,
            VersionInfo.Name + " stopped",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        // The fail-fast skips OnExit, so the icon is taken down here; it is cosmetic at this
        // point, and an error taking it down must not get in the way of the fail-fast itself.
        try
        {
            _tray?.Dispose();
        }
        catch (Exception)
        {
        }

        // Not Shutdown(): WPF's shutdown path loads assemblies, and on 2026-10-26 it was the
        // second failure after the first, inside this very handler. A fail-fast needs nothing,
        // and a clean exit would not be a crash to Windows, so nothing would restart (ADR 0036).
        Environment.FailFast(text, exception);
    }

    /// <summary>
    /// Disposes the coordinator and the tray icon, and releases and disposes the
    /// single-instance mutex -- whichever of them this run created.
    /// </summary>
    /// <param name="e">Passed on to the base class.</param>
    protected override void OnExit(ExitEventArgs e)
    {
        _coordinator?.Dispose();
        _tray?.Dispose();

        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();

        base.OnExit(e);
    }
}
