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

using System.Windows.Media;

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

    /// <summary>
    /// Un canal pe cardul de histograme. Tot ce e aici e deja formatat sau deja
    /// normalizat: desenul nu mai calculeaza nimic, iar un test poate asserta "44.4 %".
    ///
    /// Pragurile sunt COPIATE pe fiecare rand, desi sunt aceleasi pe toate. Asa sablonul
    /// se leaga local, cu {Binding BandLo}, in loc de un RelativeSource catre stramosul
    /// potrivit - o legare care compileaza, arata bine, si se rupe tacut cand sablonul e
    /// refolosit in alt loc din arbore.
    /// </summary>
    public sealed class ChannelHistogramRow
    {
        /// <summary>"R", "G1", ... sau "CFA[0,1]" cand fisierul n-a spus ordinea.</summary>
        public string Label { get; init; }

        /// <summary>Pozitia in mozaic, cand eticheta e o culoare; altfel gol.</summary>
        public string Position { get; init; }

        public Brush Colour { get; init; }

        /// <summary>Mediana canalului intreg si a ferestrei centrale, deja formatate.</summary>
        public string Median { get; init; }
        public string Centre { get; init; }

        public string Note { get; init; }
        public bool IsBrightest { get; init; }

        /// <summary>Cele doua curbe, 0..1, fiecare normalizata la propriul varf.</summary>
        public double[] Whole { get; init; }
        public double[] CentreCurve { get; init; }

        /// <summary>Mediana centrului, in procente - linia verticala de pe desen.</summary>
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
    /// Un rand din tabelul de comparatie raw / ecran: acelasi canal, cele doua cifre, si
    /// diferenta. Toate trei sunt mediane ale ferestrei centrale, deja formatate.
    /// </summary>
    public sealed class ScreenPairRow
    {
        public string Channel { get; init; }   // "R", "G (media G1, G2)", sau eticheta de rezerva
        public Brush Colour { get; init; }
        public string Raw { get; init; }       // "29.0 %"
        public string Screen { get; init; }    // "81.2 %"
        public string Gap { get; init; }       // "+52 puncte"
    }

    /// <summary>
    /// Un rand din lista tabului de histograma: masterul din process/ sau un cadru din
    /// flats/. Formatat aici, nu printr-un convertor, ca un test sa poata asserta "MASTER"
    /// si "process/" direct.
    /// </summary>
    public sealed class HistEqFileRow
    {
        public string Path { get; init; }

        /// <summary>Numai numele fisierului.</summary>
        public string Name { get; init; }

        /// <summary>"process/" pentru master, "flats/" pentru un cadru.</summary>
        public string Where { get; init; }

        public bool IsMaster { get; init; }

        /// <summary>Chipul din lista: "MASTER" pe master, gol pe un cadru.</summary>
        public string Tag => IsMaster ? "MASTER" : "";
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
    /// <summary>
    /// O componenta terta din fereastra "Despre". Copie publica a lui ThirdPartyComponent
    /// din nucleu, care e internal: un ViewModel public nu poate expune un tip internal
    /// catre XAML, si compilatorul e cel care tine granita asta.
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
