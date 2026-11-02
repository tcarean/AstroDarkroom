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

using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using AstroDarkroom.Core;
using NLog;
using static AstroDarkroom.Core.Ui;

namespace AstroDarkroom.Wpf;

/// <summary>
/// The launcher's window: a row per app of the catalogue, each with its open button, the
/// suite's copyright (R-019), the language selector and the about button, and a warning when the
/// Visual C++ runtime the raw tools need is missing or too old, asked again at each activation
/// while it shows (R-018). It shows what the core says about the suite folder and starts what the
/// core plans (R-002, R-003); it decides nothing itself. Every label worded in two languages is
/// assigned in <see cref="Relabel"/>, in the current language, which is what lets a change of
/// language apply at once (R-006); the banner and the copyright, the same in both, once in the
/// constructor. What it does goes to the diagnostic log (R-012).
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>The diagnostic log: the suite folder, the window, the language switches, the starts, About (R-012).</summary>
    private static readonly Logger Log = LogManager.GetLogger(Loggers.Window);

    private readonly SuiteFolder _folder;
    private readonly string _settingsPath;
    private readonly ReleaseInfo? _release;
    private IReadOnlyList<string> _missingRuntime;

    /// <summary>
    /// The window on one suite folder, labelled in the language <see cref="Ui.Default"/> holds
    /// when it is made.
    /// </summary>
    /// <param name="folder">The suite folder the apps are looked up in: the launcher's own folder.</param>
    /// <param name="settingsPath">The file the chosen language is written to at every change (R-007).</param>
    /// <param name="missingRuntime">
    /// The Visual C++ runtime's DLLs that did not load at start (<see cref="VcRuntime.Missing"/>);
    /// empty when the runtime is there, and the warning line stays hidden (R-018). While it holds
    /// a file, each activation of the window asks again (<see cref="RecheckRuntime"/>).
    /// </param>
    public MainWindow(SuiteFolder folder, string settingsPath, IReadOnlyList<string> missingRuntime)
    {
        InitializeComponent();
        SourceInitialized += (_, _) => DarkTitleBar.Apply(this);
        Loaded += (_, _) => LogShown();
        Activated += (_, _) => RecheckRuntime();
        Closing += (_, _) => Log.Info("window closing");
        _folder = folder;
        _settingsPath = settingsPath;
        _missingRuntime = missingRuntime;
        _release = ReleaseManifest.TryRead(folder.ManifestPath);
        RuntimeLink.NavigateUri = new Uri(VcRuntime.DownloadUrl);
        Logo.Source = Icons.LargestFrame(Icons.Launcher);
        Banner.Text = VersionInfo.Banner;
        CopyrightLine.Text = VersionInfo.Copyright;
        // Checking the current language fires LanguageChecked, which sees no change and does
        // nothing; from here on the selector fires only for the user's clicks.
        (Ui.Current == UiLanguage.Romanian ? RomanianChoice : EnglishChoice).IsChecked = true;
        Relabel();
    }

    /// <summary>
    /// Assigns every text of the window worded in two languages, in the current one, and rebuilds
    /// the rows, whose texts come from the core in that language too. Called once at start and at
    /// every change of the selector.
    /// </summary>
    private void Relabel()
    {
        // Two sentences, one per line: each fits on its own in the top bar's 568 px (ADR 0017).
        Subtitle.Text = T(
            "From the night's frames to the print: six tools for DSLR astrophotography, in the order they are used.\nEach checks or prepares one step; open it from its row.",
            "De la cadrele noptii la print: sase unelte pentru astrofoto cu DSLR, in ordinea in care se folosesc.\nFiecare verifica sau pregateste un pas; deschide-o din randul ei.");
        PrerequisitesLine.Text = Prerequisites.Summary;
        RuntimeWarning.Visibility = _missingRuntime.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        RuntimeWarningText.Text = VcRuntime.Warning;
        RuntimeLinkText.Text = VcRuntime.DownloadUrl;
        EnglishChoice.ToolTip = RomanianChoice.ToolTip = T("Interface language", "Limba interfetei");
        AboutButton.Content = T("About", "Despre");
        AboutButton.ToolTip = T("Version, licence, components (F1)", "Versiune, licenta, componente (F1)");
        Rows.ItemsSource = AppCatalogue.Apps
            .Select(app => new AppRow(app, _folder.Locate(app), _release?.Find(app.Folder)))
            .ToList();
        Log.Info(LogText.Presence(_folder, AppCatalogue.Apps));
    }

    /// <summary>
    /// Writes, once the window is shown, its size and place, the primary screen's work area, the
    /// whole desktop's and the window's DPI, all in device-independent pixels but the DPI: what a
    /// report of a window cut off or off the screen needs (R-012). The work area is the primary
    /// screen's, whichever screen the window is on, and with screens of different DPIs its pixels
    /// and the window's are not the same size; the line says "primary" so no reader takes it for
    /// the window's own screen.
    /// </summary>
    private void LogShown()
    {
        if (!Log.IsDebugEnabled)
        {
            return;
        }

        Rect area = SystemParameters.WorkArea;
        DpiScale dpi = VisualTreeHelper.GetDpi(this);
        Log.Debug(FormattableString.Invariant(
            $"window shown {ActualWidth:0}x{ActualHeight:0} at ({Left:0},{Top:0}); primary work area {area.Width:0}x{area.Height:0} at ({area.Left:0},{area.Top:0}); ")
            + FormattableString.Invariant(
            $"desktop {SystemParameters.VirtualScreenWidth:0}x{SystemParameters.VirtualScreenHeight:0} at ({SystemParameters.VirtualScreenLeft:0},{SystemParameters.VirtualScreenTop:0}); dpi {dpi.PixelsPerInchX:0}x{dpi.PixelsPerInchY:0}"));
    }

    /// <summary>
    /// Switches the language to the checked choice: the default for the whole run, the
    /// setting file, then every label (R-006, R-007). A check that names the language already
    /// in use (the one the constructor makes) changes nothing.
    /// </summary>
    /// <param name="sender">The checked RadioButton; its Tag is the language.</param>
    /// <param name="e">The check; not read.</param>
    private void LanguageChecked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton { Tag: UiLanguage language } || language == Ui.Default)
        {
            return;
        }

        Ui.Default = language;
        Log.Info("language switched to " + Ui.Code(language));
        LanguageSetting.Write(_settingsPath, language);
        Relabel();
    }

    /// <summary>
    /// Starts the app of the clicked row, from the suite folder, in the launcher's language,
    /// through the shell (R-003, R-008). A start that fails (the file went away since the
    /// window opened, or Windows refused it) is told in a message box; the launcher stays open.
    /// The diagnostic log has the start asked for and its outcome: the process's id, or the
    /// exception (R-012).
    /// </summary>
    /// <param name="sender">The row's button; its DataContext is the row.</param>
    /// <param name="e">The click; not read.</param>
    private void OpenApp(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: AppRow row })
        {
            return;
        }

        try
        {
            LaunchPlan plan = Launcher.Plan(row.App, _folder);
            Log.Info(LogText.Open(row.App, plan));
            // The Process object is only a handle to the started app; disposing it does not touch the app.
            using Process? started = Process.Start(new ProcessStartInfo(plan.FileName)
            {
                UseShellExecute = true,
                WorkingDirectory = plan.WorkingDirectory,
                Arguments = plan.Arguments,
            });
            Log.Info("started " + row.App.WindowExe + (started is null ? "; Windows gave no process back" : ", process " + IdOf(started)));
        }
        catch (InvalidOperationException ex)
        {
            Log.Error(ex, "open " + row.Title + " refused; the message box shows the reason");
            MessageBox.Show(this, ex.Message, row.Title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Win32Exception ex)
        {
            Log.Error(ex, "open " + row.Title + " failed: Windows refused to start " + row.App.WindowExe + "; the message box says so");
            MessageBox.Show(this, T($"{row.App.WindowExe} could not start: {ex.Message}", $"{row.App.WindowExe} nu a putut porni: {ex.Message}"),
                row.Title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>
    /// The id of a process just started, for the log; <c>unknown</c> when Windows gave none to
    /// read, so the line never turns a start that worked into the message box of one that failed.
    /// </summary>
    /// <param name="process">The handle <see cref="Process.Start(ProcessStartInfo)"/> returned.</param>
    /// <returns>The id, invariant, or <c>unknown</c>.</returns>
    private static string IdOf(Process process)
    {
        try
        {
            return process.Id.ToString(CultureInfo.InvariantCulture);
        }
        catch (InvalidOperationException)
        {
            return "unknown";
        }
    }

    /// <summary>
    /// Asks Windows again for the Visual C++ runtime, each time the window is activated while the
    /// warning shows, so a user who installed it from the link and comes back sees the line go
    /// (R-018); once the four load, nothing is asked any more. An answer that differs from the
    /// last goes to the log, Info when the four load, Warn with those that still do not, and to
    /// the line.
    /// </summary>
    private void RecheckRuntime()
    {
        if (_missingRuntime.Count == 0)
        {
            return;
        }

        IReadOnlyList<string> missing = NativeProbe.MissingRuntime();
        if (missing.SequenceEqual(_missingRuntime, StringComparer.Ordinal))
        {
            return;
        }

        _missingRuntime = missing;
        if (missing.Count == 0)
        {
            Log.Info(LogText.VcRuntime(missing));
        }
        else
        {
            Log.Warn(LogText.VcRuntime(missing));
        }

        RuntimeWarning.Visibility = missing.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// Opens Microsoft's download of the Visual C++ runtime, the warning line's link, in the
    /// browser (R-018). The address opened goes to the log; when Windows will not open it, a
    /// message box gives the address, so it can be typed in a browser, and the log the exception.
    /// </summary>
    /// <param name="sender">The link; not read.</param>
    /// <param name="e">The navigation asked for; its URI, as written, is what opens.</param>
    private void OpenRuntimeLink(object sender, RequestNavigateEventArgs e)
    {
        e.Handled = true;
        string address = e.Uri.OriginalString;
        try
        {
            using Process? browser = Process.Start(new ProcessStartInfo(address) { UseShellExecute = true });
            Log.Info("opened " + address);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            Log.Error(ex, "open " + address + " failed: Windows refused it; the message box gives the address");
            MessageBox.Show(this, T($"Windows could not open {address}: {ex.Message}", $"Windows nu a putut deschide {address}: {ex.Message}"),
                "Astro Darkroom", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>Opens the about window (the Help command, F1 or the button).</summary>
    /// <param name="sender">The window; not read.</param>
    /// <param name="e">The command; not read.</param>
    private void ShowAbout(object sender, ExecutedRoutedEventArgs e)
    {
        Log.Info("About opened");
        AboutWindow about = new(_release, ScreenArea.WorkAreaOf(this)) { Owner = this };
        about.ShowDialog();
        Log.Info("About closed");
    }
}
