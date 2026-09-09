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

// TabViewModel.cs - ce au in comun cele patru taburi.
//
// Toate patru fac acelasi lucru: arata un ecran de asteptare, calculeaza pe alt fir,
// apoi ori arata rezultate, ori arata o explicatie. Starea aia - ocupat / rezultat /
// eroare - e aici, o singura data, ca cele patru sa nu ajunga cu patru feluri de a
// esua.
//
// DE CE SUNT PUBLICE clasele astea, cand tot restul aplicatiei e internal: legarea WPF
// citeste prin reflexie proprietati publice. Consecinta e utila, nu doar birocratica -
// un ViewModel public NU poate expune un tip din nucleu, care e internal, deci
// compilatorul insusi impiedica scurgerea unui StructureResult in XAML. Ce se vede pe
// ecran trece obligatoriu prin campuri simple, alese anume.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace FlatCheck.Wpf
{
    public abstract class TabViewModel : ObservableObject
    {
        bool _busy;
        string _busyMessage = "";
        string _busyDetail = "";
        double _progress;
        bool _progressKnown;
        string _errorTitle;
        string _errorText;
        bool _hasResult;
        string _report = "";

        /// <summary>Cat timp e true, tabul isi arata ecranul de asteptare.</summary>
        public bool IsBusy
        {
            get => _busy;
            private set { if (Set(ref _busy, value)) Raise(nameof(IsIdle)); }
        }

        public bool IsIdle => !_busy;

        public string BusyMessage { get => _busyMessage; private set => Set(ref _busyMessage, value); }

        /// <summary>A doua linie a ecranului de asteptare: ce cadru, al catelea din cate.</summary>
        public string BusyDetail { get => _busyDetail; protected set => Set(ref _busyDetail, value); }

        /// <summary>0..1. Are sens doar cand <see cref="ProgressKnown"/> e true.</summary>
        public double Progress { get => _progress; protected set => Set(ref _progress, value); }

        /// <summary>
        /// false pentru o singura analiza - nu se poate sti cat mai dureaza, deci bara
        /// merge nedefinit. true doar pe serie, unde se stie cate cadre sunt.
        /// </summary>
        public bool ProgressKnown { get => _progressKnown; protected set => Set(ref _progressKnown, value); }

        public string ErrorTitle { get => _errorTitle; private set => Set(ref _errorTitle, value); }

        public string ErrorText
        {
            get => _errorText;
            private set { if (Set(ref _errorText, value)) Raise(nameof(HasError)); }
        }

        public bool HasError => !string.IsNullOrEmpty(_errorText);

        /// <summary>true cand tabul chiar are cifre de aratat.</summary>
        public bool HasResult { get => _hasResult; protected set => Set(ref _hasResult, value); }

        /// <summary>
        /// Raportul complet, exact cum l-ar fi scris consola. Se arata intr-un panou
        /// pliabil si e ce se salveaza pe disc - nu se reformateaza nicaieri.
        /// </summary>
        public string Report { get => _report; protected set => Set(ref _report, value); }

        /// <summary>
        /// Ruleaza munca pe alt fir, cu ecran de asteptare, si transforma orice esec
        /// intr-o eroare afisabila.
        ///
        /// Rezultatul e ASTEPTAT, nu abandonat: dupa await executia se intoarce pe firul
        /// de interfata (in aplicatie; in teste nu exista context, deci pe thread pool),
        /// asa ca ce urmeaza poate atinge linistit proprietatile legate de XAML.
        ///
        /// Anularea nu e o eroare - inseamna ca omul a schimbat folderul sau a inchis
        /// fereastra, si atunci tabul ramane pur si simplu gol.
        /// </summary>
        protected async Task RunGuardedAsync(string busyMessage, Func<Task> work)
        {
            BeginBusy(busyMessage);
            try
            {
                await work().ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                HasResult = false;
            }
            catch (Exception e)
            {
                HasResult = false;
                Fail("Analiza a esuat", Describe(e));
            }
            finally
            {
                EndBusy();
            }
        }

        /// <summary>
        /// Mesajul unei exceptii, cu cauza daca exista. Nu se arata stiva: pe un fisier
        /// FITS stricat mesajul cititorului spune deja ce nu s-a putut citi, iar o stiva
        /// de zece cadre pe ecran nu ajuta pe nimeni sa refaca flat-urile.
        /// </summary>
        internal static string Describe(Exception e)
        {
            string m = e.Message;
            for (Exception inner = e.InnerException; inner != null; inner = inner.InnerException)
                if (!string.IsNullOrWhiteSpace(inner.Message) && !m.Contains(inner.Message))
                    m += "\n\n" + inner.Message;
            return m;
        }

        protected void BeginBusy(string message)
        {
            ClearError();
            BusyMessage = message;
            BusyDetail = "";
            Progress = 0;
            ProgressKnown = false;
            IsBusy = true;
        }

        protected void EndBusy()
        {
            IsBusy = false;
            BusyDetail = "";
        }

        /// <summary>
        /// Arata o explicatie in locul rezultatelor. Titlu + text, fiindca a doua parte e
        /// mereu lunga: cerintele cer sa se explice CE e fisierul lipsa si CE sa faca omul,
        /// nu doar ca lipseste.
        /// </summary>
        protected void Fail(string title, string text)
        {
            HasResult = false;
            ErrorTitle = title;
            ErrorText = text;
        }

        protected void ClearError()
        {
            ErrorTitle = null;
            ErrorText = null;
        }

        /// <summary>Pentru cand tabul se goleste: alt folder ales, sau folder invalid.</summary>
        internal virtual void Reset()
        {
            ClearError();
            HasResult = false;
            Report = "";
            EndBusy();
        }
    }
}
