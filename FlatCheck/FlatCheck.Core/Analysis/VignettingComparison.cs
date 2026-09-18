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

// Vignetarea masterului fata de profilele publicate ale obiectivului - al saselea test al
// modului structura, de alt fel decat celelalte cinci si pe alta scara.
//
// DE CE EXISTA. Cele cinci teste masoara abaterea de la simetria radiala, si sunt oarbe
// prin constructie la un exces RADIAL: masterele de tableta (2026-09-18, Tokina 20 mm
// f/2.8, patru orientari la 90 de grade) au iesit PERFECT pe toate cinci cu 1.26 stopuri
// de vignetare, cand cele doua baze dau 0.67-0.92 pentru obiectivul asta - caderea
// unghiulara a panoului, facuta radiala de rotatie. --ratio e la fel de orb (tot ce se
// roteste cu aparatul sau e simetrizat de rotatie ii scapa). Singura referinta externa pe
// care o are unealta e modelul obiectivului, si asta e comparatia.
//
// CUM SE MASOARA. Nu evaluand polinomul in punctul din colt, ci construind rasterul
// modelului la marimea rasterului de analiza (SyntheticFlatBuilder) si rulandu-i EXACT
// Metrics.CornerStats - acelasi operator ca pe master, deci diferenta dintre "mediana unei
// cutii de 5 %" si "valoarea intr-un punct" nu exista, iar artefactul de jumatate de pixel
// (known issue 5) e acelasi pe amandoua partile. Costul e ~1.5 M evaluari, zeci de ms.
//
// VERDICTUL E AL CELUI MAI APROPIAT PROFIL, nu al celui mai rau. Cele doua baze difera
// intre ele cu ~0.2 stopuri pe acelasi obiectiv (docs/lens-profiles.md, sectiunea 8), si
// unealta nu poate acuza un master cu mai multa precizie decat au bazele intre ele: un
// master care coincide cu una din ele nu e gresit. "Cel mai rau" ar fi dat REFA si pe
// masterele de monitor bune (0.15 fata de Adobe, 0.03 fata de lensfun). Pragurile nu sunt
// noi: diferenta in stopuri trece prin Stats.DepthErrorPct (2^|D| - 1) pe scara de reziduu,
// exact ca adancimea din --against. Semnul e diagnosticul: plus = masterul cade mai mult
// decat modelul (sursa cade cu unghiul: panou fara difuzor real, prea aproape), minus =
// mai putin (sursa umple colturile: cupola cerului, ambient, o reflexie).
//
// CE NU POATE STI, si raportul o spune: profilul e modelul obiectivului, nu exemplarul;
// e acromatic, iar masterul e o medie 2x2 a mozaicului (ponderata pe verde); un exces
// radial nu se vede nici in cele cinci teste, nici in harta. Fara raw langa master nu e
// niciun ID de obiectiv, fara diafragma nu se poate alege profilul: atunci nu se compara,
// se explica de ce, si verdictul ramane al celor cinci teste - "Unknown is not a refusal",
// regula de la --light.

using System;
using System.Collections.Generic;
using System.Linq;

namespace FlatCheck
{
    /// <summary>De ce nu s-a putut compara. None cand s-a comparat.</summary>
    internal enum ComparisonGap
    {
        None,
        /// <summary>Niciun profil pe masina: nici Adobe, nici lensfun/.</summary>
        NoProfiles,
        /// <summary>Niciun raw langa master (sau ca referinta), deci niciun ID de obiectiv - si niciun --lens.</summary>
        NoRaw,
        /// <summary>Raw-ul are ID, dar niciun obiectiv din catalog nu i se potriveste.</summary>
        NoLensMatch,
        /// <summary>Focala nu se stie (nici FOCALLEN, nici raw, nici --focal).</summary>
        NoFocal,
        /// <summary>Diafragma nu se stie: Siril n-o scrie, si n-a fost niciun raw sau --fstop.</summary>
        NoFStop,
        /// <summary>Doar profile lensfun, si crop-ul aparatului nu se stie.</summary>
        NoCrop,
    }

    /// <summary>Un profil pus fata in fata cu masterul.</summary>
    internal sealed class ProfileDepth
    {
        internal readonly LensProfile Profile;
        /// <summary>Adancimea modelului in colt, masurata cu CornerStats pe rasterul lui, in stopuri.</summary>
        internal readonly double CornerStops;
        /// <summary>master - profil, in stopuri; pozitiv = masterul cade mai mult spre colt decat modelul.</summary>
        internal readonly double DiffStops;
        /// <summary>2^|DiffStops| - 1, in procente: eroarea lasata in coltul unui light calibrat cu masterul, daca modelul e adevarul.</summary>
        internal readonly double ErrorPct;
        internal readonly string Verdict;
        /// <summary>Crop-ul cu care s-a construit modelul (0 = senzorul de calibrare al profilului).</summary>
        internal readonly double CameraCrop;
        /// <summary>Coltul masterului fata de coltul de calibrare al profilului (SyntheticFlatResult.Extrapolation).</summary>
        internal readonly double Extrapolation;

        internal ProfileDepth(LensProfile profile, double cornerStops, double masterStops, double cameraCrop, double extrapolation)
        {
            Profile = profile;
            CornerStops = cornerStops;
            DiffStops = masterStops - cornerStops;
            ErrorPct = Stats.DepthErrorPct(DiffStops);
            Verdict = Stats.DepthVerdict(DiffStops);
            CameraCrop = cameraCrop;
            Extrapolation = extrapolation;
        }
    }

    internal sealed class VignettingComparison
    {
        internal readonly double MasterStops;
        internal readonly ShotIdentity Shot;
        /// <summary>Un rand pe obiectiv candidat, in ordinea catalogului; gol cand nu s-a putut compara.</summary>
        internal readonly IReadOnlyList<ProfileDepth> Profiles;
        /// <summary>Cel cu diferenta cea mai mica in modul; null cand nu s-a comparat.</summary>
        internal readonly ProfileDepth Nearest;
        /// <summary>Verdictul comparatiei = al celui mai apropiat profil; null cand nu s-a comparat.</summary>
        internal readonly string Verdict;
        /// <summary>Cat difera profilele intre ele in colt, in stopuri (max - min); 0 cu un singur profil.</summary>
        internal readonly double SpreadStops;
        internal readonly ComparisonGap Gap;
        /// <summary>Obiectivele lensfun sarite fiindca crop-ul nu se stie; goala de obicei.</summary>
        internal readonly IReadOnlyList<LensChoice> SkippedForCrop;

        internal bool Compared => Verdict != null;

        internal VignettingComparison(double masterStops, ShotIdentity shot, IReadOnlyList<ProfileDepth> profiles,
                                      ComparisonGap gap, IReadOnlyList<LensChoice> skippedForCrop)
        {
            MasterStops = masterStops;
            Shot = shot;
            Profiles = profiles ?? Array.Empty<ProfileDepth>();
            Gap = gap;
            SkippedForCrop = skippedForCrop ?? Array.Empty<LensChoice>();
            if (Profiles.Count > 0)
            {
                // NaN (un profil degenerat) nu castiga niciodata: Min pe o cheie NaN ar putea sa-l ia.
                Nearest = Profiles.OrderBy(p => double.IsNaN(p.DiffStops) ? double.PositiveInfinity : Math.Abs(p.DiffStops)).First();
                Verdict = Nearest.Verdict;
                SpreadStops = Profiles.Max(p => p.CornerStops) - Profiles.Min(p => p.CornerStops);
            }
            else SpreadStops = double.NaN;
        }
    }

    internal static class VignettingComparer
    {
        /// <summary>
        /// Compara masterul cu profilele obiectivelor din <paramref name="lenses"/> (implicit
        /// candidatii identitatii: ce a indicat raw-ul), fiecare prin profilul cel mai apropiat
        /// de focala si diafragma. <paramref name="catalog"/> null sau gol inseamna NoProfiles.
        /// </summary>
        internal static VignettingComparison Analyze(StructureResult master, ShotIdentity shot, LensCatalog catalog,
                                                     IReadOnlyList<LensChoice> lenses = null)
        {
            lenses ??= shot?.Candidates ?? Array.Empty<LensChoice>();
            double masterStops = master.VignetteStops;
            // Un catalog gol e motivul cel mai adanc; unul necunoscut (null) nu e un motiv, deci
            // fara raw se spune "fara raw", care e ce poate omul sa repare.
            if (catalog != null && catalog.Lenses.Count == 0)
                return new VignettingComparison(masterStops, shot, null, ComparisonGap.NoProfiles, null);
            if (lenses.Count == 0)
                return new VignettingComparison(masterStops, shot, null,
                    shot != null && shot.HasRawLensId ? ComparisonGap.NoLensMatch : ComparisonGap.NoRaw, null);
            if (catalog == null)
                return new VignettingComparison(masterStops, shot, null, ComparisonGap.NoProfiles, null);
            if (shot?.FocalMm == null)
                return new VignettingComparison(masterStops, shot, null, ComparisonGap.NoFocal, null);
            if (shot.FStop == null)
                return new VignettingComparison(masterStops, shot, null, ComparisonGap.NoFStop, null);

            double crop = shot.Crop ?? 0;
            var rows = new List<ProfileDepth>();
            var skipped = new List<LensChoice>();
            foreach (LensChoice lens in lenses)
            {
                // lensfun normalizeaza raza la semidiagonala senzorului de calibrare, deci fara
                // crop-ul aparatului nu stie unde e coltul tau; Adobe are SFF-ul ca rezerva.
                if (lens.Source == ProfileSource.Lensfun && !(crop > 0)) { skipped.Add(lens); continue; }
                LensProfile p = LensCatalog.Nearest(catalog.ProfilesOf(lens), shot.FocalMm.Value, shot.FStop.Value);
                if (p == null) continue;
                rows.Add(Measure(p, master, crop));
            }
            ComparisonGap gap = rows.Count > 0 ? ComparisonGap.None : skipped.Count > 0 ? ComparisonGap.NoCrop : ComparisonGap.NoLensMatch;
            return new VignettingComparison(masterStops, shot, rows, gap, skipped);
        }

        /// <summary>
        /// Adancimea unui profil in colt, cu operatorul masterului: rasterul modelului la
        /// marimea rasterului de analiza, apoi CornerStats pe el.
        /// </summary>
        internal static ProfileDepth Measure(LensProfile p, StructureResult master, double crop)
        {
            SyntheticFlatResult built = SyntheticFlatBuilder.Build(p, master.Width, master.Height, crop);
            var (corners, centre) = Metrics.CornerStats(built.Flat);
            double stops = Math.Log2(centre / corners.Values.Average());
            return new ProfileDepth(p, stops, master.VignetteStops, crop, built.Extrapolation);
        }
    }
}
