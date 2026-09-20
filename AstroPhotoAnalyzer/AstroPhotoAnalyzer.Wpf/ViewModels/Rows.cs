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

// Rows.cs - randurile pe care le leaga XAML-ul. Campuri simple, deja formatate: aici
// nu se calculeaza si nu se decide nimic, se aseaza. Publice, fiindca legarea WPF
// citeste prin reflexie proprietati publice.

using System.Collections.Generic;
using System.Windows.Media.Imaging;

namespace AstroPhotoAnalyzer.Wpf
{
    public sealed class RecommendationRow
    {
        public string Mark { get; init; }
        public string Text { get; init; }
        public string Because { get; init; }
        public string VerdictLabel { get; init; }
    }

    public sealed class SectionRow
    {
        public int Number { get; init; }
        public string Title { get; init; }
        public string VerdictLabel { get; init; }
        public IReadOnlyList<RecommendationRow> Recommendations { get; init; }
    }

    public sealed class MetricRow
    {
        public string Label { get; init; }
        public string Value { get; init; }
        public string Note { get; init; }
    }

    public sealed class PreviewTile
    {
        public string Title { get; init; }
        public BitmapSource Image { get; init; }
        public string Note { get; init; }
    }

    public sealed class ComponentRow
    {
        public string Name { get; init; }
        public string Role { get; init; }
        public string Licence { get; init; }
        public string Copyright { get; init; }
        public string Url { get; init; }
    }
}
