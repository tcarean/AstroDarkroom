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
    /// The soft proof's full-screen window (R-163), over the tab's own
    /// <see cref="SoftProofTabViewModel"/>; non-modal. The keys P, G, M, O and I go to the
    /// view-model, Esc closes.
    /// </summary>
    public partial class SoftProofWindow : Window
    {
        /// <summary>
        /// Loads the XAML, asks for the dark title bar once there is a handle; the keys as above.
        /// </summary>
        public SoftProofWindow()
        {
            InitializeComponent();
            SourceInitialized += (_, _) => DarkTitleBar.Apply(this);
            PreviewKeyDown += (_, e) =>
            {
                if (e.Key == Key.Escape) { Close(); e.Handled = true; return; }
                if (Keyboard.FocusedElement is System.Windows.Controls.Primitives.TextBoxBase) return;
                if (Keyboard.Modifiers != ModifierKeys.None) return;
                if (DataContext is SoftProofTabViewModel vm && vm.HandleKey(e.Key)) e.Handled = true;
            };
        }
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
