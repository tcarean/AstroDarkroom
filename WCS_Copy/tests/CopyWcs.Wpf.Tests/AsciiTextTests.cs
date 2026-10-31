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

using System.Globalization;
using System.Text;

namespace CopyWcs.Wpf.Tests;

/// <summary>
/// The table of <c>AsciiText</c> against the runtime's own canonical decomposition. In this
/// project, since it runs with culture data: the core tests run with InvariantGlobalization,
/// where <c>string.Normalize</c> returns a text unchanged (ADR 0010).
/// </summary>
public class AsciiTextTests
{
    /// <summary>
    /// For every character of the Basic Multilingual Plane, the table gives what FormD gives
    /// with the combining marks dropped: the first character of the decomposition when it is
    /// ASCII (nothing but marks follows it, as the table was generated), nothing for a combining
    /// mark standing alone, <c>?</c> for any other character outside ASCII (R-011).
    /// </summary>
    [Fact]
    public void R011_TheBaseCharacterTable_IsTheRuntimesFormDWithoutTheMarks_OverTheWholePlane()
    {
        // a with breve, by its code point so this file stays ASCII: culture data is loaded here.
        var aBreve = ((char)0x0103).ToString();
        Assert.NotEqual(aBreve, aBreve.Normalize(NormalizationForm.FormD));

        var differences = new List<string>();
        var withABase = 0;
        for (var cp = 0x80; cp < 0x10000; cp++)
        {
            if (cp is >= 0xD800 and <= 0xDFFF)
            {
                continue;
            }

            var s = ((char)cp).ToString();
            string decomposed;
            try
            {
                decomposed = s.Normalize(NormalizationForm.FormD);
            }
            catch (ArgumentException)
            {
                continue;   // not a Unicode scalar value the runtime normalizes
            }

            var ascii = new string(decomposed.Where(c => c < 0x80 && CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
            var isMark = CharUnicodeInfo.GetUnicodeCategory((char)cp) == UnicodeCategory.NonSpacingMark;
            var expected = ascii.Length > 0 ? ascii : isMark ? "" : "?";
            if (ascii.Length > 0)
            {
                withABase++;
            }

            var actual = AsciiText.Of(s);
            if (actual != expected)
            {
                differences.Add($"U+{cp:X4}: expected '{expected}', got '{actual}'");
            }
        }

        Assert.Empty(differences);
        Assert.Equal(495, withABase);   // the table's size on .NET 8.0.33
    }
}
