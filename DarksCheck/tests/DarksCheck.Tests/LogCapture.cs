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
using DarksCheck.Core;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace DarksCheck.Tests;

/// <summary>
/// The diagnostic log in the tests (linked into DarksCheck.Wpf.Tests too), LightsCheck's and
/// FlatCheck's. A module initializer configures NLog once, before any test runs, with one target at
/// <c>Trace</c> that renders every event through <see cref="LogSetup.LineLayout"/>: every test thus
/// also runs every log line on its path, and a line that throws fails the test that reaches it
/// instead of a user's run. A test that reads the lines opens a capture with <see cref="Start"/>;
/// it sees only the events of its own call tree (an <see cref="AsyncLocal{T}"/> scope, the
/// mechanism <see cref="Ui.Use"/> uses), so the tests running beside it add nothing; one capture at
/// a time per call tree. The tests never configure the log through LogSetup: only the two entry
/// points do.
/// </summary>
internal sealed class LogCapture : IDisposable
{
    /// <summary>The capture open in this call tree, or null.</summary>
    private static readonly AsyncLocal<LogCapture?> Current = new();

    /// <summary>The events seen, in the order they were written.</summary>
    private readonly ConcurrentQueue<Captured> _events = new();

    /// <summary>Opens a capture for the calling test's call tree.</summary>
    private LogCapture() => Current.Value = this;

    /// <summary>The events seen so far.</summary>
    internal IReadOnlyList<Captured> Events => _events.ToArray();

    /// <summary>The messages seen so far, without time, level, ids or exception.</summary>
    internal IReadOnlyList<string> Messages => _events.Select(e => e.Message).ToArray();

    /// <summary>Every rendered line, exception included, joined by newlines.</summary>
    internal string Text => string.Join("\n", _events.Select(e => e.Line));

    /// <summary>Configures the test assembly's log before any test runs.</summary>
    [ModuleInitializer]
    internal static void Install()
    {
        LoggingConfiguration config = new() { DefaultCultureInfo = CultureInfo.InvariantCulture };
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

    /// <summary>One event as the test sees it.</summary>
    /// <param name="Level">Its level.</param>
    /// <param name="Logger">The logger's name.</param>
    /// <param name="Message">The message alone, without time, level, ids or exception.</param>
    /// <param name="Line">The whole rendered line, exception included.</param>
    /// <param name="Exception">The exception logged with it, or null.</param>
    internal sealed record Captured(LogLevel Level, string Logger, string Message, string Line, Exception? Exception);

    /// <summary>The target: renders every event, keeps it in the capture open in its call tree.</summary>
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
            Current.Value?._events.Enqueue(new Captured(logEvent.Level, logEvent.LoggerName ?? "", logEvent.FormattedMessage ?? "", line, logEvent.Exception));
        }
    }
}
