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

using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.CompilerServices;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace FlatCheck.Tests;

/// <summary>
/// The diagnostic log in the tests (linked into FlatCheck.Wpf.Tests too). A module initializer
/// configures NLog once, before any test runs, with one target at <c>Trace</c> that renders
/// every event through <see cref="LogSetup.LineLayout"/>: every test thus also runs every log
/// line on its path, and a line that throws fails the test that reaches it instead of a user's
/// run. A test that reads the lines opens a capture with <see cref="Start"/>; it sees only the
/// events of its own call tree (an <see cref="AsyncLocal{T}"/> scope, the same mechanism as
/// <see cref="Ui.Use"/>), so the tests running beside it add nothing. The tests never call
/// <see cref="LogSetup.Apply"/>: only the two entry points do.
/// </summary>
internal sealed class LogCapture : IDisposable
{
    /// <summary>The capture open in this call tree, or null.</summary>
    private static readonly AsyncLocal<LogCapture?> current = new();

    /// <summary>The capture this one replaced, put back by <see cref="Dispose"/>.</summary>
    private readonly LogCapture? previous;

    /// <summary>The events seen, in the order they were written.</summary>
    private readonly ConcurrentQueue<Captured> events = new();

    /// <summary>One event as the test sees it.</summary>
    /// <param name="Level">Its level.</param>
    /// <param name="Logger">The logger's name.</param>
    /// <param name="Message">The message alone, without time, level or ids.</param>
    /// <param name="Line">The whole rendered line, exception included.</param>
    /// <param name="Exception">The exception logged with it, or null.</param>
    internal sealed record Captured(LogLevel Level, string Logger, string Message, string Line, Exception? Exception);

    /// <summary>Configures the test assembly's log before any test runs.</summary>
    [ModuleInitializer]
    internal static void Install()
    {
        var config = new LoggingConfiguration { DefaultCultureInfo = CultureInfo.InvariantCulture };
        config.AddRule(LogLevel.Trace, LogLevel.Fatal, new CaptureTarget(), "*");
        LogManager.Configuration = config;
    }

    /// <summary>Opens a capture for the calling test's call tree.</summary>
    private LogCapture()
    {
        previous = current.Value;
        current.Value = this;
    }

    /// <summary>Opens a capture: from now until <see cref="Dispose"/>, the events of this call
    /// tree are kept.</summary>
    /// <returns>The capture.</returns>
    internal static LogCapture Start() => new();

    /// <summary>Closes the capture; the events stay readable.</summary>
    public void Dispose() => current.Value = previous;

    /// <summary>The events seen so far.</summary>
    internal IReadOnlyList<Captured> Events => events.ToArray();

    /// <summary>The messages seen so far, without time, level or ids.</summary>
    internal IReadOnlyList<string> Messages => events.Select(e => e.Message).ToArray();

    /// <summary>Every rendered line, joined by newlines.</summary>
    internal string Text => string.Join("\n", events.Select(e => e.Line));

    /// <summary>The target: renders every event, keeps it in the open captures.</summary>
    private sealed class CaptureTarget : TargetWithLayout
    {
        /// <summary>A target named <c>capture</c> with the applications' line layout.</summary>
        public CaptureTarget()
        {
            Name = "capture";
            Layout = LogSetup.LineLayout;
        }

        /// <inheritdoc/>
        protected override void Write(LogEventInfo logEvent)
        {
            string line = RenderLogEvent(Layout, logEvent);
            var seen = new Captured(logEvent.Level, logEvent.LoggerName ?? "", logEvent.FormattedMessage ?? "", line, logEvent.Exception);
            // A capture opened inside another sees the event, and so do the ones around it.
            for (LogCapture? c = current.Value; c != null; c = c.previous)
                c.events.Enqueue(seen);
        }
    }
}
