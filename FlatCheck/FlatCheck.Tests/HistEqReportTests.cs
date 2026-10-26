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
/// The text of the histogram-equalisation mode. There is no verdict to check, so what
/// the tests pin is that the report says what was equalised and, above all, what the
/// picture cannot tell you.
/// </summary>
public class HistEqReportTests
{
    static string Written(HistEqResult r, string path = "flat.fit")
    {
        var w = new StringWriter();
        HistEqReport.Write(r, path, w);
        return w.ToString();
    }

    [Fact]
    public void OnACfaFrameWithAKnownOrder_NamesTheFourChannelsAndTheSource()
    {
        var r = HistEqAnalyzer.Analyze(TestImages.FlatCfa(64, 64, 1000), CfaPattern.Parse("RGGB", "BAYERPAT"));

        string text = Written(r, "raw.fit");

        Assert.Contains("fisier:     raw.fit", text);
        Assert.Contains("64 x 64, CFA (ordine RGGB, din BAYERPAT)", text);
        foreach (string label in new[] { "R ", "G1", "G2", "B " })
            Assert.Contains("\n  " + label, text);
        Assert.Contains("/ 256", text);
    }

    [Fact]
    public void OnACfaFrameWithoutAnOrder_SaysSoInsteadOfGuessing()
    {
        var r = HistEqAnalyzer.Analyze(TestImages.FlatCfa(64, 64, 1000), null);

        string text = Written(r);

        Assert.Contains("ordine necunoscuta", text);
        Assert.Contains("CFA[0,1]", text);
    }

    [Fact]
    public void OnAMonoFrame_PrintsOneChannel()
    {
        var r = HistEqAnalyzer.Analyze(TestImages.From(64, 64, FitsTestData.Radial(64, 64)), null);

        string text = Written(r);

        Assert.Contains("mono/procesat", text);
        Assert.Single(text.Split('\n').Where(l => l.StartsWith("  mono")));
    }

    [Fact]
    public void TheCaveatSaysThereIsNoVerdictAndWhereTheNumbersAre()
    {
        string c = HistEqReport.Caveat();

        Assert.Contains("nu da verdict", c);
        Assert.Contains("structura", c);
        Assert.Contains("INFORMATIV: " + c, Written(HistEqAnalyzer.Analyze(TestImages.FlatCfa(32, 32, 1000), null)));
    }

    /// <summary>
    /// The two screen notes, master and smoothed, are not confused; the smoothed one says
    /// what left and why the smoothed flat looks noisier, not cleaner.
    /// </summary>
    [Fact]
    public void TheSmoothedNoteSaysWhatWasRemovedAndWhyItLooksNoisier()
    {
        string s = HistEqReport.SmoothedVersusMaster();

        Assert.Contains("netezit", s);
        Assert.Contains("ZGOMOTOS", s);
        Assert.Contains("--ratio", s);
        Assert.NotEqual(HistEqReport.MasterVersusFrame(), s);
    }

    /// <summary>
    /// With false colours the display line says so and the legend follows the table's order:
    /// magenta first, red last. Without them neither appears.
    /// </summary>
    [Fact]
    public void R104_WithFalseColoursTheDisplayLineAndTheLegendSaySo()
    {
        var r = HistEqAnalyzer.Analyze(TestImages.FlatCfa(32, 32, 1000), null);
        var w = new StringWriter();

        HistEqReport.Write(r, "raw.fit", w, rainbow: true);
        string text = w.ToString();
        string legend = HistEqReport.RainbowLegend();

        Assert.Contains("culori false", HistEqReport.DisplayLine(r, rainbow: true));
        Assert.DoesNotContain("culori false", HistEqReport.DisplayLine(r, rainbow: false));
        Assert.Contains("culori false", text);
        Assert.Contains(legend, text);
        Assert.DoesNotContain(legend, Written(r));
        Assert.True(legend.IndexOf("magenta") < legend.IndexOf("rosu"), legend);
        // the RGB sentence is the window's alone: the console writes one layer and has no RGB view
        Assert.DoesNotContain("RGB", legend);
        Assert.DoesNotContain(HistEqReport.RainbowNotOnRgb(), text);
        Assert.Contains("RGB", HistEqReport.RainbowNotOnRgb());
        Assert.NotEqual(legend, HistEqReport.RainbowNotOnRgb());
        using (Ui.Use(UiLanguage.English))
        {
            string en = HistEqReport.RainbowLegend();
            Assert.True(en.IndexOf("magenta") < en.IndexOf("red"), en);
            Assert.Contains("false colours", HistEqReport.DisplayLine(r, rainbow: true));
            Assert.Contains("RGB", HistEqReport.RainbowNotOnRgb());
        }
    }

    [Fact]
    public void AnAllZeroChannelIsReportedAsSuch()
    {
        var r = HistEqAnalyzer.Analyze(TestImages.From(32, 32, (x, y) => 0.0), null);

        Assert.Contains("(numai zerouri)", Written(r));
    }
}
