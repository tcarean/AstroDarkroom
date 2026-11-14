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

using System.Diagnostics;

namespace AstroDarkroom.Core;

/// <summary>
/// The start of an app as the core plans it (R-003); <see cref="Launcher.StartInfo"/> turns it
/// into what the window passes to Process.Start. The core never starts a process itself
/// (the rule "Core has no UI").
/// </summary>
/// <param name="FileName">The full path of the executable to start.</param>
/// <param name="WorkingDirectory">The folder to start it in: the suite folder, where its own files are.</param>
/// <param name="Arguments">The command line after the executable: the launcher's language as <c>--lang=ro</c> or <c>--lang=en</c>, one token (R-008). A string, so the record keeps its value equality.</param>
public sealed record LaunchPlan(string FileName, string WorkingDirectory, string Arguments);

/// <summary>Plans the start of an app from the suite folder (R-003).</summary>
public static class Launcher
{
    /// <summary>
    /// The start of an available app: its window executable, run from the suite folder, in
    /// the launcher's current language (R-008), the form every component's window accepts.
    /// </summary>
    /// <param name="app">The app to start; not null.</param>
    /// <param name="folder">The suite folder the app is looked up in; not null.</param>
    /// <returns>The file to start, the working directory and the <c>--lang=</c> argument.</returns>
    /// <exception cref="InvalidOperationException">The app is not in the folder; the message is the reason of R-002.</exception>
    public static LaunchPlan Plan(SuiteApp app, SuiteFolder folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        AppLocation location = folder.Locate(app);
        if (!location.IsAvailable)
        {
            throw new InvalidOperationException(location.Reason);
        }

        return new LaunchPlan(location.Path, folder.BaseDirectory, "--lang=" + Ui.Code(Ui.Current));
    }

    /// <summary>
    /// What the window hands to Process.Start for a plan (R-003): the plan's file, working
    /// folder and arguments, started directly, not through the shell. The shell asks Windows'
    /// question about a downloaded program again for every executable that carries the mark of
    /// the web, which every file taken out of a downloaded zip does; the user has answered it
    /// for the launcher, and a direct start does not ask it for each tool (ADR 0027). The core
    /// still starts nothing.
    /// </summary>
    /// <param name="plan">The start, as <see cref="Plan"/> gave it; not null.</param>
    /// <returns>The start information, a new object at each call.</returns>
    public static ProcessStartInfo StartInfo(LaunchPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return new ProcessStartInfo(plan.FileName)
        {
            UseShellExecute = false,
            WorkingDirectory = plan.WorkingDirectory,
            Arguments = plan.Arguments,
        };
    }
}
