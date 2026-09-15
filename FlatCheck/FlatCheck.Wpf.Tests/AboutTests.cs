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

// AboutTests.cs - fereastra "Despre" arata ce arata consola, nu o copie a ei.
//
// Tot ce e verificabil aici e ca ViewModel-ul nu si-a facut propriile texte: versiunea
// si copyright-ul sunt ale lui VersionInfo, garantia e cea din RunReport, componentele
// sunt cele din ThirdParty, in aceeasi ordine. Daca una din legaturile astea se rupe,
// fereastra ar putea spune altceva decat --help despre aceeasi licenta.

namespace FlatCheck.Wpf.Tests;

public class AboutTests
{
    [Fact]
    public void TheBannerAndCopyrightAreTheOnesEveryReportPrints()
    {
        var vm = new AboutViewModel();

        Assert.Equal(VersionInfo.Banner, vm.Banner);
        Assert.Equal(VersionInfo.Copyright, vm.Copyright);
        Assert.Contains(VersionInfo.Value, vm.Banner);
    }

    /// <summary>
    /// GPLv3 5(d): notita de garantie si trimiterea la licenta. Acelasi text ca la --help,
    /// doar lipit intr-un paragraf - consola il rupe la ~70 de coloane.
    /// </summary>
    [Fact]
    public void TheWarrantyIsTheConsoleTextJoinedIntoAParagraph()
    {
        var vm = new AboutViewModel();

        Assert.Equal(string.Join(" ", RunReport.WarrantyLines), vm.Warranty);
        Assert.Contains("FARA NICIO GARANTIE", vm.Warranty);
        Assert.Contains("GPLv3", vm.Warranty);
        Assert.Equal(RunReport.LicenseUrl, vm.LicenseUrl);
        Assert.StartsWith("https://", vm.LicenseUrl);
    }

    /// <summary>
    /// Fereastra nu poate pierde o componenta pe care nucleul o declara - LGPL-ul cere
    /// notita LibRaw exact aici. Aceleasi nume, in aceeasi ordine, cu aceleasi campuri.
    /// </summary>
    [Fact]
    public void TheComponentsAreTheOnesTheCoreDeclares()
    {
        var vm = new AboutViewModel();

        Assert.Equal(ThirdParty.Components.Select(c => c.Name), vm.Components.Select(r => r.Name));
        Assert.Equal(ThirdParty.Components.Select(c => c.Licence), vm.Components.Select(r => r.Licence));
        Assert.Equal(ThirdParty.Components.Select(c => c.Copyright), vm.Components.Select(r => r.Copyright));
        Assert.Equal(ThirdParty.Components.Select(c => c.Url), vm.Components.Select(r => r.Url));
        Assert.All(vm.Components, r => Assert.False(string.IsNullOrWhiteSpace(r.Role)));
    }

    [Fact]
    public void EveryHeadingAndLeadIsWritten()
    {
        var vm = new AboutViewModel();

        Assert.False(string.IsNullOrWhiteSpace(vm.Title));
        Assert.False(string.IsNullOrWhiteSpace(vm.Tagline));
        Assert.False(string.IsNullOrWhiteSpace(vm.LicenceHeading));
        Assert.False(string.IsNullOrWhiteSpace(vm.LicenseLinkLabel));
        Assert.False(string.IsNullOrWhiteSpace(vm.ThirdPartyHeading));
        Assert.Contains("THIRD-PARTY-NOTICES.md", vm.ThirdPartyLead);
    }
}
