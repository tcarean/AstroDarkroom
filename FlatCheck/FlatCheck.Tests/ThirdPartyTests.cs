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

// ThirdPartyTests.cs - lista componentelor terte nu poate ramane in urma csproj-ului.
//
// Lista din ThirdParty.Components e ce arata fereastra "Despre", si LGPL-2.1 sectiunea 6
// cere ca notita LibRaw sa apara acolo unde programul isi arata propriul copyright. Un
// pachet adaugat in csproj fara o intrare aici ar fi un pachet livrat si neafisat; un
// pachet scos din csproj cu intrarea ramasa ar afisa o licenta pe care n-o mai folosim.
// Testele leaga cele doua in ambele sensuri, si fiecare intrare de fisierul de notite.

using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace FlatCheck.Tests;

public class ThirdPartyTests
{
    static string RepoDir([CallerFilePath] string thisFile = "")
        => Path.GetDirectoryName(Path.GetDirectoryName(thisFile))!;

    /// <summary>Pachetele NuGet pe care le leaga nucleul, citite din csproj-ul lui.</summary>
    static string[] CorePackages()
    {
        string csproj = File.ReadAllText(Path.Combine(RepoDir(), "FlatCheck.Core", "FlatCheck.Core.csproj"));
        return Regex.Matches(csproj, @"<PackageReference\s+Include=""([^""]+)""")
            .Select(m => m.Groups[1].Value)
            .ToArray();
    }

    /// <summary>
    /// Fiecare pachet din csproj e revendicat de exact o componenta, si fiecare pachet
    /// revendicat exista in csproj. Acesta e testul care pica la urmatoarea dependinta
    /// adaugata fara intrare - inainte sa ajunga pe ecran fara notita.
    /// </summary>
    [Fact]
    public void EveryPackageTheCoreLinksIsListed()
    {
        string[] linked = CorePackages();
        Assert.NotEmpty(linked);

        string[] claimed = ThirdParty.Components.SelectMany(c => c.Packages).ToArray();

        foreach (string p in linked)
            Assert.True(claimed.Count(c => c == p) == 1,
                $"pachetul {p} din FlatCheck.Core.csproj nu e revendicat de exact o componenta din ThirdParty.Components");

        foreach (string p in claimed)
            Assert.True(linked.Contains(p),
                $"ThirdParty.Components revendica pachetul {p}, care nu mai e in FlatCheck.Core.csproj");
    }

    /// <summary>
    /// Motivele de compatibilitate cu GPLv3 stau in THIRD-PARTY-NOTICES.md, deci fiecare
    /// componenta afisata trebuie sa aiba un heading acolo. Se cauta numele in headinguri,
    /// nu oriunde in text - "LibRaw" apare si in paragraful despre Sdcb.LibRaw.
    /// </summary>
    [Fact]
    public void EveryComponentHasAnEntryInTheNoticesFile()
    {
        string notices = File.ReadAllText(Path.Combine(RepoDir(), "THIRD-PARTY-NOTICES.md"));
        string[] headings = Regex.Matches(notices, @"^##\s+(.+)$", RegexOptions.Multiline)
            .Select(m => m.Groups[1].Value)
            .ToArray();

        foreach (var c in ThirdParty.Components)
            Assert.True(headings.Any(h => h.Contains(c.Name)),
                $"THIRD-PARTY-NOTICES.md nu are un heading pentru {c.Name}");
    }

    /// <summary>Ce arata ecranul nu poate fi gol: nume, rol, licenta, copyright, si un link https.</summary>
    [Fact]
    public void EveryComponentNamesItsLicenceAndUrl()
    {
        Assert.Equal(4, ThirdParty.Components.Count);

        foreach (var c in ThirdParty.Components)
        {
            Assert.False(string.IsNullOrWhiteSpace(c.Name));
            Assert.False(string.IsNullOrWhiteSpace(c.Role));
            Assert.False(string.IsNullOrWhiteSpace(c.Licence));
            Assert.False(string.IsNullOrWhiteSpace(c.Copyright));
            Assert.StartsWith("https://", c.Url);
            // O componenta de cod vine printr-un pachet; una de date (lensfun) vine ca fisiere
            // si nu revendica niciun pachet - dar e singura care are voie sa nu revendice.
            if (!c.IsData) Assert.NotEmpty(c.Packages);
        }
        Assert.Single(ThirdParty.Components, c => c.IsData);
    }

    /// <summary>
    /// CC BY-SA cere atribuirea: baza lensfun e livrata ca fisiere langa exe, deci notita
    /// ei trebuie sa apara in lista pe care programul o afiseaza, cu licenta si sursa, iar
    /// folderul din repo trebuie sa poarte licenta si versiunea copiei.
    /// </summary>
    [Fact]
    public void TheLensfunDatabaseIsAttributedAndItsFolderCarriesLicenceAndVersion()
    {
        var lf = Assert.Single(ThirdParty.Components, c => c.Name.StartsWith("lensfun"));
        Assert.True(lf.IsData);
        Assert.Contains("CC BY-SA", lf.Licence);
        Assert.Contains("github.com/lensfun", lf.Url);

        string folder = Path.Combine(RepoDir(), ProfileLocator.LensfunFolderName);
        Assert.True(File.Exists(Path.Combine(folder, "LICENSE.txt")), "lensfun/LICENSE.txt lipseste");
        string version = File.ReadAllText(Path.Combine(folder, ProfileLocator.LensfunVersionFile));
        Assert.Contains("upstream commit:", version);
        Assert.Contains("CC BY-SA", version + File.ReadAllText(Path.Combine(folder, "LICENSE.txt")));
        Assert.NotEmpty(Directory.EnumerateFiles(folder, "*.xml"));
    }

    /// <summary>
    /// LGPL-2.1 sectiunea 6 e motivul pentru care lista se afiseaza: notita LibRaw
    /// trebuie sa apara printre notitele de copyright ale programului, si sa se stie sub
    /// care dintre cele doua licente s-a luat.
    /// </summary>
    [Fact]
    public void LibRawIsTakenUnderTheLgplAndCarriesItsCopyright()
    {
        var libraw = Assert.Single(ThirdParty.Components, c => c.Name == "LibRaw");

        Assert.Contains("LGPL-2.1", libraw.Licence);
        Assert.Contains("LibRaw LLC", libraw.Copyright);
    }

    /// <summary>
    /// --help tipareste notita de garantie din RunReport, linie cu linie. Golden/help.txt
    /// pastreaza textul; testul asta spune direct ce s-a rupt daca cineva desface bucla.
    /// </summary>
    [Fact]
    public void HelpPrintsTheSharedWarrantyLines()
    {
        var sw = new StringWriter();
        FlatCheckCli.Run(new[] { "--help" }, sw, sw);
        string[] lines = sw.ToString().Replace("\r\n", "\n").Split('\n');

        foreach (string l in RunReport.WarrantyLines)
            Assert.Contains(l, lines);

        Assert.Contains(RunReport.LicenseUrl, sw.ToString());
    }
}
