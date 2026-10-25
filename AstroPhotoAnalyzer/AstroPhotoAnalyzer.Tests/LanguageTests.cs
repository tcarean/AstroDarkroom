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

using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace AstroPhotoAnalyzer.Tests;

/// <summary>
/// The interface language (ADR 0048): the --lang parser, the console's English default, the
/// error for a wrong value, and the numbers that do not change with the language.
/// </summary>
public class LanguageTests
{
    static (int Code, string Out, string Err) Run(params string[] args)
    {
        var o = new StringWriter(); var e = new StringWriter();
        int code = AnalyzerCli.Run(args, o, e);
        return (code, o.ToString(), e.ToString());
    }

    static string DarkTiff(TempWorkspace ws)
    {
        Raster r = Scenes.DarkSky();
        return ws.WriteFile("dark.tif", TiffTestData.Build16Rgb(r.W, r.H,
            (x, y, c) => RasterOps.Level16(r.At(c, y, x)), IccTestData.V2("sRGB test")));
    }

    static UiLanguage? Found(params string[] args)
    {
        Assert.True(UiLanguageArgs.Find(args, out UiLanguage? lang, out string error), error);
        return lang;
    }

    [Fact]
    public void R150_TheLanguageIsReadInBothFormsAnywhereInAnyCase()
    {
        Assert.Equal(UiLanguage.Romanian, Found("poza.tif", "--lang=ro"));
        Assert.Equal(UiLanguage.Romanian, Found("--lang", "ro", "poza.tif"));
        Assert.Equal(UiLanguage.English, Found("--lang=en", "poza.tif"));
        Assert.Equal(UiLanguage.Romanian, Found("--LANG", "RO"));
        Assert.Equal(UiLanguage.Romanian, Found("--Lang=Ro"));
        Assert.Null(Found("poza.tif", "--medium", "canvas"));
    }

    [Fact]
    public void R150_TheLastLangCounts()
    {
        Assert.Equal(UiLanguage.English, Found("--lang", "ro", "poza.tif", "--lang=en"));
        Assert.Equal(UiLanguage.Romanian, Found("--lang=en", "--lang", "ro"));
    }

    [Fact]
    public void R150_TheOptionsAndTheWindowNeverSeeLangOrItsValue()
    {
        Assert.Equal(new[] { "poza.tif", "--medium", "canvas" },
            UiLanguageArgs.Strip(new[] { "--lang", "ro", "poza.tif", "--lang=en", "--medium", "canvas" }));

        var o = Options.Parse(new[] { "--lang", "ro", "poza.tif", "--medium", "canvas" });
        Assert.Null(o.Error);
        Assert.Equal("poza.tif", o.Image);

        Assert.True(Options.Parse(new[] { "--lang=ro" }).Help);
    }

    [Fact]
    public void R150_AUseScopeRestoresTheLanguageItFound()
    {
        UiLanguage before = Tr.Current;
        using (Tr.Use(UiLanguage.English))
        {
            Assert.Equal("en", Tr.Pick("en", "ro"));
            using (Tr.Use(UiLanguage.Romanian))
                Assert.Equal("ro", Tr.Pick("en", "ro"));
            Assert.Equal("en", Tr.Pick("en", "ro"));
        }
        Assert.Equal(before, Tr.Current);
    }

    [Fact]
    public void R151_WithoutLangTheConsoleSpeaksEnglish()
    {
        UiLanguage before = Tr.Current;

        var (code, help, _) = Run("--help");
        var (_, ajutor, _) = Run("--help", "--lang", "ro");

        Assert.Equal(0, code);
        Assert.Contains("Usage:", help);
        Assert.DoesNotContain("Folosire:", help);
        Assert.Contains("Folosire:", ajutor);
        Assert.Equal(before, Tr.Current);
    }

    [Fact]
    public void R151_WithoutLangTheReportIsEnglish()
    {
        using var ws = new TempWorkspace();
        string tif = DarkTiff(ws);

        var (code, output, err) = Run(tif, "--dmax", "2.0");
        var (codeRo, outputRo, _) = Run(tif, "--dmax", "2.0", "--lang=ro");

        Assert.Equal(3, code);
        Assert.Equal(code, codeRo);
        Assert.Equal("", err);
        Assert.Contains("VERDICT: PROBLEM", output);
        Assert.DoesNotContain("PROBLEMA", output);
        Assert.Contains("VERDICT: PROBLEMA", outputRo);
    }

    [Fact]
    public void R152_AnUnknownLanguageIsAnErrorInEnglish()
    {
        var (code, output, err) = Run("poza.tif", "--lang", "de");

        Assert.Equal(1, code);
        Assert.StartsWith(VersionInfo.Banner, output);
        Assert.Equal("ERROR: --lang expects en or ro, got 'de'" + Environment.NewLine, err);
    }

    [Fact]
    public void R152_LangWithoutAValueIsAnErrorInEnglish()
    {
        var (code, _, err) = Run("poza.tif", "--lang");
        var (codeEq, _, errEq) = Run("poza.tif", "--lang=");

        Assert.Equal(1, code);
        Assert.Equal("ERROR: --lang expects en or ro, but no value was given" + Environment.NewLine, err);
        Assert.Equal(1, codeEq);
        Assert.Equal("ERROR: --lang expects en or ro, got ''" + Environment.NewLine, errEq);
    }

    /// <summary>
    /// The same image in both languages prints the same numbers: only the words change, never a
    /// figure or its format (no decimal comma in Romanian, R-006).
    /// </summary>
    [Fact]
    public void R154_TheNumbersAreTheSameInBothLanguages()
    {
        using var ws = new TempWorkspace();
        string tif = DarkTiff(ws);

        var (_, en, _) = Run(tif, "--aspect", "50:35", "--distance", "1.5", "--dmax", "2.0", "--lang", "en");
        var (_, ro, _) = Run(tif, "--aspect", "50:35", "--distance", "1.5", "--dmax", "2.0", "--lang", "ro");

        Assert.Equal(Numbers(ro), Numbers(en));
    }

    static string GoldenDir([CallerFilePath] string thisFile = "")
        => Path.Combine(Path.GetDirectoryName(thisFile)!, "Golden");

    [Fact]
    public void R153_TheEnglishGoldensHoldNoRomanianWord()
    {
        string[] english = Directory.GetFiles(GoldenDir(), "*.en.txt");
        Assert.Equal(Directory.GetFiles(GoldenDir(), "*.txt").Length - english.Length, english.Length);

        foreach (string file in english)
        {
            // The one Romanian the English output holds by design: the value the user typed.
            string text = File.ReadAllText(file).Replace("'nouă'", "");
            Assert.True(RomanianWords.In(text).Count == 0,
                $"{Path.GetFileName(file)} holds Romanian words: {string.Join(", ", RomanianWords.In(text))}");
        }
    }

    [Fact]
    public void R153_TheGuardFindsARomanianWord()
    {
        Assert.Equal(new[] { "praful", "sub" }, RomanianWords.In(@"dust = praful sub <TEMP>\cer-intunecat.tif, mono.png"));
    }

    static List<string> Numbers(string text)
        => Regex.Matches(text, @"\d+(\.\d+)?").Select(m => m.Value).OrderBy(s => s, StringComparer.Ordinal).ToList();
}
