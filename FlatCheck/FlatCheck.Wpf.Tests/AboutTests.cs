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

// AboutTests.cs - the "Despre" window shows what the console shows, not a copy of it.
//
// All that can be checked here is that the view-model did not make its own texts: the
// version and the copyright are VersionInfo's, the warranty is RunReport's, the components
// are ThirdParty's, in the same order. If one of these links broke, the window could say
// something other than --help about the same licence.

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
    /// GPLv3 5(d): the warranty notice and the pointer to the licence. The same text as in
    /// --help, only joined into one paragraph - the console breaks it at ~70 columns.
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
    /// The window cannot lose a component the core declares - the LGPL requires LibRaw's
    /// notice exactly here. The same names, in the same order, with the same fields.
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

    /// <summary>
    /// The lead counts the linked components from the core's list, so it cannot stay behind
    /// the list: NLog made them four on 2026-10-27 and the window still said three (the review
    /// of 2026-10-27, A5). The database is named apart, as the README does.
    /// </summary>
    [Fact]
    public void TheLeadCountsTheLinkedComponentsFromTheCoresList()
    {
        Assert.Equal(4, ThirdParty.Components.Count(c => !c.IsData));

        using (Ui.Use(UiLanguage.English))
        {
            string lead = new AboutViewModel().ThirdPartyLead;
            Assert.Contains("links four components", lead);
            Assert.Contains("database", lead);
            Assert.DoesNotContain("three", lead);
        }
        using (Ui.Use(UiLanguage.Romanian))
            Assert.Contains("leaga patru componente", new AboutViewModel().ThirdPartyLead);
    }
}
