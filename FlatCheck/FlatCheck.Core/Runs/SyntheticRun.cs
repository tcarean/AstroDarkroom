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

// Lantul flatului sintetic: cadrul de referinta (marime, carduri, ce stie despre
// fotografiere) -> cererea completata (focala, diafragma, crop, fiecare cu sursa ei) ->
// alegerea obiectivului si a profilului -> raster -> raport. Fisierul il scrie apelantul,
// prin WriteFits, dupa ce a raspuns la intrebarea de suprascriere - acelasi motiv ca la
// --smooth. Refuzurile sunt text, ca la celelalte run-uri, si nu arunca.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FlatCheck
{
    /// <summary>Ce s-a cerut, cu ce s-a completat din cadrul de referinta si de unde.</summary>
    internal sealed class SyntheticRequest
    {
        internal string LensText;
        internal ProfileSource? Source;
        internal LensChoice Lens;
        internal double FocalMm;
        internal string FocalSource;
        internal double FStop;
        internal string FStopSource;
        internal double CameraCrop;
        internal string CropSource;
    }

    internal sealed class SyntheticRunOutcome
    {
        internal readonly SyntheticFlatResult Result;
        internal readonly SyntheticRequest Request;
        internal readonly LoadedFrame Frame;
        internal readonly string Refusal;
        /// <summary>Refuzul e despre ALEGEREA obiectivului (nimic / prea multe): "citeste lista", cod 2, nu 1.</summary>
        internal readonly bool RefusedChoice;
        internal bool Refused => Result == null;

        internal SyntheticRunOutcome(SyntheticFlatResult r, SyntheticRequest req, LoadedFrame f, string refusal, bool refusedChoice = false)
        {
            Result = r;
            Request = req;
            Frame = f;
            Refusal = refusal;
            RefusedChoice = refusedChoice;
        }
    }

    internal static class SyntheticRun
    {
        /// <summary>
        /// Completeaza cererea din identitatea fotografierii: fiecare valoare cu sursa ei,
        /// ca raportul sa spuna de unde a venit. Null cand totul s-a putut completa; altfel
        /// refuzul. Identificarea insasi e in LensIdentification, comuna cu fereastra.
        /// </summary>
        internal static string Complete(SyntheticRequest req, ShotIdentity id, bool lensfunNeedsCrop)
        {
            if (!id.FocalMm.HasValue) return SyntheticReport.NoFocal();
            req.FocalMm = id.FocalMm.Value;
            req.FocalSource = id.FocalSource;

            if (!id.FStop.HasValue) return SyntheticReport.NoFStop();
            req.FStop = id.FStop.Value;
            req.FStopSource = id.FStopSource;

            if (id.Crop.HasValue) { req.CameraCrop = id.Crop.Value; req.CropSource = id.CropSource; }
            else if (lensfunNeedsCrop) return SyntheticReport.NoCrop(id.Camera);
            else { req.CameraCrop = 0; req.CropSource = id.CropSource; }
            return null;
        }

        /// <summary>Pentru teste si pentru apelantii care n-au identificat inca: identifica si completeaza.</summary>
        internal static string Complete(SyntheticRequest req, LoadedFrame f, ProfileSources sources,
                                        double? focal, double? fstop, double? crop, bool lensfunNeedsCrop)
            => Complete(req, LensIdentification.Identify(f, null, sources, focal, fstop, crop), lensfunNeedsCrop);

        /// <summary>
        /// Alege obiectivul din text: exact un rezultat, altfel refuz cu lista. Fara text,
        /// candidatii pe care raw-ul i-a indicat (identitatea), restransi dupa sursa ceruta.
        /// </summary>
        internal static string ChooseLens(SyntheticRequest req, ProfileSources sources, LoadedFrame f, ShotIdentity id)
        {
            LensCatalog cat = sources.Catalog;
            IReadOnlyList<LensChoice> hits;
            if (string.IsNullOrWhiteSpace(req.LensText))
            {
                hits = id.Candidates;
                if (req.Source.HasValue) hits = hits.Where(h => h.Source == req.Source.Value).ToList();
                if (hits.Count == 0) return SyntheticReport.NoLensGiven();
            }
            else
            {
                hits = MatchLenses(req.LensText, req.Source, sources, id);
                if (hits.Count == 0) return SyntheticReport.NoMatch(req.LensText, sources);
            }
            if (hits.Count > 1) return SyntheticReport.Ambiguous(req.LensText ?? id.RawLensLabel, hits);
            req.Lens = hits[0];
            return null;
        }

        /// <summary>
        /// Obiectivele care se potrivesc cu un text, restranse cum le restrange si alegerea:
        /// potrivirea exacta pe nume bate pe cele partiale ("Tokina AT-X 11-20mm F2.8 PRO DX"
        /// e continut si de numele variantei "II"), montura aparatului cand se stie, apoi
        /// fisierul propriu al corpului. Fara regula "exact unul": --synthetic o cere,
        /// comparatia din modul structura compara cu toate cate raman.
        /// </summary>
        internal static IReadOnlyList<LensChoice> MatchLenses(string text, ProfileSource? source, ProfileSources sources, ShotIdentity id)
        {
            IReadOnlyList<LensChoice> hits = sources.Catalog.Search(text, source);
            if (hits.Count == 0) return hits;
            var exact = hits.Where(h => string.Equals(h.Name, text.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
            if (exact.Count > 0) hits = exact;
            string makerWord = id?.Camera?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (hits.Count > 1 && !string.IsNullOrEmpty(makerWord))
            {
                var byMount = hits.Where(h => (h.Mount + " " + h.Maker).Contains(makerWord, StringComparison.OrdinalIgnoreCase)).ToList();
                if (byMount.Count > 0) hits = byMount;
            }
            return LensIdentification.PreferCamera(hits, id?.Camera);
        }

        /// <summary>Pentru teste: identifica intai, apoi alege.</summary>
        internal static string ChooseLens(SyntheticRequest req, ProfileSources sources, LoadedFrame f)
            => ChooseLens(req, sources, f, LensIdentification.Identify(f, null, sources));

        internal static SyntheticRunOutcome Execute(string referencePath, SyntheticRequest req, ProfileSources sources,
                                                    double? focal, double? fstop, double? crop, TextWriter report)
        {
            LoadedFrame f = ImageLoader.Load(referencePath);
            return Execute(f, referencePath, req, sources, focal, fstop, crop, report);
        }

        internal static SyntheticRunOutcome Execute(LoadedFrame f, string referencePath, SyntheticRequest req, ProfileSources sources,
                                                    double? focal, double? fstop, double? crop, TextWriter report)
        {
            string refusal = f.Planes > 1 ? SyntheticReport.RefusalCube(referencePath, f.Planes) : null;
            bool choice = false;
            // O singura identificare pentru alegere si completare: ByLensId parseaza fisierele
            // Adobe ale monturii, si nu e ceva de facut de doua ori.
            ShotIdentity id = LensIdentification.Identify(f, null, sources, focal, fstop, crop);
            // Fereastra da obiectivul gata ales din lista (req.Lens); consola il cauta din text.
            if (refusal == null && req.Lens == null)
            {
                refusal = ChooseLens(req, sources, f, id);
                choice = refusal != null;
            }
            refusal ??= Complete(req, id, req.Lens.Source == ProfileSource.Lensfun);
            if (refusal != null)
            {
                report.WriteLine();
                report.WriteLine(refusal);
                return new SyntheticRunOutcome(null, req, f, refusal, choice);
            }
            IReadOnlyList<LensProfile> all = sources.Catalog.ProfilesOf(req.Lens);
            LensProfile profile = LensCatalog.Nearest(all, req.FocalMm, req.FStop);
            var r = SyntheticFlatBuilder.Build(profile, f.Image.W, f.Image.H, req.CameraCrop);
            r.LensProfiles = all;
            SyntheticReport.Write(r, req, referencePath, report);
            return new SyntheticRunOutcome(r, req, f, null);
        }

        internal static IReadOnlyList<string> History(SyntheticRunOutcome o, string referencePath)
        {
            LensProfile p = o.Result.Profile;
            return new[]
            {
                $"{VersionInfo.Banner} --synthetic: flat sintetic din profilul de vignetare, sursa {p.Lens.SourceLabel}",
                $"obiectiv: {p.Lens.Name} [{p.Lens.Where}]; profil: {p.Settings}; cerut: {SyntheticReport.Requested(o.Request)}",
                $"model: {p.Model.Describe()}",
                $"geometrie si carduri din: {Path.GetFileName(referencePath)}",
            };
        }

        /// <summary>Float mereu: flatul e un factor in jurul lui 1, nu ADU.</summary>
        internal static void WriteFits(SyntheticRunOutcome o, string referencePath, Stream s)
            => FitsWriter.Write(s, o.Result.Flat, -32, o.Frame.Cards, History(o, referencePath));
    }
}
