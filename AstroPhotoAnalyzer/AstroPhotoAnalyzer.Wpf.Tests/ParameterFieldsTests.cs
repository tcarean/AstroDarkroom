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

namespace AstroPhotoAnalyzer.Wpf.Tests;

public class ParameterFieldsTests
{
    [Fact]
    public void R024_TheDefaultsBuildTheDefaultParameters()
    {
        Parameters d = new();
        Parameters? p = new ParameterFields().TryBuild(out string? error);

        Assert.Null(error);
        Assert.Equal(d.Medium, p!.Medium);
        Assert.Null(p.DmaxOverride);   // the field shows the medium's Dmax; untouched, it is not an override
        Assert.Equal(d.Dmax, p.Dmax);
        Assert.Null(p.ViewingDistanceM);
        Assert.Equal(d.CandidateWidthsCm, p.CandidateWidthsCm);
        Assert.Null(p.TargetAspect);
        Assert.Equal(d.TargetBackground, p.TargetBackground);
        Assert.Equal(d.PassepartoutCm, p.PassepartoutCm);
        Assert.Equal(d.FrameCm, p.FrameCm);
    }

    /// <summary>
    /// The Dmax field always shows the Dmax in use: the chosen medium's, from the core, until
    /// the user types another; choosing a medium writes over whatever the field held.
    /// </summary>
    [Fact]
    public void R020_ChoosingAMediumFillsTheDmaxFieldWithItsDmax()
    {
        var f = new ParameterFields();
        Assert.Equal("2.4", f.Dmax);

        f.Medium = "canvas";
        Assert.Equal("1.5", f.Dmax);

        f.Dmax = "1.7";
        f.Medium = "acrilic";
        Assert.Equal("2.2", f.Dmax);
        Assert.Null(f.TryBuild(out _)!.DmaxOverride);   // untouched: the core's own value, not a copy of its text

        f.Medium = "Canvas";   // from code, in another case: still the medium, still its Dmax
        Assert.Equal("1.5", f.Dmax);
        f.Medium = "acrilic";

        f.Dmax = "2.35";
        Parameters? typed = f.TryBuild(out _);
        Assert.Equal(2.35, typed!.Dmax);
        Assert.True(typed.DmaxIsExplicit);

        f.Dmax = "";   // emptied by hand: the medium's
        Parameters? empty = f.TryBuild(out string? error);
        Assert.Null(error);
        Assert.Null(empty!.DmaxOverride);
        Assert.Equal(2.2, empty.Dmax);
    }

    [Fact]
    public void R024_EveryFieldIsHonoured()
    {
        // 8 by 4: the passepartout must be at least twice the frame (Parameters.Validate).
        var f = new ParameterFields { Medium = "canvas", Dmax = "1.7", Distance = "2", Sizes = "60, 70;80", Aspect = "50x35", TargetBg = "0.2", Passepartout = "8", Frame = "4" };
        Parameters? p = f.TryBuild(out string? error);

        Assert.Null(error);
        Assert.Equal(PrintMedium.Canvas, p!.Medium);
        Assert.Equal(1.7, p.Dmax);
        Assert.Equal(2, p.ViewingDistanceM);
        Assert.Equal(new double[] { 60, 70, 80 }, p.CandidateWidthsCm);
        Assert.Equal(50.0 / 35.0, p.TargetAspect!.Value, 9);
        Assert.Equal(0.2, p.TargetBackground);
        Assert.Equal(8, p.PassepartoutCm);
        Assert.Equal(4, p.FrameCm);

        // The field's refusal is the core's message, word for word (R-023, ADR 0040).
        Assert.Null(new ParameterFields { TargetBg = "1" }.TryBuild(out string? bad));
        Assert.Equal(Parameters.TargetBackgroundError(1), bad);
        Assert.Equal(0.123, new ParameterFields { TargetBg = "0.1234567" }.TryBuild(out _)!.TargetBackground);
    }

    [Theory]
    [InlineData("Dmax", "1,5", "punctul")]
    [InlineData("Dmax", "9", "cel mult")]
    [InlineData("Distance", "abc", "numar")]
    [InlineData("Sizes", "", "cel putin o latime")]
    [InlineData("Sizes", "60,x", "latime")]
    [InlineData("Aspect", "50", "latime:inaltime")]
    [InlineData("TargetBg", "1.5", "mai mare decat 0 si mai mic decat 1")]
    [InlineData("TargetBg", "1", "mai mare decat 0 si mai mic decat 1")]
    [InlineData("TargetBg", "0.0004", "mai mare decat 0 si mai mic decat 1")]
    [InlineData("TargetBg", "NaN", "mai mare decat 0 si mai mic decat 1")]
    [InlineData("TargetBg", "0,2", "punctul")]
    public void R024_ABadFieldGivesAMessageNotAnException(string field, string value, string fragment)
    {
        var f = new ParameterFields();
        typeof(ParameterFields).GetProperty(field)!.SetValue(f, value);

        Parameters? p = f.TryBuild(out string? error);

        Assert.Null(p);
        Assert.NotNull(error);
        Assert.Contains(fragment, error);
    }

    [Fact]
    public void R025_APassepartoutNarrowerThanTwiceTheFrameIsRefusedWithTheCoresMessage()
    {
        var f = new ParameterFields { Passepartout = "5", Frame = "3" };

        Parameters? p = f.TryBuild(out string? error);

        Assert.Null(p);
        Assert.Equal(new Parameters { PassepartoutCm = 5, FrameCm = 3 }.Validate(), error);
    }

    [Fact]
    public void R025_ZeroMeansNoPassepartoutAndIsAllowed()
    {
        var f = new ParameterFields { Passepartout = "0", Frame = "3" };

        Parameters? p = f.TryBuild(out string? error);

        Assert.Null(error);
        Assert.Equal(0, p!.PassepartoutCm);
    }

    /// <summary>
    /// A field that reads "NaN" or "Infinity" past the fields' own checks gets the core's
    /// message, the same the console prints (R-024, ADR 0045). Dmax, the distance, the
    /// passepartout and the frame refuse "Infinity" by their range already. The aspect's sides
    /// are syntax: a side that reads as NaN or infinity gets the field's own message, which
    /// quotes the text typed, not the core's on the ratio it would give.
    /// </summary>
    [Theory]
    [InlineData("Dmax", "NaN", "Dmax trebuie sa fie un numar; a primit NaN, care nu e un numar")]
    [InlineData("Distance", "NaN", "distanta de privire trebuie sa fie un numar; a primit NaN, care nu e un numar")]
    [InlineData("Sizes", "NaN", "latimea de print trebuie sa fie un numar; a primit NaN, care nu e un numar")]
    [InlineData("Sizes", "60, Infinity", "latimea de print trebuie sa fie un numar; a primit infinit")]
    [InlineData("Aspect", "NaN:1", "proportia: 'NaN:1' nu e de forma latime:inaltime (ex. 50:35)")]
    [InlineData("Aspect", "Infinity:1", "proportia: 'Infinity:1' nu e de forma latime:inaltime (ex. 50:35)")]
    [InlineData("Aspect", "1:Infinity", "proportia: '1:Infinity' nu e de forma latime:inaltime (ex. 50:35)")]
    [InlineData("Aspect", "Infinity:Infinity", "proportia: 'Infinity:Infinity' nu e de forma latime:inaltime (ex. 50:35)")]
    [InlineData("Passepartout", "NaN", "passepartout-ul trebuie sa fie un numar; a primit NaN, care nu e un numar")]
    [InlineData("Frame", "NaN", "rama trebuie sa fie un numar; a primit NaN, care nu e un numar")]
    public void R024_NaNAndInfinityInAFieldGiveTheCoresMessage(string field, string value, string message)
    {
        var f = new ParameterFields();
        typeof(ParameterFields).GetProperty(field)!.SetValue(f, value);

        Parameters? p = f.TryBuild(out string? error);

        Assert.Null(p);
        Assert.Equal(message, error);
    }

    [Fact]
    public void R026_TheFramingFieldsCarryHelpText()
    {
        var f = new ParameterFields();

        Assert.Contains("dublul", f.PassepartoutHelp);
        Assert.Contains("3-4 cm", f.FrameHelp);
        Assert.Contains("Gol = proportia imaginii", f.AspectHelp);
        Assert.Contains("formate fixe", f.AspectHelp);
    }

    [Fact]
    public void R026_TheDistanceFieldExplainsTheEmptyCaseAndTheFactor()
    {
        var f = new ParameterFields();

        Assert.Contains("Gol = 1.5 x diagonala", f.DistanceHelp);
        Assert.Contains("nu se recomanda niciuna", f.DistanceHelp);
        Assert.Contains("De ce 1.5: de la distanta asta cuprinzi tabloul intreg", f.DistanceHelp);
    }

    /// <summary>
    /// The target is explained by what the simulation does with it, and the figures in the
    /// text are the core's, not typed: the default, the print-safe threshold, the extra
    /// targets.
    /// </summary>
    [Fact]
    public void R026_TheTargetBackgroundFieldExplainsTheSimulationAndTheDefault()
    {
        var f = new ParameterFields();

        Assert.Contains("o curba simpla (gamma)", f.TargetBgHelp);
        Assert.Contains("valoarea din mijloc a canalului verde", f.TargetBgHelp);
        Assert.Contains("Nu e stretch-ul din Siril", f.TargetBgHelp);
        Assert.Contains($"de la {new Parameters().SimulationTargets.Min():0.00} la {new Parameters().SimulationTargets.Max():0.00}", f.TargetBgHelp);
        Assert.Contains("\"(dat)\"", f.TargetBgHelp);
        Assert.Contains("0.15-0.25", f.TargetBgHelp);
        Assert.Contains("cel mai mic nivel", f.TargetBgHelp);
        Assert.Equal("", f.TargetBg);   // empty: the grid only
    }

    /// <summary>R-027: the WCS field lends a FITS's header; empty means none, and the help says what it is for.</summary>
    [Fact]
    public void R027_TheWcsFieldIsOptionalAndExplained()
    {
        var f = new ParameterFields();
        Assert.Equal("", f.WcsPath);
        Assert.Null(f.TryBuild(out _)!.WcsPath);

        f.WcsPath = "  E:\\stack.fit ";
        Assert.Equal("E:\\stack.fit", f.TryBuild(out _)!.WcsPath);

        Assert.Contains("plate-solved", f.WcsHelp);
        Assert.Contains("aceeasi latime si inaltime", f.WcsHelp);
        Assert.Contains("catalogul Lynds", f.WcsHelp);
        Assert.Contains("cu rosu", f.WcsHelp);
    }

    /// <summary>
    /// A single block of 900 characters does not read in a tooltip: every help text is in
    /// paragraphs separated by an empty line, with no spaces at the ends.
    /// </summary>
    [Fact]
    public void R026_TheHelpTextsAreParagraphs()
    {
        var f = new ParameterFields();
        foreach (string help in new[] { f.DistanceHelp, f.AspectHelp, f.TargetBgHelp, f.PassepartoutHelp, f.FrameHelp, f.WcsHelp })
        {
            string[] paragraphs = help.Split("\n\n");
            Assert.True(paragraphs.Length >= 2, "a single paragraph: " + help);
            Assert.All(paragraphs, p => Assert.Equal(p.Trim(), p));
            Assert.All(paragraphs, p => Assert.DoesNotContain("\n", p));
        }
    }

    [Fact]
    public void R020_TheMediaListIsTheCoreOne()
    {
        Assert.Equal(new[] { "baryta", "canvas", "acrilic" }, new ParameterFields().Media);
    }

    /// <summary>
    /// R-026 (ADR 0079): every text of the parameter card is in plain words, in both languages:
    /// the help of each field, the profile line when there is none, the messages of a bad
    /// field, the card's own labels read from the main window's XAML, and Appendix F of the
    /// report, which prints the same parameters, with every field left empty and with each one
    /// given.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void R026_TheCardTextsAreInPlainWords(bool english)
    {
        using IDisposable scope = Tr.Use(english ? UiLanguage.English : UiLanguage.Romanian);
        var f = new ParameterFields();
        var texts = new List<string> { f.ProfileText, f.DistanceHelp, f.AspectHelp, f.TargetBgHelp, f.WcsHelp, f.PassepartoutHelp, f.FrameHelp };
        foreach (var (field, value) in new[] { ("Dmax", "9"), ("Distance", "abc"), ("Distance", "150"), ("Passepartout", "-1"), ("Aspect", "50"), ("TargetBg", "1.5"), ("Sizes", "") })
        {
            var bad = new ParameterFields();
            typeof(ParameterFields).GetProperty(field)!.SetValue(bad, value);
            Assert.Null(bad.TryBuild(out string? error));
            texts.Add(error!);
        }
        texts.Add(new Parameters { TargetAspect = 0 }.Validate());
        var pair = new System.Text.RegularExpressions.Regex(@"\{local:T En='([^']*)', Ro='([^']*)'\}");
        texts.AddRange(pair.Matches(MainWindowXaml()).Select(m => m.Groups[english ? 1 : 2].Value));
        foreach (Parameters p in new[] { new Parameters(), new Parameters { DmaxOverride = 1.8, ViewingDistanceM = 2, TargetAspect = 50.0 / 35, TargetBackground = 0.17 } })
        {
            var report = new System.IO.StringWriter();
            ReportWriter.WriteFull(AnalysisRun.Execute(AstroPhotoAnalyzer.Tests.Scenes.CleanSky(), "memorie", p), report);
            string[] lines = report.ToString().Split('\n');
            int start = Array.FindIndex(lines, l => l.StartsWith(Tr.Pick("Appendix F.", "Anexa F.")));
            Assert.True(start > 0, "the report has Appendix F");
            texts.AddRange(lines.Skip(start));
        }

        var methodWords = new System.Text.RegularExpressions.Regex(
            @"\b(mediana|median|grila|grid|samanta|nativ|native|candidate|intervalului|interval|strict|strictly|explicit|mediu\w*|medium\w*)\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        Assert.All(texts, t => Assert.False(methodWords.IsMatch(t), t));
        Assert.All(texts, t => Assert.DoesNotContain("ln(", t));
        Assert.Contains(Tr.Pick("What you print on", "Pe ce printezi"), texts);
        Assert.Contains(texts, t => t.Contains(Tr.Pick("the starting number of the choice of patches (seed)", "numarul de pornire al alegerii dalelor (seed)")));
    }

    /// <summary>The main window's XAML, read from the source tree beside this file's project.</summary>
    static string MainWindowXaml([System.Runtime.CompilerServices.CallerFilePath] string thisFile = "")
        => File.ReadAllText(Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(thisFile)!)!, "AstroPhotoAnalyzer.Wpf", "MainWindow.xaml"));
}
