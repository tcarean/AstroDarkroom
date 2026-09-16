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

// SmoothRun.cs - modul 6 cap-coada: fisier -> flat netezit + text + doua harti.
//
// Ca LightRun, poate REFUZA: un raw (un cadru, necalibrat) sau un cub de culoare (pe
// care cititorul l-ar media intr-un mono). Refuzul nu produce fisier si nu produce cifre.
//
// Fisierul FITS NU se scrie aici, ci prin WriteFits, la cererea apelantului, intr-un
// Stream al lui: consola deschide un FileStream la calea din --smooth, fereastra la
// calea din sesiune dupa ce a intrebat de suprascriere, testele un MemoryStream. Acelasi
// motiv ca la harta PNG din RunOutcome - modul nu stie unde vrea apelantul fisierul.

using System.Collections.Generic;
using System.IO;

namespace FlatCheck
{
    internal sealed class SmoothRunOutcome
    {
        /// <summary>Cifrele si rasterul netezit, sau null la refuz.</summary>
        internal readonly SmoothResult Result;

        /// <summary>Cadrul incarcat - cardurile si BITPIX-ul lui merg in fisierul scris. Null la refuz.</summary>
        internal readonly LoadedFrame Frame;

        /// <summary>Harta componentei scoase (original / netezit), centrata pe 1. Null la refuz.</summary>
        internal readonly byte[] Png;

        /// <summary>Reziduul flat-ului netezit dupa propriul model radial - ce face obiectivul. Null la refuz.</summary>
        internal readonly byte[] SecondaryPng;

        /// <summary>De ce s-a refuzat, sau null.</summary>
        internal readonly string Refusal;

        internal bool Refused => Result == null;

        internal SmoothRunOutcome(SmoothResult r, LoadedFrame f, byte[] png, byte[] secondary, string refusal)
        {
            Result = r;
            Frame = f;
            Png = png;
            SecondaryPng = secondary;
            Refusal = refusal;
        }
    }

    /// <summary>Propunerea de scara, sau refuzul (raw, cub) - acelasi ca la Execute.</summary>
    internal sealed class SmoothAdviceOutcome
    {
        internal readonly SmoothScaleAdvice Advice;
        /// <summary>Cadrul incarcat, ca rularea de dupa propunere sa nu-l mai citeasca o data.</summary>
        internal readonly LoadedFrame Frame;
        internal readonly string Refusal;
        internal bool Refused => Advice == null;

        internal SmoothAdviceOutcome(SmoothScaleAdvice a, LoadedFrame f, string refusal)
        {
            Advice = a;
            Frame = f;
            Refusal = refusal;
        }
    }

    internal static class SmoothRun
    {
        internal static SmoothRunOutcome Execute(string path, double scalePx, TextWriter report)
        {
            LoadedFrame f = ImageLoader.Load(path);
            string refusal = Refusal(f, path);
            if (refusal != null)
            {
                report.WriteLine();
                report.WriteLine(refusal);
                return new SmoothRunOutcome(null, null, null, null, refusal);
            }
            return Execute(f, path, scalePx, report);
        }

        /// <summary>
        /// Propunerea de scara: incarca, refuza ce refuza si Execute, altfel ruleaza
        /// sfatuitorul si scrie tabelul lui. Cadrul ramane in rezultat, ca o netezire la
        /// scara propusa sa porneasca fara o a doua citire a fisierului de 96 MB.
        /// </summary>
        internal static SmoothAdviceOutcome Advise(string path, TextWriter report)
        {
            LoadedFrame f = ImageLoader.Load(path);
            string refusal = Refusal(f, path);
            if (refusal != null)
            {
                report.WriteLine();
                report.WriteLine(refusal);
                return new SmoothAdviceOutcome(null, null, refusal);
            }
            SmoothScaleAdvice a = SmoothScaleAdvisor.Advise(f.Image);
            SmoothReport.WriteAdvice(a, path, report);
            return new SmoothAdviceOutcome(a, f, null);
        }

        // Refuzurile INAINTE de orice calcul, ca la --light: un fisier scris pe un raw sau
        // pe un cub ar fi un fisier gresit cu aerul unuia bun.
        static string Refusal(LoadedFrame f, string path)
            => f.Cards.Count == 0 ? SmoothReport.RefusalRaw(path)
             : f.Planes > 1 ? SmoothReport.RefusalCube(path, f.Planes)
             : null;

        /// <summary>Netezirea unui cadru deja incarcat si deja acceptat.</summary>
        internal static SmoothRunOutcome Execute(LoadedFrame f, string path, double scalePx, TextWriter report)
        {
            var r = SmoothAnalyzer.Analyze(f.Image, scalePx);
            SmoothReport.Write(r, path, report);
            return new SmoothRunOutcome(r, f,
                PngWriter.EncodeResidualMap(r.Removed),
                PngWriter.EncodeResidualMap(r.After.Residual),
                null);
        }

        /// <summary>
        /// Tipul de iesire pentru un tip de intrare: acelasi, cand se poate. Float ramane
        /// float la aceleasi valori (masterul 0..1 al lui Siril ramane 0..1), 16 biti raman
        /// 16 biti; restul (8, 32, -64, si 0 = necunoscut) devin float. Nimic nu se
        /// renormalizeaza - Siril normalizeaza flat-ul singur la calibrare, iar un fisier la
        /// o scara surprinzatoare e un fisier pe care cineva il va citi gresit.
        /// </summary>
        internal static int OutputBitpix(int inputBitpix) => inputBitpix == 16 ? 16 : -32;

        /// <summary>Liniile HISTORY: cine, ce versiune, ce scara, din ce fisier.</summary>
        internal static IReadOnlyList<string> History(SmoothRunOutcome o, string sourcePath) => new[]
        {
            // sub 72 de caractere fiecare, ca sa incapa pe un singur card HISTORY
            $"{VersionInfo.Banner} --smooth: fit patratic local pe dale, sigma {o.Result.ScalePx:F0} px",
            $"sursa: {Path.GetFileName(sourcePath)}",
        };

        /// <summary>Scrie flat-ul netezit, cu headerul intrarii si liniile HISTORY, in <paramref name="s"/>.</summary>
        internal static void WriteFits(SmoothRunOutcome o, string sourcePath, Stream s)
            => FitsWriter.Write(s, o.Result.Smoothed, OutputBitpix(o.Frame.Bitpix), o.Frame.Cards, History(o, sourcePath));
    }
}
