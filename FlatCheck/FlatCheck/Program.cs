// FlatCheck.cs - diagnostic cantitativ pentru master flats (port .NET al flatcheck.py)
// =====================================================================================
//
// CITESTE ASTA INTAI: CE FISIER II DAI
// ------------------------------------
// Cea mai probabila cauza de rezultat gresit nu e in cod, e in fisierul ales.
// Scriptul are nevoie de flat-uri din care s-a SCAZUT DEJA flat-dark-ul (offset-ul).
// Dupa scriptul Siril (OSC_Preprocessing / _WithoutDark), fisierul corect este:
//
//     process/pp_flat_stacked.fit        <- prefixul "pp_" = calibrat. ASTA.
//
// NU un .NEF brut, NU un stack necalibrat. Offset-ul (~150-600 ADU) aplatizeaza
// profilul, subraporteaza vignetarea cu ~1%, iar toate testele vor spune "OK"
// pentru ca profilul ramane simetric radial - doar amplitudinea e gresita.
//
// FORMATE ACCEPTATE: FITS (.fit/.fits/.fts), 16-bit int sau 32-bit float, mono sau
// color (ia media pe canale). Pentru NEF brut, lasa Siril sa produca FITS-ul - exact
// ce faci deja in workflow. (Un decoder RAW in .NET ar adauga o dependinta fragila
// fara castig, de vreme ce pipeline-ul tau trece oricum prin Siril.)
//
// IDEEA DE BAZA
// -------------
// Pentru un obiectiv fotografic fara tren optic (Samyang 135 f/2 pe D3300), un flat
// CORECT este, prin constructie, o functie *pur radiala* centrata pe axa optica.
// Orice abatere de la simetria radiala NU e vignetare - e eroare (sursa neuniforma,
// light leak, camera inclinata, ambient). "Flat bun" = "abaterea de la simetria
// radiala e sub zgomot". E o cifra, si o poti masura.
//
// TREI MODURI
// -----------
//   dotnet run -- process/pp_flat_stacked.fit                  # 1. STRUCTURA
//   dotnet run -- pp_flat_A.fit --ratio pp_flat_B.fit          # 2. ROTATIE 180
//   dotnet run -- process/pp_light_00001.fit --light           # 3. END-TO-END
//   dotnet run -- flat_brut.fit --expo                          # 4. EXPUNERE
//
// 4. EXPUNERE: pe un SINGUR flat BRUT NECALIBRAT (convertit in Siril, nu master -
//    stacking-ul normalizeaza si sterge nivelul). Spune cat de sus e CENTRUL cadrului
//    fata de saturatie si in ce directie sa modifici. Masoara CENTRUL (fereastra 20%),
//    nu mediana globala: un flat are distributie larga (centru luminos, colturi
//    intunecate = vignetarea), iar mediana globala e trasa in jos de colturi si te-ar
//    pacali sa supraexpui pana clipezi centrul. Tinta: centru la 50-65%. Histograma
//    camerei/ViewNX arata imaginea PROCESATA (debayer+WB+gamma) si pare "la limita"
//    cand datele brute sunt la ~40% - foloseste Siril pt histograma pe date brute.
//    Scala (white) auto-detectata; daca greseste, --sat (si --black). white-ul exact:
//    un cadru supraexpus, maxul de acolo E saturatia. (C# nu citeste NEF - flat Siril.)
//    Pentru SKY FLATS (blue hour): adauga --sky. Cerul se intuneca continuu si e plin
//    de stele slabe, deci un nivel modest (canal dominant 15-50%) e CORECT - nu-l urca,
//    SNR-ul vine din numarul de cadre. Fara --sky, un sky flat la 22% ar fi raportat
//    gresit ca "prea jos".
//    CUM OBTII UN FITS BRUT IN SIRIL (pt --expo): NU deschide NEF-ul si "Save" - asta il
//    DEBAYERIZEAZA si normalizeaza, iar --expo il respinge (canale CFA identice, nivel
//    ~0). Foloseste tab-ul CONVERSION: adauga NEF-urile -> Convert. Fisierul rezultat
//    pastreaza mozaicul CFA brut, la scala ADU originala.
//
// 1. STRUCTURA: e master flat-ul simetric radial? Ruleaza pe masterul final.
// 2. ROTATIE 180: valideaza SETUP-ul. Tragi 15 flats, rotesti camera 180 grade in
//    jurul axei optice, mai tragi 15, stackuiesti separat. Vignetarea+praful sunt
//    fixe pe senzor (identice in ambele); orice gradient extern se inverseaza -> apare
//    in A/B la ~2x amplitudine. PUNCT ORB: contaminare simetrica radial in jurul axei
//    (ex: cadere de backlight centrata pe monitor) trece nedetectata. Pt asta ai
//    testul de distanta: un set la 30cm, unul la 60cm, --ratio intre ele.
// 3. END-TO-END: un light CALIBRAT cu flat, dar FARA background extraction. Fondul nu
//    trebuie sa aiba componenta radiala. Gradientul de poluare (liniar) e separat de
//    vignetarea reziduala (radial) printr-un fit simultan plan+radial -> merge si pe
//    cer poluat. Semnul: fond care CADE spre margini = sub-corectie; care CRESTE =
//    supra-corectie (aproape sigur offset: flat-darks lipsa, sau lights necalibrate
//    cu bias). Modul 3 e singurul care inchide bucla; 1 si 2 sunt proxy.
//
// ISO-ul NU CONTEAZA: fiecare metrica e un raport, ISO e un scalar, se simplifica.
//
// SCARA celor trei teste de abatere (imprastiere colturi, asimetrie azimutala,
// reziduu dupa model radial). Toate trei masoara aceeasi abatere de la simetria
// radiala, din unghiuri diferite, deci impart o scara:
//     sub  2 %   calitate perfecta
//      2 - 10 %  utilizabil; background extraction scoate ce ramane
//     10 - 20 %  utilizabil, dar confirma cu --light inainte de un stretch dur
//     peste 20 % refa flat-urile
// Cel mai important e REZIDUUL DUPA MODEL RADIAL: masoara direct cat din cadru nu se
// explica printr-un model pur radial, adica exact eroarea de flat. Celelalte doua sunt
// vederi partiale ale aceleiasi abateri.
//
// PRAGURILE de mai jos sunt reguli empirice, nu constante fizice. Un punct de plecare.
// Ruleaza o data pe un setup despre care esti sigur si calibreaza-le pe cifrele tale.

using System;

namespace FlatCheck
{
    static class Program
    {
        internal static int Main(string[] args)
            => FlatCheckCli.Run(args, Console.Out, Console.Error);
    }
}
