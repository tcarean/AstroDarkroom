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
using System.Windows;
using CopyWcs.Tests;
using NLog;

namespace CopyWcs.Wpf.Tests;

/// <summary>
/// Closing the window during a run (R-050): the close waits for the run, then the window closes
/// by itself, and the file the run writes is whole. A run is held in flight by replacing the
/// view-model's <see cref="MainViewModel.RunCore"/> with one that waits for the test.
/// </summary>
[Collection("window")]
public class WindowCloseTests
{
    private readonly StaFixture _sta;

    public WindowCloseTests(StaFixture sta) => _sta = sta;

    /// <summary>A run that waits until the test releases it, then runs the core as the window would.</summary>
    internal static Func<WcsCopyRequest, WcsCopyOutcome> HeldRun(ManualResetEventSlim release)
        => request =>
        {
            release.Wait(TimeSpan.FromSeconds(30));
            return WcsCopy.Run(request);
        };

    /// <summary>
    /// A close, and a second one, while a Copy into a new file is in flight leave the window open;
    /// once the copy ends the window closes, once, and the new file is the console's byte for byte
    /// (CR-002 of the review of 2026-11-16: the close ended the process inside the write).
    /// </summary>
    [Fact]
    public Task R050_ClosingTheWindowDuringACopy_WaitsForTheCopy_ThenCloses()
        => _sta.InvokeAsync(async () =>
        {
            using WindowFixture f = new();
            using ManualResetEventSlim release = new();
            string output = f.FilePath("out.fit");
            MainViewModel vm = new() { Source = f.Source, Dest = f.Dest, WriteInPlace = false, Output = output, RunCore = HeldRun(release) };
            MainWindow w = new(vm);
            int closed = 0;
            w.Closed += (_, _) => closed++;

            Task copy = vm.Copy();
            try
            {
                Assert.True(vm.IsRunning);
                w.Close();
                Assert.Equal(0, closed);
                w.Close();
                Assert.Equal(0, closed);
            }
            finally
            {
                release.Set();
                await copy;
            }

            Assert.Equal(1, closed);
            Assert.False(vm.IsRunning);
            string console = f.FilePath("console.fit");
            Assert.Equal(0, CopyWcsCli.Run([f.Source, f.Dest, "-o", console], new StringWriter(), new StringWriter()));
            Assert.Equal(File.ReadAllBytes(console), File.ReadAllBytes(output));
        });

    /// <summary>A Preview in flight holds a close the same way: the window closes once the preview ends.</summary>
    [Fact]
    public Task R050_ClosingTheWindowDuringAPreview_WaitsForThePreview_ThenCloses()
        => _sta.InvokeAsync(async () =>
        {
            using WindowFixture f = new();
            using ManualResetEventSlim release = new();
            MainViewModel vm = new() { Source = f.Source, Dest = f.Dest, RunCore = HeldRun(release) };
            MainWindow w = new(vm);
            int closed = 0;
            w.Closed += (_, _) => closed++;

            Task preview = vm.Preview();
            try
            {
                w.Close();
                Assert.Equal(0, closed);
            }
            finally
            {
                release.Set();
                await preview;
            }

            Assert.Equal(1, closed);
        });

    /// <summary>
    /// Once a close is held the window takes no more input until it closes, so no dialog opened
    /// after the close is still open when the run ends and the window closes over it (R-050; the
    /// review of 2026-11-18: About and the three Browse buttons stayed live).
    /// </summary>
    [Fact]
    public Task R050_ACloseHeldDuringARun_TakesNoMoreInputUntilTheWindowCloses()
        => _sta.InvokeAsync(async () =>
        {
            using WindowFixture f = new();
            using ManualResetEventSlim release = new();
            MainViewModel vm = new() { Source = f.Source, Dest = f.Dest, RunCore = HeldRun(release) };
            MainWindow w = new(vm);
            int closed = 0;
            w.Closed += (_, _) => closed++;

            Task preview = vm.Preview();
            try
            {
                Assert.True(w.IsEnabled);
                w.Close();
                Assert.False(w.IsEnabled);
                Assert.Equal(0, closed);
            }
            finally
            {
                release.Set();
                await preview;
            }

            Assert.Equal(1, closed);
        });

    /// <summary>
    /// Windows ending the session during a run closes the window at once, as WPF closes every
    /// window then, with the cancel ignored; the log says the run was cut short, after the line of
    /// the close it could not hold, and the run's end later closes nothing more (R-050; the review
    /// of 2026-11-18: the log said the window would close when the run ended).
    /// </summary>
    [Fact]
    public Task R050_WindowsEndingTheSessionDuringARun_ClosesTheWindow_AndTheLogSaysTheRunIsCutShort()
        => _sta.InvokeAsync(async () =>
        {
            using WindowFixture f = new();
            using ManualResetEventSlim release = new();
            MainViewModel vm = new() { Source = f.Source, Dest = f.Dest, RunCore = HeldRun(release) };
            MainWindow w = new(vm);
            int closed = 0;
            w.Closed += (_, _) => closed++;
            using LogCapture capture = LogCapture.Start();

            Task preview = vm.Preview();
            try
            {
                CloseAsAtTheSessionsEnd(w);
                Assert.Equal(1, closed);
            }
            finally
            {
                release.Set();
                await preview;
            }

            Assert.Equal(1, closed);
            LogCapture.Captured[] lines = capture.Events.Where(e => e.Logger == "Window").ToArray();
            Assert.Equal(2, lines.Length);
            Assert.Equal("close asked while a run is in flight: the window closes when it ends", lines[0].Message);
            Assert.Equal("the window closed while a run is in flight: Windows ended the session, the run is cut short", lines[1].Message);
            Assert.Equal(LogLevel.Warn, lines[1].Level);
        });

    /// <summary>
    /// Closes a window as WPF does when Windows ends the session: <c>Application.Shutdown</c> closes
    /// every window through <c>Window.InternalClose(shutdown: true, ignoreCancel: true)</c>, which no
    /// handler can cancel. Reached by reflection, since the tests' one Application serves every
    /// window test and cannot be shut down.
    /// </summary>
    private static void CloseAsAtTheSessionsEnd(Window w)
    {
        MethodInfo close = typeof(Window).GetMethod("InternalClose", BindingFlags.Instance | BindingFlags.NonPublic, [typeof(bool), typeof(bool)])
            ?? throw new InvalidOperationException("WPF's Window.InternalClose(bool, bool) was not found");
        close.Invoke(w, [true, true]);
    }

    /// <summary>With no run in flight the window closes at once.</summary>
    [Fact]
    public void R050_ClosingTheWindowWithNoRunInFlight_ClosesAtOnce()
        => _sta.Invoke(() =>
        {
            MainWindow w = new();
            int closed = 0;
            w.Closed += (_, _) => closed++;

            w.Close();

            Assert.Equal(1, closed);
        });
}
