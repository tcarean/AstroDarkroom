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
// where a click puts it (R-114).
//
// WPF builds that tree from automation peers, and the peers are asked here directly, in process:
// a client in another process is answered by the same peers.

using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;

namespace FlatCheck.Wpf.Tests;

[Collection("fereastra")]
public class AutomationTreeTests
{
    readonly StaFixture _sta;

    public AutomationTreeTests(StaFixture sta) => _sta = sta;

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
    /// Every tab, selected in turn over an open session, has its view under its item in the
    /// tree, once, and under no other tab's item.
    /// </summary>
    [Fact]
    public async Task R114_EveryTabsContentIsInTheAutomationTree()
    {
        using var f = new SessionFixture()
            .WithFlatMaster()
            .WithLightSeries(6)
            .WithRawFlats("DSC_0101.NEF", "DSC_0102.NEF");
        var vm = new MainViewModel();

        await _sta.InvokeAsync(async () =>
        {
            await vm.OpenAsync(f.Root);
            var window = new MainWindow { DataContext = vm };
            try
            {
                var content = (FrameworkElement)window.Content;
                var tabs = (TabControl)window.FindName("Tabs");
                AutomationPeer tabsPeer = UIElementAutomationPeer.CreatePeerForElement(tabs);

                // Collapsed, the tabs would lay nothing out and the test would prove nothing. The
                // window's style shows them by a trigger, read here after a first layout pass.
                content.Measure(new Size(1360, 820)); content.Arrange(new Rect(0, 0, 1360, 820)); content.UpdateLayout();
                Assert.Equal(Visibility.Visible, tabs.Visibility);
                Assert.NotEmpty(tabs.Items);
                var views = tabs.Items.Cast<TabItem>().Select(t => t.Content).ToList();
                foreach (TabItem item in tabs.Items)
                {
                    tabs.SelectedItem = item;
                    content.Measure(new Size(1360, 820)); content.Arrange(new Rect(0, 0, 1360, 820)); content.UpdateLayout();
                    tabsPeer.ResetChildrenCache();
                    List<ItemAutomationPeer> itemPeers = tabsPeer.GetChildren().OfType<ItemAutomationPeer>().ToList();
                    foreach (ItemAutomationPeer peer in itemPeers) peer.ResetChildrenCache();
                    AutomationPeer itemPeer = itemPeers.Single(p => p.Item == item);

                    Assert.Single(itemPeer.GetChildren() ?? new List<AutomationPeer>(), p => p is UIElementAutomationPeer u && u.Owner == item.Content);
                    // A screen reader must not find the selected view under another tab's name.
                    foreach (ItemAutomationPeer other in itemPeers.Where(p => p.Item != item))
                        Assert.DoesNotContain(other.GetChildren() ?? new List<AutomationPeer>(), p => p is UIElementAutomationPeer u && views.Contains(u.Owner));
                }

                await vm.Synthetic.Pending;
            }
            finally
            {
                // Radio buttons with a GroupName and no PresentationSource, as in a window never
                // shown, form one group across the process: a box this window leaves checked
                // would uncheck the box of the same group in a later test's view. Without its
                // view-model the window's boxes go back to unchecked.
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
    public void R114_AClickOnATabsHeaderLeavesTheFocusOnIt()
        => _sta.Invoke(() =>
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
                // The radio buttons' one group, as in the test above.
                window.DataContext = null;
            }
        });
}
