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

// IccPickerViewModel.cs - the "Din profil ICC..." window (R-132): the list of the installed
// printer profiles, with the Dmax of each, and the chosen profile. It parses no profile and
// computes nothing: the figures and the sentences about a profile are the core's
// (IccPaperBlack); here are only the list, the selection and the text that explains what is
// read.

using System.Collections.ObjectModel;
using System.Linq;

namespace AstroPhotoAnalyzer.Wpf
{
    /// <summary>
    /// A profile in the list: its name, its file, and what the core read from it.
    /// </summary>
    public sealed class IccProfileRow
    {
        /// <summary>Copies what the core read into bindable fields.</summary>
        /// <param name="b">The core's reading of the profile, readable or not.</param>
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

        /// <summary>
        /// "Dmax 2.41 (negrul la L* 3.5 fata de albul hartiei)", or why it could not be read.
        /// </summary>
        public string Detail { get; }

        /// <summary>
        /// The core's reservation about the figure (the CMYK profiles); null when there is none.
        /// </summary>
        public string Note { get; }
        public bool Readable { get; }

        /// <summary>
        /// What goes into the Dmax field; empty on a profile that could not be read.
        /// </summary>
        public string DmaxText { get; }
    }

    /// <summary>
    /// The profile window's state (R-132): the installed profiles, the one selected, and whether
    /// "Foloseste" may take it.
    /// </summary>
    public sealed class IccPickerViewModel : ObservableObject
    {
        IccProfileRow _selected;

        /// <summary>Lists the profiles of a folder and selects the first readable one.</summary>
        /// <param name="folder">
        /// The folder with the profiles; null = Windows's. The tests give one of their own.
        /// </param>
        internal IccPickerViewModel(string folder = null)
        {
            Folder = folder ?? IccPaperBlack.InstalledFolder;
            Profiles = new ObservableCollection<IccProfileRow>(IccPaperBlack.Installed(Folder).Select(b => new IccProfileRow(b)));
            _selected = Profiles.FirstOrDefault(p => p.Readable);
        }

        /// <summary>The window's constructor: the installed profiles of Windows's folder.</summary>
        public IccPickerViewModel() : this(null) { }

        public string Folder { get; }
        public ObservableCollection<IccProfileRow> Profiles { get; }
        public bool IsEmpty => Profiles.Count == 0;

        public IccProfileRow Selected
        {
            get => _selected;
            set { if (Set(ref _selected, value)) Raise(nameof(CanUse)); }
        }

        /// <summary>
        /// "Foloseste" is enabled only on a profile from which the black could be read.
        /// </summary>
        public bool CanUse => _selected != null && _selected.Readable;

        /// <summary>
        /// A profile from anywhere, chosen with "Alege fisier...": it enters the list and is
        /// selected, readable or not - one that cannot be read shows its reason instead of
        /// disappearing.
        /// </summary>
        /// <param name="path">
        /// The profile's path; one already in the list is selected, not added again.
        /// </param>
        public void AddFile(string path)
        {
            // A file already in the list (chosen a second time, or one of the installed ones) is
            // selected, not duplicated.
            IccProfileRow row = Profiles.FirstOrDefault(p => string.Equals(p.File, path, System.StringComparison.OrdinalIgnoreCase));
            if (row == null)
            {
                row = new IccProfileRow(IccPaperBlack.Read(path));
                Profiles.Add(row);
                Raise(nameof(IsEmpty));
            }
            Selected = row;
        }

        // Explanation, not diagnosis (ADR 0020): what is read and why the printer matters.
        public string Explanation =>
            "Dmax-ul se citeste din tabela colorimetrica a profilului: negrul imprimantei fata de albul hartiei. "
            + "Alege profilul hartiei tale pe imprimanta care o va tipari - de obicei il da atelierul; "
            + "aceeasi hartie pe alta imprimanta are alt negru.";
    }
}
