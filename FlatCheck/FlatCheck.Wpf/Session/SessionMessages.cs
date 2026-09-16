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

// SessionMessages.cs - explicatiile pe care le da aplicatia cand intrarea nu e buna.
//
// Stau intr-un singur loc, ca textele din Report/ ale nucleului, si din acelasi motiv:
// sunt singurul lucru pe care il citeste omul cand ceva nu merge, deci merita scrise o
// data si bine, nu improvizate in trei locuri din interfata. Ca sunt aici si nu in
// XAML le face si verificabile - un test poate cere ca mesajul sa numeasca scriptul
// Siril, ceea ce e chiar cerinta, nu o preferinta de formulare.
//
// Tine si ghidul hartii - vezi nota de la MapGuideIntro. Nu e un mesaj de eroare, dar
// e acelasi fel de text: documentatie aratata in fereastra, scrisa o data si bine.

using System.Collections.Generic;
using System.Linq;

namespace FlatCheck.Wpf
{
    internal static class SessionMessages
    {
        internal static string Title(SessionProblem p) => p switch
        {
            SessionProblem.NoFolder => "Folderul nu exista",
            SessionProblem.NoProcessDir => "Nu s-a rulat niciun script de preprocesare",
            SessionProblem.NoFlatMaster => "Lipseste masterul de flat",
            _ => "",
        };

        /// <summary>
        /// Cerinta 4: cand masterul lipseste, aplicatia nu spune doar "nu gasesc
        /// fisierul" - spune ce sa faca omul ca sa apara, adica sa ruleze scriptul.
        /// Un mesaj care descrie doar simptomul lasa utilizatorul sa caute singur
        /// legatura dintre un fisier lipsa si un script pe care poate nu l-a rulat
        /// niciodata.
        /// </summary>
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
        /// Cerinta 6: cand omul alege de mana un fisier care nu e master de flat, i se
        /// explica ce sunt fisierele astea si de ce testul are nevoie de unul, in loc sa
        /// primeasca o eroare de parsare de la cititorul FITS peste cateva secunde.
        /// </summary>
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

        /// <summary>
        /// Documentatia tabului, aratata inainte de orice calcul. Modul asta e singurul
        /// care cere ceva ce nu se poate deduce dintr-un folder, deci e singurul care
        /// trebuie sa explice ce vrea INAINTE, nu dupa ce a esuat.
        /// </summary>
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
        /// Acelasi fisier in ambele sloturi. Prins la alegere, nu la calcul, ca sa nu se
        /// piarda selectia buna de dinainte.
        /// </summary>
        internal static string SameFileTwice() =>
            "Testul compara doua stive DIFERITE: una dinainte de rotatia aparatului, una "
            + "de dupa. Impartind un fisier la el insusi iese 1 peste tot, deci o "
            + "componenta externa de exact zero - care nu inseamna \"setup curat\", "
            + "inseamna \"nu s-a masurat nimic\".";

        /// <summary>
        /// Masterul exista si in masters/, si in process/: doua preprocesari pe acelasi
        /// folder. Se masoara cel mai nou, si omul trebuie sa stie care, fiindca cele doua
        /// pot veni din seturi de flat-uri diferite.
        /// </summary>
        internal static string TwoFlatMasters(FlatMasterLookup m) =>
            "Sunt DOUA mastere de flat in folderul asta, in masters/ si in process/ - doua "
            + "rulari de preprocesare. Se masoara cel mai nou:\n" + m.Path + "\n"
            + "Celalalt, mai vechi, nu e citit:\n" + m.Other + "\n"
            + "Daca nu e cel pe care il vrei, sterge-l sau redenumeste-l pe celalalt si "
            + "alege din nou folderul.";

        /// <summary>Cand process/ nu contine cadre calibrate pentru testul end-to-end.</summary>
        internal static string NoLightSeries(SirilSession s) =>
            $"Niciun cadru care sa se potriveasca cu {SeriesFinder.DefaultPattern} in:\n"
            + $"{s.ProcessDir}\n\n"
            + "Testul end-to-end are nevoie de lights CALIBRATE cu flat, FARA background "
            + "extraction si NEREGISTRATE.\n\n"
            + "Cadrele registrate (r_pp_light_*) sunt excluse intentionat: registrarea "
            + "transforma geometric cadrul, deci muta eroarea de flat - care e fixa pe "
            + "senzor - fata de centrul in jurul caruia se fiteaza modelul radial. Ar "
            + "corupe masuratoarea fara sa se vada.";

        /// <summary>Cand flats/ nu contine niciun raw de aparat.</summary>
        internal static string NoRawFlats(SirilSession s) =>
            $"Niciun fisier raw de aparat in:\n{s.FlatsDir}\n\n"
            + "Verificarea expunerii ruleaza pe un flat BRUT, NECALIBRAT, cu mozaicul CFA "
            + "intact si scala ADU originala - stivuirea normalizeaza exact nivelul care se "
            + "masoara aici, deci un master nu se poate folosi.\n\n"
            + "Cel mai simplu e sa lasi in flats/ chiar fisierele de la aparat "
            + "(.nef, .cr2, .cr3, .arw, .dng, .raf). Pe ele white si black se citesc exact "
            + "din fisier, nu se mai ghicesc din maxim.";

        // --------------------------------------------------- cum citesc tabul
        //
        // Intrebarea "e dubios?" are doua jumatati, si numai una e a ochiului. Textul asta
        // exista fiindca despartirea nu se vede din asezarea ecranului: cifrele din stanga
        // si harta din dreapta arata ca doua vederi ale aceluiasi lucru, cand de fapt una
        // decide si cealalta explica.

        /// <summary>Ce decid cifrele si ce decide ochiul - intro-ul de deasupra celor doua randuri.</summary>
        internal static string HowToReadIntro() =>
            "Tabul raspunde la doua intrebari diferite, si nu cu aceleasi mijloace.";

        /// <summary>Cele doua jumatati ale citirii, in ordinea in care se folosesc.</summary>
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

        // --------------------------------------------------- ghidul histogramelor
        //
        // Acelasi fel de text ca ghidul hartii de mai jos: documentatie aratata in
        // fereastra, pliata, scrisa o data. Si aceeasi limita - explica ce se vede, NU
        // clasifica. Verdictul ramane mediana ferestrei centrale a canalului celui mai
        // luminos, si nimic din desen nu voteaza.
        //
        // Tabelul e in oglinda cu README, sectiunea despre tabul Expunere. NU exista test
        // care sa lege cele doua - daca reformulezi aici, treci si pe acolo.

        /// <summary>Ce e axa si ce rol are desenul, inainte de lista de citiri.</summary>
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
        /// Cum se citeste desenul, in ordinea in care merita privit: intai cele doua
        /// curbe, apoi cifra care decide, apoi benzile, apoi capcanele.
        /// </summary>
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

        // ------------------------------------------------------- ghidul hartii
        //
        // DE CE E DOCUMENTATIE SI NU DIAGNOSTIC. Unealta nu clasifica forma hartii si nu
        // trebuie s-o faca: o pata poate fi light leak sau un nor, iar din pixeli nu se
        // poate deosebi. Ce se poate face e sa i se spuna omului ce a insemnat fiecare
        // forma pe mastere reale, si care e testul care desparte cazurile. Deci: rows de
        // text, fara verdict, fara prag, fara nicio cifra calculata aici.
        //
        // Textul e in oglinda cu tabelul din README, sectiunea "Iesire PNG". NU exista
        // test care sa lege cele doua - daca reformulezi aici, treci si pe acolo.

        /// <summary>Introducerea ghidului: cum se citeste harta, inainte de lista de forme.</summary>
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
        /// Formele intalnite pe harta si ce au insemnat, in ordinea in care merita
        /// cautate: de la cea mai des intalnita si mai inofensiva, spre cele care cer un
        /// contra-test anume.
        /// </summary>
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
             + "intre timp - profilul se ia acum din rasterul simetrizat la 180 de grade."),

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

        // --------------------------------------------------- tabul de histograma

        /// <summary>
        /// Cand nu e nici master, nici vreun cadru citibil in flats/. Prin MainViewModel nu
        /// se ajunge aici - Check() cere masterul - dar tabul se ataseaza si in teste.
        /// </summary>
        internal static string NoFlatFrames(SirilSession s) =>
            $"Nimic de egalizat: nici masterul {SirilSession.FlatMasterName} in masters/ sau process/, nici "
            + $"vreun cadru citibil in:\n{s.FlatsDir}\n\n"
            + "Tabul arata cadrele asa cum au fost trase - raw de aparat (.nef, .cr2, .cr3, "
            + ".arw, .dng, .raf) sau FITS (.fit, .fits, .fts) - si, pe prima pozitie, masterul "
            + "facut din ele, ca sa se vada ce s-a mediat si ce a ramas.";

        // Ghidul de mai jos e in oglinda cu tabelul din README ("Histogram"). Nu exista
        // test care sa lege cele doua - daca reformulezi aici, treci si pe acolo.

        /// <summary>Ce e poza, inainte de lista de citiri.</summary>
        internal static string HistEqGuideIntro() =>
            "E vederea \"Histogram\" din Siril, refacuta: imaginea trecuta prin propria ei "
            + "functie de repartitie, astfel incat fiecare nivel de gri sa primeasca acelasi "
            + "numar de pixeli. Nu e un grafic si nu e o masuratoare. Pe un flat, a carui "
            + "distributie e ingusta fata de scala fisierului, curba e abrupta exact pe "
            + "intervalul vignetarii, si tot ce nu e uniform iese la contrast maxim.\n\n"
            + "Poza EXPLICA, nu voteaza: nicio cifra din unealta nu se schimba cu ce se vede "
            + "aici, si nicio forma de aici nu intra in vreun verdict.";

        /// <summary>Cum se citeste poza, in ordinea in care merita privita.</summary>
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

        // ------------------------------------------------------- netezire
        //
        // Ghidul tabului de netezire. Ca celelalte, e in oglinda cu README ("Netezire")
        // fara test care sa le lege. Spre deosebire de celelalte e DESCHIS implicit: e
        // preconditia tabului, ca documentatia de pe Ratio - un buton care scrie un fisier
        // de 96 MB langa master nu se apasa inainte de a sti ce face.

        /// <summary>Ce face butonul si ce nu poate sti, inainte de lista.</summary>
        internal static string SmoothGuideIntro() =>
            "Scrie un al doilea master, pp_flat_stacked" + SirilSession.SmoothedSuffix + ".fit, langa cel "
            + "fotografiat: acelasi flat, cu tot ce e neted pastrat si tot ce nu e scos - textura "
            + "difuzorului, umbrele de praf, petele compacte. Nu e o analiza si nu da verdict; "
            + "ce raporteaza e CE A SCOS, fiindca asta e singurul lucru dupa care se poate "
            + "decide daca fisierul e de incredere.\n\n"
            + "Masterul fotografiat ramane neatins si ramane masterul sesiunii: tabul Structura "
            + "arata in continuare textura, ca sa se vada de ce a fost nevoie de netezire.";

        /// <summary>Ce pastreaza, ce scoate, scara, cum ajunge in Siril.</summary>
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

        /// <summary>Intrebarea de suprascriere, cand fisierul netezit exista deja.</summary>
        internal static string SmoothOverwriteQuestion(string path) =>
            $"Exista deja un flat netezit:\n{path}\n\nIl scriu din nou peste el?";

        /// <summary>Eticheta butonului.</summary>
        internal static string SmoothButtonLabel() => "Netezeste si salveaza";

        /// <summary>Eticheta butonului de propunere a scarii.</summary>
        internal static string SmoothAdviseLabel() => "Propune scara";

        /// <summary>Cand omul a raspuns "nu" la suprascriere: rezultatul ramane pe ecran, fisierul vechi ramane pe disc.</summary>
        internal static string SmoothNotOverwritten(string path) =>
            $"Nu s-a scris nimic: fisierul existent a fost pastrat.\n{path}";

        /// <summary>Titlul ecranului de refuz (raw, cub).</summary>
        internal static string SmoothRefusedTitle() => "Nu se poate netezi";

        // ------------------------------------------------------- rularea scriptului
        //
        // Textele blocului de pe ecranul de problema si ale ferestrei de rulare. Niciunul
        // nu interpreteaza ce a facut scriptul: dupa rulare folderul se redeschide si
        // Check() spune singur daca masterul a aparut.

        /// <summary>Ce face butonul, inainte de lista de scripturi.</summary>
        internal static string PreprocessLead() =>
            "Scriptul se poate rula de aici, cu Siril-ul instalat pe calculator (siril-cli, "
            + "fara fereastra). Lista de mai jos e cea din meniul de scripturi al lui Siril; "
            + "propus e cel care lasa masterul in process/. Iesirea se vede intr-o fereastra, "
            + "ca in Siril, iar cand o inchizi folderul se redeschide singur.";

        internal static string SirilCliNotFound() =>
            "Nu gasesc siril-cli.exe: nici prin intrarea de instalare din registru, nici in "
            + "Program Files\\Siril\\bin. Fara el scriptul se ruleaza din Siril, si apoi alegi "
            + "din nou folderul.";

        internal static string NoSirilScripts(string configPath) =>
            "N-am gasit niciun script .ssf. Lista se ia din configurarea Siril ("
            + (configPath ?? "fisierul de configurare nu exista")
            + "): scripturile bifate in Preferinte > Scripturi si folderele de scripturi de "
            + "acolo. Bifeaza scriptul in Siril - sau ia depozitul de scripturi din acelasi "
            + "loc - si alege din nou folderul.";

        internal static string NoScriptChosen() => "Alege un script din lista.";

        internal static string ScriptUnreadable(string path, string why) =>
            $"Nu pot citi scriptul:\n{path}\n\n{why}";

        /// <summary>
        /// Ce ii lipseste folderului ca sa poata porni scriptul ales. Se spune si ce cere
        /// scriptul in total, ca lipsa sa aiba context.
        /// </summary>
        internal static string MissingScriptInputs(string script, IEnumerable<string> required,
                                                   IEnumerable<string> missing) =>
            $"Scriptul {script} intra in subfolderele {string.Join(", ", required.Select(f => f + "/"))}. "
            + $"In folderul ales lipsesc sau sunt goale: {string.Join(", ", missing.Select(f => f + "/"))}. "
            + "Pune acolo cadrele de la aparat, apoi apasa din nou.";

        internal static string SirilGuiRunning() =>
            "Siril e deschis. Daca are ca folder de lucru chiar folderul asta, doua procese "
            + "ar scrie in acelasi process/ in acelasi timp si fisierele s-ar strica. "
            + "Inchide-l, sau asigura-te ca lucreaza in alt folder, inainte sa apesi Ruleaza.";

        internal static string RunWindowTitle() => "Procesare Siril";

        internal static string RunStep(int index, int total, string command) =>
            total > 0 ? $"comanda {index} din {total}:  {command}" : $"comanda {index}:  {command}";

        internal static string RunSucceeded() =>
            "Scriptul s-a terminat cu succes. Inchide fereastra: folderul se redeschide si "
            + "analiza porneste pe ce a produs.";

        internal static string RunFailed(int code, string step) =>
            $"Siril s-a oprit cu codul {code}"
            + (string.IsNullOrEmpty(step) ? "." : $", la {step}.")
            + " Log-ul de mai sus spune de ce; folderul ramane cum era.";

        internal static string RunCancelled() =>
            "Rulare anulata. Siril a fost oprit la mijloc, deci process/ poate contine "
            + "fisiere pe jumatate scrise; o rulare noua le suprascrie.";

        internal static string RunStartFailed(string why) =>
            $"Nu am putut porni siril-cli:\n{why}";

        // ------------------------------------------------------------------ Despre
        //
        // Textele ferestrei "Despre". Cifra, versiunea si notita de garantie NU stau aici:
        // vin din VersionInfo si RunReport, aceleasi cu ale consolei. Aici sunt doar
        // titlurile si propozitiile pe care consola n-are unde sa le tipareasca.

        internal static string AboutTitle() => "Despre FlatCheck";

        /// <summary>Prima linie din README, ce face unealta intr-o propozitie.</summary>
        internal static string AboutTagline() =>
            "Diagnostic cantitativ pentru flat frames de astrofoto.";

        internal static string AboutLicenceHeading() => "LICENTA";

        internal static string AboutLicenseLinkLabel() =>
            "Textul complet al GNU General Public License v3";

        internal static string AboutThirdPartyHeading() => "COMPONENTE TERTE";

        /// <summary>
        /// De ce e lista aici si nu doar intr-un fisier: LGPL-ul LibRaw cere notita acolo
        /// unde programul isi arata propriul copyright, si un exe copiat singur n-are
        /// fisierul de notite langa el. Motivele de compatibilitate raman in fisier.
        /// </summary>
        internal static string AboutThirdPartyLead() =>
            "Unealta leaga trei componente scrise de altii, toate compatibile cu GPLv3. "
            + "Cititorul FITS, encoderul PNG, toate calculele si toate pragurile sunt scrise "
            + "de mana. Motivele pentru care fiecare licenta e compatibila sunt in "
            + "THIRD-PARTY-NOTICES.md, langa sursa.";
    }
}
