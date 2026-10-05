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

// OverlayWindows.xaml.cs - legaturile cu XAML-ul ferestrelor de vizualizare: harta
// praf / cer (R-130), preview-ul unui rand (R-131) si curba sugerata (R-147). Comportamentul e in ViewModel-uri;
// aici doar bara de titlu intunecata si Esc.

using System.Windows;
using System.Windows.Input;

namespace AstroPhotoAnalyzer.Wpf
{
    public partial class MapWindow : Window
    {
        public MapWindow()
        {
            InitializeComponent();
            SourceInitialized += (_, _) => DarkTitleBar.Apply(this);
            KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
        }
    }

    public partial class CurveWindow : Window
    {
        public CurveWindow()
        {
            InitializeComponent();
            SourceInitialized += (_, _) => DarkTitleBar.Apply(this);
            KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
        }
    }

    public partial class PreviewWindow : Window
    {
        public PreviewWindow()
        {
            InitializeComponent();
            SourceInitialized += (_, _) => DarkTitleBar.Apply(this);
            KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
        }
    }
}
