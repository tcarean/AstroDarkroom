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

// MapGuideViewModel.cs - the reading guide of the residual map.
//
// WHAT IT IS: documentation shown in the window, not a diagnosis. The tool does NOT classify the
// map's shape - a spot can be a light leak or a cloud, and the pixels cannot tell them apart.
// What can be done is to tell the user what each shape meant on real masters, and which
// counter-test separates the cases. So no figure is computed here and no threshold compared:
// the text comes whole from SessionMessages.
//
// WHY IT IS A CLASS AND NOT THREE PROPERTIES ON THE TAB: the guide belongs to the residual map,
// not to a tab, and TabViewModel is not the place - the exposure tab has no map, so it has
// nothing to do with it.
//
// INSTANCE properties, though the text is constant: {Binding} resolves through the DataContext
// and does not see static properties - a binding to one would have rendered empty, exactly as
// the Ratio tab's documentation once did (ADR 0033).

using System.Collections.Generic;
using System.Linq;

namespace FlatCheck.Wpf
{
    /// <summary>The collapsible guide under the residual map, on the Structure
    /// tab: an intro and one row per shape, the text from
    /// <see cref="SessionMessages"/>.</summary>
    public sealed class MapGuideViewModel : ObservableObject
    {
        bool _expanded;

        /// <summary>How to read the map, before the list of shapes.</summary>
        public string Intro => SessionMessages.MapGuideIntro();

        /// <summary>The shapes and what they meant, in the order worth looking for
        /// them.</summary>
        public IReadOnlyList<GuideRow> Shapes { get; } =
            SessionMessages.MapGuideShapes()
                .Select(s => new GuideRow { Shape = s.Shape, Meaning = s.Meaning })
                .ToList();

        /// <summary>
        /// Collapsed by default: six rows of text under every map, on every run, would drown the
        /// very figures. The state lives here and not in the control so a test can open it - a
        /// wrong binding in a collapsed panel compiles and fails only when shown, and that is
        /// exactly what WindowSmokeTests catches.
        /// </summary>
        public bool IsExpanded { get => _expanded; set => Set(ref _expanded, value); }
    }

    /// <summary>
    /// "Cum citesc tabul": what the figures decide and what the eye decides.
    ///
    /// The same shape as the map guide, and for the same reason - documentation shown, with an
    /// intro and two rows - but another subject and another place on screen: the guide sits
    /// under the map and talks about shapes, this one sits under the verdict and talks about the
    /// division between "how much" (the figures) and "what kind" (the map). The division does not
    /// show in the screen's layout, because the figures and the map look like two views of the
    /// same thing, when in fact one decides and the other explains.
    /// </summary>
    public sealed class HowToReadViewModel : ObservableObject
    {
        bool _expanded;

        public string Intro => SessionMessages.HowToReadIntro();

        public IReadOnlyList<GuideRow> Rows { get; } =
            SessionMessages.HowToReadRows()
                .Select(s => new GuideRow { Shape = s.Shape, Meaning = s.Meaning })
                .ToList();

        /// <summary>Collapsed by default, like the map guide: the verdict must stay the first
        /// thing visible.</summary>
        public bool IsExpanded { get => _expanded; set => Set(ref _expanded, value); }
    }

    /// <summary>
    /// "Cum citesc histogramele", on the exposure tab. The third piece of documentation shown in
    /// the window, with the same shape as the other two and for the same reason: the drawing
    /// shows what is there, but not why two curves, or why the wide one does NOT mean
    /// underexposure - and that is exactly the wrong conclusion someone draws when looking at a
    /// flat's histogram for the first time.
    /// </summary>
    public sealed class HistogramGuideViewModel : ObservableObject
    {
        bool _expanded;

        public string Intro => SessionMessages.HistogramGuideIntro();

        public IReadOnlyList<GuideRow> Rows { get; } =
            SessionMessages.HistogramGuideRows()
                .Select(s => new GuideRow { Shape = s.Shape, Meaning = s.Meaning })
                .ToList();

        /// <summary>Collapsed by default, like the other two guides.</summary>
        public bool IsExpanded { get => _expanded; set => Set(ref _expanded, value); }
    }

    /// <summary>
    /// "Cum citesc poza egalizata", on the histogram tab. The fourth guide, with the same shape.
    /// Its reason is the most pressing of all: the picture shows any non-uniformity at maximum
    /// contrast and does not say how large it is, and someone seeing it for the first time reads
    /// a clean flat's noise as a defect.
    /// </summary>
    public sealed class HistEqGuideViewModel : ObservableObject
    {
        bool _expanded;

        public string Intro => SessionMessages.HistEqGuideIntro();

        public IReadOnlyList<GuideRow> Rows { get; } =
            SessionMessages.HistEqGuideRows()
                .Select(s => new GuideRow { Shape = s.Shape, Meaning = s.Meaning })
                .ToList();

        /// <summary>Collapsed by default, like the other three.</summary>
        public bool IsExpanded { get => _expanded; set => Set(ref _expanded, value); }
    }
}
