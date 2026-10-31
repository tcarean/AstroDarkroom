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
using System.Windows;
using System.Xml.Linq;

namespace ImageVisualCompare.Tests;

/// <summary>
/// The WPF test thread itself (ADR 0032, ADR 0046): it shows nothing and leaves the interface's
/// language alone. WPF's <c>Application</c> constructor queues the application's start, which
/// runs at the thread's first dispatcher passes even though <c>Application.Run</c> is never
/// called, so the thread's application object must be a plain <c>Application</c>, whose start is
/// empty, never the application's <c>App</c> or a class built on it: no <c>StartupUri</c> window,
/// no <c>OnStartup</c> that reads the test host's arguments for the language. And since the tests
/// load the theme without <c>App.xaml</c>, the resources of <c>App.xaml</c> must be that theme
/// alone.
/// </summary>
public class WpfThreadTests
{
    [Fact]
    public async Task TheTestThreadRunsNoApplicationStart_AndShowsNoWindow()
    {
        // The start runs in the dispatcher's first passes: give it a few before looking.
        for (int i = 0; i < 5; i++)
        {
            await WpfThread.RunAsync(() => { });
            await Task.Delay(100);
        }

        await WpfThread.RunAsync(() =>
        {
            Assert.IsType<Application>(Application.Current);
            Assert.Equal(ShutdownMode.OnExplicitShutdown, Application.Current.ShutdownMode);
            Assert.Empty(VisibleWindowsOfThisThread());
        });
    }

    /// <summary>
    /// The thread loads <c>Theme.xaml</c> alone, so a resource put in <c>App.xaml</c> would be
    /// missing from every test and show up only when the application is launched: the resources
    /// of <c>App.xaml</c>, comments aside, must be the theme's merge and nothing else, element for
    /// element and attribute for attribute.
    /// </summary>
    [Fact]
    public void TheApplicationsResourcesAreTheThemeAlone()
    {
        XNamespace wpf = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XElement app = XDocument.Load(Path.Combine(TestData.RepoDir(), "src", "ImageVisualCompare.Wpf", "App.xaml")).Root!;
        var resources = new XElement(Assert.Single(app.Elements(wpf + "Application.Resources")));
        resources.DescendantNodes().OfType<XComment>().Remove();
        var expected = new XElement(wpf + "Application.Resources",
            new XElement(wpf + "ResourceDictionary",
                new XElement(wpf + "ResourceDictionary.MergedDictionaries",
                    new XElement(wpf + "ResourceDictionary", new XAttribute("Source", "Theme.xaml")))));

        Assert.True(
            XNode.DeepEquals(expected, resources),
            "App.xaml's resources must be the merge of Theme.xaml alone, which the WPF test thread "
            + "loads without App.xaml (ADR 0046); a resource for the windows goes in Theme.xaml. "
            + "App.xaml has: " + resources);
    }

    /// <summary>
    /// The titles of the calling thread's visible top-level windows, the Win32 ones included, a
    /// file dialog or a message box, which <c>Application.Windows</c> does not list.
    /// </summary>
    private static string[] VisibleWindowsOfThisThread()
    {
        var titles = new List<string>();
        EnumThreadWindows(GetCurrentThreadId(), (window, _) =>
        {
            if (IsWindowVisible(window))
            {
                char[] title = new char[256];
                titles.Add(new string(title, 0, GetWindowText(window, title, title.Length)));
            }

            return true;
        }, IntPtr.Zero);
        return [.. titles];
    }

    private delegate bool EnumThreadWindowsProc(IntPtr window, IntPtr param);

    [DllImport("user32.dll")]
    private static extern bool EnumThreadWindows(uint threadId, EnumThreadWindowsProc callback, IntPtr param);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr window);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr window, char[] text, int capacity);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();
}
