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

namespace AstroPhotoAnalyzer.Tests;

public class IccProfileInfoTests
{
    [Fact]
    public void ReadsAVersion2TextDescription()
    {
        Assert.Equal("sRGB-elle-V2-srgbtrc.icc", IccProfileInfo.Describe(IccTestData.V2("sRGB-elle-V2-srgbtrc.icc")));
    }

    [Fact]
    public void ReadsAVersion4MultiLocalizedDescription()
    {
        Assert.Equal("sRGB IEC61966-2.1", IccProfileInfo.Describe(IccTestData.V4("sRGB IEC61966-2.1")));
    }

    [Fact]
    public void AProfileWithoutADescriptionGivesNull()
    {
        Assert.Null(IccProfileInfo.Describe(IccTestData.WithoutDescription()));
    }

    [Fact]
    public void GarbageGivesNullNotAnException()
    {
        Assert.Null(IccProfileInfo.Describe(null));
        Assert.Null(IccProfileInfo.Describe(new byte[10]));
        Assert.Null(IccProfileInfo.Describe(new byte[200]));

        // A valid profile, truncated right in the middle of the tag.
        byte[] cut = IccTestData.V2("a name long enough for it to be seen").Take(150).ToArray();
        Assert.Null(IccProfileInfo.Describe(cut));
    }
}
