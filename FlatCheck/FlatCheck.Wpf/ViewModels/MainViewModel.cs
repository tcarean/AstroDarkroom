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

// MainViewModel.cs - folderul sesiunii si cele sase taburi.
//
// Doua decizii de comportament merita explicate:
//
// SECVENTIAL, NU PARALEL. Structura si testul end-to-end pornesc amandoua la deschiderea
// unui folder, dar unul dupa altul. Un cadru de 24 MP ajunge la sute de MB in analiza
// (vezi "Known issues" din CLAUDE.md), iar seria mai tine si un cadru incarcat pe langa;
// doua lanturi simultane ar putea trece de un gigaoctet pe o masina obisnuita. Timpul
// castigat n-ar merita riscul.
//
// FOLDER NOU = ANULARE. Cand se alege alt folder in timp ce ruleaza ceva, munca veche se
// anuleaza si taburile se golesc, in loc sa se lase rezultate din folderul precedent
// langa cele noi. Anularea se prinde intre cadre, deci raspunde in sub o secunda.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace FlatCheck.Wpf
{
    public sealed class MainViewModel : ObservableObject
    {
        CancellationTokenSource _cts;
        SirilSession _session;

        string _folder = "";
        string _problemTitle;
        string _problemText;
        bool _running;

        readonly Func<SirilInstall> _locate;
        readonly Func<bool> _sirilGuiRunning;

        /// <summary>Lantul pornit la deschidere (structura, seria, ratio), ca tabul de netezire sa-l poata astepta.</summary>
        Task _chain = Task.CompletedTask;

        public MainViewModel() : this(null, null, null) { }

        /// <summary>
        /// Cele doua intrebari despre masina - unde e Siril, si daca ruleaza - se dau din
        /// afara ca testele sa nu depinda de ce e instalat pe calculatorul care le ruleaza.
        /// Null inseamna raspunsul adevarat. A treia e intrebarea de suprascriere a tabului
        /// de netezire: implicit un MessageBox, in teste un raspuns dat dinainte.
        /// </summary>
        internal MainViewModel(Func<SirilInstall> locate, Func<bool> sirilGuiRunning, Func<string, bool> confirm = null,
                               Func<ProfileSources> locateProfiles = null)
        {
            _locate = locate ?? SirilInstall.Locate;
            _sirilGuiRunning = sirilGuiRunning ?? SirilInstall.IsGuiRunning;

            Structure = new StructureTabViewModel();
            Ratio = new RatioTabViewModel();
            Light = new LightTabViewModel();
            Exposure = new ExposureTabViewModel();
            HistEq = new HistEqTabViewModel();
            Smooth = new SmoothTabViewModel(confirm ?? AskOnScreen);
            Synthetic = new SyntheticTabViewModel(confirm ?? AskOnScreen, locateProfiles ?? ProfileLocator.Locate);
            Preprocess = new PreprocessViewModel();

            // Singura legatura intre doua taburi: flat-ul netezit pe care il scrie Netezire
            // apare in lista tabului Histogram, langa master. Legata aici, ca niciunul din
            // ele sa nu-l cunoasca pe celalalt; FileWritten ignora singur o cale care nu e
            // a sesiunii curente, deci schimbarea folderului nu are nimic de dezlegat.
            Smooth.Saved += HistEq.FileWritten;
        }

        static bool AskOnScreen(string question)
            => System.Windows.MessageBox.Show(question, "FlatCheck", System.Windows.MessageBoxButton.YesNo,
                   System.Windows.MessageBoxImage.Question, System.Windows.MessageBoxResult.No)
               == System.Windows.MessageBoxResult.Yes;

        public StructureTabViewModel Structure { get; }
        public RatioTabViewModel Ratio { get; }
        public LightTabViewModel Light { get; }
        public ExposureTabViewModel Exposure { get; }
        public HistEqTabViewModel HistEq { get; }
        public SmoothTabViewModel Smooth { get; }
        public SyntheticTabViewModel Synthetic { get; }

        /// <summary>
        /// Se termina cand lantul de la deschidere s-a terminat. Tabul de netezire ruleaza
        /// dintr-un click, deci s-ar putea suprapune peste structura daca omul apasa in
        /// timpul ei; asteapta intai - regula "secvential, nu paralel" de mai sus.
        /// </summary>
        internal Task WhenIdleAsync() => _chain;

        /// <summary>
        /// Blocul "ruleaza scriptul de aici" de pe ecranul de problema. Exista mereu, ca
        /// legarile din XAML sa aiba pe ce sta; e oferit doar cand problema e una pe care
        /// un script o rezolva.
        /// </summary>
        public PreprocessViewModel Preprocess { get; }

        /// <summary>Folderul sesiunii, sau gol daca nu s-a ales inca niciunul.</summary>
        public string Folder
        {
            get => _folder;
            private set { if (Set(ref _folder, value)) Raise(nameof(HasFolder)); }
        }

        public bool HasFolder => !string.IsNullOrEmpty(_folder);

        /// <summary>
        /// Problema care impiedica pornirea pe folderul asta, daca exista. Cand e setata,
        /// taburile nu se arata deloc: n-are rost sa vezi patru panouri goale cand ce
        /// trebuie facut e sa rulezi un script in Siril.
        /// </summary>
        public string ProblemTitle { get => _problemTitle; private set => Set(ref _problemTitle, value); }

        public string ProblemText
        {
            get => _problemText;
            private set { if (Set(ref _problemText, value)) Raise(nameof(HasProblem)); }
        }

        public bool HasProblem => !string.IsNullOrEmpty(_problemText);

        /// <summary>true cat timp ruleaza analizele de la deschiderea folderului.</summary>
        public bool IsRunning { get => _running; private set => Set(ref _running, value); }

        /// <summary>Versiunea si copyright-ul, aceleasi cu ce scrie consola pe primele doua linii.</summary>
        public string Banner => VersionInfo.Banner;
        public string Copyright => VersionInfo.Copyright;

        /// <summary>
        /// Deschide un folder: valideaza, pregateste taburile care asteapta o alegere, si
        /// porneste analizele care ruleaza singure.
        /// </summary>
        internal async Task OpenAsync(string folder)
        {
            CancelPending();

            Folder = folder ?? "";
            ResetTabs();

            if (string.IsNullOrWhiteSpace(folder)) { ClearProblem(); return; }

            var session = new SirilSession(folder);
            SessionProblem problem = session.Check();
            if (problem != SessionProblem.None)
            {
                _session = null;
                ProblemTitle = SessionMessages.Title(problem);
                ProblemText = SessionMessages.Explain(session, problem);

                // Un script Siril produce process/ si masterul din el; un folder care nu
                // exista nu-l ajuta niciun script.
                if (problem == SessionProblem.NoProcessDir || problem == SessionProblem.NoFlatMaster)
                    Preprocess.Attach(session, _locate(), _sirilGuiRunning);
                return;
            }

            ClearProblem();
            _session = session;
            _cts = new CancellationTokenSource();
            CancellationToken ct = _cts.Token;

            // Taburile care asteapta o alegere se pregatesc imediat: listele lor se
            // citesc din folder, nu din vreun calcul, deci n-au de ce sa astepte.
            Ratio.Attach(session, ct);
            Exposure.Attach(session, ct);
            // Tot o lista, tot fara calcul: masterul de 24 MP nu se incarca de doua ori la
            // deschidere - il citeste deja Structura, mai jos.
            HistEq.Attach(session, ct);
            // Nici netezirea nu calculeaza la deschidere: scrie un fisier, si un fisier
            // nu se produce fiindca s-a deschis un folder. Asteapta un click.
            Smooth.Attach(session, ct, WhenIdleAsync);
            Synthetic.Attach(session, ct, WhenIdleAsync);

            IsRunning = true;
            _chain = RunChainAsync(session, ct);
            try
            {
                await _chain.ConfigureAwait(true);
            }
            finally
            {
                IsRunning = false;
            }
        }

        async Task RunChainAsync(SirilSession session, CancellationToken ct)
        {
            await Structure.RunAsync(session, ct).ConfigureAwait(true);
            if (ct.IsCancellationRequested) return;

            await Light.RunAsync(session, ct).ConfigureAwait(true);
            if (ct.IsCancellationRequested) return;

            // Tabul Ratio are, de obicei, doua mastere preselectate; abia acum, dupa
            // ce celelalte doua au terminat, se poate calcula fara sa se bata pe memorie.
            await Ratio.RecomputeAsync().ConfigureAwait(true);
        }

        /// <summary>Se cheama la inchiderea ferestrei, ca sa nu ramana fire care lucreaza degeaba.</summary>
        internal void CancelPending()
        {
            CancellationTokenSource cts = _cts;
            _cts = null;
            if (cts == null) return;

            try { cts.Cancel(); } catch (ObjectDisposedException) { }
            cts.Dispose();
        }

        /// <summary>Dupa o rulare de script reusita: acelasi folder, de la capat, prin Check().</summary>
        internal Task ReopenAsync() => OpenAsync(Folder);

        void ResetTabs()
        {
            Preprocess.Detach();
            Structure.Reset();
            Ratio.Reset();
            Light.Reset();
            Exposure.Reset();
            HistEq.Reset();
            Smooth.Reset();
            Synthetic.Reset();
        }

        void ClearProblem()
        {
            ProblemTitle = null;
            ProblemText = null;
        }
    }
}
