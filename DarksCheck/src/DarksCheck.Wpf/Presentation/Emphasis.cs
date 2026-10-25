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

using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace DarksCheck.Wpf;

/// <summary>
/// Bold figures inside a row's text (R-071, ADR 0016): the core names the substrings a row
/// emphasises (`ReportRow.Bold`), the console prints the text as it is, and the window sets
/// <see cref="TextProperty"/> and <see cref="BoldProperty"/> on a TextBlock, whose inlines are
/// rebuilt as plain and bold runs. Nothing is computed here: the words and the figures are the
/// core's, this only draws them.
/// </summary>
public static class Emphasis
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterAttached(
        "Text", typeof(string), typeof(Emphasis), new PropertyMetadata(null, OnChanged));

    public static readonly DependencyProperty BoldProperty = DependencyProperty.RegisterAttached(
        "Bold", typeof(IEnumerable<string>), typeof(Emphasis), new PropertyMetadata(null, OnChanged));

    /// <summary>The attached text of an element.</summary>
    /// <param name="d">The element; null gives null.</param>
    /// <returns>The text, or null when unset.</returns>
    public static string? GetText(DependencyObject d) => (string?)d?.GetValue(TextProperty);

    /// <summary>Sets the attached text of an element; a TextBlock's inlines are rebuilt.</summary>
    /// <param name="d">The element; null does nothing.</param>
    /// <param name="value">The row's text; null is drawn as nothing.</param>
    public static void SetText(DependencyObject d, string? value) => d?.SetValue(TextProperty, value);

    /// <summary>The attached bold substrings of an element.</summary>
    /// <param name="d">The element; null gives null.</param>
    /// <returns>The substrings, or null when unset.</returns>
    public static IEnumerable<string>? GetBold(DependencyObject d) => (IEnumerable<string>?)d?.GetValue(BoldProperty);

    /// <summary>
    /// Sets the attached bold substrings of an element; a TextBlock's inlines are rebuilt.
    /// </summary>
    /// <param name="d">The element; null does nothing.</param>
    /// <param name="value">The substrings to draw bold; null or empty draws the text plain.</param>
    public static void SetBold(DependencyObject d, IEnumerable<string>? value) => d?.SetValue(BoldProperty, value);

    /// <summary>
    /// The text cut into plain and bold pieces: every occurrence of every bold substring that
    /// stands as a figure of its own (not preceded by a digit, a sign or a point, not followed by a
    /// digit) is bold, the longest substring first at a position, pieces never overlap.
    /// </summary>
    /// <param name="text">The row's text; null counts as empty.</param>
    /// <param name="bold">The substrings to make bold; null, empty strings and duplicates are
    /// ignored.</param>
    /// <returns>
    /// The pieces in order, which joined give the text back; adjacent pieces differ in weight.
    /// Empty for an empty text; the whole text as one plain piece when nothing is bold.
    /// </returns>
    public static IReadOnlyList<(string Text, bool Bold)> Segments(string? text, IEnumerable<string>? bold)
    {
        text ??= "";
        string[] marks = (bold ?? []).Where(b => !string.IsNullOrEmpty(b)).Distinct(StringComparer.Ordinal).OrderByDescending(b => b.Length).ToArray();
        if (marks.Length == 0 || text.Length == 0)
        {
            return text.Length == 0 ? [] : [(text, false)];
        }

        bool[] isBold = new bool[text.Length];
        foreach (string mark in marks)
        {
            int from = 0;
            while (from < text.Length)
            {
                int at = text.IndexOf(mark, from, StringComparison.Ordinal);
                if (at < 0)
                {
                    break;
                }

                bool wholeFigure = (at == 0 || !IsFigureChar(text[at - 1])) && (at + mark.Length == text.Length || !char.IsAsciiDigit(text[at + mark.Length]));
                if (wholeFigure)
                {
                    for (int i = at; i < at + mark.Length; i++)
                    {
                        isBold[i] = true;
                    }
                }

                from = at + 1;
            }
        }

        List<(string, bool)> pieces = [];
        int start = 0;
        for (int i = 1; i <= text.Length; i++)
        {
            if (i == text.Length || isBold[i] != isBold[start])
            {
                pieces.Add((text[start..i], isBold[start]));
                start = i;
            }
        }

        return pieces;
    }

    private static readonly DependencyProperty PendingProperty = DependencyProperty.RegisterAttached(
        "Pending", typeof(bool), typeof(Emphasis), new PropertyMetadata(false));

    /// <summary>
    /// Whether a character can be part of a figure: an ASCII digit, a point or a sign.
    /// </summary>
    /// <param name="c">The character before a candidate match.</param>
    /// <returns>True for 0-9, '.', '+' and '-'.</returns>
    private static bool IsFigureChar(char c) => char.IsAsciiDigit(c) || c is '.' or '+' or '-';

    /// <summary>
    /// One rebuild per binding pass: the two properties arrive one after the other, so the first
    /// schedules it and the second finds it pending. Elements other than a TextBlock are ignored.
    /// </summary>
    /// <param name="d">The element whose text or bold list changed.</param>
    /// <param name="e">The change; unused, the rebuild reads both properties.</param>
    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock block || (bool)block.GetValue(PendingProperty))
        {
            return;
        }

        block.SetValue(PendingProperty, true);
        block.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.DataBind, () =>
        {
            block.SetValue(PendingProperty, false);
            Rebuild(block);
        });
    }

    /// <summary>
    /// Replaces the block's inlines with the plain and bold runs of its two properties.
    /// </summary>
    /// <param name="block">The text block to redraw.</param>
    private static void Rebuild(TextBlock block)
    {
        block.Inlines.Clear();
        foreach ((string piece, bool isBold) in Segments(GetText(block), GetBold(block)))
        {
            block.Inlines.Add(isBold ? new Run(piece) { FontWeight = FontWeights.Bold } : new Run(piece));
        }
    }
}
