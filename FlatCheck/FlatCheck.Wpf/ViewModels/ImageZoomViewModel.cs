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

// ImageZoomViewModel.cs - starea de zoom a unei poze: potrivit in fereastra, sau un factor.
//
// Acelasi tipar ca MapScaleViewModel: nu decide nimic, tine o alegere. Ce e al lui e
// STAREA - potrivit sau manual, si factorul - ca un test s-o poata conduce si tabul s-o
// poata reseta la fisier nou. Deplasarea cu mouse-ul, rotita si calculul lui "potrivit"
// din marimea ferestrei sunt ale controlului ZoomableImage: sunt interactiune, nu
// decizie, si au nevoie de dimensiuni pe care numai controlul le stie.
//
// EffectiveZoom e scris de control (legare TwoWay): pe "potrivit" e factorul pe care l-a
// calculat el, altfel e chiar Zoom. Sta aici ca eticheta sa poata spune "potrivit (13 %)",
// si ca o marire pornita din "potrivit" sa fie un pas, nu un salt la 100 %.

using System;
using System.Windows.Input;

namespace FlatCheck.Wpf
{
    public sealed class ImageZoomViewModel : ObservableObject
    {
        internal const double Min = 0.1, Max = 8.0, Step = 1.25;

        bool _fit = true;
        double _zoom = 1.0;
        double _effective = 1.0;

        public ImageZoomViewModel()
        {
            ZoomIn = new RelayCommand(() => Manual(EffectiveZoom * Step));
            ZoomOut = new RelayCommand(() => Manual(EffectiveZoom / Step));
            Fit = new RelayCommand(() => IsFit = true);
            Actual = new RelayCommand(() => Manual(1.0));
        }

        /// <summary>true: controlul calculeaza factorul ca poza sa incapa intreaga.</summary>
        public bool IsFit
        {
            get => _fit;
            set { if (Set(ref _fit, value)) Raise(nameof(Label)); }
        }

        /// <summary>Factorul manual, 1 = 100 %; conteaza cand IsFit e false. Taiat la [Min, Max].</summary>
        public double Zoom
        {
            get => _zoom;
            set
            {
                double v = Math.Clamp(value, Min, Max);
                if (Set(ref _zoom, v)) Raise(nameof(Label));
            }
        }

        /// <summary>Ce e chiar pe ecran. Il scrie controlul; pe manual e egal cu Zoom.</summary>
        public double EffectiveZoom
        {
            get => _effective;
            set { if (Set(ref _effective, value)) Raise(nameof(Label)); }
        }

        /// <summary>"potrivit (13 %)" sau "125 %".</summary>
        public string Label => IsFit ? $"potrivit ({EffectiveZoom * 100:0} %)" : $"{Zoom * 100:0} %";

        public ICommand ZoomIn { get; }
        public ICommand ZoomOut { get; }
        public ICommand Fit { get; }
        public ICommand Actual { get; }

        /// <summary>
        /// Trece pe manual la factorul dat. Intai factorul, apoi iesirea din "potrivit":
        /// asa controlul aplica o singura data valoarea noua, nu intai pe cea veche.
        /// </summary>
        internal void Manual(double zoom)
        {
            Zoom = zoom;
            IsFit = false;
        }

        /// <summary>Inapoi la potrivit; tabul o cheama la fisier nou, NU la schimbarea vederii.</summary>
        internal void Reset()
        {
            Zoom = 1.0;
            IsFit = true;
        }
    }
}
