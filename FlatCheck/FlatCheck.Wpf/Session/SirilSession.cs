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

// SirilSession.cs - ce inseamna, pe disc, "folderul unei sesiuni".
//
// Aplicatia de consola primeste fisiere; asta primeste UN folder si deduce restul prin
// conventia scripturilor Siril (flats/ darks/ biases/ lights/ process/). Tot ce tine de
// deducerea aia sta aici, si nu in ferestre, dintr-un motiv practic: e singura parte a
// aplicatiei WPF care are reguli adevarate - ce fisier e masterul, care fisiere sunt
// candidati pentru raport, care sunt raw-uri - si e singura care se poate testa fara sa
// deschizi o fereastra.
//
// Clasa nu atinge nimic si nu arunca la constructie: intreaba discul abia cand e
// intrebata. Asa un folder ales gresit devine o stare a interfetei, nu o exceptie.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FlatCheck.Wpf
{
    /// <summary>De ce nu se poate porni pe folderul asta. <see cref="None"/> = se poate.</summary>
    internal enum SessionProblem
    {
        None,
        NoFolder,        // calea nu exista deloc
        NoProcessDir,    // nu s-a rulat niciun script de preprocesare
        NoFlatMaster,    // process/ exista, dar masterul de flat nu
    }

    /// <summary>
    /// Ce fel de rand e in lista tabului de histograma. Masterul e stiva celorlalte;
    /// flat-ul netezit e masterul fara textura, scris de tabul Netezire; un cadru e un
    /// cadru. Un bool nu poate spune trei lucruri.
    /// </summary>
    internal enum HistEqKind { Master, Smoothed, Frame }

    /// <summary>
    /// Un fisier pe care il poate arata tabul de histograma: masterul din masters/ sau
    /// process/, flat-ul netezit de langa el cand exista, sau un cadru din flats/.
    /// </summary>
    internal readonly record struct HistEqCandidate(string Path, HistEqKind Kind);

    /// <summary>
    /// Rezultatul cautarii masterului: calea aleasa (null = nu e nicaieri) si, cand
    /// exista in ambele locuri, calea celui mai vechi, pe care nu il masoara nimeni.
    /// </summary>
    internal readonly record struct FlatMasterLookup(string Path, string Other)
    {
        internal bool Ambiguous => Other != null;
    }

    internal sealed class SirilSession
    {
        /// <summary>Numele pe care il da scriptul OSC_Preprocessing.ssf masterului de flat.</summary>
        internal const string FlatMasterName = "pp_flat_stacked.fit";

        /// <summary>
        /// Scriptul de preprocesare numit in mesaje si propus implicit de butonul de
        /// rulare: cel pe care il foloseste autorul (aparat fara darks), din depozitul de
        /// scripturi Siril, nu din instalare - vezi SirilInstall pentru unde e fisierul.
        /// Lasa masterul in process/; scriptul standard din instalarea 1.4 il lasa in
        /// masters/, si ambele locuri se accepta - vezi <see cref="FlatMasterCandidates"/>.
        /// </summary>
        internal const string PreprocessingScript = "OSC_Preprocessing_WithoutDark.ssf";

        /// <summary>
        /// Tiparul fisierelor acceptate de testul de rotatie 180: MASTERE de flat, adica
        /// stive, nu cadre individuale.
        ///
        /// A fost "pp_flat*.fit", si ala prindea si cadrele calibrate individuale pe care
        /// Siril le scrie in acelasi folder cu acelasi prefix - pp_flat_00001.fit si
        /// urmatoarele. Exact capcana pentru care s-a scos lista de candidati: o pereche
        /// de cadre individuale da un rezultat sigur si fals, fiindca zgomotul unui
        /// singur cadru e de acelasi ordin cu semnalul cautat. Dialogul le propunea in
        /// continuare.
        ///
        /// "*" de dupa "stacked" ramane fiindca cele doua mastere pot ajunge in acelasi
        /// folder, iar atunci macar unul trebuie redenumit (pp_flat_stacked_0deg.fit);
        /// numele stivei ramane recunoscut, cadrele individuale nu intra.
        /// </summary>
        internal const string RatioPattern = "pp_flat_stacked*.fit";

        /// <summary>Raportul de structura, salvat langa datele sesiunii.</summary>
        internal const string ResultFileName = "Rezultat testare.txt";

        internal string Root { get; }

        internal SirilSession(string root)
        {
            Root = root ?? throw new ArgumentNullException(nameof(root));
        }

        internal string ProcessDir => Path.Combine(Root, "process");
        internal string MastersDir => Path.Combine(Root, "masters");
        internal string FlatsDir => Path.Combine(Root, "flats");

        /// <summary>
        /// Unde poate sta masterul de flat, in ordinea in care se cauta. Doua conventii
        /// Siril coexista: scripturile din depozit (WithoutDark si fratii lui) il lasa in
        /// process/, langa cadrele calibrate; scripturile din instalarea 1.4
        /// (OSC_Preprocessing.ssf) il scriu in masters/. Ambele exista in arhiva reala,
        /// deci se accepta amandoua.
        /// </summary>
        internal IReadOnlyList<string> FlatMasterCandidates => new[]
        {
            Path.Combine(MastersDir, FlatMasterName),
            Path.Combine(ProcessDir, FlatMasterName),
        };

        /// <summary>
        /// Intrarea modului 1: masterul gasit in unul din cele doua locuri, sau null.
        /// Cand exista in amandoua se ia CEL MAI NOU - e ce a produs ultima preprocesare -
        /// si <see cref="FlatMasterLookup.Other"/> il numeste pe celalalt, ca tabul sa
        /// poata avertiza: doua mastere in acelasi folder inseamna doua rulari, si omul
        /// trebuie sa stie pe care il masoara.
        /// </summary>
        internal FlatMasterLookup FindFlatMaster()
        {
            var existing = FlatMasterCandidates.Where(File.Exists).ToList();
            if (existing.Count == 0) return new FlatMasterLookup(null, null);
            if (existing.Count == 1) return new FlatMasterLookup(existing[0], null);

            var byAge = existing.OrderByDescending(File.GetLastWriteTimeUtc).ToList();
            return new FlatMasterLookup(byAge[0], byAge[1]);
        }

        /// <summary>Calea masterului gasit, sau null. Vezi <see cref="FindFlatMaster"/>.</summary>
        internal string FlatMasterPath => FindFlatMaster().Path;

        /// <summary>
        /// Sufixul flat-ului netezit: "pp_flat_stacked_netezit.fit", langa masterul din care
        /// s-a facut. Numele NU e unul din <see cref="FlatMasterCandidates"/> - cai exacte,
        /// nu tipare - deci masterul sesiunii ramane cel fotografiat si tabul Structura
        /// arata in continuare textura; fisierul netezit nu trebuie sa ascunda problema
        /// pentru care a fost facut. Se potriveste insa cu <see cref="RatioPattern"/>, ceea
        /// ce e corect: o stiva netezita e tot o stiva.
        /// </summary>
        internal const string SmoothedSuffix = "_netezit";

        /// <summary>Unde scrie tabul de netezire, sau null cand nu exista master.</summary>
        internal string SmoothedMasterPath
        {
            get
            {
                string master = FlatMasterPath;
                return master == null ? null : SmoothedPathBeside(master);
            }
        }

        /// <summary>Numele flat-ului netezit facut din <paramref name="master"/>, in acelasi folder.</summary>
        internal static string SmoothedPathBeside(string master)
            => Path.Combine(Path.GetDirectoryName(master),
                Path.GetFileNameWithoutExtension(master) + SmoothedSuffix + Path.GetExtension(master));

        /// <summary>
        /// Sufixul flat-ului SINTETIC (tabul Sintetic): pp_flat_stacked_sintetic.fit, langa
        /// master, din aceleasi motive ca la cel netezit - nu devine masterul sesiunii, dar
        /// se potriveste cu RatioPattern si cu ce asteapta --against.
        /// </summary>
        internal const string SyntheticSuffix = "_sintetic";

        /// <summary>Unde scrie tabul Sintetic, sau null cand nu exista master.</summary>
        internal string SyntheticMasterPath
        {
            get
            {
                string master = FlatMasterPath;
                return master == null ? null : SyntheticPathBeside(master);
            }
        }

        internal static string SyntheticPathBeside(string master)
            => Path.Combine(Path.GetDirectoryName(master),
                Path.GetFileNameWithoutExtension(master) + SyntheticSuffix + Path.GetExtension(master));

        /// <summary>Unde se scrie raportul de structura: in folderul dat, nu in process/.</summary>
        internal string ResultTextPath => Path.Combine(Root, ResultFileName);

        /// <summary>Harta reziduala, langa raport si cu acelasi nume ca la consola.</summary>
        internal string ResidualPngPath => Path.Combine(Root, "flatcheck.png");

        /// <summary>Raportul si mozaicul tabului Split, in radacina sesiunii, langa cele ale structurii (ADR 0054).</summary>
        internal const string SplitResultFileName = "Rezultat split.txt";
        internal string SplitResultTextPath => Path.Combine(Root, SplitResultFileName);
        internal string SplitPngPath => Path.Combine(Root, "flatcheck-split.png");

        /// <summary>
        /// Prima verificare, inainte de orice calcul. Ordinea conteaza: un folder gresit
        /// ales, unul pe care nu s-a rulat inca niciun script si unul pe care scriptul a
        /// rulat dar n-a lasat masterul sunt trei greseli diferite, si merita trei mesaje.
        ///
        /// Singurul lucru cerut e MASTERUL, oriunde ar fi din cele doua locuri. process/
        /// nu e o conditie: un folder cu masters/ si fara process/ - cadrele sterse ca sa
        /// faca loc, masterul pastrat - ruleaza structura, iar tabul end-to-end spune
        /// singur ca n-are serie.
        /// </summary>
        internal SessionProblem Check()
        {
            if (!Directory.Exists(Root)) return SessionProblem.NoFolder;
            if (!Directory.Exists(ProcessDir) && !Directory.Exists(MastersDir)) return SessionProblem.NoProcessDir;
            if (FlatMasterPath == null) return SessionProblem.NoFlatMaster;
            return SessionProblem.None;
        }

        /// <summary>
        /// Dintre subfolderele in care intra un script (SirilScript.RequiredFolders), cele
        /// care lipsesc sau sunt goale - adica exact cele la care Siril s-ar opri cu
        /// "cd" esuat sau cu o conversie fara fisiere. Gol inseamna ca se poate porni.
        /// </summary>
        internal IReadOnlyList<string> MissingInputFolders(IEnumerable<string> required)
        {
            var missing = new List<string>();
            foreach (string name in required)
            {
                string dir = Path.Combine(Root, name);
                bool present = Directory.Exists(dir) && Directory.EnumerateFiles(dir).Any();
                if (!present) missing.Add(name);
            }
            return missing;
        }

        /// <summary>
        /// Cadrele calibrate pentru testul end-to-end. Acelasi tipar implicit ca la
        /// consola, deci si aceeasi excludere a cadrelor registrate (r_pp_light_*) - vezi
        /// SeriesFinder.DefaultPattern pentru de ce conteaza.
        /// </summary>
        internal IReadOnlyList<string> LightSeries()
            => Directory.Exists(ProcessDir)
                ? SeriesFinder.Find(ProcessDir, SeriesFinder.DefaultPattern)
                : Array.Empty<string>();

        /// <summary>
        /// Un fisier e acceptat de tabul Ratio doar daca e un master de flat. Verificarea
        /// e pe NUME, nu pe cale: in fluxul normal cele doua mastere vin din DOUA foldere
        /// de sesiune diferite - cate unul pentru fiecare orientare - deci calea nu spune
        /// nimic. Cerinta 6 spune sa explicam, nu sa refuzam tacut.
        ///
        /// Nu exista o regula separata pentru "pp_flat_stacked.fit": el E masterul de flat
        /// al unei sesiuni, deci exact ce cere modul. A existat o excludere a lui pana la
        /// rescrierea tabului, dar aia era o reparatie de PRESELECTIE - sortarea fiind
        /// ordinala, "pp_flat_stacked.fit" cadea dupa "pp_flat_A/B.fit" si ateriza pe
        /// preselectia setului B. Fara lista si fara preselectie, motivul a disparut.
        ///
        /// Ce se refuza acum sunt cadrele individuale calibrate (pp_flat_00001.fit) -
        /// vezi <see cref="RatioPattern"/>.
        /// </summary>
        internal static bool IsFlatMasterName(string path)
            => !string.IsNullOrEmpty(path)
               && SeriesFinder.GlobMatch(Path.GetFileName(path), RatioPattern);

        /// <summary>
        /// Flat-urile brute pentru --expo: doar raw de aparat.
        ///
        /// Modul are nevoie de mozaicul CFA intact si de scala ADU originala, iar un raw
        /// le duce pe amandoua, exact, in fisier. Extensiile vin din RawReader, nu dintr-o
        /// lista scrisa aici, ca sa nu apara un aparat suportat de cititor si invizibil
        /// in lista.
        /// </summary>
        internal IReadOnlyList<string> RawFlats()
        {
            if (!Directory.Exists(FlatsDir)) return Array.Empty<string>();

            return Directory.EnumerateFiles(FlatsDir)
                .Where(RawReader.IsRawPath)
                .OrderBy(Path.GetFileName, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// Cadrele din flats/ pe care le poate citi incarcatorul: raw de aparat SI FITS.
        /// Pentru egalizarea de histograma un flat trecut prin Conversion e la fel de bun
        /// ca NEF-ul; motivul pentru care tabul de expunere accepta doar raw (white si
        /// black exacte) nu se aplica aici. Ambele liste de extensii vin din cititoare.
        /// </summary>
        internal IReadOnlyList<string> FlatFrames()
        {
            if (!Directory.Exists(FlatsDir)) return Array.Empty<string>();

            return Directory.EnumerateFiles(FlatsDir)
                .Where(p => RawReader.IsRawPath(p) || FitsReader.IsFitsPath(p))
                .OrderBy(Path.GetFileName, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// Raw-ul de langa master din care se citesc diafragma si obiectivul - primul din
        /// flats/, sau null cand nu e niciunul. O singura regula pentru tabul Sintetic
        /// (preumplerea) si pentru tabul Structura (comparatia cu profilul), ca amandoua sa
        /// vorbeasca despre acelasi fisier; regula insasi e in nucleu.
        /// </summary>
        internal string ReferenceRawPath => LensIdentification.FirstRaw(FlatFrames());

        /// <summary>
        /// Ce listeaza tabul de histograma: masterul intai, DOAR daca exista; dupa el
        /// flat-ul netezit, DOAR daca exista - pe calea exacta pe care o scrie tabul
        /// Netezire, <see cref="SmoothedMasterPath"/>, nu dupa tipar; apoi cadrele din
        /// flats/. In fereastra masterul exista mereu - Check() refuza folderul fara el -
        /// dar clasa asta se testeaza singura si nu arunca niciodata.
        ///
        /// Numai masterul prin conventie - cel pe care il masoara si Structura, deci acelasi
        /// cand sunt doua - nu orice stiva din process/: doua stive intr-un folder sunt
        /// treaba tabului Ratio. Din acelasi motiv un fisier netezit de langa masterul mai
        /// VECHI, cand sunt doua, nu apare: e netezirea altui master decat cel de pe ecran.
        /// Si nimic nu se dedubleaza dupa continut - un master copiat in flats/ apare de
        /// doua ori, sub doua cai, ceea ce e adevarul.
        /// </summary>
        internal IReadOnlyList<HistEqCandidate> HistEqCandidates()
        {
            var list = new List<HistEqCandidate>();
            string master = FlatMasterPath;
            if (master != null)
            {
                list.Add(new HistEqCandidate(master, HistEqKind.Master));
                string smoothed = SmoothedPathBeside(master);
                if (File.Exists(smoothed)) list.Add(new HistEqCandidate(smoothed, HistEqKind.Smoothed));
            }
            list.AddRange(FlatFrames().Select(p => new HistEqCandidate(p, HistEqKind.Frame)));
            return list;
        }
    }
}
