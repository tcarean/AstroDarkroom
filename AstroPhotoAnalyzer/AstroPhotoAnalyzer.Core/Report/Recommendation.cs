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

// Recommendation.cs - a recommendation: about what, how serious, what to do, and WHY.
//
// "Because" is not optional. A recommendation without a figure and without a threshold cannot
// be checked, and this tool exists precisely so that it can be checked. The window shows it as
// small text under the recommendation; the console as "because:" ("fiindca:" in Romanian).

using System;

namespace AstroPhotoAnalyzer
{
    /// <summary>
    /// How serious a recommendation is. Monotone: Ok &lt; Attention &lt; Problem (printed "OK",
    /// "CAUTION", "PROBLEM", in Romanian "OK", "ATENTIE", "PROBLEMA"). A section's verdict and
    /// the overall verdict are the maximum.
    /// </summary>
    internal enum Verdict { Ok = 0, Attention = 1, Problem = 2 }

    /// <summary>
    /// The report's sections, in the order of the decisions - not of the measurements.
    /// </summary>
    internal enum Topic
    {
        Format = 1,
        Background = 2,
        Colour = 3,
        Size = 4,
        Denoise = 5,
        Warnings = 6,
    }

    /// <summary>
    /// A recommendation of the report or of the Curbe tab: its section, its verdict, what to do
    /// and why (ADR 0011).
    /// </summary>
    internal sealed record Recommendation
    {
        internal Topic Topic { get; init; }
        internal Verdict Verdict { get; init; }

        /// <summary>One sentence: what to do.</summary>
        internal string Text { get; init; }

        /// <summary>The metric and the threshold: why.</summary>
        internal string Because { get; init; }

        /// <summary>A recommendation with its evidence.</summary>
        /// <param name="topic">The report's section it belongs to.</param>
        /// <param name="verdict">How serious it is.</param>
        /// <param name="text">One sentence, in the interface language: what to do.</param>
        /// <param name="because">
        /// The metric and the threshold, in the interface language; never null, empty or blank:
        /// the constructor throws an <see cref="ArgumentException"/> for one.
        /// </param>
        internal Recommendation(Topic topic, Verdict verdict, string text, string because)
        {
            if (string.IsNullOrWhiteSpace(because))
                throw new ArgumentException(Tr.Pick("a recommendation without a 'because' is not a recommendation", "o recomandare fara 'fiindca' nu e o recomandare"));
            Topic = topic; Verdict = verdict; Text = text; Because = because;
        }
    }

    /// <summary>The words the report and the window show for a verdict and for a section.</summary>
    internal static class VerdictText
    {
        /// <summary>A verdict as the report prints it.</summary>
        /// <param name="v">The verdict; a value outside the enum throws.</param>
        /// <returns>"OK", "CAUTION" or "PROBLEM" ("OK", "ATENTIE" or "PROBLEMA").</returns>
        internal static string Label(Verdict v) => v switch
        {
            Verdict.Ok => "OK",
            Verdict.Attention => Tr.Pick("CAUTION", "ATENTIE"),
            Verdict.Problem => Tr.Pick("PROBLEM", "PROBLEMA"),
            _ => throw new ArgumentOutOfRangeException(nameof(v)),
        };

        /// <summary>A section's title, as the report numbers it and the window shows it.</summary>
        /// <param name="t">The section; a value outside the enum throws.</param>
        /// <returns>
        /// The title in the interface language, for example "The format to send" ("Formatul de
        /// trimis").
        /// </returns>
        internal static string Title(Topic t) => t switch
        {
            Topic.Format => Tr.Pick("The format to send", "Formatul de trimis"),
            Topic.Background => Tr.Pick("The background and the paper", "Fundalul si hartia"),
            Topic.Colour => Tr.Pick("The background colour and the gradients", "Culoarea fundalului si gradientele"),
            Topic.Size => Tr.Pick("The size of the print", "Marimea printului"),
            Topic.Denoise => Tr.Pick("Denoise for the print version", "Denoise pentru versiunea de print"),
            Topic.Warnings => Tr.Pick("Warnings", "Avertismente"),
            _ => throw new ArgumentOutOfRangeException(nameof(t)),
        };
    }
}
