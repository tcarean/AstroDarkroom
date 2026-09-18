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

namespace FlatCheck.Wpf.Tests;

/// <summary>
/// The synthetic tab, driven without a window and without anything installed: the profile
/// sources are hand-made (LensProfileTestData), the session is the usual fixture. What is
/// pinned: the target path beside the master, the prefill from the header, the search, the
/// button writing the file exactly once with the Saved event, the overwrite question, and
/// the comparison against the file just written.
/// </summary>
public class SyntheticTabTests
{
    static (SyntheticTabViewModel Vm, TempWorkspace Ws) Attached(SessionFixture f, Func<string, bool>? confirm = null)
    {
        var ws = new TempWorkspace();
        var sources = LensProfileTestData.Sources(ws);
        var vm = new SyntheticTabViewModel(confirm, () => sources);
        vm.Attach(f.Session, CancellationToken.None);
        return (vm, ws);
    }

    static IEnumerable<string> MasterCards => new[]
    {
        FitsTestData.Card("FOCALLEN", "20.0"),
        FitsTestData.Card("INSTRUME", "'Nikon D3300'"),
    };

    static SessionFixture Fixture()
    {
        var f = new SessionFixture().WithProcess();
        string path = Path.Combine(f.ProcessDir, SirilSession.FlatMasterName);
        File.WriteAllBytes(path, FitsTestData.BuildFits(128, 128,
            FitsTestData.Radial(128, 128), extraCards: MasterCards));
        return f;
    }

    [Fact]
    public async Task AttachNamesTheTargetBesideTheMaster_AndPrefillsFromTheHeader()
    {
        using var f = Fixture();
        var (vm, ws) = Attached(f);
        using (ws)
        {
            await vm.Pending;

            Assert.True(vm.CanRun);
            Assert.False(vm.HasResult);
            Assert.False(vm.Guide.IsExpanded);                       // documentation, collapsed
            Assert.Equal(Path.Combine(f.ProcessDir, "pp_flat_stacked_sintetic.fit"), vm.Target);
            Assert.False(vm.CanCompare);                             // nothing written yet
            Assert.Equal("20", vm.FocalText);
            Assert.Equal("1.523", vm.CropText);                      // Nikon D3300 from the lensfun cameras
            Assert.Equal("", vm.FStopText);                          // no raw in flats/: nobody knows the f-stop
            Assert.Contains("FOCALLEN", vm.PrefillNote);
            Assert.Equal(3, vm.Lenses.Count);
            Assert.NotEmpty(vm.AdobeWhere);
            Assert.Contains("lensfun", vm.LensfunWhere);
        }
    }

    [Fact]
    public async Task SearchFiltersTheListOnEveryWord_AndKeepsTheSelectionWhenItSurvives()
    {
        using var f = Fixture();
        var (vm, ws) = Attached(f);
        using (ws)
        {
            await vm.Pending;
            vm.Search = "nikon corporation";
            Assert.Single(vm.Lenses);
            vm.SelectedLens = vm.Lenses[0];
            vm.Search = "tokina";
            Assert.Equal(3, vm.Lenses.Count);
            Assert.NotNull(vm.SelectedLens);
            vm.Search = "zeiss";
            Assert.Empty(vm.Lenses);
            Assert.Null(vm.SelectedLens);
        }
    }

    [Fact]
    public async Task TheButtonWritesTheFileOnce_AndTheComparisonFollowsByItself()
    {
        using var f = Fixture();
        var (vm, ws) = Attached(f);
        using (ws)
        {
            await vm.Pending;
            var saved = new List<string>();
            vm.Saved += saved.Add;
            vm.Search = "nikon corporation";
            vm.SelectedLens = vm.Lenses[0];
            vm.FStopText = "2.8";

            await vm.RunAsync();

            Assert.True(vm.HasResult, vm.ErrorText);
            Assert.False(vm.HasError);
            Assert.Equal(vm.Target, vm.SavedTo);
            Assert.Equal(new[] { vm.Target }, saved);
            Assert.True(File.Exists(vm.Target));
            Assert.Equal(4, vm.Depth.Count);
            Assert.Contains("20 mm, f/2.8", vm.ProfileLine);
            Assert.Contains("Adobe", vm.ProfileLine);
            Assert.True(vm.CanCompare);
            Assert.Equal(4, vm.Depth.Count);
            Assert.All(vm.Depth, d => Assert.True(d.HasNote, d.Label));   // each depth point says what it is

            // the write is followed by the comparison, with no second click: the screen after
            // "Scrie" without the table and the map was reported as a bug
            AssertComparison(vm);
            Assert.StartsWith("flatcheck ", vm.Report);
            Assert.Contains("FLAT SINTETIC", vm.Report);
            Assert.Contains("flat sintetic scris", vm.Report);
            Assert.True(vm.Report.IndexOf("FLAT SINTETIC") < vm.Report.IndexOf("COMPARATIE"));
            Assert.Equal(1, vm.Report.Split("flatcheck ").Length - 1);      // one banner, not two
        }
    }

    [Fact]
    public async Task ASensorFormatChipFillsTheCrop_AndTypingReleasesIt()
    {
        using var f = Fixture();
        var (vm, ws) = Attached(f);
        using (ws)
        {
            await vm.Pending;
            Assert.Equal("1.523", vm.CropText);                  // from the table: the APS-C chip shows it, the note says where from
            Assert.Equal("APS-C 1.5", vm.SensorFormat.Selected?.Label);
            Assert.Equal("crop 1.523 din baza lensfun (Nikon D3300) - format APS-C", vm.CropNote);
            Assert.Equal(6, vm.SensorFormat.Options.Count);

            vm.SensorFormat.Options.Single(o => o.Label.StartsWith("APS-C Canon")).IsSelected = true;

            Assert.Equal("1.6", vm.CropText);
            Assert.Contains("nominal, din chip-ul APS-C Canon", vm.CropNote);
            Assert.Equal("APS-C Canon 1.6", vm.SensorFormat.Selected.Label);

            vm.CropText = "1.15";                                // no format within 5 %: every chip off, the note says so

            Assert.Null(vm.SensorFormat.Selected);
            Assert.All(vm.SensorFormat.Options, o => Assert.False(o.IsSelected));
            Assert.Equal("crop 1.15 tastat - format neobisnuit, niciun chip nu i se potriveste", vm.CropNote);

            vm.CropText = "1.0";                                 // typing lights the chip it means

            Assert.Equal("full frame 1.0", vm.SensorFormat.Selected?.Label);
            Assert.Equal("crop 1.0 tastat - format full frame", vm.CropNote);

            vm.CropText = "";
            Assert.Null(vm.SensorFormat.Selected);
            Assert.Equal("", vm.CropNote);
        }
    }

    /// <summary>Choosing a lens says at once what it was calibrated on and whether that is the user's sensor.</summary>
    [Fact]
    public async Task ChoosingALensSaysWhetherItsProfileFitsTheSensor()
    {
        using var f = Fixture();
        var (vm, ws) = Attached(f);
        using (ws)
        {
            await vm.Pending;
            Assert.Equal("", vm.FormatNote);

            vm.Search = "nikon corporation";
            vm.SelectedLens = vm.Lenses[0];
            await vm.FormatPending;
            Assert.StartsWith("Tokina AT-X 11-20mm F2.8 PRO DX: profil calibrat pe APS-C (crop 1.5); senzorul tau APS-C (1.523): acelasi format", vm.FormatNote);

            vm.Search = "Aspherical";
            vm.SelectedLens = vm.Lenses.Single();
            await vm.FormatPending;
            Assert.Contains("profil calibrat pe APS-C Canon (crop 1.61); senzorul tau APS-C (1.523): alt format", vm.FormatNote);
            Assert.Contains("extrapolare", vm.FormatNote);      // the fixture master is square, so the corner sits further out than on 3:2

            vm.CropText = "1.0";                                 // a full frame on the same profile: the note follows the crop
            await vm.FormatPending;
            Assert.Contains("senzorul tau full frame (1): alt format", vm.FormatNote);
            Assert.Contains("extrapolare MARE", vm.FormatNote);
            Assert.True(vm.FormatWarning);                       // coloured, unlike the same-format line

            vm.CropText = "2.0";                                 // a smaller sensor sits inside the profile: no warning colour
            await vm.FormatPending;
            Assert.Contains("in interiorul profilului", vm.FormatNote);
            Assert.False(vm.FormatWarning);

            vm.SelectedLens = null;
            Assert.Equal("", vm.FormatNote);
        }
    }

    [Fact]
    public async Task TheCompareButtonReadsAFileWrittenEarlier()
    {
        using var f = Fixture();
        var (first, ws1) = Attached(f);
        using (ws1)
        {
            await first.Pending;
            first.Search = "nikon corporation";
            first.SelectedLens = first.Lenses[0];
            first.FStopText = "2.8";
            await first.RunAsync();
            Assert.True(first.HasCompare, first.ErrorText);
        }

        var (vm, ws) = Attached(f);
        using (ws)
        {
            await vm.Pending;
            Assert.True(vm.CanCompare);
            Assert.False(vm.HasCompare);

            await vm.CompareAsync();

            AssertComparison(vm);
            Assert.StartsWith("flatcheck ", vm.Report);
            Assert.DoesNotContain("FLAT SINTETIC", vm.Report);
        }
    }

    static void AssertComparison(SyntheticTabViewModel vm)
    {
        Assert.True(vm.HasCompare, vm.ErrorText);
        Assert.False(vm.HasError);
        Assert.Equal(8, vm.CompareTable.Count);                 // three informative rows + five tests
        Assert.All(vm.CompareTable, r => { Assert.NotEmpty(r.A); Assert.NotEmpty(r.B); Assert.NotEmpty(r.Diff); Assert.True(r.HasNote); });
        Assert.Single(vm.CompareTable, r => r.HasDiffVerdict);          // only the depth row grades the difference
        Assert.True(vm.CompareTable[0].HasDiffVerdict);
        Assert.Equal(5, vm.CompareTable.Count(r => r.HasAVerdict));
        Assert.Single(vm.CompareTable, r => r.IsKey);
        Assert.NotEmpty(vm.CompareVerdict);
        Assert.Contains("forma: ", vm.CompareVerdictDetail);
        Assert.Contains("Adancimea: ", vm.CompareVerdictDetail);
        Assert.NotNull(vm.CompareMap);
        Assert.NotEmpty(vm.MapLegend);
        Assert.Contains("COMPARATIE", vm.Report);
    }

    [Fact]
    public async Task ALensIdSharedBySeveralLensesNarrowsTheListInsteadOfPickingTheFirst()
    {
        using var f = Fixture();
        var (vm, ws) = Attached(f);
        using (ws)
        {
            await vm.Pending;
            int all = vm.Lenses.Count;
            var both = vm.Lenses.Where(r => r.Label.Contains("11-20mm F2.8")).Select(r => r.Choice).ToList();
            Assert.Equal(2, both.Count);

            string note = vm.OfferCandidates(both, "ID-ul Nikon 139 din raw (11-20 mm f/2.8)");

            Assert.Null(vm.SelectedLens);
            Assert.Equal(2, vm.Lenses.Count);
            Assert.Contains("2 obiective", note);
            Assert.Contains("ID-ul Nikon 139", note);

            // typing brings the whole list back
            vm.Search = "tokina";
            Assert.True(vm.Lenses.Count >= 2);
            vm.Search = "";
            Assert.Equal(all, vm.Lenses.Count);

            // one candidate is simply selected
            note = vm.OfferCandidates(new[] { both[0] }, "ID-ul Nikon 139 din raw (11-20 mm f/2.8)");
            Assert.Equal(both[0], vm.SelectedLens?.Choice);
            Assert.StartsWith("obiectivul dupa ID-ul Nikon 139", note);
            Assert.Null(vm.OfferCandidates(Array.Empty<LensChoice>(), "x"));
        }
    }

    [Fact]
    public async Task ANoToTheOverwriteQuestionKeepsTheOldFile_AndTheResultStays()
    {
        using var f = Fixture();
        var (vm, ws) = Attached(f, confirm: _ => false);
        using (ws)
        {
            await vm.Pending;
            File.WriteAllText(vm.Target, "vechi");
            vm.Search = "nikon corporation";
            vm.SelectedLens = vm.Lenses[0];
            vm.FStopText = "2.8";

            await vm.RunAsync();

            Assert.True(vm.HasResult);
            Assert.Equal("", vm.SavedTo);
            Assert.Contains(vm.Target, vm.SaveError);
            Assert.Equal("vechi", File.ReadAllText(vm.Target));
        }
    }

    [Fact]
    public async Task WithoutALensOrAnFStopTheButtonRefusesWithTheReason()
    {
        using var f = Fixture();
        var (vm, ws) = Attached(f);
        using (ws)
        {
            await vm.Pending;
            await vm.RunAsync();
            Assert.True(vm.HasError);
            Assert.Equal(SessionMessages.SyntheticNoLensSelected(), vm.ErrorText);

            vm.Search = "nikon corporation";
            vm.SelectedLens = vm.Lenses[0];
            await vm.RunAsync();
            Assert.True(vm.HasError);
            Assert.Contains("--fstop", vm.ErrorText);
            Assert.False(File.Exists(vm.Target));
        }
    }

    [Fact]
    public async Task ResetForgetsTheSessionAndTheResult()
    {
        using var f = Fixture();
        var (vm, ws) = Attached(f);
        using (ws)
        {
            await vm.Pending;
            vm.Search = "nikon corporation";
            vm.SelectedLens = vm.Lenses[0];
            vm.FStopText = "2.8";
            await vm.RunAsync();

            vm.Reset();

            Assert.False(vm.CanRun);
            Assert.False(vm.HasResult);
            Assert.Equal("", vm.Target);
            Assert.Null(vm.SelectedLens);
            Assert.Equal("", vm.FocalText);
        }
    }
}
