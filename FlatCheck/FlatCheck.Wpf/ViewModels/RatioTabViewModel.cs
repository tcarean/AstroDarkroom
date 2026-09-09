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

// RatioTabViewModel.cs - tabul 2, testul de rotatie la 180 de grade.
//
// Singurul tab care NU ruleaza la pornire: are nevoie de doua fisiere alese de om, si
// nu exista o conventie care sa spuna care sunt. Lista se populeaza din process/ cu
// masterele de flat gasite acolo; se poate alege si un fisier din alta parte, si atunci
// se verifica numele - vezi SessionMessages.NotAFlatMaster pentru de ce se explica in
// loc sa se refuze scurt.
//
// Recalculeaza la orice schimbare a oricareia dintre cele doua alegeri.

using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace FlatCheck.Wpf
{
    public sealed class RatioTabViewModel : TabViewModel
    {
        SirilSession _session;
        CancellationToken _ct;

        string _fileA;
        string _fileB;
        string _structure = "";
        string _structureVerdict = "";
        string _corners = "";
        string _cornerVerdict = "";
        string _verdict = "";
        string _advice = "";
        byte[] _map;

        /// <summary>Masterele gasite in process/. Se arata doar numele, nu calea intreaga.</summary>
        public ObservableCollection<string> Candidates { get; } = new();

        public string FileA
        {
            get => _fileA;
            set { if (Set(ref _fileA, value)) OnSelectionChanged(); }
        }

        public string FileB
        {
            get => _fileB;
            set { if (Set(ref _fileB, value)) OnSelectionChanged(); }
        }

        public string Structure { get => _structure; private set => Set(ref _structure, value); }
        public string StructureVerdict { get => _structureVerdict; private set => Set(ref _structureVerdict, value); }
        public string Corners { get => _corners; private set => Set(ref _corners, value); }
        public string CornerVerdict { get => _cornerVerdict; private set => Set(ref _cornerVerdict, value); }
        public string Verdict { get => _verdict; private set => Set(ref _verdict, value); }
        public string Advice { get => _advice; private set => Set(ref _advice, value); }
        public byte[] Map { get => _map; private set => Set(ref _map, value); }

        /// <summary>
        /// Umple lista si preselecteaza primele doua mastere, daca exista. Preselectia e
        /// o comoditate, nu o presupunere: pe un folder cu exact doua mastere - cazul
        /// obisnuit, setul A si setul B - tabul e gata sa calculeze fara nicio alegere.
        /// </summary>
        internal void Attach(SirilSession session, CancellationToken ct)
        {
            _session = session;
            _ct = ct;

            Candidates.Clear();
            foreach (string p in session.RatioCandidates()) Candidates.Add(p);

            _fileA = Candidates.Count > 0 ? Candidates[0] : null;
            _fileB = Candidates.Count > 1 ? Candidates[1] : null;
            Raise(nameof(FileA));
            Raise(nameof(FileB));
        }

        /// <summary>
        /// Un fisier ales de mana, din afara listei. Se valideaza dupa nume, iar daca nu e
        /// un master de flat se explica ce sunt fisierele astea si de ce testul cere unul.
        /// </summary>
        internal void Choose(bool isA, string path)
        {
            if (string.IsNullOrEmpty(path)) return;

            if (!SirilSession.IsFlatMasterName(path))
            {
                Fail("Fisierul ales nu e un master de flat", SessionMessages.NotAFlatMaster(path));
                return;
            }

            ClearError();
            if (!Candidates.Contains(path)) Candidates.Add(path);
            if (isA) FileA = path; else FileB = path;
        }

        void OnSelectionChanged()
        {
            if (_session == null) return;
            _ = RecomputeAsync();
        }

        internal Task RecomputeAsync()
        {
            if (string.IsNullOrEmpty(FileA) || string.IsNullOrEmpty(FileB))
            {
                HasResult = false;
                return Task.CompletedTask;
            }

            if (FileA == FileB)
            {
                Fail("Aceleasi doua fisiere",
                     "Testul compara doua stive DIFERITE: una inainte de rotatia camerei, una "
                   + "dupa. Impartind un fisier la el insusi iese 1 peste tot, deci un \"OK\" "
                   + "care nu inseamna nimic.");
                return Task.CompletedTask;
            }

            string a = FileA, b = FileB;

            return RunGuardedAsync("Compar cele doua mastere de flat...", async () =>
            {
                var text = new StringWriter();
                var outcome = await Task.Run(() =>
                {
                    RunReport.WriteBanner(text);
                    return RatioRun.Execute(a, b, text);
                }, _ct).ConfigureAwait(true);

                Report = text.ToString();
                Populate(outcome.Result);
                Map = outcome.Png;
                HasResult = true;
            });
        }

        void Populate(RatioResult r)
        {
            Structure = $"{r.PtpPct:F2} %";
            StructureVerdict = r.PtpVerdict;
            Corners = $"{r.CornerSpreadPct:F2} %";
            CornerVerdict = r.CornerVerdict;
            Verdict = r.Verdict;
            Advice = r.Verdict == "OK"
                ? "Setup-ul de flats e curat. Poti construi biblioteca pe el."
                : "Setup-ul contamineaza flat-ul. Repara sursa inainte de biblioteca.";
        }

        internal override void Reset()
        {
            base.Reset();
            _session = null;
            Candidates.Clear();
            _fileA = null;
            _fileB = null;
            Raise(nameof(FileA));
            Raise(nameof(FileB));
            Structure = Corners = "";
            StructureVerdict = CornerVerdict = "";
            Verdict = "";
            Advice = "";
            Map = null;
        }
    }
}
