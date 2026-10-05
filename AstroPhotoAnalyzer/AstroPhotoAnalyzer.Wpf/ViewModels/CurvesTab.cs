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

// CurvesTab.cs - tabul Curbe (R-146) si fereastra curbei (R-147): galeria de previzualizari
// ale curbelor sugerate, dovada fiecareia, tabelul si graficele unei curbe, salvarea.
//
// Nu calculeaza nimic: curbele, previzualizarile, cifrele si propozitiile vin de la
// CurveRun; salvarea o face CurveExport. Aici sunt doar pozitia in galerie, comenzile si
// formatarea punctelor pentru tabel. Spre deosebire de celelalte taburi, continutul nu
// vine cu analiza: se cere cu "Calculeaza", fiindca costa si nu e diagnostic.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace AstroPhotoAnalyzer.Wpf
{
    /// <summary>O imagine a galeriei: originalul sau o curba sugerata.</summary>
    public sealed class CurveGalleryItem
    {
        public string Name { get; init; }
        public BitmapSource Image { get; init; }
        public int Width { get; init; }
        public int Height { get; init; }

        /// <summary>Dovada curbei, rand cu rand; goala pentru original.</summary>
        public IReadOnlyList<string> Evidence { get; init; } = Array.Empty<string>();

        internal CurveSuggestion Suggestion { get; init; }
    }

    /// <summary>Un rand al tabelului de puncte, cum se tasteaza in Siril.</summary>
    public sealed class CurvePointRow
    {
        public string Label { get; init; }
        public string Input { get; init; }
        public string Output { get; init; }
        public string LstarBefore { get; init; }
        public string LstarAfter { get; init; }
    }

    /// <summary>Unde sta o clasa pe axa intrarilor.</summary>
    public sealed class CurveMarker
    {
        public double X { get; init; }
        public string Label { get; init; }
    }

    public sealed class CurveWindowViewModel
    {
        public string Title { get; init; }
        public string Caption { get; init; }
        public IReadOnlyList<CurvePointRow> Rows { get; init; }

        /// <summary>Curba esantionata pe 0..1, si doar umbrele, pe 0..ShadowLimit.</summary>
        public IReadOnlyList<Point> Chart { get; init; }
        public IReadOnlyList<Point> ShadowChart { get; init; }
        public double ShadowLimit { get; init; }
        public string ShadowHeading { get; init; }

        /// <summary>Punctele de control si pozitiile celor cinci clase.</summary>
        public IReadOnlyList<Point> ControlPoints { get; init; }
        public IReadOnlyList<CurveMarker> Markers { get; init; }

        internal static CurveWindowViewModel From(CurveSuggestion s, string imageName)
        {
            ToneCurve c = s.Curve;
            CurveEvidence e = s.Evidence;
            var rows = new List<CurvePointRow>();
            int k = 0;
            foreach (CurvePoint p in c.Points)
            {
                bool labelled = p.Label != null;
                rows.Add(new CurvePointRow
                {
                    Label = p.Label ?? "",
                    Input = Fmt.F3(p.X),
                    Output = Fmt.F3(p.Y),
                    LstarBefore = labelled && e != null ? Fmt.F1(e.LstarBefore[k]) : "",
                    LstarAfter = labelled && e != null ? Fmt.F1(e.LstarAfter[k]) : "",
                });
                if (labelled) k++;
            }
            double shadow = Thresholds.CurveShadowChartFactor * c.Points[3].X;
            return new CurveWindowViewModel
            {
                Title = $"Curba {s.Name} - {imageName}",
                Caption = "Spline cubic prin puncte, aceeasi curba pe R, G si B: in Siril, Curves Transformation, modul cubic spline, cu punctele din tabel.",
                Rows = rows,
                Chart = c.Sample(1, Thresholds.CurveChartSamples).Select(p => new Point(p.X, p.Y)).ToArray(),
                ShadowChart = c.Sample(shadow, Thresholds.CurveChartSamples).Select(p => new Point(p.X, p.Y)).ToArray(),
                ShadowLimit = shadow,
                ShadowHeading = $"UMBRELE MARITE (0 - {Fmt.F3(shadow)}): unde stau praful si cerul",
                ControlPoints = c.Points.Select(p => new Point(p.X, p.Y)).ToArray(),
                Markers = c.Points.Where(p => p.Label != null).Select(p => new CurveMarker { X = p.X, Label = p.Label }).ToArray(),
            };
        }
    }

    public sealed class CurvesTabViewModel : ResultTabViewModel
    {
        AnalysisResult _result;
        CurveSet _set;
        IReadOnlyList<CurveGalleryItem> _items = Array.Empty<CurveGalleryItem>();
        int _index, _width = 1600, _height = 900;
        bool _isPrint = true, _busy;
        string _message, _anchorSource, _saveMessage, _saveError;

        public CurvesTabViewModel()
        {
            Calculate = new RelayCommand(() => _ = ComputeAsync(), () => _result != null && !IsBusy);
            Next = new RelayCommand(() => Move(1), () => _index < _items.Count - 1);
            Previous = new RelayCommand(() => Move(-1), () => _index > 0);
            OpenCurve = new RelayCommand(() => CurveRequested?.Invoke(CurveWindowViewModel.From(Current.Suggestion, Path.GetFileName(_result.Path))), HasCurve);
            SaveAs = new RelayCommand(() => SaveRequested?.Invoke(CurveTexts.ProposedFileName(_result.Path, Current.Name)), HasCurve);
        }

        public ICommand Calculate { get; }
        public ICommand Next { get; }
        public ICommand Previous { get; }
        public ICommand OpenCurve { get; }
        public ICommand SaveAs { get; }

        /// <summary>Fereastra deschide fereastra curbei; view-model-ul doar o cere.</summary>
        public event Action<CurveWindowViewModel> CurveRequested;

        /// <summary>Fereastra intreaba unde se salveaza, cu numele propus, apoi apeleaza Save.</summary>
        public event Action<string> SaveRequested;

        public bool IsPrint
        {
            get => _isPrint;
            set
            {
                if (!Set(ref _isPrint, value)) return;
                Raise(nameof(IsScreen));
                // Scopul schimba doar dovada: aceleasi curbe, aceeasi pozitie in galerie.
                if (HasGallery) Show(CurveRun.Execute(_result, Purpose, _width, _height), _index);
            }
        }

        public bool IsScreen { get => !_isPrint; set => IsPrint = !value; }

        CurvePurpose Purpose => _isPrint ? CurvePurpose.Print : CurvePurpose.Screen;

        public bool IsBusy
        {
            get => _busy;
            private set { if (Set(ref _busy, value)) (Calculate as RelayCommand)?.RaiseCanExecuteChanged(); }
        }

        public IReadOnlyList<CurveGalleryItem> Items { get => _items; private set => Set(ref _items, value); }
        public bool HasGallery => _items.Count > 0;
        public CurveGalleryItem Current => _items.Count == 0 ? null : _items[_index];
        public string Position => _items.Count == 0 ? "" : $"{_index + 1} / {_items.Count}";

        /// <summary>De ce nu s-a sugerat nimic, sau eroarea calculului; null cand galeria exista.</summary>
        public string Message { get => _message; private set => Set(ref _message, value); }

        /// <summary>De unde vin ancorele (R-141).</summary>
        public string AnchorSource { get => _anchorSource; private set => Set(ref _anchorSource, value); }

        public string SaveMessage { get => _saveMessage; private set => Set(ref _saveMessage, value); }
        public string SaveError { get => _saveError; private set => Set(ref _saveError, value); }

        /// <summary>Folderul imaginii, de unde porneste dialogul de salvare.</summary>
        public string InputFolder => _result == null || !File.Exists(_result.Path) ? null : Path.GetDirectoryName(Path.GetFullPath(_result.Path));

        /// <summary>Marimea zonei galeriei, in pixeli: o pune vederea, o foloseste "Calculeaza".</summary>
        internal void SetGallerySize(int width, int height)
        {
            if (width > 0 && height > 0) { _width = width; _height = height; }
        }

        bool HasCurve() => Current?.Suggestion != null;

        internal override void Populate(AnalysisResult r)
        {
            Reset();
            _result = r;
            HasResult = true;
            (Calculate as RelayCommand)?.RaiseCanExecuteChanged();
        }

        internal override void Clear()
        {
            base.Clear();
            Reset();
            _result = null;
            if (Set(ref _isPrint, true, nameof(IsPrint))) Raise(nameof(IsScreen));
            (Calculate as RelayCommand)?.RaiseCanExecuteChanged();
        }

        void Reset()
        {
            _set = null;
            _index = 0;
            Items = Array.Empty<CurveGalleryItem>();
            Message = null; AnchorSource = null; SaveMessage = null; SaveError = null;
            RaiseGallery();
        }

        /// <summary>Ce ruleaza "Calculeaza", sincron: curbele pentru scopul ales, la marimea data a galeriei.</summary>
        internal void Compute(int width, int height)
        {
            SetGallerySize(width, height);
            Show(CurveRun.Execute(_result, Purpose, _width, _height), 0);
        }

        async Task ComputeAsync()
        {
            AnalysisResult result = _result;
            CurvePurpose purpose = Purpose;
            int w = _width, h = _height;
            IsBusy = true;
            Message = "se calculeaza curbele...";
            try
            {
                CurveSet set = await Task.Run(() => CurveRun.Execute(result, purpose, w, h));
                if (ReferenceEquals(result, _result)) Show(set, 0);
            }
            catch (Exception e)
            {
                Reset();
                Message = MainViewModel.Describe(e);
            }
            finally
            {
                IsBusy = false;
            }
        }

        void Show(CurveSet set, int index)
        {
            _set = set;
            SaveMessage = null; SaveError = null;
            AnchorSource = set.SourceSentence;
            if (set.Problem != null)
            {
                _index = 0;
                Items = Array.Empty<CurveGalleryItem>();
                Message = set.Problem;
                RaiseGallery();
                return;
            }
            var items = new List<CurveGalleryItem> { Item(CurveTexts.Original, set.OriginalPreview, null) };
            foreach (CurveSuggestion s in set.Suggestions) items.Add(Item(s.Name, s.Preview, s));
            _index = Math.Clamp(index, 0, items.Count - 1);
            Message = null;
            Items = items;
            RaiseGallery();
        }

        static CurveGalleryItem Item(string name, Raster image, CurveSuggestion s) => new()
        {
            Name = name,
            Image = RasterBitmaps.Region(image, 0, 0, image.W, image.H),
            Width = image.W,
            Height = image.H,
            Evidence = s == null ? Array.Empty<string>() : CurveTexts.EvidenceLines(s),
            Suggestion = s,
        };

        void Move(int step)
        {
            _index = Math.Clamp(_index + step, 0, Math.Max(0, _items.Count - 1));
            RaiseGallery();
        }

        void RaiseGallery()
        {
            Raise(nameof(HasGallery)); Raise(nameof(Current)); Raise(nameof(Position));
            foreach (ICommand c in new[] { Next, Previous, OpenCurve, SaveAs }) (c as RelayCommand)?.RaiseCanExecuteChanged();
        }

        /// <summary>Salveaza curba aratata intr-un FITS nou (R-145); eroarea se spune, nu se arunca.</summary>
        internal void Save(string path)
        {
            try
            {
                CurveExport.Save(_result, Current.Suggestion, path);
                SaveError = null;
                SaveMessage = CurveTexts.Saved(path);
            }
            catch (Exception e)
            {
                SaveMessage = null;
                SaveError = MainViewModel.Describe(e);
            }
        }
    }
}
