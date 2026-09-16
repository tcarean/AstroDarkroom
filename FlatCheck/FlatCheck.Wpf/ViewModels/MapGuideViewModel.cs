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

// MapGuideViewModel.cs - ghidul de citire al hartii reziduale.
//
// CE E: documentatie afisata in fereastra, nu un diagnostic. Unealta NU clasifica forma
// hartii - o pata poate fi light leak sau un nor, si din pixeli nu se pot deosebi. Ce se
// poate face e sa i se spuna omului ce a insemnat fiecare forma pe mastere reale, si
// care e contra-testul care desparte cazurile. Deci nicio cifra nu se calculeaza aici si
// niciun prag nu se compara: textul vine intreg din SessionMessages.
//
// DE CE E O CLASA SI NU TREI PROPRIETATI PE FIECARE TAB: Structura si Ratio arata
// amandoua acelasi ghid, iar TabViewModel nu e locul - tabul de expunere nu are harta,
// deci n-are ce sa faca cu el.
//
// Proprietati de INSTANTA, desi textul e constant: {Binding} rezolva prin DataContext si
// nu vede proprietati statice - o legare la una ar fi randat gol, exact ca la
// documentatia tabului Ratio.

using System.Collections.Generic;
using System.Linq;

namespace FlatCheck.Wpf
{
    public sealed class MapGuideViewModel : ObservableObject
    {
        bool _expanded;

        /// <summary>Cum se citeste harta, inainte de lista de forme.</summary>
        public string Intro => SessionMessages.MapGuideIntro();

        /// <summary>Formele si ce au insemnat, in ordinea in care merita cautate.</summary>
        public IReadOnlyList<GuideRow> Shapes { get; } =
            SessionMessages.MapGuideShapes()
                .Select(s => new GuideRow { Shape = s.Shape, Meaning = s.Meaning })
                .ToList();

        /// <summary>
        /// Pliat implicit: sase randuri de text sub fiecare harta, la fiecare rulare, ar
        /// ineca tocmai cifrele. Starea sta aici si nu in control ca s-o poata deschide un
        /// test - o legare gresita intr-un panou pliat compileaza si cade abia la afisare,
        /// si asta e exact ce prinde WindowSmokeTests.
        /// </summary>
        public bool IsExpanded { get => _expanded; set => Set(ref _expanded, value); }
    }

    /// <summary>
    /// "Cum citesc tabul": ce decid cifrele si ce decide ochiul.
    ///
    /// Aceeasi forma ca ghidul hartii, si din acelasi motiv - documentatie afisata, cu un
    /// intro si doua randuri - dar alt subiect si alt loc pe ecran: ghidul sta sub harta
    /// si vorbeste despre forme, asta sta sub verdict si vorbeste despre despartirea
    /// dintre "cat de mult" (cifrele) si "ce fel" (harta). Despartirea nu se vede din
    /// asezarea ecranului, fiindca cifrele si harta arata ca doua vederi ale aceluiasi
    /// lucru, cand de fapt una decide si cealalta explica.
    /// </summary>
    public sealed class HowToReadViewModel : ObservableObject
    {
        bool _expanded;

        public string Intro => SessionMessages.HowToReadIntro();

        public IReadOnlyList<GuideRow> Rows { get; } =
            SessionMessages.HowToReadRows()
                .Select(s => new GuideRow { Shape = s.Shape, Meaning = s.Meaning })
                .ToList();

        /// <summary>Pliat implicit, ca ghidul hartii: verdictul trebuie sa ramana primul lucru vizibil.</summary>
        public bool IsExpanded { get => _expanded; set => Set(ref _expanded, value); }
    }

    /// <summary>
    /// "Cum citesc histogramele", pe tabul de expunere. A treia bucata de documentatie
    /// afisata in fereastra, cu aceeasi forma ca celelalte doua si din acelasi motiv:
    /// desenul arata ce e, dar nu si de ce doua curbe, sau de ce cea lata NU inseamna
    /// subexpunere - iar aia e chiar concluzia gresita pe care o trage cineva care se uita
    /// prima oara la histograma unui flat.
    /// </summary>
    public sealed class HistogramGuideViewModel : ObservableObject
    {
        bool _expanded;

        public string Intro => SessionMessages.HistogramGuideIntro();

        public IReadOnlyList<GuideRow> Rows { get; } =
            SessionMessages.HistogramGuideRows()
                .Select(s => new GuideRow { Shape = s.Shape, Meaning = s.Meaning })
                .ToList();

        /// <summary>Pliat implicit, ca celelalte doua ghiduri.</summary>
        public bool IsExpanded { get => _expanded; set => Set(ref _expanded, value); }
    }

    /// <summary>
    /// "Cum citesc poza egalizata", pe tabul de histograma. Al patrulea ghid, cu aceeasi
    /// forma. Motivul lui e cel mai apasat dintre toate: poza arata orice neuniformitate
    /// la contrast maxim si nu spune cat de mare e, iar cineva care o vede prima oara
    /// citeste zgomotul unui flat curat drept defect.
    /// </summary>
    public sealed class HistEqGuideViewModel : ObservableObject
    {
        bool _expanded;

        public string Intro => SessionMessages.HistEqGuideIntro();

        public IReadOnlyList<GuideRow> Rows { get; } =
            SessionMessages.HistEqGuideRows()
                .Select(s => new GuideRow { Shape = s.Shape, Meaning = s.Meaning })
                .ToList();

        /// <summary>Pliat implicit, ca celelalte trei.</summary>
        public bool IsExpanded { get => _expanded; set => Set(ref _expanded, value); }
    }

    /// <summary>
    /// Ghidul tabului de netezire. Aceeasi forma ca celelalte, dar DESCHIS implicit: e
    /// preconditia tabului, nu o nota de subsol - un buton care scrie un fisier langa
    /// master nu se apasa inainte de a sti ce face. Vezi SessionMessages.SmoothGuideIntro.
    /// </summary>
    public sealed class SmoothGuideViewModel : ObservableObject
    {
        bool _expanded = true;

        public string Intro => SessionMessages.SmoothGuideIntro();

        public IReadOnlyList<GuideRow> Rows { get; } =
            SessionMessages.SmoothGuideRows()
                .Select(s => new GuideRow { Shape = s.Shape, Meaning = s.Meaning })
                .ToList();

        public bool IsExpanded { get => _expanded; set => Set(ref _expanded, value); }
    }
}
