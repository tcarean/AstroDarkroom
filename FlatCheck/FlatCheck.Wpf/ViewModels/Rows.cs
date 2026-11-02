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

// Rows.cs - the rows the interface draws.
//
// Plain public types, with exactly what is seen: a label, a figure already formatted and, where
// it applies, a verdict. The figure comes formatted from the view-model and not from the XAML, so
// it can be checked in a test - "51.6 %" is something you can assert, a binding's StringFormat
// is not.

using System.Windows.Media;

namespace FlatCheck.Wpf
{
    /// <summary>A metric row: label, value, and possibly a coloured verdict.</summary>
    public sealed class MetricRow
    {
        public string Label { get; init; }
        public string Value { get; init; }

        /// <summary>Null when the row is informative and NOT a test.</summary>
        public string Verdict { get; init; }

        /// <summary>The row that matters most (the residual) is drawn bold.</summary>
        public bool IsKey { get; init; }

        /// <summary>A short explanation under the row, or null.</summary>
        public string Note { get; init; }

        public bool HasVerdict => !string.IsNullOrEmpty(Verdict);
        public bool HasNote => !string.IsNullOrEmpty(Note);
    }

    /// <summary>A band of the Structure tab's one verdict scale (R-126): its interval for the
    /// five tests and, when the vignetting was compared, for the depth, each cell marked when
    /// its verdict fell in this band.</summary>
    public sealed class ScaleRow
    {
        public string Band { get; init; }

        /// <summary>The five tests' interval, in percent.</summary>
        public string Range { get; init; }

        /// <summary>The same band for the depth against the lens profile, in stops; empty when
        /// nothing was compared, and the cell is then off the table.</summary>
        public string DepthRange { get; init; }

        public string Meaning { get; init; }

        /// <summary>The five tests' verdict fell in this band: the percent cell is
        /// marked.</summary>
        public bool IsCurrent { get; init; }

        /// <summary>The depth's verdict fell in this band: the stops cell is marked.</summary>
        public bool IsDepthCurrent { get; init; }

        public bool HasDepth => !string.IsNullOrEmpty(DepthRange);
    }

    /// <summary>
    /// A row of the Sintetic tab's comparison table: the same quantity on the photographed master
    /// (A), on the synthetic flat (B) and on their ratio, with the verdict where it is a test.
    /// Copied from the core's AgainstRow, which is internal.
    /// </summary>
    public sealed class CompareRow
    {
        public string Label { get; init; }
        public string A { get; init; }
        public string AVerdict { get; init; }
        public string B { get; init; }
        public string BVerdict { get; init; }
        public string Diff { get; init; }
        public string DiffVerdict { get; init; }
        public string Note { get; init; }
        public bool IsKey { get; init; }
        public bool HasAVerdict => !string.IsNullOrEmpty(AVerdict);
        public bool HasBVerdict => !string.IsNullOrEmpty(BVerdict);
        public bool HasDiffVerdict => !string.IsNullOrEmpty(DiffVerdict);
        public bool HasNote => !string.IsNullOrEmpty(Note);
    }

    /// <summary>
    /// A row of the "vignetting against the lens profile" table on the Structure tab: the public
    /// copy of the core's ProfileRow (internal), with the texts already formatted there.
    /// </summary>
    public sealed class LensProfileRow
    {
        public string Source { get; init; }       // "Adobe" / "lensfun"
        public string Lens { get; init; }
        /// <summary>The mount, plus the calibration format for lensfun
        /// (LensChoice.Where).</summary>
        public string Where { get; init; }
        public string Profile { get; init; }      // "20 mm, f/2.8, infinit"
        public string Depth { get; init; }        // "0.66 stopuri (colt = 63 % din centru)"
        public string Diff { get; init; }         // "+0.59 stopuri"
        public string Error { get; init; }        // "50.8 %"
        public string Verdict { get; init; }
        public bool IsNearest { get; init; }
        public string Note { get; init; }
        public bool IsWarning { get; init; }
        public string DiffAndError => $"{Diff} = {Error}";
        public bool HasNote => !string.IsNullOrEmpty(Note);
    }

    /// <summary>
    /// An entry of the Structure tab's lens picker (R-121): a candidate lens by its catalog key,
    /// or, with a null key, the first entry meaning no choice - the nearest profile decides.
    /// </summary>
    public sealed class LensOption
    {
        /// <summary>The lens's catalog key (LensChoice.Key), the value remembered in the
        /// session's settings; null for the no-choice entry.</summary>
        public string Key { get; init; }
        /// <summary>What the picker shows: "lensfun Samyang 135mm f/2.0 ED UMC [Nikon F AI, ...]",
        /// or the no-choice words.</summary>
        public string Label { get; init; }

        /// <summary>The label, so the picker's closed box shows it wherever a template does not
        /// reach (the theme's selection box).</summary>
        /// <returns>The label.</returns>
        public override string ToString() => Label;
    }

    /// <summary>A shape from the map guide: its name and what it meant on real masters. The
    /// other guides use it for their rows too.</summary>
    public sealed class GuideRow
    {
        public string Shape { get; init; }
        public string Meaning { get; init; }
    }

    /// <summary>
    /// A channel on the histogram card. Everything here is already formatted or already
    /// normalised: the drawing computes nothing more, and a test can assert "44.4 %".
    ///
    /// The thresholds are COPIED onto every row, though they are the same on all. That way the
    /// template binds locally, with {Binding BandLo}, instead of a RelativeSource to the right
    /// ancestor - a binding that compiles, looks fine, and breaks silently when the template is
    /// reused elsewhere in the tree.
    /// </summary>
    public sealed class ChannelHistogramRow
    {
        /// <summary>"R", "G1", ... or "CFA[0,1]" when the file did not say the order.</summary>
        public string Label { get; init; }

        /// <summary>The position in the mosaic, when the label is a colour; empty
        /// otherwise.</summary>
        public string Position { get; init; }

        public Brush Colour { get; init; }

        /// <summary>The median of the whole channel and of the central window, already
        /// formatted.</summary>
        public string Median { get; init; }
        public string Centre { get; init; }

        public string Note { get; init; }
        public bool IsBrightest { get; init; }

        /// <summary>The two curves, 0..1, each normalised to its own peak.</summary>
        public double[] Whole { get; init; }
        public double[] CentreCurve { get; init; }

        /// <summary>The centre's median, in percent - the vertical line on the drawing.</summary>
        public double Marker { get; init; }

        public double BandLo { get; init; }
        public double BandHi { get; init; }
        public double TooLow { get; init; }
        public double TooHigh { get; init; }
        public string BandLabel { get; init; }

        public bool HasNote => !string.IsNullOrEmpty(Note);
        public bool HasPosition => !string.IsNullOrEmpty(Position);
    }

    /// <summary>
    /// A row of the raw / screen comparison table: the same channel, the two figures, and the
    /// difference. All three are medians of the central window, already formatted.
    /// </summary>
    public sealed class ScreenPairRow
    {
        public string Channel { get; init; }   // "R", "G (media G1, G2)", or the fallback label
        public Brush Colour { get; init; }
        public string Raw { get; init; }       // "29.0 %"
        public string Screen { get; init; }    // "81.2 %"
        public string Gap { get; init; }       // "+52 puncte"
    }

    /// <summary>
    /// A row of the histogram tab's list: the master from masters/ or process/, or a frame from
    /// flats/. Formatted here, not through a converter, so a test can assert "MASTER" and
    /// "process/" directly.
    /// </summary>
    public sealed class HistEqFileRow
    {
        public string Path { get; init; }

        /// <summary>The file's name only.</summary>
        public string Name { get; init; }

        /// <summary>The real folder: "process/" or "masters/" for the master, "flats/" for a
        /// frame.</summary>
        public string Where { get; init; }

        /// <summary>The row's kind. Internal - an internal type cannot leave through a public
        /// property.</summary>
        internal HistEqKind Kind { get; init; }

        public bool IsMaster => Kind == HistEqKind.Master;

        /// <summary>The chip in the list: "MASTER" on the master, empty on a frame.</summary>
        public string Tag => Kind == HistEqKind.Master ? "MASTER" : "";

        public bool HasTag => Tag.Length > 0;
    }

    /// <summary>A frame of the end-to-end series table.</summary>
    public sealed class SeriesRow
    {
        public string Name { get; init; }
        public string Radial { get; init; }
        public string Sky { get; init; }
        public string Tiles { get; init; }
        public string Verdict { get; init; }
    }
    /// <summary>
    /// A third-party component in the "Despre" window. A public copy of the core's
    /// ThirdPartyComponent, which is internal: a public view-model cannot expose an internal type
    /// to the XAML, and the compiler is the one that keeps that border.
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
