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

using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace AstroPhotoAnalyzer.Wpf.Tests;

/// <summary>
/// R-164: the soft proof's full-screen window covers the screen, its panel hides until the
/// mouse is over its band, and its Close button leaves it. The band's MouseEnter and
/// MouseLeave are raised here by hand, on a window never shown: whether the three window
/// attributes cover the taskbar is Windows' doing and is checked on a live run, not here.
/// </summary>
[Collection("fereastra")]
public class SoftProofWindowTests
{
    readonly StaFixture _sta;

    public SoftProofWindowTests(StaFixture sta) => _sta = sta;

    static void RaiseMouse(UIElement on, RoutedEvent what)
        => on.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = what });

    [Fact]
    public void R164_TheWindowIsBorderlessAndItsPanelHidesUntilTheMouseIsOverItsBand()
        => _sta.Invoke(() =>
        {
            var tab = new SoftProofTabViewModel();
            var window = new SoftProofWindow { DataContext = tab };
            try
            {
                // A true full screen: no title bar, no buttons, no border, over the taskbar.
                Assert.Equal(WindowStyle.None, window.WindowStyle);
                Assert.Equal(WindowState.Maximized, window.WindowState);
                Assert.Equal(ResizeMode.NoResize, window.ResizeMode);

                var content = (FrameworkElement)window.Content;
                Layout(content);
                Assert.StartsWith("Soft proof", window.Title);   // the taskbar still names it
                // The panel starts hidden; its band keeps the panel's height, so there is
                // something at the top to point at.
                Assert.False(window.PanelShown);
                Assert.Equal(Visibility.Hidden, window.HoverPanel.Visibility);
                Assert.True(window.PanelZone.ActualHeight > 0);
                Assert.Equal(window.HoverPanel.ActualHeight, window.PanelZone.ActualHeight);
                Assert.Equal(VerticalAlignment.Top, window.PanelZone.VerticalAlignment);
                // The band is hit-testable while the panel is hidden: a background of its own.
                Assert.NotNull(window.PanelZone.Background);
                // The proof fills the whole window behind the band, not the part under it.
                Assert.Equal(content.ActualHeight, window.Proof.ActualHeight);
                // The controls (without "Ecran intreg") and the Close button are in the panel,
                // so they hide with it.
                Assert.True(window.HoverPanel.IsAncestorOf(window.ProofControls));
                Assert.False(window.ProofControls.ShowFullScreenButton);
                Assert.True(window.HoverPanel.IsAncestorOf(window.CloseButton));

                // The mouse enters the band: the panel shows; it leaves: the panel hides.
                RaiseMouse(window.PanelZone, UIElement.MouseEnterEvent);
                Layout(content);
                Assert.True(window.PanelShown);
                Assert.Equal(Visibility.Visible, window.HoverPanel.Visibility);
                RaiseMouse(window.PanelZone, UIElement.MouseLeaveEvent);
                Layout(content);
                Assert.False(window.PanelShown);
                Assert.Equal(Visibility.Hidden, window.HoverPanel.Visibility);
                // The hold while the margin list is open is not reachable here: a ComboBox that
                // is not loaded coerces IsDropDownOpen back to false, and the tests show no
                // window. Nor is the focus rule of HidePanel: Keyboard.Focus needs a shown
                // window. Both paths are read, and checked on a live run.
            }
            finally
            {
                window.DataContext = null;
            }
        });

    [Fact]
    public void R164_TheCloseButtonInThePanelClosesTheWindow()
        => _sta.Invoke(() =>
        {
            var tab = new SoftProofTabViewModel();
            var window = new SoftProofWindow { DataContext = tab };
            try
            {
                Layout((FrameworkElement)window.Content);
                Assert.Equal("Inchide", (string)window.CloseButton.Content);   // the tests run in Romanian
                bool closing = false;
                window.Closing += (_, _) => closing = true;
                window.CloseButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                Assert.True(closing);
            }
            finally
            {
                window.DataContext = null;
            }
            using (Tr.Use(UiLanguage.English))
            {
                var english = new SoftProofWindow { DataContext = tab };
                try
                {
                    Assert.Equal("Close", (string)english.CloseButton.Content);
                }
                finally
                {
                    english.DataContext = null;
                }
            }
        });

    static void Layout(FrameworkElement el)
    {
        el.Measure(new Size(1400, 860));
        el.Arrange(new Rect(0, 0, 1400, 860));
        el.UpdateLayout();
    }
}
