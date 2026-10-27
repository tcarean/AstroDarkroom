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
// with those parts in bold, after an optional label. The report's "because" lines carry the
// marks where a figure must catch the eye (the share of the frame with a visible plateau and its
// threshold); the text report strips them (Fmt.Plain), the Raport tab renders them through these
// properties. The block's inlines are owned here, label included: a XAML attribute is applied
// before the element's content, so a label given as content would land after the text.
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace AstroPhotoAnalyzer.Wpf
{
    /// <summary>
    /// The attached properties <c>Emphasis.Label</c> and <c>Emphasis.Text</c> for a
    /// <see cref="TextBlock"/>: the block's inlines become the label, semi-bold, then the text
    /// with the parts between <c>**</c> marks in bold.
    /// </summary>
    internal static class Emphasis
    {
        /// <summary>The mark that opens and closes an emphasised part, as the core writes it.</summary>
        internal const string Mark = "**";

        /// <summary>The attached property: the label before the text ("because: "), semi-bold; null or empty for none.</summary>
        public static readonly DependencyProperty LabelProperty = DependencyProperty.RegisterAttached(
            "Label", typeof(string), typeof(Emphasis), new PropertyMetadata(null, OnChanged));

        /// <summary>The attached property: the text with <c>**</c> marks.</summary>
        public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterAttached(
            "Text", typeof(string), typeof(Emphasis), new PropertyMetadata(null, OnChanged));

        /// <summary>Reads the attached label.</summary>
        /// <param name="block">The text block.</param>
        /// <returns>The label, or null.</returns>
        public static string GetLabel(TextBlock block) => (string)block.GetValue(LabelProperty);

        /// <summary>Sets the attached label.</summary>
        /// <param name="block">The text block.</param>
        /// <param name="value">The label; null or empty for none.</param>
        public static void SetLabel(TextBlock block, string value) => block.SetValue(LabelProperty, value);

        /// <summary>Reads the attached text.</summary>
        /// <param name="block">The text block.</param>
        /// <returns>The text with its marks, or null.</returns>
        public static string GetText(TextBlock block) => (string)block.GetValue(TextProperty);

        /// <summary>Sets the attached text.</summary>
        /// <param name="block">The text block.</param>
        /// <param name="value">The text with <c>**</c> marks; null clears the text's runs.</param>
        public static void SetText(TextBlock block, string value) => block.SetValue(TextProperty, value);

        /// <summary>
        /// Rebuilds the block's inlines from both properties: the label's run, then the text's
        /// runs, bold between the marks.
        /// </summary>
        /// <param name="d">The text block.</param>
        /// <param name="e">The change; either property.</param>
        static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not TextBlock block) return;
            block.Inlines.Clear();
            string label = GetLabel(block);
            if (!string.IsNullOrEmpty(label)) block.Inlines.Add(new Run(label) { FontWeight = FontWeights.SemiBold });
            foreach (Inline inline in Parse(GetText(block))) block.Inlines.Add(inline);
        }

        /// <summary>
        /// The runs of a marked text: plain and bold alternating, the marks themselves gone. An
        /// unclosed mark is kept as text, so that a stray <c>**</c> never hides what follows.
        /// </summary>
        /// <param name="text">The text with <c>**</c> marks; null or empty gives no run.</param>
        /// <returns>The runs, in order.</returns>
        internal static Inline[] Parse(string text)
        {
            var runs = new List<Inline>();
            if (string.IsNullOrEmpty(text)) return runs.ToArray();
            int at = 0;
            while (at < text.Length)
            {
                int open = text.IndexOf(Mark, at, StringComparison.Ordinal);
                if (open < 0) { runs.Add(new Run(text.Substring(at))); break; }
                int close = text.IndexOf(Mark, open + Mark.Length, StringComparison.Ordinal);
                if (close < 0) { runs.Add(new Run(text.Substring(at))); break; }
                if (open > at) runs.Add(new Run(text.Substring(at, open - at)));
                runs.Add(new Bold(new Run(text.Substring(open + Mark.Length, close - open - Mark.Length))));
                at = close + Mark.Length;
            }
            return runs.ToArray();
        }
    }
}
