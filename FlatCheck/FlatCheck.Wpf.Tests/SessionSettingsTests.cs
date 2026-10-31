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

using FlatCheck.Tests;
using NLog;

namespace FlatCheck.Wpf.Tests;

/// <summary>
/// The two switches the window remembers per session folder, in flatcheck.json in the
/// session root (R-119): the Structure tab's "no filters" box and the Exposure tab's "sky
/// flat" box. The file is state of the window, not a result: what cannot be read is the
/// default, what cannot be written is said in the log and kept for this run.
/// </summary>
public class SessionSettingsTests
{
    /// <summary>R-119: the file is where the session is, under the name the requirement
    /// gives, and a session without one starts on the defaults.</summary>
    [Fact]
    public void R119_AMissingFileGivesTheDefaultsAndNamesWhereItWouldBe()
    {
        using var f = new SessionFixture().WithFlatMaster();

        var settings = SessionSettings.Read(f.Session.SettingsPath);

        Assert.Equal(Path.Combine(f.Root, "flatcheck.json"), settings.FilePath);
        Assert.Equal(Path.Combine(f.Root, SessionSettings.FileName), f.Session.SettingsPath);
        Assert.True(settings.NoFilters);
        Assert.False(settings.SkyFlat);
        Assert.False(File.Exists(settings.FilePath));
    }

    /// <summary>R-119: both switches survive a write and a read, under the two keys the
    /// requirement names.</summary>
    [Fact]
    public void R119_BothSwitchesRoundTripThroughTheFile()
    {
        using var f = new SessionFixture().WithFlatMaster();
        var settings = SessionSettings.Read(f.Session.SettingsPath);

        settings.NoFilters = false;
        settings.SkyFlat = true;
        settings.Write();

        string json = File.ReadAllText(settings.FilePath);
        Assert.Contains("\"noFilters\":false", json);
        Assert.Contains("\"skyFlat\":true", json);
        Assert.False(File.Exists(settings.FilePath + ".tmp"));   // written through a move, nothing left beside

        var again = SessionSettings.Read(f.Session.SettingsPath);
        Assert.False(again.NoFilters);
        Assert.True(again.SkyFlat);
    }

    /// <summary>R-119: a file that is not JSON, not an object, or whose key holds another type
    /// is no setting - the defaults, never an exception or a message.</summary>
    [Theory]
    [InlineData("not json at all")]
    [InlineData("[1, 2]")]
    [InlineData("{\"noFilters\":\"no\",\"skyFlat\":1}")]
    [InlineData("")]
    public void R119_AMalformedFileOrAKeyOfAnotherTypeGivesTheDefaults(string text)
    {
        using var f = new SessionFixture().WithFlatMaster();
        File.WriteAllText(f.Session.SettingsPath, text);

        var settings = SessionSettings.Read(f.Session.SettingsPath);

        Assert.True(settings.NoFilters);
        Assert.False(settings.SkyFlat);
    }

    /// <summary>R-119: one key present, the other absent: the present one is read, the absent
    /// one is its default.</summary>
    [Fact]
    public void R119_AKeyMissingFromTheFileIsItsDefault()
    {
        using var f = new SessionFixture().WithFlatMaster();
        File.WriteAllText(f.Session.SettingsPath, "{\"skyFlat\":true}");

        var settings = SessionSettings.Read(f.Session.SettingsPath);

        Assert.True(settings.NoFilters);
        Assert.True(settings.SkyFlat);
    }

    /// <summary>R-119: a write that fails (here the file's name is taken by a folder) is a Warn
    /// line naming the file, no exception, and the value holds for this run.</summary>
    [Fact]
    public void R119_AFailedWriteIsAWarnLineAndTheSwitchKeepsItsValue()
    {
        using var f = new SessionFixture().WithFlatMaster();
        Directory.CreateDirectory(f.Session.SettingsPath);
        var settings = SessionSettings.Read(f.Session.SettingsPath);
        using var capture = LogCapture.Start();

        settings.SkyFlat = true;
        settings.Write();

        Assert.True(settings.SkyFlat);
        var warned = Assert.Single(capture.Events, e => e.Level == LogLevel.Warn);
        Assert.Contains("flatcheck.json", warned.Message);
        Assert.NotNull(warned.Exception);
        Assert.False(File.Exists(settings.FilePath + ".tmp"));   // the temporary file does not stay behind
    }

    /// <summary>R-121: the chosen lens's key is the third thing the file holds; it round-trips,
    /// and a write without a choice writes no `lens` key at all.</summary>
    [Fact]
    public void R121_TheChosenLensRoundTripsAndIsAbsentWhenNoneWasChosen()
    {
        using var f = new SessionFixture().WithFlatMaster();
        var settings = SessionSettings.Read(f.Session.SettingsPath);
        Assert.Null(settings.Lens);

        settings.SkyFlat = true;
        settings.Write();
        Assert.DoesNotContain("lens", File.ReadAllText(settings.FilePath));

        settings.Lens = "Lensfun|Samyang|Samyang 135mm f/2.0 ED UMC|Nikon F AI|full frame 1";
        settings.Write();

        string json = File.ReadAllText(settings.FilePath);
        Assert.Contains("\"lens\":\"Lensfun|Samyang|Samyang 135mm f/2.0 ED UMC|Nikon F AI|full frame 1\"", json);
        Assert.Contains("\"skyFlat\":true", json);
        var again = SessionSettings.Read(f.Session.SettingsPath);
        Assert.Equal(settings.Lens, again.Lens);
        Assert.True(again.SkyFlat);

        again.Lens = null;
        again.Write();
        Assert.DoesNotContain("lens", File.ReadAllText(settings.FilePath));
        Assert.Null(SessionSettings.Read(f.Session.SettingsPath).Lens);
    }

    /// <summary>R-121: a `lens` key that is empty or of another type is no choice.</summary>
    [Theory]
    [InlineData("{\"lens\":\"\"}")]
    [InlineData("{\"lens\":12}")]
    [InlineData("{\"lens\":true}")]
    [InlineData("{\"lens\":null}")]
    public void R121_AnEmptyOrMistypedLensKeyIsNoChoice(string text)
    {
        using var f = new SessionFixture().WithFlatMaster();
        File.WriteAllText(f.Session.SettingsPath, text);

        var settings = SessionSettings.Read(f.Session.SettingsPath);

        Assert.Null(settings.Lens);
        Assert.True(settings.NoFilters);
    }

    /// <summary>Settings without a file - a tab attached in a test, a tab before any folder -
    /// remember nothing and write nowhere.</summary>
    [Fact]
    public void SettingsWithoutAFileWriteNothing()
    {
        var none = SessionSettings.None;

        none.NoFilters = false;
        none.Write();

        Assert.Null(none.FilePath);
        Assert.False(none.NoFilters);
    }
}
