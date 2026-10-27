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

using System.Text.Json;
using System.Text.RegularExpressions;
using AstroDarkroom.Core;

namespace AstroDarkroom.Tests;

/// <summary>
/// The suite's consistency, read from the sibling projects of the photo repository: the
/// catalogue against the components kit.json bundles and the executables their project files
/// build (the rule "The catalogue and kit.json agree"), and the components' frameworks and
/// package versions (the rule "Same .NET and same packages across the suite"), and the icon
/// copies against the components' own files. Repository facts rather than behaviour of the
/// launcher, so not requirement-numbered, except the icon copies (R-010), which the window
/// shows. They return early, with the reason in the output, where the sibling folders are
/// not beside this project (a clone of this folder alone).
/// </summary>
public partial class SuiteTests
{
    /// <summary>One release.publish entry that publishes from a sibling project, with its output subfolder ("" is the root).</summary>
    private sealed record ComponentEntry(string Component, string ProjectFile, string Output);

    private static string Root => TestData.FindRepoRoot();

    private static string Sibling(string component) => Path.GetFullPath(Path.Combine(Root, "..", component));

    /// <summary>The entries of kit.json's release.publish whose project is in a sibling project ("../X/...").</summary>
    private static List<ComponentEntry> ComponentEntries()
    {
        using JsonDocument kit = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "kit.json")));
        List<ComponentEntry> entries = [];
        foreach (JsonElement entry in kit.RootElement.GetProperty("release").GetProperty("publish").EnumerateArray())
        {
            string project = entry.GetProperty("project").GetString()!;
            if (project.StartsWith("../", StringComparison.Ordinal))
            {
                string[] parts = project.Split('/');
                string output = entry.TryGetProperty("output", out JsonElement o) && o.ValueKind == JsonValueKind.String ? o.GetString()! : "";
                entries.Add(new ComponentEntry(parts[1], Path.GetFullPath(Path.Combine(Root, project)), output));
            }
        }

        return entries;
    }

    private static bool SiblingsPresent(List<ComponentEntry> entries) => entries.All(e => Directory.Exists(Sibling(e.Component)));

    [Fact]
    public void EveryCatalogueApp_IsAComponentOfTheRelease_AndTheReverse()
    {
        HashSet<string> components = ComponentEntries().Select(e => e.Component).ToHashSet(StringComparer.Ordinal);
        HashSet<string> catalogue = AppCatalogue.Apps.Select(a => a.Folder).ToHashSet(StringComparer.Ordinal);
        Assert.True(components.SetEquals(catalogue),
            $"kit.json components: {string.Join(", ", components.Order())}; catalogue: {string.Join(", ", catalogue.Order())}");
    }

    /// <summary>
    /// Each component has one entry, a WinExe project published at the root, and the catalogue's
    /// window exe is that project's AssemblyName plus ".exe" (AssemblyName defaults to the
    /// project file name). A console entry (OutputType Exe) is refused: a console's base runtime
    /// collides with the windows' desktop runtime in the flat folder (ADR 0002).
    /// </summary>
    [Fact]
    public void EveryWindowExe_IsWhatTheComponentsProjectFileBuilds_AndNoConsoleIsBundled()
    {
        List<ComponentEntry> entries = ComponentEntries();
        if (!SiblingsPresent(entries))
        {
            return;   // the sibling projects are not beside this one
        }

        foreach (SuiteApp app in AppCatalogue.Apps)
        {
            ComponentEntry entry = Assert.Single(entries, e => e.Component == app.Folder);
            Assert.Equal("", entry.Output);
            string csproj = File.ReadAllText(entry.ProjectFile);
            Match output = OutputType().Match(csproj);
            Assert.True(output.Success, $"{entry.ProjectFile} has no <OutputType>");
            Assert.True(output.Groups[1].Value == "WinExe", $"{entry.ProjectFile}: OutputType {output.Groups[1].Value}; the suite bundles windows only");
            Match name = AssemblyName().Match(csproj);
            string exe = (name.Success ? name.Groups[1].Value : Path.GetFileNameWithoutExtension(entry.ProjectFile)) + ".exe";
            Assert.Equal(exe, app.WindowExe);
        }
    }

    /// <summary>
    /// Every lock file under a component's code folders (kit.json codeFolders; the test folders
    /// do not ship) is keyed net8.0 only, and a package two components both resolve has one
    /// resolved version, so the flat suite folder holds one copy of every library.
    /// </summary>
    [Fact]
    public void EveryComponent_TargetsNet8_AndSharedPackagesResolveToOneVersion()
    {
        List<ComponentEntry> entries = ComponentEntries();
        if (!SiblingsPresent(entries))
        {
            return;   // the sibling projects are not beside this one
        }

        Dictionary<string, Dictionary<string, string>> resolved = new(StringComparer.Ordinal);   // package -> component -> version
        List<string> problems = [];
        foreach (string component in entries.Select(e => e.Component).Distinct(StringComparer.Ordinal))
        {
            string root = Sibling(component);
            foreach (string lockFile in LockFiles(root))
            {
                string shown = Path.GetRelativePath(Path.GetDirectoryName(Root)!, lockFile).Replace('\\', '/');
                using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(lockFile));
                foreach (JsonProperty framework in doc.RootElement.GetProperty("dependencies").EnumerateObject())
                {
                    if (!framework.Name.StartsWith("net8.0", StringComparison.Ordinal))
                    {
                        problems.Add($"{shown}: framework {framework.Name}");
                    }

                    foreach (JsonProperty package in framework.Value.EnumerateObject())
                    {
                        if (package.Value.GetProperty("type").GetString() == "Project"
                            || !package.Value.TryGetProperty("resolved", out JsonElement version))
                        {
                            continue;
                        }

                        Dictionary<string, string> byComponent = resolved.TryGetValue(package.Name, out Dictionary<string, string>? d)
                            ? d
                            : resolved[package.Name] = new Dictionary<string, string>(StringComparer.Ordinal);
                        string v = version.GetString()!;
                        if (byComponent.TryGetValue(component, out string? seen) && seen != v)
                        {
                            problems.Add($"{shown}: {package.Name} {v}, but {seen} elsewhere in {component}");
                        }

                        byComponent[component] = v;
                    }
                }
            }
        }

        foreach ((string package, Dictionary<string, string> byComponent) in resolved)
        {
            if (byComponent.Values.Distinct(StringComparer.Ordinal).Count() > 1)
            {
                problems.Add($"{package}: " + string.Join(", ", byComponent.Select(kv => $"{kv.Key} {kv.Value}")));
            }
        }

        Assert.True(problems.Count == 0, "Suite drift:\n  " + string.Join("\n  ", problems));
        Assert.True(resolved.Count > 0, "no package was read from any lock file");
    }

    /// <summary>
    /// Each app's icon is a copy of the component's own, the ApplicationIcon of its window
    /// project, kept in Icon/ under the same file name (R-010, ADR 0011). The copy's presence
    /// is checked anywhere; its bytes against the component's file only beside the siblings.
    /// </summary>
    [Fact]
    public void R010_EveryAppsIcon_IsACopyOfTheComponentsOwn()
    {
        List<ComponentEntry> entries = ComponentEntries();
        bool siblings = SiblingsPresent(entries);
        foreach (SuiteApp app in AppCatalogue.Apps)
        {
            string copy = Path.Combine(Root, "Icon", app.Icon);
            Assert.True(File.Exists(copy), $"{app.Folder}: Icon/{app.Icon} is not in the repository");
            if (!siblings)
            {
                continue;   // the sibling projects are not beside this one
            }

            ComponentEntry entry = Assert.Single(entries, e => e.Component == app.Folder);
            Match icon = ApplicationIcon().Match(File.ReadAllText(entry.ProjectFile));
            Assert.True(icon.Success, $"{entry.ProjectFile} has no <ApplicationIcon>");
            string original = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(entry.ProjectFile)!, icon.Groups[1].Value));
            Assert.Equal(Path.GetFileName(original), app.Icon);
            Assert.True(File.ReadAllBytes(original).AsSpan().SequenceEqual(File.ReadAllBytes(copy)),
                $"Icon/{app.Icon} differs from {original}; copy the component's file over it");
        }
    }

    /// <summary>The packages.lock.json files under a component's codeFolders, bin/ and obj/ excluded.</summary>
    private static IEnumerable<string> LockFiles(string componentRoot)
    {
        using JsonDocument kit = JsonDocument.Parse(File.ReadAllText(Path.Combine(componentRoot, "kit.json")));
        foreach (JsonElement folder in kit.RootElement.GetProperty("codeFolders").EnumerateArray())
        {
            string dir = Path.Combine(componentRoot, folder.GetString()!.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(dir))
            {
                continue;
            }

            foreach (string f in Directory.EnumerateFiles(dir, "packages.lock.json", SearchOption.AllDirectories))
            {
                string rel = Path.GetRelativePath(componentRoot, f).Replace('\\', '/');
                if (!rel.Contains("/bin/", StringComparison.Ordinal) && !rel.Contains("/obj/", StringComparison.Ordinal))
                {
                    yield return f;
                }
            }
        }
    }

    [GeneratedRegex(@"<OutputType>\s*(\w+)\s*</OutputType>")]
    private static partial Regex OutputType();

    [GeneratedRegex(@"<AssemblyName>\s*([^<\s]+)\s*</AssemblyName>")]
    private static partial Regex AssemblyName();

    [GeneratedRegex(@"<ApplicationIcon>\s*([^<\s]+)\s*</ApplicationIcon>")]
    private static partial Regex ApplicationIcon();
}
