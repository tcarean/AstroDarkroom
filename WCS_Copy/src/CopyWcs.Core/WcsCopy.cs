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

// WcsCopy.cs - the copy run, shared by the console and the window: reads both headers, checks
// the pixel grid, selects the cards, lists or copies them, and returns what happened as lines
// already worded, so that each application only prints or shows them (ADR 0001, ADR 0007).

using static CopyWcs.Ui;

namespace CopyWcs;

/// <summary>What a copy run is asked to do.</summary>
/// <param name="Source">The path of the plate-solved FITS the cards come from.</param>
/// <param name="Dest">The path of the FITS that receives them, and whose data area is kept.</param>
/// <param name="Output">The path of the file to write, or null to rewrite <paramref name="Dest"/> in place.</param>
/// <param name="IncludeOptical">True to copy the optical metadata keys beside the WCS keys.</param>
/// <param name="Force">True to copy even when the two images' dimensions differ.</param>
/// <param name="DryRun">True to list the cards that would be copied and write nothing.</param>
internal sealed record WcsCopyRequest(
    string Source,
    string Dest,
    string? Output,
    bool IncludeOptical,
    bool Force,
    bool DryRun);

/// <summary>How a copy run ended.</summary>
internal enum WcsCopyKind
{
    /// <summary>The cards were copied and the target file written.</summary>
    Copied,

    /// <summary>A dry run: the cards were selected and nothing was written.</summary>
    Previewed,

    /// <summary>The run stopped before writing; <see cref="WcsCopyOutcome.Error"/> says why.</summary>
    Refused,
}

/// <summary>
/// What a copy run did, as lines already worded in the run's language: the applications print
/// or show them and compose nothing about the copy themselves.
/// </summary>
/// <param name="Kind">How the run ended.</param>
/// <param name="Error">The one line that says why the run was refused; null unless <paramref name="Kind"/> is <see cref="WcsCopyKind.Refused"/>.</param>
/// <param name="Warnings">The warning lines, in the order they arose, each starting with the warning word; empty when there was none.</param>
/// <param name="Cards">The raw 80-character cards selected for the copy, in the source's order; empty when the run was refused before the selection.</param>
/// <param name="Target">The path of the file written; null unless <paramref name="Kind"/> is <see cref="WcsCopyKind.Copied"/>.</param>
/// <param name="Notes">The closing lines: the heading of a preview, or the count copied and the reminder to check the annotation; empty when the run was refused.</param>
internal sealed record WcsCopyOutcome(
    WcsCopyKind Kind,
    string? Error,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Cards,
    string? Target,
    IReadOnlyList<string> Notes);

/// <summary>The copy run: the one sequence both applications call.</summary>
internal static class WcsCopy
{
    /// <summary>
    /// Runs a copy: reads both headers, refuses a pixel grid that differs unless forced, warns
    /// about a source without <c>CTYPE1</c>, selects the cards, and either lists them (a dry
    /// run) or copies them verbatim, appends a HISTORY card and writes the target with the
    /// destination's data.
    /// </summary>
    /// <param name="request">What to copy, from where to where, and how; not null.</param>
    /// <returns>The outcome, with every line the applications print.</returns>
    internal static WcsCopyOutcome Run(WcsCopyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var warnings = new List<string>();

        FitsHeader source;
        try
        {
            source = FitsHeader.ReadPrimary(request.Source);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
        {
            return Refused(T($"Cannot read the source '{request.Source}': {ex.Message}",
                $"Nu pot citi sursa '{request.Source}': {ex.Message}"), warnings);
        }

        FitsHeader dest;
        try
        {
            dest = FitsHeader.ReadPrimary(request.Dest);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
        {
            return Refused(T($"Cannot read the destination '{request.Dest}': {ex.Message}",
                $"Nu pot citi destinatia '{request.Dest}': {ex.Message}"), warnings);
        }

        // The WCS holds only on the same pixel grid.
        var srcDim = (source.GetInt("NAXIS1"), source.GetInt("NAXIS2"));
        var dstDim = (dest.GetInt("NAXIS1"), dest.GetInt("NAXIS2"));
        if (srcDim != dstDim)
        {
            var msg = T(
                $"Different dimensions: source ({srcDim.Item1}, {srcDim.Item2}) vs destination ({dstDim.Item1}, {dstDim.Item2}).",
                $"Dimensiuni diferite: sursa ({srcDim.Item1}, {srcDim.Item2}) vs destinatie ({dstDim.Item1}, {dstDim.Item2}).");
            if (!request.Force)
            {
                return Refused(msg + T(" The WCS would be wrong. Use --force if you know what you are doing.",
                    " WCS-ul ar fi gresit. Foloseste --force daca stii ce faci."), warnings);
            }

            warnings.Add(T($"WARNING: {msg} Continuing anyway (--force).", $"ATENTIE: {msg} Continui oricum (--force)."));
        }

        if (!source.Contains("CTYPE1"))
        {
            warnings.Add(T("WARNING: the source does not look plate-solved (CTYPE1 is missing).",
                "ATENTIE: sursa nu pare plate-solved (lipseste CTYPE1)."));
        }

        var present = WcsMetadataKeys.SelectKeysToCopy(source, request.IncludeOptical);
        if (present.Count == 0)
        {
            return Refused(T("No relevant key found in the source. Nothing to copy.",
                "Nicio cheie relevanta gasita in sursa. Nimic de copiat."), warnings);
        }

        var cards = present.Select(k => source.GetCard(k)!).ToList();

        if (request.DryRun)
        {
            return new WcsCopyOutcome(WcsCopyKind.Previewed, null, warnings, cards, null,
                [T($"Keys that would be copied ({cards.Count}):", $"Chei care ar fi copiate ({cards.Count}):")]);
        }

        // The raw 80-byte card is copied, not a reparsed value: that keeps the source's
        // formatting and inline comment exactly (R-010).
        foreach (var k in present)
        {
            dest.SetCard(k, source.GetCard(k)!);
        }

        // The card is data other programs read, not interface text: English in both languages
        // (ADR 0004).
        dest.AddHistory("WCS copied from " + Path.GetFileName(request.Source));

        var target = request.Output ?? request.Dest;
        try
        {
            dest.WriteWithDataFrom(request.Dest, target);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return Refused(T($"Write error: {ex.Message}", $"Eroare la scriere: {ex.Message}"), warnings);
        }

        return new WcsCopyOutcome(WcsCopyKind.Copied, null, warnings, cards, target,
        [
            T($"Copied {cards.Count} keys into {target}.", $"Copiate {cards.Count} chei in {target}."),
            T("Check the annotation by eye: if the image was mirrored or flipped during processing, " +
              "the copied WCS will be wrong without any error.",
              "Verifica vizual adnotarea: daca imaginea a fost oglindita/flip-uita " +
              "pe parcursul procesarii, WCS-ul copiat va fi gresit fara sa dea eroare."),
        ]);
    }

    /// <summary>A refused outcome: the error line and the warnings raised before it.</summary>
    /// <param name="error">The one line that says why.</param>
    /// <param name="warnings">The warnings raised so far; the outcome keeps them.</param>
    /// <returns>The outcome, with no cards, no target and no notes.</returns>
    private static WcsCopyOutcome Refused(string error, List<string> warnings)
        => new(WcsCopyKind.Refused, error, warnings, [], null, []);
}
