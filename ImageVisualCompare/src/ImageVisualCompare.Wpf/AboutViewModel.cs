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

// AboutViewModel.cs - what the About window shows, after FlatCheck's (ADR 0045).
//
// It words nothing: the name, the version, the copyright and the licence page are VersionInfo's,
// every sentence is UiText's in the interface's language, the components are ThirdParty's. All
// that happens here is the passage from those internal types to public properties the XAML can
// bind, read once, when the window is built; they are instance properties because {Binding}
// resolves through the DataContext and does not see a static one.

using ImageVisualCompare.Core;

namespace ImageVisualCompare.Wpf;

/// <summary>A component written by others, as a row of the About window's card (R-075).</summary>
/// <param name="Name">The component's name, as its authors spell it.</param>
/// <param name="Role">What it does in the program, in the interface's language.</param>
/// <param name="Licence">The licence it is taken under, as its SPDX identifier.</param>
/// <param name="Copyright">Its copyright line.</param>
/// <param name="Url">The address of its project, which the row links.</param>
public sealed record ComponentRow(string Name, string Role, string Licence, string Copyright, string Url);

/// <summary>The About window's content, as public properties for its bindings (R-072, R-075).</summary>
public sealed class AboutViewModel
{
    /// <summary>Reads the texts in the interface's language of the moment, and the components into rows.</summary>
    public AboutViewModel()
    {
        UiText text = UiText.Current;
        Title = text.AboutTitle;
        Tagline = text.AboutTagline;
        LicenceHeading = text.AboutLicenceHeading;
        Warranty = text.AboutWarranty;
        LicenceLinkLabel = text.AboutLicenceLink;
        ThirdPartyHeading = text.AboutThirdPartyHeading;
        ThirdPartyLead = text.AboutThirdParty;
        Close = text.Close;
        Components = [.. ThirdParty.Components.Select(c => new ComponentRow(c.Name, c.Role, c.Licence, c.Copyright, c.Url))];
    }

    public string Title { get; }

    public string Name => VersionInfo.Name;

    public string Tagline { get; }

    public string Banner => VersionInfo.Banner;

    public string Copyright => VersionInfo.Copyright;

    public string LicenceHeading { get; }

    public string Warranty { get; }

    public string LicenceUrl => VersionInfo.LicenceUrl;

    public string LicenceLinkLabel { get; }

    public string ThirdPartyHeading { get; }

    public string ThirdPartyLead { get; }

    public string Close { get; }

    public IReadOnlyList<ComponentRow> Components { get; }
}
