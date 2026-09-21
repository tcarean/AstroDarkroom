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

        /// <summary>
        /// Unde sta de obicei cerul (0..1) intr-o imagine facuta pentru ecran, si unde
        /// trebuie sa stea pentru hartie. Reguli de degetul mare din conversatia care a
        /// nascut unealta, nu masuratori: pe ecran un cer la 0.05-0.10 arata "negru
        /// adanc" si bine; pe hartie tot ce e sub negrul ei se printeaza cu acelasi negru,
        /// asa ca versiunea de print vrea cerul la 0.15-0.25, un gri inchis. Imaginea de
        /// referinta avea 0.103 si a iesit print-safe la 0.19. Doar pentru textele de
        /// ajutor ale ferestrei; nicio regula nu se ia dupa ele.
        /// </summary>
        internal const double ScreenBackgroundLo = 0.05;
        internal const double ScreenBackgroundHi = 0.10;
        internal const double PrintBackgroundLo = 0.15;
        internal const double PrintBackgroundHi = 0.25;

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

        /// <summary>
        /// Peste atat la suta din esantioane cu eroare peste JpegErrorCountLevels, q100 primeste
        /// "Atentie" chiar daca eroarea maxima e acceptabila: compresia loveste prea larg, de
        /// obicei in cerul intunecat. Regula de la prima imagine, ca si pragul de 2 niveluri.
        /// </summary>
        internal const double JpegOverThresholdWarnPct = 5;

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

        /// <summary>
        /// Candidatii se cauta peste media planului + amplitudinea minima a unei stele.
        /// Scriptul de acceptanta folosea 3 x media - bine pe un cer la 0.10, dar la un
        /// fundal de 0.25 (versiunea de print!) pragul urca la 0.75 si stelele de 0.55
        /// nu mai trec. Conditia necesara e cea de aici; filtrul adevarat e amplitudinea
        /// fata de fundalul local, care se aplica oricum.
        /// </summary>
        internal const double StarCandidateAboveMean16 = StarMinAmplitude16;

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

        /// <summary>
        /// Fara FWHM (prea putine stele) nu se masoara detaliul, ci doar grila de pixeli:
        /// pasul pixelului trebuie sa fie sub ce desface ochiul. Fara distanta data, pragul e
        /// conventia laboratoarelor pentru un print mare "de calitate" (150-200 dpi): la 180
        /// dpi pasul e 0.14 mm, invizibil de la ~0.5 m - lungimea bratului, cand te apropii
        /// sa te uiti. Cu distanta data pragul e cel fizic, 25.4 / (0.29 x d): 300 dpi = 0.29
        /// m (citit), 180 = 0.49 m, 100 = 0.88 m, 60 = 1.46 m (canapea). E o limita a
        /// fisierului, nu a detaliului - o nebuloasa moale poate cere mult mai putin - deci
        /// nu recomanda un rand.
        /// </summary>
        internal const double FallbackMinDpi = 180;

        /// <summary>Calitatile JPEG din tabel. 100 e singura recomandabila; 95 si 90 arata de ce.</summary>
        internal static readonly int[] JpegQualities = { 100, 95, 90 };

        // ------------------------------------------------------------ fundal

        /// <summary>Fundalul = cei mai intunecati atatia pixeli (fractie) ...</summary>
        internal const double BackgroundDarkestFraction = 0.25;

        /// <summary>... fara banda de jos (fractie din inaltime), unde sta orizontul si poluarea lui.</summary>
        internal const double BackgroundExcludeBottomFraction = 0.25;

        /// <summary>
        /// Regiunile de margine: coltul e atata din inaltime x atata din latime (regiunea din
        /// scriptul de acceptare); laturile sunt benzi de aceeasi grosime, intre colturi.
        /// </summary>
        internal const double EdgeRegionHeightFraction = 0.15;
        internal const double EdgeRegionWidthFraction = 0.20;

        /// <summary>O tenta de peste atatea niveluri pe fundal se vede odata ce fundalul e ridicat.</summary>
        internal const double CastWarnLevels = 1.0;

        /// <summary>Un colt cu atatea niveluri peste fundal e gradient rezidual sau lumina de orizont.</summary>
        internal const double CornerGradientLevels = 3.0;

        /// <summary>
        /// O abatere e COLORATA daca diferenta dintre canale (max - min din delta) trece
        /// de CornerGradientLevels si e cel putin atata fractie din abaterea medie.
        /// Masurat pe imaginea de referinta: coltul de orizont are R +9, B -10 (colorat,
        /// poluare); coltul in care intra Calea Lactee are +11 / +10 / +16 (neutru - e
        /// obiectul, nu un gradient). O regula care confunda obiectul cu defectul e
        /// gresita mai des decat e utila, deci neutru = informativ, colorat = atentie.
        /// </summary>
        internal const double CornerColourSpreadRatio = 0.5;

        // ------------------------------------------------------------ marime

        /// <summary>Acuitatea ochiului: 1 minut de arc = 0.29 mm la 1 m. Scaleaza liniar cu distanta.</summary>
        internal const double AcuityMmAt1m = 0.29;

        /// <summary>
        /// Distanta implicita de privire a unui tablou: de atatea ori diagonala lui. Regula de
        /// degetul mare a printului si a inramarii, "1-2 x diagonala", are doua repere: 1 x
        /// diagonala e distanta de la care perspectiva e cea a obiectivului (centrul de
        /// perspectiva al unui obiectiv normal, cu focala cat diagonala cadrului), iar 2 x e
        /// practica de galerie, de unde tabloul se cuprinde intreg fara sa misti capul. La
        /// 1.5 x tabloul subintinde ~37 de grade pe diagonala: intreg in campul central, dar
        /// destul de aproape cat sa te uiti la detaliu. E o prezumtie despre om, nu o
        /// masuratoare a imaginii; diagonala e a imaginii, nu a ramei.
        /// </summary>
        internal const double ViewingDistanceFactor = 1.5;

        /// <summary>
        /// La distanta NATURALA, "steaua e punct" nu depinde de marime: si distanta de la
        /// care steaua devine punct, si distanta naturala cresc proportional cu latimea,
        /// iar raportul lor e fwhm x 1000 / (Wpx x 0.29 x 1.5 x sqrt(1 + r^2)) - o
        /// constanta a imaginii. Pe imaginea de referinta e 1.018: toate marimile "picau"
        /// cu 2 %, si regula cadea pe cea mai mica; cu FWHM 3.00 in loc de 3.13 ar fi
        /// trecut toate. O toleranta de 15 % (cat imprastierea estimatorului de FWHM)
        /// face testul sa raspunda la intrebarea pe care chiar o poate: e imaginea destul
        /// de clara pentru distanta ei naturala, da sau nu. Marimea o alege peretele.
        /// </summary>
        internal const double NaturalDistanceTolerance = 1.15;

        /// <summary>Peste latimea asta (cm) intervin sticla, greutatea si transportul, nu fisierul.</summary>
        internal const double OversizeCm = 100;

        /// <summary>
        /// Pasul pixelului unui monitor obisnuit (96-110 ppi), pentru regula pasului inapoi
        /// din Preview: la 100 % fisierul se vede ca un print de Wpx x atat. O prezumtie
        /// despre ecran, nu o masuratoare; un 4K de 27" are 0.16 mm si vederea e cu atat
        /// mai aspra.
        /// </summary>
        internal const double ScreenPixelMm = 0.25;

        /// <summary>
        /// Passepartout-ul trebuie sa fie cel putin de atatea ori latimea ramei: sub asta
        /// rama "strange" imaginea si albul din jur nu mai are ce sa faca. Regula
        /// inramatorilor; 0 inseamna fara passepartout si nu e verificat.
        /// </summary>
        internal const double PassepartoutMinFrameRatio = 2;

        /// <summary>Densitatea "clasica" de referinta, doar pentru a arata ca nu e limita reala.</summary>
        internal const double ReferenceDpi = 300;
    }
}
