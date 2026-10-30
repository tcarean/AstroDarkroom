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

namespace AstroPhotoAnalyzer.Wpf.Tests;

/// <summary>
/// R-164: the soft proof's full-screen window covers the screen, its panel hides until the
/// mouse is over its band, and its Close button leaves it. The hover itself is WPF's
/// MouseEnter / MouseLeave on the band; the tests drive the window's state, not the mouse.
/// </summary>
[Collection("fereastra")]
public class SoftProofWindowTests
{
    readonly StaFixture _sta;

    public SoftProofWindowTests(StaFixture sta) => _sta = sta;

    [Fact]
    public void R164_TheWindowIsBorderlessAndItsPanelHidesUntilShown()
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
                Assert.Equal(Visibility.Hidden, window.Panel.Visibility);
                Assert.True(window.PanelZone.ActualHeight > 0);
                Assert.Equal(window.Panel.ActualHeight, window.PanelZone.ActualHeight);
                Assert.Equal(VerticalAlignment.Top, window.PanelZone.VerticalAlignment);
                // The band is hit-testable while the panel is hidden: a background of its own.
                Assert.NotNull(window.PanelZone.Background);
                // The proof fills the whole window behind the band, not the part under it.
                Assert.Equal(content.ActualHeight, window.Proof.ActualHeight);

                window.PanelShown = true;
                Layout(content);
                Assert.Equal(Visibility.Visible, window.Panel.Visibility);
                window.PanelShown = false;
                Layout(content);
                Assert.Equal(Visibility.Hidden, window.Panel.Visibility);
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
                using (Tr.Use(UiLanguage.English))
                    Assert.Equal("Close", (string)new SoftProofWindow { DataContext = tab }.CloseButton.Content);
                bool closing = false;
                window.Closing += (_, _) => closing = true;
                window.CloseButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                Assert.True(closing);
            }
            finally
            {
                window.DataContext = null;
            }
        });

    static void Layout(FrameworkElement el)
    {
        el.Measure(new Size(1400, 860));
        el.Arrange(new Rect(0, 0, 1400, 860));
        el.UpdateLayout();
    }
}
