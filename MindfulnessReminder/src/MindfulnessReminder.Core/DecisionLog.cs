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
using System.Text;

namespace MindfulnessReminder.Core;

/// <summary>Which of the two reminders a log line is about (R-112).</summary>
public enum ReminderKind
{
    /// <summary>The reminder shown on an unlock (R-010).</summary>
    Unlock,

    /// <summary>The reminder shown at the interval (R-021).</summary>
    Periodic,
}

/// <summary>
/// Every text the decision log can hold, and the shape of a line (R-110 to R-114; ADR 0037).
/// Pure functions: the callers decide when, this type decides the words, and the tests pin
/// both. The writer stamps the time with <see cref="FormatLine"/>.
/// </summary>
public static class DecisionLog
{
    /// <summary>The size above which the file is rotated before the next write: 256 KB.</summary>
    public const long MaxBytes = 262_144;

    /// <summary>The line written when the workstation locks.</summary>
    public const string Locked = "Workstation locked";

    /// <summary>The line written when the workstation unlocks.</summary>
    public const string Unlocked = "Workstation unlocked";

    /// <summary>What a line break inside an event's text becomes, so one event is one line.</summary>
    private const string BreakSeparator = " | ";

    /// <summary>Whether the file has grown past the cap and must be rotated before a write.</summary>
    /// <param name="length">The file's length in bytes.</param>
    /// <returns><c>true</c> above <see cref="MaxBytes"/>, never at or below it.</returns>
    public static bool ShouldRotate(long length) => length > MaxBytes;

    /// <summary>
    /// Renders one line of the file: the time, two spaces, the text on one line.
    /// </summary>
    /// <param name="localTime">When the event happened, in the time the owner reads.</param>
    /// <param name="text">The event's text; not null. Line breaks become <c> | </c>.</param>
    /// <returns><c>yyyy-MM-dd HH:mm:ss</c>, two spaces, the folded text.</returns>
    public static string FormatLine(DateTimeOffset localTime, string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var folded = text.Replace("\r\n", BreakSeparator).Replace("\n", BreakSeparator);
        return localTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "  " + folded;
    }

    /// <summary>The first line of a start (R-111).</summary>
    /// <param name="banner">The name and version, <see cref="VersionInfo.Banner"/>.</param>
    /// <param name="runtime">The runtime's description, as <c>RuntimeInformation.FrameworkDescription</c> gives it.</param>
    /// <param name="args">The command-line arguments, without the program's name; not null.</param>
    /// <param name="hresult">What <c>RegisterApplicationRestart</c> returned; 0 is success.</param>
    /// <returns>
    /// <c>Started &lt;banner&gt; on &lt;runtime&gt;, arguments: &lt;args or (none)&gt;; restart
    /// registration: ok</c>, or <c>refused (0x&lt;hresult&gt;)</c>.
    /// </returns>
    public static string Started(string banner, string runtime, IReadOnlyList<string> args, int hresult)
    {
        ArgumentNullException.ThrowIfNull(banner);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(args);

        var arguments = args.Count == 0 ? "(none)" : string.Join(' ', args);
        var registration = hresult == 0
            ? "ok"
            : "refused (0x" + hresult.ToString("X8", CultureInfo.InvariantCulture) + ")";

        return "Started " + banner + " on " + runtime + ", arguments: " + arguments
               + "; restart registration: " + registration;
    }

    /// <summary>Every setting in one line, as the settings lines use it (R-113).</summary>
    /// <param name="settings">The settings to describe; not null.</param>
    /// <returns>
    /// <c>periodic on every 30 min, unlock on, pause for mic on, pause for full screen on, retry
    /// 20 s, messages "Ground yourself" / "Ground yourself"</c>, with the values in force.
    /// </returns>
    public static string Summary(ReminderSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return "periodic " + OnOff(settings.PeriodicEnabled)
               + " every " + Number(settings.IntervalMinutes) + " min, unlock " + OnOff(settings.UnlockEnabled)
               + ", pause for mic " + OnOff(settings.PauseWhileMicrophoneInUse)
               + ", pause for full screen " + OnOff(settings.PauseWhileFullScreen)
               + ", retry " + Number(settings.RetrySeconds) + " s, messages \""
               + settings.UnlockMessage + "\" / \"" + settings.PeriodicMessage + "\"";
    }

    /// <summary>The words for a reason a reminder was held back or skipped.</summary>
    /// <param name="reason">The reason; <see cref="ReminderSuppression.None"/> is not one.</param>
    /// <returns><c>microphone or camera in use</c> or <c>full screen</c>.</returns>
    public static string Reason(ReminderSuppression reason) => reason switch
    {
        ReminderSuppression.MicrophoneInUse => "microphone or camera in use",
        ReminderSuppression.FullScreen => "full screen",
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "Not a reason to hold a reminder back."),
    };

    /// <summary>A reminder that appeared (R-112).</summary>
    /// <param name="kind">Which reminder.</param>
    /// <param name="message">What it said; not null.</param>
    /// <returns><c>Unlock reminder shown: &lt;message&gt;</c> or the periodic one.</returns>
    public static string ReminderShown(ReminderKind kind, string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return Name(kind) + " shown: " + message;
    }

    /// <summary>A reminder that found its window open and raised it instead (R-112).</summary>
    /// <param name="kind">Which reminder.</param>
    /// <returns><c>&lt;kind&gt; reminder: window already open, brought to front</c>.</returns>
    public static string BroughtToFront(ReminderKind kind) => Name(kind) + ": window already open, brought to front";

    /// <summary>An unlock with the unlock reminder switched off (R-013).</summary>
    /// <returns><c>Unlock reminder skipped: switched off</c>.</returns>
    public static string UnlockSkippedOff() => "Unlock reminder skipped: switched off";

    /// <summary>An unlock within the debounce of the last reminder (R-011).</summary>
    /// <param name="since">How long ago the last reminder was shown; whole seconds are kept.</param>
    /// <returns><c>Unlock reminder skipped: one shown &lt;N&gt; s ago</c>.</returns>
    public static string UnlockSkippedRecent(TimeSpan since)
        => "Unlock reminder skipped: one shown " + Number((long)since.TotalSeconds) + " s ago";

    /// <summary>An unlock reminder skipped for a pause (R-014).</summary>
    /// <param name="reason">Why; not <see cref="ReminderSuppression.None"/>.</param>
    /// <returns><c>Unlock reminder skipped: &lt;reason&gt;</c>.</returns>
    public static string UnlockSkipped(ReminderSuppression reason) => "Unlock reminder skipped: " + Reason(reason);

    /// <summary>A periodic reminder held back for the first time, or for a new reason (R-033).</summary>
    /// <param name="reason">Why; not <see cref="ReminderSuppression.None"/>.</param>
    /// <param name="retry">How often it is looked at again.</param>
    /// <returns><c>Periodic reminder held back: &lt;reason&gt;, looking again every &lt;N&gt; s</c>.</returns>
    public static string HeldBack(ReminderSuppression reason, TimeSpan retry)
        => "Periodic reminder held back: " + Reason(reason) + ", looking again every "
           + Number((long)retry.TotalSeconds) + " s";

    /// <summary>A periodic reminder that appeared after being held back (R-033).</summary>
    /// <param name="held">How long it was held back; rounded up to whole minutes, 1 at least.</param>
    /// <param name="message">What it said; not null.</param>
    /// <returns><c>Periodic reminder shown after &lt;N&gt; min held back: &lt;message&gt;</c>.</returns>
    public static string ShownAfterHold(TimeSpan held, string message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var minutes = Math.Max(1, (long)Math.Ceiling(held.TotalMinutes));
        return "Periodic reminder shown after " + Number(minutes) + " min held back: " + message;
    }

    /// <summary>Settings saved from the window and applied at once (R-026).</summary>
    /// <param name="settings">The settings now in force; not null.</param>
    /// <returns><c>Settings applied: &lt;summary&gt;</c>.</returns>
    public static string SettingsApplied(ReminderSettings settings) => "Settings applied: " + Summary(settings);

    /// <summary>Settings read from the registry at start (R-113).</summary>
    /// <param name="settings">What was read, after the clamp; not null.</param>
    /// <param name="fallbacks">The names of the values that were missing or did not read; not null.</param>
    /// <param name="clamps">One text per number out of range, <c>&lt;name&gt; &lt;read&gt; -&gt; &lt;kept&gt;</c>; not null.</param>
    /// <param name="refused">Whether the registry refused to be read, every default used.</param>
    /// <returns>
    /// <c>Settings loaded: &lt;summary&gt;</c>, then <c>; defaults used for ...</c>, <c>; clamped
    /// ...</c> or <c>; the registry refused to be read, every default used</c> as they apply.
    /// </returns>
    public static string SettingsLoaded(
        ReminderSettings settings,
        IReadOnlyList<string> fallbacks,
        IReadOnlyList<string> clamps,
        bool refused)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(fallbacks);
        ArgumentNullException.ThrowIfNull(clamps);

        var text = new StringBuilder("Settings loaded: ").Append(Summary(settings));

        if (refused)
        {
            return text.Append("; the registry refused to be read, every default used").ToString();
        }

        if (fallbacks.Count > 0)
        {
            text.Append("; defaults used for ").Append(string.Join(", ", fallbacks));
        }

        foreach (var clamp in clamps)
        {
            text.Append("; clamped ").Append(clamp);
        }

        return text.ToString();
    }

    /// <summary>Settings written to the registry (R-113).</summary>
    /// <param name="settings">What was written, after the clamp; not null.</param>
    /// <returns><c>Settings saved: &lt;summary&gt;</c>.</returns>
    public static string SettingsSaved(ReminderSettings settings) => "Settings saved: " + Summary(settings);

    /// <summary>The first run registered the application for Windows startup (R-003).</summary>
    /// <param name="command">The quoted path written to the Run key; not null.</param>
    /// <returns><c>Auto-start registered: &lt;command&gt;</c>.</returns>
    public static string AutoStartRegistered(string command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return "Auto-start registered: " + command;
    }

    /// <summary>A later run rewrote the Run key's path to the running executable (R-006).</summary>
    /// <param name="command">The quoted path now in the Run key; not null.</param>
    /// <returns><c>Auto-start entry repaired to &lt;command&gt;</c>.</returns>
    public static string AutoStartRepaired(string command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return "Auto-start entry repaired to " + command;
    }

    /// <summary>The registry refused a read or a write while registering or repairing (R-003, R-006).</summary>
    /// <param name="refusal">The exception the registry threw; not null.</param>
    /// <returns><c>Auto-start: the registry refused (&lt;type name&gt;), nothing changed</c>.</returns>
    public static string AutoStartRefused(Exception refusal)
    {
        ArgumentNullException.ThrowIfNull(refusal);
        return "Auto-start: the registry refused (" + refusal.GetType().Name + "), nothing changed";
    }

    /// <summary>The crash dialog's text, as the log records it before the dialog opens (R-114).</summary>
    /// <param name="text">What <see cref="CrashReport.Format"/> rendered; not null.</param>
    /// <returns><c>Crashed: &lt;text&gt;</c>, the line breaks left for <see cref="FormatLine"/> to fold.</returns>
    public static string Crashed(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return "Crashed: " + text;
    }

    /// <summary>The words a line starts with for each reminder.</summary>
    /// <param name="kind">Which reminder.</param>
    /// <returns><c>Unlock reminder</c> or <c>Periodic reminder</c>.</returns>
    private static string Name(ReminderKind kind) => kind == ReminderKind.Unlock ? "Unlock reminder" : "Periodic reminder";

    /// <summary>A switch, as the summary writes it.</summary>
    /// <param name="value">The switch.</param>
    /// <returns><c>on</c> or <c>off</c>.</returns>
    private static string OnOff(bool value) => value ? "on" : "off";

    /// <summary>A number in the invariant culture, so a line reads the same on every machine.</summary>
    /// <param name="value">The number.</param>
    /// <returns>Its decimal digits.</returns>
    private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);
}
