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

// TExtension.cs - a XAML text in both interface languages, side by side where it is used
// (ADR 0048): Text="{local:T En='Open image...', Ro='Deschide imagine...'}".
//
// It picks once, when the XAML loads; the language is set before the first window
// (App.OnStartup) and does not change during a run, so nothing needs to be re-bound.

using System;
using System.Windows.Markup;

namespace AstroPhotoAnalyzer.Wpf
{
    /// <summary>
    /// The markup extension that gives a XAML property the wording of the interface language in
    /// use: <see cref="En"/> or <see cref="Ro"/>.
    /// </summary>
    [MarkupExtensionReturnType(typeof(string))]
    internal sealed class TExtension : MarkupExtension
    {
        /// <summary>The English wording.</summary>
        public string En { get; set; }

        /// <summary>The Romanian wording, without diacritics.</summary>
        public string Ro { get; set; }

        /// <summary>The wording of the language in use.</summary>
        /// <param name="serviceProvider">The XAML parser's services; not used.</param>
        /// <returns><see cref="En"/> or <see cref="Ro"/>, by <c>Tr.Current</c>.</returns>
        public override object ProvideValue(IServiceProvider serviceProvider) => Tr.Pick(En, Ro);
    }
}
