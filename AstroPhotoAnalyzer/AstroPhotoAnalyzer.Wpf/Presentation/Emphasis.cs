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

// Emphasis.cs - a text with Fmt.Strong marks ("**...**", the core's one piece of markup) shown
// with those parts in bold. The report's "because" lines carry the marks where a figure must
// catch the eye (the share of the frame with a visible plateau and its threshold); the text
// report strips them (Fmt.Plain), the Raport tab renders them through this property.
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace AstroPhotoAnalyzer.Wpf
{
    /// <summary>
    /// The attached property <c>Emphasis.Text</c> for a <see cref="TextBlock"/>: the text with
    /// the parts between <c>**</c> marks in bold, appended after the inlines the XAML gave the
    /// block (a label such as "because: "), which stay.
    /// </summary>
    internal static class Emphasis
    {
        /// <summary>The mark that opens and closes an emphasised part, as the core writes it.</summary>
        internal const string Mark = "**";

        /// <summary>The attached property: the text with <c>**</c> marks.</summary>
        public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterAttached(
            "Text", typeof(string), typeof(Emphasis), new PropertyMetadata(null, OnTextChanged));

        /// <summary>Reads the attached text.</summary>
        /// <param name="block">The text block.</param>
        /// <returns>The text with its marks, or null.</returns>
        public static string GetText(TextBlock block) => (string)block.GetValue(TextProperty);

        /// <summary>Sets the attached text.</summary>
        /// <param name="block">The text block.</param>
        /// <param name="value">The text with <c>**</c> marks; null clears the appended runs.</param>
        public static void SetText(TextBlock block, string value) => block.SetValue(TextProperty, value);

        /// <summary>
        /// Rebuilds the appended runs: every inline after the first one the XAML gave is
        /// removed, then the text's parts are added, bold between the marks.
        /// </summary>
        /// <param name="d">The text block.</param>
        /// <param name="e">The change; the new value is the text.</param>
        static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not TextBlock block) return;
            while (block.Inlines.Count > 1) block.Inlines.Remove(block.Inlines.LastInline);
            foreach (Inline inline in Parse(e.NewValue as string)) block.Inlines.Add(inline);
        }

        /// <summary>
        /// The runs of a marked text: plain and bold alternating, the marks themselves gone. An
        /// unclosed mark is kept as text, so that a stray <c>**</c> never hides what follows.
        /// </summary>
        /// <param name="text">The text with <c>**</c> marks; null or empty gives no run.</param>
        /// <returns>The runs, in order.</returns>
        internal static Inline[] Parse(string text)
        {
            var runs = new System.Collections.Generic.List<Inline>();
            if (string.IsNullOrEmpty(text)) return runs.ToArray();
            int at = 0;
            while (at < text.Length)
            {
                int open = text.IndexOf(Mark, at, System.StringComparison.Ordinal);
                if (open < 0) { runs.Add(new Run(text.Substring(at))); break; }
                int close = text.IndexOf(Mark, open + Mark.Length, System.StringComparison.Ordinal);
                if (close < 0) { runs.Add(new Run(text.Substring(at))); break; }
                if (open > at) runs.Add(new Run(text.Substring(at, open - at)));
                runs.Add(new Bold(new Run(text.Substring(open + Mark.Length, close - open - Mark.Length))));
                at = close + Mark.Length;
            }
            return runs.ToArray();
        }
    }
}
