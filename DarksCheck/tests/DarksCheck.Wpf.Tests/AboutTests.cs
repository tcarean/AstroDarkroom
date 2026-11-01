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

using DarksCheck.Core;

namespace DarksCheck.Wpf.Tests;

/// <summary>
/// The "Despre" window shows what the console shows, not a copy of it: the version and the
/// copyright are VersionInfo's, the warranty is RunReport's, the components are ThirdParty's,
/// in the same order. If one of these links broke, the window could say something else than
/// --help about the same licence.
/// </summary>
public class AboutTests
{
    /// <summary>How the lead begins in each language, its count the list's (R-120's example).</summary>
    private static readonly (UiLanguage Language, string Start)[] LeadStarts =
    [
        (UiLanguage.English, "The program uses six components written by others"),
        (UiLanguage.Romanian, "Programul foloseste sase componente scrise de altii"),
    ];

    [Fact]
    public void TheBannerAndCopyrightAreTheOnesEveryReportPrints()
    {
        AboutViewModel vm = new();

        Assert.Equal(VersionInfo.Banner, vm.Banner);
        Assert.Equal(VersionInfo.Copyright, vm.Copyright);
        Assert.Contains(VersionInfo.Value, vm.Banner, StringComparison.Ordinal);
    }

    /// <summary>GPLv3 5(d): the warranty notice and the pointer to the licence, the --help text joined into one paragraph.</summary>
    [Fact]
    public void TheWarrantyIsTheConsoleTextJoinedIntoAParagraph()
    {
        AboutViewModel vm = new();

        Assert.Equal(string.Join(" ", RunReport.WarrantyLines), vm.Warranty);
        Assert.Contains("GARANTIE", vm.Warranty, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(RunReport.LicenseUrl, vm.LicenseUrl);
        Assert.StartsWith("https://", vm.LicenseUrl, StringComparison.Ordinal);
    }

    /// <summary>The window cannot lose a component the core declares: the LGPL asks for the LibRaw notice exactly here.</summary>
    [Fact]
    public void TheComponentsAreTheOnesTheCoreDeclares()
    {
        AboutViewModel vm = new();

        Assert.Equal(ThirdParty.Components.Select(c => c.Name), vm.Components.Select(r => r.Name));
        Assert.Equal(ThirdParty.Components.Select(c => c.Licence), vm.Components.Select(r => r.Licence));
        Assert.Equal(ThirdParty.Components.Select(c => c.Copyright), vm.Components.Select(r => r.Copyright));
        Assert.Equal(ThirdParty.Components.Select(c => c.Url), vm.Components.Select(r => r.Url));
        Assert.All(vm.Components, r => Assert.False(string.IsNullOrWhiteSpace(r.Role)));
        Assert.NotEmpty(vm.Components);
    }

    [Fact]
    public void EveryHeadingAndLeadIsWritten()
    {
        AboutViewModel vm = new();

        Assert.False(string.IsNullOrWhiteSpace(vm.Title));
        Assert.False(string.IsNullOrWhiteSpace(vm.Tagline));
        Assert.False(string.IsNullOrWhiteSpace(vm.LicenceHeading));
        Assert.False(string.IsNullOrWhiteSpace(vm.LicenseLinkLabel));
        Assert.False(string.IsNullOrWhiteSpace(vm.ThirdPartyHeading));
        Assert.Contains("THIRD-PARTY-NOTICES.md", vm.ThirdPartyLead, StringComparison.Ordinal);
    }

    /// <summary>
    /// The lead counts the components in words, from the list it stands above, so the count
    /// cannot drift from the rows again (it said "two" of three), and it says where their
    /// licence texts are, in the suite's folder and in the source repository (R-120).
    /// </summary>
    [Fact]
    public void R120_TheLeadCountsTheListInWordsAndSaysWhereTheLicenceTextsAre_InBothLanguages()
    {
        foreach ((UiLanguage language, string count) in LeadStarts)
        {
            using IDisposable _ = Ui.Use(language);
            AboutViewModel vm = new();

            Assert.Equal(6, vm.Components.Count);
            Assert.StartsWith(count, vm.ThirdPartyLead, StringComparison.Ordinal);
            Assert.Contains("THIRD-PARTY-NOTICES.txt", vm.ThirdPartyLead, StringComparison.Ordinal);
            Assert.Contains("Astro Darkroom", vm.ThirdPartyLead, StringComparison.Ordinal);
            Assert.Contains("AstroDarkroom/THIRD-PARTY-NOTICES.md", vm.ThirdPartyLead, StringComparison.Ordinal);
            Assert.Contains("https://github.com/tcarean/AstroDarkroom", vm.ThirdPartyLead, StringComparison.Ordinal);
            Assert.All(vm.ThirdPartyLead, c => Assert.True(c < 128, $"'{c}' is not ASCII"));
        }
    }

    /// <summary>The count's word: the words up to ten in each language, the digits past them.</summary>
    [Fact]
    public void R120_ACountIsAWordUpToTenAndDigitsPastIt()
    {
        using (Ui.Use(UiLanguage.English))
        {
            Assert.Equal(["one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "11"], Enumerable.Range(1, 11).Select(WindowTexts.CountWord));
        }

        using (Ui.Use(UiLanguage.Romanian))
        {
            Assert.Equal(["una", "doua", "trei", "patru", "cinci", "sase", "sapte", "opt", "noua", "zece", "11"], Enumerable.Range(1, 11).Select(WindowTexts.CountWord));
        }
    }
}
