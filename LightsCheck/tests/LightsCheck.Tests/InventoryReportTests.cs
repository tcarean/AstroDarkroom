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
/// needed. The words are R-078's plain ones, `(when shot)` and the mount named as one. The total
/// exposure line that opens the session lines (R-086), on frames with the exposures of the real
/// nights.
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
            Assert.Equal("50 mm f/4.0 (when shot), 50 mm f/1.4 lens, \"EF50mm f/1.4 USM\" (ID 198, Canon EF mount)",
                LensLine(Frame("Canon EOS 7D Mark II", 50, 4, 198, "Canon EF", "EF50mm f/1.4 USM", 50, 50, 1.4142135)));
            // Pentax K-70: the ID pair as libraw reads it, no name, no range
            Assert.Equal("18 mm f/5.6 (when shot), ID 2263 (Pentax K mount)",
                LensLine(Frame("Pentax K-70", 18, 5.6, 2263, "Pentax K", null)));
            // Sony ILCE-7M3 by its name alone
            Assert.Equal("32 mm f/4.0 (when shot), 16-35 mm f/2.8 lens, \"FE 16-35mm F2.8 GM\" (Sony E mount)",
                LensLine(Frame("Sony ILCE-7M3", 32, 4, null, "Sony E", "FE 16-35mm F2.8 GM", 16, 35, 2.8)));
            // the Veil session's D3300 with the Samyang 135: what read "Nikon ID 74" until 2026-10-27
            Assert.Equal("135 mm f/2.8 (when shot), 135 mm f/2.0 lens, ID 74 (Nikon F mount)",
                LensLine(Frame("Nikon D3300", 135, 2.8, 74, "Nikon F", null, 135, 135, 2.0)));
            // a D3300 on a telescope: the mount alone says nothing about the lens
            Assert.Equal("135 mm f/2.8 (when shot)", LensLine(Frame("Nikon D3300", 135, 2.8, null, "Nikon F", null)));
            // a FITS: the focal length only
            Assert.Equal("135 mm (when shot)", LensLine(Frame("Nikon D3300", 135, null, null, null, null)));
            // a raw with an ID but no focal length is still the frame the line is read from
            Assert.Equal("ID 2263 (Pentax K mount)", LensLine(Frame("Pentax K-70", null, null, 2263, "Pentax K", null)));
            Assert.Equal("unknown", LensLine(Frame("Nikon D3300", null, null, null, null, null)));
        }
    }

    [Fact]
    public void R065_TheLensLineInRomanian_KeepsIdAndTheQuotedName()
    {
        using (Ui.Use(UiLanguage.Romanian))
        {
            Assert.Equal("50 mm f/4.0 (la fotografiere), obiectiv de 50 mm f/1.4, \"EF50mm f/1.4 USM\" (ID 198, montura Canon EF)",
                LensLine(Frame("Canon EOS 7D Mark II", 50, 4, 198, "Canon EF", "EF50mm f/1.4 USM", 50, 50, 1.4142135)));
            Assert.Equal("135 mm f/2.8 (la fotografiere), obiectiv de 135 mm f/2.0, ID 74 (montura Nikon F)",
                LensLine(Frame("Nikon D3300", 135, 2.8, 74, "Nikon F", null, 135, 135, 2.0)));
            Assert.Equal("135 mm (la fotografiere)", LensLine(Frame("Nikon D3300", 135, null, null, null, null)));
            Assert.Equal("necunoscut", LensLine(Frame("Nikon D3300", null, null, null, null, null)));
        }
    }

    /// <summary>
    /// Frames of the given exposures in seconds, numbered from 1 and shot one after the other
    /// from the fixture's first start with 30 s between them.
    /// </summary>
    private static List<FrameInfo> Shots(params double[] exposures)
    {
        List<FrameInfo> frames = [];
        DateTime t = SessionFixture.FirstStart;
        for (int i = 0; i < exposures.Length; i++)
        {
            frames.Add(Frame("Nikon D3300", 135, 2.8, 74, "Nikon F", null) with { Index = i + 1, ExposureSeconds = exposures[i], Timestamp = t });
            t = t.AddSeconds(exposures[i] + 30);
        }

        return frames;
    }

    /// <summary>The line right under the session's title line, in the header written for the frames.</summary>
    private static string FirstSessionLine(List<FrameInfo> frames)
    {
        using SessionFixture s = new SessionFixture().WithLights(1);
        var inv = new Inventory(SessionFolder.Open(s.Root), frames, CadenceAnalyzer.Analyze(frames));
        using var w = new StringWriter();
        InventoryReport.WriteHeader(inv, w);
        string[] lines = w.ToString().Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
        int title = Array.FindIndex(lines, l => l.StartsWith("=== ", StringComparison.Ordinal));
        return lines[title + 1];
    }

    [Fact]
    public void R086_TheTotalExposureOpensTheSessionLines_InHoursMinutesAndSeconds()
    {
        // The Veil night of 2026-10-06: 86 frames of 120.2 s and frame 75 of 133.2 s, 10470.4 s.
        List<FrameInfo> veil = Shots(Enumerable.Range(1, 87).Select(i => i == 75 ? 133.2 : 120.2).ToArray());
        // The Smida night of 2026-08-12: 31 frames of 119.2 s and one of 118.5 s, 3813.7 s.
        List<FrameInfo> smida = Shots(Enumerable.Range(1, 32).Select(i => i == 32 ? 118.5 : 119.2).ToArray());
        using (Ui.Use(UiLanguage.English))
        {
            Assert.Equal("    total exposure     : 2 h 54 min 30 s", FirstSessionLine(veil));
            Assert.Equal("    total exposure     : 1 h 3 min 34 s", FirstSessionLine(smida));
            Assert.Equal("    total exposure     : 45 min 12 s", FirstSessionLine(Shots(1356, 1356)));
            Assert.Equal("    total exposure     : 38 s", FirstSessionLine(Shots(30, 8)));
            // 3599.6 s rounds to the whole hour.
            Assert.Equal("    total exposure     : 1 h 0 min 0 s", FirstSessionLine(Shots(1800, 1799.6)));
        }

        using (Ui.Use(UiLanguage.Romanian))
        {
            Assert.Equal("    expunere totala    : 2 h 54 min 30 s", FirstSessionLine(veil));
            Assert.Equal("    expunere totala    : 45 min 12 s", FirstSessionLine(Shots(1356, 1356)));
        }
    }

    [Fact]
    public void R086_WithoutAFrameWithATimeAndAnExposure_TheTotalIsUnknown()
    {
        List<FrameInfo> untimed = [Shots(120)[0] with { Timestamp = null }, Shots(120, 120)[1] with { ExposureSeconds = null }];
        using (Ui.Use(UiLanguage.English))
        {
            Assert.Equal("    total exposure     : unknown (the frames carry no time or no exposure)", FirstSessionLine(untimed));
        }

        using (Ui.Use(UiLanguage.Romanian))
        {
            Assert.Equal("    expunere totala    : necunoscuta (cadrele nu au ora sau expunerea)", FirstSessionLine(untimed));
        }
    }
}
