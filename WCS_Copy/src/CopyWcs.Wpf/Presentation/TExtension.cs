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

// Copied from DarksCheck (which took it from FlatCheck, its ADR 0062) for the window's two
// languages (ADR 0004, ADR 0007); a fix goes there first. Nothing switches language at run
// time, but the wording is picked when the element is made, not when the XAML is parsed: a
// template or a style in a resource dictionary is parsed once, possibly before a test opens an
// English scope, and a string returned there would stay in that first language. So on a
// dependency property, or in a Setter, the extension returns a one-time binding, which each
// instance resolves when it is made; on a plain property it returns the string at once.

using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace CopyWcs.Wpf;

/// <summary>
/// <c>{l:T En='...', Ro='...'}</c>: a fixed label written in both languages where it stands, the
/// wording of the run's language (<see cref="Ui.T"/>) picked when the element that shows it is made.
/// </summary>
[MarkupExtensionReturnType(typeof(object))]
public sealed class TExtension : MarkupExtension
{
    /// <summary>The English wording; ASCII.</summary>
    public string En { get; set; } = string.Empty;

    /// <summary>The Romanian wording; ASCII, without diacritics.</summary>
    public string Ro { get; set; } = string.Empty;

    /// <summary>
    /// A one-time binding to the pair on a dependency property or a Setter's value, so the wording
    /// is picked when the element is made; the wording itself anywhere else.
    /// </summary>
    /// <param name="serviceProvider">The XAML parser's services, for the target; null is allowed and
    /// gives the wording.</param>
    /// <returns>The binding's value for the target, or the wording.</returns>
    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        IProvideValueTarget? target = serviceProvider?.GetService(typeof(IProvideValueTarget)) as IProvideValueTarget;
        bool bindable = target?.TargetProperty is DependencyProperty || target?.TargetObject is Setter;
        if (!bindable)
        {
            return Ui.T(En, Ro);
        }

        Binding binding = new(nameof(Pair.Value)) { Source = new Pair(En, Ro), Mode = BindingMode.OneTime };
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
