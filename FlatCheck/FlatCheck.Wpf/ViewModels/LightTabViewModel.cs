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

// LightTabViewModel.cs - tabul 3, testul end-to-end pe toata seria din process/.
//
// Trei stari de sfarsit, si toate trei sunt raspunsuri corecte:
//
//   1. verdict pe serie - media chiar estimeaza eroarea de flat;
//   2. NEDETERMINAT - a rulat, dar seria nu e concludenta, si se spune de ce;
//   3. INAPLICABIL - campul e prea larg, cerul si vignetarea au aceeasi forma, deci nu
//      se produce nicio cifra.
//
// A treia NU e o eroare a aplicatiei si nu se afiseaza ca atare. E rezultatul gardei din
// 1.2.0, adaugata dupa ce unealta raportase increzator "+3.13 % SUB-corectie" pe o
// sesiune la 20 mm unde 88 % din ce masura era cer. Un tab care ar arata asta ca pe un
// esec l-ar invata pe om ca aplicatia e stricata, cand de fapt tocmai l-a scutit de o
// concluzie falsa.
//
// Tabelul se umple pe masura ce cadrele sunt gata, prin callback-ul lui LightSeriesRun.
// Pe 32 de cadre rularea ia vreo doua minute; un ecran gol tot timpul ala ar parea blocat.

using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace FlatCheck.Wpf
{
    public sealed class LightTabViewModel : TabViewModel
    {
        string _source = "";
        string _field = "";
        string _fieldWarning = "";
        string _meanRadial = "";
        string _spread = "";
        string _sem = "";
        string _correlation = "";
        string _verdict = "";
        string _diagnosis = "";
        string _reasons = "";
        bool _refused;
        byte[] _map;
        string _mapLegend = "";

        /// <summary>
        /// Firul pe care se scrie in Frames, captat O SINGURA DATA la pornirea rularii.
        ///
        /// Nu se intreaba Application.Current la fiecare cadru, si asta nu e o optimizare:
        /// daca raspunsul s-ar schimba la mijlocul seriei, o parte din randuri ar intra pe
        /// un fir si restul pe altul, iar ObservableCollection nu suporta asa ceva - se
        /// pierd randuri, tacut. S-a si intamplat, intr-o rulare de teste unde aplicatia
        /// WPF aparea intre timp: sase cadre prelucrate, cinci randuri in tabel.
        /// </summary>
        Dispatcher _ui;

        public string Source { get => _source; private set => Set(ref _source, value); }

        /// <summary>Campul acoperit si de unde s-a aflat - se arata si cand se refuza.</summary>
        public string Field { get => _field; private set => Set(ref _field, value); }

        /// <summary>Avertismentul de camp la limita, sau nota ca nu s-a putut verifica.</summary>
        public string FieldWarning { get => _fieldWarning; private set => Set(ref _fieldWarning, value); }

        public string MeanRadial { get => _meanRadial; private set => Set(ref _meanRadial, value); }
        public string Spread { get => _spread; private set => Set(ref _spread, value); }
        public string Sem { get => _sem; private set => Set(ref _sem, value); }
        public string Correlation { get => _correlation; private set => Set(ref _correlation, value); }

        public string Verdict { get => _verdict; private set => Set(ref _verdict, value); }
        public string Diagnosis { get => _diagnosis; private set => Set(ref _diagnosis, value); }

        /// <summary>De ce nu e concludenta seria. Gol cand e.</summary>
        public string Reasons { get => _reasons; private set => Set(ref _reasons, value); }

        /// <summary>true cand garda de camp a oprit testul. Nu e o eroare.</summary>
        public bool Refused
        {
            get => _refused;
            private set { if (Set(ref _refused, value)) Raise(nameof(HasNumbers)); }
        }

        /// <summary>Se arata panoul de cifre doar cand chiar exista cifre.</summary>
        public bool HasNumbers => !_refused;

        public byte[] Map { get => _map; private set => Set(ref _map, value); }

        /// <summary>
        /// Ce inseamna capetele culorilor. Doar legenda, fara comutator de scara: harta
        /// de aici e termenul radial fitat, iar pragurile lui sunt Th.LightRad*, nu scara
        /// de reziduu - deci plafoanele de +/-2/10/20 % n-ar insemna nimic pe ea.
        /// </summary>
        public string MapLegend { get => _mapLegend; private set => Set(ref _mapLegend, value); }

        public ObservableCollection<SeriesRow> Frames { get; } = new();

        internal Task RunAsync(SirilSession session, CancellationToken ct)
            => RunGuardedAsync("Rulez testul end-to-end pe seria de lights...", async () =>
            {
                // Lista se cere INAINTE de rulare, ca "niciun cadru" sa fie o stare a
                // interfetei cu explicatie, nu o exceptie prinsa la mijlocul unui calcul.
                var files = session.LightSeries();
                if (files.Count == 0)
                {
                    Fail("Nu am pe ce rula testul end-to-end", SessionMessages.NoLightSeries(session));
                    return;
                }

                Source = $"{session.ProcessDir}   ({files.Count} cadre, tipar {SeriesFinder.DefaultPattern})";

                _ui = UiDispatcher();
                OnUi(Frames.Clear);
                ProgressKnown = true;

                var text = new StringWriter();
                var outcome = await Task.Run(() => LightSeriesRun.Execute(
                    session.ProcessDir, SeriesFinder.DefaultPattern, null, text,
                    onFrame: OnFrame, ct: ct), ct).ConfigureAwait(true);

                Report = text.ToString();
                Populate(outcome);
                HasResult = true;
            });

        /// <summary>
        /// Chemat de pe firul de calcul. Adaugarea intr-o ObservableCollection trebuie sa
        /// se intample pe firul de interfata, altfel WPF arunca la prima legare - deci se
        /// trece prin dispecer.
        ///
        /// Doua situatii ocolesc dispecerul, si amandoua sunt reale:
        ///   - nu exista aplicatie WPF (teste): nu deseneaza nimeni, deci se adauga direct;
        ///   - dispecerul se inchide (fereastra a fost inchisa in mijlocul unei serii de
        ///     32 de cadre): un Invoke acolo ar arunca, iar randul oricum n-are cine sa-l
        ///     mai vada. Anularea opreste bucla la cadrul urmator.
        /// </summary>
        void OnFrame(int index, int total, string name, LightResult r)
        {
            var row = new SeriesRow
            {
                Name = name,
                Radial = LightReport.Signed(r.RadPct) + " %",
                Sky = $"{r.PlanePct:F2} %",
                Tiles = $"{r.KeptTiles}/{r.TotalTiles}",
                Verdict = r.Verdict,
            };

            void Apply()
            {
                Frames.Add(row);
                Progress = (index + 1) / (double)total;
                BusyDetail = $"cadrul {index + 1} din {total}: {name}";
            }

            OnUi(Apply);
        }

        /// <summary>
        /// Ruleaza pe firul care detine Frames pe durata rularii curente.
        ///
        /// Fara aplicatie WPF - in teste - nu exista fir de interfata, deci se executa
        /// direct: nu deseneaza nimeni si nu e nimic de sincronizat.
        ///
        /// Cu dispecerul inchis in timpul rularii - fereastra inchisa in mijlocul unei
        /// serii de 32 de cadre - actualizarea se ARUNCA, nu se executa direct. Pare
        /// tentant sa cazi inapoi pe firul curent, dar aia ar rupe exact invariantul care
        /// tine colectia intreaga: o parte din randuri ar ajunge pe firul de interfata si
        /// restul pe firul de calcul, iar ObservableCollection pierde randuri tacut cand i
        /// se intampla asta. Randul oricum n-are cine sa-l mai vada - fereastra se inchide
        /// - iar anularea opreste bucla la cadrul urmator.
        /// </summary>
        void OnUi(Action work)
        {
            Dispatcher d = _ui;
            if (d == null) { work(); return; }

            // Inchiderea poate cadea oricand dupa captura. Actualizarea se ARUNCA atunci,
            // nu se executa pe firul curent: aia ar rupe invariantul de mai sus la
            // jumatatea rularii, si ObservableCollection pierde randuri tacut cand i se
            // intampla asta. Randul n-are cine sa-l mai vada oricum - fereastra se inchide.
            if (d.HasShutdownStarted || d.HasShutdownFinished) return;

            try { d.Invoke(work); }
            catch (System.ComponentModel.Win32Exception) { }
            catch (TaskCanceledException) { }
        }

        /// <summary>
        /// Firul de interfata, daca exista unul UTILIZABIL acum.
        ///
        /// Nu e destul sa existe un Application: obiectul ala supravietuieste propriului
        /// dispecer, deci dupa o inchidere Application.Current e in continuare nenul iar
        /// dispecerul lui nu mai primeste nimic. Captat asa, campul spune un singur lucru
        /// pentru toata rularea - "exista un fir de interfata" sau "nu exista" - si nu
        /// se schimba la mijloc.
        /// </summary>
        static Dispatcher UiDispatcher()
        {
            Dispatcher d = System.Windows.Application.Current?.Dispatcher;
            return d != null && !d.HasShutdownStarted && !d.HasShutdownFinished ? d : null;
        }

        void Populate(LightSeriesRunOutcome outcome)
        {
            Field = DescribeField(outcome.Suitability);
            FieldWarning = FieldWarningFor(outcome.Suitability);

            if (outcome.Refused)
            {
                Refused = true;
                Verdict = Stats.NotApplicable;
                Diagnosis = "";
                Reasons =
                    "Peste " + $"{Th.LightFovMax:F0}" + " de grade diagonala, cupola de poluare luminoasa se "
                  + "CURBEAZA in interiorul cadrului. Partea radial simetrica a acelei curburi are "
                  + "exact forma vignetarii, deci fitul nu le mai poate separa: orice cifra ar iesi "
                  + "ar fi cerul, nu flat-ul.\n\n"
                  + "Nu e o limitare a uneltei, e o degenerare geometrica.\n\n"
                  + "Pe un setup cu camp larg, testul care raspunde e cel din tabul Ratio: "
                  + "rotatia la 180 de grade valideaza flat-ul fara sa implice cerul deloc.";
                MeanRadial = Spread = Sem = Correlation = "";
                Map = null;
                MapLegend = "";
                return;
            }

            var r = outcome.Result;
            Refused = false;

            MeanRadial = LightReport.Signed(r.MeanRad) + " %";
            Spread = $"{LightReport.Signed(r.MinRad)} .. {LightReport.Signed(r.MaxRad)} %";
            Sem = $"{r.Sem:F2} puncte";
            Correlation = r.CorrApplies
                ? $"{r.SkyCorr:F2}"
                : $"nu se aplica (gradientul variaza doar {r.SkySpan:F1} puncte)";

            Verdict = r.Verdict;
            Diagnosis = r.Conclusive ? LightReport.Diagnosis(r.MeanRad) : "";
            Reasons = r.Conclusive ? "" : DescribeReasons(r);
            Map = outcome.Png;
            // Aceleasi capete cu care a fost encodata harta primita, deci scara automata.
            MapLegend = RunReport.MapLegend(PngWriter.AutoRange(r.RadialMap));
        }

        /// <summary>
        /// De ce nu se poate citi media ca eroare de flat. Aceeasi lista de motive ca in
        /// raportul de consola, doar ca fara indentare de coloane.
        /// </summary>
        static string DescribeReasons(LightSeriesResult r)
        {
            var parts = new System.Collections.Generic.List<string>();

            foreach (string reason in r.Reasons)
            {
                if (reason == "sem")
                    parts.Add($"Eroarea mediei ({r.Sem:F2}) depaseste chiar pragul de OK "
                            + $"({Th.LightRadOk:F2}), deci seria nu rezolva flat-ul la precizia la "
                            + "care se pronunta pragurile. Un verdict aici ar fi o parere, nu o "
                            + "masuratoare.");
                else if (reason == "corelatie")
                    parts.Add($"Componenta radiala urmareste gradientul de cer (r = {r.SkyCorr:F2} "
                            + $"pe un interval de {r.SkySpan:F0} puncte), semn ca ce se masoara e cerul.");
                else
                    parts.Add(char.ToUpperInvariant(reason[0]) + reason.Substring(1) + ".");
            }

            parts.Add("Flat-ul e acelasi pe toate cadrele prin constructie - s-a impartit din "
                    + "fiecare. Deci tot ce se misca in timpul seriei e cer, nu flat. Pe date ca "
                    + "astea raspunsul il da tabul Ratio, care valideaza flat-ul fara sa implice "
                    + "cerul deloc.");

            return string.Join("\n\n", parts);
        }

        static string DescribeField(SuitabilityResult s)
        {
            if (s == null) return "";
            if (!double.IsNaN(s.FovDeg)) return $"{s.FovDeg:F1} grade diagonala  ({s.Source})";
            if (!double.IsNaN(s.FocalMm)) return $"nedeterminat  ({s.Source})";
            return "necunoscut - headerul nu da nici focala, nici scara";
        }

        static string FieldWarningFor(SuitabilityResult s)
        {
            if (s == null) return "";

            if (s.Level == FovLevel.Marginal)
                return $"Camp la limita. Separarea plan/radial slabeste peste {Th.LightFovOk:F0} grade "
                     + "diagonala, iar cifra de mai jos poate contine si cer, nu doar flat. "
                     + "Citeste-o ca pe un indiciu, nu ca pe o masuratoare.";

            if (s.Level == FovLevel.Unknown)
                return "Nu pot verifica daca testul se aplica: headerul nu spune destul despre camp. "
                     + "Rezultatul ruleaza oricum, dar necunoscut inseamna neverificat, nu verificat.";

            return "";
        }

        internal override void Reset()
        {
            base.Reset();
            Source = "";
            Field = "";
            FieldWarning = "";
            MeanRadial = Spread = Sem = Correlation = "";
            Verdict = "";
            Diagnosis = "";
            Reasons = "";
            Refused = false;
            Map = null;
            MapLegend = "";
            OnUi(Frames.Clear);
            _ui = null;
        }
    }
}
