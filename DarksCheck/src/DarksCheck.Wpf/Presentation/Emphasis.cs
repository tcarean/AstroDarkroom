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

    public static string? GetText(DependencyObject d) => (string?)d?.GetValue(TextProperty);

    public static void SetText(DependencyObject d, string? value) => d?.SetValue(TextProperty, value);

    public static IEnumerable<string>? GetBold(DependencyObject d) => (IEnumerable<string>?)d?.GetValue(BoldProperty);

    public static void SetBold(DependencyObject d, IEnumerable<string>? value) => d?.SetValue(BoldProperty, value);

    /// <summary>The text cut into plain and bold pieces: every occurrence of every bold substring that stands as a figure of its own (not preceded by a digit, a sign or a point, not followed by a digit) is bold, the longest substring first at a position, pieces never overlap.</summary>
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

    private static bool IsFigureChar(char c) => char.IsAsciiDigit(c) || c is '.' or '+' or '-';

    /// <summary>One rebuild per binding pass: the two properties arrive one after the other, so the first schedules it and the second finds it pending.</summary>
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

    private static void Rebuild(TextBlock block)
    {
        block.Inlines.Clear();
        foreach ((string piece, bool isBold) in Segments(GetText(block), GetBold(block)))
        {
            block.Inlines.Add(isBold ? new Run(piece) { FontWeight = FontWeights.Bold } : new Run(piece));
        }
    }
}
