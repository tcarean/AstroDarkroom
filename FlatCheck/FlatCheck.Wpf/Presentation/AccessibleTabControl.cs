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

// AccessibleTabControl.cs - the window's tab control, whose UI Automation tree holds the
// selected tab's content (R-114, ADR 0074).
//
// A stock TabControl's automation peer puts that content under the tab's item only through the
// presenter its template names PART_SelectedContentHost. Through the same name WPF moves the
// keyboard focus into a tab on a click on its header, and the control that takes the focus
// brings itself into view, scrolling a tab left scrolled down back up. The theme's template
// names the presenter otherwise, and the peers below add the content from it as the stock peer
// would.

using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace FlatCheck.Wpf
{
    /// <summary>
    /// A <see cref="TabControl"/> whose automation peer puts the selected tab's content under that
    /// tab's item, as a stock one's does when its template names the content's presenter
    /// <c>PART_SelectedContentHost</c>, while a click on a tab's header leaves the keyboard focus on
    /// the header. Both rest on the template: the theme's names the presenter
    /// <see cref="ContentHostName"/>; without the theme's style the control gets the system's
    /// <see cref="TabControl"/> template, which names it <c>PART_SelectedContentHost</c>, and WPF
    /// then adds the content itself and moves the focus into the tab.
    /// </summary>
    [TemplatePart(Name = ContentHostName, Type = typeof(ContentPresenter))]
    public sealed class AccessibleTabControl : TabControl
    {
        /// <summary>The name the template gives the presenter of the selected tab's content.</summary>
        public const string ContentHostName = "ContentHost";

        /// <summary>The template's presenter of the selected tab's content; null before the template is applied.</summary>
        ContentPresenter ContentHost => GetTemplateChild(ContentHostName) as ContentPresenter;

        /// <summary>Makes the peer that puts the selected tab's content under that tab's item.</summary>
        /// <returns>The control's automation peer.</returns>
        protected override AutomationPeer OnCreateAutomationPeer() => new Peer(this);

        /// <summary>The control's peer: the stock one, making the tabs' peers below.</summary>
        sealed class Peer : TabControlAutomationPeer
        {
            /// <summary>Makes the peer of the control.</summary>
            /// <param name="owner">The control.</param>
            public Peer(AccessibleTabControl owner) : base(owner) { }

            /// <summary>Makes the peer of one tab.</summary>
            /// <param name="item">The tab's item; in the window, the <see cref="TabItem"/> itself.</param>
            /// <returns>The tab's peer.</returns>
            protected override ItemAutomationPeer CreateItemAutomationPeer(object item) => new TabPeer(item, this);
        }

        /// <summary>
        /// A tab's peer: the stock one, whose children are the header's, followed on the selected
        /// tab by the children of the template's content presenter, which the stock peer adds only
        /// through <c>PART_SelectedContentHost</c>.
        /// </summary>
        sealed class TabPeer : TabItemAutomationPeer
        {
            /// <summary>Makes the peer of one tab.</summary>
            /// <param name="item">The tab's item.</param>
            /// <param name="parent">The control's peer, the tab's parent in the tree.</param>
            public TabPeer(object item, Peer parent) : base(item, parent) { }

            /// <summary>The header's children, then, on the selected tab, the content's.</summary>
            /// <returns>The children in that order; null when there are none.</returns>
            protected override List<AutomationPeer> GetChildrenCore()
            {
                List<AutomationPeer> header = base.GetChildrenCore();
                var tabs = (AccessibleTabControl)ItemsControlAutomationPeer.Owner;
                if (!Equals(Item, tabs.SelectedItem) || tabs.ContentHost is not ContentPresenter host) return header;
                // A peer of the presenter's own, made here as the stock peer makes it: its children
                // are the content's peers. The base's list belongs to the header's peer, so the two
                // go into a new one.
                List<AutomationPeer> content = new FrameworkElementAutomationPeer(host).GetChildren();
                if (content == null) return header;
                var children = new List<AutomationPeer>(header ?? new List<AutomationPeer>());
                children.AddRange(content);
                return children;
            }
        }
    }
}
