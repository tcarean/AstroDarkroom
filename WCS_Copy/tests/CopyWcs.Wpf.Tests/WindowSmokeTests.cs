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

using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CopyWcs.Wpf.Tests;

/// <summary>
/// The window really loads and really draws. The view-model tests check C#; XAML is checked
/// only at run time: a misnamed resource, a missing style or a binding to a property that
/// does not exist pass the compiler and show when the window loads. Each test builds the
/// element, lays it out, renders it to a bitmap and asserts that no binding error was traced.
/// With COPYWCS_DUMP_SCREENS=&lt;folder&gt; each render is also saved as a PNG, to look at
/// the window without starting it; it is not a comparison against a reference. After
/// DarksCheck's (ADR 0007).
/// </summary>
[Collection("window")]
public class WindowSmokeTests
{
    private readonly StaFixture _sta;

    public WindowSmokeTests(StaFixture sta) => _sta = sta;

    /// <summary>Catches the binding errors WPF otherwise writes only to the debugger's output.</summary>
    internal sealed class BindingErrors : IDisposable
    {
        private readonly StringBuilder _sb = new();
        private readonly TraceListener _listener;
        private readonly SourceLevels _before;

        internal BindingErrors()
        {
            PresentationTraceSources.Refresh();
            _before = PresentationTraceSources.DataBindingSource.Switch.Level;
            PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
            _listener = new TextWriterTraceListener(new StringWriter(_sb));
            PresentationTraceSources.DataBindingSource.Listeners.Add(_listener);
        }

        internal string Text
        {
            get
            {
                _listener.Flush();
                return _sb.ToString();
            }
        }

        public void Dispose()
        {
            PresentationTraceSources.DataBindingSource.Listeners.Remove(_listener);
            PresentationTraceSources.DataBindingSource.Switch.Level = _before;
            _listener.Dispose();
        }
    }

    internal static void Render(FrameworkElement el, int w = 720, int h = 560, string? name = null)
    {
        el.Measure(new Size(w, h));
        el.Arrange(new Rect(0, 0, w, h));
        el.UpdateLayout();

        // The window's background first: the content alone renders on transparency, where the
        // anti-aliased labels come out dim in the dumped PNG.
        System.Windows.Shapes.Rectangle background = new() { Width = w, Height = h, Fill = new SolidColorBrush(Color.FromRgb(0x1B, 0x1B, 0x1D)) };
        background.Measure(new Size(w, h));
        background.Arrange(new Rect(0, 0, w, h));
        RenderTargetBitmap bmp = new(w, h, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(background);
        bmp.Render(el);

        Assert.Equal(w, bmp.PixelWidth);
        Dump(bmp, name);
    }

    private static void Dump(RenderTargetBitmap bmp, string? name)
    {
        string? dir = Environment.GetEnvironmentVariable("COPYWCS_DUMP_SCREENS");
        if (string.IsNullOrEmpty(dir) || name is null)
        {
            return;
        }

        Directory.CreateDirectory(dir);
        PngBitmapEncoder enc = new();
        enc.Frames.Add(BitmapFrame.Create(bmp));
        using FileStream fs = File.Create(Path.Combine(dir, name + ".png"));
        enc.Save(fs);
    }

    /// <summary>The window before any file: the empty state, the dark palette as its background (R-037).</summary>
    [Fact]
    public void R037_TheMainWindowLoadsAndRendersEmpty()
        => _sta.Invoke(() =>
        {
            using BindingErrors errors = new();
            MainWindow w = new();
            Render((FrameworkElement)w.Content, name: "window-empty");
            Assert.True(errors.Text.Length == 0, "binding errors:\n" + errors.Text);
            Assert.Equal(Color.FromRgb(0x1B, 0x1B, 0x1D), Assert.IsType<SolidColorBrush>(w.Background).Color);
        });

    /// <summary>The files from the command line fill the boxes, either one alone too (R-034).</summary>
    [Fact]
    public void R034_StartFillsTheBoxesWithTheFilesGiven()
        => _sta.Invoke(() =>
        {
            using WindowFixture f = new();
            MainViewModel vm = new();
            MainWindow w = new(vm);

            w.Start(f.Source, f.Dest);
            Assert.Equal(f.Source, vm.Source);
            Assert.Equal(f.Dest, vm.Dest);
            Assert.True(vm.CanPreview);

            MainViewModel one = new();
            new MainWindow(one).Start(null, f.Dest);
            Assert.Equal("", one.Source);
            Assert.Equal(f.Dest, one.Dest);
            Assert.False(one.CanPreview);
        });

    /// <summary>The window with an outcome of every kind drawn: the refusal, the warning, the cards and the closing lines (R-037).</summary>
    [Fact]
    public void R037_TheOutcomeCardRendersEveryKindWithNoBindingError()
        => _sta.Invoke(() =>
        {
            using WindowFixture f = new(destWidth: 200);
            using BindingErrors errors = new();
            MainViewModel vm = new() { Source = f.Source, Dest = f.Dest };
            MainWindow w = new(vm);

            vm.Preview();
            Assert.True(vm.HasError);
            Render((FrameworkElement)w.Content, name: "window-refused");

            vm.Force = true;
            vm.Preview();
            Assert.True(vm.HasWarnings);
            Assert.True(vm.HasCards);
            Render((FrameworkElement)w.Content, name: "window-preview");

            vm.Copy();
            Assert.True(vm.HasNotes);
            Render((FrameworkElement)w.Content, name: "window-copied");

            Assert.True(errors.Text.Length == 0, "binding errors:\n" + errors.Text);
        });
}
