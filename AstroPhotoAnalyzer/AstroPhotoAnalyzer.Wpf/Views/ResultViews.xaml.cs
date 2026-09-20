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

// ResultViews.xaml.cs - legaturile cu XAML-ul celor sase taburi. Comportamentul e in
// ViewModel-uri; aici nu e nimic.

using System.Windows.Controls;

namespace AstroPhotoAnalyzer.Wpf
{
    public partial class ReportView : UserControl { public ReportView() => InitializeComponent(); }
    public partial class TonesView : UserControl { public TonesView() => InitializeComponent(); }
    public partial class ExportView : UserControl { public ExportView() => InitializeComponent(); }
    public partial class ResolutionView : UserControl { public ResolutionView() => InitializeComponent(); }
    public partial class BackgroundView : UserControl { public BackgroundView() => InitializeComponent(); }
    public partial class PreviewView : UserControl { public PreviewView() => InitializeComponent(); }
}
