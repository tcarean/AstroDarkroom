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

// SplitRun.cs - masterele partilor langa masterul intregii sesiuni: N + 1 rulari de
// structura, un tabel, un raport, un mozaic de harti reziduale.
//
// Nu stie nimic despre foldere temporare sau despre Siril: primeste cai de fisiere.
// Fereastra face impartirea si rularile; consola ar putea da aceleasi cai cu un flag,
// daca va fi nevoie (ADR 0054). Cifrele fiecarei coloane sunt exact cele ale rularii de
// structura pe fisierul ei - StructureRun.Analyze, aceeasi functie.
//
// PROFILUL DE REFERINTA e cel mai apropiat profil al INTREGULUI, acelasi pe toate
// coloanele. "Cel mai apropiat" se alege pe fiecare master in parte, si pe prima sesiune
// reala partea 1 si intregul au ales profile diferite (Adobe +0.07 fata de lensfun -0.08);
// un tabel cu alta referinta pe fiecare coloana n-ar compara nimic.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FlatCheck
{
    /// <summary>O coloana a tabelului: o parte sau intregul, cu rularea ei de structura.</summary>
    internal sealed class SplitColumn
    {
        internal readonly string Path;
        /// <summary>"partea 1 din 4" sau "intreg" - textul din antetul coloanei si din raport.</summary>
        internal readonly string Label;
        internal readonly StructureOutcome Outcome;
        /// <summary>Adancimea acestui master fata de profilul de referinta; null fara referinta.</summary>
        internal readonly ProfileDepth Reference;
        /// <summary>
        /// Verdictul coloanei: cel mai rau dintre cele cinci teste si adancimea fata de
        /// PROFILUL DE REFERINTA - nu verdictul rularii (Outcome.Verdict), care ia adancimea
        /// fata de profilul cel mai apropiat al coloanei insesi; in tabel randul de mai sus e
        /// fata de referinta, si cele doua randuri trebuie sa vorbeasca despre acelasi profil.
        /// Fara referinta, verdictul celor cinci teste.
        /// </summary>
        internal readonly string Verdict;

        internal SplitColumn(string path, string label, StructureOutcome outcome, ProfileDepth reference)
        {
            Path = path;
            Label = label;
            Outcome = outcome;
            Reference = reference;
            Verdict = reference != null && Stats.Severity(reference.Verdict) > Stats.Severity(outcome.Structure.Verdict)
                ? reference.Verdict : outcome.Structure.Verdict;
        }
    }

    internal sealed class SplitResult
    {
        internal readonly IReadOnlyList<SplitColumn> Parts;
        internal readonly SplitColumn Whole;
        /// <summary>Cel mai apropiat profil al intregului, sau null cand intregul nu s-a putut compara.</summary>
        internal readonly LensProfile ReferenceProfile;
        /// <summary>Adancimea profilului de referinta, masurata cu operatorul masterului; null fara referinta.</summary>
        internal readonly ProfileDepth ReferenceDepth;
        /// <summary>Toate profilele cu care s-a comparat intregul, in ordinea tabului Structura; gol fara comparatie.</summary>
        internal readonly IReadOnlyList<ProfileDepth> Profiles;
        internal readonly int FramesPerPart;
        internal readonly DateTime When;
        /// <summary>Scara comuna a mozaicului: cea automata peste toate hartile deodata.</summary>
        internal readonly PngWriter.MapRange MapRange;

        internal SplitResult(IReadOnlyList<SplitColumn> parts, SplitColumn whole, LensProfile referenceProfile,
                             ProfileDepth referenceDepth, int framesPerPart, DateTime when, PngWriter.MapRange mapRange)
        {
            Parts = parts;
            Whole = whole;
            ReferenceProfile = referenceProfile;
            ReferenceDepth = referenceDepth;
            Profiles = whole.Outcome.Compared ? whole.Outcome.Profiles.Profiles : Array.Empty<ProfileDepth>();
            FramesPerPart = framesPerPart;
            When = when;
            MapRange = mapRange;
        }

        internal bool HasReference => ReferenceProfile != null;

        /// <summary>Partile in ordine, apoi intregul - ordinea coloanelor si a dalelor.</summary>
        internal IEnumerable<SplitColumn> Columns => Parts.Append(Whole);
    }

    internal static class SplitRun
    {
        /// <param name="wholePath">Masterul intregii sesiuni.</param>
        /// <param name="partPaths">Masterele partilor, in ordinea partilor.</param>
        /// <param name="query">Profilele si raw-ul de langa master, ca la structura; null = fara comparatie.</param>
        /// <param name="when">Data si ora rularii, scrise in raport si in titlul mozaicului.</param>
        /// <param name="framesPerPart">Cate cadre a avut fiecare parte; 0 cand nu se stie.</param>
        /// <param name="report">Where the report text goes.</param>
        internal static RunOutcome<SplitResult> Execute(string wholePath, IReadOnlyList<string> partPaths, StructureQuery query,
                                                        DateTime when, int framesPerPart, TextWriter report)
        {
            SplitResult r = Analyze(wholePath, partPaths, query, when, framesPerPart);
            SplitReport.Write(r, wholePath, report);

            var tiles = new List<(Img, string)>();
            for (int i = 0; i < r.Parts.Count; i++)
                tiles.Add((r.Parts[i].Outcome.Structure.Residual, SplitReport.PngLabel(i + 1, r.Parts.Count)));
            tiles.Add((r.Whole.Outcome.Structure.Residual, SplitReport.PngLabelWhole));

            return new RunOutcome<SplitResult>(r, PngWriter.EncodeMosaic(tiles, r.MapRange, SplitReport.PngTitle(when, r.MapRange)));
        }

        internal static SplitResult Analyze(string wholePath, IReadOnlyList<string> partPaths, StructureQuery query,
                                            DateTime when, int framesPerPart)
        {
            if (partPaths == null || partPaths.Count == 0) throw new ArgumentException("nicio parte", nameof(partPaths));

            StructureOutcome whole = StructureRun.Analyze(wholePath, query);
            var parts = new List<StructureOutcome>(partPaths.Count);
            foreach (string p in partPaths) parts.Add(StructureRun.Analyze(p, query));

            ProfileDepth referenceDepth = whole.Compared ? whole.Profiles.Nearest : null;
            LensProfile reference = referenceDepth?.Profile;

            var columns = new List<SplitColumn>(partPaths.Count);
            for (int i = 0; i < partPaths.Count; i++)
                columns.Add(new SplitColumn(partPaths[i], SplitReport.PartLabel(i + 1, partPaths.Count), parts[i], Against(parts[i], reference)));
            var wholeColumn = new SplitColumn(wholePath, SplitReport.WholeLabel, whole, referenceDepth);

            return new SplitResult(columns, wholeColumn, reference, referenceDepth, framesPerPart, when,
                                   SharedRange(columns.Append(wholeColumn)));
        }

        /// <summary>Adancimea unui master fata de un profil dat: randul aceluiasi obiectiv din comparatia lui; null cand nu e.</summary>
        internal static ProfileDepth Against(StructureOutcome o, LensProfile profile)
        {
            if (profile == null || o.Profiles == null) return null;
            return o.Profiles.Profiles.FirstOrDefault(d => d.Profile.Lens.Key == profile.Lens.Key && d.Profile.File == profile.File)
                ?? o.Profiles.Profiles.FirstOrDefault(d => d.Profile.Lens.Key == profile.Lens.Key);
        }

        /// <summary>Scara automata (percentilele 1/99 plus padding, R-006) peste toate reziduurile deodata.</summary>
        static PngWriter.MapRange SharedRange(IEnumerable<SplitColumn> columns)
        {
            var maps = columns.Select(c => c.Outcome.Structure.Residual).ToList();
            var all = new Img(1, maps.Sum(m => m.D.Length));
            int at = 0;
            foreach (Img m in maps)
            {
                Array.Copy(m.D, 0, all.D, at, m.D.Length);
                at += m.D.Length;
            }
            return PngWriter.AutoRange(all);
        }
    }
}
