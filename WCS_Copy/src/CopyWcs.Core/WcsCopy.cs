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
// the pixel grid, selects the cards and the destination's old plate-solve cards to drop (R-016),
// lists them, or drops those and copies the others, and returns what happened as lines
// already worded, so that each application only prints or shows them (ADR 0001, ADR 0007). The
// run also writes its own lines to the diagnostic log, in English, never the worded ones (R-042,
// R-046, ADR 0009).

using NLog;
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
/// <param name="Dropped">The one line naming the destination's old plate-solve keys the run drops, or would drop in a preview (R-016); null when there is none to drop or the run was refused.</param>
internal sealed record WcsCopyOutcome(
    WcsCopyKind Kind,
    string? Error,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Cards,
    string? Target,
    IReadOnlyList<string> Notes,
    string? Dropped);

/// <summary>The copy run: the one sequence both applications call.</summary>
internal static class WcsCopy
{
    /// <summary>The copy run's lines in the diagnostic log: its start and done lines, the selection, every card, the refusals and the warnings (R-042).</summary>
    private static readonly Logger Log = LogManager.GetLogger("WcsCopy");

    /// <summary>
    /// Runs a copy: reads both headers, refuses a pixel grid that differs unless forced, warns
    /// about a source without <c>CTYPE1</c>, selects the cards and the destination's old
    /// plate-solve cards to drop (R-016), and either lists them (a dry run) or drops those,
    /// copies the selected cards verbatim, appends a HISTORY card and writes the target with the
    /// destination's data. Every step writes its line to the diagnostic log (R-042); a refusal's
    /// exception goes there with its stack, the outcome keeps its message only.
    /// </summary>
    /// <param name="request">What to copy, from where to where, and how; not null.</param>
    /// <returns>The outcome, with every line the applications print.</returns>
    internal static WcsCopyOutcome Run(WcsCopyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        // The start line holds the request; the done line, or the "ended without an outcome"
        // line of a run that threw, closes it with the time of each stage reached.
        using RunClock clock = RunClock.Start(Log, "copy", LogText.Request(request));
        var warnings = new List<string>();

        FitsHeader source;
        try
        {
            source = FitsHeader.ReadPrimary(request.Source);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
        {
            Log.Warn(ex, "cannot read the source " + request.Source);
            clock.Lap("source");
            clock.Done("refused: cannot read the source");
            return Refused(T($"Cannot read the source '{request.Source}': {ex.Message}",
                $"Nu pot citi sursa '{request.Source}': {ex.Message}"), warnings);
        }

        clock.Lap("source");

        FitsHeader dest;
        try
        {
            dest = FitsHeader.ReadPrimary(request.Dest);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
        {
            Log.Warn(ex, "cannot read the destination " + request.Dest);
            clock.Lap("dest");
            clock.Done("refused: cannot read the destination");
            return Refused(T($"Cannot read the destination '{request.Dest}': {ex.Message}",
                $"Nu pot citi destinatia '{request.Dest}': {ex.Message}"), warnings);
        }

        clock.Lap("dest");

        // The WCS holds only on the same pixel grid.
        var srcDim = (source.GetInt("NAXIS1"), source.GetInt("NAXIS2"));
        var dstDim = (dest.GetInt("NAXIS1"), dest.GetInt("NAXIS2"));
        if (srcDim != dstDim)
        {
            string sizes = "source " + LogText.Dimensions(srcDim.Item1, srcDim.Item2)
                           + ", destination " + LogText.Dimensions(dstDim.Item1, dstDim.Item2);
            var msg = T(
                $"Different dimensions: source ({srcDim.Item1}, {srcDim.Item2}) vs destination ({dstDim.Item1}, {dstDim.Item2}).",
                $"Dimensiuni diferite: sursa ({srcDim.Item1}, {srcDim.Item2}) vs destinatie ({dstDim.Item1}, {dstDim.Item2}).");
            if (!request.Force)
            {
                Log.Warn("dimensions differ: " + sizes + "; not forced, nothing is written");
                clock.Lap("select");
                clock.Done("refused: dimensions differ (" + sizes + ")");
                return Refused(msg + T(" The WCS would be wrong. Use --force if you know what you are doing.",
                    " WCS-ul ar fi gresit. Foloseste --force daca stii ce faci."), warnings);
            }

            Log.Warn("dimensions differ: " + sizes + "; forced, the copy goes on");
            warnings.Add(T($"WARNING: {msg} Continuing anyway (--force).", $"ATENTIE: {msg} Continui oricum (--force)."));
        }

        if (!source.Contains("CTYPE1"))
        {
            Log.Warn("the source has no CTYPE1, it does not look plate-solved; the copy goes on");
            warnings.Add(T("WARNING: the source does not look plate-solved (CTYPE1 is missing).",
                "ATENTIE: sursa nu pare plate-solved (lipseste CTYPE1)."));
        }

        var present = WcsMetadataKeys.SelectKeysToCopy(source, request.IncludeOptical);
        if (present.Count == 0)
        {
            Log.Warn("no relevant key in the source; nothing is written");
            clock.Lap("select");
            clock.Done("refused: no relevant key in the source");
            return Refused(T("No relevant key found in the source. Nothing to copy.",
                "Nicio cheie relevanta gasita in sursa. Nimic de copiat."), warnings);
        }

        var cards = present.Select(k => source.GetCard(k)!).ToList();

        // A plate solve replaces the destination's own whole (R-016): a card of the old one that
        // the source lacks would stay beside the copied ones, and Siril 1.4.4 reads a CD matrix
        // over PC and CDELT (ADR 0011). Only a source with a plate solve has one to give.
        List<string> drop = source.Contains("CTYPE1") ? WcsMetadataKeys.SelectKeysToDrop(source, dest) : [];
        string counts = LogSelection(present, cards, dest, request.IncludeOptical, drop);
        clock.Lap("select");

        string names = string.Join(", ", drop);
        if (request.DryRun)
        {
            clock.Done("previewed " + LogText.Int(cards.Count) + " keys " + counts);
            return new WcsCopyOutcome(WcsCopyKind.Previewed, null, warnings, cards, null,
                [T($"Keys that would be copied ({cards.Count}):", $"Chei care ar fi copiate ({cards.Count}):")],
                drop.Count == 0 ? null : T(
                    $"Old plate-solve keys of the destination that would be dropped ({drop.Count}): {names}",
                    $"Chei vechi de plate solve ale destinatiei care ar fi eliminate ({drop.Count}): {names}"));
        }

        foreach (var k in drop)
        {
            dest.RemoveCards(k);
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
            Log.Error(ex, "the write of " + target + " failed");
            clock.Lap("write");
            clock.Done("refused: the write failed");
            return Refused(T($"Write error: {ex.Message}", $"Eroare la scriere: {ex.Message}"), warnings);
        }

        clock.Lap("write");
        clock.Done("copied " + LogText.Int(cards.Count) + " keys into " + target + " " + counts);
        return new WcsCopyOutcome(WcsCopyKind.Copied, null, warnings, cards, target,
        [
            T($"Copied {cards.Count} keys into {target}.", $"Copiate {cards.Count} chei in {target}."),
            T("Check the annotation by eye: if the image was mirrored or flipped during processing, " +
              "the copied WCS will be wrong without any error.",
              "Verifica vizual adnotarea: daca imaginea a fost oglindita/flip-uita " +
              "pe parcursul procesarii, WCS-ul copiat va fi gresit fara sa dea eroare."),
        ],
        drop.Count == 0 ? null : T(
            $"Dropped {drop.Count} old plate-solve keys of the destination: {names}.",
            $"Eliminate {drop.Count} chei vechi de plate solve ale destinatiei: {names}."));
    }

    /// <summary>
    /// Writes the selection to the diagnostic log, before any card is set or dropped: the count
    /// of keys per list, and at Debug one line per key with the source's card and the
    /// destination's card it replaces (<c>none</c>, <c>same</c>, or that card), then one line per
    /// card of the destination dropped (R-016), the one record a wrong WCS can be diagnosed from
    /// (R-042).
    /// </summary>
    /// <param name="present">The keys selected, in the copy's order.</param>
    /// <param name="cards">The source's cards of those keys, in the same order.</param>
    /// <param name="dest">The destination's header, still as read.</param>
    /// <param name="includeOptical">True when the optical keys were asked for.</param>
    /// <param name="drop">The destination's keys the copy drops, in its order; empty when none.</param>
    /// <returns>The counts for the done line: <c>(new 14, replaced 1, identical 0, dropped 0)</c>, the dropped ones counted by key.</returns>
    private static string LogSelection(List<string> present, List<string> cards, FitsHeader dest, bool includeOptical, List<string> drop)
    {
        int wcs = present.Count(k => WcsMetadataKeys.WcsKeys.Contains(k));
        int optical = present.Count(k => WcsMetadataKeys.OpticalKeys.Contains(k));
        Log.Debug("selected " + LogText.Int(present.Count) + " keys: " + LogText.Int(wcs) + " WCS, "
                  + LogText.Int(present.Count - wcs - optical) + " SIP, " + LogText.Int(optical)
                  + " optical (optical keys asked: " + LogText.Yes(includeOptical) + ")");

        int added = 0, replaced = 0, identical = 0;
        for (var i = 0; i < present.Count; i++)
        {
            string? old = dest.GetCard(present[i]);
            string was;
            if (old is null)
            {
                added++;
                was = "none";
            }
            else if (string.Equals(old, cards[i], StringComparison.Ordinal))
            {
                identical++;
                was = "same";
            }
            else
            {
                replaced++;
                was = LogText.Card(old);
            }

            if (Log.IsDebugEnabled)
            {
                Log.Debug("card " + present[i] + ": " + LogText.Card(cards[i]) + " | dest: " + was);
            }
        }

        if (Log.IsDebugEnabled)
        {
            foreach (var key in drop)
            {
                foreach (var card in dest.CardsOf(key))
                {
                    Log.Debug("drop " + key + ": " + LogText.Card(card));
                }
            }
        }

        return "(new " + LogText.Int(added) + ", replaced " + LogText.Int(replaced) + ", identical " + LogText.Int(identical)
               + ", dropped " + LogText.Int(drop.Count) + ")";
    }

    /// <summary>A refused outcome: the error line and the warnings raised before it.</summary>
    /// <param name="error">The one line that says why.</param>
    /// <param name="warnings">The warnings raised so far; the outcome keeps them.</param>
    /// <returns>The outcome, with no cards, no target, no notes and nothing dropped.</returns>
    private static WcsCopyOutcome Refused(string error, List<string> warnings)
        => new(WcsCopyKind.Refused, error, warnings, [], null, [], null);
}
