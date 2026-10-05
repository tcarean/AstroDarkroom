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

// IccPickerViewModel.cs - fereastra "Din profil ICC..." (R-132): lista profilelor de
// imprimanta instalate, cu Dmax-ul fiecaruia, si profilul ales. Nu citeste niciun profil
// si nu calculeaza nimic: cifrele si propozitiile despre un profil sunt ale nucleului
// (IccPaperBlack); aici e doar lista, selectia si textul care explica ce se citeste.

using System.Collections.ObjectModel;
using System.Linq;

namespace AstroPhotoAnalyzer.Wpf
{
    /// <summary>Un profil din lista: numele lui, fisierul, si ce a citit nucleul din el.</summary>
    public sealed class IccProfileRow
    {
        internal IccProfileRow(IccBlack b)
        {
            Name = b.Description;
            File = b.Path;
            Readable = b.Readable;
            Detail = IccPaperBlack.Describe(b);
            Note = b.Note;
            DmaxText = b.Readable ? Fmt.Dmax(b.Dmax) : "";
        }

        public string Name { get; }
        public string File { get; }

        /// <summary>"Dmax 2.41 (negrul la L* 3.5 fata de albul hartiei)", sau de ce nu s-a putut citi.</summary>
        public string Detail { get; }

        /// <summary>Rezerva nucleului despre cifra (profilele CMYK); null cand nu e niciuna.</summary>
        public string Note { get; }
        public bool Readable { get; }

        /// <summary>Ce se scrie in campul Dmax; gol pe un profil care nu s-a putut citi.</summary>
        public string DmaxText { get; }
    }

    public sealed class IccPickerViewModel : ObservableObject
    {
        IccProfileRow _selected;

        /// <param name="folder">Folderul cu profile; null = cel al Windows-ului. Testele dau unul al lor.</param>
        internal IccPickerViewModel(string folder = null)
        {
            Folder = folder ?? IccPaperBlack.InstalledFolder;
            Profiles = new ObservableCollection<IccProfileRow>(IccPaperBlack.Installed(Folder).Select(b => new IccProfileRow(b)));
            _selected = Profiles.FirstOrDefault(p => p.Readable);
        }

        public IccPickerViewModel() : this(null) { }

        public string Folder { get; }
        public ObservableCollection<IccProfileRow> Profiles { get; }
        public bool IsEmpty => Profiles.Count == 0;

        public IccProfileRow Selected
        {
            get => _selected;
            set { if (Set(ref _selected, value)) Raise(nameof(CanUse)); }
        }

        /// <summary>"Foloseste" e activ doar pe un profil din care s-a putut citi negrul.</summary>
        public bool CanUse => _selected != null && _selected.Readable;

        /// <summary>
        /// Un profil de oriunde, ales cu "Alege fisier...": intra in lista si e selectat, citit
        /// sau nu - cel care nu se poate citi isi arata motivul in loc sa dispara.
        /// </summary>
        public void AddFile(string path)
        {
            var row = new IccProfileRow(IccPaperBlack.Read(path));
            Profiles.Add(row);
            Raise(nameof(IsEmpty));
            Selected = row;
        }

        // Explicatie, nu diagnostic (ADR 0020): ce se citeste si de ce conteaza imprimanta.
        public string Explanation =>
            "Dmax-ul se citeste din tabela colorimetrica a profilului: negrul imprimantei fata de albul hartiei. "
            + "Alege profilul hartiei tale pe imprimanta care o va tipari - de obicei il da atelierul; "
            + "aceeasi hartie pe alta imprimanta are alt negru.";
    }
}
