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

        _tray = new TrayIconHost(autoStart);
        _tray.ExitRequested += (_, _) => Shutdown();

        _coordinator = new ReminderCoordinator(
            new SystemEventsSessionMonitor(Dispatcher),
            new WpfReminderPresenter(Dispatcher));
        _coordinator.Start();

        if (new StartupRegistrar(registry, autoStart).EnsureRegisteredOnFirstRun())
        {
            _tray.ShowBalloon(
                "Mindfulness Reminder",
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
        // A tray app that dies silently just vanishes; say so before going.
        MessageBox.Show(
            e.Exception.Message,
            "Mindfulness Reminder stopped",
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
