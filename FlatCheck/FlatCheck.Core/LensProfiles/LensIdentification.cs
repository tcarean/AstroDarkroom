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

// Ce se stie despre fotografiere - focala, diafragma, crop-ul aparatului, obiectivul -
// adunat din cadrul de referinta (un master FITS, sau un raw dat consolei) si dintr-un raw
// de langa el (unul din flats/, in fereastra), cu SURSA fiecarei valori, si obiectivele
// din catalog pe care raw-ul le indica singur.
//
// Exista fiindca regula asta traia intr-un view-model (preumplerea tabului Sintetic) si
// pe jumatate in SyntheticRun.Complete, iar comparatia vignetarii de pe tabul Structura
// avea nevoie exact de aceeasi identificare. Doua copii ar fi ales doua obiective diferite
// pentru acelasi folder. Acum consola, tabul Sintetic si tabul Structura pornesc de aici.
//
// Precedenta, aceeasi ca inainte: ce e dat de om bate ce e citit din fisiere; la focala
// cadrul de referinta bate raw-ul de langa el (FOCALLEN e ce a scris Siril pentru chiar
// masterul asta); diafragma o da doar un raw (Siril nu scrie f-numarul); crop-ul vine din
// tabelul de aparate lensfun, dupa INSTRUME sau dupa ce spune raw-ul. Nimic nu se ghiceste:
// o valoare care lipseste ramane null, cu motivul in sursa ei, si cine o cere refuza sau
// explica. Nu se citeste niciun pixel: ambele cadre pot veni din ImageLoader.LoadInfo.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FlatCheck
{
    /// <summary>Identitatea fotografierii: valorile, sursele lor, si obiectivele candidate.</summary>
    internal sealed class ShotIdentity
    {
        internal double? FocalMm;
        /// <summary>"--focal", "FOCALLEN", "raw (libraw)" pentru un raw de referinta, "raw (DSC_0001.NEF)" pentru unul de langa.</summary>
        internal string FocalSource;
        internal double? FStop;
        internal string FStopSource;
        internal double? Crop;
        /// <summary>"--crop", "baza lensfun, Nikon D3300", sau de ce nu se stie.</summary>
        internal string CropSource;
        /// <summary>Adevarat cand crop-ul a iesit din tabelul de aparate, nu din om.</summary>
        internal bool CropFromTable;
        /// <summary>Numele aparatului, din referinta sau din raw; null cand niciunul nu-l spune.</summary>
        internal string Camera;
        /// <summary>Raw-ul din care se citeste obiectivul (referinta insasi, sau cel de langa), sau null.</summary>
        internal LoadedFrame Raw;
        /// <summary>Numele fisierului raw de langa referinta, pentru note; null cand nu e unul.</summary>
        internal string RawName;
        /// <summary>Obiectivele pe care raw-ul le indica: ID-ul Nikon restrans pe gama si pe montura. Goala fara raw sau fara ID.</summary>
        internal IReadOnlyList<LensChoice> Candidates = Array.Empty<LensChoice>();
        /// <summary>De unde a venit fiecare valoare, in cuvinte: "focala din FOCALLEN", "diafragma din DSC_0001.NEF".</summary>
        internal IReadOnlyList<string> Notes = Array.Empty<string>();

        /// <summary>
        /// Adevarat cand candidatii NU vin din ID-ul de obiectiv, ci doar din gama raw-ului
        /// (focala minima / maxima, deschiderea): obiectivele monturii al caror nume spune
        /// exact gama aceea. E cazul unui obiectiv manual - Samyang 135 f/2 pe un D3300 -
        /// pentru care aparatul scrie ID-ul "obiectiv fara CPU" si gama din meniul lui, si
        /// niciun profil nu poarta ID-ul acela. Lista poate cuprinde si alte obiective cu
        /// aceeasi gama (un Nikkor 135 f/2 DC), si raportul o spune.
        /// </summary>
        internal bool CandidatesByRangeOnly;

        internal bool HasRawLensId => Raw?.NikonLensId != null;

        /// <summary>Adevarat cand raw-ul spune o gama intreaga: focala minima si maxima si deschiderea.</summary>
        internal bool HasRawLensRange => Raw?.LensMinFocalMm != null && Raw.LensMaxFocalMm != null && Raw.LensMaxAperture != null;

        /// <summary>"ID-ul Nikon 139 din raw (11-20 mm f/2.8)", sau, cand ID-ul n-a dat nimic, gama singura.</summary>
        internal string RawLensLabel => CandidatesByRangeOnly ? SyntheticReport.RawRangeLabel(Raw) : SyntheticReport.RawLensLabel(Raw);
    }

    internal static class LensIdentification
    {
        /// <summary>
        /// Aduna ce se stie. <paramref name="reference"/> e cadrul pentru care se lucreaza
        /// (masterul, sau raw-ul dat consolei); <paramref name="raw"/> e un raw de langa el,
        /// sau null; <paramref name="rawName"/> numele lui, pentru note.
        /// </summary>
        internal static ShotIdentity Identify(LoadedFrame reference, LoadedFrame raw, ProfileSources sources,
                                              double? focal = null, double? fstop = null, double? crop = null,
                                              string rawName = null)
        {
            var id = new ShotIdentity();
            var notes = new List<string>();
            // Un raw are headerul FITS GOL (RawReader.NoHeader), un FITS are macar SIMPLE.
            bool referenceIsRaw = reference != null && (reference.Header == null || reference.Header.Count == 0);
            string besideLabel = $"raw ({rawName ?? "libraw"})";

            if (focal.HasValue) { id.FocalMm = focal; id.FocalSource = "--focal"; }
            else if (reference?.FocalMm != null)
            {
                id.FocalMm = reference.FocalMm;
                id.FocalSource = referenceIsRaw ? "raw (libraw)" : "FOCALLEN";
                notes.Add(referenceIsRaw ? "focala din raw" : "focala din FOCALLEN");
            }
            else if (raw?.FocalMm != null) { id.FocalMm = raw.FocalMm; id.FocalSource = besideLabel; notes.Add($"focala din {rawName ?? "raw"}"); }

            if (fstop.HasValue) { id.FStop = fstop; id.FStopSource = "--fstop"; }
            else if (reference?.FNumber != null) { id.FStop = reference.FNumber; id.FStopSource = "raw (libraw)"; }
            else if (raw?.FNumber != null) { id.FStop = raw.FNumber; id.FStopSource = besideLabel; notes.Add($"diafragma din {rawName ?? "raw"}"); }

            id.Camera = reference?.Camera ?? raw?.Camera;
            if (crop.HasValue) { id.Crop = crop; id.CropSource = "--crop"; }
            else
            {
                double? known = id.Camera == null ? null : sources?.Lensfun?.CropOf(id.Camera);
                if (known.HasValue)
                {
                    id.Crop = known;
                    id.CropSource = $"baza lensfun, {id.Camera}";
                    id.CropFromTable = true;
                    notes.Add($"crop din baza lensfun ({id.Camera.Trim()})");
                }
                else id.CropSource = "necunoscut - se ia senzorul de calibrare al profilului";
            }

            // Obiectivul: din raw-ul de langa, sau din referinta cand ea insasi e un raw.
            id.Raw = raw?.NikonLensId != null ? raw
                   : reference?.NikonLensId != null ? reference
                   : raw ?? (referenceIsRaw ? reference : null);
            id.RawName = rawName;
            if (id.Raw?.NikonLensId != null && sources != null)
            {
                var hits = CandidatesFromRaw(sources.Catalog, id.Raw);
                // Acelasi obiectiv exista pe mai multe monturi (Adobe: un fisier per montura).
                // Cand se stie aparatul, montura lui castiga: "Nikon D3300" alege [NIKON
                // CORPORATION] in fata lui [Canon]. Un cuvant din numele aparatului, nu o
                // tabela de monturi - nu se ghiceste nimic ce nu scrie in fisiere.
                string maker = id.Camera?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                if (hits.Count > 1 && !string.IsNullOrEmpty(maker))
                {
                    var byMount = hits.Where(h => (h.Mount + " " + h.Maker).Contains(maker, StringComparison.OrdinalIgnoreCase)).ToList();
                    if (byMount.Count > 0) hits = byMount;
                }
                id.Candidates = PreferCamera(hits, id.Camera);
            }
            // ID-ul n-a dat nimic (sau nu e), dar raw-ul spune gama: obiectivele monturii
            // aparatului al caror nume spune exact gama aceea. Numai cu aparatul stiut -
            // "135 mm f/2.0" pe toate monturile ar fi o lista, nu o identificare.
            if (id.Candidates.Count == 0 && id.HasRawLensRange && sources != null && !string.IsNullOrEmpty(id.Camera))
            {
                var byRange = CandidatesByRange(sources.Catalog, id.Raw, id.Camera);
                if (byRange.Count > 0)
                {
                    id.Candidates = byRange;
                    id.CandidatesByRangeOnly = true;
                }
            }
            id.Notes = notes;
            return id;
        }

        /// <summary>
        /// Obiectivele de pe montura aparatului al caror nume spune exact gama raw-ului: ambele
        /// capete ale focalei in toleranta lui LensSpec si deschiderea la un sfert de stop. Un
        /// nume care nu spune toata gama nu se potriveste - aici se cere egalitate, nu doar
        /// lipsa contradictiei ca la Narrow.
        /// </summary>
        internal static IReadOnlyList<LensChoice> CandidatesByRange(LensCatalog cat, LoadedFrame f, string camera)
        {
            string maker = camera?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (f == null || string.IsNullOrEmpty(maker)) return Array.Empty<LensChoice>();
            var hits = cat.Lenses.Where(l =>
            {
                LensSpec spec = LensSpec.Parse(l.Name);
                return spec.MinFocalMm.HasValue && spec.MaxFocalMm.HasValue && spec.MaxAperture.HasValue
                    && !spec.Contradicts(f.LensMinFocalMm, f.LensMaxFocalMm, f.LensMaxAperture)
                    && (l.Mount + " " + l.Maker).Contains(maker, StringComparison.OrdinalIgnoreCase);
            }).ToList();
            return PreferCamera(hits, camera);
        }

        /// <summary>
        /// Obiectivele pe care raw-ul le indica singur: cele cu ID-ul lui Nikon (in fisierele
        /// monturii aparatului), minus cele al caror nume contrazice gama din MakerNote - si,
        /// fiindca doar profilele Adobe poarta ID-uri Nikon, obiectivele lensfun ale acelorasi
        /// producatori a caror gama nu contrazice raw-ul. Fara ele lista restransa arata doar
        /// jumatatea Adobe, si utilizatorul a intrebat unde a disparut Tokina din lensfun.
        /// Goala fara ID.
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

        /// <summary>
        /// Dintre potriviri, cele al caror fisier Adobe e al aparatului insusi ("NIKON D750
        /// (obiectiv)") bat pe cele generice ale monturii ("NIKON CORPORATION (obiectiv)"):
        /// Adobe tine profile pe corp, cu SFF-ul corpului, si Lightroom pe acela il ia. Fara
        /// aparat, sau fara un asemenea fisier, lista ramane cum e.
        /// </summary>
        internal static IReadOnlyList<LensChoice> PreferCamera(IReadOnlyList<LensChoice> hits, string camera)
        {
            if (hits == null || hits.Count < 2) return hits;
            string wanted = Letters(camera);
            if (wanted.Length == 0) return hits;
            var own = hits.Where(h => Letters(h.Mount).Contains(wanted)).ToList();
            return own.Count > 0 ? own : hits;
        }

        static string Letters(string s) => new string((s ?? "").Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

        /// <summary>
        /// Primul raw dintr-o lista de cadre, sau null: regula "raw-ul de langa master", o
        /// singura data, pentru toate taburile care au nevoie de unul.
        /// </summary>
        internal static string FirstRaw(IEnumerable<string> frames)
            => frames?.FirstOrDefault(RawReader.IsRawPath);

        /// <summary>Numele de fisier pentru note, sau null.</summary>
        internal static string NameOf(string path) => path == null ? null : Path.GetFileName(path);
    }
}
