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

using LightsCheck.Core;

namespace LightsCheck.Tests;

/// <summary>
/// The inventory's lens line (R-065) on frames fabricated with the values libraw gave FlatCheck
/// for each make on 2026-10-26: the record is what the readers would have filled, so no raw is
/// needed. The FITS wording the goldens pin (`135 mm (as shot)`) must not move.
/// </summary>
public class InventoryReportTests
{
    private static FrameInfo Frame(string camera, double? focal, double? fNumber, long? lensId, string? mount, string? name,
                                   double? min = null, double? max = null, double? aperture = null)
        => new(1, "lights/x.raw", camera, focal, fNumber, 120.2, 1600, SessionFixture.FirstStart, null, null, null, null, lensId, mount, name, min, max, aperture, null);

    /// <summary>The text after "lens :" (or "obiectiv :") in the header written for one frame.</summary>
    private static string LensLine(FrameInfo frame)
    {
        using SessionFixture s = new SessionFixture().WithLights(1);
        var inv = new Inventory(SessionFolder.Open(s.Root), [frame], CadenceAnalyzer.Analyze([frame]));
        using var w = new StringWriter();
        InventoryReport.WriteHeader(inv, w);
        string line = w.ToString().Split('\n').Select(l => l.TrimEnd('\r'))
            .Single(l => l.StartsWith("    lens ", StringComparison.Ordinal) || l.StartsWith("    obiectiv ", StringComparison.Ordinal));
        return line[(line.IndexOf(':', StringComparison.Ordinal) + 2)..];
    }

    [Fact]
    public void R065_TheLensLineNamesTheLensAsTheRawGaveIt_WithItsIdAndMount()
    {
        using (Ui.Use(UiLanguage.English))
        {
            // Canon EOS 7D Mark II: the name, Canon's lens type and the mount; the chip's range too
            Assert.Equal("50 mm f/4.0 (as shot), lens 50 mm f/1.4, \"EF50mm f/1.4 USM\" (ID 198, Canon EF)",
                LensLine(Frame("Canon EOS 7D Mark II", 50, 4, 198, "Canon EF", "EF50mm f/1.4 USM", 50, 50, 1.4142135)));
            // Pentax K-70: the ID pair as libraw reads it, no name, no range
            Assert.Equal("18 mm f/5.6 (as shot), ID 2263 (Pentax K)",
                LensLine(Frame("Pentax K-70", 18, 5.6, 2263, "Pentax K", null)));
            // Sony ILCE-7M3 by its name alone
            Assert.Equal("32 mm f/4.0 (as shot), lens 16-35 mm f/2.8, \"FE 16-35mm F2.8 GM\" (Sony E)",
                LensLine(Frame("Sony ILCE-7M3", 32, 4, null, "Sony E", "FE 16-35mm F2.8 GM", 16, 35, 2.8)));
            // the Veil session's D3300 with the Samyang 135: what read "Nikon ID 74" until 2026-10-27
            Assert.Equal("135 mm f/2.8 (as shot), lens 135 mm f/2.0, ID 74 (Nikon F)",
                LensLine(Frame("Nikon D3300", 135, 2.8, 74, "Nikon F", null, 135, 135, 2.0)));
            // a D3300 on a telescope: the mount alone says nothing about the lens
            Assert.Equal("135 mm f/2.8 (as shot)", LensLine(Frame("Nikon D3300", 135, 2.8, null, "Nikon F", null)));
            // a FITS: the focal length only, the wording the goldens pin
            Assert.Equal("135 mm (as shot)", LensLine(Frame("Nikon D3300", 135, null, null, null, null)));
            // a raw with an ID but no focal length is still the frame the line is read from
            Assert.Equal("ID 2263 (Pentax K)", LensLine(Frame("Pentax K-70", null, null, 2263, "Pentax K", null)));
            Assert.Equal("unknown", LensLine(Frame("Nikon D3300", null, null, null, null, null)));
        }
    }

    [Fact]
    public void R065_TheLensLineInRomanian_KeepsIdAndTheQuotedName()
    {
        using (Ui.Use(UiLanguage.Romanian))
        {
            Assert.Equal("50 mm f/4.0 (poza), obiectiv 50 mm f/1.4, \"EF50mm f/1.4 USM\" (ID 198, Canon EF)",
                LensLine(Frame("Canon EOS 7D Mark II", 50, 4, 198, "Canon EF", "EF50mm f/1.4 USM", 50, 50, 1.4142135)));
            Assert.Equal("135 mm f/2.8 (poza), obiectiv 135 mm f/2.0, ID 74 (Nikon F)",
                LensLine(Frame("Nikon D3300", 135, 2.8, 74, "Nikon F", null, 135, 135, 2.0)));
            Assert.Equal("135 mm (poza)", LensLine(Frame("Nikon D3300", 135, null, null, null, null)));
            Assert.Equal("necunoscut", LensLine(Frame("Nikon D3300", null, null, null, null, null)));
        }
    }
}
