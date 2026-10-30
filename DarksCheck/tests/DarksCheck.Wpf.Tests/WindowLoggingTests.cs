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

using System.Reflection;
using System.Runtime.Versioning;
using DarksCheck.Tests;
using NLog.Targets;
using Xunit.Abstractions;

namespace DarksCheck.Wpf.Tests;

/// <summary>
/// The window's side of the diagnostic log (R-101, R-103, ADR 0025): two writers on one file
/// through the build of the atomic target the window ships. LightsCheck's WindowLoggingTests are
/// the model.
/// </summary>
public class WindowLoggingTests
{
    private readonly ITestOutputHelper _output;

    /// <summary>The runner's output, for the measured file list.</summary>
    public WindowLoggingTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// Two windows writing one log keep every line (R-103), checked here on the build of the atomic
    /// target the window ships, <c>net8.0-windows</c>, a binary of its own that the core's tests do
    /// not load (LightsCheck's review of 2026-10-30).
    /// </summary>
    [Fact]
    public void R103_TheWindowsBuildOfTheAtomicTargetKeepsEveryLineOfTwoWindows()
    {
        Assert.Equal("Windows7.0", typeof(AtomicFileTarget).Assembly.GetCustomAttribute<TargetPlatformAttribute>()?.PlatformName);
        using TempWorkspace ws = new();

        LogFiles.TwoWritersKeepEveryLine(Path.Combine(ws.Dir, "log"), "DarksCheck.Wpf", _output);
    }
}
