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
// reader and every UI Automation client walk (R-157).
//
// WPF builds that tree from automation peers, and the peers are asked here directly, in process:
// a client in another process is answered by the same peers.

using AstroPhotoAnalyzer.Tests;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

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

    /// <summary>
    /// Every tab, selected in turn over a real result, has its view under its item in the tree,
    /// and on Tonuri the simulation's Preview buttons are found there, as a client looks for them.
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
            var content = (FrameworkElement)window.Content;
            var tabs = (TabControl)window.FindName("Tabs");
            AutomationPeer tabsPeer = UIElementAutomationPeer.CreatePeerForElement(tabs);

            Assert.NotEmpty(tabs.Items);
            foreach (TabItem item in tabs.Items)
            {
                tabs.SelectedItem = item;
                content.Measure(new Size(1400, 860)); content.Arrange(new Rect(0, 0, 1400, 860)); content.UpdateLayout();
                tabsPeer.ResetChildrenCache();
                AutomationPeer itemPeer = tabsPeer.GetChildren().OfType<ItemAutomationPeer>().Single(p => p.Item == item);
                itemPeer.ResetChildrenCache();

                Assert.Contains(itemPeer.GetChildren() ?? new List<AutomationPeer>(), p => p is UIElementAutomationPeer u && u.Owner == item.Content);
                if (item.Content is TonesView)
                    Assert.Contains(Descendants(itemPeer), p => p.GetAutomationControlType() == AutomationControlType.Button && p.GetName() == "Preview");
            }

            await vm.CurvesComputation;

            // Radio buttons with a GroupName and no PresentationSource, as in a window never
            // shown, form one group across the process: the Curves tab's Print box left checked
            // here would uncheck the Print box of a later test's CurvesView. Without its
            // view-model the window's boxes go back to unchecked.
            window.DataContext = null;
        });
    }
}
