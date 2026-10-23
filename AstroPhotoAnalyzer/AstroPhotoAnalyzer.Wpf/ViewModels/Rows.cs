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

// Rows.cs - the rows the XAML binds to. Simple fields, already formatted: nothing is computed
// and nothing is decided here, only laid out. Public, because WPF's binding reads public
// properties through reflection.

using System.Collections.Generic;
using System.Windows.Media.Imaging;

namespace AstroPhotoAnalyzer.Wpf
{
    /// <summary>
    /// One recommendation of the Raport tab (R-112): its mark ("-" for OK, "!" for attention,
    /// "!!" for a problem), its text, its "fiindca" and the label of its verdict.
    /// </summary>
    public sealed class RecommendationRow
    {
        public string Mark { get; init; }
        public string Text { get; init; }
        public string Because { get; init; }
        public string VerdictLabel { get; init; }
    }

    /// <summary>
    /// One topic of the Raport tab (R-112): its number in the decision order, its title, the
    /// label of its worst verdict and its recommendations, possibly none.
    /// </summary>
    public sealed class SectionRow
    {
        public int Number { get; init; }
        public string Title { get; init; }
        public string VerdictLabel { get; init; }
        public IReadOnlyList<RecommendationRow> Recommendations { get; init; }
    }

    /// <summary>
    /// One metric of a tab: a label, its formatted value, an optional note beside the value and
    /// an optional description under the label.
    /// </summary>
    public sealed class MetricRow
    {
        public string Label { get; init; }
        public string Value { get; init; }
        public string Note { get; init; }

        /// <summary>What the row measures, under the label; null = no description.</summary>
        public string Description { get; init; }
    }

    /// <summary>A paragraph of an explanatory panel: a bold lead, then the text.</summary>
    public sealed class GuideParagraph
    {
        public string Lead { get; init; }
        public string Text { get; init; }
    }

    /// <summary>
    /// One tile of the Preview tab (R-117): its title, the bitmap, an optional note under it and
    /// what to check in it.
    /// </summary>
    public sealed class PreviewTile
    {
        public string Title { get; init; }
        public BitmapSource Image { get; init; }
        public string Note { get; init; }

        /// <summary>What to verify in this tile, under it; wrapped to the image's width.</summary>
        public string Check { get; init; }
    }

    /// <summary>
    /// A row of the simulation table: the report's mono text and its preview button (R-113).
    /// </summary>
    public sealed class SimulationRowItem
    {
        public string Text { get; init; }

        /// <summary>"asa cum e", "0.17", "0.135 (dat)".</summary>
        public string Target { get; init; }
        public System.Windows.Input.ICommand Preview { get; init; }
    }

    /// <summary>
    /// A cloud on the map: its circle in pixels of the reduced image, its name, and whether the
    /// user left it visible (R-130).
    /// </summary>
    public sealed class CloudLayer : ObservableObject
    {
        bool _visible = true;

        public string Name { get; init; }

        /// <summary>
        /// "LDN 42 (B78)  L* 0.9  tinta 0.392", for the tick list and for the label.
        /// </summary>
        public string Caption { get; init; }

        /// <summary>
        /// The centre and the radius, in pixels of the REDUCED image the window shows.
        /// </summary>
        public double CentreX { get; init; }
        public double CentreY { get; init; }
        public double Radius { get; init; }
        public double Left => CentreX - Radius;
        public double Top => CentreY - Radius;
        public double Diameter => 2 * Radius;

        /// <summary>
        /// The darkest clouds get their label on the map; the rest only the circle.
        /// </summary>
        public bool Labelled { get; init; }

        public bool IsVisible { get => _visible; set => Set(ref _visible, value); }
    }

    /// <summary>
    /// One third-party component of the "Despre" window (R-119): its name, its role in the tool,
    /// its licence, its copyright line and its URL, from ThirdParty.Components.
    /// </summary>
    public sealed class ComponentRow
    {
        public string Name { get; init; }
        public string Role { get; init; }
        public string Licence { get; init; }
        public string Copyright { get; init; }
        public string Url { get; init; }
    }
}
