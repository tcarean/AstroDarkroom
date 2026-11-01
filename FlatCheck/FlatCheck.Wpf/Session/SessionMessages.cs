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

// SessionMessages.cs - the explanations the application gives when the input is not right.
//
// They sit in a single place, like the texts in the core's Report/, and for the same reason:
// they are the only thing the user reads when something does not work, so they deserve to be
// written once and well, not improvised in three places of the interface. Being here and not
// in the XAML also makes them checkable - a test can ask that the message name the Siril
// script, which is the requirement itself, not a preference of wording.
//
// It also holds the map's guide - see the note at MapGuideIntro. It is not an error message,
// but it is the same kind of text: documentation shown in the window, written once and well.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using static FlatCheck.Ui;

namespace FlatCheck.Wpf
{
    /// <summary>
    /// The texts the window builds in code that the core does not write, each a
    /// <c>T(english, romanian)</c> pair (the fixed labels and headings stay in the XAML, as
    /// <c>{l:T}</c>): the session problems, the guides, the button
    /// labels, the Siril run's and the Split tab's messages, the "Despre" window's headings.
    /// </summary>
    internal static class SessionMessages
    {
        /// <summary>The problem card's title.</summary>
        /// <param name="p">The session problem.</param>
        /// <returns>The title, or "" for <see cref="SessionProblem.None"/>.</returns>
        internal static string Title(SessionProblem p) => p switch
        {
            SessionProblem.NoFolder => T("The folder does not exist",
                                         "Folderul nu exista"),
            SessionProblem.NoProcessDir => T("No preprocessing script has been run",
                                             "Nu s-a rulat niciun script de preprocesare"),
            SessionProblem.NoFlatMaster => T("The flat master is missing",
                                             "Lipseste masterul de flat"),
            _ => "",
        };

        /// <summary>
        /// R-080 and R-088: when the master is missing, the application does not only say "I
        /// cannot find the file" - it says what the user should do to make it appear, that is
        /// run the script. A message that only describes the symptom leaves the user to find
        /// alone the link between a missing file and a script they may never have run.
        /// </summary>
        /// <param name="s">The session, for its paths.</param>
        /// <param name="p">The problem.</param>
        /// <returns>The problem card's text, or "" for
        /// <see cref="SessionProblem.None"/>.</returns>
        internal static string Explain(SirilSession s, SessionProblem p) => p switch
        {
            SessionProblem.NoFolder =>
                T($"Cannot find the folder:\n{s.Root}\n\n"
                + "Choose the session's working folder - the one that holds the subfolders "
                + "flats/, darks/, biases/, lights/ and process/ or masters/.",
                  $"Nu gasesc folderul:\n{s.Root}\n\n"
                + "Alege folderul de lucru al sesiunii - cel care contine subfolderele "
                + "flats/, darks/, biases/, lights/ si process/ sau masters/."),

            SessionProblem.NoProcessDir =>
                T($"The folder chosen has neither a process/ nor a masters/ subfolder:\n{s.Root}\n\n"
                + "That is where Siril puts the results of the calibration, so their absence means that "
                + "no preprocessing script has been run yet.\n\n"
                + $"Run the script {SirilSession.PreprocessingScript} - from here, with the button "
                + "below, or from Siril, and then choose the folder again.",
                  $"Folderul ales nu are nici subfolderul process/, nici masters/:\n{s.Root}\n\n"
                + "Acolo isi pune Siril rezultatele calibrarii, deci lipsa lor inseamna ca "
                + $"nu s-a rulat inca niciun script de preprocesare.\n\n"
                + $"Ruleaza scriptul {SirilSession.PreprocessingScript} - de aici, cu butonul de "
                + "mai jos, sau din Siril si apoi alege din nou folderul."),

            SessionProblem.NoFlatMaster =>
                T($"Cannot find the flat master, {SirilSession.FlatMasterName}, in either of "
                + "the two places the Siril scripts leave it:\n"
                + string.Join("\n", s.FlatMasterCandidates) + "\n\n"
                + "That is the file the preprocessing script produces: the flats "
                + "stacked and CALIBRATED (the prefix \"pp_\" means exactly that the flat-dark and the "
                + "offset have already been subtracted). Every structure test starts from it.\n\n"
                + $"Run {SirilSession.PreprocessingScript} - from here, with the button below, "
                + "or from Siril, and then choose the folder again.",
                  $"Nu gasesc masterul de flat, {SirilSession.FlatMasterName}, in niciunul din "
                + "cele doua locuri in care il lasa scripturile Siril:\n"
                + string.Join("\n", s.FlatMasterCandidates) + "\n\n"
                + "Asta e fisierul pe care il produce scriptul de preprocesare: flat-urile "
                + "stivuite si CALIBRATE (prefixul \"pp_\" inseamna exact ca flat-dark-ul si "
                + "offsetul au fost deja scazute). Toate testele de structura pornesc de la el.\n\n"
                + $"Ruleaza {SirilSession.PreprocessingScript} - de aici, cu butonul de mai jos, "
                + "sau din Siril si apoi alege din nou folderul."),

            _ => "",
        };

        // ------------------------------------------------------- the waiting screen
        //
        // A run a click starts waits for the gate another run holds (ADR 0061; since R-120 the
        // tabs are not on screen while the opening chain holds it, so the other run is another
        // click's); while it waits, the waiting screen says that and not the run's own message,
        // which would claim work that has not begun.

        /// <summary>The waiting screen's first line while a click-run waits for the gate another
        /// run holds.</summary>
        /// <returns>The line.</returns>
        internal static string WaitingForRunningAnalysis() =>
            T("Waiting for the analysis that is running to finish...",
              "Astept sa se termine analiza care ruleaza...");

        /// <summary>
        /// The master exists both in masters/ and in process/: two preprocessings on the same
        /// folder. The newest is measured, and the user must know which, because the two can
        /// come from different sets of flats.
        /// </summary>
        /// <param name="m">The lookup, with both paths.</param>
        /// <returns>The warning, naming the master measured and the one ignored.</returns>
        internal static string TwoFlatMasters(FlatMasterLookup m) =>
            T("There are TWO flat masters in this folder, in masters/ and in process/ - two "
            + "preprocessing runs. The newest is measured:\n" + m.Path + "\n"
            + "The other, older one is not read:\n" + m.Other + "\n"
            + "If it is not the one you want, delete or rename the other one and "
            + "choose the folder again.",
              "Sunt DOUA mastere de flat in folderul asta, in masters/ si in process/ - doua "
            + "rulari de preprocesare. Se masoara cel mai nou:\n" + m.Path + "\n"
            + "Celalalt, mai vechi, nu e citit:\n" + m.Other + "\n"
            + "Daca nu e cel pe care il vrei, sterge-l sau redenumeste-l pe celalalt si "
            + "alege din nou folderul.");

        /// <summary>When process/ holds no calibrated frames for the end-to-end test.</summary>
        /// <param name="s">The session, for its process/ path.</param>
        /// <returns>The explanation, with the pattern and the folder.</returns>
        internal static string NoLightSeries(SirilSession s) =>
            T($"No {SeriesFinder.DefaultPattern} frame found in:\n"
            + $"{s.ProcessDir}\n\n"
            + "The test needs the lights Siril calibrated with the flat, before alignment and "
            + "without background extraction.\n\n"
            + "The aligned frames (r_pp_light_*) are left out on purpose: alignment shifts them, "
            + "the vignetting would no longer sit at the centre of the frame, and the measurement "
            + "would come out wrong without showing it.",
              $"Nu am gasit niciun cadru {SeriesFinder.DefaultPattern} in:\n"
            + $"{s.ProcessDir}\n\n"
            + "Testul are nevoie de lights-urile pe care Siril le-a calibrat cu flat-ul, inainte "
            + "de aliniere si fara extragere de fond.\n\n"
            + "Cadrele aliniate (r_pp_light_*) nu sunt folosite dinadins: alinierea le deplaseaza, "
            + "vignetarea nu ar mai fi in centrul cadrului si masuratoarea ar iesi gresita fara "
            + "sa se vada.");

        /// <summary>When flats/ holds no camera raw.</summary>
        /// <param name="s">The session, for its flats/ path.</param>
        /// <returns>The explanation, with the folder.</returns>
        internal static string NoRawFlats(SirilSession s) =>
            T($"No camera raw file in:\n{s.FlatsDir}\n\n"
            + "The exposure check works on a flat as it came out of the camera, uncalibrated: a "
            + "master will not do, because stacking normalises the level, and the level is exactly "
            + "what is measured here.\n\n"
            + "The simplest is to leave the camera's own files in flats/ "
            + "(.nef, .cr2, .cr3, .arw, .dng, .raf, .pef): on them the white and the black points "
            + "are read exactly from the file.",
              $"Niciun fisier raw de aparat in:\n{s.FlatsDir}\n\n"
            + "Verificarea expunerii se face pe un flat asa cum a iesit din aparat, necalibrat: "
            + "masterul nu merge, pentru ca la stivuire nivelul se normalizeaza, iar exact nivelul "
            + "e ce se masoara aici.\n\n"
            + "Cel mai simplu e sa lasi in flats/ chiar fisierele de la aparat "
            + "(.nef, .cr2, .cr3, .arw, .dng, .raf, .pef): pe ele punctul de alb si cel de negru "
            + "se citesc exact din fisier.");

        // --------------------------------------------------- how to read the tab
        //
        // The question "is it dubious?" has two halves, and only one is the eye's. This text
        // exists because the split does not show from the screen's layout: the figures on the
        // left and the map on the right look like two views of the same thing, when in fact
        // one decides and the other explains.

        /// <summary>What the figures decide and what the eye decides - the intro above the two
        /// rows.</summary>
        /// <returns>One sentence.</returns>
        internal static string HowToReadIntro() =>
            T("The tab answers two questions: how large the flat's deviation is, which the figures say, "
            + "and what kind it is, which the map shows.",
              "Tabul raspunde la doua intrebari: cat de mare e abaterea flat-ului, pe care o spun "
            + "cifrele, si de ce fel e, pe care o arata harta.");

        /// <summary>The two halves of the reading, in the order they are used.</summary>
        /// <returns>Two rows, heading and text.</returns>
        internal static IReadOnlyList<(string Shape, string Meaning)> HowToReadRows() => new[]
        {
            (T("HOW MUCH - the figures decide, not the map",
               "CAT DE MULT - decid cifrele, nu harta"),
             T("\"Residual (radial model)\" says how much of the frame does not look like a usual "
             + "vignetting, symmetric about the centre, on the scale below. Under 2 % it does not matter "
             + "what shape it has. Between 2 and 10 % Siril's background extraction removes what is left, "
             + "whatever the shape. The verdict never depends on what you see in the map.",
               "\"Reziduu dupa model rad.\" spune cat din cadru nu arata ca o vignetare obisnuita, "
             + "simetrica in jurul centrului, pe scara de mai jos. Sub 2 % nu conteaza ce forma are. "
             + "Intre 2 si 10 % extragerea de fond din Siril scoate ce ramane, oricare ar fi forma. "
             + "Verdictul nu depinde niciodata de ce vezi in harta.")),

            (T("WHAT KIND - here the eye comes in, but not from scratch",
               "CE FEL - aici intra ochiul, dar nu de la zero"),
             T("The shape says what to change for the next flats, not whether the flat is good. The "
             + "figures help before you look: if \"tilt\" is close to the residual, one side of the frame "
             + "is brighter than the other, and the map says in which direction; if \"worst spot\" is far "
             + "above the residual, it is something small and compact, a light leak or condensation, and "
             + "\"local spots\" says where; if the dust warning appeared, the question is no longer whether "
             + "the flat is good, but what you use it for: the session whose lights have the same spot, "
             + "or as a library flat. For the rest there is the guide under the map.",
               "Forma spune ce sa schimbi la urmatoarele flat-uri, nu daca flat-ul e bun. Cifrele te "
             + "ajuta inainte sa te uiti: daca \"inclinare\" e aproape de reziduu, o parte a cadrului e "
             + "mai luminoasa decat cealalta, si harta spune in ce directie; daca \"pata maxima\" e mult "
             + "peste reziduu, e ceva mic si compact, o scurgere de lumina sau condens, si \"pete locale\" "
             + "spune unde; daca a aparut atentionarea de praf, intrebarea nu mai e daca flat-ul e bun, "
             + "ci la ce il folosesti: la sesiunea ale carei lights au aceeasi pata, sau ca flat de "
             + "biblioteca. Pentru restul e ghidul de sub harta.")),
        };

        // --------------------------------------------------- the histograms' guide
        //
        // The same kind of text as the map's guide below: documentation shown in the window,
        // folded, written once. And the same limit - it explains what is seen, it does NOT
        // classify. The verdict stays the median of the central window of the brightest
        // channel, and nothing in the drawing votes.
        //
        // The table mirrors the README's, in its window section under "Exposure", which carries
        // the English text. There is NO test linking the two - if you reword here, go there too.

        /// <summary>What the axis is and what role the drawing has, before the list of
        /// readings.</summary>
        /// <returns>The guide's intro.</returns>
        internal static string HistogramGuideIntro() =>
            T("The horizontal axis is the same one every figure of the tab sits on: how much of the "
            + "sensor's useful range, from the camera's black (0) to saturation (100). On the camera's "
            + "preview the axis is percent of 255, again \"how much of the available range\", the only "
            + "way the two can be read side by side.\n\n"
            + "The drawing EXPLAINS, it does not vote. The verdict is the level at the middle of the "
            + "frame on the brightest channel, measured on the raw data, and nothing else; the "
            + "histograms show where that figure comes from and what is around it.",
              "Axa orizontala e aceeasi pe care stau toate cifrele tabului: cat la suta din "
            + "intervalul util al senzorului, de la negrul aparatului (0) la saturatie (100). Pe "
            + "previzualizarea aparatului axa e in procente din 255, tot \"cat din intervalul "
            + "disponibil\", singurul fel in care cele doua se pot citi una langa alta.\n\n"
            + "Desenul EXPLICA, nu voteaza. Verdictul e nivelul din mijlocul cadrului pe canalul "
            + "cel mai luminos, masurat pe datele brute, si nimic altceva; histogramele arata de "
            + "unde vine cifra aceea si ce e in jurul ei.");

        /// <summary>
        /// How the drawing is read, in the order it is worth looking: first the two curves,
        /// then the figure that decides, then the bands, then the traps.
        /// </summary>
        /// <returns>The guide's rows, heading and text.</returns>
        internal static IReadOnlyList<(string Shape, string Meaning)> HistogramGuideRows() => new[]
        {
            (T("two curves on each channel",
               "doua curbe pe fiecare canal"),
             T("The pale, wide one is the whole frame; the solid, narrow one is only the middle of the "
             + "frame (20 %), where the verdict is taken from. Each is drawn to its own full height, "
             + "so the HEIGHTS are not compared with each other; the positions on the axis are.",
               "Cea palida si lata e tot cadrul; cea plina si ingusta e doar mijlocul cadrului "
             + "(20 %), de unde se ia verdictul. Fiecare e desenata la inaltimea ei maxima, deci "
             + "INALTIMILE nu se compara intre ele; pozitiile pe axa se compara.")),

            (T("the vertical line",
               "linia verticala"),
             T("The typical level at the middle of the frame, the one figure that decides. On the "
             + "brightest channel it is the very number in the verdict pill above.",
               "Nivelul tipic din mijlocul cadrului, singura cifra care decide. Pe canalul cel mai "
             + "luminos e chiar numarul din pastila de verdict de sus.")),

            (T("the green band",
               "banda verde"),
             T("The target, from exactly the same thresholds as the verdict. It moves when you tick "
             + "\"Sky flat\": a sky flat MUST sit lower, because the sky fades continuously, and the "
             + "master comes out clean from the number of frames, not from the exposure.",
               "Tinta, din exact aceleasi praguri ca verdictul. Se muta cand bifezi \"Sky flat\": "
             + "un flat pe cer TREBUIE sa stea mai jos, fiindca cerul se stinge continuu, iar "
             + "masterul iese curat din numarul de cadre, nu din expunere.")),

            (T("the yellow and red bands",
               "benzile galbene si rosii"),
             T("The same scale further on: yellow \"acceptable, but\", red too low or too "
             + "high. On a sky flat there is no yellow on the left: there the target starts right at "
             + "the too-low threshold.",
               "Aceeasi scara mai departe: galben \"acceptabil, dar\", rosu prea jos sau prea "
             + "sus. La flat pe cer nu exista galben in stanga: acolo tinta incepe chiar de la "
             + "pragul de prea jos.")),

            (T("the red stripe on the right",
               "dunga rosie din dreapta"),
             T("The last 2 % before saturation: there the burnt pixels are counted. If any part of "
             + "the solid curve reaches that stripe, the verdict is TOO HIGH, however well the middle "
             + "figure sits: there the vignetting information is lost, exactly where the signal is "
             + "largest.",
               "Ultimele 2 % pana la saturatie: acolo se numara pixelii arsi. Daca o parte din "
             + "curba plina ajunge in dunga aceea, verdictul e PREA SUS, oricat de bine ar sta "
             + "cifra din mijloc: acolo se pierde exact informatia de vignetare, unde semnalul e "
             + "cel mai mare.")),

            (T("why the pale curve is much wider",
               "de ce curba palida e mult mai lata"),
             T("That is the vignetting, not underexposure: the corners of a flat ARE darker, that is "
             + "the lens, and the distance between the two peaks is just how deep the vignetting is. "
             + "If you expose by the wide curve, you burn the centre and lose exactly what you measure.",
               "Aceea e vignetarea, nu subexpunere: colturile unui flat SUNT mai intunecate, asa e "
             + "obiectivul, iar distanta dintre cele doua varfuri e chiar cat de adanca e "
             + "vignetarea. Daca expui dupa curba lata, arzi centrul si pierzi exact ce masori.")),

            (T("why the channels peak in different places",
               "de ce canalele au varfuri diferite"),
             T("The coloured filters on the sensor pass different amounts of the source's light. On "
             + "a warm panel red may lead, on the sky at the blue hour usually blue. The brightest "
             + "reaches saturation first, so it limits the exposure. The program does no white "
             + "balance; that is exactly the point.",
               "Filtrele colorate de pe senzor lasa sa treaca cantitati diferite din lumina "
             + "sursei. Pe un panou cald poate fi in fata rosul, pe cer la ora albastra de obicei "
             + "albastrul. Cel mai luminos ajunge primul la saturatie, deci el limiteaza expunerea. "
             + "Programul nu face balans de alb; tocmai asta e ideea.")),

            (T("what this drawing is NOT",
               "ce NU e desenul asta"),
             T("It is not the camera's histogram. That one is of the JPEG image the camera processed "
             + "(colours, white balance, tone curve) and sits much further right. That is why a frame "
             + "that looks \"at the limit\" on the camera's screen can be at 40 % in the raw data.",
               "Nu e histograma de pe aparat. Aceea e a imaginii JPEG prelucrate de aparat (culori, "
             + "balans de alb, curba de ton) si sta mult mai la dreapta. De aceea un cadru care "
             + "pare \"la limita\" pe ecranul aparatului poate fi la 40 % in datele brute.")),

            (T("the switch above the drawings",
               "comutatorul de deasupra desenelor"),
             T("It chooses what you look at: the raw data, which the program measures, or the "
             + "preview the camera put into the raw file, the very image it made its on-screen "
             + "histogram from. Only the raw view has the target band, because only it says "
             + "something about the light on the sensor. On the other there is no target, nor could "
             + "there be: it would mean exposing by the screen. The table under the drawings puts "
             + "the two figures side by side, on each channel.",
               "Alege la ce te uiti: datele brute, pe care le masoara programul, sau "
             + "previzualizarea pe care aparatul a pus-o in fisierul raw, chiar imaginea din care "
             + "si-a facut histograma de pe ecran. Numai vederea bruta are banda tinta, fiindca "
             + "numai ea spune ceva despre lumina de pe senzor. Pe cealalta nu e nicio tinta, si "
             + "nici n-ar putea fi: ar insemna sa expui dupa ecran. Tabelul de sub desene pune "
             + "cele doua cifre una langa alta, pe fiecare canal.")),

            (T("why the screen is much further right",
               "de ce ecranul e mult mai la dreapta"),
             T("The camera processes the image for the screen with a curve that brightens the low and "
             + "middle tones the most, exactly where a flat sits; no setting removes it, every JPEG "
             + "is made that way. And, on the cameras measured here, the screen saturated before the "
             + "sensor, by a third of a stop up to a stop. So the screen is a one-way alarm: if the "
             + "histogram is not touching the right edge, nothing is burnt for sure; if it is "
             + "touching, you know nothing. It is not a measuring instrument.",
               "Aparatul prelucreaza imaginea pentru ecran cu o curba care lumineaza cel mai tare "
             + "tonurile de jos si de mijloc, exact unde sta un flat; nicio setare nu o scoate, asa "
             + "e facut orice JPEG. Si, pe aparatele masurate aici, ecranul s-a saturat inaintea "
             + "senzorului, cu o treime de stop pana la un stop. Deci ecranul e o alarma intr-un "
             + "singur sens: daca histograma nu e lipita de dreapta, sigur nu ai ars nimic; daca e "
             + "lipita, nu stii nimic. Nu e un instrument de masura.")),
        };

        // ------------------------------------------------------- the map's guide
        //
        // WHY IT IS DOCUMENTATION AND NOT A DIAGNOSIS. The tool does not classify the map's
        // shape and must not: a spot can be a light leak or a cloud, and from the pixels it
        // cannot be told apart. What can be done is tell the user what each shape meant on real
        // masters, and which test separates the cases. So: rows of text, no verdict, no
        // threshold, no figure computed here.
        //
        // The text mirrors the README's table under "What the shapes mean", in English. There is
        // NO test linking the two - if you reword here, go there too.

        /// <summary>The guide's introduction: how the map is read, before the list of
        /// shapes.</summary>
        /// <returns>The guide's intro.</returns>
        internal static string MapGuideIntro() =>
            T("The map shows what is left of the flat once the usual vignetting, the one symmetric about "
            + "the centre, is taken out: white means exactly as much as it predicts, red brighter, blue "
            + "darker. So what you see in colour can NOT be vignetting.\n\n"
            + "Read the figures first, which say HOW MUCH, and only then the shape, which says WHY. "
            + "How strong the colours are depends on the scale chosen above: on \"auto\" they always "
            + "fill the whole range, even when the figures are tiny; look at the legend.",
              "Harta arata ce ramane din flat dupa ce se scoate vignetarea obisnuita, cea simetrica in "
            + "jurul centrului: alb inseamna exact atat cat prezice ea, rosu mai luminos, albastru mai "
            + "intunecat. Deci ce vezi colorat NU poate fi vignetare.\n\n"
            + "Citeste intai cifrele, care spun CAT, si abia apoi forma, care spune DE CE. Cat de tari "
            + "sunt culorile depinde de scara aleasa mai sus: pe \"auto\" ele umplu mereu tot intervalul, "
            + "chiar si cand cifrele sunt minuscule; uita-te la legenda.");

        /// <summary>
        /// The shapes met on the map and what they meant, in the order they are worth looking
        /// for: from the most common and most harmless, towards those that ask for a particular
        /// counter-test.
        /// </summary>
        /// <returns>The guide's rows, shape and meaning.</returns>
        internal static IReadOnlyList<(string Shape, string Meaning)> MapGuideShapes() => new[]
        {
            (T("blotches with no direction",
               "pete fara directie"),
             T("Small deviations, with no particular direction: the diffuser's mark and the noise. "
             + "Background extraction removes them without effort; the figures say whether they can be "
             + "ignored.",
               "Abateri mici, fara o directie anume: urma difuzorului si zgomotul. Extragerea de fond "
             + "le scoate fara efort; cifrele spun daca pot fi ignorate.")),

            (T("one side blue, the other red",
               "o parte albastra, cealalta rosie"),
             T("One side of the frame is brighter than the other: the same thing \"tilt\" measures. An "
             + "uneven light source, light from around, or something lying askew over the lens. Whether "
             + "it comes from the world (the sky) or from the camera (the diffuser, the lens hood) one "
             + "master cannot say; a second session with the same lens can.",
               "O parte a cadrului e mai luminoasa decat cealalta: acelasi lucru pe care il masoara "
             + "\"inclinare\". Sursa de lumina neuniforma, lumina din jur, sau ceva asezat stramb peste "
             + "obiectiv. Daca vine din lume (cerul) sau de la aparat (difuzorul, parasolarul) un singur "
             + "master nu poate spune; o a doua sesiune cu acelasi obiectiv poate.")),

            (T("concentric circles or arcs",
               "cercuri sau arce concentrice"),
             T("A ring centred on the sensor cannot stay in the map: it is taken out with the vignetting. "
             + "So if you see one, it is centred elsewhere: something round in front of the lens is not on "
             + "the axis, or the light is not diffuse. It has not been seen on a real master yet.",
               "Un inel centrat pe senzor nu poate ramane in harta: e scos odata cu vignetarea. Deci daca "
             + "vezi unul, e centrat in alta parte: ceva rotund din fata obiectivului nu e pe axa, sau "
             + "lumina nu e difuza. Pe un master real nu s-a vazut inca.")),

            (T("a local spot in a corner or at an edge",
               "pata locala intr-un colt sau la o margine"),
             T("A light leak: through the viewfinder, at the mount, past the diffuser. It belongs to the "
             + "camera, so it comes back in every session until you find its cause.",
               "O scurgere de lumina: prin vizor, pe la montura, pe langa difuzor. Tine de aparat, deci "
             + "revine in fiecare sesiune pana ii gasesti cauza.")),

            (T("a round spot in the centre",
               "pata rotunda in centru"),
             T("Extra light right in the middle: a panel too close, the light behind a monitor. When it "
             + "is exactly centred it does not appear on the map and no test sees it, because it looks "
             + "exactly like vignetting; measured, a 10 % spot in the centre leaves the map empty and "
             + "every figure under 0.1 %. Its only trace is the vignetting figure, which grows. It shows "
             + "only when it is slightly off-centre, and then as a crescent, not as a disc. Turning the "
             + "camera does not remove it; the test is distance: one set of flats at 30 cm, one at 60.",
               "Lumina in plus chiar in mijloc: un panou prea aproape, lumina din spatele unui monitor. "
             + "Cand e exact in centru nu apare pe harta si nu o vede niciun test, pentru ca seamana "
             + "perfect cu vignetarea; masurat, o pata de 10 % in centru lasa harta goala si toate "
             + "cifrele sub 0.1 %. Singura ei urma e cifra de vignetare, care creste. Se vede doar cand "
             + "e putin descentrata, si atunci ca o semiluna, nu ca un disc. Rotirea aparatului nu o "
             + "scoate; proba e distanta: un set de flat-uri la 30 cm, unul la 60.")),

            (T("fine grain everywhere",
               "granulatie fina peste tot"),
             T("Noise, not a defect: too few frames or too low an exposure. The figures stay small; "
             + "there is nothing to fix at the source, only more frames to stack.",
               "Zgomot, nu un defect: prea putine cadre sau expunere prea mica. Cifrele raman mici; nu "
             + "e nimic de reparat la sursa, doar de stivuit mai multe cadre.")),
        };

        // --------------------------------------------------- the histogram tab

        /// <summary>
        /// When there is neither a master nor any readable frame in flats/. Through
        /// MainViewModel this is not reached - Check() requires the master - but the tab is
        /// attached in the tests too.
        /// </summary>
        /// <param name="s">The session, for its flats/ path.</param>
        /// <returns>The explanation, with the folder.</returns>
        internal static string NoFlatFrames(SirilSession s) =>
            T($"Nothing to equalise: neither the master {SirilSession.FlatMasterName} in masters/ or process/, nor "
            + $"any readable frame in:\n{s.FlatsDir}\n\n"
            + "The tab shows the frames as they were shot - camera raw (.nef, .cr2, .cr3, "
            + ".arw, .dng, .raf, .pef) or FITS (.fit, .fits, .fts) - and, in the first place, the master "
            + "made from them, so that it shows what was averaged out and what stayed.",
              $"Nimic de egalizat: nici masterul {SirilSession.FlatMasterName} in masters/ sau process/, nici "
            + $"vreun cadru citibil in:\n{s.FlatsDir}\n\n"
            + "Tabul arata cadrele asa cum au fost trase - raw de aparat (.nef, .cr2, .cr3, "
            + ".arw, .dng, .raf, .pef) sau FITS (.fit, .fits, .fts) - si, pe prima pozitie, masterul "
            + "facut din ele, ca sa se vada ce s-a mediat si ce a ramas.");

        // The guide below mirrors the table in the README's window section ("Histogram"), in
        // English. There is no test linking the two - if you reword here, go there too.

        /// <summary>What the picture is, before the list of readings.</summary>
        /// <returns>The guide's intro.</returns>
        internal static string HistEqGuideIntro() =>
            T("It is Siril's \"Histogram\" view, redone: the image's levels are redistributed so "
            + "that every grey step holds the same number of pixels. It is not a graph and not a "
            + "measurement. On a flat, whose levels sit in a narrow range, this stretches exactly "
            + "the vignetting's range to the maximum, and everything that is not uniform comes out "
            + "at maximum contrast.\n\n"
            + "The picture EXPLAINS, it does not vote: no figure of the program changes with what "
            + "shows here, and no shape here enters any verdict.",
              "E vederea \"Histogram\" din Siril, refacuta: nivelurile imaginii sunt reimpartite "
            + "astfel incat fiecare treapta de gri sa aiba acelasi numar de pixeli. Nu e un grafic "
            + "si nu e o masuratoare. Pe un flat, ale carui niveluri stau intr-un interval ingust, "
            + "asta intinde la maximum exact intervalul vignetarii, si tot ce nu e uniform iese la "
            + "contrast maxim.\n\n"
            + "Poza EXPLICA, nu voteaza: nicio cifra din program nu se schimba cu ce se vede "
            + "aici, si nicio forma de aici nu intra in vreun verdict.");

        /// <summary>How the picture is read, in the order it is worth looking.</summary>
        /// <returns>The guide's rows, heading and text.</returns>
        internal static IReadOnlyList<(string Shape, string Meaning)> HistEqGuideRows() => new[]
        {
            (T("why a clean flat looks noisy",
               "de ce un flat curat arata zgomotos"),
             T("The stretch is applied to ANY frame, even one with no defect at all. Then what is "
             + "left to stretch is the noise, and it reaches maximum contrast. Fine grain everywhere "
             + "means \"it has nothing else to show\".",
               "Intinderea se face pe ORICE cadru, si pe unul fara niciun defect. Atunci ce ramane "
             + "de intins e zgomotul, si el ajunge la contrast maxim. Granulatie fina peste tot "
             + "inseamna \"nu are altceva de aratat\".")),

            (T("why the size of a shape cannot be read",
               "de ce nu se poate citi cat de mare e o forma"),
             T("Every grey step gets the same number of pixels, however close their values were. A "
             + "ripple of 0.5 % and one of 10 % with the same shape come out identical. How large a "
             + "shape is, the figures of the Structure tab say, not the picture here.",
               "Fiecare treapta de gri primeste acelasi numar de pixeli, indiferent cat de "
             + "apropiate erau valorile lor. O ondulatie de 0.5 % si una de 10 % cu aceeasi forma "
             + "ies identic. Cat de mare e o forma spun cifrele din tabul Structura, nu poza de "
             + "aici.")),

            (T("the Mosaic view (the default)",
               "vederea Mozaic (implicita)"),
             T("The whole raw frame, with the colour channels not separated, stretched as a single "
             + "layer: exactly what Siril shows on a raw opened the way it opens it by default; that "
             + "is why it comes first. The three colours sit mixed in the same histogram, so at "
             + "100 % the sensor's checkerboard shows, as in Siril; shrunk, it smooths out.",
               "Tot cadrul brut, cu canalele de culoare neseparate, intins ca un singur strat: "
             + "exact ce arata Siril pe un raw deschis asa cum il deschide implicit; de asta e "
             + "prima. Cele trei culori stau amestecate in aceeasi histograma, deci la 100 % se "
             + "vede tabla de sah a senzorului, ca in Siril; micsorata, se netezeste.")),

            (T("the RGB view",
               "vederea RGB"),
             T("The sensor's four channels, each stretched on its own and put in its colour, like "
             + "Siril on an image with the colours separated. It has more contrast than Mosaic, "
             + "because each channel is stretched over its whole range. A uniform tint means "
             + "nothing; a tint that CHANGES across the frame means that one channel has another "
             + "shape than the others: a source whose colour is not the same everywhere, a coloured "
             + "light leak.",
               "Cele patru canale ale senzorului, fiecare intins separat si pus in culoarea lui, "
             + "ca Siril pe o imagine cu culorile separate. Are mai mult contrast decat Mozaicul, "
             + "fiindca fiecare canal e intins pe tot intervalul lui. O nuanta uniforma nu "
             + "inseamna nimic; o nuanta care SE SCHIMBA pe cadru inseamna ca un canal are alta "
             + "forma decat celelalte: o sursa a carei culoare nu e aceeasi peste tot, o scurgere "
             + "de lumina colorata.")),

            (T("the per-channel views",
               "vederile pe canal"),
             T("The same picture, a single channel, in grey. Useful when the composite is hard to read "
             + "or when you want to see whether a shape is in every channel or only in one.",
               "Aceeasi poza, un singur canal, in gri. Utile cand compozitul e greu de citit "
             + "sau cand vrei sa vezi daca o forma e in toate canalele sau doar intr-unul.")),

            (T("the False colours chip",
               "chipul Culori false"),
             T("Siril's rainbow colours over the view on screen: from magenta at the lowest level, "
             + "through blue, cyan, green and yellow, to red at the highest. Bands of the same colour "
             + "are lines of equal level, so the SHAPE of the vignetting (round, off-centre, square) "
             + "reads at a glance. The colours are only an order, not values: how large it is still "
             + "does not show. It applies to one layer (Mosaic, a channel, mono), never to the RGB "
             + "view, where the button is greyed out. The button stays on when you change the view "
             + "or the file.",
               "Culorile curcubeu din Siril peste vederea de pe ecran: de la magenta la nivelul cel "
             + "mai jos, prin albastru, cyan, verde si galben, pana la rosu la cel mai sus. Benzile "
             + "de aceeasi culoare sunt linii de nivel egal, asa ca FORMA vignetarii (rotunda, "
             + "descentrata, patrata) se citeste dintr-o privire. Culorile sunt doar o ordine, nu "
             + "valori: cat de mare e tot nu se vede. Se aplica pe un singur strat (Mozaic, un "
             + "canal, mono), niciodata pe vederea RGB, unde butonul e dezactivat. Butonul ramane "
             + "bifat cand schimbi vederea sau fisierul.")),

            (T("zoom and pan",
               "zoom si deplasare"),
             T("The wheel zooms around the cursor, dragging with the mouse pans, "
             + "double click switches between fit and 100 %. The page scrolls with the wheel "
             + "outside the picture. At 100 % one screen pixel is "
             + "one sensor pixel - the picture is at full resolution, not shrunk. The zoom is "
             + "kept when you change the view and reset when you change the file.",
               "Rotita mareste in jurul cursorului, tragerea cu mouse-ul deplaseaza, "
             + "dublu click comuta intre potrivit si 100 %. Pagina se deruleaza cu rotita "
             + "in afara pozei. La 100 % un pixel de pe ecran e "
             + "un pixel de senzor - poza e la rezolutie intreaga, nu micsorata. Zoom-ul se "
             + "pastreaza cand schimbi vederea si se reseteaza cand schimbi fisierul.")),

            (T("the master against a frame",
               "masterul fata de un cadru"),
             T("The master is the mean of the frames under it. A shape that shows on a frame and is "
             + "missing from the master was transient (a cloud, a shadow, a flicker) and was lost in "
             + "the averaging. One that shows on both belongs to the setup, and that is the one the "
             + "figures of Structure measure. Because the master has less noise, a shared shape "
             + "looks CLEARER there, not weaker.",
               "Masterul e media cadrelor de sub el. O forma care se vede pe un cadru si lipseste "
             + "de pe master a fost trecatoare (un nor, o umbra, o palpaire) si s-a pierdut la "
             + "mediere. Una care se vede pe amandoua tine de montaj, si pe ea o masoara cifrele "
             + "din Structura. Pentru ca masterul are mai putin zgomot, o forma comuna pare acolo "
             + "mai CLARA, nu mai slaba.")),

            (T("where the figure is",
               "unde e cifra"),
             T("In the Structure tab: the residual, the corners, the tilt, the worst spot. The "
             + "picture here only says WHAT SHAPE what they measure has.",
               "In tabul Structura: reziduul, colturile, inclinarea, pata maxima. Poza de aici "
             + "spune doar CE FORMA are ceea ce masoara ele.")),
        };

        // ------------------------------------------------------- the synthetic flat
        //
        // The Sintetic tab writes a flat from the lens's published vignetting profile. The texts
        // here are only the window's: what the report says about the flat comes from
        // SyntheticReport, as in the console. The sources panel is COLLAPSED by default - it is
        // documentation, not a precondition: the button does nothing dangerous and the lens list
        // explains itself.

        /// <summary>The Sintetic tab's lead: what the file is and what it lacks.</summary>
        /// <returns>The intro.</returns>
        internal static string SyntheticGuideIntro() =>
            T("Writes an ideal flat, pp_flat_stacked" + SirilSession.SyntheticSuffix + ".fit, from the lens's published "
            + "vignetting profile: the lens model, not a measurement of your copy. "
            + "There is no light source in it, so neither the sky's dome nor a panel seen at an angle; "
            + "nor the differences between colours, nor the dust, nor the shading the camera adds. "
            + "\"Compare with the master\" says by how much the master photographed differs from this model, in stops, and in what shape.",
              "Scrie un flat ideal, pp_flat_stacked" + SirilSession.SyntheticSuffix + ".fit, din profilul de "
            + "vignetare publicat al obiectivului: modelul obiectivului, nu o masuratoare a exemplarului tau. "
            + "Nu are in el nicio sursa de lumina, deci nici cupola cerului, nici un panou vazut din unghi; "
            + "nu are nici diferentele dintre culori, nici praful, nici umbrele date de aparat. "
            + "\"Compara cu masterul\" spune cu cat difera masterul fotografiat de acest model, in stopuri, si in ce forma.");

        /// <summary>The title of the collapsed panel about the profile sources.</summary>
        /// <returns>The title.</returns>
        internal static string SyntheticSourcesTitle() => T("The profiles: where they come from and how they are updated",
                                                            "Profilele: de unde vin si cum se actualizeaza");

        /// <summary>The write button's label.</summary>
        /// <returns>The label.</returns>
        internal static string SyntheticButtonLabel() => T("Write synthetic flat",
                                                           "Scrie flat sintetic");

        /// <summary>The compare button's label.</summary>
        /// <returns>The label.</returns>
        internal static string SyntheticCompareLabel() => T("Compare with the master",
                                                            "Compara cu masterul");

        /// <summary>The camera is not in lensfun's camera database: the sensor chips are the
        /// fallback.</summary>
        /// <param name="camera">The camera's name from the header, or null or blank when the
        /// reference frame names none.</param>
        /// <returns>The note under the crop field.</returns>
        internal static string SyntheticCropUnknown(string camera) =>
            T((string.IsNullOrWhiteSpace(camera) ? "crop unknown: the reference frame does not say which camera it is" : $"crop unknown: \"{camera.Trim()}\" is not in lensfun's list of cameras")
            + " - choose the sensor type above, or type the crop (36 divided by the sensor's width in mm)",
              (string.IsNullOrWhiteSpace(camera) ? "crop necunoscut: cadrul de referinta nu spune ce aparat e" : $"crop necunoscut: \"{camera.Trim()}\" nu e in lista de aparate a lensfun")
            + " - alege tipul de senzor de mai sus, sau scrie crop-ul (36 impartit la latimea senzorului in mm)");

        /// <summary>The crop came from a sensor chip.</summary>
        /// <param name="label">The chip's label.</param>
        /// <param name="crop">The crop factor, formatted.</param>
        /// <returns>The note under the crop field.</returns>
        internal static string SyntheticCropFromFormat(string label, string crop) =>
            T($"crop {crop} approximate, by the {label} sensor type; a camera in the lensfun list would give it exactly",
              $"crop {crop} aproximativ, dupa tipul de senzor {label}; un aparat din lista lensfun l-ar da exact");

        /// <summary>Where the field's crop is from and which format it is: "crop 1.523 din
        /// baza lensfun (Nikon D3300) - format APS-C".</summary>
        /// <param name="crop">The crop factor, formatted.</param>
        /// <param name="source">Where it came from (<see cref="SyntheticCropTyped"/> or
        /// <see cref="SyntheticCropFromTable"/>).</param>
        /// <param name="formatLabel">The sensor format's label, or
        /// <c>SensorFormats.Unusual</c>.</param>
        /// <returns>The note under the crop field.</returns>
        internal static string SyntheticCropNote(string crop, string source, string formatLabel) =>
            T($"crop {crop} {source} - {(formatLabel == SensorFormats.Unusual ? formatLabel + ", no chip matches it" : "format " + formatLabel)}",
              $"crop {crop} {source} - {(formatLabel == SensorFormats.Unusual ? formatLabel + ", niciun chip nu i se potriveste" : "format " + formatLabel)}");

        /// <summary>The crop's source when the user typed it.</summary>
        /// <returns>"tastat".</returns>
        internal static string SyntheticCropTyped() => T("typed", "tastat");
        /// <summary>The crop's source when lensfun's camera table gave it.</summary>
        /// <param name="camera">The camera's name; not null.</param>
        /// <returns>The source, naming the camera.</returns>
        internal static string SyntheticCropFromTable(string camera) => T($"from the lensfun database ({camera.Trim()})",
                                                                          $"din baza lensfun ({camera.Trim()})");

        /// <summary>A lens ID carried by several lenses: the list narrowed to them, the user
        /// chooses.</summary>
        /// <param name="rawLabel">The lens as the raw names it.</param>
        /// <param name="count">How many lenses match.</param>
        /// <returns>The note under the list.</returns>
        internal static string SyntheticLensCandidates(string rawLabel, int count) =>
            T($"{rawLabel} matches {count} lenses - the list above is narrowed to them, choose yours",
              $"{rawLabel} se potriveste cu {count} obiective - lista de mai sus s-a restrans la ele, alege-l pe al tau");

        /// <summary>The overwrite question, when the synthetic file exists already.</summary>
        /// <param name="path">The existing file.</param>
        /// <returns>The question, with the path.</returns>
        internal static string SyntheticOverwriteQuestion(string path) =>
            T($"A synthetic flat exists already:\n{path}\n\nWrite over it again?",
              $"Exista deja un flat sintetic:\n{path}\n\nIl scriu din nou peste el?");

        /// <summary>When the user answered "no" to the overwrite.</summary>
        /// <param name="path">The file kept.</param>
        /// <returns>The notice, with the path.</returns>
        internal static string SyntheticNotOverwritten(string path) =>
            T($"Nothing was written: the existing file was kept.\n{path}",
              $"Nu s-a scris nimic: fisierul existent a fost pastrat.\n{path}");

        /// <summary>The refusal screen's title.</summary>
        /// <returns>The title.</returns>
        internal static string SyntheticRefusedTitle() => T("Cannot write the synthetic flat",
                                                            "Nu se poate scrie flatul sintetic");

        /// <summary>When the button is pressed with no lens selected.</summary>
        /// <returns>The prompt.</returns>
        internal static string SyntheticNoLensSelected() => T("Choose a lens from the list.",
                                                              "Alege un obiectiv din lista.");

        /// <summary>When neither source holds a profile.</summary>
        /// <returns>The explanation, naming the sources panel.</returns>
        internal static string SyntheticNoProfiles() =>
            T("No lens profile found: neither Adobe on this machine nor the lensfun/ folder beside the program. "
            + "See the panel \"" + SyntheticSourcesTitle() + "\".",
              "Niciun profil de obiectiv gasit: nici Adobe pe masina, nici folderul lensfun/ langa program. "
            + "Vezi panoul \"" + SyntheticSourcesTitle() + "\".");

        // The comparison's note comes from AgainstReport.Meaning(), as in the console.

        // ------------------------------------------------------- running the script
        //
        // The texts of the block on the problem screen and of the run window. None interprets
        // what the script did: after the run the folder is reopened and Check() says by itself
        // whether the master appeared.

        /// <summary>What the button does, before the list of scripts.</summary>
        /// <returns>The lead text.</returns>
        internal static string PreprocessLead() =>
            T("The script can be run from here, with the Siril installed on this computer (siril-cli, "
            + "without a window). The list below is the one in Siril's scripts menu; "
            + "the one proposed leaves the master in process/. The output shows in a window, "
            + "as in Siril, and when you close it the folder reopens by itself.",
              "Scriptul se poate rula de aici, cu Siril-ul instalat pe calculator (siril-cli, "
            + "fara fereastra). Lista de mai jos e cea din meniul de scripturi al lui Siril; "
            + "propus e cel care lasa masterul in process/. Iesirea se vede intr-o fereastra, "
            + "ca in Siril, iar cand o inchizi folderul se redeschide singur.");

        /// <summary>When siril-cli.exe was not found.</summary>
        /// <returns>The explanation, with the places looked in.</returns>
        internal static string SirilCliNotFound() =>
            T("Cannot find siril-cli.exe: neither through the installation entry in the registry nor in "
            + "Program Files\\Siril\\bin. Without it the script is run from Siril, and then you choose "
            + "the folder again.",
              "Nu gasesc siril-cli.exe: nici prin intrarea de instalare din registru, nici in "
            + "Program Files\\Siril\\bin. Fara el scriptul se ruleaza din Siril, si apoi alegi "
            + "din nou folderul.");

        /// <summary>When no .ssf script was found.</summary>
        /// <param name="configPath">The Siril config read, or null when there is none.</param>
        /// <returns>The explanation, naming the config.</returns>
        internal static string NoSirilScripts(string configPath) =>
            T("No .ssf script found. The list comes from Siril's configuration ("
            + (configPath ?? "the configuration file does not exist")
            + "): the scripts ticked in Preferences > Scripts and the script folders "
            + "there. Tick the script in Siril - or get the scripts repository from the same "
            + "place - and choose the folder again.",
              "N-am gasit niciun script .ssf. Lista se ia din configurarea Siril ("
            + (configPath ?? "fisierul de configurare nu exista")
            + "): scripturile bifate in Preferinte > Scripturi si folderele de scripturi de "
            + "acolo. Bifeaza scriptul in Siril - sau ia depozitul de scripturi din acelasi "
            + "loc - si alege din nou folderul.");

        /// <summary>When the run button is pressed with no script selected.</summary>
        /// <returns>The prompt.</returns>
        internal static string NoScriptChosen() => T("Choose a script from the list.",
                                                     "Alege un script din lista.");

        /// <summary>When the chosen script cannot be read.</summary>
        /// <param name="path">The script's path.</param>
        /// <param name="why">The exception's message.</param>
        /// <returns>The error, with both.</returns>
        internal static string ScriptUnreadable(string path, string why) =>
            T($"Cannot read the script:\n{path}\n\n{why}",
              $"Nu pot citi scriptul:\n{path}\n\n{why}");

        /// <summary>
        /// What the folder lacks for the chosen script to start. What the script asks for in
        /// total is said too, so the gap has context.
        /// </summary>
        /// <param name="script">The script's file name.</param>
        /// <param name="required">Every folder the script enters.</param>
        /// <param name="missing">Those missing or empty.</param>
        /// <returns>The explanation.</returns>
        internal static string MissingScriptInputs(string script, IEnumerable<string> required,
                                                   IEnumerable<string> missing) =>
            T($"The script {script} enters the subfolders {string.Join(", ", required.Select(f => f + "/"))}. "
            + $"In the folder chosen these are missing or empty: {string.Join(", ", missing.Select(f => f + "/"))}. "
            + "Put the camera's frames there, then press again.",
              $"Scriptul {script} intra in subfolderele {string.Join(", ", required.Select(f => f + "/"))}. "
            + $"In folderul ales lipsesc sau sunt goale: {string.Join(", ", missing.Select(f => f + "/"))}. "
            + "Pune acolo cadrele de la aparat, apoi apasa din nou.");

        /// <summary>The warning when Siril's window is open
        /// (<see cref="SirilInstall.IsGuiRunning"/>).</summary>
        /// <returns>The warning.</returns>
        internal static string SirilGuiRunning() =>
            T("Siril is open. If its working folder is this very folder, two processes "
            + "would write into the same process/ at the same time and the files would be damaged. "
            + "Close it, or make sure it works in another folder, before you press Run in Siril.",
              "Siril e deschis. Daca are ca folder de lucru chiar folderul asta, doua procese "
            + "ar scrie in acelasi process/ in acelasi timp si fisierele s-ar strica. "
            + "Inchide-l, sau asigura-te ca lucreaza in alt folder, inainte sa apesi Ruleaza.");

        /// <summary>The run window's title.</summary>
        /// <returns>The title.</returns>
        internal static string RunWindowTitle() => T("Siril processing",
                                                     "Procesare Siril");

        /// <summary>The line naming the command running.</summary>
        /// <param name="index">The command's number, from 1.</param>
        /// <param name="total">The script's command count; 0 when unknown.</param>
        /// <param name="command">The whole command line.</param>
        /// <returns>"comanda 7 din 20:  ..." or, without a total, "comanda 7:  ...".</returns>
        internal static string RunStep(int index, int total, string command) =>
            T(total > 0 ? $"command {index} of {total}:  {command}" : $"command {index}:  {command}",
              total > 0 ? $"comanda {index} din {total}:  {command}" : $"comanda {index}:  {command}");

        /// <summary>The closing line of a successful run.</summary>
        /// <returns>The notice.</returns>
        internal static string RunSucceeded() =>
            T("The script finished successfully. Close the window: the folder reopens and "
            + "the analysis starts on what it produced.",
              "Scriptul s-a terminat cu succes. Inchide fereastra: folderul se redeschide si "
            + "analiza porneste pe ce a produs.");

        /// <summary>The closing line of a failed run.</summary>
        /// <param name="code">siril-cli's exit code.</param>
        /// <param name="step">The command it stopped at, or null or empty when unknown.</param>
        /// <returns>The notice.</returns>
        internal static string RunFailed(int code, string step) =>
            T($"Siril stopped with code {code}"
            + (string.IsNullOrEmpty(step) ? "." : $", at {step}.")
            + " The log above says why; the folder stays as it was.",
              $"Siril s-a oprit cu codul {code}"
            + (string.IsNullOrEmpty(step) ? "." : $", la {step}.")
            + " Log-ul de mai sus spune de ce; folderul ramane cum era.");

        /// <summary>The closing line of a cancelled run.</summary>
        /// <returns>The notice.</returns>
        internal static string RunCancelled() =>
            T("Run cancelled. Siril was stopped midway, so process/ may hold "
            + "half-written files; a new run overwrites them.",
              "Rulare anulata. Siril a fost oprit la mijloc, deci process/ poate contine "
            + "fisiere pe jumatate scrise; o rulare noua le suprascrie.");

        /// <summary>When siril-cli could not be started.</summary>
        /// <param name="why">The exception's message.</param>
        /// <returns>The error.</returns>
        internal static string RunStartFailed(string why) =>
            T($"Could not start siril-cli:\n{why}",
              $"Nu am putut porni siril-cli:\n{why}");

        /// <summary>
        /// siril-cli works on a copy of Siril's settings, so without it it does not start -
        /// otherwise it would work with the default values without the user knowing.
        /// </summary>
        /// <param name="path">The settings file.</param>
        /// <param name="why">The exception's message.</param>
        /// <returns>The error, with both.</returns>
        internal static string SirilSettingsUnreadable(string path, string why) =>
            T($"Cannot read Siril's settings (siril-cli works on a copy of them):\n{path}\n\n{why}\n\n"
            + "Siril did not start. If Siril is open and is just saving its settings, press again in a few seconds.",
              $"Nu pot citi setarile lui Siril (siril-cli lucreaza pe o copie a lor):\n{path}\n\n{why}\n\n"
            + "Siril nu a pornit. Daca Siril e deschis si tocmai isi salveaza setarile, apasa din nou peste cateva secunde.");

        // ------------------------------------------------------------------ Split
        //
        // The Split tab (ADR 0054): the flats split into temp sessions, the script run in each,
        // the parts' masters beside the whole. The texts below are the screen's; the table, the
        // report and the mosaic's labels are written by the core (SplitReport).

        /// <summary>The Split tab's documentation, above the chips.</summary>
        /// <returns>The intro.</returns>
        internal static string SplitIntro() =>
            T("Splits the session's flats into 2 or 4 consecutive groups, in the order of the file "
            + "names - one per orientation, if you rotated the camera between the groups - and "
            + "builds a temporary session for each: every folder the script enters "
            + $"(biases/, darks/) whole, at most {SplitPlan.MaxLights} frames from lights/ (enough for "
            + "the script to run to the end) and its slice of flats/; process/ and masters/ are never "
            + "copied. It runs the chosen Siril script in each, then puts the parts' masters "
            + "beside the whole master: the five tests, the vignetting against the lens profile and "
            + "the residual maps, side by side. The original session is only read; the report and "
            + "the mosaic are saved in it, and the temporary folders are deleted at the end.",
              "Imparte flat-urile sesiunii in 2 sau 4 grupuri consecutive, in ordinea numelor de "
            + "fisier - cate unul pe orientare, daca ai rotit aparatul intre grupuri - si "
            + "construieste pentru fiecare o sesiune temporara: fiecare folder in care intra "
            + $"scriptul (biases/, darks/) intreg, cel mult {SplitPlan.MaxLights} cadre din lights/ (cat sa "
            + "mearga scriptul pana la capat) si felia lui de flats/; process/ si masters/ nu se "
            + "copiaza niciodata. Ruleaza scriptul Siril ales in fiecare, apoi pune masterele partilor "
            + "langa masterul intreg: cele cinci teste, vignetarea fata de profilul obiectivului si "
            + "hartile reziduale, una langa alta. Sesiunea originala e doar citita; raportul si "
            + "mozaicul se salveaza in ea, iar folderele temporare se sterg la sfarsit.");

        /// <summary>The line counting the flats and the split.</summary>
        /// <param name="count">The frames in flats/.</param>
        /// <param name="parts">The parts chosen.</param>
        /// <param name="perPart">The frames per part; 0 when the count does not divide.</param>
        /// <returns>"30 cadre in flats/  ->  2 x 15", or the count alone.</returns>
        internal static string SplitFramesLine(int count, int parts, int perPart) =>
            T(perPart > 0 ? $"{count} frames in flats/  ->  {parts} x {perPart}" : $"{count} frames in flats/",
              perPart > 0 ? $"{count} cadre in flats/  ->  {parts} x {perPart}" : $"{count} cadre in flats/");

        /// <summary>When flats/ holds no frame.</summary>
        /// <param name="s">The session, for its flats/ path.</param>
        /// <returns>The explanation.</returns>
        internal static string SplitNoFrames(SirilSession s) =>
            T($"No frame (raw or FITS) in {s.FlatsDir}. The split needs the camera's frames.",
              $"Niciun cadru (raw sau FITS) in {s.FlatsDir}. Impartirea are nevoie de cadrele de la aparat.");

        /// <summary>When the frame count does not divide by the parts.</summary>
        /// <param name="count">The frames in flats/.</param>
        /// <param name="parts">The parts chosen.</param>
        /// <returns>The explanation.</returns>
        internal static string SplitNotDivisible(int count, int parts) =>
            T($"{count} frames do not divide exactly by {parts}. Choose another number of parts or check flats/: "
            + "each rotation group must have the same number of frames.",
              $"{count} cadre nu se impart exact la {parts}. Alege alt numar de parti sau verifica flats/: "
            + "fiecare grup de rotatie trebuie sa aiba acelasi numar de cadre.");

        /// <summary>The start button's label.</summary>
        /// <returns>The label.</returns>
        internal static string SplitStartLabel() => "Start Split";
        /// <summary>The stop button's label.</summary>
        /// <returns>The label.</returns>
        internal static string SplitStopLabel() => T("Stop", "Opreste");

        /// <summary>A part-count chip's label.</summary>
        /// <param name="parts">2 or 4.</param>
        /// <returns>"2 parti" and the like.</returns>
        internal static string SplitPartsLabel(int parts) => T($"{parts} parts",
                                                               $"{parts} parti");

        /// <summary>The phase line while a part is copied.</summary>
        /// <param name="part">The part's number, from 1.</param>
        /// <param name="parts">The parts in total.</param>
        /// <param name="files">The files being copied.</param>
        /// <returns>The phase line.</returns>
        internal static string SplitPhaseCopy(int part, int parts, int files) =>
            T($"part {part} of {parts}: copying {files} files into the temporary folder",
              $"partea {part} din {parts}: copiez {files} fisiere in folderul temporar");

        /// <summary>The phase line while Siril runs on a part.</summary>
        /// <param name="part">The part's number, from 1.</param>
        /// <param name="parts">The parts in total.</param>
        /// <param name="script">The script's file name.</param>
        /// <returns>The phase line.</returns>
        internal static string SplitPhaseSiril(int part, int parts, string script) =>
            T($"part {part} of {parts}: running {script}",
              $"partea {part} din {parts}: ruleaza {script}");

        /// <summary>The phase line while the masters are analysed.</summary>
        /// <param name="parts">The parts in total.</param>
        /// <returns>The phase line.</returns>
        internal static string SplitPhaseAnalyse(int parts) =>
            T($"analysing the {parts} parts and the whole",
              $"analizez cele {parts} parti si intregul");

        /// <summary>The card's title when the folder passed the check but could not be read
        /// through (a folder the user may not list, a broken junction).</summary>
        /// <returns>The title.</returns>
        internal static string FolderUnreadableTitle() => T("The folder could not be read", "Folderul nu a putut fi citit");

        /// <summary>The card's text for <see cref="FolderUnreadableTitle"/>: the folder, the
        /// exception's message, and where the details are.</summary>
        /// <param name="folder">The session folder.</param>
        /// <param name="message">The exception's message, with its causes.</param>
        /// <returns>The text.</returns>
        internal static string FolderUnreadable(string folder, string message)
            => T($"{folder} exists and looks like a Siril session, but one of its folders could not be listed:\n\n{message}\n\n"
                 + "Check the folder's permissions (flats/, process/, masters/) and open it again. The details are in the log.",
                 $"{folder} exista si arata ca o sesiune Siril, dar unul din folderele lui nu a putut fi listat:\n\n{message}\n\n"
                 + "Verifica drepturile pe foldere (flats/, process/, masters/) si deschide-l din nou. Detaliile sunt in log.");

        /// <summary>The failure screen's title.</summary>
        /// <returns>The title.</returns>
        internal static string SplitFailedTitle() => T("The split stopped",
                                                       "Impartirea s-a oprit");

        /// <summary>A part's failure, with the temp folder kept.</summary>
        /// <param name="part">The part's number, from 1.</param>
        /// <param name="parts">The parts in total.</param>
        /// <param name="outcome">What went wrong, already worded.</param>
        /// <param name="tempRoot">The temp root kept.</param>
        /// <returns>The failure text.</returns>
        internal static string SplitFailed(int part, int parts, string outcome, string tempRoot) =>
            T($"At part {part} of {parts}: {outcome}\n\n{SplitTempKept(tempRoot)}",
              $"La partea {part} din {parts}: {outcome}\n\n{SplitTempKept(tempRoot)}");

        /// <summary>When the script ended but left no master in a part.</summary>
        /// <param name="part">The part's number, from 1.</param>
        /// <param name="parts">The parts in total.</param>
        /// <param name="partRoot">The part's folder; its parent is the temp root kept.</param>
        /// <param name="script">The script's file name.</param>
        /// <returns>The failure text.</returns>
        internal static string SplitNoMaster(int part, int parts, string partRoot, string script) =>
            T($"At part {part} of {parts}: {script} finished, but left no {SirilSession.FlatMasterName} "
            + $"in masters/ or in process/ of\n{partRoot}\n\n{SplitTempKept(Path.GetDirectoryName(partRoot))}",
              $"La partea {part} din {parts}: {script} s-a terminat, dar n-a lasat {SirilSession.FlatMasterName} "
            + $"nici in masters/, nici in process/ din\n{partRoot}\n\n{SplitTempKept(Path.GetDirectoryName(partRoot))}");

        /// <summary>The note that the temp folder was kept after a failure.</summary>
        /// <param name="tempRoot">The temp root.</param>
        /// <returns>The note.</returns>
        internal static string SplitTempKept(string tempRoot) =>
            T($"The temporary folder was kept, so that you can see what happened:\n{tempRoot}\n"
            + "Delete it when you no longer need it. The original session was not touched.",
              $"Folderul temporar a ramas, ca sa poti vedea ce s-a intamplat:\n{tempRoot}\n"
            + "Sterge-l cand nu-ti mai trebuie. Sesiunea originala nu a fost atinsa.");

        /// <summary>When the temp folder could not be deleted.</summary>
        /// <param name="tempRoot">The temp root.</param>
        /// <returns>The note.</returns>
        internal static string SplitTempNotDeleted(string tempRoot) =>
            T($"The temporary folder could not be deleted: {tempRoot}. Delete it by hand.",
              $"Folderul temporar nu s-a putut sterge: {tempRoot}. Sterge-l de mana.");

        /// <summary>The question when the window is closed during a split.</summary>
        /// <returns>The question.</returns>
        internal static string SplitCloseQuestion() =>
            T("The split is still running. Stop it? Siril is stopped midway, the temporary folder is "
            + "deleted, and the window closes after that. The original session is not touched.",
              "Impartirea inca ruleaza. O opresc? Siril e oprit la mijloc, folderul temporar se "
            + "sterge, si fereastra se inchide dupa aceea. Sesiunea originala nu e atinsa.");

        /// <summary>Where the report and the mosaic were saved.</summary>
        /// <param name="reportPath">The report's path.</param>
        /// <param name="pngPath">The mosaic's path.</param>
        /// <returns>Two lines.</returns>
        internal static string SplitSaved(string reportPath, string pngPath) =>
            T($"Report saved: {reportPath}\nMosaic saved: {pngPath}",
              $"Raport salvat: {reportPath}\nMozaic salvat: {pngPath}");

        // ------------------------------------------------------------------ Despre
        //
        // The texts of the "Despre" window. The figure, the version and the warranty notice do
        // NOT sit here: they come from VersionInfo and RunReport, the same as the console's.
        // Here are only the titles and sentences the console has nowhere to print.

        /// <summary>The "Despre" window's title.</summary>
        /// <returns>The title.</returns>
        internal static string AboutTitle() => T("About FlatCheck",
                                                 "Despre FlatCheck");

        /// <summary>The README's first line, what the tool does in one sentence.</summary>
        /// <returns>The tagline.</returns>
        internal static string AboutTagline() =>
            T("Quantitative diagnostic for astrophotography flat frames.",
              "Diagnostic cantitativ pentru flat frames de astrofoto.");

        /// <summary>The licence section's heading.</summary>
        /// <returns>The heading.</returns>
        internal static string AboutLicenceHeading() => T("LICENCE",
                                                          "LICENTA");

        /// <summary>The label of the link to the GPLv3 text.</summary>
        /// <returns>The label.</returns>
        internal static string AboutLicenseLinkLabel() =>
            T("The full text of the GNU General Public License v3",
              "Textul complet al GNU General Public License v3");

        /// <summary>The third-party section's heading.</summary>
        /// <returns>The heading.</returns>
        internal static string AboutThirdPartyHeading() => T("THIRD-PARTY COMPONENTS",
                                                             "COMPONENTE TERTE");

        /// <summary>
        /// Why the list is here and not only in a file: LibRaw's LGPL asks for the notice where
        /// the program shows its own copyright, and an exe copied alone has no notices file
        /// beside it. The compatibility reasons stay in the file.
        /// </summary>
        /// <param name="linked">How many components are linked into the executable, from the
        /// core's list: the database it ships is named apart.</param>
        /// <returns>The lead above the list.</returns>
        internal static string AboutThirdPartyLead(int linked) =>
            T($"The tool links {CountWord(linked)} components written by others and ships a database made by "
            + "others, all compatible with GPLv3. "
            + "The FITS reader, the PNG encoder, every computation and every threshold are written "
            + "by hand. Why each licence is compatible is in "
            + "THIRD-PARTY-NOTICES.md, beside the source.",
              $"Unealta leaga {CountWord(linked)} componente scrise de altii si livreaza o baza de date facuta de "
            + "altii, toate compatibile cu GPLv3. "
            + "Cititorul FITS, encoderul PNG, toate calculele si toate pragurile sunt scrise "
            + "de mana. Motivele pentru care fiecare licenta e compatibila sunt in "
            + "THIRD-PARTY-NOTICES.md, langa sursa.");

        /// <summary>A small count as a word, for a sentence: <c>four</c> / <c>patru</c>; the
        /// digits past nine.</summary>
        /// <param name="n">The count.</param>
        /// <returns>The word in the window's language.</returns>
        internal static string CountWord(int n) => n switch
        {
            1 => T("one", "una"),
            2 => T("two", "doua"),
            3 => T("three", "trei"),
            4 => T("four", "patru"),
            5 => T("five", "cinci"),
            6 => T("six", "sase"),
            7 => T("seven", "sapte"),
            8 => T("eight", "opt"),
            9 => T("nine", "noua"),
            _ => n.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };

        // ------------------------------------------------------------ the End to end tab
        // The tab explains itself in place (R-105, ADR 0064): a line under each series figure,
        // a tooltip on each column header, a paragraph under the map - not a folded guide, so
        // that each explanation sits beside what it explains. The thresholds are formatted from
        // Th, so a recalibration cannot leave a stale figure on screen. The texts mirror the
        // README ("End to end" in the window section) by hand; no test binds the two.

        /// <summary>What the mean radial component is, when it gets a verdict, and what its
        /// sign says.</summary>
        /// <returns>The line under the series' first figure.</returns>
        internal static string LightMeanNote() =>
            T($"The figure the verdict is given on, once the series has at least "
            + $"{Th.LightSeriesMinFrames} frames. It says how much darker or brighter the corners are "
            + "than the centre, on average over all the frames, after the flat was applied. Under "
            + $"{Th.LightRadOk:F0} % it is OK, above {Th.LightRadBad:F0} % a problem, suspect in "
            + "between. With a plus, the corners stayed darker: the flat corrected too little "
            + "(another aperture at the flats than at the lights, or a dirty flat). With a minus, "
            + "the corners came out brighter: the flat corrected too much (almost always the "
            + "flat-darks or the bias are missing).",
              $"Cifra care da verdictul, dupa ce seria are cel putin {Th.LightSeriesMinFrames} "
            + "cadre. Spune cu cat sunt colturile mai intunecate sau mai luminoase decat centrul, in "
            + $"medie pe toate cadrele, dupa aplicarea flat-ului. Sub {Th.LightRadOk:F0} % e OK, "
            + $"peste {Th.LightRadBad:F0} % e problema, suspect intre ele. Cu plus, colturile au "
            + "ramas mai intunecate: flat-ul a corectat prea putin (alta diafragma la flat-uri decat "
            + "la lights, sau un flat murdar). Cu minus, colturile au iesit mai luminoase: flat-ul a "
            + "corectat prea mult (aproape sigur lipsesc flat-dark-urile sau bias-ul).");

        /// <summary>What the spread is and why a mean alone is not enough.</summary>
        /// <returns>The line under the series' second figure.</returns>
        internal static string LightSpreadNote() =>
            T("The lowest and the highest frame in the series. When they are far apart, the mean "
            + "says little: the flat is the same in every frame, so what changes from one frame to "
            + "the next is the sky, not the flat.",
              "Cel mai mic si cel mai mare cadru din serie. Daca sunt departe unul de altul, media "
            + "nu spune mare lucru: flat-ul e acelasi in toate cadrele, deci ce se schimba de la un "
            + "cadru la altul e cerul, nu flat-ul.");

        /// <summary>What the error of the mean is and where it stops the verdict.</summary>
        /// <returns>The line under the series' third figure.</returns>
        internal static string LightSemNote() =>
            T("How sure the mean is: the more frames, and the closer they are to each other, the "
            + $"smaller it gets. Above {Th.LightSeriesSemMax:F2} points the mean is too uncertain to "
            + $"be compared with the {Th.LightRadOk:F0} % and {Th.LightRadBad:F0} % thresholds, and "
            + "the verdict stays undetermined.",
              "Cat de sigura e media: cu cat cadrele sunt mai multe si mai apropiate intre ele, cu "
            + $"atat e mai mica. Peste {Th.LightSeriesSemMax:F2} puncte media e prea nesigura ca sa "
            + $"fie comparata cu pragurile de {Th.LightRadOk:F0} % si {Th.LightRadBad:F0} %, si "
            + "verdictul ramane nedeterminat.");

        /// <summary>What the sky correlation is, where it stops the verdict, and when it is not
        /// checked at all.</summary>
        /// <returns>The line under the series' fourth figure.</returns>
        internal static string LightCorrelationNote() =>
            T("Checks whether the figure in the radial column moves together with the sky (the sky "
            + $"column). When they move together, above {Th.LightSeriesSkyCorr:F2}, what is measured "
            + "is the sky, not the flat, and the verdict stays undetermined. It is checked only when "
            + $"the sky changed enough during the session (by at least {Th.LightSeriesSkySpanMin:F0} "
            + "points); on a sky that stood still there is nothing to compare.",
              "Verifica daca cifra din coloana radial se misca odata cu cerul (coloana cer). Daca se "
            + $"misca impreuna, peste {Th.LightSeriesSkyCorr:F2}, ce masuram e cerul, nu flat-ul, si "
            + "verdictul ramane nedeterminat. Verificarea se face doar daca cerul s-a schimbat destul "
            + $"in timpul sesiunii (cu cel putin {Th.LightSeriesSkySpanMin:F0} puncte); pe un cer "
            + "care a stat pe loc nu are ce compara.");

        /// <summary>What the picture under the table is and what its colours mean. The sign
        /// convention is the figures': blue corners are corners that stayed darker, the positive
        /// sign, under-correction.</summary>
        /// <returns>The paragraph under the map and its legend.</returns>
        internal static string LightMapNote() =>
            T("What is left of the vignetting after the flat was applied, averaged over the series. "
            + "It is not a picture of the sky: no stars, no nebula, only the shape. White: the "
            + "corners are the same as the centre. Blue corners: they stayed darker (the flat "
            + "corrected too little). Red corners: they came out brighter (the flat corrected too "
            + "much). The colours are stretched over the map's own range, so a strong colour can "
            + "still mean a small figure; how much it is, the figures on the left say.",
              "Cum arata, in medie pe serie, ce a ramas din vignetare dupa aplicarea flat-ului. Nu e "
            + "o poza a cerului: fara stele, fara nebuloasa, doar forma. Alb: colturile sunt la fel "
            + "ca centrul. Colturi albastre: au ramas mai intunecate (flat-ul a corectat prea "
            + "putin). Colturi rosii: au iesit mai luminoase (a corectat prea mult). Culorile sunt "
            + "intinse pe tot intervalul hartii, deci o culoare tare poate insemna o cifra mica; cat "
            + "de mult e, spun cifrele din stanga.");

        /// <summary>What the table's first column holds and which files are left out.</summary>
        /// <returns>The tooltip of the "frame" header.</returns>
        internal static string LightFrameTip() =>
            T($"The lights of process/ that Siril calibrated with the flat "
            + $"({SeriesFinder.DefaultPattern}), in file-name order. The aligned frames "
            + "(r_pp_light_*) are not used: alignment shifts them, the vignetting would no longer "
            + "sit at the centre of the frame, and the measurement would come out wrong without "
            + "showing it.",
              $"Lights-urile din process/ pe care Siril le-a calibrat cu flat-ul "
            + $"({SeriesFinder.DefaultPattern}), in ordinea numelui. Cadrele aliniate "
            + "(r_pp_light_*) nu sunt folosite: alinierea le deplaseaza, vignetarea nu ar mai fi in "
            + "centrul cadrului si masuratoarea ar iesi gresita fara sa se vada.");

        /// <summary>What the table's radial column holds and why one row is not the
        /// answer.</summary>
        /// <returns>The tooltip of the "radial" header.</returns>
        internal static string LightRadialTip() =>
            T("How much darker (plus) or brighter (minus) the corners of this frame are than its "
            + "centre, in per cent, after the flat was applied. A single frame holds the sky of "
            + "that moment too, so the verdict is given on the mean of the series, not on one row.",
              "Cu cat sunt colturile acestui cadru mai intunecate (plus) sau mai luminoase (minus) "
            + "decat centrul, in procente, dupa aplicarea flat-ului. Un singur cadru contine si "
            + "cerul din acel moment, asa ca verdictul se da pe media seriei, nu pe un rand.");

        /// <summary>What the table's sky column holds and that it never votes.</summary>
        /// <returns>The tooltip of the "sky" header.</returns>
        internal static string LightSkyTip() =>
            T("How much brighter the sky is on one side of the frame than on the other, in per "
            + "cent: light pollution, the Moon, a nearby horizon. Information only, it never enters "
            + $"the verdict (above {Th.LightPlaneWarn:F0} % it counts as large). If it changes from "
            + "one row to the next, the sky changed during the session; that is what the "
            + "correlation in the summary is compared with.",
              "Cat de mult e cerul mai luminos pe o parte a cadrului decat pe cealalta, in procente: "
            + "poluare luminoasa, Luna, orizontul apropiat. Doar informativ, nu intra in verdict "
            + $"(peste {Th.LightPlaneWarn:F0} % e considerat mare). Daca se schimba de la un rand "
            + "la altul, cerul s-a schimbat in timpul sesiunii; cu asta se compara corelatia din "
            + "sinteza.");

        /// <summary>What the table's tiles column holds and what a low count means.</summary>
        /// <returns>The tooltip of the "tiles" header.</returns>
        internal static string LightTilesTip() =>
            T("The tiles are the small squares the frame is cut into; this is how many were used "
            + "out of how many there are. Squares with nebula or other extra light are set aside, "
            + "so that only the sky background remains. With few squares left, the measurement "
            + "stands on less data.",
              "Tile-urile sunt patratelele in care e impartit cadrul; aici scrie cate au fost "
            + "folosite din cate sunt. Patratelele cu nebuloasa sau alta lumina in plus sunt date "
            + "la o parte, ca sa ramana doar fondul cerului. Cu putine patratele ramase, "
            + "masuratoarea sta pe mai putine date.");
    }
}
