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

        /// <summary>Ce masoara randul, sub eticheta; null = fara descriere.</summary>
        public string Description { get; init; }
    }

    /// <summary>Un paragraf dintr-un panou explicativ: un inceput ingrosat, apoi textul.</summary>
    public sealed class GuideParagraph
    {
        public string Lead { get; init; }
        public string Text { get; init; }
    }

    public sealed class PreviewTile
    {
        public string Title { get; init; }
        public BitmapSource Image { get; init; }
        public string Note { get; init; }

        /// <summary>Ce verifici in dala asta, sub ea; se rupe pe latimea imaginii.</summary>
        public string Check { get; init; }
    }

    /// <summary>Un rand al tabelului simularii: textul mono al raportului si butonul lui de preview (R-113).</summary>
    public sealed class SimulationRowItem
    {
        public string Text { get; init; }

        /// <summary>"asa cum e", "0.17", "0.135 (dat)".</summary>
        public string Target { get; init; }
        public System.Windows.Input.ICommand Preview { get; init; }
    }

    /// <summary>Un nor pe harta: cercul lui in pixelii imaginii reduse, numele, si daca omul l-a lasat vizibil (R-130).</summary>
    public sealed class CloudLayer : ObservableObject
    {
        bool _visible = true;

        public string Name { get; init; }

        /// <summary>"LDN 42 (B78)  L* 0.9  tinta 0.392", pentru lista de bife si pentru eticheta.</summary>
        public string Caption { get; init; }

        /// <summary>Centrul si raza, in pixelii imaginii REDUSE aratate de fereastra.</summary>
        public double CentreX { get; init; }
        public double CentreY { get; init; }
        public double Radius { get; init; }
        public double Left => CentreX - Radius;
        public double Top => CentreY - Radius;
        public double Diameter => 2 * Radius;

        /// <summary>Cei mai intunecati primesc eticheta pe harta; restul doar cercul.</summary>
        public bool Labelled { get; init; }

        public bool IsVisible { get => _visible; set => Set(ref _visible, value); }
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
