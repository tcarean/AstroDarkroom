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

// CropTexts.cs - the sentences of the Crop with aspect ratio tab (R-180 to R-183, ADR 0093):
// what the rectangle keeps and cuts, what the save writes, the refusals, the proposed file
// name, the HISTORY cards and the description tag of the saved file.
//
// The window writes none of them: it shows them. Plain words (ADR 0071), every text a pair in
// the interface language (ADR 0048), every number the core's.

using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace AstroPhotoAnalyzer
{
    /// <summary>
    /// The sentences the Crop with aspect ratio tab shows and the texts its saved file carries,
    /// in the interface language (R-180 to R-183); the console prints none of them.
    /// </summary>
    internal static class CropTexts
    {
        /// <summary>What the tab asks for while the two sizes are missing (R-183).</summary>
        internal static string AskSizes => Tr.Pick(
            "Type the frame's width and height, in cm, to see the part of the image that goes on it.",
            "Scrie latimea si inaltimea ramei, in cm, ca sa vezi partea din poza care intra pe ea.");

        /// <summary>The tab's message while the file is being written.</summary>
        internal static string Saving => Tr.Pick("writing the file...", "se scrie fisierul...");

        /// <summary>How the rectangle is moved and resized, under the image (R-183).</summary>
        internal static string Help => Tr.Pick(
            "Drag inside the rectangle to move it; drag one of its corners to make it smaller or larger. Its proportions stay the frame's. The darkened part is left out of the saved file. The image here is a reduced copy; the saved file is cut from the full image.",
            "Tragi din interiorul dreptunghiului ca sa-l muti; tragi de un colt ca sa-l micsorezi sau sa-l maresti. Proportiile lui raman ale ramei. Partea intunecata nu ajunge in fisierul salvat. Poza de aici e o copie micsorata; fisierul salvat se taie din poza intreaga.");

        /// <summary>A frame size as the texts and the file name write it: a point, no trailing zeros.</summary>
        /// <param name="cm">The size.</param>
        /// <returns>"70", "54.62".</returns>
        internal static string Size(double cm) => cm.ToString("0.####", CultureInfo.InvariantCulture);

        /// <summary>The sentence for a field that does not hold a size (R-180).</summary>
        /// <param name="width">True for the width's field, false for the height's.</param>
        /// <param name="typed">What the field holds.</param>
        /// <returns>The sentence, naming the field and what was typed.</returns>
        internal static string BadSize(bool width, string typed)
            => width
                ? Tr.Pick($"The frame's width is not a number above 0: '{typed}'. Write it with a point, for example 54.62.", $"Latimea ramei nu e un numar mai mare ca 0: '{typed}'. Scrie-l cu punct, de exemplu 54.62.")
                : Tr.Pick($"The frame's height is not a number above 0: '{typed}'. Write it with a point, for example 54.62.", $"Inaltimea ramei nu e un numar mai mare ca 0: '{typed}'. Scrie-l cu punct, de exemplu 54.62.");

        /// <summary>
        /// What the rectangle keeps and what it cuts, in one sentence (R-183): its size, its
        /// share of the image, and the pixels cut on each side that loses any.
        /// </summary>
        /// <param name="f">The rectangle's figures.</param>
        /// <returns>The sentence.</returns>
        internal static string Kept(FrameCropFacts f)
        {
            var cuts = new List<string>();
            if (f.CutLeft > 0) cuts.Add(Fmt.Int(f.CutLeft) + Tr.Pick(" px on the left", " px in stanga"));
            if (f.CutRight > 0) cuts.Add(Fmt.Int(f.CutRight) + Tr.Pick(" px on the right", " px in dreapta"));
            if (f.CutTop > 0) cuts.Add(Fmt.Int(f.CutTop) + Tr.Pick(" px at the top", " px sus"));
            if (f.CutBottom > 0) cuts.Add(Fmt.Int(f.CutBottom) + Tr.Pick(" px at the bottom", " px jos"));
            string cut = cuts.Count == 0
                ? Tr.Pick("nothing is cut away", "nu se taie nimic")
                : Tr.Pick("cut away: ", "se taie ") + Join(cuts);
            return Tr.Pick($"{Fmt.Dim(f.W, f.H)} px stay, {Fmt.Pct(f.PctKept)} of the image; {cut}", $"ramane {Fmt.Dim(f.W, f.H)} px, {Fmt.Pct(f.PctKept)} din poza; {cut}");
        }

        /// <summary>A list in words: commas between the items, "and" before the last.</summary>
        /// <param name="items">The items, at least one.</param>
        /// <returns>"a", "a and b", "a, b and c".</returns>
        static string Join(List<string> items)
        {
            if (items.Count == 1) return items[0];
            return string.Join(", ", items.GetRange(0, items.Count - 1)) + Tr.Pick(" and ", " si ") + items[^1];
        }

        /// <summary>How fine the kept pixels print on the frame (R-183).</summary>
        /// <param name="f">The rectangle's figures.</param>
        /// <param name="frameW">The frame's width, in cm.</param>
        /// <param name="frameH">The frame's height, in cm.</param>
        /// <returns>The sentence, with the pixels per inch as a whole number.</returns>
        internal static string AtFrame(FrameCropFacts f, double frameW, double frameH)
            => Tr.Pick($"on a frame of {Size(frameW)} x {Size(frameH)} cm: {Fmt.F0(f.Ppi)} pixels per inch (ppi)", $"pe o rama de {Size(frameW)} x {Size(frameH)} cm: {Fmt.F0(f.Ppi)} pixeli pe inch (ppi)");

        /// <summary>The kind of file a crop is written as, in a few words: "a 16-bit TIFF".</summary>
        /// <param name="o">The output, without a problem.</param>
        /// <returns>The kind and its depth; for a FITS of real numbers, that too.</returns>
        static string KindName(CropOutput o)
        {
            string format = o.Kind switch { CropFileKind.Fits => "FITS", CropFileKind.Tiff => "TIFF", _ => "PNG" };
            string bits = Fmt.Int(o.Bits);
            string real = o.IsFloat ? Tr.Pick(" (real numbers)", " (numere reale)") : "";
            return Tr.Pick($"a {bits}-bit {format}{real}", $"{format} pe {Fmt.RoCount(bits)}biti{real}");
        }

        /// <summary>
        /// What the save will write, or why it cannot (R-183): the input's own kind and depth,
        /// and for a JPEG why it becomes a TIFF.
        /// </summary>
        /// <param name="o">The output the core chose for the input.</param>
        /// <returns>The sentence; the problem itself when there is one.</returns>
        internal static string Output(CropOutput o)
        {
            if (o.Problem != null) return o.Problem;
            string unchanged = Tr.Pick(" The input image is not changed.", " Poza de intrare nu se modifica.");
            if (o.FromJpeg)
                return Tr.Pick(
                    $"The input is a JPEG, which cannot be saved again without a second compression that loses detail: the save writes {KindName(o)} with the same pixels.",
                    $"Intrarea e un JPEG, care nu poate fi salvat din nou fara o a doua comprimare, cu pierderi: se salveaza un {KindName(o)}, cu aceiasi pixeli.") + unchanged;
            return Tr.Pick(
                $"The save writes a new file of the input's kind, with the same pixels: {KindName(o)}.",
                $"Se salveaza un fisier nou, de acelasi fel ca intrarea, cu aceiasi pixeli: {KindName(o)}.") + unchanged;
        }

        /// <summary>The refusal of a TIFF the reader only gets through a conversion (R-181).</summary>
        /// <param name="bits">The file's bits per sample.</param>
        /// <returns>The sentence.</returns>
        internal static string TiffDepthRefused(int bits)
        {
            string b = Fmt.Int(bits);
            return Tr.Pick(
                $"a TIFF with {b} bits per sample cannot be cropped without loss: the program only reads it converted to 16 bits. Save it from Siril at 16 bits and crop that one.",
                $"un TIFF cu {Fmt.RoCount(b)}biti pe esantion nu poate fi decupat fara pierderi: programul il citeste doar transformat in 16 biti. Salveaza-l din Siril pe 16 biti si decupeaza-l pe acela.");
        }

        /// <summary>The refusal of a file that is none of the kinds the crop writes (R-181).</summary>
        /// <param name="format">The format the reader found.</param>
        /// <returns>The sentence.</returns>
        internal static string FormatRefused(string format)
            => Tr.Pick(
                $"a file of this kind ({format}) cannot be cropped without loss: the program crops FITS, TIFF, PNG and JPEG files.",
                $"un fisier de felul acesta ({format}) nu poate fi decupat fara pierderi: programul decupeaza fisiere FITS, TIFF, PNG si JPEG.");

        /// <summary>
        /// The file name proposed for a crop (R-182): the input's name, "_cropped_", the frame
        /// as typed, the input's extension or the one the output needs.
        /// </summary>
        /// <param name="inputPath">The input's path or name.</param>
        /// <param name="extension">The extension to use instead of the input's, with its point; null keeps the input's.</param>
        /// <param name="frameW">The frame's width, as typed.</param>
        /// <param name="frameH">The frame's height, as typed.</param>
        /// <returns>The file name alone.</returns>
        internal static string ProposedFileName(string inputPath, string extension, double frameW, double frameH)
            => Path.GetFileNameWithoutExtension(inputPath) + "_cropped_" + Size(frameW) + "x" + Size(frameH) + (extension ?? Path.GetExtension(inputPath));

        /// <summary>The HISTORY cards of a cropped FITS: the program, the frame, the rectangle.</summary>
        /// <param name="r">The rectangle kept.</param>
        /// <param name="imageW">The input's width, in pixels.</param>
        /// <param name="imageH">The input's height, in pixels.</param>
        /// <param name="frameW">The frame's width, in cm.</param>
        /// <param name="frameH">The frame's height, in cm.</param>
        /// <returns>The texts, one card each, each under 72 characters.</returns>
        internal static IReadOnlyList<string> History(CropRect r, int imageW, int imageH, double frameW, double frameH)
            => new[]
            {
                Tr.Pick($"{VersionInfo.Banner}: crop for a frame, {Size(frameW)} x {Size(frameH)} cm", $"{VersionInfo.Banner}: decupare pentru rama, {Size(frameW)} x {Size(frameH)} cm"),
                Tr.Pick($"kept {Fmt.Dim(r.W, r.H)} px of {Fmt.Dim(imageW, imageH)}", $"pastrat {Fmt.Dim(r.W, r.H)} px din {Fmt.Dim(imageW, imageH)}"),
                Tr.Pick($"from column {Fmt.Int(r.X)}, row {Fmt.Int(r.Y)}, counted from the top left", $"de la coloana {Fmt.Int(r.X)}, randul {Fmt.Int(r.Y)}, numarate din stanga sus"),
                Tr.Pick("the pixels are the input's own, none changed", "pixelii sunt ai intrarii, niciunul schimbat"),
            };

        /// <summary>The description tag of a cropped TIFF.</summary>
        /// <param name="r">The rectangle kept.</param>
        /// <param name="frameW">The frame's width, in cm.</param>
        /// <param name="frameH">The frame's height, in cm.</param>
        /// <returns>One line, ASCII.</returns>
        internal static string Description(CropRect r, double frameW, double frameH)
            => Tr.Pick(
                $"{VersionInfo.Banner}: crop for a frame, {Size(frameW)} x {Size(frameH)} cm ({Fmt.Dim(r.W, r.H)} px from {Fmt.Int(r.X)}, {Fmt.Int(r.Y)})",
                $"{VersionInfo.Banner}: decupare pentru rama, {Size(frameW)} x {Size(frameH)} cm ({Fmt.Dim(r.W, r.H)} px de la {Fmt.Int(r.X)}, {Fmt.Int(r.Y)})");
    }
}
