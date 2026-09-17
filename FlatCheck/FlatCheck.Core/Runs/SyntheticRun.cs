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
        /// Completeaza cererea din cadrul de referinta: focala si diafragma date bat pe
        /// cele din fisier, crop-ul dat bate baza de camere. Fiecare valoare isi tine sursa,
        /// ca raportul sa spuna de unde a venit. Null cand totul s-a putut completa; altfel
        /// refuzul.
        /// </summary>
        internal static string Complete(SyntheticRequest req, LoadedFrame f, ProfileSources sources,
                                        double? focal, double? fstop, double? crop, bool lensfunNeedsCrop)
        {
            if (focal.HasValue) { req.FocalMm = focal.Value; req.FocalSource = "--focal"; }
            else if (f.FocalMm.HasValue) { req.FocalMm = f.FocalMm.Value; req.FocalSource = f.Header.Count > 0 ? "FOCALLEN" : "raw (libraw)"; }
            else return SyntheticReport.NoFocal();

            if (fstop.HasValue) { req.FStop = fstop.Value; req.FStopSource = "--fstop"; }
            else if (f.FNumber.HasValue) { req.FStop = f.FNumber.Value; req.FStopSource = "raw (libraw)"; }
            else return SyntheticReport.NoFStop();

            if (crop.HasValue) { req.CameraCrop = crop.Value; req.CropSource = "--crop"; }
            else
            {
                double? known = sources.Lensfun.CropOf(f.Camera);
                if (known.HasValue) { req.CameraCrop = known.Value; req.CropSource = $"baza lensfun, {f.Camera}"; }
                else if (lensfunNeedsCrop) return SyntheticReport.NoCrop(f.Camera);
                else { req.CameraCrop = 0; req.CropSource = "nefolosit (profil Adobe)"; }
            }
            return null;
        }

        /// <summary>
        /// Alege obiectivul din text: exact un rezultat, altfel refuz cu lista. Un ID de
        /// obiectiv Nikon din raw restrange lista cand textul e gol - si, fiindca ID-ul nu e
        /// unic, gama din raw (11-20 mm f/2.8) scoate obiectivele al caror nume o contrazice.
        /// </summary>
        internal static string ChooseLens(SyntheticRequest req, ProfileSources sources, LoadedFrame f)
        {
            LensCatalog cat = sources.Catalog;
            IReadOnlyList<LensChoice> hits;
            if (string.IsNullOrWhiteSpace(req.LensText))
            {
                hits = CandidatesFromRaw(cat, f);
                if (req.Source.HasValue) hits = hits.Where(h => h.Source == req.Source.Value).ToList();
                if (hits.Count == 0) return SyntheticReport.NoLensGiven();
            }
            else hits = cat.Search(req.LensText, req.Source);

            if (hits.Count == 0) return SyntheticReport.NoMatch(req.LensText, sources);
            // O potrivire exacta pe nume castiga in fata celor partiale: "Tokina AT-X 11-20mm F2.8 PRO DX"
            // e continut si de numele variantei "II", daca exista.
            var exact = hits.Where(h => string.Equals(h.Name, req.LensText?.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
            if (exact.Count > 0) hits = exact;
            // Acelasi obiectiv exista pe mai multe monturi (Adobe: un fisier per montura). Cand
            // cadrul de referinta spune ce aparat e, montura lui castiga: "Nikon D3300" alege
            // [NIKON CORPORATION] in fata lui [Canon]. Un cuvant din numele aparatului, nu o
            // tabela de monturi - nu se ghiceste nimic ce nu scrie in fisiere.
            string makerWord = f?.Camera?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (hits.Count > 1 && !string.IsNullOrEmpty(makerWord))
            {
                var byMount = hits.Where(h => (h.Mount + " " + h.Maker).Contains(makerWord, StringComparison.OrdinalIgnoreCase)).ToList();
                if (byMount.Count > 0) hits = byMount;
            }
            if (hits.Count > 1) return SyntheticReport.Ambiguous(req.LensText ?? SyntheticReport.RawLensLabel(f), hits);
            req.Lens = hits[0];
            return null;
        }

        /// <summary>
        /// Obiectivele pe care raw-ul le indica singur: cele cu ID-ul lui Nikon (in fisierele
        /// monturii aparatului), minus cele al caror nume contrazice gama din MakerNote - si,
        /// fiindca doar profilele Adobe poarta ID-uri Nikon, obiectivele lensfun ale acelorasi
        /// producatori a caror gama nu contrazice raw-ul. Fara ele lista restransa arata doar
        /// jumatatea Adobe, si utilizatorul a intrebat unde a disparut Tokina din lensfun.
        /// Goala fara ID. Consola si fereastra pornesc de aici, deci aleg la fel.
        /// </summary>
        internal static IReadOnlyList<LensChoice> CandidatesFromRaw(LensCatalog cat, LoadedFrame f)
        {
            if (f?.NikonLensId == null) return Array.Empty<LensChoice>();
            string hint = f.Camera?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            var byId = LensCatalog.Narrow(cat.ByLensId(f.NikonLensId.Value, hint), f.LensMinFocalMm, f.LensMaxFocalMm, f.LensMaxAperture);
            if (byId.Count == 0) return byId;
            var makers = new HashSet<string>(byId.Select(l => l.Maker), StringComparer.OrdinalIgnoreCase);
            var lensfun = cat.Lenses.Where(l => l.Source == ProfileSource.Lensfun && makers.Contains(l.Maker)).ToList();
            return byId.Concat(LensCatalog.Narrow(lensfun, f.LensMinFocalMm, f.LensMaxFocalMm, f.LensMaxAperture)).ToList();
        }

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
            // Fereastra da obiectivul gata ales din lista (req.Lens); consola il cauta din text.
            if (refusal == null && req.Lens == null)
            {
                refusal = ChooseLens(req, sources, f);
                choice = refusal != null;
            }
            refusal ??= Complete(req, f, sources, focal, fstop, crop, req.Lens.Source == ProfileSource.Lensfun);
            if (refusal != null)
            {
                report.WriteLine();
                report.WriteLine(refusal);
                return new SyntheticRunOutcome(null, req, f, refusal, choice);
            }
            LensProfile profile = LensCatalog.Nearest(sources.Catalog.ProfilesOf(req.Lens), req.FocalMm, req.FStop);
            var r = SyntheticFlatBuilder.Build(profile, f.Image.W, f.Image.H, req.CameraCrop);
            SyntheticReport.Write(r, req, referencePath, report);
            return new SyntheticRunOutcome(r, req, f, null);
        }

        internal static IReadOnlyList<string> History(SyntheticRunOutcome o, string referencePath)
        {
            LensProfile p = o.Result.Profile;
            return new[]
            {
                $"{VersionInfo.Banner} --synthetic: flat sintetic din profilul de vignetare, sursa {p.Lens.SourceLabel}",
                $"obiectiv: {p.Lens.Name} [{p.Lens.Mount}]; profil: {p.Settings}; cerut: {SyntheticReport.Requested(o.Request)}",
                $"model: {p.Model.Describe()}",
                $"geometrie si carduri din: {Path.GetFileName(referencePath)}",
            };
        }

        /// <summary>Float mereu: flatul e un factor in jurul lui 1, nu ADU.</summary>
        internal static void WriteFits(SyntheticRunOutcome o, string referencePath, Stream s)
            => FitsWriter.Write(s, o.Result.Flat, -32, o.Frame.Cards, History(o, referencePath));
    }
}
