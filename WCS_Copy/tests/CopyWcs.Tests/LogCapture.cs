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

namespace CopyWcs.Tests;

/// <summary>
/// The diagnostic log in the tests (linked into CopyWcs.Wpf.Tests too), FlatCheck's. A module
/// initializer configures NLog once, before any test runs, with one target at <c>Trace</c> that
/// renders every event through <see cref="LogSetup.LineLayout"/>: every test thus also runs every
/// log line on its path, and a line whose message throws while it is composed fails the test
/// that reaches it instead of a user's run (an error inside NLog's own rendering is swallowed, as
/// in a user's run). A test that reads the lines opens a capture with <see cref="Start"/>; it sees only
/// the events of its own call tree (an <see cref="AsyncLocal{T}"/> scope, the same mechanism as
/// <see cref="Ui.Use"/>), so the tests running beside it add nothing; one capture at a time per
/// call tree. The tests never call <see cref="LogSetup.Apply"/>: only the two entry points do.
/// </summary>
internal sealed class LogCapture : IDisposable
{
    /// <summary>The capture open in this call tree, or null.</summary>
    private static readonly AsyncLocal<LogCapture?> Current = new();

    /// <summary>The events seen, in the order they were written.</summary>
    private readonly ConcurrentQueue<Captured> _events = new();

    /// <summary>The prefix of the message <see cref="_atAction"/> waits for.</summary>
    private string _atPrefix = "";

    /// <summary>The action to run at the first message that starts with <see cref="_atPrefix"/>, or null.</summary>
    private Action? _atAction;

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
        Current.Value = this;
    }

    /// <summary>Opens a capture: from now until <see cref="Dispose"/>, the events of this call
    /// tree are kept. Not nested: a test opens one at a time.</summary>
    /// <returns>The capture.</returns>
    internal static LogCapture Start()
    {
        if (Current.Value != null)
        {
            throw new InvalidOperationException("a capture is already open in this call tree");
        }

        return new();
    }

    /// <summary>Closes the capture; the events stay readable.</summary>
    public void Dispose() => Current.Value = null;

    /// <summary>
    /// Runs an action once, at the first event of this capture whose message starts with a
    /// prefix, on the thread that logs it and before the code that logged it goes on: a test's
    /// way to act at a moment inside a run, such as moving the working folder as a write begins.
    /// Once even when two threads of the capture log at the same moment. NLog swallows what the
    /// action throws, so a test should also assert that the action ran.
    /// </summary>
    /// <param name="prefix">The start of the message to wait for, compared ordinally.</param>
    /// <param name="action">What to do then.</param>
    internal void At(string prefix, Action action)
    {
        _atPrefix = prefix;
        _atAction = action;
    }

    /// <summary>Keeps an event, then runs the action <see cref="At"/> set when its message is the one waited for.</summary>
    /// <param name="captured">The event.</param>
    private void Keep(Captured captured)
    {
        _events.Enqueue(captured);
        if (_atAction is not null && captured.Message.StartsWith(_atPrefix, StringComparison.Ordinal)
            && Interlocked.Exchange(ref _atAction, null) is { } action)
        {
            action();
        }
    }

    /// <summary>The events seen so far.</summary>
    internal IReadOnlyList<Captured> Events => _events.ToArray();

    /// <summary>The messages seen so far, without time, level or ids.</summary>
    internal IReadOnlyList<string> Messages => _events.Select(e => e.Message).ToArray();

    /// <summary>Every rendered line, joined by newlines.</summary>
    internal string Text => string.Join("\n", _events.Select(e => e.Line));

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
            Current.Value?.Keep(new Captured(logEvent.Level, logEvent.LoggerName ?? "", logEvent.FormattedMessage ?? "", line, logEvent.Exception));
        }
    }
}
