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
using System.Diagnostics;

namespace LightsCheck.Wpf.Tests;

/// <summary>
/// A synchronization context that holds every post until the test runs it, so the order in
/// which the view-model's continuations and progress reports land is the test's to fix: what a
/// Progress&lt;T&gt; posts and what an await continues with wait in one queue, as they do on the
/// window's dispatcher. Installed on the current thread at construction; the previous context
/// comes back at dispose. The tests that use it stay synchronous, so the thread never changes.
/// </summary>
internal sealed class QueuedContext : SynchronizationContext, IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);
    private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> _posts = new();
    private readonly SynchronizationContext? _previous = Current;

    internal QueuedContext() => SetSynchronizationContext(this);

    public override void Post(SendOrPostCallback d, object? state) => _posts.Enqueue((d, state));

    public override void Send(SendOrPostCallback d, object? state) => d(state);

    /// <summary>Blocks until at least one post is queued and not yet run.</summary>
    internal void WaitForAPost()
    {
        Stopwatch sw = Stopwatch.StartNew();
        while (_posts.IsEmpty)
        {
            if (sw.Elapsed > Timeout)
            {
                throw new TimeoutException("no post was queued");
            }

            Thread.Sleep(1);
        }
    }

    /// <summary>
    /// Runs the queued posts, in order, until the task completes; a fault of the task is the
    /// caller's to look at.
    /// </summary>
    internal void RunUntil(Task task)
    {
        Stopwatch sw = Stopwatch.StartNew();
        while (!task.IsCompleted)
        {
            if (_posts.TryDequeue(out (SendOrPostCallback Callback, object? State) post))
            {
                post.Callback(post.State);
                continue;
            }

            if (sw.Elapsed > Timeout)
            {
                throw new TimeoutException("the task did not complete while the posts were run");
            }

            Thread.Sleep(1);
        }
    }

    public void Dispose() => SetSynchronizationContext(_previous);
}
