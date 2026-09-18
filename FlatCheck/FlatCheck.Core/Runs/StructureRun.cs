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

// StructureRun.cs - modul 1 cap-coada: fisier -> cifre + text + harta.
//
// Cu o StructureQuery, si comparatia vignetarii cu profilele obiectivului: identificarea
// (LensIdentification, din master si din raw-ul de langa el sau din --lens), comparatia
// (VignettingComparer), si verdictul combinat - cel mai rau dintre cele cinci teste si
// adancime, ca la --against. Fara query, exact ce era: cele cinci teste si verdictul lor,
// deci golden-urile si crosscheck.py nu se misca.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FlatCheck
{
    /// <summary>
    /// Ce mai vrea apelantul pe langa structura: profilele cu care sa compare vignetarea.
    /// Consola o construieste din --lens / --raw, fereastra din raw-ul din flats/.
    /// </summary>
    internal sealed class StructureQuery
    {
        internal ProfileSources Sources;
        /// <summary>Un raw de langa master, pentru diafragma si ID-ul de obiectiv; null cand nu e.</summary>
        internal string RawPath;
        /// <summary>Cititorul de metadate; in teste, un cadru fabricat in locul unui raw.</summary>
        internal Func<string, LoadedFrame> LoadInfo = ImageLoader.LoadInfo;
        /// <summary>Obiectivele dupa text (--lens), in locul celor indicate de raw; null = din raw.</summary>
        internal string LensText;
        internal ProfileSource? Source;
        internal double? Focal, FStop, Crop;
    }

    internal sealed class StructureOutcome
    {
        internal readonly StructureResult Structure;
        /// <summary>Null fara query; altfel comparatia, chiar si cand n-a putut compara (Gap spune de ce).</summary>
        internal readonly VignettingComparison Profiles;
        internal readonly ShotIdentity Shot;
        /// <summary>Raw-ul de langa master n-a putut fi citit: mesajul, sau null.</summary>
        internal readonly string RawError;
        /// <summary>Cel mai rau dintre verdictul celor cinci teste si cel al adancimii; al structurii cand nu s-a comparat.</summary>
        internal readonly string Verdict;

        internal StructureOutcome(StructureResult structure, VignettingComparison profiles, ShotIdentity shot, string rawError)
        {
            Structure = structure;
            Profiles = profiles;
            Shot = shot;
            RawError = rawError;
            Verdict = profiles != null && profiles.Compared && Stats.Severity(profiles.Verdict) > Stats.Severity(structure.Verdict)
                ? profiles.Verdict : structure.Verdict;
        }

        internal bool Compared => Profiles != null && Profiles.Compared;

        /// <summary>Adevarat cand adancimea a inrautatit verdictul: sfatul e atunci al ei, nu al formei.</summary>
        internal bool DepthCarriesTheVerdict
            => Compared && Stats.Severity(Profiles.Verdict) > Stats.Severity(Structure.Verdict);
    }

    internal static class StructureRun
    {
        /// <param name="report">
        /// Unde se scrie raportul. Consola da stdout, fereastra da un StringWriter din
        /// care apoi salveaza "Rezultat testare.txt" - acelasi text, prin constructie.
        /// </param>
        /// <param name="query">Null = doar structura; altfel si comparatia cu profilele.</param>
        internal static RunOutcome<StructureOutcome> Execute(string path, TextWriter report, StructureQuery query = null)
        {
            LoadedFrame f = ImageLoader.Load(path);
            var r = StructureAnalyzer.Analyze(f.Image);
            StructureOutcome o = query == null ? new StructureOutcome(r, null, null, null) : Compare(r, f, query);
            StructureReport.Write(o, path, report);
            return new RunOutcome<StructureOutcome>(o, PngWriter.EncodeResidualMap(r.Residual));
        }

        /// <summary>
        /// Comparatia pe un rezultat de structura gata calculat: identifica, alege obiectivele,
        /// compara. Separat de Execute ca fereastra sa poata compara fara sa reincarce masterul.
        /// </summary>
        internal static StructureOutcome Compare(StructureResult r, LoadedFrame reference, StructureQuery query)
        {
            LoadedFrame raw = null;
            string rawError = null;
            if (query.RawPath != null)
            {
                try { raw = query.LoadInfo(query.RawPath); }
                catch (Exception e) { rawError = $"{Path.GetFileName(query.RawPath)} nu s-a putut citi: {e.Message}"; }
            }
            ShotIdentity shot = LensIdentification.Identify(reference, raw, query.Sources, query.Focal, query.FStop, query.Crop,
                                                            LensIdentification.NameOf(query.RawPath));
            IReadOnlyList<LensChoice> lenses = string.IsNullOrWhiteSpace(query.LensText)
                ? (query.Source.HasValue ? shot.Candidates.Where(c => c.Source == query.Source.Value).ToList() : shot.Candidates)
                : SyntheticRun.MatchLenses(query.LensText, query.Source, query.Sources, shot);
            var cmp = VignettingComparer.Analyze(r, shot, query.Sources?.Catalog, lenses);
            return new StructureOutcome(r, cmp, shot, rawError);
        }
    }
}
