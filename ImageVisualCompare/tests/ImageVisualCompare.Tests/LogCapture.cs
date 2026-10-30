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
using ImageVisualCompare.Wpf.Logging;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace ImageVisualCompare.Tests;

/// <summary>
/// The diagnostic log in the tests (ADR 0043). A module initializer configures NLog once, before
/// any test runs, with one target at <c>Trace</c> that renders every event through
/// <see cref="LogSetup.LineLayout"/>, and with NLog throwing rather than swallowing: every test
/// thus runs every log line on its path, and a line that throws, in its composition, its layout or
/// the target, fails the test that reaches it instead of a user's run. A test that reads the
/// lines opens a capture with <see cref="Start"/>; it sees only the events of its own call tree
/// (an <see cref="AsyncLocal{T}"/> scope that flows into the tasks it starts), so the tests
/// running beside it add nothing; one capture at a time per call tree. The tests never call
/// <see cref="LogSetup.Apply"/>: only the application's entry point does, which no test runs.
/// </summary>
internal sealed class LogCapture : IDisposable
{
    /// <summary>The capture open in this call tree, or null.</summary>
    private static readonly AsyncLocal<LogCapture?> Current = new();

    /// <summary>The events seen, in the order they were written.</summary>
    private readonly ConcurrentQueue<Captured> _events = new();

    /// <summary>Opens a capture for the calling test's call tree.</summary>
    private LogCapture() => Current.Value = this;

    /// <summary>One event as the test sees it.</summary>
    /// <param name="Level">Its level.</param>
    /// <param name="Logger">The logger's name.</param>
    /// <param name="Message">The message alone, without time, level or ids.</param>
    /// <param name="Line">The whole rendered line, exception included.</param>
    /// <param name="Exception">The exception logged with it, or null.</param>
    internal sealed record Captured(LogLevel Level, string Logger, string Message, string Line, Exception? Exception);

    /// <summary>The events seen so far.</summary>
    internal IReadOnlyList<Captured> Events => [.. _events];

    /// <summary>The messages seen so far, without time, level or ids.</summary>
    internal IReadOnlyList<string> Messages => [.. _events.Select(e => e.Message)];

    /// <summary>
    /// Configures the test assembly's log before any test runs, with NLog throwing what it would
    /// swallow in a user's run, an exception in a layout or in the target, so that it fails the
    /// test whose line caused it.
    /// </summary>
    [ModuleInitializer]
    internal static void Install()
    {
        LogManager.ThrowExceptions = true;
        var config = new LoggingConfiguration { DefaultCultureInfo = CultureInfo.InvariantCulture };
        config.AddRule(LogLevel.Trace, LogLevel.Fatal, new CaptureTarget(), "*");
        LogManager.Configuration = config;
    }

    /// <summary>
    /// Opens a capture: from now until <see cref="Dispose"/>, the events of this call tree are
    /// kept. Not nested: a test opens one at a time.
    /// </summary>
    /// <returns>The capture.</returns>
    internal static LogCapture Start()
    {
        if (Current.Value is not null)
        {
            throw new InvalidOperationException("a capture is already open in this call tree");
        }

        return new LogCapture();
    }

    /// <summary>Closes the capture; the events stay readable.</summary>
    public void Dispose() => Current.Value = null;

    /// <summary>The messages of one logger, in order.</summary>
    /// <param name="logger">The logger's name, one of <see cref="Loggers"/>.</param>
    /// <returns>Its messages.</returns>
    internal IReadOnlyList<string> MessagesOf(string logger) =>
        [.. _events.Where(e => e.Logger == logger).Select(e => e.Message)];

    /// <summary>The target: renders every event and keeps it in the capture open in its call tree.</summary>
    private sealed class CaptureTarget : TargetWithLayout
    {
        /// <summary>A target named <c>capture</c>, with the application's line layout.</summary>
        public CaptureTarget()
        {
            Name = "capture";
            Layout = LogSetup.LineLayout;
        }

        /// <inheritdoc/>
        protected override void Write(LogEventInfo logEvent)
        {
            // Rendered whether or not a capture is open, so a line that throws fails every test
            // that reaches it, not only the tests that read the log.
            string line = RenderLogEvent(Layout, logEvent);
            Current.Value?._events.Enqueue(new Captured(
                logEvent.Level, logEvent.LoggerName ?? string.Empty, logEvent.FormattedMessage ?? string.Empty, line, logEvent.Exception));
        }
    }
}
