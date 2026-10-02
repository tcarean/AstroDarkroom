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

// AnalysisRun.cs - lantul intreg: incarca -> masoara -> recomanda -> formateaza.
//
// Consola si fereastra apeleaza AMANDOUA de aici si nu fac nimic in plus in afara de a
// arata rezultatul. Ordinea analizoarelor conteaza doar unde un rezultat il hraneste pe
// altul: tonurile inaintea simularii, stelele si decupajul inaintea tabelului de marimi,
// tabelul inaintea recomandarilor (palierul se judeca la dpi-ul randului de referinta).
//
// Progresul e un IProgress<string> cu numele etapei: fereastra il arata in overlay,
// consola il ignora. Nu e procentaj - etapele au durate prea diferite (JPEG-ul e
// jumatate din tot) ca un procent sa spuna ceva.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AstroPhotoAnalyzer
{
    /// <summary>
    /// O imagine pe care unealta refuza sa o masoare - nu o eroare de citire, ci una de
    /// CONTINUT: e liniara, nu stretch-uita. Consola o raporteaza ca EROARE si iese cu 1.
    /// </summary>
    internal sealed class RefusedImageException : Exception
    {
        internal RefusedImageException(string message) : base(message) { }
    }

    internal sealed class AnalysisResult
    {
        internal string Path;
        internal Parameters Parameters;
        internal Raster Raster;

        internal FileFactsResult File;
        internal BitDepthResult BitDepth;
        internal TonalResult Tonal;
        internal SimulationResult Simulation;
        internal NoiseResult Noise;
        internal BackgroundResult Background;
        internal CropResult Crop;
        internal StarResult Stars;
        internal PrintSizeResult Size;
        internal QuantizationResult Quantization;
        internal JpegResult Jpeg;

        internal IReadOnlyList<Recommendation> Recommendations;
        internal Verdict Overall => Recommendations.Count == 0 ? Verdict.Ok : Recommendations.Max(r => r.Verdict);

        /// <summary>Raportul FARA banner - consola l-a scris deja; fisierul salvat il primeste prin WriteFull.</summary>
        internal string ReportBody;
    }

    internal static class AnalysisRun
    {
        internal static AnalysisResult Execute(string path, Parameters p, IProgress<string> progress = null)
        {
            progress?.Report("citire");
            Raster r = ImageLoader.Load(path);
            if (p.WcsPath != null) r.Source = r.Source with { Wcs = BorrowWcs(p.WcsPath, r) };
            return Execute(r, path, p, progress);
        }

        /// <summary>
        /// WCS-ul unui alt FITS, pentru un raster de aceeasi geometrie (R-027). Un PNG sau TIFF
        /// exportat din Siril e in orientarea afisata, adica FITS-ul BOTTOM-UP rasturnat, deci
        /// rasturnarea WCS-ului urmeaza ROWORDER-ul FITS-ului imprumutat, nu fisierul analizat.
        /// </summary>
        internal static Wcs BorrowWcs(string wcsPath, Raster r)
        {
            if (!File.Exists(wcsPath)) throw new Exception("--wcs: fisierul nu exista: " + wcsPath);
            IReadOnlyDictionary<string, string> hdr; int w, h; bool bottomUp;
            try { (hdr, w, h, bottomUp) = FitsReader.ReadImageHeader(wcsPath); }
            catch (Exception e) { throw new Exception("--wcs: " + e.Message); }
            if (w != r.W || h != r.H)
                throw new Exception($"--wcs: FITS-ul are {Fmt.Dim(w, h)} px, imaginea {Fmt.Dim(r.W, r.H)}: WCS-ul se imprumuta doar intre imagini de aceeasi marime.");
            Wcs wcs = Wcs.TryFrom(hdr, w, h, bottomUp);
            if (wcs == null) throw new Exception("--wcs: FITS-ul n-are WCS (CTYPE RA---TAN / DEC--TAN cu CRPIX, CRVAL si CD, PC + CDELT sau CDELT + CROTA2): " + wcsPath);
            return wcs;
        }

        internal static AnalysisResult Execute(Raster r, string path, Parameters p, IProgress<string> progress = null)
        {
            if (RasterOps.LooksLinear(r, out double median))
                throw new RefusedImageException(
                    $"imaginea pare LINIARA (mediana {Fmt.F4(median)}, sub {Fmt.F2(Thresholds.LinearMedianMax)}): unealta masoara doar imagini finite, stretch-uite. "
                    + "Stretch-uieste in Siril si analizeaza rezultatul.");

            var res = new AnalysisResult { Path = path, Parameters = p, Raster = r };

            progress?.Report("fisier");
            res.File = FileFacts.Of(r);
            res.BitDepth = BitDepthAnalyzer.Analyze(r);
            res.Crop = CropAnalyzer.Analyze(r.W, r.H, p.TargetAspect);

            progress?.Report("tonuri");
            res.Tonal = TonalAnalyzer.Analyze(r, p);
            progress?.Report("praf");
            res.Simulation = PrintStretchSimulator.Simulate(r, res.Tonal, p);

            progress?.Report("zgomot");
            res.Noise = NoiseAnalyzer.Analyze(r, p);

            progress?.Report("fundal");
            res.Background = BackgroundAnalyzer.Analyze(r);

            progress?.Report("stele");
            res.Stars = StarAnalyzer.Analyze(r);
            res.Size = PrintSizeAnalyzer.Analyze(res.Crop.W, res.Crop.H, res.Stars.FwhmMedian, p);

            progress?.Report("cuantizare");
            res.Quantization = QuantizationAnalyzer.Analyze(r);

            progress?.Report("JPEG");
            res.Jpeg = JpegAnalyzer.Analyze(r);

            progress?.Report("raport");
            res.Recommendations = Recommendations.Build(res);
            var sw = new StringWriter();
            ReportWriter.WriteBody(res, sw);
            res.ReportBody = sw.ToString();
            return res;
        }
    }
}
