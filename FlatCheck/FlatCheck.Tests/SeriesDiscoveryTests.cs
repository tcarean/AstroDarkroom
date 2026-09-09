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

namespace FlatCheck.Tests;

/// <summary>
/// Alegerea fisierelor pentru --light pe un folder.
///
/// Doua capcane sunt fixate aici, si amandoua ar trece neobservate in productie.
///
/// Prima: <see cref="ThePatternDoesNotLeakIntoLongerExtensions"/>. Directory.GetFiles cu
/// un tipar foloseste potrivirea Win32, care din mostenirea 8.3 mai prinde si extensii
/// mai lungi - "*.fit" ar inghiti si ".fits". De aceea enumerarea e completa si potrivirea
/// se face in proces.
///
/// A doua: <see cref="RegisteredFramesAreLeftOutByTheDefaultPattern"/>. Cadrele r_pp_light_*
/// sunt registrate, adica transformate geometric, deci eroarea de flat - fixa pe senzor -
/// e mutata fata de centrul in jurul caruia se fiteaza modelul radial. Includerea lor ar
/// strica masuratoarea in tacere.
/// </summary>
public class SeriesDiscoveryTests
{
    // ------------------------------------------------------------ potrivirea

    [Theory]
    [InlineData("pp_light_00001.fit", "pp_light_*.fit", true)]
    [InlineData("pp_light_00042.fit", "pp_light_*.fit", true)]
    [InlineData("r_pp_light_00001.fit", "pp_light_*.fit", false)]   // tiparul e ancorat la inceput
    [InlineData("light_00001.fit", "pp_light_*.fit", false)]
    [InlineData("pp_flat_stacked.fit", "pp_light_*.fit", false)]
    [InlineData("pp_light_00001.fits", "pp_light_*.fit", false)]
    [InlineData("PP_LIGHT_00001.FIT", "pp_light_*.fit", true)]      // fara distinctie de litere
    [InlineData("a.fit", "?.fit", true)]
    [InlineData("ab.fit", "?.fit", false)]
    [InlineData("orice", "*", true)]
    [InlineData("", "*", true)]
    [InlineData("", "?", false)]
    public void TheGlobMatcherHandlesStarAndQuestionMark(string name, string pattern, bool expected)
    {
        Assert.Equal(expected, SeriesFinder.GlobMatch(name, pattern));
    }

    /// <summary>
    /// Clasele de caractere nu sunt suportate - sunt tratate LITERAL, in ambele
    /// implementari. Daca C#-ul le-ar ignora si Python-ul le-ar interpreta (fnmatch o
    /// face), cele doua ar alege fisiere diferite din acelasi folder, iar crosscheck-ul
    /// ar raporta diferente numerice care de fapt sunt diferente de intrare.
    /// </summary>
    [Fact]
    public void CharacterClassesAreLiteralNotWildcards()
    {
        Assert.True(SeriesFinder.GlobMatch("[ab].fit", "[ab].fit"));
        Assert.False(SeriesFinder.GlobMatch("a.fit", "[ab].fit"));
    }

    /// <summary>Backtracking-ul: mai multe stele si o coada care trebuie sa se potriveasca.</summary>
    [Theory]
    [InlineData("pp_light_00001.fit", "*light*.fit", true)]
    [InlineData("pp_light_00001.fit", "*light*.png", false)]
    [InlineData("aaa.fit", "*a.fit", true)]
    [InlineData("aaab.fit", "*a.fit", false)]
    public void TheMatcherBacktracksCorrectly(string name, string pattern, bool expected)
    {
        Assert.Equal(expected, SeriesFinder.GlobMatch(name, pattern));
    }

    // ------------------------------------------------------------ enumerarea

    [Fact]
    public void ThePatternDoesNotLeakIntoLongerExtensions()
    {
        using var ws = new TempWorkspace();
        ws.WriteFile("pp_light_00001.fit", new byte[] { 1 });
        ws.WriteFile("pp_light_00002.fits", new byte[] { 1 });

        var found = SeriesFinder.Find(ws.Dir, "pp_light_*.fit");

        Assert.Single(found);
        Assert.EndsWith("pp_light_00001.fit", found[0]);
    }

    [Fact]
    public void RegisteredFramesAreLeftOutByTheDefaultPattern()
    {
        using var ws = new TempWorkspace();
        foreach (string n in new[]
                 { "pp_light_00001.fit", "pp_light_00002.fit", "r_pp_light_00001.fit",
                   "light_00001.fit", "bias_00001.fit", "pp_light_.seq" })
            ws.WriteFile(n, new byte[] { 1 });

        var found = SeriesFinder.Find(ws.Dir, "pp_light_*.fit");

        Assert.Equal(2, found.Count);
        Assert.All(found, p => Assert.StartsWith("pp_light_", Path.GetFileName(p)));
    }

    /// <summary>
    /// Ordinea trebuie sa fie ordinala pe numele fisierului, identica cu sorted() din
    /// Python. Daca cele doua implementari ar lista cadrele in ordini diferite, tabelele
    /// ar iesi diferite si crosscheck-ul ar raporta o diferenta care nu exista.
    /// </summary>
    [Fact]
    public void FilesComeBackSortedOrdinallyByName()
    {
        using var ws = new TempWorkspace();
        foreach (string n in new[] { "pp_light_00010.fit", "pp_light_00002.fit", "pp_light_00001.fit" })
            ws.WriteFile(n, new byte[] { 1 });

        var names = SeriesFinder.Find(ws.Dir, "pp_light_*.fit")
            .Select(Path.GetFileName).ToArray();

        Assert.Equal(new[] { "pp_light_00001.fit", "pp_light_00002.fit", "pp_light_00010.fit" }, names);
    }

    [Fact]
    public void AFolderWithNoMatchesComesBackEmpty()
    {
        using var ws = new TempWorkspace();
        ws.WriteFile("bias_00001.fit", new byte[] { 1 });

        Assert.Empty(SeriesFinder.Find(ws.Dir, "pp_light_*.fit"));
    }

    /// <summary>Subfolderele nu se coboara: process/cache/ nu contine cadre de analizat.</summary>
    [Fact]
    public void SubdirectoriesAreNotDescendedInto()
    {
        using var ws = new TempWorkspace();
        Directory.CreateDirectory(Path.Combine(ws.Dir, "cache"));
        File.WriteAllBytes(Path.Combine(ws.Dir, "cache", "pp_light_00001.fit"), new byte[] { 1 });

        Assert.Empty(SeriesFinder.Find(ws.Dir, "pp_light_*.fit"));
    }
}
