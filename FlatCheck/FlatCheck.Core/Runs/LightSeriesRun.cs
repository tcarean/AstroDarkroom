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

// LightSeriesRun.cs - modul 3 pe toata seria dintr-un folder, cap-coada.
//
// Dintre cele cinci rulari, asta e singura care chiar avea ce duplica: e o bucla, cu
// stare, cu progres si cu o optimizare de memorie care nu se vede din semnatura. Scrisa
// a doua oara in aplicatia WPF, ar fi divergat sigur - macar prin faptul ca a doua
// versiune ar fi uitat sa elibereze primul cadru.
//
// Doua iesiri, nu una, si de-asta functioneaza pentru amandoi apelantii:
//   - randurile se scriu in TextWriter pe masura ce cadrele sunt gata (consola nu tace
//     doua minute), si
//   - acelasi cadru pleaca si prin onFrame, ca fereastra sa-si umple tabelul progresiv.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace FlatCheck
{
    internal sealed class LightSeriesRunOutcome
    {
        internal readonly SuitabilityResult Suitability;

        /// <summary>Sinteza pe serie, sau null cand testul nu se aplica.</summary>
        internal readonly LightSeriesResult Result;

        internal readonly byte[] Png;

        /// <summary>Fisierele care au intrat in rulare, in ordinea prelucrarii.</summary>
        internal readonly IReadOnlyList<string> Files;

        internal bool Refused => Result == null;

        internal LightSeriesRunOutcome(
            SuitabilityResult s, LightSeriesResult r, byte[] png, IReadOnlyList<string> files)
        {
            Suitability = s;
            Result = r;
            Png = png;
            Files = files;
        }
    }

    internal static class LightSeriesRun
    {
        /// <param name="onFrame">
        /// Chemat dupa fiecare cadru, cu indexul (de la 0), totalul si rezultatul. Pentru
        /// consola e null - ei ii ajunge randul scris in report. Pentru fereastra e cum
        /// se umple tabelul si cum avanseaza bara de progres.
        /// </param>
        /// <param name="ct">
        /// Se verifica INTRE cadre, nu in interiorul unuia: analizoarele sunt pure si
        /// sincrone, iar un cadru dureaza sub o secunda. A le presara cu verificari ar
        /// fi murdarit codul care conteaza pentru o jumatate de secunda de raspuns.
        /// </param>
        internal static LightSeriesRunOutcome Execute(
            string dir,
            string pattern,
            double? fov,
            TextWriter report,
            Action<int, int, string, LightResult> onFrame = null,
            CancellationToken ct = default)
        {
            var files = SeriesFinder.Find(dir, pattern);
            if (files.Count == 0)
                throw new Exception(
                    $"Niciun fisier care sa se potriveasca cu '{pattern}' in {dir}. "
                    + "Da alt tipar cu --pattern.");

            // Aplicabilitatea se decide pe primul cadru: toate vin din aceeasi sesiune,
            // deci au aceeasi focala si aceeasi scara.
            LoadedFrame first = ImageLoader.Load(files[0]);
            var s = LightSuitability.Check(first.Header, fov);
            if (!s.CanRun)
            {
                LightReport.WriteTooWide(s, dir, report);
                return new LightSeriesRunOutcome(s, null, null, files);
            }

            LightSeriesReport.WriteHeader(dir, pattern, files.Count, s, report);

            // Cadrele se incarca pe rand si NU se pastreaza: un cadru de 24 MP ocupa
            // sute de MB, deci 32 tinute simultan ar fi de neexecutat. Primul e deja
            // incarcat pentru garda de mai sus, si se elibereaza imediat ce a fost
            // folosit - de aici "first = null", care altfel ar parea gratuit.
            var frames = new List<(string, LightResult)>();
            for (int i = 0; i < files.Count; i++)
            {
                ct.ThrowIfCancellationRequested();

                Img img = first?.Image ?? ImageLoader.LoadImage(files[i]);
                first = null;

                var r = LightAnalyzer.Analyze(img);
                string name = Path.GetFileName(files[i]);
                LightSeriesReport.WriteRow(name, r, report);
                frames.Add((name, r));
                onFrame?.Invoke(i, files.Count, name, r);
            }

            var series = LightSeriesAnalyzer.Combine(frames);
            LightSeriesReport.WriteSummary(series, report);
            return new LightSeriesRunOutcome(
                s, series, PngWriter.EncodeResidualMap(series.RadialMap), files);
        }
    }
}
