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

// Rows.cs - randurile pe care le deseneaza interfata.
//
// Tipuri simple si publice, cu exact ce se vede: o eticheta, o cifra deja formatata si,
// unde e cazul, un verdict. Cifra vine formatata din ViewModel si nu din XAML, ca sa fie
// verificabila intr-un test - "51.6 %" e un lucru pe care il poti asserta, un
// StringFormat dintr-un binding nu.

namespace FlatCheck.Wpf
{
    /// <summary>Un rand de metrica: eticheta, valoare, si eventual un verdict colorat.</summary>
    public sealed class MetricRow
    {
        public string Label { get; init; }
        public string Value { get; init; }

        /// <summary>Null cand randul e informativ si NU e un test.</summary>
        public string Verdict { get; init; }

        /// <summary>Se ingroasa randul care conteaza cel mai mult (reziduul).</summary>
        public bool IsKey { get; init; }

        /// <summary>Explicatie scurta sub rand, sau null.</summary>
        public string Note { get; init; }

        public bool HasVerdict => !string.IsNullOrEmpty(Verdict);
        public bool HasNote => !string.IsNullOrEmpty(Note);
    }

    /// <summary>O banda din scara de reziduu, cu marcaj daca e cea in care a cazut verdictul.</summary>
    public sealed class ScaleRow
    {
        public string Band { get; init; }
        public string Range { get; init; }
        public string Meaning { get; init; }
        public bool IsCurrent { get; init; }
    }

    /// <summary>O forma din ghidul hartii: numele ei si ce a insemnat pe mastere reale.</summary>
    public sealed class GuideRow
    {
        public string Shape { get; init; }
        public string Meaning { get; init; }
    }

    /// <summary>Un cadru din tabelul seriei end-to-end.</summary>
    public sealed class SeriesRow
    {
        public string Name { get; init; }
        public string Radial { get; init; }
        public string Sky { get; init; }
        public string Tiles { get; init; }
        public string Verdict { get; init; }
    }
}
