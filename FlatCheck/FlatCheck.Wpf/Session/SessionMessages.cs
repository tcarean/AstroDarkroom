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

namespace FlatCheck.Wpf
{
    /// <summary>
    /// The Romanian texts the window builds in code that the core does not write (the fixed
    /// labels and headings stay in the XAML): the session problems, the guides, the button
    /// labels, the Siril run's and the Split tab's messages, the "Despre" window's headings.
    /// </summary>
    internal static class SessionMessages
    {
        /// <summary>The problem card's title.</summary>
        /// <param name="p">The session problem.</param>
        /// <returns>The title, or "" for <see cref="SessionProblem.None"/>.</returns>
        internal static string Title(SessionProblem p) => p switch
        {
            SessionProblem.NoFolder => "Folderul nu exista",
            SessionProblem.NoProcessDir => "Nu s-a rulat niciun script de preprocesare",
            SessionProblem.NoFlatMaster => "Lipseste masterul de flat",
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
                $"Nu gasesc folderul:\n{s.Root}\n\n"
                + "Alege folderul de lucru al sesiunii - cel care contine subfolderele "
                + "flats/, darks/, biases/, lights/ si process/ sau masters/.",

            SessionProblem.NoProcessDir =>
                $"Folderul ales nu are nici subfolderul process/, nici masters/:\n{s.Root}\n\n"
                + "Acolo isi pune Siril rezultatele calibrarii, deci lipsa lor inseamna ca "
                + $"nu s-a rulat inca niciun script de preprocesare.\n\n"
                + $"Ruleaza scriptul {SirilSession.PreprocessingScript} - de aici, cu butonul de "
                + "mai jos, sau din Siril si apoi alege din nou folderul.",

            SessionProblem.NoFlatMaster =>
                $"Nu gasesc masterul de flat, {SirilSession.FlatMasterName}, in niciunul din "
                + "cele doua locuri in care il lasa scripturile Siril:\n"
                + string.Join("\n", s.FlatMasterCandidates) + "\n\n"
                + "Asta e fisierul pe care il produce scriptul de preprocesare: flat-urile "
                + "stivuite si CALIBRATE (prefixul \"pp_\" inseamna exact ca flat-dark-ul si "
                + "offsetul au fost deja scazute). Toate testele de structura pornesc de la el.\n\n"
                + $"Ruleaza {SirilSession.PreprocessingScript} - de aici, cu butonul de mai jos, "
                + "sau din Siril si apoi alege din nou folderul.",

            _ => "",
        };

        /// <summary>
        /// R-082: when the user picks by hand a file that is not a flat master, it is
        /// explained what these files are and why the test needs one, instead of a parsing
        /// error from the FITS reader a few seconds later.
        /// </summary>
        /// <param name="path">The file picked.</param>
        /// <returns>The explanation, with the path.</returns>
        internal static string NotAFlatMaster(string path) =>
            $"Fisierul ales nu e un master de flat:\n{path}\n\n"
            + "Testul de rotatie la 180 de grade compara doua MASTERE de flat, adica doua "
            + "stive calibrate de flat-uri - fisierele pe care Siril le scrie in process/ sau "
            + $"in masters/, cu numele de forma {SirilSession.RatioPattern}.\n\n"
            + "Cadrele individuale calibrate stau in acelasi folder si arata la fel "
            + "(pp_flat_00001.fit si urmatoarele), dar NU merg: zgomotul unui singur cadru "
            + "e de acelasi ordin cu semnalul cautat, deci o pereche de cadre da un "
            + "rezultat sigur si fals.\n\n"
            + "Prefixul \"pp_\" inseamna \"preprocesat\": flat-dark-ul si offsetul au fost deja "
            + "scazute. Pe un flat necalibrat mai ramane un piedestal de cateva sute de ADU "
            + "care aplatizeaza profilul, iar testele ies \"OK\" oricum, fiindca profilul "
            + "ramane simetric radial si doar amplitudinea e gresita. Nu se poate detecta, "
            + "doar evita.\n\n"
            + "Cum obtii doua: fotografiezi ~15 flat-uri, rotesti aparatul 180 de grade in "
            + "jurul axei optice, mai faci ~15, si stivuiesti cele doua seturi SEPARAT - "
            + "adica in doua foldere de sesiune, cate unul pentru fiecare orientare.";

        // ------------------------------------------------------- the waiting screen
        //
        // A run a click starts waits for the opening chain (the "sequential, not parallel" rule
        // of MainViewModel); while it waits, the waiting screen says that and not the run's own
        // message, which would claim work that has not begun.

        /// <summary>The waiting screen's first line while a click-run waits for the analyses
        /// started by opening the folder.</summary>
        /// <returns>The line.</returns>
        internal static string WaitingForOpeningChain() =>
            "Astept sa se termine analizele pornite la deschiderea folderului...";

        /// <summary>The Ratio tab's waiting screen while the two masters are compared.</summary>
        /// <returns>The line.</returns>
        internal static string RatioComparing() => "Compar cele doua mastere de flat...";

        /// <summary>
        /// The tab's documentation, shown before any computation. This mode is the only one
        /// that asks for something that cannot be deduced from a folder, so it is the only one
        /// that must explain what it wants BEFORE, not after it has failed.
        /// </summary>
        /// <returns>The Ratio tab's documentation.</returns>
        internal static string RatioNeedsTwoMasters() =>
            "CE FACE. Desparte structura ne-radiala a flat-ului tau in doua: ce e fixat de "
            + "APARAT (obiectiv, senzor - se imparte corect din lights) si ce e fixat in "
            + "LUME (panou neuniform, gradient de cer - nu se reproduce). Verdictul e al "
            + "primei parti, adica al masterului pe care il obtii stivuind ambele orientari.\n\n"

            + "DE CE AI NEVOIE: DOUA MASTERE DE FLAT, DIN DOUA SEDINTE.\n"
            + "  1. ~15 flat-uri.\n"
            + "  2. Rotesti aparatul 180 de grade in jurul axei optice. Nu muta nimic "
            + "altceva - nici trepiedul, nici sursa, nici distanta.\n"
            + "  3. Inca ~15 flat-uri.\n"
            + "  4. Stivuiesti cele doua seturi SEPARAT, in doua foldere de sesiune, cu "
            + $"scriptul de preprocesare. Iti raman doua fisiere {SirilSession.FlatMasterName}, "
            + "cate unul in fiecare sesiune - in masters/ sau in process/, dupa script.\n\n"

            + "Rotatia nu trebuie sa fie exacta. Vignetarea e rotational simetrica, deci "
            + "se anuleaza la orice unghi; cele 180 de grade servesc doar la inversarea "
            + "gradientului extern, iar o eroare de unghi doar subraporteaza, nu "
            + "supraraporteaza.\n\n"

            + "CE NU ITI SPUNE. Daca partea fixata de aparat e obiectivul (inofensiva) sau "
            + "ceva PE obiectiv - curea, condens, parasolara stramba - care se roteste "
            + "odata cu el si arata identic, dar la lights nu mai e acolo. Pentru asta "
            + "compara doua sedinte diferite cu acelasi obiectiv.";

        /// <summary>
        /// The same file in both slots. Caught at the pick, not at the computation, so the good
        /// selection from before is not lost.
        /// </summary>
        /// <returns>The explanation.</returns>
        internal static string SameFileTwice() =>
            "Testul compara doua stive DIFERITE: una dinainte de rotatia aparatului, una "
            + "de dupa. Impartind un fisier la el insusi iese 1 peste tot, deci o "
            + "componenta externa de exact zero - care nu inseamna \"setup curat\", "
            + "inseamna \"nu s-a masurat nimic\".";

        /// <summary>
        /// The master exists both in masters/ and in process/: two preprocessings on the same
        /// folder. The newest is measured, and the user must know which, because the two can
        /// come from different sets of flats.
        /// </summary>
        /// <param name="m">The lookup, with both paths.</param>
        /// <returns>The warning, naming the master measured and the one ignored.</returns>
        internal static string TwoFlatMasters(FlatMasterLookup m) =>
            "Sunt DOUA mastere de flat in folderul asta, in masters/ si in process/ - doua "
            + "rulari de preprocesare. Se masoara cel mai nou:\n" + m.Path + "\n"
            + "Celalalt, mai vechi, nu e citit:\n" + m.Other + "\n"
            + "Daca nu e cel pe care il vrei, sterge-l sau redenumeste-l pe celalalt si "
            + "alege din nou folderul.";

        /// <summary>When process/ holds no calibrated frames for the end-to-end test.</summary>
        /// <param name="s">The session, for its process/ path.</param>
        /// <returns>The explanation, with the pattern and the folder.</returns>
        internal static string NoLightSeries(SirilSession s) =>
            $"Niciun cadru care sa se potriveasca cu {SeriesFinder.DefaultPattern} in:\n"
            + $"{s.ProcessDir}\n\n"
            + "Testul end-to-end are nevoie de lights CALIBRATE cu flat, FARA background "
            + "extraction si NEREGISTRATE.\n\n"
            + "Cadrele registrate (r_pp_light_*) sunt excluse intentionat: registrarea "
            + "transforma geometric cadrul, deci muta eroarea de flat - care e fixa pe "
            + "senzor - fata de centrul in jurul caruia se fiteaza modelul radial. Ar "
            + "corupe masuratoarea fara sa se vada.";

        /// <summary>When flats/ holds no camera raw.</summary>
        /// <param name="s">The session, for its flats/ path.</param>
        /// <returns>The explanation, with the folder.</returns>
        internal static string NoRawFlats(SirilSession s) =>
            $"Niciun fisier raw de aparat in:\n{s.FlatsDir}\n\n"
            + "Verificarea expunerii ruleaza pe un flat BRUT, NECALIBRAT, cu mozaicul CFA "
            + "intact si scala ADU originala - stivuirea normalizeaza exact nivelul care se "
            + "masoara aici, deci un master nu se poate folosi.\n\n"
            + "Cel mai simplu e sa lasi in flats/ chiar fisierele de la aparat "
            + "(.nef, .cr2, .cr3, .arw, .dng, .raf). Pe ele white si black se citesc exact "
            + "din fisier, nu se mai ghicesc din maxim.";

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
            "Tabul raspunde la doua intrebari diferite, si nu cu aceleasi mijloace.";

        /// <summary>The two halves of the reading, in the order they are used.</summary>
        /// <returns>Two rows, heading and text.</returns>
        internal static IReadOnlyList<(string Shape, string Meaning)> HowToReadRows() => new[]
        {
            ("CAT DE MULT - decid cifrele, nu harta",
             "\"Reziduu dupa model radial\" e chiar masura a cat din cadru nu se explica "
             + "printr-o functie pur radiala, pe scara de mai jos. Sub 2 % nu conteaza ce "
             + "forma are. Intre 2 si 10 % background extraction scoate ce ramane, oricare "
             + "ar fi forma. Verdictul de utilizabilitate NU depinde niciodata de ce vezi "
             + "in harta."),

            ("CE FEL - aici intra ochiul, dar nu de la zero",
             "Forma spune ce sa schimbi la sedinta urmatoare, nu daca flat-ul e bun. Si "
             + "cifrele fac o parte din treaba inainte sa te uiti: daca \"inclinare\" e "
             + "aproape de reziduu, e un gradient, si tabul Ratio spune daca e fix pe "
             + "aparat sau in lume; daca \"pata maxima\" e mult peste reziduu, e ceva "
             + "compact - o scurgere mica, condens - si \"pete locale\" spune unde; iar "
             + "daca a aparut atentionarea de praf, intrebarea nu mai e daca flat-ul e "
             + "bun, ci pentru ce il folosesti: sesiunea ale carei lights au aceeasi "
             + "pata, sau biblioteca. Ce ramane pentru ochi e ghidul de sub harta."),
        };

        // --------------------------------------------------- the histograms' guide
        //
        // The same kind of text as the map's guide below: documentation shown in the window,
        // folded, written once. And the same limit - it explains what is seen, it does NOT
        // classify. The verdict stays the median of the central window of the brightest
        // channel, and nothing in the drawing votes.
        //
        // The table mirrors the README, the section about the Expunere tab. There is NO test
        // linking the two - if you reword here, go there too.

        /// <summary>What the axis is and what role the drawing has, before the list of
        /// readings.</summary>
        /// <returns>The guide's intro.</returns>
        internal static string HistogramGuideIntro() =>
            "Axa orizontala e aceeasi pe care stau toate cifrele tabului: procente din "
            + "(white - black), adica din intervalul util al senzorului. Zero e negrul "
            + "aparatului, 100 e saturatia. Pe previzualizarea aparatului, axa e procente "
            + "din 255 - tot \"cat din intervalul disponibil\", singurul fel in care cele "
            + "doua se pot citi una langa alta.\n\n"
            + "Desenul EXPLICA, nu voteaza. Verdictul e mediana ferestrei centrale a "
            + "canalului celui mai luminos, pe datele brute, si nimic altceva - "
            + "histogramele arata de unde vine cifra aia si ce se intampla in jurul ei.";

        /// <summary>
        /// How the drawing is read, in the order it is worth looking: first the two curves,
        /// then the figure that decides, then the bands, then the traps.
        /// </summary>
        /// <returns>The guide's rows, heading and text.</returns>
        internal static IReadOnlyList<(string Shape, string Meaning)> HistogramGuideRows() => new[]
        {
            ("doua curbe pe fiecare canal",
             "Cea palida si lata e tot canalul; cea plina si ingusta e fereastra centrala "
             + "de 20 %, chiar cea din care se ia verdictul. Fiecare e scalata la propriul "
             + "varf, deci INALTIMILE nu se compara intre ele - pozitiile se compara."),

            ("linia verticala",
             "Mediana ferestrei centrale, singura cifra care decide. Pe canalul cel mai "
             + "luminos e chiar numarul din pastila de verdict, de sus."),

            ("banda verde",
             "Tinta, din exact aceleasi praguri ca verdictul. Se muta cand bifezi \"Sky\": "
             + "un sky flat TREBUIE sa stea mai jos, fiindca cerul se stinge continuu si "
             + "SNR-ul vine din numarul de cadre."),

            ("benzile galbene si rosii",
             "Aceeasi scara mai departe: galben \"acceptabil, dar\", rosu prea jos sau prea "
             + "sus. Pe cer nu exista galben in stanga - acolo tinta incepe chiar de la "
             + "pragul de prea-jos."),

            ("dunga rosie din dreapta",
             "98-100 % din white: acolo analizorul numara pixelii saturati. Orice masa a "
             + "curbei pline in dunga aia inseamna PREA SUS, oricat de cuminte ar fi "
             + "mediana - acolo se pierde informatia de vignetare, exact unde semnalul e maxim."),

            ("de ce curba palida e mult mai lata",
             "Aia e vignetarea, nu subexpunere: colturile unui flat SUNT mai intunecate "
             + "prin constructie, si distanta dintre cele doua varfuri e chiar cat de "
             + "adanca e. Daca expui dupa curba lata, clipezi centrul si pierzi exact ce "
             + "masori."),

            ("de ce canalele au varfuri diferite",
             "Filtrele Bayer lasa sa treaca cantitati diferite din lumina sursei. Pe un "
             + "panou cald poate conduce R, pe cer la ora albastra de obicei B. Cel mai "
             + "luminos clipeaza primul, deci el e limitantul. Unealta nu face balans de "
             + "alb - tocmai asta e ideea."),

            ("ce NU e desenul asta",
             "Nu e histograma de pe aparat. Aia e a JPEG-ului procesat - debayer, balans "
             + "de alb, gama - si sta mult mai la dreapta. De aceea un cadru care pare "
             + "\"la limita\" pe ecranul aparatului poate fi la 40 % in date brute."),

            ("comutatorul de deasupra desenelor",
             "Alege la ce te uiti: datele brute, pe care le masoara unealta, sau "
             + "previzualizarea pe care aparatul a inglobat-o in raw - chiar imaginea din "
             + "care si-a calculat histograma de pe ecran. Numai vederea bruta are banda "
             + "tinta, fiindca numai ea spune ceva despre lumina de pe senzor. Pe cealalta "
             + "nu e nicio tinta, si nici n-ar putea fi: ar insemna sa expui dupa ecran. "
             + "Tabelul de sub desene pune cele doua cifre cap la cap, pe fiecare canal."),

            ("de ce ecranul e mult mai la dreapta",
             "Gama plus curba de ton, pe care nicio setare din aparat nu le scoate: un JPEG "
             + "e codificat gamma prin definitie. O curba compresiva intinde cel mai tare "
             + "nivelurile de jos si de mijloc, adica exact unde sta un flat. Si, pe "
             + "aparatele masurate aici, ecranul s-a saturat inaintea senzorului, cu o "
             + "treime de stop pana la un stop: deci e o alarma intr-un singur sens - \"nu "
             + "e lipit\" e o garantie, \"e lipit\" nu e informatie - nu un instrument de "
             + "masura."),
        };

        // ------------------------------------------------------- the map's guide
        //
        // WHY IT IS DOCUMENTATION AND NOT A DIAGNOSIS. The tool does not classify the map's
        // shape and must not: a spot can be a light leak or a cloud, and from the pixels it
        // cannot be told apart. What can be done is tell the user what each shape meant on real
        // masters, and which test separates the cases. So: rows of text, no verdict, no
        // threshold, no figure computed here.
        //
        // The text mirrors the table in the README, the "Iesire PNG" section. There is NO test
        // linking the two - if you reword here, go there too.

        /// <summary>The guide's introduction: how the map is read, before the list of
        /// shapes.</summary>
        /// <returns>The guide's intro.</returns>
        internal static string MapGuideIntro() =>
            "Harta e imaginea impartita la modelul radial: alb inseamna exact cat prezice "
            + "un model pur radial in jurul centrului senzorului, rosu mai luminos decat "
            + "modelul, albastru mai intunecat. Tot ce era simetric radial a fost scos, "
            + "deci ce a ramas NU poate fi vignetare.\n\n"
            + "Citeste intai cifrele, care spun CAT, si abia apoi forma, care spune DE CE. "
            + "Intensitatea culorii depinde de scara aleasa mai sus: pe \"auto\" culorile "
            + "umplu mereu tot intervalul, chiar si cand cifrele sunt minuscule - uita-te "
            + "la legenda.";

        /// <summary>
        /// The shapes met on the map and what they meant, in the order they are worth looking
        /// for: from the most common and most harmless, towards those that ask for a particular
        /// counter-test.
        /// </summary>
        /// <returns>The guide's rows, shape and meaning.</returns>
        internal static IReadOnlyList<(string Shape, string Meaning)> MapGuideShapes() => new[]
        {
            ("pete fara directie",
             "Abateri mici, fara o axa preferata. Semnatura difuzorului plus zgomotul "
             + "cerului. Background extraction le scoate fara efort; cifrele decid daca "
             + "sunt de ignorat."),

            ("dipol (o parte albastra, cealalta rosie)",
             "Un gradient, deci o directie. Se potriveste cu \"inclinare\". Sursa "
             + "neuniforma, lumina ambientala, sau un obstacol intins asimetric peste "
             + "obiectiv. Tabul Ratio spune daca e fix in lume (cerul) sau fix pe aparat "
             + "(difuzorul, parasolarul)."),

            ("cercuri sau arce concentrice",
             "Un inel centrat pe senzor NU poate ramane in harta - modelul radial il "
             + "absoarbe prin constructie. Deci daca vezi unul, e centrat in alta parte: "
             + "ceva rotund din fata obiectivului e in afara axei, sau lumina nu e "
             + "difuza. Nu s-a vazut inca pe un master real: inelele care apareau pe "
             + "mastere cu gradient mare erau un artefact al modelului radial, corectat "
             + "intre timp - profilul se ia acum din rasterul simetrizat la 180 de grade, "
             + "iar pe inelele partiale de dincolo de cercul inscris se scoate si termenul "
             + "par (o sa a cerului), care desena o creasta pe acel cerc."),

            ("pata locala intr-un colt sau la o margine",
             "Light leak: vizor, montura, latura difuzorului. Nu se roteste cu cerul, "
             + "deci tabul Ratio o pune pe aparat."),

            ("pata rotunda in centru",
             "Iluminare contaminata simetric fata de axa: un panou prea aproape, "
             + "backlight-ul unui monitor. Cand e chiar centrata, NU poate aparea pe "
             + "harta si nu o vede niciun test - modelul radial o absoarbe intreaga, "
             + "exact ca vignetarea; masurat, o pata de 10% in centru lasa harta goala "
             + "si toate cifrele sub 0.1%. Singura ei urma e cifra de vignetare, care "
             + "creste. Se vede doar cand e putin descentrata, si atunci ca o semiluna, "
             + "nu ca un disc. Testul prin rotatie e la fel de orb la ea; contra-testul "
             + "e distanta - un set la 30 cm, unul la 60."),

            ("granulatie fina peste tot",
             "Zgomot, nu structura: prea putine cadre sau expunere prea joasa. Cifrele "
             + "raman mici; nu e nimic de reparat la sursa, doar de stivuit mai mult."),
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
            $"Nimic de egalizat: nici masterul {SirilSession.FlatMasterName} in masters/ sau process/, nici "
            + $"vreun cadru citibil in:\n{s.FlatsDir}\n\n"
            + "Tabul arata cadrele asa cum au fost trase - raw de aparat (.nef, .cr2, .cr3, "
            + ".arw, .dng, .raf) sau FITS (.fit, .fits, .fts) - si, pe prima pozitie, masterul "
            + "facut din ele, ca sa se vada ce s-a mediat si ce a ramas.";

        // The guide below mirrors the table in the README ("Histogram"). There is no test
        // linking the two - if you reword here, go there too.

        /// <summary>What the picture is, before the list of readings.</summary>
        /// <returns>The guide's intro.</returns>
        internal static string HistEqGuideIntro() =>
            "E vederea \"Histogram\" din Siril, refacuta: imaginea trecuta prin propria ei "
            + "functie de repartitie, astfel incat fiecare nivel de gri sa primeasca acelasi "
            + "numar de pixeli. Nu e un grafic si nu e o masuratoare. Pe un flat, a carui "
            + "distributie e ingusta fata de scala fisierului, curba e abrupta exact pe "
            + "intervalul vignetarii, si tot ce nu e uniform iese la contrast maxim.\n\n"
            + "Poza EXPLICA, nu voteaza: nicio cifra din unealta nu se schimba cu ce se vede "
            + "aici, si nicio forma de aici nu intra in vreun verdict.";

        /// <summary>How the picture is read, in the order it is worth looking.</summary>
        /// <returns>The guide's rows, heading and text.</returns>
        internal static IReadOnlyList<(string Shape, string Meaning)> HistEqGuideRows() => new[]
        {
            ("de ce un flat curat arata zgomotos",
             "Egalizarea intinde ORICE distributie pe 0..255, si a unui cadru fara nicio "
             + "structura. Atunci ce ramane de intins e zgomotul, si el ajunge la contrast "
             + "maxim. Granulatie fina peste tot inseamna \"n-are altceva de aratat\"."),

            ("de ce nu se poate citi cat de mare e o forma",
             "Fiecare nivel de gri primeste acelasi numar de pixeli, indiferent cat de "
             + "apropiate erau valorile lor. O unda de 0.5 % si un gradient de 10 % cu aceeasi "
             + "forma ies identic. Amplitudinea e in tabul Structura, nu aici."),

            ("vederea Mozaic (implicita)",
             "Tot mozaicul egalizat ca un singur strat - literal ce arata Siril pe un raw "
             + "deschis nedebayerizat, cum il deschide implicit; de asta e prima. Cele trei "
             + "culori sunt trei populatii in aceeasi histograma, deci la 100 % se vede tabla "
             + "de sah a mozaicului, ca in Siril; micsorata, se mediaza."),

            ("vederea RGB",
             "Cele patru pozitii Bayer, fiecare egalizata pe repartitia ei si pusa in "
             + "culoarea ei - Siril pe o imagine debayerizata. Are mai mult contrast decat "
             + "mozaicul, fiindca fiecare canal e intins pe tot intervalul lui. O nuanta "
             + "uniforma nu inseamna nimic; o nuanta care VARIAZA pe cadru inseamna ca un "
             + "canal are alta forma decat celelalte - o sursa cu gradient de culoare, o "
             + "scurgere de lumina colorata."),

            ("vederile pe canal",
             "Aceeasi poza, un singur canal, in gri. Utile cand compozitul e greu de citit "
             + "sau cand vrei sa vezi daca o forma e in toate canalele sau doar intr-unul."),

            ("zoom si deplasare",
             "Rotita mareste in jurul cursorului, tragerea cu mouse-ul deplaseaza, "
             + "dublu click comuta intre potrivit si 100 %. Pagina se deruleaza cu rotita "
             + "in afara pozei. La 100 % un pixel de pe ecran e "
             + "un pixel de senzor - poza e la rezolutie intreaga, nu micsorata. Zoom-ul se "
             + "pastreaza cand schimbi vederea si se reseteaza cand schimbi fisierul."),

            ("masterul fata de un cadru",
             "Masterul e stiva cadrelor de sub el. O forma care se vede pe un cadru si "
             + "lipseste de pe master a fost trecatoare - un nor, o umbra, o palpaire - si "
             + "s-a mediat. Una care se vede pe amandoua e fixa pe montaj, si pe ea o "
             + "masoara cifrele din Structura. Zgomotul mai mic al masterului face o forma "
             + "comuna sa para acolo mai CLARA, nu mai slaba: egalizarea e pe ranguri."),

            ("unde e cifra",
             "In tabul Structura: reziduul dupa modelul radial, colturile, inclinarea, pata "
             + "maxima. Poza de aici spune CE FORMA are ce masoara ele."),
        };

        // ------------------------------------------------------- smoothing
        //
        // The smoothing tab's guide. Like the others, it mirrors the README ("Netezire") with no
        // test linking them. Unlike the others it is OPEN by default: it is the tab's
        // precondition, like the documentation on Ratio - a button that writes a 96 MB file
        // beside the master is not pressed before knowing what it does.

        /// <summary>What the button does and what it cannot know, before the list.</summary>
        /// <returns>The guide's intro.</returns>
        internal static string SmoothGuideIntro() =>
            "Scrie un al doilea master, pp_flat_stacked" + SirilSession.SmoothedSuffix + ".fit, langa cel "
            + "fotografiat: acelasi flat, cu tot ce e neted pastrat si tot ce nu e scos - textura "
            + "difuzorului, umbrele de praf, petele compacte. Nu e o analiza si nu da verdict; "
            + "ce raporteaza e CE A SCOS, fiindca asta e singurul lucru dupa care se poate "
            + "decide daca fisierul e de incredere.\n\n"
            + "Masterul fotografiat ramane neatins si ramane masterul sesiunii: tabul Structura "
            + "arata in continuare textura, ca sa se vada de ce a fost nevoie de netezire.";

        /// <summary>What it keeps, what it removes, the scale, how it reaches Siril.</summary>
        /// <returns>The guide's rows, heading and text.</returns>
        internal static IReadOnlyList<(string Shape, string Meaning)> SmoothGuideRows() => new[]
        {
            ("ce pastreaza",
             "Tot ce e neted la scara aleasa: vignetarea, colturile inegale, un gradient, "
             + "petele largi ale unui monitor. Nu stie ce e al obiectivului si ce e al sursei - "
             + "doar --ratio le desparte - deci o iluminare proasta ramane la fel de proasta "
             + "in fisierul netezit. Bateria de structura de dedesubt arata ca ce era al "
             + "obiectivului a ramas: cifrele se misca sub o jumatate de punct."),

            ("ce scoate",
             "Textura difuzorului (celule de ~300 px pe un master prin material pus pe "
             + "monitor), umbrele de praf, petele compacte, zgomotul. Praful poate fi si "
             + "ceva dorit: o umbra de praf e corecta pentru sesiunea ale carei lights o "
             + "au si ele, si gresita intr-un flat de biblioteca. Harta din dreapta arata "
             + "exact ce a plecat; daca in ea se vede altceva decat textura si praf, "
             + "netezirea a luat si ceva real."),

            ("scara",
             "Sigma nucleului de netezire, in pixeli de senzor. La 300 px celulele "
             + "difuzorului trec la ~30 % in RMS si o structura reala de 300 px e luata "
             + "partial; la 150 px ramane, dar raman si mai multe celule; la 600 px pleaca "
             + "aproape tot ce nu e vignetare. Singurul buton al modului."),

            ("propunerea de scara",
             "\"Propune scara\" masoara textura fina a masterului (sub 0.20 % nu e nimic de "
             + "netezit), apoi incearca cele trei trepte si o bifeaza pe cea mai mica la care "
             + "textura a plecat fara ca asimetria azimutala sau inclinarea sa scada cu peste "
             + "0.5 puncte - cele doua teste pe care textura nu le atinge. Propune, nu decide: "
             + "nu stie ce e al obiectivului si ce e al sursei. ~15 s pe un master de 24 MP."),

            ("cum se citeste harta",
             "Harta principala e original / netezit: alb unde nu s-a scos nimic, culoare unde "
             + "s-a scos. Pe un master curat e zgomot uniform plus punctele de praf. Cea de-a "
             + "doua harta e reziduul flat-ului netezit dupa propriul lui model radial - ce "
             + "face obiectivul, odata textura plecata."),

            ("cum ajunge in Siril",
             "Scripturile numesc pp_flat_stacked explicit si nu iau fisierul netezit singure. "
             + "Redenumeste-l in locul masterului (pastreaza originalul sub alt nume) sau "
             + "da-l la calibrate cu -flat=. Fisierul are headerul masterului, BAYERPAT "
             + "inclusiv, deci Siril il trateaza ca pe un master CFA."),

            ("ce nu se poate verifica de aici",
             "Ca flat-ul netezit chiar corecteaza mai bine: asta o spune doar modul --light "
             + "pe lights calibrate, si pe un camp mai larg de 40 de grade el refuza. Pe o "
             + "lentila larga verificarea ramane harta de aici si un stack de lights intins tare."),
        };

        /// <summary>The overwrite question, when the smoothed file exists already.</summary>
        /// <param name="path">The existing file.</param>
        /// <returns>The question, with the path.</returns>
        internal static string SmoothOverwriteQuestion(string path) =>
            $"Exista deja un flat netezit:\n{path}\n\nIl scriu din nou peste el?";

        /// <summary>The button's label.</summary>
        /// <returns>The label.</returns>
        internal static string SmoothButtonLabel() => "Netezeste si salveaza";

        /// <summary>The label of the scale-proposal button.</summary>
        /// <returns>The label.</returns>
        internal static string SmoothAdviseLabel() => "Propune scara";

        /// <summary>When the user answered "no" to the overwrite: the result stays on screen,
        /// the old file stays on disk.</summary>
        /// <param name="path">The file kept.</param>
        /// <returns>The notice, with the path.</returns>
        internal static string SmoothNotOverwritten(string path) =>
            $"Nu s-a scris nimic: fisierul existent a fost pastrat.\n{path}";

        /// <summary>The refusal screen's title (raw, cube).</summary>
        /// <returns>The title.</returns>
        internal static string SmoothRefusedTitle() => "Nu se poate netezi";

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
            "Scrie un flat radial, pp_flat_stacked" + SirilSession.SyntheticSuffix + ".fit, din profilul de "
            + "vignetare publicat al obiectivului - modelul lui, nu o masuratoare a exemplarului tau. "
            + "N-are nicio sursa de lumina in el, deci nici curbura cerului, nici caderea unui panou; "
            + "n-are nici partea cromatica, nici praful, nici umbrirea specifica body-ului. "
            + "\"Compara cu masterul\" spune cu cat difera masterul fotografiat de model, in stopuri, si in ce forma.";

        /// <summary>The title of the collapsed panel about the profile sources.</summary>
        /// <returns>The title.</returns>
        internal static string SyntheticSourcesTitle() => "Profilele: de unde vin si cum se actualizeaza";

        /// <summary>The write button's label.</summary>
        /// <returns>The label.</returns>
        internal static string SyntheticButtonLabel() => "Scrie flat sintetic";

        /// <summary>The compare button's label.</summary>
        /// <returns>The label.</returns>
        internal static string SyntheticCompareLabel() => "Compara cu masterul";

        /// <summary>The camera is not in lensfun's camera database: the sensor chips are the
        /// fallback.</summary>
        /// <param name="camera">The camera's name from the header, or null or blank when the
        /// reference frame names none.</param>
        /// <returns>The note under the crop field.</returns>
        internal static string SyntheticCropUnknown(string camera) =>
            (string.IsNullOrWhiteSpace(camera) ? "crop necunoscut: cadrul de referinta nu spune ce aparat e" : $"crop necunoscut: \"{camera.Trim()}\" nu e in baza de camere lensfun")
            + " - alege tipul de senzor de mai sus, sau scrie crop-ul (36 impartit la latimea senzorului in mm)";

        /// <summary>The crop came from a sensor chip.</summary>
        /// <param name="label">The chip's label.</param>
        /// <param name="crop">The crop factor, formatted.</param>
        /// <returns>The note under the crop field.</returns>
        internal static string SyntheticCropFromFormat(string label, string crop) =>
            $"crop {crop} nominal, din chip-ul {label}; un aparat din baza lensfun l-ar da exact";

        /// <summary>Where the field's crop is from and which format it is: "crop 1.523 din
        /// baza lensfun (Nikon D3300) - format APS-C".</summary>
        /// <param name="crop">The crop factor, formatted.</param>
        /// <param name="source">Where it came from (<see cref="SyntheticCropTyped"/> or
        /// <see cref="SyntheticCropFromTable"/>).</param>
        /// <param name="formatLabel">The sensor format's label, or
        /// <c>SensorFormats.Unusual</c>.</param>
        /// <returns>The note under the crop field.</returns>
        internal static string SyntheticCropNote(string crop, string source, string formatLabel) =>
            $"crop {crop} {source} - {(formatLabel == SensorFormats.Unusual ? formatLabel + ", niciun chip nu i se potriveste" : "format " + formatLabel)}";

        /// <summary>The crop's source when the user typed it.</summary>
        /// <returns>"tastat".</returns>
        internal static string SyntheticCropTyped() => "tastat";
        /// <summary>The crop's source when lensfun's camera table gave it.</summary>
        /// <param name="camera">The camera's name; not null.</param>
        /// <returns>The source, naming the camera.</returns>
        internal static string SyntheticCropFromTable(string camera) => $"din baza lensfun ({camera.Trim()})";

        /// <summary>A lens ID carried by several lenses: the list narrowed to them, the user
        /// chooses.</summary>
        /// <param name="rawLabel">The lens as the raw names it.</param>
        /// <param name="count">How many lenses match.</param>
        /// <returns>The note under the list.</returns>
        internal static string SyntheticLensCandidates(string rawLabel, int count) =>
            $"{rawLabel} se potriveste cu {count} obiective - lista de mai sus s-a restrans la ele, alege-l pe al tau";

        /// <summary>The overwrite question, when the synthetic file exists already.</summary>
        /// <param name="path">The existing file.</param>
        /// <returns>The question, with the path.</returns>
        internal static string SyntheticOverwriteQuestion(string path) =>
            $"Exista deja un flat sintetic:\n{path}\n\nIl scriu din nou peste el?";

        /// <summary>When the user answered "no" to the overwrite.</summary>
        /// <param name="path">The file kept.</param>
        /// <returns>The notice, with the path.</returns>
        internal static string SyntheticNotOverwritten(string path) =>
            $"Nu s-a scris nimic: fisierul existent a fost pastrat.\n{path}";

        /// <summary>The refusal screen's title.</summary>
        /// <returns>The title.</returns>
        internal static string SyntheticRefusedTitle() => "Nu se poate scrie flatul sintetic";

        /// <summary>When the button is pressed with no lens selected.</summary>
        /// <returns>The prompt.</returns>
        internal static string SyntheticNoLensSelected() => "Alege un obiectiv din lista.";

        /// <summary>When neither source holds a profile.</summary>
        /// <returns>The explanation, naming the sources panel.</returns>
        internal static string SyntheticNoProfiles() =>
            "Niciun profil de obiectiv gasit: nici Adobe pe masina, nici folderul lensfun/ langa program. "
            + "Vezi panoul \"" + SyntheticSourcesTitle() + "\".";

        // The comparison's note comes from AgainstReport.Meaning(), as in the console.

        // ------------------------------------------------------- running the script
        //
        // The texts of the block on the problem screen and of the run window. None interprets
        // what the script did: after the run the folder is reopened and Check() says by itself
        // whether the master appeared.

        /// <summary>What the button does, before the list of scripts.</summary>
        /// <returns>The lead text.</returns>
        internal static string PreprocessLead() =>
            "Scriptul se poate rula de aici, cu Siril-ul instalat pe calculator (siril-cli, "
            + "fara fereastra). Lista de mai jos e cea din meniul de scripturi al lui Siril; "
            + "propus e cel care lasa masterul in process/. Iesirea se vede intr-o fereastra, "
            + "ca in Siril, iar cand o inchizi folderul se redeschide singur.";

        /// <summary>When siril-cli.exe was not found.</summary>
        /// <returns>The explanation, with the places looked in.</returns>
        internal static string SirilCliNotFound() =>
            "Nu gasesc siril-cli.exe: nici prin intrarea de instalare din registru, nici in "
            + "Program Files\\Siril\\bin. Fara el scriptul se ruleaza din Siril, si apoi alegi "
            + "din nou folderul.";

        /// <summary>When no .ssf script was found.</summary>
        /// <param name="configPath">The Siril config read, or null when there is none.</param>
        /// <returns>The explanation, naming the config.</returns>
        internal static string NoSirilScripts(string configPath) =>
            "N-am gasit niciun script .ssf. Lista se ia din configurarea Siril ("
            + (configPath ?? "fisierul de configurare nu exista")
            + "): scripturile bifate in Preferinte > Scripturi si folderele de scripturi de "
            + "acolo. Bifeaza scriptul in Siril - sau ia depozitul de scripturi din acelasi "
            + "loc - si alege din nou folderul.";

        /// <summary>When the run button is pressed with no script selected.</summary>
        /// <returns>The prompt.</returns>
        internal static string NoScriptChosen() => "Alege un script din lista.";

        /// <summary>When the chosen script cannot be read.</summary>
        /// <param name="path">The script's path.</param>
        /// <param name="why">The exception's message.</param>
        /// <returns>The error, with both.</returns>
        internal static string ScriptUnreadable(string path, string why) =>
            $"Nu pot citi scriptul:\n{path}\n\n{why}";

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
            $"Scriptul {script} intra in subfolderele {string.Join(", ", required.Select(f => f + "/"))}. "
            + $"In folderul ales lipsesc sau sunt goale: {string.Join(", ", missing.Select(f => f + "/"))}. "
            + "Pune acolo cadrele de la aparat, apoi apasa din nou.";

        /// <summary>The warning when Siril's window is open
        /// (<see cref="SirilInstall.IsGuiRunning"/>).</summary>
        /// <returns>The warning.</returns>
        internal static string SirilGuiRunning() =>
            "Siril e deschis. Daca are ca folder de lucru chiar folderul asta, doua procese "
            + "ar scrie in acelasi process/ in acelasi timp si fisierele s-ar strica. "
            + "Inchide-l, sau asigura-te ca lucreaza in alt folder, inainte sa apesi Ruleaza.";

        /// <summary>The run window's title.</summary>
        /// <returns>The title.</returns>
        internal static string RunWindowTitle() => "Procesare Siril";

        /// <summary>The line naming the command running.</summary>
        /// <param name="index">The command's number, from 1.</param>
        /// <param name="total">The script's command count; 0 when unknown.</param>
        /// <param name="command">The whole command line.</param>
        /// <returns>"comanda 7 din 20:  ..." or, without a total, "comanda 7:  ...".</returns>
        internal static string RunStep(int index, int total, string command) =>
            total > 0 ? $"comanda {index} din {total}:  {command}" : $"comanda {index}:  {command}";

        /// <summary>The closing line of a successful run.</summary>
        /// <returns>The notice.</returns>
        internal static string RunSucceeded() =>
            "Scriptul s-a terminat cu succes. Inchide fereastra: folderul se redeschide si "
            + "analiza porneste pe ce a produs.";

        /// <summary>The closing line of a failed run.</summary>
        /// <param name="code">siril-cli's exit code.</param>
        /// <param name="step">The command it stopped at, or null or empty when unknown.</param>
        /// <returns>The notice.</returns>
        internal static string RunFailed(int code, string step) =>
            $"Siril s-a oprit cu codul {code}"
            + (string.IsNullOrEmpty(step) ? "." : $", la {step}.")
            + " Log-ul de mai sus spune de ce; folderul ramane cum era.";

        /// <summary>The closing line of a cancelled run.</summary>
        /// <returns>The notice.</returns>
        internal static string RunCancelled() =>
            "Rulare anulata. Siril a fost oprit la mijloc, deci process/ poate contine "
            + "fisiere pe jumatate scrise; o rulare noua le suprascrie.";

        /// <summary>When siril-cli could not be started.</summary>
        /// <param name="why">The exception's message.</param>
        /// <returns>The error.</returns>
        internal static string RunStartFailed(string why) =>
            $"Nu am putut porni siril-cli:\n{why}";

        /// <summary>
        /// siril-cli works on a copy of Siril's settings, so without it it does not start -
        /// otherwise it would work with the default values without the user knowing.
        /// </summary>
        /// <param name="path">The settings file.</param>
        /// <param name="why">The exception's message.</param>
        /// <returns>The error, with both.</returns>
        internal static string SirilSettingsUnreadable(string path, string why) =>
            $"Nu pot citi setarile lui Siril (siril-cli lucreaza pe o copie a lor):\n{path}\n\n{why}\n\n"
            + "Siril nu a pornit. Daca Siril e deschis si tocmai isi salveaza setarile, apasa din nou peste cateva secunde.";

        // ------------------------------------------------------------------ Split
        //
        // The Split tab (ADR 0054): the flats split into temp sessions, the script run in each,
        // the parts' masters beside the whole. The texts below are the screen's; the table, the
        // report and the mosaic's labels are written by the core (SplitReport).

        /// <summary>The Split tab's documentation, above the chips.</summary>
        /// <returns>The intro.</returns>
        internal static string SplitIntro() =>
            "Imparte flat-urile sesiunii in 2 sau 4 grupuri consecutive, in ordinea numelor de "
            + "fisier - cate unul pe orientare, daca ai rotit aparatul intre grupuri - si "
            + "construieste pentru fiecare o sesiune temporara: fiecare folder in care intra "
            + $"scriptul (biases/, darks/) intreg, cel mult {SplitPlan.MaxLights} cadre din lights/ (cat sa "
            + "mearga scriptul pana la capat) si felia lui de flats/; process/ si masters/ nu se "
            + "copiaza niciodata. Ruleaza scriptul Siril ales in fiecare, apoi pune masterele partilor "
            + "langa masterul intreg: cele cinci teste, vignetarea fata de profilul obiectivului si "
            + "hartile reziduale, una langa alta. Sesiunea originala e doar citita; raportul si "
            + "mozaicul se salveaza in ea, iar folderele temporare se sterg la sfarsit.";

        /// <summary>The line counting the flats and the split.</summary>
        /// <param name="count">The frames in flats/.</param>
        /// <param name="parts">The parts chosen.</param>
        /// <param name="perPart">The frames per part; 0 when the count does not divide.</param>
        /// <returns>"30 cadre in flats/  ->  2 x 15", or the count alone.</returns>
        internal static string SplitFramesLine(int count, int parts, int perPart) =>
            perPart > 0 ? $"{count} cadre in flats/  ->  {parts} x {perPart}" : $"{count} cadre in flats/";

        /// <summary>When flats/ holds no frame.</summary>
        /// <param name="s">The session, for its flats/ path.</param>
        /// <returns>The explanation.</returns>
        internal static string SplitNoFrames(SirilSession s) =>
            $"Niciun cadru (raw sau FITS) in {s.FlatsDir}. Impartirea are nevoie de cadrele de la aparat.";

        /// <summary>When the frame count does not divide by the parts.</summary>
        /// <param name="count">The frames in flats/.</param>
        /// <param name="parts">The parts chosen.</param>
        /// <returns>The explanation.</returns>
        internal static string SplitNotDivisible(int count, int parts) =>
            $"{count} cadre nu se impart exact la {parts}. Alege alt numar de parti sau verifica flats/: "
            + "fiecare grup de rotatie trebuie sa aiba acelasi numar de cadre.";

        /// <summary>The start button's label.</summary>
        /// <returns>The label.</returns>
        internal static string SplitStartLabel() => "Start Split";
        /// <summary>The stop button's label.</summary>
        /// <returns>The label.</returns>
        internal static string SplitStopLabel() => "Opreste";

        /// <summary>A part-count chip's label.</summary>
        /// <param name="parts">2 or 4.</param>
        /// <returns>"2 parti" and the like.</returns>
        internal static string SplitPartsLabel(int parts) => $"{parts} parti";

        /// <summary>The phase line while a part is copied.</summary>
        /// <param name="part">The part's number, from 1.</param>
        /// <param name="parts">The parts in total.</param>
        /// <param name="files">The files being copied.</param>
        /// <returns>The phase line.</returns>
        internal static string SplitPhaseCopy(int part, int parts, int files) =>
            $"partea {part} din {parts}: copiez {files} fisiere in folderul temporar";

        /// <summary>The phase line while Siril runs on a part.</summary>
        /// <param name="part">The part's number, from 1.</param>
        /// <param name="parts">The parts in total.</param>
        /// <param name="script">The script's file name.</param>
        /// <returns>The phase line.</returns>
        internal static string SplitPhaseSiril(int part, int parts, string script) =>
            $"partea {part} din {parts}: ruleaza {script}";

        /// <summary>The phase line while the masters are analysed.</summary>
        /// <param name="parts">The parts in total.</param>
        /// <returns>The phase line.</returns>
        internal static string SplitPhaseAnalyse(int parts) =>
            $"analizez cele {parts} parti si intregul";

        /// <summary>The failure screen's title.</summary>
        /// <returns>The title.</returns>
        internal static string SplitFailedTitle() => "Impartirea s-a oprit";

        /// <summary>A part's failure, with the temp folder kept.</summary>
        /// <param name="part">The part's number, from 1.</param>
        /// <param name="parts">The parts in total.</param>
        /// <param name="outcome">What went wrong, already worded.</param>
        /// <param name="tempRoot">The temp root kept.</param>
        /// <returns>The failure text.</returns>
        internal static string SplitFailed(int part, int parts, string outcome, string tempRoot) =>
            $"La partea {part} din {parts}: {outcome}\n\n{SplitTempKept(tempRoot)}";

        /// <summary>When the script ended but left no master in a part.</summary>
        /// <param name="part">The part's number, from 1.</param>
        /// <param name="parts">The parts in total.</param>
        /// <param name="partRoot">The part's folder; its parent is the temp root kept.</param>
        /// <param name="script">The script's file name.</param>
        /// <returns>The failure text.</returns>
        internal static string SplitNoMaster(int part, int parts, string partRoot, string script) =>
            $"La partea {part} din {parts}: {script} s-a terminat, dar n-a lasat {SirilSession.FlatMasterName} "
            + $"nici in masters/, nici in process/ din\n{partRoot}\n\n{SplitTempKept(Path.GetDirectoryName(partRoot))}";

        /// <summary>The note that the temp folder was kept after a failure.</summary>
        /// <param name="tempRoot">The temp root.</param>
        /// <returns>The note.</returns>
        internal static string SplitTempKept(string tempRoot) =>
            $"Folderul temporar a ramas, ca sa poti vedea ce s-a intamplat:\n{tempRoot}\n"
            + "Sterge-l cand nu-ti mai trebuie. Sesiunea originala nu a fost atinsa.";

        /// <summary>When the temp folder could not be deleted.</summary>
        /// <param name="tempRoot">The temp root.</param>
        /// <returns>The note.</returns>
        internal static string SplitTempNotDeleted(string tempRoot) =>
            $"Folderul temporar nu s-a putut sterge: {tempRoot}. Sterge-l de mana.";

        /// <summary>The question when the window is closed during a split.</summary>
        /// <returns>The question.</returns>
        internal static string SplitCloseQuestion() =>
            "Impartirea inca ruleaza. O opresc? Siril e oprit la mijloc, folderul temporar se "
            + "sterge, si fereastra se inchide dupa aceea. Sesiunea originala nu e atinsa.";

        /// <summary>Where the report and the mosaic were saved.</summary>
        /// <param name="reportPath">The report's path.</param>
        /// <param name="pngPath">The mosaic's path.</param>
        /// <returns>Two lines.</returns>
        internal static string SplitSaved(string reportPath, string pngPath) =>
            $"Raport salvat: {reportPath}\nMozaic salvat: {pngPath}";

        // ------------------------------------------------------------------ Despre
        //
        // The texts of the "Despre" window. The figure, the version and the warranty notice do
        // NOT sit here: they come from VersionInfo and RunReport, the same as the console's.
        // Here are only the titles and sentences the console has nowhere to print.

        /// <summary>The "Despre" window's title.</summary>
        /// <returns>The title.</returns>
        internal static string AboutTitle() => "Despre FlatCheck";

        /// <summary>The README's first line, what the tool does in one sentence.</summary>
        /// <returns>The tagline.</returns>
        internal static string AboutTagline() =>
            "Diagnostic cantitativ pentru flat frames de astrofoto.";

        /// <summary>The licence section's heading.</summary>
        /// <returns>The heading.</returns>
        internal static string AboutLicenceHeading() => "LICENTA";

        /// <summary>The label of the link to the GPLv3 text.</summary>
        /// <returns>The label.</returns>
        internal static string AboutLicenseLinkLabel() =>
            "Textul complet al GNU General Public License v3";

        /// <summary>The third-party section's heading.</summary>
        /// <returns>The heading.</returns>
        internal static string AboutThirdPartyHeading() => "COMPONENTE TERTE";

        /// <summary>
        /// Why the list is here and not only in a file: LibRaw's LGPL asks for the notice where
        /// the program shows its own copyright, and an exe copied alone has no notices file
        /// beside it. The compatibility reasons stay in the file.
        /// </summary>
        /// <returns>The lead above the list.</returns>
        internal static string AboutThirdPartyLead() =>
            "Unealta leaga trei componente scrise de altii, toate compatibile cu GPLv3. "
            + "Cititorul FITS, encoderul PNG, toate calculele si toate pragurile sunt scrise "
            + "de mana. Motivele pentru care fiecare licenta e compatibila sunt in "
            + "THIRD-PARTY-NOTICES.md, langa sursa.";
    }
}
