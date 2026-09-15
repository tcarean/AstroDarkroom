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

// SirilInstallTests.cs - ce se citeste din configurarea Siril, din scripturi si din
// stdout-ul lui siril-cli. Toate pe text, fara registru si fara Siril instalat: textele
// sunt copiate din masina pe care s-a masurat (Siril 1.4.4), nu inventate.

namespace FlatCheck.Wpf.Tests;

public class SirilInstallTests
{
    /// <summary>Sectiunea [gui] din config.1.4.ini, cu caile scrise ca in GKeyFile ("\\").</summary>
    const string Config =
        "[core]\n" +
        "wd=E:\\\\Astro\\\\sesiune\n" +
        "script_path=nu-asta\n" +
        "\n" +
        "[gui]\n" +
        "first_start=1.4.4\n" +
        "script_path=C:\\\\Users\\\\User\\\\AppData\\\\Roaming\\\\siril\\\\scripts;C:\\\\Program Files\\\\Siril\\\\scripts;\n" +
        "use_scripts_repository=true\n" +
        "selected_scripts=C:\\\\Users\\\\User\\\\AppData\\\\Local\\\\siril-scripts\\\\processing/GraXpert-AI.py;" +
        "C:\\\\Users\\\\User\\\\AppData\\\\Local\\\\siril-scripts\\\\preprocessing/OSC_Preprocessing_WithoutDark.ssf;" +
        "C:\\\\Users\\\\User\\\\AppData\\\\Local\\\\siril-scripts\\\\preprocessing/OSC_Preprocessing_WithoutDBF.ssf;" +
        "C:\\\\Users\\\\User\\\\AppData\\\\Local\\\\siril-scripts\\\\preprocessing/Sters.ssf;\n" +
        "startup_scripts=\n";

    // ------------------------------------------------------------------ config

    [Fact]
    public void TheConfigGivesTheScriptFoldersAndTheSelectedScripts_FromTheGuiSectionOnly()
    {
        var (dirs, files) = SirilInstall.ParseConfig(Config);

        Assert.Equal(new[] { @"C:\Users\User\AppData\Roaming\siril\scripts", @"C:\Program Files\Siril\scripts" }, dirs);
        Assert.Equal(4, files.Count);
        Assert.Equal(@"C:\Users\User\AppData\Local\siril-scripts\preprocessing/OSC_Preprocessing_WithoutDark.ssf", files[1]);
    }

    /// <summary>
    /// Doar .ssf, doar ce exista, fara dubluri, in ordinea numelui - adica lista din
    /// meniul Siril. Scriptul .py e al mediului Python si nu e de preprocesare.
    /// </summary>
    [Fact]
    public void TheScriptsAreTheExistingSsfFilesFromBothKeys_SortedByName()
    {
        var scripts = SirilInstall.ScriptsFrom(Config,
            fileExists: p => Path.GetFileName(p) != "Sters.ssf",
            listScripts: dir => dir.Contains("Program Files")
                ? new[] { @"C:\Program Files\Siril\scripts\OSC_Preprocessing.ssf", @"C:\Program Files\Siril\scripts\Mono_Preprocessing.ssf" }
                : Array.Empty<string>());

        Assert.Equal(new[]
        {
            "Mono_Preprocessing.ssf",
            "OSC_Preprocessing.ssf",
            "OSC_Preprocessing_WithoutDark.ssf",
            "OSC_Preprocessing_WithoutDBF.ssf",
        }, scripts.Select(Path.GetFileName));
        Assert.All(scripts, p => Assert.False(p.Contains('/')));  // cai normalizate
    }

    [Fact]
    public void WithoutAConfigThereAreNoScripts()
    {
        Assert.Empty(SirilInstall.ScriptsFrom(null, _ => true, _ => Array.Empty<string>()));
    }

    /// <summary>WithoutDark e propus indiferent de pozitia lui in lista; fara el, primul.</summary>
    [Fact]
    public void TheDefaultScriptIsWithoutDarkWhenPresent_OtherwiseTheFirst()
    {
        var with = new SirilInstall("cli", new[] { @"C:\s\A.ssf", @"C:\s\OSC_Preprocessing_WithoutDark.ssf", @"C:\s\Z.ssf" });
        var without = new SirilInstall("cli", new[] { @"C:\s\A.ssf", @"C:\s\Z.ssf" });
        var none = new SirilInstall(null, Array.Empty<string>());

        Assert.Equal(@"C:\s\OSC_Preprocessing_WithoutDark.ssf", with.DefaultScriptPath);
        Assert.Equal(@"C:\s\A.ssf", without.DefaultScriptPath);
        Assert.Null(none.DefaultScriptPath);
        Assert.False(none.HasCli);
        Assert.Equal(SirilSession.PreprocessingScript, SirilInstall.DefaultScript);
    }

    // ------------------------------------------------------------------ scriptul

    internal const string WithoutDark =
        "############################################\n" +
        "# Script for Siril 1.0\n" +
        "############################################\n" +
        "\n" +
        "requires 1.2.0\n" +
        "\n" +
        "# Convert Bias Frames to .fit files\n" +
        "cd biases\n" +
        "convert bias -out=../process\n" +
        "cd ../process\n" +
        "stack bias rej 3 3 -nonorm\n" +
        "cd ..\n" +
        "cd flats\n" +
        "convert flat -out=../process\n" +
        "cd ../process\n" +
        "calibrate flat -bias=bias_stacked\n" +
        "stack pp_flat rej 3 3 -norm=mul\n" +
        "cd ..\n" +
        "cd lights\n" +
        "convert light -out=../process\n" +
        "cd ../process\n" +
        "calibrate light -bias=bias_stacked -flat=pp_flat_stacked -cfa -equalize_cfa -debayer\n" +
        "register pp_light\n" +
        "stack r_pp_light rej 3 3 -norm=addscale -output_norm -out=../result\n" +
        "cd ..\n" +
        "close\n";

    [Fact]
    public void TheCommandsAreTheNonCommentLines_InOrder()
    {
        var cmds = SirilScript.Commands(WithoutDark);

        Assert.Equal(20, cmds.Count);
        Assert.Equal("requires 1.2.0", cmds[0]);
        Assert.Equal("cd biases", cmds[1]);
        Assert.Equal("close", cmds[^1]);
        Assert.Empty(SirilScript.Commands(null));
    }

    /// <summary>
    /// Intrarile sunt "cd <nume simplu>"; "cd ../process" e iesirea si "cd .." e
    /// intoarcerea, si niciuna nu e o cerinta a folderului.
    /// </summary>
    [Fact]
    public void TheRequiredFoldersAreTheOnesTheScriptEntersDirectly()
    {
        Assert.Equal(new[] { "biases", "flats", "lights" }, SirilScript.RequiredFolders(WithoutDark));

        string stock14 = "requires 1.3.4\ncd biases\ncd ../process\ncd ..\ncd flats\ncd ../process\ncd ..\ncd darks\ncd ..\ncd lights\ncd ..\nclose\n";
        Assert.Equal(new[] { "biases", "flats", "darks", "lights" }, SirilScript.RequiredFolders(stock14));

        Assert.Empty(SirilScript.RequiredFolders("requires 1.2.0\nload x\nclose\n"));
    }

    // ------------------------------------------------------------------ stdout

    [Theory]
    [InlineData("log: Running command: convert", "Log", "Running command: convert")]
    [InlineData("log: Script execution finished successfully.", "Log", "Script execution finished successfully.")]
    [InlineData("1789481943: running command load", "Other", "1789481943: running command load")]
    [InlineData("seqfile 'flat_.seq' already exists, not overwriting", "Other", "seqfile 'flat_.seq' already exists, not overwriting")]
    [InlineData("", "Other", "")]
    public void LinesAreSortedIntoLogAndOther(string raw, string kind, string text)
    {
        var line = SirilLogLine.Parse(raw);

        Assert.Equal(Enum.Parse<SirilLineKind>(kind), line.Kind);
        Assert.Equal(text, line.Text);
    }

    /// <summary>Cele doua forme ale progresului: cu eticheta la inceput, apoi doar procentul, cu ESC-uri in fata.</summary>
    [Fact]
    public void ProgressLinesGiveTheLabelAndThePercent_EscapesStripped()
    {
        var start = SirilLogLine.Parse("progress: Converting files, 0.00%");
        var update = SirilLogLine.Parse("\x1b[A\x1b[2KTprogress: 66.67%");
        var labelled = SirilLogLine.Parse("progress: Global star registration. Processing image 2 (pp_light_00002.fit), 33.33%");
        var end = SirilLogLine.Parse("progress: , 100.00%");

        Assert.Equal(SirilLineKind.Progress, start.Kind);
        Assert.Equal("Converting files", start.Label);
        Assert.Equal(0, start.Percent);

        Assert.Equal(SirilLineKind.Progress, update.Kind);
        Assert.Equal("", update.Label);
        Assert.Equal(66.67, update.Percent, 9);

        Assert.Equal("Global star registration. Processing image 2 (pp_light_00002.fit)", labelled.Label);
        Assert.Equal(33.33, labelled.Percent, 9);

        Assert.Equal("", end.Label);
        Assert.Equal(100, end.Percent);
    }

    [Fact]
    public void ACommandStartIsRecognisedWithItsVerb()
    {
        Assert.True(SirilLogLine.Parse("log: Running command: cd").IsCommandStart(out string verb));
        Assert.Equal("cd", verb);

        Assert.False(SirilLogLine.Parse("log: Reading FITS: file x.fit").IsCommandStart(out _));
        Assert.False(SirilLogLine.Parse("1789481943: running command cd").IsCommandStart(out _));
    }
}
