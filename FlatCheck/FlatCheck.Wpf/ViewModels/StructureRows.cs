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

// StructureRows.cs - cele cinci randuri de test ale bateriei de structura, construite
// o singura data pentru amandoua taburile care le arata.
//
// DE CE EXISTA. Tabul Structura si tabul Ratio arata ACEEASI baterie - Ratio o ruleaza pe
// masterul combinat (A+B)/2, dar cifrele, etichetele si verdictele sunt aceleasi, fiindca
// e literalmente acelasi analizor. Pana acum fiecare tab si le compunea singur: Structura
// dintr-un ItemsControl peste MetricRow, Ratio din cinci blocuri de Grid scrise de mana in
// XAML, cu etichetele ca literale. Consecinta a fost exact ce era de asteptat - cand
// Structura a capatat descrieri sub fiecare rand, Ratio a ramas fara, si cand a aparut al
// cincilea test a trebuit adaugat in doua locuri.
//
// Deci randurile se construiesc aici, iar taburile doar le afiseaza. Textul tot nu se
// scrie aici: etichetele sunt ale randurilor, dar explicatiile vin din
// StructureReport.TestNote, in nucleu, ca sa fie aceleasi si daca maine le cere si consola.

using System.Collections.Generic;
using System.Linq;

namespace FlatCheck.Wpf
{
    internal static class StructureRows
    {
        /// <summary>
        /// Cele cinci teste de abatere, in ordinea din raportul de consola.
        ///
        /// Ordinea conteaza: raportul o are pe asta, iar cine compara ecranul cu un raport
        /// lipit pe un forum trebuie sa gaseasca randurile la fel. Testele o verifica.
        /// </summary>
        internal static IReadOnlyList<MetricRow> Tests(StructureResult r) => new[]
        {
            new MetricRow
            {
                Label = "imprastiere 4 colturi",
                Value = Pct(r.CornerSpreadPct),
                Verdict = r.CornerVerdict,
                // singurul rand cu cifre proprii sub el: cele patru colturi, prin acelasi
                // raport ca in raport
                Note = Note(nameof(r.CornerSpreadPct)) + "\n" + CornerNote(r),
            },
            new MetricRow
            {
                Label = "asimetrie azimutala max",
                Value = Pct(r.AzimuthalMaxPct),
                Verdict = r.AzimuthalVerdict,
                Note = Note(nameof(r.AzimuthalMaxPct)),
            },
            new MetricRow
            {
                Label = "reziduu dupa model radial",
                Value = Pct(r.ResidualPtpPct),
                Verdict = r.ResidualVerdict,
                IsKey = true,
                Note = Note(nameof(r.ResidualPtpPct)),
            },
            new MetricRow
            {
                Label = "inclinare (gradient)",
                Value = Pct(r.TiltPct),
                Verdict = r.TiltVerdict,
                Note = Note(nameof(r.TiltPct)),
            },
            new MetricRow
            {
                Label = "pata maxima (o dala)",
                Value = Pct(r.MaxTilePct),
                Verdict = r.MaxTileVerdict,
                Note = Note(nameof(r.MaxTilePct)),
            },
        };

        /// <summary>
        /// Marimile care NU sunt teste, in ordinea din raport: vignetarea, offsetul
        /// centrului de iluminare, petele locale. Fara verdict, deci fara pastila.
        ///
        /// Tot ce tipareste consola sub "INFORMATIV" in WriteBody, MINUS linia de niveluri
        /// ADU - aceea sta in antetul modului de structura, nu in corp, fiindca pe masterul
        /// combinat al modului Ratio (normalizat la mediana 1) n-ar insemna nimic. De asta
        /// nivelurile raman in StructureTabViewModel, iar randurile de aici merg in
        /// amandoua taburile.
        /// </summary>
        internal static IReadOnlyList<MetricRow> Informative(StructureResult r) => new[]
        {
            new MetricRow
            {
                Label = "vignetare centru -> colt",
                Value = $"{r.VignetteStops:F2} stopuri",
                Note = $"coltul e {r.CornerMean / r.CentreMedian * 100:F0} % din centru; "
                     + "e o proprietate a obiectivului, nu o eroare",
            },
            new MetricRow
            {
                Label = "offset centru iluminare",
                Value = r.CentroidValid ? Pct(r.CentroidOffsetPct) : "nedeterminat",
                Note = r.CentroidValid
                    ? $"varf ({r.FitCx:F0}, {r.FitCy:F0}) vs centru senzor ({r.SensorCx:F0}, {r.SensorCy:F0}); "
                      + "raport gradient/curbura, deci NU e scale-invariant - amplitudinea o da inclinarea"
                    : "varf in afara cadrului sau fit degenerat",
            },
            new MetricRow
            {
                Label = "pete locale (praf etc.)",
                Value = StructureReport.LocalSpotValue(r),
                // Explicatia vine din nucleu, ca sa fie aceeasi cu cea din raportul
                // salvat - inclusiv pragul, care se citeste din Th si nu se scrie aici.
                Note = StructureReport.LocalSpotNote().Replace('\n', ' '),
            },
        };

        /// <summary>
        /// Explicatia unui rand. Trece prin nucleu, ca sa nu existe o a doua formulare in
        /// fereastra; daca ar lipsi una, randul ar ramane fara descriere, ceea ce s-a si
        /// intamplat cu asimetria azimutala si cu inclinarea.
        /// </summary>
        static string Note(string metric) => StructureReport.TestNote(metric);

        /// <summary>Cele patru colturi, ca procent din media lor - la fel ca in raport.</summary>
        static string CornerNote(StructureResult r)
            => string.Join("   ", r.Corners.Select(
                c => $"{c.Key} {c.Value / r.CornerMean * 100:F1} %"));

        /// <summary>NaN se scrie "n/a", nu "NaN %": e un input degenerat, nu o masuratoare.</summary>
        internal static string Pct(double v) => double.IsNaN(v) ? "n/a" : $"{v:F2} %";
    }
}
