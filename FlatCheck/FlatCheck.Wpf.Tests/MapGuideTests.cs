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

namespace FlatCheck.Wpf.Tests;

/// <summary>
/// Ghidul hartii. Nu se poate testa daca un text e bine scris, dar se poate testa ca
/// spune lucrurile fara care nu ajuta la nimic: ce inseamna albul, si care sunt formele
/// pe care le explica. Fara astea ghidul ar putea fi golit intr-o reformulare si nimic
/// nu ar cadea.
/// </summary>
public class MapGuideTests
{
    [Fact]
    public void TheGuideNamesEveryShapeItExplains()
    {
        var shapes = SessionMessages.MapGuideShapes();

        Assert.Equal(6, shapes.Count);
        Assert.All(shapes, s =>
        {
            Assert.NotEmpty(s.Shape);
            Assert.NotEmpty(s.Meaning);
        });

        string names = string.Join(" | ", shapes.Select(s => s.Shape));
        Assert.Contains("pete", names);
        Assert.Contains("dipol", names);
        Assert.Contains("cercuri", names);
        Assert.Contains("pata", names);
    }

    /// <summary>
    /// Ce spune intro-ul, si de ce conteaza: albul e 1.0, iar tot ce a ramas pe harta NU
    /// poate fi vignetare. Cine nu stie asta citeste harta pe dos.
    /// </summary>
    [Fact]
    public void TheIntroSaysWhatWhiteMeansAndWhatWasRemoved()
    {
        string intro = SessionMessages.MapGuideIntro();

        Assert.Contains("alb", intro);
        Assert.Contains("model", intro);
        Assert.Contains("vignetare", intro);
    }

    /// <summary>
    /// Si avertismentul despre scara automata: pe ea culorile umplu mereu tot intervalul,
    /// deci intensitatea NU e o masura. Asta e chiar confuzia din care a pornit
    /// comutatorul de scara.
    /// </summary>
    [Fact]
    public void TheIntroWarnsThatColourIntensityDependsOnTheScale()
    {
        string intro = SessionMessages.MapGuideIntro();

        Assert.Contains("auto", intro);
        Assert.Contains("legenda", intro);
    }

    /// <summary>
    /// Fara diacritice, ca tot restul textului scos de unealta - raportul de consola si
    /// fisierul salvat nu au niciuna, si fereastra nu trebuie sa arate ca alt program.
    /// </summary>
    [Fact]
    public void TheGuideUsesNoDiacritics()
    {
        string all = SessionMessages.MapGuideIntro() + string.Concat(
            SessionMessages.MapGuideShapes().Select(s => s.Shape + s.Meaning));

        Assert.All(all, c => Assert.True(c < 128, $"caracter non-ASCII: '{c}'"));
    }

    /// <summary>ViewModel-ul doar transporta textul, nu-l rescrie.</summary>
    [Fact]
    public void TheViewModelCarriesTheSameTextAsTheSource()
    {
        var vm = new MapGuideViewModel();

        Assert.Equal(SessionMessages.MapGuideIntro(), vm.Intro);
        Assert.Equal(
            SessionMessages.MapGuideShapes().Select(s => s.Shape),
            vm.Shapes.Select(r => r.Shape));
    }

    /// <summary>
    /// Randul despre inele NU mai trebuie sa spuna ca inelele inseamna un obstacol rotund
    /// descentrat. Aia era citirea unui artefact al modelului radial, corectat de atunci -
    /// iar cineva care citea harta pleca sa caute un obiect care nu existase niciodata.
    /// Testul exista ca sa nu se intoarca formularea veche din istoria git.
    /// </summary>
    [Fact]
    public void TheRingRowDoesNotClaimARingMeansADecentredObstruction()
    {
        var rings = SessionMessages.MapGuideShapes().Single(s => s.Shape.Contains("cercuri"));

        Assert.DoesNotContain("descentrat", rings.Shape);
        Assert.Contains("NU poate", rings.Meaning);       // un inel centrat pe senzor
        Assert.Contains("artefact", rings.Meaning);       // si de ce se vedeau inainte
    }

    // ------------------------------------------------- cum citesc tabul

    /// <summary>
    /// Cele doua jumatati ale citirii. Textul trebuie sa spuna explicit care decide -
    /// altfel cifrele din stanga si harta din dreapta arata ca doua vederi ale aceluiasi
    /// lucru, si asta e chiar confuzia pe care panoul o rezolva.
    /// </summary>
    [Fact]
    public void TheHowToReadNoteSeparatesHowMuchFromWhatKind()
    {
        var rows = SessionMessages.HowToReadRows();

        Assert.Equal(2, rows.Count);
        Assert.Contains("CAT DE MULT", rows[0].Shape);
        Assert.Contains("CE FEL", rows[1].Shape);
        Assert.Contains("cifrele", rows[0].Shape);
        Assert.Contains("NU depinde", rows[0].Meaning);   // verdictul nu depinde de harta
    }

    [Fact]
    public void TheHowToReadNoteUsesNoDiacritics()
    {
        string all = SessionMessages.HowToReadIntro() + string.Concat(
            SessionMessages.HowToReadRows().Select(s => s.Shape + s.Meaning));

        Assert.All(all, c => Assert.True(c < 128, $"caracter non-ASCII: '{c}'"));
    }

    [Fact]
    public void TheHowToReadViewModelCarriesTheSameTextAndStartsCollapsed()
    {
        var vm = new HowToReadViewModel();

        Assert.Equal(SessionMessages.HowToReadIntro(), vm.Intro);
        Assert.Equal(
            SessionMessages.HowToReadRows().Select(s => s.Shape),
            vm.Rows.Select(r => r.Shape));
        Assert.False(vm.IsExpanded);
    }
}
