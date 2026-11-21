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

using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using ImageVisualCompare.Core;
using ImageVisualCompare.Wpf;
using ImageVisualCompare.Wpf.Controls;
using ImageVisualCompare.Wpf.Imaging;
using ImageVisualCompare.Wpf.Services;

namespace ImageVisualCompare.Tests;

/// <summary>
/// How the theme shows the keyboard focus (R-068), on the WPF test thread (ADR 0032). CR-016 of
/// the review of 2026-11-13: the theme had no focus visual of its own, so a button with the
/// focus got WPF's, a dotted rectangle stroked black on a nearly black button, the comparison
/// surface got the same over the image, and the accent border of a dialog's default button
/// stayed on it when the focus, and with it the answer Enter gives, moved to the other button.
///
/// Nothing here has the keyboard: the windows of the tests have no handle. WPF's read-only
/// focus properties are set through their keys instead, as the keyboard device sets them, and
/// what the styles' triggers make of them is read back.
/// </summary>
public class ThemeFocusTests
{
    [Theory]
    [InlineData("ToolButton")]
    [InlineData("ChangeButton")]
    [InlineData("DialogButton")]
    [InlineData("ToolToggleButton")]
    public async Task R068_AButtonWithTheKeyboardFocus_HasTheAccentBorder_AndNoDottedRectangle(string styleKey)
    {
        Brush? resting = null;
        Brush? focused = null;
        Brush? after = null;
        Style? dotted = new();

        await WpfThread.RunAsync(() =>
        {
            ButtonBase button = styleKey == "ToolToggleButton" ? new ToggleButton() : new Button();
            button.Style = (Style)Application.Current.FindResource(styleKey);
            button.Content = "x";
            button.ApplyTemplate();
            var chrome = (Border)button.Template.FindName("Chrome", button);

            resting = chrome.BorderBrush;
            button.SetValue(KeyOf(typeof(UIElement), "IsKeyboardFocusedPropertyKey"), true);
            focused = chrome.BorderBrush;
            button.SetValue(KeyOf(typeof(UIElement), "IsKeyboardFocusedPropertyKey"), false);
            after = chrome.BorderBrush;
            dotted = button.FocusVisualStyle;
        });

        Assert.Same(Brush("ChromeBorderBrush"), resting);
        Assert.Same(Brush("AccentBrush"), focused);
        Assert.Same(Brush("ChromeBorderBrush"), after);
        Assert.Null(dotted);
    }

    [Fact]
    public async Task R068_TheDefaultDialogButton_IsLitOnlyWhileEnterAnswersWithIt()
    {
        Brush? plain = null;
        Brush? noFocusInTheDialog = null;
        Brush? enterAnswersWithIt = null;
        Brush? anotherButtonHasTheFocus = null;

        await WpfThread.RunAsync(() =>
        {
            var button = new Button { Style = (Style)Application.Current.FindResource("DialogButton"), Content = "Yes" };
            button.ApplyTemplate();
            var chrome = (Border)button.Template.FindName("Chrome", button);
            plain = chrome.BorderBrush;

            try
            {
                // WPF keeps IsDefaulted true on a default button while the keyboard focus is in
                // its window and on nothing that takes Enter itself, as another button does.
                // Here nothing has the focus, so it is false until the test sets it.
                button.IsDefault = true;
                noFocusInTheDialog = chrome.BorderBrush;

                button.SetValue(KeyOf(typeof(Button), "IsDefaultedPropertyKey"), true);
                enterAnswersWithIt = chrome.BorderBrush;

                button.SetValue(KeyOf(typeof(Button), "IsDefaultedPropertyKey"), false);
                anotherButtonHasTheFocus = chrome.BorderBrush;
            }
            finally
            {
                // A default button listens to every change of the keyboard focus; let it go.
                button.IsDefault = false;
            }
        });

        Assert.Same(Brush("ChromeBorderBrush"), plain);
        Assert.Same(Brush("ChromeBorderBrush"), noFocusInTheDialog);
        Assert.Same(Brush("AccentBrush"), enterAnswersWithIt);
        Assert.Same(Brush("ChromeBorderBrush"), anotherButtonHasTheFocus);
    }

    [Fact]
    public async Task R068_TheSurface_ShowsNoMarkOfTheFocus()
    {
        Style? visual = new();
        bool focusable = false;

        await WpfThread.RunAsync(() =>
        {
            var surface = new CompareView();
            visual = surface.FocusVisualStyle;
            focusable = surface.Focusable;
        });

        // It takes the focus, for its shortcuts (R-063); it draws nothing for having it.
        Assert.True(focusable);
        Assert.Null(visual);
    }

    [Fact]
    public async Task R068_ALinkWithTheKeyboardFocus_IsUnderlined_AndHasNoDottedRectangle()
    {
        int resting = -1;
        TextDecorationCollection? focused = null;
        Style? dotted = new();

        await WpfThread.RunAsync(() =>
        {
            var link = new Hyperlink(new Run("https://example.org"))
            {
                Style = (Style)Application.Current.FindResource(typeof(Hyperlink)),
            };

            resting = link.TextDecorations?.Count ?? 0;
            link.SetValue(KeyOf(typeof(UIElement), "IsKeyboardFocusedPropertyKey"), true);
            focused = link.TextDecorations;
            dotted = link.FocusVisualStyle;
        });

        Assert.Equal(0, resting);
        // One decoration, an underline: the style's own, which the XAML reader builds, not the
        // instance WPF keeps as TextDecorations.Underline.
        Assert.Equal(TextDecorationLocation.Underline, Assert.Single(focused!).Location);
        Assert.Null(dotted);
    }

    /// <summary>
    /// Everything that can take the keyboard focus in the three windows, as far as each window's
    /// own tree goes, the parts the controls' templates add left out: whatever kept a
    /// <c>FocusVisualStyle</c> would get WPF's dotted rectangle. Not only what Tab stops at: the
    /// surface is no tab stop and had the rectangle after a click. The two reviews of this fix
    /// found two elements it had missed: the list of components in About, an
    /// <c>ItemsControl</c>, which WPF makes a tab stop; and the message area of the dialog, a
    /// <c>ScrollViewer</c>, no tab stop, which takes the focus at a click on the message.
    /// </summary>
    [Fact]
    public async Task R068_NothingThatCanTakeTheFocus_KeepsWpfsDottedRectangle()
    {
        var dotted = new List<string>();
        var stops = new List<string>();
        var focusable = new List<string>();

        await WpfThread.RunAsync(() =>
        {
            var windows = new (string Name, Window Window)[]
            {
                ("MainWindow", new MainWindow(new NoDialogs(), new ImagePairLoader(new NoLoader()))),
                ("MessageDialog", (Window)Activator.CreateInstance(typeof(MessageDialog), nonPublic: true)!),
                ("AboutWindow", new AboutWindow()),
            };

            foreach ((string name, Window window) in windows)
            {
                foreach (DependencyObject node in LogicalTree(window))
                {
                    // The window itself is left out: it is focusable, and has the focus when it
                    // opens, but a focus mark is drawn on the adorner layer above an element,
                    // and the layer is inside the window, so the window never gets one.
                    if (ReferenceEquals(node, window))
                    {
                        continue;
                    }

                    (bool takesFocus, bool stop, Style? visual, string what) = node switch
                    {
                        FrameworkElement e => (e.Focusable, KeyboardNavigation.GetIsTabStop(e), e.FocusVisualStyle, e.Name.Length > 0 ? e.Name : e.GetType().Name),
                        FrameworkContentElement c => (c.Focusable, true, c.FocusVisualStyle, c.GetType().Name),
                        _ => (false, false, null, string.Empty),
                    };

                    if (!takesFocus)
                    {
                        continue;
                    }

                    focusable.Add($"{name}.{what}");
                    if (stop)
                    {
                        stops.Add($"{name}.{what}");
                    }

                    if (visual is not null)
                    {
                        dotted.Add($"{name}.{what}");
                    }
                }
            }
        });

        // The walk does see the tab stops: the toolbar's, the dialog's two buttons, About's.
        Assert.Contains("MainWindow.OpenButton", stops);
        Assert.Contains("MessageDialog.PrimaryButton", stops);
        Assert.Contains("MessageDialog.SecondaryButton", stops);
        Assert.Contains("AboutWindow.CloseButton", stops);
        Assert.Contains("AboutWindow.Hyperlink", stops);

        // And what takes the focus without being one: the surface, the dialog's message area.
        Assert.Contains("MainWindow.Compare", focusable);
        Assert.DoesNotContain("MainWindow.Compare", stops);
        Assert.Contains("MessageDialog.ScrollViewer", focusable);
        Assert.DoesNotContain("MessageDialog.ScrollViewer", stops);
        Assert.Empty(dotted);
    }

    /// <summary>A window's logical tree, the window first: what its XAML and its code put in it,
    /// without the parts the controls' templates add.</summary>
    private static IEnumerable<DependencyObject> LogicalTree(DependencyObject node)
    {
        yield return node;
        foreach (DependencyObject child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>())
        {
            foreach (DependencyObject below in LogicalTree(child))
            {
                yield return below;
            }
        }
    }

    /// <summary>Dialogs that are never asked: the window is built, not loaded.</summary>
    private sealed class NoDialogs : IFileDialogService
    {
        public ImagePairSelection? PickImagePair(string? initialDirectory = null) => null;

        public string? PickImage(ImageSide side, string? initialDirectory = null) => null;
    }

    /// <summary>A loader that is never asked.</summary>
    private sealed class NoLoader : IImageLoader
    {
        public ImageInfo Probe(string path) => throw new NotSupportedException();

        public Task<LoadedImage> LoadAsync(
            string path, DisplayMode mode = DisplayMode.Linear, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    /// <summary>A brush of the theme, by its key.</summary>
    private static Brush Brush(string key)
    {
        Brush? brush = null;
        WpfThread.RunAsync(() => brush = (Brush)Application.Current.FindResource(key)).GetAwaiter().GetResult();
        return brush!;
    }

    /// <summary>
    /// The key of one of WPF's read-only properties, which only WPF holds: the keyboard device
    /// sets <c>IsKeyboardFocused</c> through it, and <c>Button</c> its own <c>IsDefaulted</c>.
    /// </summary>
    private static DependencyPropertyKey KeyOf(Type owner, string field) =>
        (DependencyPropertyKey?)owner.GetField(field, BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null)
        ?? throw new InvalidOperationException($"{owner.Name}.{field} was not found: WPF has changed, and this test with it.");
}
