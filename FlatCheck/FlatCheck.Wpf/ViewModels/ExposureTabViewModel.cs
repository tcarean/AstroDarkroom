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

// ExposureTabViewModel.cs - tabul 4, verificarea expunerii unui flat brut.
//
// Lista vine din flats/ si contine DOAR raw-uri de aparat: modul are nevoie de mozaicul
// CFA intact si de scala ADU originala, iar pe un raw white si black se citesc exact din
// fisier in loc sa se ghiceasca din maxim.
//
// Se recalculeaza si la schimbarea fisierului, si la comutarea lui "Sky", fiindca cele
// doua benzi sunt diferite: fara --sky un sky flat la 22 % ar fi raportat gresit ca
// subexpus. Un sky flat TREBUIE sa stea modest - cerul se stinge continuu si e plin de
// stele slabe, deci SNR-ul masterului vine din numarul de cadre, nu din a impinge un
// cadru la 55 %.

using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace FlatCheck.Wpf
{
    public sealed class ExposureTabViewModel : TabViewModel
    {
        SirilSession _session;
        CancellationToken _ct;

        string _selected;
        bool _sky;
        string _scale = "";
        string _centre = "";
        string _global = "";
        string _clipping = "";
        string _channels = "";
        string _verdict = "";
        string _advice = "";
        bool _isRaw = true;

        /// <summary>Raw-urile din flats/, cu calea completa; se afiseaza doar numele.</summary>
        public ObservableCollection<string> Files { get; } = new();

        public string Selected
        {
            get => _selected;
            set { if (Set(ref _selected, value)) Recompute(); }
        }

        /// <summary>Cerinta 8: comutatorul schimba pragurile, deci re-calculeaza.</summary>
        public bool Sky
        {
            get => _sky;
            set { if (Set(ref _sky, value)) Recompute(); }
        }

        public string Scale { get => _scale; private set => Set(ref _scale, value); }
        public string Centre { get => _centre; private set => Set(ref _centre, value); }
        public string Global { get => _global; private set => Set(ref _global, value); }
        public string Clipping { get => _clipping; private set => Set(ref _clipping, value); }
        public string Channels { get => _channels; private set => Set(ref _channels, value); }
        public string Verdict { get => _verdict; private set => Set(ref _verdict, value); }
        public string Advice { get => _advice; private set => Set(ref _advice, value); }

        /// <summary>
        /// false cand gate-ul de sanitate a taiat verdictul: fisierul e deja calibrat sau
        /// debayerizat. Nu e un verdict prost, e alt fisier decat cere modul.
        /// </summary>
        public bool IsRawFlat { get => _isRaw; private set => Set(ref _isRaw, value); }

        internal void Attach(SirilSession session, CancellationToken ct)
        {
            _session = session;
            _ct = ct;

            Files.Clear();
            foreach (string p in session.RawFlats()) Files.Add(p);

            _selected = null;
            Raise(nameof(Selected));

            if (Files.Count == 0)
                Fail("Niciun flat brut de verificat", SessionMessages.NoRawFlats(session));
            else
                ClearError();
        }

        void Recompute()
        {
            if (_session == null || string.IsNullOrEmpty(Selected)) return;
            _ = RecomputeAsync();
        }

        internal Task RecomputeAsync()
        {
            string path = Selected;
            bool sky = Sky;
            if (string.IsNullOrEmpty(path)) return Task.CompletedTask;

            return RunGuardedAsync($"Masor expunerea pe {Path.GetFileName(path)}...", async () =>
            {
                var text = new StringWriter();
                var outcome = await Task.Run(() =>
                {
                    RunReport.WriteBanner(text);
                    return ExposureRun.Execute(path, null, null, sky, text);
                }, _ct).ConfigureAwait(true);

                Report = text.ToString();
                Populate(outcome.Result);
                HasResult = true;
            });
        }

        void Populate(ExposureResult r)
        {
            Scale = $"white {r.White:F0}   black {r.Black:F0}   ({r.ScaleSource})";
            Centre = $"{r.CentreMedianPct:F1} %";
            Global = $"{r.GlobalMedianPct:F1} %";
            Clipping = $"{r.CentreClipPct:F3} %";
            Channels = DescribeChannels(r);
            IsRawFlat = r.IsRaw;
            Verdict = VerdictText(r);
            Advice = AdviceText(r);
        }

        static readonly string[] ChannelNames = { "CFA[0,0]", "CFA[0,1]", "CFA[1,0]", "CFA[1,1]" };

        static string DescribeChannels(ExposureResult r)
        {
            if (!r.IsCfa) return "imagine mono/procesata (fara CFA)";

            var parts = new string[4];
            for (int i = 0; i < 4; i++)
            {
                double pct = (r.ChannelMedians[i] - r.Black) / r.Range * 100;
                parts[i] = $"{ChannelNames[i]} {pct:F1} %"
                         + (i == r.BrightestChannel ? "  (cel mai luminos)" : "");
            }
            return string.Join("\n", parts);
        }

        /// <summary>
        /// Verdictul in cuvinte. Sirurile sunt cele pe care le coloreaza VerdictPalette
        /// prin Stats.Severity, deci raman in acelasi vocabular ca restul aplicatiei.
        /// </summary>
        static string VerdictText(ExposureResult r) => r.Verdict switch
        {
            ExposureVerdict.NotRaw => Stats.NotApplicable,
            ExposureVerdict.Ok => "OK",
            ExposureVerdict.AcceptableLow => "ACCEPTABIL",
            ExposureVerdict.AcceptableHigh => "ACCEPTABIL",
            _ => "PROBLEMA",
        };

        static string AdviceText(ExposureResult r)
        {
            if (!r.IsRaw) return NotRawAdvice(r);

            double cen = r.CentreMedianPct;
            bool clipping = r.CentreClipPct > Th.ExpoClip;
            double target = r.IsSky ? Th.ExpoSkyTarget : Th.ExpoTarget;
            double stops = cen > 0 ? Math.Log2(target / cen) : double.NaN;

            if (r.IsSky)
                return r.Verdict switch
                {
                    ExposureVerdict.TooHigh =>
                        "PREA SUS" + (clipping ? " - CLIPPING in centru!" : " - risc de stele in cadru.")
                        + "\nScade expunerea.",
                    ExposureVerdict.TooLow =>
                        $"PREA JOS pentru sky flat.\nCreste cu ~{stops:F1} stopuri, dar repede - cerul se stinge.",
                    ExposureVerdict.Ok =>
                        $"OK pentru sky flat (centru {Th.ExpoSkyLo:F0}-{Th.ExpoSkyHi:F0} %).\n"
                      + "La sky flat nivelul modest e corect. SNR-ul vine din numarul de cadre, nu "
                      + "din a urca expunerea - ai risca stele. NU forta mai sus.",
                    _ =>
                        "Cam sus, dar acceptabil.\nSub 60 % e ok; peste, risti stele. Nu urca mai mult.",
                };

            return r.Verdict switch
            {
                ExposureVerdict.TooHigh =>
                    "PREA SUS" + (clipping ? " - CLIPPING in centru!" : ".")
                    + $"\nScade expunerea cu ~{-stops:F1} stopuri."
                    + (clipping
                        ? "\nCentrul clipeaza, deci pierzi informatia de vignetare exact unde semnalul e maxim."
                        : ""),
                ExposureVerdict.TooLow =>
                    $"PREA JOS.\nCreste expunerea cu ~{stops:F1} stopuri.\n"
                  + "Sub ~30 % in centru masterul iese zgomotos (zgomot fixed-pattern).",
                ExposureVerdict.AcceptableLow =>
                    $"Acceptabil, dar cam jos.\nAi putea creste cu ~{stops:F1} stopuri.",
                ExposureVerdict.Ok =>
                    $"OK - centrul e in tinta {Th.ExpoLo:F0}-{Th.ExpoHi:F0} %.",
                _ =>
                    $"Acceptabil, dar cam sus.\nAi putea scadea cu ~{-stops:F1} stopuri.",
            };
        }

        /// <summary>
        /// Gate-ul de sanitate. Cauza tipica e "am deschis NEF-ul in Siril si am dat
        /// Save", care debayerizeaza - deci se spune exact asta, nu doar ca fisierul nu e
        /// bun.
        /// </summary>
        static string NotRawAdvice(ExposureResult r)
        {
            string why = "";
            if (r.LevelTooLow)
                why += $"Nivelul e ~{r.CentreMedianPct:F1} % (langa zero) - flat-dark deja scazut, sau "
                     + "imagine normalizata. Modul are nevoie de nivelul ADU original.\n\n";
            if (r.LooksDebayered)
                why += $"Cele 4 canale CFA sunt quasi-identice (spread {r.CfaSpread:F3}) - fisierul e "
                     + "DEBAYERIZAT. Modul are nevoie de mozaicul Bayer intact.\n\n";

            return "FISIERUL NU PARE UN FLAT BRUT\n\n" + why
                 + "Cauza tipica: ai deschis NEF-ul in Siril si ai dat Save (asta debayerizeaza).\n\n"
                 + "Solutii: ruleaza direct pe NEF - aplicatia citeste brutul exact - sau produ un "
                 + "FITS brut cu tab-ul Conversion din Siril (NU Open + Save), care pastreaza "
                 + "mozaicul CFA nedebayerizat.";
        }

        internal override void Reset()
        {
            base.Reset();
            _session = null;
            Files.Clear();
            _selected = null;
            Raise(nameof(Selected));
            Scale = Centre = Global = Clipping = Channels = "";
            Verdict = "";
            Advice = "";
            IsRawFlat = true;
        }
    }
}
