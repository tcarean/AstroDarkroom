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

using System.Xml.Linq;
using AstroDarkroom.Core;

namespace AstroDarkroom.Tests;

/// <summary>
/// R-021: the Help button and F1 open the launcher's manual at the section of the window
/// (ADR 0030). The address is the core's; the window has no test project (ADR 0001), so of the
/// window its XAML is read as XML, as <see cref="WindowFitTests"/> reads it, and its code-behind
/// as text for the wordings <c>Relabel</c> gives the two buttons.
/// </summary>
public class HelpTests
{
    private static readonly XNamespace Wpf = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static string WindowFolder => Path.Combine(TestData.FindRepoRoot(), "src", "AstroDarkroom.Wpf");

    [Fact]
    public void R021_TheManualOpensAtTheWindowsSection()
    {
        Assert.Equal("https://github.com/tcarean/AstroDarkroom/wiki", Manual.Page);
        Assert.Equal("https://github.com/tcarean/AstroDarkroom/wiki#the-window", Manual.Window);

        // What the Help command runs opens that address, the one constant whatever the language
        // (the wiki is English only), through the path the runtime's link takes (R-018).
        string code = File.ReadAllText(Path.Combine(WindowFolder, "MainWindow.xaml.cs"));
        Assert.Contains("void ShowHelp(object sender, ExecutedRoutedEventArgs e) => OpenInBrowser(Manual.Window);", code, StringComparison.Ordinal);
    }

    [Fact]
    public void R021_F1AndTheHelpButtonOpenTheManual_AboutOpensFromItsButtonOnly()
    {
        XElement window = XDocument.Load(Path.Combine(WindowFolder, "MainWindow.xaml")).Root!;

        // F1 is the default gesture of ApplicationCommands.Help: its one binding opens the manual.
        XElement binding = Assert.Single(window.Element(Wpf + "Window.CommandBindings")!.Elements(Wpf + "CommandBinding"));
        Assert.Equal("Help", binding.Attribute("Command")?.Value);
        Assert.Equal("ShowHelp", binding.Attribute("Executed")?.Value);

        // The bottom bar: the copyright, the selector, Help, About, each in a column of its own.
        XElement bar = window.Element(Wpf + "Grid")!.Elements(Wpf + "Border").Last().Element(Wpf + "Grid")!;
        Assert.Equal(["*", "Auto", "Auto", "Auto"],
            bar.Element(Wpf + "Grid.ColumnDefinitions")!.Elements(Wpf + "ColumnDefinition").Select(c => c.Attribute("Width")?.Value));
        XElement selector = Named(bar, "EnglishChoice").Parent!;
        XElement help = Named(bar, "HelpButton");
        XElement about = Named(bar, "AboutButton");
        Assert.All([selector, help, about], e => Assert.Same(bar, e.Parent));
        Assert.Equal(["1", "2", "3"], new[] { selector, help, about }.Select(e => e.Attribute("Grid.Column")?.Value));

        // Help: the command, then the icon before the word.
        Assert.Equal("Help", help.Attribute("Command")?.Value);
        Assert.Equal(["Image", "TextBlock"],
            help.Descendants().Where(e => e.Name == Wpf + "Image" || e.Name == Wpf + "TextBlock").Select(e => e.Name.LocalName));
        Assert.Equal("{StaticResource HelpIcon}", help.Descendants(Wpf + "Image").Single().Attribute("Source")?.Value);
        Assert.Equal("HelpLabel", help.Descendants(Wpf + "TextBlock").Single().Attribute(Xaml + "Name")?.Value);

        // About: its own click, no command, so no key.
        Assert.Null(about.Attribute("Command"));
        Assert.Equal("ShowAbout", about.Attribute("Click")?.Value);

        // The icon is drawn in the application's resources, not taken from a font.
        XElement app = XDocument.Load(Path.Combine(WindowFolder, "App.xaml")).Root!;
        Assert.Contains(app.Descendants(Wpf + "DrawingImage"), d => d.Attribute(Xaml + "Key")?.Value == "HelpIcon");
    }

    [Fact]
    public void R021_TheButtonsWordsAndTips_InBothLanguages()
    {
        string code = File.ReadAllText(Path.Combine(WindowFolder, "MainWindow.xaml.cs"));

        // Each wording on its own element, so a tooltip given to the other button fails.
        Assert.Contains("HelpLabel.Text = T(\"Help\", \"Ajutor\");", code, StringComparison.Ordinal);
        Assert.Contains("HelpButton.ToolTip = T(\"The manual, at the section of this window (F1)\", \"Manualul, la sectiunea acestei ferestre (F1)\");", code, StringComparison.Ordinal);
        Assert.Contains("AboutButton.Content = T(\"About\", \"Despre\");", code, StringComparison.Ordinal);
        Assert.Contains("AboutButton.ToolTip = T(\"Version, licence, components\", \"Versiune, licenta, componente\");", code, StringComparison.Ordinal);
        Assert.DoesNotContain("components (F1)", code, StringComparison.Ordinal);
    }

    /// <summary>The one element of <paramref name="root"/>'s tree with that <c>x:Name</c>.</summary>
    private static XElement Named(XElement root, string name) =>
        root.Descendants().Single(e => e.Attribute(Xaml + "Name")?.Value == name);
}
