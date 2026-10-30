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

// OverlayWindows.xaml.cs - the links with the XAML of the viewing windows: the dust / sky map
// (R-130), the preview of a row (R-131) and the suggested curve (R-147). The behaviour is in
// the view-models; here only the dark title bar and Esc.

using System.Windows;
using System.Windows.Input;

namespace AstroPhotoAnalyzer.Wpf
{
    /// <summary>The map window (R-130), over a <see cref="MapViewModel"/>; non-modal.</summary>
    public partial class MapWindow : Window
    {
        /// <summary>
        /// Loads the XAML, asks for the dark title bar once there is a handle; Esc closes.
        /// </summary>
        public MapWindow()
        {
            InitializeComponent();
            SourceInitialized += (_, _) => DarkTitleBar.Apply(this);
            KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
        }
    }

    /// <summary>
    /// The curve window (R-147), over a <see cref="CurveWindowViewModel"/>; non-modal.
    /// </summary>
    public partial class CurveWindow : Window
    {
        /// <summary>
        /// Loads the XAML, asks for the dark title bar once there is a handle; Esc closes.
        /// </summary>
        public CurveWindow()
        {
            InitializeComponent();
            SourceInitialized += (_, _) => DarkTitleBar.Apply(this);
            KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
        }
    }

    /// <summary>
    /// The soft proof's full-screen window (R-163, R-164), over the tab's own
    /// <see cref="SoftProofTabViewModel"/>; non-modal, borderless, over the whole screen. Its
    /// panel of controls shows while the mouse is over the band it occupies at the top
    /// (<see cref="PanelShown"/>). The keys G, O and I go to the view-model; Esc and the
    /// panel's Close button close.
    /// </summary>
    public partial class SoftProofWindow : Window
    {
        /// <summary>
        /// Whether the panel over the top of the image is shown (R-164): true while the mouse is
        /// over its band, set from the band's MouseEnter and MouseLeave; false at first.
        /// </summary>
        public static readonly DependencyProperty PanelShownProperty =
            DependencyProperty.Register(nameof(PanelShown), typeof(bool), typeof(SoftProofWindow), new PropertyMetadata(false));

        /// <summary>
        /// Loads the XAML; the band's MouseEnter and MouseLeave drive <see cref="PanelShown"/>,
        /// held while the margin list is open; the keys as above.
        /// </summary>
        public SoftProofWindow()
        {
            InitializeComponent();
            // The band is a transparent Border, so it is under the mouse even while the panel
            // in it is hidden; MouseLeave fires when the mouse leaves the band's subtree. The
            // margin list's drop-down is in that subtree, but it is taller than the band, so
            // the mouse can leave past it while the list is open: the panel then stays, and
            // hides when the list closes with the mouse already outside the band.
            PanelZone.MouseEnter += (_, _) => PanelShown = true;
            PanelZone.MouseLeave += (_, _) => { if (!Controls.MarginList.IsDropDownOpen) HidePanel(); };
            Controls.MarginList.DropDownClosed += (_, _) => { if (!PanelZone.IsMouseOver) HidePanel(); };
            PreviewKeyDown += (_, e) =>
            {
                if (e.Key == Key.Escape) { Close(); e.Handled = true; return; }
                if (Keyboard.FocusedElement is System.Windows.Controls.Primitives.TextBoxBase) return;
                if (Keyboard.Modifiers != ModifierKeys.None) return;
                if (DataContext is SoftProofTabViewModel vm && vm.HandleKey(e.Key)) e.Handled = true;
            };
        }

        /// <summary>Whether the panel over the top of the image is shown (R-164).</summary>
        public bool PanelShown
        {
            get => (bool)GetValue(PanelShownProperty);
            set => SetValue(PanelShownProperty, value);
        }

        /// <summary>
        /// Hides the panel and takes the keyboard focus back from it: a hidden control would
        /// otherwise keep it, and the arrow keys would change the intent or the margin unseen.
        /// </summary>
        void HidePanel()
        {
            PanelShown = false;
            if (HoverPanel.IsKeyboardFocusWithin) Focus();
        }

        /// <summary>The panel's Close button (R-164): closes the window, as Esc does.</summary>
        /// <param name="sender">The button; not read.</param>
        /// <param name="e">The click; not read.</param>
        void CloseClicked(object sender, RoutedEventArgs e) => Close();
    }

    /// <summary>
    /// The preview window (R-131), over a <see cref="PreviewWindowViewModel"/>; non-modal.
    /// </summary>
    public partial class PreviewWindow : Window
    {
        /// <summary>
        /// Loads the XAML, asks for the dark title bar once there is a handle; Esc closes.
        /// </summary>
        public PreviewWindow()
        {
            InitializeComponent();
            SourceInitialized += (_, _) => DarkTitleBar.Apply(this);
            KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
        }
    }
}
