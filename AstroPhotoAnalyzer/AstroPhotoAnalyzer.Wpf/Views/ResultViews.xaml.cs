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

// ResultViews.xaml.cs - the links with the XAML of the seven tabs. The behaviour is in the
// view-models; here there is only the size of the Curbe tab's gallery, which only the view
// knows.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AstroPhotoAnalyzer.Wpf
{
    /// <summary>The Raport tab's view (R-112); the XAML holds everything.</summary>
    public partial class ReportView : UserControl
    {
        /// <summary>Loads the XAML.</summary>
        public ReportView() => InitializeComponent();
    }

    /// <summary>The Tonuri tab's view (R-113); the XAML holds everything.</summary>
    public partial class TonesView : UserControl
    {
        /// <summary>Loads the XAML.</summary>
        public TonesView() => InitializeComponent();
    }

    /// <summary>The Export tab's view (R-114); the XAML holds everything.</summary>
    public partial class ExportView : UserControl
    {
        /// <summary>Loads the XAML.</summary>
        public ExportView() => InitializeComponent();
    }

    /// <summary>The Rezolutie tab's view (R-115); the XAML holds everything.</summary>
    public partial class ResolutionView : UserControl
    {
        /// <summary>Loads the XAML.</summary>
        public ResolutionView() => InitializeComponent();
    }

    /// <summary>The Culoare tab's view (R-116); the XAML holds everything.</summary>
    public partial class BackgroundView : UserControl
    {
        /// <summary>Loads the XAML.</summary>
        public BackgroundView() => InitializeComponent();
    }

    /// <summary>The Preview tab's view (R-117); the XAML holds everything.</summary>
    public partial class PreviewView : UserControl
    {
        /// <summary>Loads the XAML.</summary>
        public PreviewView() => InitializeComponent();
    }

    /// <summary>
    /// The Curbe tab's view (R-146): the XAML, plus the gallery's size for the view-model.
    /// </summary>
    public partial class CurvesView : UserControl
    {
        /// <summary>Loads the XAML.</summary>
        public CurvesView() => InitializeComponent();

        /// <summary>
        /// The previews are asked for at the size of the gallery area (R-146), in pixels of the
        /// screen: "Calculeaza" takes it from the view-model, where this handler puts it at every
        /// layout pass.
        /// </summary>
        /// <param name="sender">The gallery area; not read.</param>
        /// <param name="e">
        /// The new size, in device-independent units, scaled here by the DPI.
        /// </param>
        void GallerySizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (DataContext is not CurvesTabViewModel vm) return;
            DpiScale dpi = VisualTreeHelper.GetDpi(this);
            vm.SetGallerySize((int)(e.NewSize.Width * dpi.DpiScaleX), (int)(e.NewSize.Height * dpi.DpiScaleY));
        }
    }
}
