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

// Recommendation.cs - o recomandare: despre ce, cat de grav, ce sa faci, si DE CE.
//
// "Because" nu e optional. O recomandare fara cifra si fara prag nu se poate verifica,
// si unealta asta exista tocmai ca sa se poata verifica. Fereastra o arata ca text mic
// sub recomandare; consola ca "fiindca:".

using System;

namespace AstroPhotoAnalyzer
{
    /// <summary>Monotone: Ok &lt; Atentie &lt; Problema. Verdictul general e maximul.</summary>
    internal enum Verdict { Ok = 0, Attention = 1, Problem = 2 }

    /// <summary>Sectiunile raportului, in ordinea deciziilor - nu a masuratorilor.</summary>
    internal enum Topic
    {
        Format = 1,
        Background = 2,
        Colour = 3,
        Size = 4,
        Denoise = 5,
        Warnings = 6,
    }

    internal sealed record Recommendation
    {
        internal Topic Topic { get; init; }
        internal Verdict Verdict { get; init; }

        /// <summary>O propozitie: ce sa faci.</summary>
        internal string Text { get; init; }

        /// <summary>Metrica si pragul: de ce.</summary>
        internal string Because { get; init; }

        internal Recommendation(Topic topic, Verdict verdict, string text, string because)
        {
            if (string.IsNullOrWhiteSpace(because))
                throw new ArgumentException("o recomandare fara 'fiindca' nu e o recomandare");
            Topic = topic; Verdict = verdict; Text = text; Because = because;
        }
    }

    internal static class VerdictText
    {
        internal static string Label(Verdict v) => v switch
        {
            Verdict.Ok => "OK",
            Verdict.Attention => "ATENTIE",
            Verdict.Problem => "PROBLEMA",
            _ => throw new ArgumentOutOfRangeException(nameof(v)),
        };

        internal static string Title(Topic t) => t switch
        {
            Topic.Format => "Formatul de trimis",
            Topic.Background => "Fundalul si hartia",
            Topic.Colour => "Culoarea fundalului si gradientele",
            Topic.Size => "Marimea printului",
            Topic.Denoise => "Denoise pentru versiunea de print",
            Topic.Warnings => "Avertismente",
            _ => throw new ArgumentOutOfRangeException(nameof(t)),
        };
    }
}
