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

// Un flat fata de altul: structura pe fiecare si pe raportul A / B, puse una langa alta.
// Exista pentru flatul sintetic - cat difera un master real de modelul obiectivului, si in
// ce forma - dar merge pe orice pereche de flaturi ale aceluiasi obiectiv: doua grosimi de
// difuzor, doua surse, testul de convergenta din README. Nu e --ratio: acela desparte
// fix-pe-aparat de fix-in-lume prin rotatie; asta spune doar cu cat si cum difera doua
// fisiere.
//
// Trei coloane, nu una: primul raport arata doar bateria pe raport, si cifra "vignetare
// -0.42 stopuri" nu se putea citi fara sa stii ca masterul are 0.26 si sinteticul 0.69.
// Ambele se pregatesc (Img.Prepare) si se normalizeaza la mediana 1; A, B si A / B trec
// fiecare prin StructureAnalyzer.AnalyzePrepared, deci cifrele au acelasi inteles ca la
// structura, iar in coloana diferentei sunt despre DIFERENTA: vignetarea raportului e
// diferenta de adancime in stopuri (pozitiv = A cade mai mult spre colt decat B), cele
// cinci teste spun daca diferenta e radiala sau nu, harta ii arata forma. Verdictul e al
// diferentei, si textul o spune. Notele randurilor sunt ale comparatiei, nu ale tabului de
// structura: "e o proprietate a obiectivului, nu o eroare" ar fi fost exact pe dos aici.
//
// Verdictul diferentei are doua parti: FORMA - cele cinci teste pe raport, care spun daca
// ce difera e radial - si ADANCIMEA - diferenta de vignetare in stopuri. Prima versiune
// avea doar forma, si masterul de cer, cu 0.42 stopuri mai putina vignetare decat modelul,
// iesea ACCEPTABIL pe forma de parca diferenta ar fi fost mica; utilizatorul a citit-o ca
// "enorma, ceva e foarte in neregula", si avea dreptate. Adancimea nu primeste un prag nou:
// o diferenta de D stopuri in colt lasa in coltul light-ului calibrat o eroare de 2^|D| - 1,
// o amplitudine fractionara ca toate celelalte, deci trece prin aceeasi scara de reziduu
// (2 / 10 / 20 %): sub 0.03 stopuri PERFECT, sub 0.14 BUN, sub 0.26 ACCEPTABIL, altfel REFA.
// Pe masterele Tokina: cer -0.42 -> 34 % REFA; monitor-aproape +0.16 -> 12 % ACCEPTABIL;
// pe-monitor +0.09 -> 6 % BUN; Adobe fata de lensfun 0.16 -> ACCEPTABIL - cele doua baze
// difera intre ele cu ~0.2 stopuri pe acelasi obiectiv, deci sub ACCEPTABIL diferenta nu
// se deosebeste de incertitudinea modelului, si nota randului o spune. Verdictul general
// e cel mai rau dintre forma si adancime. Chip-urile pe cele cinci teste in coloana
// diferentei au fost scoase: "PERFECT" langa 0.11 % pe un raport nu spunea nimic ce nu
// spune verdictul de sus.

using System;
using System.Collections.Generic;
using System.IO;

namespace FlatCheck
{
    internal sealed class AgainstResult
    {
        /// <summary>Structura pe A, pe B si pe raportul A / B, toate la rezolutia de analiza.</summary>
        internal readonly StructureResult A, B, Structure;
        internal readonly Img Ratio;
        internal readonly int Width, Height;

        /// <summary>Diferenta de adancime in stopuri (a raportului), si eroarea pe care o lasa in colt, in procente.</summary>
        internal readonly double DepthDiffStops, DepthErrorPct;
        /// <summary>Verdictul adancimii, pe scara de reziduu aplicata erorii din colt.</summary>
        internal readonly string DepthVerdict;
        /// <summary>Verdictul diferentei: cel mai rau dintre forma (cele cinci teste pe raport) si adancime.</summary>
        internal readonly string Verdict;

        internal AgainstResult(StructureResult a, StructureResult b, StructureResult ratio, Img ratioRaster, int w, int h)
        {
            A = a;
            B = b;
            Structure = ratio;
            Ratio = ratioRaster;
            Width = w;
            Height = h;
            DepthDiffStops = ratio.VignetteStops;
            DepthErrorPct = Stats.DepthErrorPct(DepthDiffStops);
            DepthVerdict = Stats.DepthVerdict(DepthDiffStops);
            Verdict = Stats.Severity(DepthVerdict) > Stats.Severity(ratio.Verdict) ? DepthVerdict : ratio.Verdict;
        }

        /// <summary>Eroarea din colt pentru o diferenta de adancime; e <see cref="Stats.DepthErrorPct"/>, care are acum si un al doilea consumator.</summary>
        internal static double DepthErrorOf(double stops) => Stats.DepthErrorPct(stops);
    }

    internal static class AgainstAnalyzer
    {
        internal static AgainstResult Analyze(Img a, Img b)
        {
            Img pa = Img.Prepare(a), pb = Img.Prepare(b);
            if (pa.W != pb.W || pa.H != pb.H)
                throw new ArgumentException($"cele doua flaturi au marimi diferite la rezolutia de analiza ({pa.W}x{pa.H} fata de {pb.W}x{pb.H})");
            Normalise(pa);
            Normalise(pb);
            Img ratio = Img.Divide(pa, pb);
            return new AgainstResult(StructureAnalyzer.AnalyzePrepared(pa), StructureAnalyzer.AnalyzePrepared(pb),
                StructureAnalyzer.AnalyzePrepared(ratio), ratio, a.W, a.H);
        }

        static void Normalise(Img img)
        {
            double med = Stats.Percentile((double[])img.D.Clone(), 50);
            if (!(med > 0)) throw new ArgumentException("un flat cu mediana zero sau negativa nu se poate normaliza");
            for (int i = 0; i < img.D.Length; i++) img.D[i] /= med;
        }
    }

    /// <summary>Un rand al tabelului: aceeasi marime pe A, pe B si pe A / B, cu verdictul unde e un test.</summary>
    internal sealed record AgainstRow(string Key, string Label, string A, string AVerdict, string B, string BVerdict,
                                      string Diff, string DiffVerdict, string Note, bool IsKey);

    internal static class AgainstReport
    {
        internal static void Write(AgainstResult r, string pathA, string pathB, TextWriter w)
        {
            w.WriteLine($"\n=== COMPARATIE: A / B ===");
            w.WriteLine($"    A : {pathA}");
            w.WriteLine($"    B : {pathB}");
            w.WriteLine($"    dimensiune analiza : {r.Structure.Residual.W} x {r.Structure.Residual.H} (dupa 2x2 + downsample)");
            w.WriteLine();
            foreach (string line in Meaning().Split('\n')) w.WriteLine("    " + line);

            w.WriteLine($"\n  {"",-28}{"A",-38}{"B",-38}{"A / B (diferenta)"}");
            foreach (AgainstRow row in Rows(r))
                w.WriteLine($"    {row.Label,-26}{Cell(row.A, row.AVerdict),-38}{Cell(row.B, row.BVerdict),-38}{Cell(row.Diff, row.DiffVerdict)}");

            w.WriteLine($"\n  VERDICTUL DIFERENTEI: {r.Verdict}");
            w.WriteLine($"    forma     : {ShapeDetail(r)}");
            w.WriteLine($"                (sub 2 % PERFECT, 2-10 BUN, 10-20 ACCEPTABIL, peste 20 REFA)");
            w.WriteLine($"    adancimea : {DepthDetail(r)},");
            w.WriteLine($"                pe aceeasi scara ({DepthScale()})");
            foreach (string line in StructureReport.DepthLightScaleNote(r.DepthDiffStops).Split('\n'))
                w.WriteLine("                " + line);
            w.WriteLine("    " + WorseOfBoth());
        }

        /// <summary>"PERFECT - cele cinci teste pe raport, pe scara de reziduu a structurii".</summary>
        internal static string ShapeDetail(AgainstResult r)
            => $"{r.Structure.Verdict}: cele cinci teste pe raport, pe scara de reziduu a structurii";

        /// <summary>"REFA - 0.30 stopuri = 23.0 % eroare in coltul light-ului calibrat".</summary>
        internal static string DepthDetail(AgainstResult r)
            => $"{r.DepthVerdict}: {Stops(r.Structure)} = {r.DepthErrorPct:F1} % eroare in coltul light-ului calibrat";

        internal static string WorseOfBoth()
            => "Cel mai rau dintre ele. Doua flaturi corecte ale aceluiasi obiectiv dau PERFECT si 0.00 stopuri.";

        /// <summary>Cele doua parti ale verdictului pe un rand, pentru fereastra, care n-are blocul consolei.</summary>
        internal static string VerdictDetail(AgainstResult r)
            => $"forma: {ShapeDetail(r)}. Adancimea: {DepthDetail(r)} ({DepthScale()}). "
             + StructureReport.DepthLightScaleNote(r.DepthDiffStops).Replace('\n', ' ') + " " + WorseOfBoth();

        /// <summary>Benzile adancimii in stopuri, derivate din scara de reziduu prin 2^|D| - 1.</summary>
        internal static string DepthScale()
            => $"sub {StopsFor(Th.ResidPerfect):F2} stopuri PERFECT, sub {StopsFor(Th.ResidUsable):F2} BUN, sub {StopsFor(Th.ResidCheck):F2} ACCEPTABIL, peste REFA";

        /// <summary>Inversul lui <see cref="AgainstResult.DepthErrorOf"/>: stopurile la care eroarea din colt atinge un prag.</summary>
        internal static double StopsFor(double errorPct) => Stats.StopsForError(errorPct);

        static string Cell(string value, string verdict) => string.IsNullOrEmpty(verdict) ? value : $"{value} [{verdict}]";

        internal static string Meaning()
            => "Trei coloane: fiecare flat prin bateria de structura, si raportul lor. In coloana A / B\n"
             + "cifrele sunt despre DIFERENTA: \"vignetare\" e diferenta de adancime (pozitiv = A cade mai\n"
             + "mult spre colt decat B) si poarta verdictul adancimii; cele cinci teste spun daca diferenta\n"
             + "e radiala sau are forma, iar harta o arata. Ce e in A si nu e in B - o sa a cerului, caderea\n"
             + "unui panou, o pata - ramane in diferenta.";

        internal static IReadOnlyList<AgainstRow> Rows(AgainstResult r)
        {
            var rows = new List<AgainstRow>
            {
                new("VignetteStops", "vignetare centru -> colt",
                    Stops(r.A), null, Stops(r.B), null, Stops(r.Structure), r.DepthVerdict,
                    "adancimea fiecaruia si diferenta lor (pozitiv = A cade mai mult spre colt decat B). "
                    + $"Verdictul e al diferentei: {r.DepthErrorPct:F1} % eroare in coltul unui light calibrat cu flatul gresit (2^|stopuri| - 1), pe scara de reziduu; "
                    + "sub ACCEPTABIL nu se deosebeste de incertitudinea modelului - cele doua baze de profile difera intre ele cu ~0.2 stopuri pe acelasi obiectiv.", true),
                new("CentroidOffsetPct", "offset centru iluminare",
                    Offset(r.A), null, Offset(r.B), null, Offset(r.Structure), null,
                    "unde sta varful iluminarii fata de centrul senzorului, la fiecare si la raport; "
                    + "aproape de zero pe raport = diferenta e centrata", false),
                new("LocalSpotPct", "pete locale (praf etc.)",
                    Spot(r.A), null, Spot(r.B), null, Spot(r.Structure), null,
                    "cea mai adanca pata compacta a fiecaruia; o pata prezenta doar intr-unul ramane in diferenta; "
                    + $"pozitii: A ({r.A.LocalSpotX}, {r.A.LocalSpotY}), B ({r.B.LocalSpotX}, {r.B.LocalSpotY}), "
                    + $"A / B ({r.Structure.LocalSpotX}, {r.Structure.LocalSpotY})", false),
            };
            foreach (string key in StructureReport.TestKeys)
            {
                var (va, ba) = StructureReport.Test(r.A, key);
                var (vb, bb) = StructureReport.Test(r.B, key);
                var (vd, bd) = StructureReport.Test(r.Structure, key);
                // Fara verdict pe coloana diferentei: "PERFECT" langa 0.11 % pe raport nu spunea nimic
                // ce nu spune verdictul de sus, care e al formei intregi.
                rows.Add(new AgainstRow(key, StructureReport.TestLabel(key), Pct(va), ba, Pct(vb), bb, Pct(vd), null,
                    TestNote(key), false));
            }
            return rows;
        }

        /// <summary>Nota unui test, spusa despre diferenta - nu despre un flat.</summary>
        internal static string TestNote(string key) => key switch
        {
            "CornerSpreadPct" => "cat difera cele patru colturi intre ele; colturi inegale in coloana A / B = structura neradiala pe care unul o are si celalalt nu",
            "AzimuthalMaxPct" => "cat difera cele 12 sectoare ale aceluiasi inel, in cercul inscris; pe raport, forma azimutala a diferentei",
            "ResidualPtpPct" => "cat din fiecare, si cat din diferenta, NU se explica printr-un model radial; cel mai important",
            "TiltPct" => "gradientul liniar al fiecaruia si al diferentei; un gradient de sursa apare aici",
            "MaxTilePct" => "dala cea mai abatuta; un defect compact prezent intr-unul singur apare in coloana A / B",
            _ => "",
        };

        static string Stops(StructureResult s) => $"{s.VignetteStops:F2} stopuri";

        /// <summary>Pata fara pozitie - pozitiile stau in nota randului, ca celula sa ramana scurta.</summary>
        static string Spot(StructureResult s)
        {
            string full = StructureReport.LocalSpotValue(s);
            int at = full.LastIndexOf(" la (", StringComparison.Ordinal);
            return at > 0 ? full.Substring(0, at) : full;
        }
        static string Offset(StructureResult s) => s.CentroidValid ? $"{s.CentroidOffsetPct:F2} %" : "nedeterminat";
        static string Pct(double v) => double.IsNaN(v) ? "n/a" : $"{v:F2} %";
    }

    internal static class AgainstRun
    {
        internal static RunOutcome<AgainstResult> Execute(string pathA, string pathB, TextWriter report)
        {
            var fa = ImageLoader.Load(pathA);
            var fb = ImageLoader.Load(pathB);
            var r = AgainstAnalyzer.Analyze(fa.Image, fb.Image);
            AgainstReport.Write(r, pathA, pathB, report);
            return new RunOutcome<AgainstResult>(r, PngWriter.EncodeResidualMap(r.Structure.Residual));
        }
    }
}
