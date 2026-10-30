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
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ImageVisualCompare.Wpf;

namespace ImageVisualCompare.Tests;

/// <summary>
/// The About window (R-072, R-075, ADR 0045) shows what <see cref="VersionInfo"/>, the interface
/// texts and <see cref="ThirdParty"/> hold, and no text of its own: its view model is read against
/// them, in English as the interface starts. The window itself is built on the WPF test thread and
/// laid out at its client width, never shown, so that its markup and the card's row template are
/// both loaded: a resource, a style key or a binding they cannot find fails here, not on the
/// user's screen (ADR 0032). The Romanian is <c>UiTextTests</c>'s.
/// </summary>
public class AboutWindowTests
{
    [Fact]
    public void R072_TheHeaderIsTheNameTheTaglineTheBannerAndTheCopyright()
    {
        var about = new AboutViewModel();

        Assert.Equal("About Image Visual Compare", about.Title);
        Assert.Equal("Image Visual Compare", about.Name);
        Assert.Equal("Before/after comparison of two versions of the same astrophotography image.", about.Tagline);
        Assert.Equal(VersionInfo.Banner, about.Banner);
        Assert.Equal(VersionInfo.Copyright, about.Copyright);
        Assert.Equal("Close", about.Close);
    }

    /// <summary>
    /// GPLv3 section 5(d): the absence of warranty, the terms, and where the full licence is, the
    /// LICENSE file beside the program (R-074) and the GNU page the link opens.
    /// </summary>
    [Fact]
    public void R072_TheLicenceCardCarriesTheNoticeAndLinksTheLicence()
    {
        var about = new AboutViewModel();

        Assert.Equal("LICENCE", about.LicenceHeading);
        Assert.Equal(
            "This program comes with ABSOLUTELY NO WARRANTY. This is free software, and you are welcome to redistribute it "
            + "under the terms of the GNU General Public License, version 3 or later. The full licence text is in the LICENSE "
            + "file next to this program.",
            about.Warranty);
        Assert.Equal("The full text of the GNU General Public License v3", about.LicenceLinkLabel);
        Assert.Equal("https://www.gnu.org/licenses/gpl-3.0.html", about.LicenceUrl);
    }

    /// <summary>The card's rows are the components of the list, in its order, with every field.</summary>
    [Fact]
    public void R075_TheComponentsCardListsTheComponentsOfTheList()
    {
        var about = new AboutViewModel();

        Assert.Equal("THIRD-PARTY COMPONENTS", about.ThirdPartyHeading);
        Assert.Equal(
            "The program links components written by others, each compatible with GPLv3; the FITS reader, the display modes "
            + "and every computation are written by hand. Each licence's full text, and why it is compatible, are in "
            + "THIRD-PARTY-NOTICES.md, beside the source code.",
            about.ThirdPartyLead);
        Assert.Equal(ThirdParty.Components.Select(c => c.Name), about.Components.Select(r => r.Name));
        Assert.Equal(ThirdParty.Components.Select(c => c.Role), about.Components.Select(r => r.Role));
        Assert.Equal(ThirdParty.Components.Select(c => c.Licence), about.Components.Select(r => r.Licence));
        Assert.Equal(ThirdParty.Components.Select(c => c.Copyright), about.Components.Select(r => r.Copyright));
        Assert.Equal(ThirdParty.Components.Select(c => c.Url), about.Components.Select(r => r.Url));
        Assert.Equal("writing the diagnostic log beside the program (the log folder)", Assert.Single(about.Components).Role);
    }

    /// <summary>
    /// The built window: its title, the icon's 256-pixel frame as its logo, Close as the default
    /// and the cancel button, so that Enter and Esc press it, and the licence card's link to the
    /// GNU page.
    /// </summary>
    [Fact]
    public async Task R072_TheWindowHasTheLogoCloseForEnterAndEscAndTheLicenceLink()
    {
        AboutWindow window = await BuildAndLayOutAsync();
        try
        {
            await WpfThread.RunAsync(() =>
            {
                Assert.Equal("About Image Visual Compare", window.Title);
                Assert.Equal(256, ((BitmapSource)window.Logo.Source).PixelWidth);
                Assert.Equal("Close", window.CloseButton.Content);
                Assert.True(window.CloseButton.IsDefault, "Enter does not press Close");
                Assert.True(window.CloseButton.IsCancel, "Esc does not press Close");
                Assert.Equal(new Uri(VersionInfo.LicenceUrl), window.LicenceLink.NavigateUri);
            });
        }
        finally
        {
            await WpfThread.RunAsync(window.Close);
        }
    }

    /// <summary>
    /// The card's row as the template draws it: the name, a dash and the role on the first line,
    /// spaced as FlatCheck's row is, then the licence, the copyright line and the link, whose
    /// address is the component's.
    /// </summary>
    [Fact]
    public async Task R075_TheWindowDrawsARowPerComponentWithItsLink()
    {
        AboutWindow window = await BuildAndLayOutAsync();
        try
        {
            await WpfThread.RunAsync(() =>
            {
                ThirdPartyComponent nlog = Assert.Single(ThirdParty.Components);

                Assert.Single(window.ComponentList.Items);
                Assert.Equal([nlog.Name + "  -  " + nlog.Role, nlog.Licence, nlog.Copyright, nlog.Url], RowTexts(window));
                Hyperlink link = Assert.Single(TextBlocksOf(Row(window)!).Last().Inlines.OfType<Hyperlink>());
                Assert.Equal(new Uri(nlog.Url), link.NavigateUri);
            });
        }
        finally
        {
            await WpfThread.RunAsync(window.Close);
        }
    }

    /// <summary>
    /// Builds the window on the WPF test thread, waits for the data-binding pass to give its
    /// bindings their values, lays its content out at the client width, which builds the card's
    /// rows from their template, and waits for the rows' bindings in turn. Each pass runs after
    /// the body that started it returns, so each wait reads from later bodies, as MainWindowTests
    /// waits for a pair.
    /// </summary>
    private static async Task<AboutWindow> BuildAndLayOutAsync()
    {
        AboutWindow window = null!;
        await WpfThread.RunAsync(() => window = new AboutWindow());
        await UntilAsync(() => window.Title.Length > 0, "the window's bindings");
        await WpfThread.RunAsync(() =>
        {
            // A Windows 10 window of 600 DIPs has a client area 8 DIPs narrower on each side.
            var root = (FrameworkElement)window.Content;
            root.Measure(new Size(584, double.PositiveInfinity));
            root.Arrange(new Rect(root.DesiredSize));
        });
        await UntilAsync(() => RowTexts(window) is [{ Length: > 0 }, { Length: > 0 }, { Length: > 0 }, { Length: > 0 }], "the row's bindings");
        return window;
    }

    /// <summary>Asks the WPF test thread every 50 ms until the condition holds, at most 5 s.</summary>
    private static async Task UntilAsync(Func<bool> condition, string what)
    {
        for (int i = 0; i < 100; i++)
        {
            bool met = false;
            await WpfThread.RunAsync(() => met = condition());
            if (met)
            {
                return;
            }

            await Task.Delay(50);
        }

        Assert.Fail(what + " did not arrive within 5 s");
    }

    /// <summary>The first row of the card as the layout generated it; null before the layout.</summary>
    private static DependencyObject? Row(AboutWindow window) => window.ComponentList.ItemContainerGenerator.ContainerFromIndex(0);

    /// <summary>The text of each text block of the first row, top to bottom; none before the layout.</summary>
    private static string[] RowTexts(AboutWindow window) =>
        Row(window) is { } row ? [.. TextBlocksOf(row).Select(t => new TextRange(t.ContentStart, t.ContentEnd).Text)] : [];

    /// <summary>The text blocks under an element of the visual tree, depth first.</summary>
    private static IEnumerable<TextBlock> TextBlocksOf(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);
            if (child is TextBlock text)
            {
                yield return text;
            }
            else
            {
                foreach (TextBlock inner in TextBlocksOf(child))
                {
                    yield return inner;
                }
            }
        }
    }
}
