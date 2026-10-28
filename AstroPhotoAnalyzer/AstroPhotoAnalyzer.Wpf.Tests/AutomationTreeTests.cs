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

// AutomationTreeTests.cs - the window's controls are in the UI Automation tree, the one a screen
// reader and every UI Automation client walk, and putting them there leaves the keyboard focus
// where a click puts it (R-157).
//
// WPF builds that tree from automation peers, and the peers are asked here directly, in process:
// a client in another process is answered by the same peers.

using AstroPhotoAnalyzer.Tests;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;

namespace AstroPhotoAnalyzer.Wpf.Tests;

[Collection("fereastra")]
public class AutomationTreeTests
{
    readonly StaFixture _sta;

    public AutomationTreeTests(StaFixture sta) => _sta = sta;

    static IEnumerable<AutomationPeer> Descendants(AutomationPeer peer)
    {
        foreach (AutomationPeer child in peer.GetChildren() ?? new List<AutomationPeer>())
        {
            yield return child;
            foreach (AutomationPeer below in Descendants(child)) yield return below;
        }
    }

    static IEnumerable<DependencyObject> VisualDescendants(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);
            yield return child;
            foreach (DependencyObject below in VisualDescendants(child)) yield return below;
        }
    }

    /// <summary>
    /// Every tab, selected in turn over a real result, has its view under its item in the tree,
    /// once, and under no other tab's item, and on Tonuri the simulation's Preview buttons are
    /// found there, as a client looks for them.
    /// </summary>
    [Fact]
    public async Task R157_EveryTabsContentIsInTheAutomationTree()
    {
        using var ws = new TempWorkspace();
        string tif = WindowSmokeTests.DarkTiff(ws);
        var vm = new MainViewModel();

        await _sta.InvokeAsync(async () =>
        {
            await vm.OpenAsync(tif);
            Assert.True(vm.HasResult, vm.Error);
            var window = new MainWindow { DataContext = vm };
            try
            {
                var content = (FrameworkElement)window.Content;
                var tabs = (TabControl)window.FindName("Tabs");
                AutomationPeer tabsPeer = UIElementAutomationPeer.CreatePeerForElement(tabs);

                Assert.NotEmpty(tabs.Items);
                var views = tabs.Items.Cast<TabItem>().Select(t => t.Content).ToList();
                bool sawTones = false;
                foreach (TabItem item in tabs.Items)
                {
                    tabs.SelectedItem = item;
                    content.Measure(new Size(1400, 860)); content.Arrange(new Rect(0, 0, 1400, 860)); content.UpdateLayout();
                    tabsPeer.ResetChildrenCache();
                    List<ItemAutomationPeer> itemPeers = tabsPeer.GetChildren().OfType<ItemAutomationPeer>().ToList();
                    foreach (ItemAutomationPeer peer in itemPeers) peer.ResetChildrenCache();
                    AutomationPeer itemPeer = itemPeers.Single(p => p.Item == item);

                    Assert.Single(itemPeer.GetChildren() ?? new List<AutomationPeer>(), p => p is UIElementAutomationPeer u && u.Owner == item.Content);
                    // A screen reader must not find the selected view under another tab's name.
                    foreach (ItemAutomationPeer other in itemPeers.Where(p => p.Item != item))
                        Assert.DoesNotContain(other.GetChildren() ?? new List<AutomationPeer>(), p => p is UIElementAutomationPeer u && views.Contains(u.Owner));
                    if (item.Content is TonesView)
                    {
                        sawTones = true;
                        Assert.Contains(Descendants(itemPeer), p => p.GetAutomationControlType() == AutomationControlType.Button && p.GetName() == "Preview");
                    }
                }
                Assert.True(sawTones, "no tab holds the Tonuri view");

                await vm.CurvesComputation;
            }
            finally
            {
                // Radio buttons with a GroupName and no PresentationSource, as in a window never
                // shown, form one group across the process: the Curves tab's Print box left
                // checked here would uncheck the Print box of a later test's CurvesView, a failure
                // of this test included. Without its view-model the window's boxes go back to
                // unchecked.
                window.DataContext = null;
            }
        });
    }

    /// <summary>
    /// A click on a tab's header leaves the keyboard focus on the header: nothing in the applied
    /// template carries the stock name of the content's presenter, through which WPF would move
    /// the focus into the tab, where the focused control would scroll a tab left scrolled down
    /// back up to itself.
    /// </summary>
    [Fact]
    public void R157_AClickOnATabsHeaderLeavesTheFocusOnIt()
    {
        _sta.Invoke(() =>
        {
            var window = new MainWindow();
            try
            {
                // Keyboard focus needs a window on screen, which no test shows. What moves it into
                // the tab is WPF finding the presenter by that name (TabItem's
                // OnPreviewGotKeyboardFocus, through TabControl.SelectedContentPresenter), so the
                // test makes the same lookup in the applied template and finds nothing.
                var tabs = (TabControl)window.FindName("Tabs");
                tabs.ApplyTemplate();
                Assert.Single(VisualDescendants(tabs).OfType<ContentPresenter>(), p => p.ContentSource == "SelectedContent");
                Assert.Null(tabs.Template.FindName("PART_SelectedContentHost", tabs));
            }
            finally
            {
                // The Curves tab's Print box, as in the test above.
                window.DataContext = null;
            }
        });
    }
}
