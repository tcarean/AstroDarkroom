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

// Catalogul de profile de obiectiv, incarcat o singura data pentru toata fereastra.
//
// Era al tabului Sintetic; de cand tabul Structura compara vignetarea masterului cu
// profilele obiectivului, il vor amandoua, si doua incarcari ar fi insemnat de doua ori
// cele ~3500 de fisiere Adobe listate (o secunda-doua) si doua liste care pot diferi.
// MainViewModel il construieste si il da ambelor taburi; testele dau un Func care
// intoarce surse facute de mana, ca fereastra sa nu depinda de ce e instalat pe masina.

using System;
using System.Threading.Tasks;

namespace FlatCheck.Wpf
{
    internal sealed class ProfileCatalog
    {
        readonly Func<ProfileSources> _locate;
        readonly object _gate = new();
        Task<ProfileSources> _loading;

        internal ProfileCatalog(Func<ProfileSources> locate = null)
        {
            _locate = locate ?? ProfileLocator.Locate;
        }

        /// <summary>
        /// Sursele, incarcate in fundal la prima cerere si tinute minte. O incarcare care a
        /// esuat (un folder inaccesibil) se reia la urmatoarea cerere, nu ramane esuata pe
        /// toata durata ferestrei.
        /// </summary>
        internal Task<ProfileSources> EnsureAsync()
        {
            lock (_gate)
            {
                if (_loading == null || _loading.IsFaulted || _loading.IsCanceled)
                    _loading = Task.Run(_locate);
                return _loading;
            }
        }

        /// <summary>Sursele deja incarcate, sau null daca inca nu sunt.</summary>
        internal ProfileSources Loaded
        {
            get
            {
                lock (_gate) return _loading != null && _loading.IsCompletedSuccessfully ? _loading.Result : null;
            }
        }
    }
}
