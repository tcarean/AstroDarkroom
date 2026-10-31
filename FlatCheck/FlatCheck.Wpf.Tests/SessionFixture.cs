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

// SessionFixture.cs - a fake Siril session folder, but a complete one.
//
// The frames are small (128 px) on purpose: the view-model tests run the whole chain, not
// mocks, and the whole suite must stay quick (it runs serially, about 100 s on 2026-10-25).
// The figures that come out are not "realistic" and need not be - what is checked here is
// the interface's behaviour, and the figures' correctness is the job of the tests in
// FlatCheck.Tests.

using FlatCheck.Tests;

namespace FlatCheck.Wpf.Tests;

internal sealed class SessionFixture : IDisposable
{
    const int Size = 128;

    readonly TempWorkspace _ws = new();

    internal string Root => _ws.Dir;
    internal SirilSession Session => new(_ws.Dir);

    internal string ProcessDir => Path.Combine(Root, "process");
    internal string MastersDir => Path.Combine(Root, "masters");
    internal string FlatsDir => Path.Combine(Root, "flats");

    internal SessionFixture WithProcess()
    {
        Directory.CreateDirectory(ProcessDir);
        return this;
    }

    internal SessionFixture WithFlats()
    {
        Directory.CreateDirectory(FlatsDir);
        return this;
    }

    /// <summary>
    /// The flat master, by the Siril scripts' convention: in process/ (the scripts from the
    /// repository, WithoutDark and its siblings) or, with <paramref name="folder"/> = "masters",
    /// where the scripts of the 1.4 install leave it. 16-bit by default;
    /// <paramref name="bitpix"/> -32 gives what Siril actually writes - float, with the values
    /// divided by 65535 - for the tests that look at the file's scale.
    /// </summary>
    internal SessionFixture WithFlatMaster(Func<int, int, double>? scene = null, int bitpix = 16,
                                           string folder = "process")
    {
        string dir = Path.Combine(Root, folder);
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, SirilSession.FlatMasterName),
            FitsTestData.BuildFits(Size, Size, scene ?? FitsTestData.Radial(Size, Size), bitpix: bitpix));
        return this;
    }

    /// <summary>
    /// The master in masters/, as OSC_Preprocessing.ssf from Siril 1.4 leaves it.
    /// </summary>
    internal SessionFixture WithFlatMasterInMasters(Func<int, int, double>? scene = null)
        => WithFlatMaster(scene, folder: "masters");

    internal SessionFixture WithFlatMasters(params string[] names)
    {
        WithProcess();
        for (int i = 0; i < names.Length; i++)
            Write(Path.Combine(ProcessDir, names[i]),
                  FitsTestData.Radial(Size, Size, k: 0.45 + 0.01 * i));
        return this;
    }

    /// <summary>The path of one of the masters written by <see cref="WithFlatMasters"/>.</summary>
    internal string MasterPath(string name) => Path.Combine(ProcessDir, name);

    /// <summary>
    /// A series of calibrated lights, with slightly different residual vignetting.
    /// </summary>
    internal SessionFixture WithLightSeries(int count, IEnumerable<string>? cards = null)
    {
        WithProcess();
        for (int i = 0; i < count; i++)
            Write(Path.Combine(ProcessDir, $"pp_light_{i:D5}.fit"),
                  FitsTestData.Radial(Size, Size, k: 0.44 + 0.002 * i), cards);
        return this;
    }

    /// <summary>
    /// A raw flat. It is not a real raw - the FITS with a Bayer mosaic keeps exactly what the
    /// mode measures, that is the four CFA positions at the original ADU scale - but it carries
    /// a raw extension so it appears in the list, and that would make the real analysis fail.
    /// So the tests that touch these files check the LIST, not the analysis.
    /// </summary>
    internal SessionFixture WithRawFlats(params string[] names)
    {
        WithFlats();
        foreach (string n in names) File.WriteAllText(Path.Combine(FlatsDir, n), "nu conteaza");
        return this;
    }

    /// <summary>
    /// An analysable raw flat: a FITS with a CFA mosaic, at the original ADU scale.
    ///
    /// With <paramref name="bayerPat"/> it also carries the colour order, as Siril writes it on
    /// a frame passed through Conversion - then the channels are named R/G1/G2/B instead of
    /// positions.
    /// </summary>
    internal string WriteAnalysableRawFlat(string name, double centre = 34000, string? bayerPat = null)
    {
        WithFlats();
        string path = Path.Combine(FlatsDir, name);
        Write(path, FitsTestData.WithBayerMosaic(FitsTestData.Radial(Size, Size, center: centre)),
              bayerPat == null ? null : FitsTestData.BayerCards(bayerPat));
        return path;
    }

    static void Write(string path, Func<int, int, double> scene, IEnumerable<string>? cards = null)
        => File.WriteAllBytes(path, FitsTestData.BuildFits(Size, Size, scene, extraCards: cards));

    /// <summary>Cards that make the field too wide for the end-to-end test.</summary>
    internal static IEnumerable<string> WideFieldCards => FitsTestData.WcsCards(0.4);

    public void Dispose() => _ws.Dispose();
}
