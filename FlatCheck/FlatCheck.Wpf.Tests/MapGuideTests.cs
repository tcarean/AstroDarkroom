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
/// The map guide. Whether a text is well written cannot be tested, but it can be tested that
/// it says the things without which it helps with nothing: what white means, and which shapes
/// it explains. Without these the guide could be emptied in a rewording and nothing would
/// fail.
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
    /// What the intro says, and why it matters: white is 1.0, and everything left on the map
    /// CANNOT be vignetting. Whoever does not know this reads the map backwards.
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
    /// And the warning about the automatic scale: on it the colours always fill the whole
    /// range, so the intensity is NOT a measure. That is exactly the confusion the scale switch
    /// started from.
    /// </summary>
    [Fact]
    public void TheIntroWarnsThatColourIntensityDependsOnTheScale()
    {
        string intro = SessionMessages.MapGuideIntro();

        Assert.Contains("auto", intro);
        Assert.Contains("legenda", intro);
    }

    /// <summary>
    /// Without diacritics, like all the rest of the text the tool puts out - the console report
    /// and the saved file have none, and the window must not look like another program.
    /// </summary>
    [Fact]
    public void TheGuideUsesNoDiacritics()
    {
        string all = SessionMessages.MapGuideIntro() + string.Concat(
            SessionMessages.MapGuideShapes().Select(s => s.Shape + s.Meaning));

        Assert.All(all, c => Assert.True(c < 128, $"caracter non-ASCII: '{c}'"));
    }

    /// <summary>The view-model only carries the text, it does not rewrite it.</summary>
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
    /// The row about rings must NO longer say that rings mean an off-centre round obstacle.
    /// That was the reading of an artefact of the radial model, corrected since - and someone
    /// reading the map went looking for an object that had never existed.
    /// The test exists so that the old wording does not come back from the git history.
    /// </summary>
    [Fact]
    public void TheRingRowDoesNotClaimARingMeansADecentredObstruction()
    {
        var rings = SessionMessages.MapGuideShapes().Single(s => s.Shape.Contains("cercuri"));

        Assert.DoesNotContain("descentrat", rings.Shape);
        Assert.Contains("NU poate", rings.Meaning);       // a ring centred on the sensor
        Assert.Contains("artefact", rings.Meaning);       // and why they showed before
    }

    /// <summary>
    /// The row about the central spot must say that the structure mode is blind to it, not
    /// only the rotation test: measured, a 10% spot exactly at the centre leaves the map empty
    /// and every figure under 0.1%, because the radial model absorbs it whole. Before, the row
    /// described a shape that cannot appear on the map.
    /// </summary>
    [Fact]
    public void TheCentralBlobRowSaysTheStructureModeCannotSeeIt()
    {
        var blob = SessionMessages.MapGuideShapes().Single(s => s.Shape.Contains("centru"));

        Assert.Contains("NU poate aparea", blob.Meaning);
        Assert.Contains("absoarbe", blob.Meaning);
        Assert.Contains("vignetare", blob.Meaning);     // the only trace it leaves
    }

    /// <summary>
    /// "CE FEL" points to the figures that tell a gradient from something compact, and names
    /// the question the dust warning asks: session or library.
    /// </summary>
    [Fact]
    public void TheWhatKindRowPointsAtTheSpotFiguresAndTheDustQuestion()
    {
        var row = SessionMessages.HowToReadRows()[1];

        Assert.Contains("pata maxima", row.Meaning);
        Assert.Contains("pete locale", row.Meaning);
        Assert.Contains("biblioteca", row.Meaning);
    }

    // ------------------------------------------------- how to read the tab

    /// <summary>
    /// The two halves of the reading. The text must say explicitly which one decides -
    /// otherwise the figures on the left and the map on the right look like two views of the
    /// same thing, and that is exactly the confusion the panel resolves.
    /// </summary>
    [Fact]
    public void TheHowToReadNoteSeparatesHowMuchFromWhatKind()
    {
        var rows = SessionMessages.HowToReadRows();

        Assert.Equal(2, rows.Count);
        Assert.Contains("CAT DE MULT", rows[0].Shape);
        Assert.Contains("CE FEL", rows[1].Shape);
        Assert.Contains("cifrele", rows[0].Shape);
        Assert.Contains("NU depinde", rows[0].Meaning);   // the verdict does not depend on the map
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
