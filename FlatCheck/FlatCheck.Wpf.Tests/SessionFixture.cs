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

// SessionFixture.cs - un folder de sesiune Siril fals, dar complet.
//
// Cadrele sunt mici (128 px) dinadins: testele de ViewModel ruleaza lantul intreg, nu
// mock-uri, si intreaga suita trebuie sa stea sub cateva secunde. Cifrele care ies nu
// sunt "realiste" si nici nu trebuie sa fie - ce se verifica aici e comportamentul
// interfetei, iar corectitudinea cifrelor e treaba testelor din FlatCheck.Tests.

using FlatCheck.Tests;

namespace FlatCheck.Wpf.Tests;

internal sealed class SessionFixture : IDisposable
{
    const int Size = 128;

    readonly TempWorkspace _ws = new();

    internal string Root => _ws.Dir;
    internal SirilSession Session => new(_ws.Dir);

    internal string ProcessDir => Path.Combine(Root, "process");
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

    /// <summary>Masterul de flat, prin conventia scriptului OSC_Preprocessing.ssf.</summary>
    internal SessionFixture WithFlatMaster(Func<int, int, double>? scene = null)
    {
        WithProcess();
        Write(Path.Combine(ProcessDir, SirilSession.FlatMasterName),
              scene ?? FitsTestData.Radial(Size, Size));
        return this;
    }

    internal SessionFixture WithFlatMasters(params string[] names)
    {
        WithProcess();
        for (int i = 0; i < names.Length; i++)
            Write(Path.Combine(ProcessDir, names[i]),
                  FitsTestData.Radial(Size, Size, k: 0.45 + 0.01 * i));
        return this;
    }

    /// <summary>Calea unuia dintre masterele scrise de <see cref="WithFlatMasters"/>.</summary>
    internal string MasterPath(string name) => Path.Combine(ProcessDir, name);

    /// <summary>O serie de lights calibrate, cu vignetare reziduala usor diferita.</summary>
    internal SessionFixture WithLightSeries(int count, IEnumerable<string>? cards = null)
    {
        WithProcess();
        for (int i = 0; i < count; i++)
            Write(Path.Combine(ProcessDir, $"pp_light_{i:D5}.fit"),
                  FitsTestData.Radial(Size, Size, k: 0.44 + 0.002 * i), cards);
        return this;
    }

    /// <summary>
    /// Un flat brut. Nu e un raw adevarat - FITS-ul cu mozaic Bayer pastreaza exact ce
    /// masoara modul, adica cele patru pozitii CFA la scala ADU originala - dar poarta o
    /// extensie raw ca sa apara in lista, si asta ar face-o pe cea reala sa esueze.
    /// Deci testele care ating fisierele astea verifica LISTA, nu analiza.
    /// </summary>
    internal SessionFixture WithRawFlats(params string[] names)
    {
        WithFlats();
        foreach (string n in names) File.WriteAllText(Path.Combine(FlatsDir, n), "nu conteaza");
        return this;
    }

    /// <summary>Un flat brut analizabil: FITS cu mozaic CFA, la scala ADU originala.</summary>
    internal string WriteAnalysableRawFlat(string name, double centre = 34000)
    {
        WithFlats();
        string path = Path.Combine(FlatsDir, name);
        Write(path, FitsTestData.WithBayerMosaic(FitsTestData.Radial(Size, Size, center: centre)));
        return path;
    }

    static void Write(string path, Func<int, int, double> scene, IEnumerable<string>? cards = null)
        => File.WriteAllBytes(path, FitsTestData.BuildFits(Size, Size, scene, extraCards: cards));

    /// <summary>Carduri care fac campul prea larg pentru testul end-to-end.</summary>
    internal static IEnumerable<string> WideFieldCards => FitsTestData.WcsCards(0.4);

    public void Dispose() => _ws.Dispose();
}
