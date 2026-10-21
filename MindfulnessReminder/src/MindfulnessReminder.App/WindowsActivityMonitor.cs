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
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using Microsoft.Win32;
using MindfulnessReminder.Core;

namespace MindfulnessReminder.App;

/// <summary>
/// The two Windows questions behind the "pause" settings, asked and handed to Core to
/// interpret.
///
/// MICROPHONE AND CAMERA come from the usage record Windows keeps for its own privacy
/// indicator -- the microphone icon in the tray. Every app that opens a capture device gets
/// a key under CapabilityAccessManager\ConsentStore with a LastUsedTimeStart and a
/// LastUsedTimeStop, and while the stream is open the stop time is zero. Reading that,
/// rather than matching window titles or process names, is what makes this work for software
/// nobody thought of: Zoom, Teams, a Slack huddle, Meet or Teams in a browser tab, Webex,
/// Discord. Window titles would have needed a table per application, would have broken on
/// the next release of each, and would have missed a Slack huddle entirely -- it has no
/// window of its own to match.
///
/// FULL SCREEN comes from SHQueryUserNotificationState, which is the same question Windows
/// asks itself before showing a notification.
///
/// Nothing here decides anything. The raw shell state goes to
/// <see cref="UserNotificationState.FromShellState"/> as an integer, and what the two
/// answers mean together is <see cref="SuppressionPolicy"/>'s business.
/// </summary>
internal sealed class WindowsActivityMonitor : IActivityMonitor
{
    private const string ConsentStorePath =
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore";

    private const string MicrophoneCapability = "microphone";

    private const string CameraCapability = "webcam";

    private const string StartValueName = "LastUsedTimeStart";

    private const string StopValueName = "LastUsedTimeStop";

    /// <summary>
    /// How far down each capability key to walk. Classic applications sit one level down,
    /// under "NonPackaged"; Store apps sit directly under the capability, and some of those
    /// keep a further key per app id. Two levels reaches all three, and a fixed depth means a
    /// hive somebody has grown a deep subtree in cannot turn this into a long walk.
    /// </summary>
    private const int ScanDepth = 2;

    /// <summary>
    /// Both hives are read. HKCU holds what the signed-in user runs, which is every meeting
    /// application; HKLM holds services, which can hold the microphone open on their behalf.
    /// </summary>
    private static readonly RegistryKey[] Hives = { Registry.CurrentUser, Registry.LocalMachine };

    /// <inheritdoc/>
    public ActivitySnapshot Read() => new(
        InUse(MicrophoneCapability),
        InUse(CameraCapability),
        UserNotificationState.FromShellState(ShellState()));

    /// <summary>
    /// What <c>--detect</c> prints. The feature cannot be exercised without a real call or a
    /// real game, so the one way to tell a wrong answer from a wrong setting is to be able to
    /// see the raw readings -- which application is holding the microphone, and what number
    /// the shell actually returned.
    /// </summary>
    /// <returns>
    /// Lines saying who holds the microphone and the camera, then the shell's number and what
    /// it maps to.
    /// </returns>
    public string Describe()
    {
        var shell = ShellState();
        var report = new StringBuilder();

        report.Append("Microphone in use: ").Append(Describe(MicrophoneCapability)).AppendLine();
        report.Append("Camera in use: ").Append(Describe(CameraCapability)).AppendLine();
        report.AppendLine();
        report.Append("Shell notification state: ").Append(shell)
              .Append(" (").Append(UserNotificationState.FromShellState(shell)).Append(')')
              .AppendLine();

        return report.ToString();
    }

    /// <summary>Says whether a capture device is open, and by which applications.</summary>
    /// <param name="capability">The ConsentStore key: "microphone" or "webcam".</param>
    /// <returns>"no", or "yes -- " followed by the holders' names, comma-separated.</returns>
    private string Describe(string capability)
    {
        var holders = Holders(capability);

        return holders.Count == 0
            ? "no"
            : "yes -- " + string.Join(", ", holders);
    }

    /// <summary>
    /// Asks the shell whether it would show a notification now. A failed call, or a Windows
    /// without the function, answers 0.
    /// </summary>
    /// <returns>The QUERY_USER_NOTIFICATION_STATE number, or 0.</returns>
    private static int ShellState()
    {
        try
        {
            // A non-zero HRESULT leaves the out parameter undefined, so it must not be read.
            // Zero is not a member of the enum and maps to Unknown, which suppresses nothing:
            // a shell that will not answer must not be able to switch the reminder off.
            return SHQueryUserNotificationState(out var state) == 0 ? state : 0;
        }
        catch (DllNotFoundException)
        {
            return 0;
        }
        catch (EntryPointNotFoundException)
        {
            return 0;
        }
    }

    /// <summary>Whether any application has a capture device open now.</summary>
    /// <param name="capability">The ConsentStore key: "microphone" or "webcam".</param>
    /// <returns><c>true</c> when a key in either hive reports it open.</returns>
    private bool InUse(string capability) => Holders(capability).Count > 0;

    /// <summary>
    /// Finds the keys, in both hives, reporting the device open right now. A key's name is an
    /// executable path with backslashes written as '#', or a package family name.
    /// </summary>
    /// <param name="capability">The ConsentStore key: "microphone" or "webcam".</param>
    /// <returns>
    /// The name of each, made readable by <see cref="LeafName"/>; empty when none is open, or
    /// none could be read.
    /// </returns>
    private static List<string> Holders(string capability)
    {
        var holders = new List<string>();

        foreach (var hive in Hives)
        {
            try
            {
                using var key = hive.OpenSubKey(ConsentStorePath + "\\" + capability);
                if (key is not null)
                {
                    Collect(key, ScanDepth, holders);
                }
            }
            catch (SecurityException)
            {
                // Fail open, here and below. A hive this process cannot read is a reason to
                // show the reminder anyway; the alternative is a reminder that silently never
                // appears again, which is the worse of the two failures by a long way.
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (IOException)
            {
            }
        }

        return holders;
    }

    /// <summary>
    /// Adds <paramref name="key"/>'s name when it reports the device open, then does the same
    /// for its subkeys, <paramref name="depth"/> levels down. A subkey that cannot be read is
    /// skipped.
    /// </summary>
    /// <param name="key">The key to look at.</param>
    /// <param name="depth">How many levels below the key to walk; 0 looks at the key alone.</param>
    /// <param name="holders">The list the names are added to.</param>
    private static void Collect(RegistryKey key, int depth, List<string> holders)
    {
        if (IsOpenNow(key))
        {
            holders.Add(LeafName(key));
        }

        if (depth == 0)
        {
            return;
        }

        foreach (var name in key.GetSubKeyNames())
        {
            try
            {
                using var child = key.OpenSubKey(name);
                if (child is not null)
                {
                    Collect(child, depth - 1, holders);
                }
            }
            catch (SecurityException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (IOException)
            {
            }
        }
    }

    /// <summary>
    /// The name of the application a key stands for, for the diagnostic to print.
    ///
    /// Path.GetFileName is the wrong tool: these key names are whole executable paths with
    /// every backslash written as a '#', so it splits at the drive colon and returns most of
    /// the path. The last path segment of the KEY is the name, and putting the backslashes
    /// back is what turns it from a mangled string into something readable. Package family
    /// names, which is what Store apps get, contain no '#' and come through unchanged.
    /// </summary>
    /// <param name="key">A key under a capability.</param>
    /// <returns>
    /// The last segment of the key's name, with each '#' turned back into a backslash.
    /// </returns>
    private static string LeafName(RegistryKey key)
        => key.Name[(key.Name.LastIndexOf('\\') + 1)..].Replace('#', '\\');

    /// <summary>
    /// Whether the key reports the device open right now.
    ///
    /// A start time on record and a stop time of exactly zero. Both halves matter: an app
    /// that has never touched the device has neither value, and requiring the stop time to be
    /// PRESENT and zero -- rather than treating a missing one as "still running" -- is what
    /// stops a half-written key, or one left behind by an application that crashed holding
    /// the microphone, from suppressing every reminder from then on.
    /// </summary>
    /// <param name="key">A key under a capability.</param>
    /// <returns>
    /// <c>true</c> when LastUsedTimeStart is above zero and LastUsedTimeStop is present and
    /// zero.
    /// </returns>
    private static bool IsOpenNow(RegistryKey key)
        => key.GetValue(StartValueName) is long start
           && start > 0
           && key.GetValue(StopValueName) is long stop
           && stop == 0;

    /// <summary>
    /// shell32's SHQueryUserNotificationState: whether the shell would show a notification
    /// now.
    /// </summary>
    /// <param name="state">
    /// The QUERY_USER_NOTIFICATION_STATE; undefined when the call fails.
    /// </param>
    /// <returns>An HRESULT: 0 when <paramref name="state"/> was written.</returns>
    [DllImport("shell32.dll")]
    private static extern int SHQueryUserNotificationState(out int state);
}
