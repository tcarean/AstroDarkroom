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

using System.Text.RegularExpressions;

namespace AstroDarkroom.Tests;

/// <summary>
/// Reads THIRD-PARTY-NOTICES.md, which the suite folder carries as THIRD-PARTY-NOTICES.txt
/// (R-017): its sections by heading, the fields of the list each opens with ("- Packages: ..."),
/// and its words with the quote markers and the line breaks taken out, so a sentence of a quoted
/// licence is found however its lines were broken.
/// </summary>
internal static partial class Notices
{
    /// <summary>The file's text, its line ends as "\n".</summary>
    public static string Text =>
        File.ReadAllText(Path.Combine(TestData.FindRepoRoot(), "THIRD-PARTY-NOTICES.md")).Replace("\r\n", "\n", StringComparison.Ordinal);

    /// <summary>Each "## " section's text, from its heading line to the next heading, by heading.</summary>
    public static Dictionary<string, string> Sections()
    {
        string text = Text;
        MatchCollection headings = Heading().Matches(text);
        Dictionary<string, string> sections = new(StringComparer.Ordinal);
        for (int i = 0; i < headings.Count; i++)
        {
            int start = headings[i].Index + headings[i].Length;
            int end = i + 1 < headings.Count ? headings[i + 1].Index : text.Length;
            sections.Add(headings[i].Groups[1].Value.Trim(), text[start..end]);
        }

        return sections;
    }

    /// <summary>
    /// The value of a section's "- Name: value" line, the indented lines that continue it joined
    /// with single spaces, or null when the section has none.
    /// </summary>
    public static string? Field(string section, string name)
    {
        Match m = Regex.Match(section, "^- " + Regex.Escape(name) + @":[ \t]*(.+(?:\n[ \t]+\S.*)*)", RegexOptions.Multiline);
        return m.Success ? Space().Replace(m.Groups[1].Value, " ").Trim() : null;
    }

    /// <summary>The names in backticks on a field's line, each with the version written right after it, or "" for none.</summary>
    public static List<(string Name, string Version)> Named(string value) =>
        NameAndVersion().Matches(value).Select(m => (m.Groups[1].Value, m.Groups[2].Value)).ToList();

    /// <summary>A text's words: the quote markers at the line starts removed and every run of white space made one space.</summary>
    public static string Words(string text) => Space().Replace(QuoteMarker().Replace(text, ""), " ");

    [GeneratedRegex(@"^##[ \t]+(.+)$", RegexOptions.Multiline)]
    private static partial Regex Heading();

    [GeneratedRegex(@"`([^`]+)`(?:[ \t]+(\d[\w.\-]*))?")]
    private static partial Regex NameAndVersion();

    [GeneratedRegex(@"^[ \t]*>[ \t]?", RegexOptions.Multiline)]
    private static partial Regex QuoteMarker();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Space();
}
