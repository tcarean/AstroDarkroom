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
// (ADR 0062). Nothing switches language at run time, but the wording is picked when the element
// is made, not when the XAML is parsed: a template or a style in a resource dictionary is parsed
// once, possibly before the language is set (the theme, in the tests, loads before a test
// switches to English), and a string returned there would stay in that first language. So on a
// dependency property, or in a Setter, the extension returns a one-time binding, which each
// instance of the template resolves when it is made; on a plain property (a Binding's
// TargetNullValue) it returns the string at once.

using System;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace FlatCheck.Wpf
{
    /// <summary>
    /// <c>{l:T En='...', Ro='...'}</c>: the wording of the run's language (<see cref="Ui.T"/>),
    /// picked when the element that shows it is made.
    /// </summary>
    [MarkupExtensionReturnType(typeof(object))]
    public sealed class TExtension : MarkupExtension
    {
        /// <summary>The English wording; ASCII.</summary>
        public string En { get; set; }

        /// <summary>The Romanian wording; ASCII, without diacritics.</summary>
        public string Ro { get; set; }

        /// <summary>
        /// A one-time binding to the pair on a dependency property or a Setter's value, so the
        /// wording is picked when the element is made; the wording itself anywhere else.
        /// </summary>
        /// <param name="serviceProvider">The XAML parser's services, for the target.</param>
        /// <returns>The binding's value for the target, or the wording.</returns>
        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            var target = serviceProvider?.GetService(typeof(IProvideValueTarget)) as IProvideValueTarget;
            bool bindable = target?.TargetProperty is DependencyProperty || target?.TargetObject is Setter;
            if (!bindable) return Ui.T(En, Ro);

            var binding = new Binding(nameof(Pair.Value)) { Source = new Pair(En, Ro), Mode = BindingMode.OneTime };
            return binding.ProvideValue(serviceProvider);
        }

        /// <summary>The two wordings, read by the binding when it resolves.</summary>
        /// <param name="En">The English wording.</param>
        /// <param name="Ro">The Romanian wording.</param>
        public sealed record Pair(string En, string Ro)
        {
            /// <summary>The wording of the language current when the binding resolves.</summary>
            public string Value => Ui.T(En, Ro);
        }
    }
}
