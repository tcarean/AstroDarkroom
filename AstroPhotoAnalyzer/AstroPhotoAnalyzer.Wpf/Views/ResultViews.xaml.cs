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

// ResultViews.xaml.cs - legaturile cu XAML-ul celor sapte taburi. Comportamentul e in
// ViewModel-uri; aici e doar marimea galeriei tabului Curbe, pe care numai vederea o stie.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AstroPhotoAnalyzer.Wpf
{
    public partial class ReportView : UserControl { public ReportView() => InitializeComponent(); }
    public partial class TonesView : UserControl { public TonesView() => InitializeComponent(); }
    public partial class ExportView : UserControl { public ExportView() => InitializeComponent(); }
    public partial class ResolutionView : UserControl { public ResolutionView() => InitializeComponent(); }
    public partial class BackgroundView : UserControl { public BackgroundView() => InitializeComponent(); }
    public partial class PreviewView : UserControl { public PreviewView() => InitializeComponent(); }

    public partial class CurvesView : UserControl
    {
        public CurvesView() => InitializeComponent();

        /// <summary>
        /// Previzualizarile se cer la marimea zonei galeriei (R-146), in pixeli ai ecranului:
        /// "Calculeaza" o ia de la view-model, unde o pune randul acesta la fiecare reasezare.
        /// </summary>
        void GallerySizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (DataContext is not CurvesTabViewModel vm) return;
            DpiScale dpi = VisualTreeHelper.GetDpi(this);
            vm.SetGallerySize((int)(e.NewSize.Width * dpi.DpiScaleX), (int)(e.NewSize.Height * dpi.DpiScaleY));
        }
    }
}
