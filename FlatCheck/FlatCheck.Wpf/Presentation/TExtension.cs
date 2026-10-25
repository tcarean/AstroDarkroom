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

// TExtension.cs - the XAML side of Ui.T: a fixed label written in both languages where it
// stands, {l:T En='Structure', Ro='Structura'}, so the XAML keeps its text in its context
// (ADR 0062). The language is chosen once, before the first view loads (App's constructor), so
// picking the wording when the view loads is enough; nothing switches language at run time.

using System;
using System.Windows.Markup;

namespace FlatCheck.Wpf
{
    /// <summary>
    /// <c>{l:T En='...', Ro='...'}</c>: the wording of the run's language (<see cref="Ui.T"/>),
    /// picked when the element loads.
    /// </summary>
    [MarkupExtensionReturnType(typeof(string))]
    public sealed class TExtension : MarkupExtension
    {
        /// <summary>The English wording; ASCII.</summary>
        public string En { get; set; }

        /// <summary>The Romanian wording; ASCII, without diacritics.</summary>
        public string Ro { get; set; }

        /// <summary>Picks the wording of the current language.</summary>
        /// <param name="serviceProvider">The XAML parser's services; not used.</param>
        /// <returns><see cref="Ro"/> in Romanian, <see cref="En"/> otherwise.</returns>
        public override object ProvideValue(IServiceProvider serviceProvider) => Ui.T(En, Ro);
    }
}
