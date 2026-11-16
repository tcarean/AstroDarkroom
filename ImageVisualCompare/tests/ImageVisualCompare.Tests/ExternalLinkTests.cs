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

using ImageVisualCompare.Core;
using ImageVisualCompare.Wpf;
using ImageVisualCompare.Wpf.Services;

namespace ImageVisualCompare.Tests;

/// <summary>
/// Which addresses <see cref="ExternalLink"/> hands the shell (R-072, R-066): a web address only,
/// since the shell resolves any other text against the working folder and the PATH. Nothing here
/// opens anything: only the test of the address is called.
/// </summary>
public class ExternalLinkTests
{
    [Theory]
    [InlineData("https://www.gnu.org/licenses/gpl-3.0.html", true)]
    [InlineData("http://nlog-project.org/", true)]
    [InlineData(@"C:\Windows\notepad.exe", false)]
    [InlineData("file:///C:/Windows/notepad.exe", false)]
    [InlineData("notepad", false)]
    [InlineData("LICENSE", false)]
    [InlineData("mailto:someone@example.org", false)]
    public void R072_OnlyAnAbsoluteHttpOrHttpsAddress_GoesToTheShell(string address, bool web)
    {
        Assert.Equal(web, ExternalLink.IsWebAddress(address, out _));
    }

    /// <summary>
    /// Every address the windows open, About's links and the manual's sections, goes to the shell
    /// as it is written, its anchor included: a guard that refused one would leave a link that
    /// silently opens nothing.
    /// </summary>
    [Fact]
    public void R072_EveryAddressTheWindowsOpen_GoesToTheShellUnchanged()
    {
        string[] addresses =
        [
            .. Enum.GetValues<ManualSection>().Select(Manual.Url),
            VersionInfo.LicenceUrl,
            .. ThirdParty.Components.Select(c => c.Url),
        ];

        Assert.Equal(6, addresses.Length);
        foreach (string address in addresses)
        {
            Assert.True(ExternalLink.IsWebAddress(address, out Uri? uri), address);
            Assert.Equal(address, uri.AbsoluteUri);
        }
    }
}
