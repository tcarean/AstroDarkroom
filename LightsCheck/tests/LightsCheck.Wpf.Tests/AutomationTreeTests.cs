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

using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;
using LightsCheck.Tests;

namespace LightsCheck.Wpf.Tests;

/// <summary>
/// The window's controls are in the UI Automation tree, the one a screen reader and every UI
/// Automation client walk, and putting them there leaves the keyboard focus where a click puts it
/// (R-067). WPF builds that tree from automation peers, and the peers are asked here directly, in
/// process: a client in another process is answered by the same peers.
/// </summary>
[Collection("window")]
public class AutomationTreeTests
{
    private readonly StaFixture _sta;

    public AutomationTreeTests(StaFixture sta) => _sta = sta;

    private static IEnumerable<AutomationPeer> Descendants(AutomationPeer peer)
    {
        foreach (AutomationPeer child in peer.GetChildren() ?? [])
        {
            yield return child;
            foreach (AutomationPeer below in Descendants(child))
            {
                yield return below;
            }
        }
    }

    private static IEnumerable<DependencyObject> VisualDescendants(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);
            yield return child;
            foreach (DependencyObject below in VisualDescendants(child))
            {
                yield return below;
            }
        }
    }

    private static IEnumerable<object> LogicalDescendants(object parent)
    {
        if (parent is not DependencyObject d)
        {
            yield break;
        }

        foreach (object child in LogicalTreeHelper.GetChildren(d))
        {
            yield return child;
            foreach (object below in LogicalDescendants(child))
            {
                yield return below;
            }
        }
    }

    // The window's one tab control, found by its type: the tests name no element of the window.
    private static TabControl Tabs(MainWindow window) => Assert.Single(LogicalDescendants(window).OfType<TabControl>());

    // A peer of the content itself, or of an element inside it when the content has no peer of
    // its own (the Raport tab's Border lends its place to its text box).
    private static bool IsFrom(AutomationPeer peer, Visual content)
        => peer is UIElementAutomationPeer u && (u.Owner == content || u.Owner.IsDescendantOf(content));

    /// <summary>
    /// Every tab, selected in turn over a real result, has its content under its item in the
    /// tree, once, and under no other tab's item, and on Raport the report's text box is found
    /// there, as a client looks for it.
    /// </summary>
    [Fact]
    public async Task R067_EveryTabsContentIsInTheAutomationTree()
    {
        using SessionFixture f = new SessionFixture().WithStarSeries(SessionFixture.Pattern, biases: 2);
        MainViewModel vm = new();

        await _sta.InvokeAsync(async () =>
        {
            await vm.OpenAsync(f.Root);
            Assert.True(vm.HasResult);
            MainWindow window = new(vm);
            FrameworkElement content = (FrameworkElement)window.Content;
            TabControl tabs = Tabs(window);
            AutomationPeer tabsPeer = UIElementAutomationPeer.CreatePeerForElement(tabs);

            Assert.NotEmpty(tabs.Items);
            List<Visual> contents = tabs.Items.Cast<TabItem>().Select(t => (Visual)t.Content).ToList();
            bool sawReport = false;
            foreach (TabItem item in tabs.Items)
            {
                tabs.SelectedItem = item;
                content.Measure(new Size(1360, 820));
                content.Arrange(new Rect(0, 0, 1360, 820));
                content.UpdateLayout();
                tabsPeer.ResetChildrenCache();
                List<ItemAutomationPeer> itemPeers = tabsPeer.GetChildren().OfType<ItemAutomationPeer>().ToList();
                foreach (ItemAutomationPeer peer in itemPeers)
                {
                    peer.ResetChildrenCache();
                }

                ItemAutomationPeer itemPeer = itemPeers.Single(p => p.Item == item);
                List<AutomationPeer> fromContent = (itemPeer.GetChildren() ?? []).Where(p => IsFrom(p, (Visual)item.Content)).ToList();
                Assert.NotEmpty(fromContent);
                Assert.Equal(fromContent.Count, fromContent.Distinct().Count());

                // A screen reader must not find the selected content under another tab's name.
                foreach (ItemAutomationPeer other in itemPeers.Where(p => p.Item != item))
                {
                    Assert.DoesNotContain(other.GetChildren() ?? [], p => contents.Any(c => IsFrom(p, c)));
                }

                if (VisualDescendants((DependencyObject)item.Content).OfType<TextBox>().Any())
                {
                    sawReport = true;
                    Assert.Contains(Descendants(itemPeer), p => p.GetAutomationControlType() == AutomationControlType.Edit);
                }
            }

            Assert.True(sawReport, "no tab holds the report's text box");
        });
    }

    /// <summary>
    /// A click on a tab's header leaves the keyboard focus on the header: nothing in the applied
    /// template carries the stock name of the content's presenter, through which WPF would move
    /// the focus into the tab, where the focused control would scroll a tab left scrolled down
    /// back up to itself.
    /// </summary>
    [Fact]
    public void R067_AClickOnATabsHeaderLeavesTheFocusOnIt()
        => _sta.Invoke(() =>
        {
            // Keyboard focus needs a window on screen, which no test shows. What moves it into the
            // tab is WPF finding the presenter by that name (TabItem's OnPreviewGotKeyboardFocus,
            // through TabControl.SelectedContentPresenter), so the test makes the same lookup in
            // the applied template and finds nothing.
            TabControl tabs = Tabs(new MainWindow());
            tabs.ApplyTemplate();
            Assert.Single(VisualDescendants(tabs).OfType<ContentPresenter>(), p => p.ContentSource == "SelectedContent");
            Assert.Null(tabs.Template.FindName("PART_SelectedContentHost", tabs));
        });
}
