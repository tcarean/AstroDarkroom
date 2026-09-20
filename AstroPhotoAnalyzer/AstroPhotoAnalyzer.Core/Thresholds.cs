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

// Thresholds.cs - toate pragurile, intr-un singur loc, fiecare cu motivul lui.
//
// Sunt reguli empirice, nu constante fizice. Aproape toate au iesit din masurarea unei
// singure imagini (de_printat.png, Calea Lactee, 16 biti) in conversatia care a nascut
// unealta, si sunt un punct de plecare de calibrat pe mai multe. Cine schimba unul
// schimba si comentariul.

namespace AstroPhotoAnalyzer
{
    internal static class Thresholds
    {
        // ------------------------------------------------------------ intrare

        /// <summary>
        /// Sub mediana asta (pe canalul de referinta, 0..1) imaginea e probabil liniara.
        /// Un stack liniar sta la 0.001-0.01; o imagine finita, chiar intunecata, la
        /// 0.08-0.25. Unealta refuza cu mesaj, nu masoara gresit.
        /// </summary>
        internal const double LinearMedianMax = 0.02;

        /// <summary>
        /// Peste fractia asta de pixeli cu valoarea == octetul_inalt * 257, un fisier de
        /// 16 biti e un 8-bit scalat. Pe 16 biti reali fractia e ~0.4 % (coincidente);
        /// pe un upscale e 100 %.
        /// </summary>
        internal const double UpscaledFraction = 0.5;

        // ------------------------------------------------------------ tonuri

        /// <summary>Nivelurile (din 255) sub care se numara "pixeli intunecati", pentru context.</summary>
        internal const int DarkLevelA = 32;
        internal const int DarkLevelB = 64;

        /// <summary>Sub L*-ul asta hartia separa tonurile, dar ochiul in lumina de camera abia le distinge.</summary>
        internal const double LowSeparationLstar = 15;

        /// <summary>Peste L*-ul asta un pixel e "luminos" - nucleul, stelele.</summary>
        internal const double HighlightLstar = 50;

        /// <summary>Peste procentul asta de pixeli sub negrul hartiei, fundalul e prea jos pentru print.</summary>
        internal const double PaperBlackWarnPct = 20;

        /// <summary>Sub procentul asta, o tinta de fundal e considerata print-safe.</summary>
        internal const double PaperBlackOkPct = 2;

        // ------------------------------------------------------------ subesantionare

        /// <summary>Pasul [::s, ::s] al fiecarei masuratori - cel din scripturile de acceptanta.</summary>
        internal const int TonalStride = 4;
        internal const int PercentileStride = 2;
        internal const int BackgroundStride = 3;

        // ------------------------------------------------------------ zgomot

        internal const int NoiseTileSize = 64;
        internal const int NoiseTileCandidates = 400;

        /// <summary>
        /// O dala e "neteda" pentru zgomot daca max-min pe ea e sub atatea niveluri din
        /// 255. E 6000 din 65535 - pragul scriptului de acceptanta - si e larg dinadins:
        /// diferenta intre pixeli vecini scoate un gradient lent, deci o dala cu putina
        /// structura tot masoara zgomotul corect. (Palierul are alt prag, mai strict.)
        /// </summary>
        internal const double NoiseTileMaxRangeLevels = 6000.0 / 257;

        /// <summary>Sub atatea dale netede sigma nu e de incredere si raportul o spune.</summary>
        internal const int NoiseMinTiles = 10;

        /// <summary>
        /// Dalele PALIERULUI (etapa 3): range sub 6 niveluri si medie sub 70 - cer curat,
        /// fara nucleu - 120 de dale din samanta 1. Sunt criteriile celuilalt script de
        /// acceptanta, si dau 2.6 px pe imaginea de referinta.
        /// </summary>
        internal const double PlateauTileMaxRangeLevels = 6;
        internal const double PlateauTileMaxMeanLevels = 70;
        internal const int PlateauTileCount = 120;
        internal const int PlateauSeed = 1;

        /// <summary>Sub sigma asta (in LSB de 8 biti) zgomotul nu mai dither-eaza cuantizarea: risc de banding.</summary>
        internal const double NoiseBandingLsb = 0.5;

        /// <summary>Intervalul bun pentru versiunea de print: destul de mic sa fie invizibil pe hartie, destul de mare sa dither-eze.</summary>
        internal const double NoisePrintLoLsb = 0.8;
        internal const double NoisePrintHiLsb = 1.5;

        /// <summary>Peste sigma asta imaginea e sub-denoise-uita pentru afisare la 100 %.</summary>
        internal const double NoiseUnderDenoisedLsb = 2.0;

        // ------------------------------------------------------------ export

        /// <summary>Un palier de valori identice mai lung de atat, pe hartie, e un contur vizibil.</summary>
        internal const double PlateauVisibleMm = 1.0;

        /// <summary>Un JPEG cu eroarea maxima peste atatea niveluri nu se recomanda.</summary>
        internal const double JpegMaxErrorReject = 10;

        /// <summary>Pixelii cu eroare JPEG peste atatea niveluri se numara si se localizeaza.</summary>
        internal const double JpegErrorCountLevels = 2;

        /// <summary>Gamma-ul testului de stres shadow-lift (x^(1/g)): un auto-correct tipic de laborator.</summary>
        internal const double ShadowLiftGamma = 1.9;

        /// <summary>Intervalul de niveluri in care se numara golurile din histograma dupa lift.</summary>
        internal const int ShadowLiftLevelLo = 8;
        internal const int ShadowLiftLevelHi = 120;

        /// <summary>Peste atatea niveluri lipsa, master-ul de 16 biti e obligatoriu pentru orice editare.</summary>
        internal const int MissingLevelsWarn = 20;

        // ------------------------------------------------------------ stele

        /// <summary>Fereastra de cautare a maximelor locale (latura, px).</summary>
        internal const int StarWindow = 25;

        /// <summary>O stea trebuie sa fie cu atat peste fundalul ferestrei, in unitati de 16 biti ...</summary>
        internal const double StarMinAmplitude16 = 3000;

        /// <summary>... si sub varful asta, ca sa nu fie saturata (profilul unei stele saturate n-are jumatate).</summary>
        internal const double StarMaxPeak16 = 60000;

        /// <summary>Candidatii se cauta doar peste de atatea ori media imaginii.</summary>
        internal const double StarCandidateFactor = 3;

        /// <summary>Latimi la jumatate acceptate; in afara lor e o pata sau un pixel fierbinte.</summary>
        internal const double StarFwhmMin = 0.8;
        internal const double StarFwhmMax = 15;

        /// <summary>Peste raportul asta colt/centru, colturile sunt moi.</summary>
        internal const double CornerSoftRatio = 1.4;

        /// <summary>Latura unei regiuni de colt, ca fractie din latura imaginii.</summary>
        internal const double RegionFraction = 0.2;

        /// <summary>
        /// Latura regiunii centrale, ca fractie. Mai mare decat coltul, ca sa aiba destule
        /// stele: centrul unei astrofoto e adesea nucleul, unde stelele se pierd in
        /// nebulozitate, iar o mediana pe trei stele nu e o mediana.
        /// </summary>
        internal const double CenterFraction = 0.4;

        /// <summary>Sub atatea stele masurate FWHM-ul global nu e de incredere.</summary>
        internal const int StarMinCount = 20;

        /// <summary>Sub atatea stele intr-o regiune, FWHM-ul ei nu se raporteaza.</summary>
        internal const int RegionMinStars = 5;

        /// <summary>Cati candidati se incearca pentru dalele palierului inainte de a renunta.</summary>
        internal const int PlateauMaxAttempts = 5000;

        /// <summary>Fara FWHM (prea putine stele), marimea se recomanda dupa dpi: cel putin atat.</summary>
        internal const double FallbackMinDpi = 180;

        /// <summary>Calitatile JPEG din tabel. 100 e singura recomandabila; 95 si 90 arata de ce.</summary>
        internal static readonly int[] JpegQualities = { 100, 95, 90 };

        // ------------------------------------------------------------ fundal

        /// <summary>Fundalul = cei mai intunecati atatia pixeli (fractie) ...</summary>
        internal const double BackgroundDarkestFraction = 0.25;

        /// <summary>... fara banda de jos (fractie din inaltime), unde sta orizontul si poluarea lui.</summary>
        internal const double BackgroundExcludeBottomFraction = 0.25;

        /// <summary>O tenta de peste atatea niveluri pe fundal se vede odata ce fundalul e ridicat.</summary>
        internal const double CastWarnLevels = 1.0;

        /// <summary>Un colt cu atatea niveluri peste fundal e gradient rezidual sau lumina de orizont.</summary>
        internal const double CornerGradientLevels = 3.0;

        // ------------------------------------------------------------ marime

        /// <summary>Acuitatea ochiului: 1 minut de arc = 0.29 mm la 1 m. Scaleaza liniar cu distanta.</summary>
        internal const double AcuityMmAt1m = 0.29;

        /// <summary>Distanta implicita de privire a unui tablou: de atatea ori diagonala lui.</summary>
        internal const double ViewingDistanceFactor = 1.5;

        /// <summary>Peste latimea asta (cm) intervin sticla, greutatea si transportul, nu fisierul.</summary>
        internal const double OversizeCm = 100;

        /// <summary>Densitatea "clasica" de referinta, doar pentru a arata ca nu e limita reala.</summary>
        internal const double ReferenceDpi = 300;
    }
}
