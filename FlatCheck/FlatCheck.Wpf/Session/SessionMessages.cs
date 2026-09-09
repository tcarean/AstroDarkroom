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

namespace FlatCheck.Wpf
{
    internal static class SessionMessages
    {
        internal static string Title(SessionProblem p) => p switch
        {
            SessionProblem.NoFolder => "Folderul nu exista",
            SessionProblem.NoProcessDir => "Lipseste subfolderul process/",
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
                + "flats/, darks/, biases/, lights/ si process/.",

            SessionProblem.NoProcessDir =>
                $"Folderul ales nu are subfolderul process/:\n{s.Root}\n\n"
                + "Acolo isi pune Siril rezultatele calibrarii, deci lipsa lui inseamna ca "
                + $"nu s-a rulat inca niciun script de preprocesare.\n\n"
                + $"Deschide sesiunea in Siril si ruleaza scriptul {SirilSession.PreprocessingScript}, "
                + "apoi alege din nou folderul.",

            SessionProblem.NoFlatMaster =>
                $"Nu gasesc masterul de flat:\n{s.FlatMasterPath}\n\n"
                + $"Asta e fisierul pe care il produce scriptul {SirilSession.PreprocessingScript} "
                + "din Siril: flat-urile stivuite si CALIBRATE (prefixul \"pp_\" inseamna "
                + "exact ca flat-dark-ul si offsetul au fost deja scazute). Toate testele de "
                + "structura pornesc de la el.\n\n"
                + $"Deschide sesiunea in Siril si ruleaza {SirilSession.PreprocessingScript}, "
                + "apoi alege din nou folderul.",

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
            + "stive calibrate de flat-uri - fisierele pe care Siril le scrie in process/ cu "
            + $"numele de forma {SirilSession.RatioPattern}.\n\n"
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
            + "cate unul in fiecare process/.\n\n"

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
             + "aparat sau in lume; daca reziduul e mare si inclinarea mica, e ceva local - "
             + "pete sau un colt. Ce ramane pentru ochi e ghidul de sub harta."),
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
             + "backlight-ul unui monitor. Testul prin rotatie e orb la ea prin "
             + "constructie; contra-testul e distanta - un set la 30 cm, unul la 60."),

            ("granulatie fina peste tot",
             "Zgomot, nu structura: prea putine cadre sau expunere prea joasa. Cifrele "
             + "raman mici; nu e nimic de reparat la sursa, doar de stivuit mai mult."),
        };
    }
}
