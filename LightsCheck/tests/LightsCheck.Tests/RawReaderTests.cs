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

using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.RegularExpressions;
using LightsCheck.Core;

namespace LightsCheck.Tests;

/// <summary>
/// Reading camera raws is the one place the tool leans on someone else's code, so what is
/// pinned here is the seam (R-007): which paths are routed to libraw, that a FITS never is,
/// that the assembly stays lazily loaded, and what the user and the log are told when libraw
/// cannot be loaded (R-074). A raw cannot live in the repo (the smallest real NEF is 18 MB), so
/// the decode itself is checked only when LIGHTSCHECK_NEF points at one.
/// </summary>
public partial class RawReaderTests
{
    private static readonly RawReader Reader = new();

    [Theory]
    [InlineData("light.NEF")]
    [InlineData("light.nef")]
    [InlineData("a/b/DSC_0060.Nef")]
    [InlineData("x.cr2")]
    [InlineData("x.cr3")]
    [InlineData("x.arw")]
    [InlineData("x.dng")]
    [InlineData("x.raf")]
    [InlineData("x.pef")]                                  // Pentax, taken from FlatCheck (R-065, ADR 0025)
    [InlineData("x.PEF")]
    public void R007_RecognisesTheRawExtensions(string path) => Assert.True(Reader.Handles(path));

    [Theory]
    [InlineData("light_00001.fit")]
    [InlineData("x.fits")]
    [InlineData("x.fts")]
    [InlineData("noextension")]
    [InlineData("")]
    [InlineData(null)]
    public void R007_DoesNotClaimAnythingThatIsNotARaw(string? path) => Assert.False(Reader.Handles(path!));

    [Fact]
    public void AMissingRawFailsWithTheFileNameNotALibrawErrorCode()
    {
        string path = Path.Combine(Path.GetTempPath(), "lightscheck-nu-exista.NEF");
        FileNotFoundException e = Assert.Throws<FileNotFoundException>(() => Reader.Load(path));
        Assert.Contains("lightscheck-nu-exista.NEF", e.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// What libraw refuses becomes the reader's own error, the type a corrupt FITS gives, naming
    /// the file in the run's language (R-007): the console and the window handle it; libraw's
    /// exception type and words do not leave the reader.
    /// </summary>
    [Fact]
    public void R007_AFileLibrawCannotOpenFailsWithInvalidDataNamingTheFile()
    {
        using TempWorkspace ws = new();
        string path = ws.WriteFile("minte.NEF", new byte[4096]);

        InvalidDataException full = Assert.Throws<InvalidDataException>(() => Reader.Load(path));
        InvalidDataException info = Assert.Throws<InvalidDataException>(() => Reader.LoadInfo(path));
        InvalidDataException english;
        using (Ui.Use(UiLanguage.English))
        {
            english = Assert.Throws<InvalidDataException>(() => Reader.Load(path));
        }

        // The tests' default, Romanian, and English, the file named once; libraw's own words
        // ("Failed opening file: <path>") are the inner exception's, not the user's (ADR 0020).
        Assert.Equal($"{path}: libraw nu poate citi fisierul", full.Message);
        Assert.Equal($"{path}: libraw nu poate citi fisierul", info.Message);
        Assert.Equal($"{path}: libraw cannot read the file", english.Message);
        Assert.NotNull(full.InnerException);
        Assert.Contains("minte.NEF", full.InnerException.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The end-to-end proof, on a real file: LIGHTSCHECK_NEF=E:/.../DSC_0001.NEF. Does nothing
    /// without the variable. The mosaic is undebayered, the scale exact, the CFA order read,
    /// and the shot metadata filled; the metadata-only load agrees with the full one.
    /// </summary>
    [Fact]
    public void R007_ARealRawDecodesToAnUndebayeredMosaicWithItsScaleAndMetadata()
    {
        string? path = Environment.GetEnvironmentVariable("LIGHTSCHECK_NEF");
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        LoadedFrame f = ImageLoader.Load(path);
        Assert.True(f.Image.H > 1000 && f.Image.W > 1000, $"{f.Image.W}x{f.Image.H}");
        Assert.Equal("libraw (exact)", f.Scale.Source);
        Assert.True(f.Scale.White > f.Scale.Black, $"white {f.Scale.White} black {f.Scale.Black}");
        Assert.NotNull(f.Header);
        Assert.Empty(f.Header);
        Assert.NotNull(f.Cfa);
        Assert.Equal("libraw", f.Cfa.Source);
        Assert.Equal(2, f.Cfa.Letters.Count(c => c == 'G'));

        // Undebayered: the four Bayer positions do not agree on a colour image.
        Img[] positions = Mosaic.Split(f.Image);
        double[] medians = positions.Select(p => Stats.Percentile(p.D, 50)).ToArray();
        Assert.True(medians.Max() / Math.Max(medians.Min(), 1) > 1.05, "the four positions agree: the frame was demosaiced");

        Assert.True(f.ExposureSeconds > 0);
        Assert.True(f.Iso > 0);
        Assert.NotNull(f.Timestamp);
        Assert.NotNull(f.Camera);

        LoadedFrame info = ImageLoader.LoadInfo(path);
        Assert.Null(info.Image);
        Assert.Equal(f.ExposureSeconds, info.ExposureSeconds);
        Assert.Equal(f.Iso, info.Iso);
        Assert.Equal(f.Timestamp, info.Timestamp);
        Assert.Equal(f.Camera, info.Camera);
        Assert.Equal(f.FocalMm, info.FocalMm);
        Assert.Equal(f.FNumber, info.FNumber);
        Assert.Equal(f.LensId, info.LensId);
        Assert.Equal(f.LensMount, info.LensMount);
        Assert.Equal(f.LensName, info.LensName);
        // The size and the 35 mm-equivalent focal length are known from the open alone (R-066).
        Assert.Equal(f.Image.W, info.Width);
        Assert.Equal(f.Image.H, info.Height);
        Assert.Equal(f.FocalLength35Mm, info.FocalLength35Mm);
    }

    // The four rules below are FlatCheck's lens hunk (its R-108; here R-065, ADR 0025), pinned on
    // the values libraw 0.21.1 gave FlatCheck on 2026-10-26 on a D3300 and on the raw.pixls.us
    // samples of its docs/validation.md (V-041 to V-048); no raw is needed.

    [Fact]
    public void R065_TheMountIsTheLenssAndTheCamerasOnlyWhenTheLenssIsUnknown()
    {
        Assert.Equal(6, RawReader.MountOf(6, 5));                 // the R5 Mark II read by an older libraw
        Assert.Equal(5, RawReader.MountOf(5, 5));
        Assert.Equal(5, RawReader.MountOf(0, 5));
        Assert.Equal(0, RawReader.MountOf(0, 0));
    }

    [Fact]
    public void R065_TheLensIdComesFromNikonsFieldOnANikonAndFromTheMakerNoteElsewhere()
    {
        const ulong signature = 5356031691206446338UL;      // what MakerNotes.LensID holds on a D3300
        Assert.Equal(74L, RawReader.LensIdOf(74, signature, 27));     // the Samyang 135, a lens without CPU
        Assert.Equal(139L, RawReader.LensIdOf(139, signature, 27));   // the Tokina 11-20
        Assert.Null(RawReader.LensIdOf(0, signature, 27));   // a Nikon on a telescope: the signature is no ID
        Assert.Null(RawReader.LensIdOf(0, 139, 28));         // Nikon Z: the same field, the same rule
        Assert.Equal(198L, RawReader.LensIdOf(0, 198, 5));   // Canon EF
        Assert.Equal(155L, RawReader.LensIdOf(0, 155, 5));
        Assert.Equal(32831L, RawReader.LensIdOf(0, 32831, 40));  // Sony E
        Assert.Equal(2263L, RawReader.LensIdOf(0, 2263, 33));    // Pentax K
        Assert.Null(RawReader.LensIdOf(0, 155, 0));          // an unknown mount: nothing is guessed
        Assert.Null(RawReader.LensIdOf(0, 0, 5));
        Assert.Null(RawReader.LensIdOf(0, 1UL << 40, 5));    // does not fit 31 bits: not an ID
    }

    [Theory]
    [InlineData(27, "Nikon F")]
    [InlineData(28, "Nikon Z")]
    [InlineData(5, "Canon EF")]
    [InlineData(4, "Canon EF-S")]
    [InlineData(6, "Canon RF")]
    [InlineData(40, "Sony E")]
    [InlineData(25, "Sony A")]
    [InlineData(33, "Pentax K")]
    [InlineData(13, "Fujifilm X")]
    [InlineData(10, "Micro 4/3")]
    [InlineData(16, "Hasselblad XCD")]
    [InlineData(2, null)]                                          // "C", a mount the table does not spell: no programming name leaks
    [InlineData(45, null)]                                         // libraw's sentinel
    [InlineData(999, null)]
    [InlineData(0, null)]
    public void R065_TheMountIsNamedAsLibrawNamesIt(int code, string? label)
        => Assert.Equal(label, RawReader.MountLabel(code));

    [Fact]
    public void R065_TheRangeFallsBackToTheTopFields_AndToMaxApertureOnlyAtTheShortEnd()
    {
        // Canon prime: the MakerNote's MaxAperture4MinFocal is 0, MaxAperture 1.414
        Assert.Equal<(double?, double?, double?)>((50.0, 50.0, 1.4142135), RawReader.RangeOf(50, 50, 0, 1.4142135, 50, 50, 0, 50));
        // Canon zoom shot at the long end: MaxAperture is the aperture THERE, not the name's; not taken
        Assert.Equal<(double?, double?, double?)>((18.0, 135.0, null), RawReader.RangeOf(18, 135, 0, 5.6, 18, 135, 0, 135));
        // the same zoom at its short end: the two apertures coincide, so it is taken
        Assert.Equal<(double?, double?, double?)>((18.0, 135.0, 3.5), RawReader.RangeOf(18, 135, 0, 3.5, 18, 135, 0, 18));
        // a constant-aperture zoom at the long end is still not taken: nothing says it is constant
        Assert.Equal<(double?, double?, double?)>((70.0, 200.0, null), RawReader.RangeOf(70, 200, 0, 2.828427, 70, 200, 0, 200));
        // Nikon: the MakerNote alone, the top fields 0 (the Veil session's Samyang 135 f/2)
        Assert.Equal<(double?, double?, double?)>((135.0, 135.0, 2.0), RawReader.RangeOf(135, 135, 2, 0, 0, 0, 0, 135));
        // Sony: both filled, the MakerNote first
        Assert.Equal<(double?, double?, double?)>((16.0, 35.0, 2.8), RawReader.RangeOf(16, 35, 2.8, 0, 16, 35, 2.8, 32));
        // Pentax: nothing anywhere
        Assert.Equal<(double?, double?, double?)>((null, null, null), RawReader.RangeOf(0, 0, 0, 0, 0, 0, 0, 18));
        // only the top fields
        Assert.Equal<(double?, double?, double?)>((24.0, 70.0, 2.8), RawReader.RangeOf(0, 0, 0, 0, 24, 70, 2.8, 50));
        // within 1 mm or 5 % the two ends count as one, a prime: the aperture is taken, the ends stay as given
        Assert.Equal<(double?, double?, double?)>((50.0, 51.0, 1.4), RawReader.RangeOf(50, 51, 0, 1.4, 0, 0, 0, 0));
        Assert.Equal<(double?, double?, double?)>((50.0, 60.0, null), RawReader.RangeOf(50, 60, 0, 1.4, 0, 0, 0, 0));
    }

    /// <summary>
    /// A frame fabricated with the values libraw gave FlatCheck for each make reaches the
    /// inventory's record with its three lens fields: a reader that filled them is all
    /// <see cref="FrameInfo.From"/> needs.
    /// </summary>
    [Theory]
    [InlineData("Canon EOS 7D Mark II", 198L, "Canon EF", "EF50mm f/1.4 USM", 50.0, 50.0, 1.4142135)]
    [InlineData("Sony ILCE-7M3", 32831L, "Sony E", "FE 16-35mm F2.8 GM", 16.0, 35.0, 2.8)]
    [InlineData("Pentax K-70", 2263L, "Pentax K", null, null, null, null)]
    [InlineData("Nikon D3300", 74L, "Nikon F", null, 135.0, 135.0, 2.0)]
    [InlineData("Nikon D3300", null, "Nikon F", null, null, null, null)]       // a D3300 on a telescope
    public void R065_AFabricatedFrameOfAnyMakeCarriesItsLensIntoFrameInfo(string camera, long? lensId, string? mount, string? name,
                                                                             double? minFocal, double? maxFocal, double? maxAperture)
    {
        LoadedFrame f = new()
        {
            Header = new Dictionary<string, string>(),
            Camera = camera,
            ExposureSeconds = 120.2,
            Iso = 1600,
            LensId = lensId,
            LensMount = mount,
            LensName = name,
            LensMinFocalMm = minFocal,
            LensMaxFocalMm = maxFocal,
            LensMaxAperture = maxAperture,
        };

        FrameInfo info = FrameInfo.From(3, "lights/x.cr2", f);

        Assert.Equal(3, info.Index);
        Assert.Equal(camera, info.Camera);
        Assert.Equal(lensId, info.LensId);
        Assert.Equal(mount, info.LensMount);
        Assert.Equal(name, info.LensName);
        Assert.Equal(minFocal, info.LensMinFocalMm);
        Assert.Equal(maxFocal, info.LensMaxFocalMm);
        Assert.Equal(maxAperture, info.LensMaxAperture);
        Assert.Equal(1600, info.Iso);
        Assert.Null(info.Error);
    }

    /// <summary>
    /// A FITS run must keep working when Sdcb.LibRaw is absent entirely. That holds only while
    /// no field of the reader is a libraw type: fields are resolved when the type is loaded,
    /// which happens on every run since ImageLoader asks it Handles(); method bodies at JIT
    /// time. Measured in FlatCheck by deleting the DLL (its ADR 0009).
    /// </summary>
    [Fact]
    public void R007_HoldsNoLibrawTypedFields_SoTheAssemblyStaysLazilyLoaded()
    {
        FieldInfo[] fields = typeof(RawReader).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        foreach (FieldInfo f in fields)
        {
            string asm = f.FieldType.Assembly.GetName().Name ?? "";
            Assert.False(asm.StartsWith("Sdcb", StringComparison.Ordinal), $"field {f.Name} is of type {f.FieldType.FullName} from {asm}: that forces libraw to load on FITS runs too");
        }
    }

    /// <summary>
    /// When libraw cannot be loaded, the user reads the likely cause and Microsoft's link, in each
    /// language, and nothing meant for a developer: no executable, no DLL, no publish option, not
    /// the loader's own words, which name libraw.dll (R-074, ADR 0030). The loader's exception
    /// stays the inner one, for the log.
    /// </summary>
    [Fact]
    public void R074_TheLoadFailureNamesTheVisualCppRuntimeAndItsLink_InBothLanguages()
    {
        DllNotFoundException loader = new("Unable to load DLL 'libraw.dll' or one of its dependencies: The specified module could not be found. (0x8007007E)");
        RawReader.RuntimeCheck asked = new(_ => true);

        InvalidOperationException en, ro;
        using (Ui.Use(UiLanguage.English))
        {
            en = RawReader.Missing(loader, asked);
        }

        using (Ui.Use(UiLanguage.Romanian))
        {
            ro = RawReader.Missing(loader, asked);
        }

        Assert.Equal(
            "cannot load libraw: Microsoft's Visual C++ runtime is likely missing or too old, so camera raw files cannot be read "
            + "until its latest version is installed (FITS frames are not affected): https://aka.ms/vc14/vc_redist.x64.exe",
            en.Message);
        Assert.Equal(
            "nu pot incarca libraw: probabil runtime-ul Microsoft Visual C++ lipseste sau este prea vechi, deci nu pot citi fisiere raw "
            + "de aparat pana nu instalezi ultima lui versiune (cadrele FITS nu sunt afectate): https://aka.ms/vc14/vc_redist.x64.exe",
            ro.Message);
        Assert.Equal("https://aka.ms/vc14/vc_redist.x64.exe", RawReader.RuntimeUrl);
        foreach (InvalidOperationException e in new[] { en, ro })
        {
            Assert.EndsWith(" " + RawReader.RuntimeUrl, e.Message, StringComparison.Ordinal);
            Assert.Same(loader, e.InnerException);
            foreach (string developer in new[] { "IncludeNativeLibrariesForSelfExtract", "lightscheck.exe", "libraw.dll", "raw_r.dll" })
            {
                Assert.DoesNotContain(developer, e.Message, StringComparison.OrdinalIgnoreCase);
            }
        }

        Assert.All(ro.Message, c => Assert.True(c < 128, $"'{c}' in the Romanian message, which is written without diacritics"));
    }

    /// <summary>
    /// The first failure asks Windows for the runtime's four DLLs, in their order, and logs at Warn
    /// those that do not load; a second failure asks nothing and logs nothing (R-074). A series of
    /// 87 raws fails 174 times, each failure a search of the PATH were it asked every time.
    /// </summary>
    [Fact]
    public void R074_TheRuntimeIsAskedForOnce_AndTheLogNamesTheDllsThatDoNotLoad()
    {
        List<string> asked = [];
        RawReader.RuntimeCheck check = new(file =>
        {
            asked.Add(file);
            return file is not ("vcruntime140_1.dll" or "vcomp140.dll");
        });
        using LogCapture capture = LogCapture.Start();

        RawReader.Missing(new DllNotFoundException("libraw.dll"), check);
        RawReader.Missing(new DllNotFoundException("libraw.dll"), check);

        string[] inOrder = ["vcruntime140.dll", "vcruntime140_1.dll", "msvcp140.dll", "vcomp140.dll"];
        Assert.Equal(inOrder, asked);
        LogCapture.Captured line = Assert.Single(capture.Events, e => e.Logger == nameof(RawReader));
        Assert.Equal(NLog.LogLevel.Warn, line.Level);
        Assert.Equal("libraw cannot be loaded; Visual C++ runtime: missing [vcruntime140_1.dll, vcomp140.dll]", line.Message);
    }

    /// <summary>
    /// The reader's own failures go through the process's one check (R-074): two failures leave it
    /// asked and write at most one line, whichever test reached it first, where a check made per
    /// failure would write a line, and search the PATH, at each of a series' 174 failures.
    /// </summary>
    [Fact]
    public void R074_TheReadersFailuresShareTheProcesssOneCheck()
    {
        using LogCapture capture = LogCapture.Start();

        RawReader.Missing(new DllNotFoundException("libraw.dll"));
        RawReader.Missing(new DllNotFoundException("libraw.dll"));

        Assert.True(RawReader.Runtime.HasAsked);
        Assert.True(capture.Events.Count(e => e.Logger == nameof(RawReader)) <= 1, "two failures wrote more than one runtime line");
    }

    /// <summary>
    /// When the four load, the line says that none is missing, which a bug report needs to know,
    /// since the user's message names the runtime as the likely cause; it claims no more, since a
    /// runtime too old for libraw's DLLs may still have the four (R-074, ADR 0030).
    /// </summary>
    [Fact]
    public void R074_WhenTheFourLoad_TheLogSaysNoneIsMissing()
    {
        RawReader.RuntimeCheck check = new(_ => true);
        using LogCapture capture = LogCapture.Start();

        RawReader.Missing(new BadImageFormatException("raw_r.dll"), check);

        LogCapture.Captured line = Assert.Single(capture.Events, e => e.Logger == nameof(RawReader));
        Assert.Equal(NLog.LogLevel.Warn, line.Level);
        Assert.Equal(
            "libraw cannot be loaded; Visual C++ runtime: [vcruntime140.dll, vcruntime140_1.dll, msvcp140.dll, vcomp140.dll] load, so none is missing",
            line.Message);
    }

    /// <summary>
    /// The runtime DLLs the reader asks for are the ones LibRaw's DLLs import, read from the import
    /// tables of the DLLs the build brings for Windows (R-074): a Sdcb.LibRaw.runtime.win64 whose
    /// DLLs import another runtime DLL turns this red until <see cref="RawReader.RuntimeFiles"/>
    /// names it. The tables are read as bytes, so this runs on any system.
    /// </summary>
    [Fact]
    public void R074_TheRuntimeFilesAreWhatLibrawsDllsImport()
    {
        string native = Path.Combine(AppContext.BaseDirectory, "runtimes", "win-x64", "native");
        Assert.True(Directory.Exists(native), $"{native} does not exist: the test reads the DLLs the build brings for Windows");
        string[] dlls = Directory.GetFiles(native, "*.dll");
        Assert.NotEmpty(dlls);

        HashSet<string> runtime = new(StringComparer.OrdinalIgnoreCase);
        foreach (string dll in dlls)
        {
            runtime.UnionWith(Imports(dll).Where(name => VcRuntimeDll().IsMatch(name)));
        }

        Assert.Equal(
            RawReader.RuntimeFiles.Select(f => f.ToLowerInvariant()).Order(StringComparer.Ordinal),
            runtime.Select(f => f.ToLowerInvariant()).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// The names of the DLLs a PE file imports, from its import directory. A file that also
    /// delay-loads DLLs fails the test, since its delay-load directory is not read: LibRaw
    /// 0.21.1's four DLLs have none.
    /// </summary>
    /// <param name="path">The DLL to read.</param>
    /// <returns>The imported DLLs' names, as the file spells them.</returns>
    private static List<string> Imports(string path)
    {
        using PEReader pe = new(File.OpenRead(path));
        Assert.True(pe.PEHeaders.PEHeader!.DelayImportTableDirectory.Size == 0, $"{Path.GetFileName(path)} delay-loads DLLs, which this test does not read");
        DirectoryEntry table = pe.PEHeaders.PEHeader!.ImportTableDirectory;
        System.Reflection.Metadata.BlobReader descriptors = pe.GetSectionData(table.RelativeVirtualAddress).GetReader();
        List<string> names = [];
        while (true)
        {
            // IMAGE_IMPORT_DESCRIPTOR: five 32-bit fields, the name's address the fourth; an
            // all-zero descriptor ends the table.
            descriptors.ReadInt32();
            descriptors.ReadInt32();
            descriptors.ReadInt32();
            int name = descriptors.ReadInt32();
            descriptors.ReadInt32();
            if (name == 0)
            {
                return names;
            }

            System.Reflection.Metadata.BlobReader text = pe.GetSectionData(name).GetReader();
            StringBuilder sb = new();
            for (byte b = text.ReadByte(); b != 0; b = text.ReadByte())
            {
                sb.Append((char)b);
            }

            names.Add(sb.ToString());
        }
    }

    // The DLLs of the Visual C++ v14 redistributable (vcruntime, msvcp, vcomp, concrt, vccorlib,
    // vcamp, mfc and mfcm), not only the four LibRaw 0.21.1 imports, so a build importing another
    // turns the test red. Read from the import directory; Imports refuses a DLL with delay loads.
    [GeneratedRegex(@"^(vcruntime|msvcp|vcomp|concrt|vccorlib|vcamp|mfcm?)\d", RegexOptions.IgnoreCase)]
    private static partial Regex VcRuntimeDll();
}
