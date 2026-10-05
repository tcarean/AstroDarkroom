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
// Lista din ThirdParty.Components e ce arata fereastra "Despre". Un pachet adaugat in
// csproj fara o intrare aici ar fi un pachet livrat si neafisat; un pachet scos din
// csproj cu intrarea ramasa ar afisa o licenta pe care n-o mai folosim. Testele leaga
// cele doua in ambele sensuri, si fiecare intrare de fisierul de notite.

using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace AstroPhotoAnalyzer.Tests;

public class ThirdPartyTests
{
    static string RepoDir([CallerFilePath] string thisFile = "")
        => Path.GetDirectoryName(Path.GetDirectoryName(thisFile))!;

    static string CoreCsproj => File.ReadAllText(Path.Combine(RepoDir(), "AstroPhotoAnalyzer.Core", "AstroPhotoAnalyzer.Core.csproj"));

    /// <summary>Pachetele NuGet pe care le leaga nucleul, citite din csproj-ul lui.</summary>
    static string[] CorePackages()
        => Regex.Matches(CoreCsproj, @"<PackageReference\s+Include=""([^""]+)""")
            .Select(m => m.Groups[1].Value)
            .ToArray();

    /// <summary>Resursele incorporate ale nucleului (datele imbarcate), dupa LogicalName.</summary>
    static string[] CoreResources()
        => Regex.Matches(CoreCsproj, @"<EmbeddedResource\s+Include=""[^""]+""\s+LogicalName=""([^""]+)""")
            .Select(m => m.Groups[1].Value)
            .ToArray();

    /// <summary>
    /// Fiecare pachet din csproj e revendicat de exact o componenta, si fiecare pachet
    /// revendicat exista in csproj. Acesta e testul care pica la urmatoarea dependinta
    /// adaugata fara intrare - inainte sa ajunga pe ecran fara notita.
    /// </summary>
    [Fact]
    public void R122_EveryPackageTheCoreLinksIsListed()
    {
        string[] linked = CorePackages();
        Assert.NotEmpty(linked);

        string[] claimed = ThirdParty.Components.SelectMany(c => c.Packages).ToArray();

        foreach (string p in linked)
            Assert.True(claimed.Count(c => c == p) == 1,
                $"pachetul {p} din AstroPhotoAnalyzer.Core.csproj nu e revendicat de exact o componenta din ThirdParty.Components");

        foreach (string p in claimed)
            Assert.True(linked.Contains(p),
                $"ThirdParty.Components revendica pachetul {p}, care nu mai e in AstroPhotoAnalyzer.Core.csproj");
    }

    /// <summary>
    /// Datele imbarcate (catalogul LDN, ADR 0025) sunt scrise de altii la fel ca un pachet:
    /// fiecare resursa din csproj e revendicata de exact o componenta, si invers.
    /// </summary>
    [Fact]
    public void R122_EveryEmbeddedDataSetIsListed()
    {
        string[] embedded = CoreResources();
        Assert.Contains(DarkNebulaCatalogue.ResourceName, embedded);

        string[] claimed = ThirdParty.Components.SelectMany(c => c.Resources).ToArray();

        foreach (string r in embedded)
            Assert.True(claimed.Count(c => c == r) == 1,
                $"resursa {r} din AstroPhotoAnalyzer.Core.csproj nu e revendicata de exact o componenta din ThirdParty.Components");

        foreach (string r in claimed)
            Assert.True(embedded.Contains(r),
                $"ThirdParty.Components revendica resursa {r}, care nu e in AstroPhotoAnalyzer.Core.csproj");

        var ldn = Assert.Single(ThirdParty.Components, c => c.Resources.Contains(DarkNebulaCatalogue.ResourceName));
        Assert.Contains("Lynds", ldn.Name);
        Assert.Contains("1962", ldn.Licence);
        Assert.Contains("VII/7A", ldn.Url);
        Assert.Empty(ldn.Packages);
    }

    /// <summary>
    /// Motivele de compatibilitate cu GPLv3 stau in THIRD-PARTY-NOTICES.md, deci fiecare
    /// componenta afisata trebuie sa aiba un heading acolo.
    /// </summary>
    [Fact]
    public void R122_EveryComponentHasAnEntryInTheNoticesFile()
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
    public void R122_EveryComponentNamesItsLicenceAndUrl()
    {
        Assert.Equal(3, ThirdParty.Components.Count);   // ImageSharp, Unicolour, the Lynds catalogue

        foreach (var c in ThirdParty.Components)
        {
            Assert.False(string.IsNullOrWhiteSpace(c.Name));
            Assert.False(string.IsNullOrWhiteSpace(c.Role));
            Assert.False(string.IsNullOrWhiteSpace(c.Licence));
            Assert.False(string.IsNullOrWhiteSpace(c.Copyright));
            Assert.StartsWith("https://", c.Url);
            Assert.True(c.Packages.Length + c.Resources.Length > 0, c.Name + " nu aduce nici pachet, nici resursa");
        }
    }

    /// <summary>
    /// Licenta ImageSharp e conditionata: termenii Apache 2.0 se aplica software-ului open
    /// source, si unealta e GPLv3. Intrarea trebuie sa spuna sub ce termeni e luata, ca
    /// urmatorul cititor sa nu presupuna ca e o licenta permisiva simpla.
    /// </summary>
    [Fact]
    public void R122_ImageSharpIsTakenUnderTheOpenSourceTermsAndCarriesItsCopyright()
    {
        var sharp = Assert.Single(ThirdParty.Components, c => c.Name == "SixLabors.ImageSharp");

        Assert.Contains("Apache 2.0", sharp.Licence);
        Assert.Contains("Six Labors", sharp.Copyright);
    }

    /// <summary>
    /// --help tipareste notita de garantie din RunReport, linie cu linie. Testul asta
    /// spune direct ce s-a rupt daca cineva desface bucla.
    /// </summary>
    [Fact]
    public void R004_HelpPrintsTheSharedWarrantyLines()
    {
        var sw = new StringWriter();
        AnalyzerCli.Run(new[] { "--help" }, sw, sw);
        string[] lines = sw.ToString().Replace("\r\n", "\n").Split('\n');

        foreach (string l in RunReport.WarrantyLines)
            Assert.Contains(l, lines);

        Assert.Contains(RunReport.LicenseUrl, sw.ToString());
    }
}
