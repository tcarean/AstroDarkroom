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
// that the three paths name three files (R-019), the pixel grid, and that the destination's own
// plate solve is no mirror of the source's (R-017),
// selects the cards and the destination's old plate-solve cards to drop (R-016), lists them, or
// drops those and the destination's CHECKSUM (R-018) and copies the others, and returns what
// happened as lines
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
/// <param name="Force">True to copy even when the two images' dimensions differ or the destination's own plate solve is the mirror image of the source's (R-005, R-017).</param>
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
/// <param name="Cards">The raw 80-character cards selected for the copy, in the source's order, a long string's CONTINUE cards under the card they continue (R-010), so there may be more cards than keys; empty when the run was refused before the selection.</param>
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

    /// <summary>The keyword of the HDU's checksum, which a copy drops from the destination (R-018).</summary>
    private const string Checksum = "CHECKSUM";

    /// <summary>
    /// Runs a copy: reads both headers, refuses paths that name one file twice (R-019), refuses
    /// a pixel grid that differs unless forced, warns about a source without <c>CTYPE1</c>,
    /// refuses a destination whose own plate solve is the
    /// mirror image of the source's unless forced (R-017), selects the cards and the destination's old
    /// plate-solve cards to drop (R-016), and either lists them (a dry run) or drops those and
    /// the destination's CHECKSUM (R-018), copies the selected cards verbatim, appends a HISTORY
    /// card and writes the target with the destination's data, a failed write's message naming
    /// the target and not its temporary file (R-014). Every step writes its line to the
    /// diagnostic log (R-042); a refusal's exception goes there with its stack, the outcome keeps
    /// its message only.
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

        // The three paths name three files, told by the file each opens and not by its spelling
        // (R-019, ADR 0018): a new file that is the source would overwrite the plate-solved
        // source, one that is the destination would be written over the file its data is read
        // from, and a source that is the destination would be copied into itself.
        if (PathClash(request) is { } clash)
        {
            Log.Warn(clash.Line + "; nothing is written");
            clock.Lap("select");
            clock.Done("refused: " + clash.Reason);
            return Refused(clash.Error, warnings);
        }

        // The WCS holds only on the same pixel grid, and a file with no size in its primary header
        // has no grid there to compare: its image, if any, is in an extension this tool never
        // reads, so the copy is refused whatever the force says (R-060, ADR 0028). The source is
        // checked first.
        var srcDim = (source.GetInt("NAXIS1"), source.GetInt("NAXIS2"));
        var dstDim = (dest.GetInt("NAXIS1"), dest.GetInt("NAXIS2"));
        foreach ((string who, (int W, int H) dim) in new[] { ("source", srcDim), ("destination", dstDim) })
        {
            if (dim.W >= 0 && dim.H >= 0)
            {
                continue;
            }

            Log.Warn("no image size in the " + who + " (NAXIS1 or NAXIS2 missing): "
                     + LogText.Dimensions(dim.W, dim.H) + "; nothing is written");
            clock.Lap("select");
            clock.Done("refused: no image size in the " + who);
            return Refused(who == "source"
                ? T("The source has no image size in its main header (NAXIS1 or NAXIS2 is missing): its image may be in an extension, which this tool does not read.",
                    "Sursa nu are dimensiunea imaginii in header-ul principal (lipseste NAXIS1 sau NAXIS2): imaginea poate fi intr-o extensie, pe care unealta nu o citeste.")
                : T("The destination has no image size in its main header (NAXIS1 or NAXIS2 is missing): its image may be in an extension, which this tool does not read.",
                    "Destinatia nu are dimensiunea imaginii in header-ul principal (lipseste NAXIS1 sau NAXIS2): imaginea poate fi intr-o extensie, pe care unealta nu o citeste."), warnings);
        }

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

        // A destination with a plate solve of its own tells which way its pixels lie: one that is
        // the mirror image of the source's means the image was mirrored or flipped after the
        // source was solved, and any copy would point the wrong way (R-017, ADR 0012). The
        // comparison is logged whatever its result, so a copy that slipped through leaves a trace.
        bool? mirrored = WcsMetadataKeys.Mirrored(source, dest);
        string dets = "source det " + LogText.Signed(WcsMetadataKeys.Determinant(source))
                      + ", destination det " + LogText.Signed(WcsMetadataKeys.Determinant(dest));
        if (mirrored is null)
        {
            Log.Debug("orientation not compared: " + dets);
        }
        else if (mirrored == false)
        {
            Log.Debug("same orientation: " + dets);
        }
        else
        {
            if (!request.Force)
            {
                Log.Warn("mirrored plate solve: " + dets + "; not forced, nothing is written");
                clock.Lap("select");
                clock.Done("refused: mirrored plate solve (" + dets + ")");
                return Refused(T(
                    "The destination's own plate solve is a mirror image of the source's: the image was mirrored or flipped "
                    + "after the source was solved, so the copied WCS would be wrong. Flip the image back first, or keep its "
                    + "own plate solve. Use --force if you know what you are doing.",
                    "Plate solve-ul destinatiei e in oglinda fata de cel al sursei: imaginea a fost oglindita/flip-uita "
                    + "dupa ce sursa a fost rezolvata, asa ca WCS-ul copiat ar fi gresit. Intoarce intai imaginea inapoi "
                    + "sau pastreaza-i plate solve-ul. Foloseste --force daca stii ce faci."), warnings);
            }

            Log.Warn("mirrored plate solve: " + dets + "; forced, the copy goes on");
            warnings.Add(T("WARNING: the destination's own plate solve is a mirror image of the source's. Continuing anyway (--force).",
                "ATENTIE: plate solve-ul destinatiei e in oglinda fata de cel al sursei. Continui oricum (--force)."));
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

        // Each key's value cards: its card and the CONTINUE cards right under it, a long string
        // whole (R-010); the outcome lists every card, the counts are of keys.
        List<IReadOnlyList<string>> values = present.Select(k => source.ValueCards(k)).ToList();
        var cards = values.SelectMany(v => v).ToList();

        // A plate solve replaces the destination's own whole (R-016): a card of the old one that
        // the source lacks would stay beside the copied ones, and Siril 1.4.4 reads a CD matrix
        // over PC and CDELT (ADR 0011). Only a source with a plate solve has one to give.
        List<string> drop = source.Contains("CTYPE1") ? WcsMetadataKeys.SelectKeysToDrop(source, dest) : [];
        string counts = LogSelection(present, values, dest, request.IncludeOptical, drop);
        clock.Lap("select");

        string? dropped = DropLine(drop, request.DryRun);
        if (request.DryRun)
        {
            clock.Done("previewed " + LogText.Int(present.Count) + " keys " + counts);
            return new WcsCopyOutcome(WcsCopyKind.Previewed, null, warnings, cards, null,
                [T($"Keys that would be copied ({present.Count}):", $"Chei care ar fi copiate ({present.Count}):")],
                dropped);
        }

        foreach (var k in drop)
        {
            dest.RemoveCards(k);
        }

        // Every copy changes the header (the HISTORY card at least), so a checksum of the
        // destination's would no longer match it: the checksum convention asks a program that
        // changes an HDU to update the card or remove it, and the copy removes it. DATASUM stays,
        // the data being the destination's, unchanged (R-018, ADR 0021).
        dest.RemoveCards(Checksum);

        // The raw 80-byte cards are copied, not a reparsed value: that keeps the source's
        // formatting and inline comment exactly, and a long string whole, the destination's old
        // CONTINUE cards going with the card they continued (R-010).
        for (var i = 0; i < present.Count; i++)
        {
            dest.SetValueCards(present[i], values[i]);
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

            // The file asked for, not the temporary file it was written through (R-014).
            string message = FitsHeader.WriteErrorMessage(ex);
            return Refused(T($"Write error: {message}", $"Eroare la scriere: {message}"), warnings);
        }

        clock.Lap("write");
        clock.Done("copied " + LogText.Int(present.Count) + " keys into " + target + " " + counts);
        // The count follows each language's grammar, as the drop line's (R-012, ADR 0013).
        return new WcsCopyOutcome(WcsCopyKind.Copied, null, warnings, cards, target,
        [
            present.Count == 1
                ? T($"Copied 1 key into {target}.", $"Copiata 1 cheie in {target}.")
                : T($"Copied {present.Count} keys into {target}.", $"Copiate {RoCount(present.Count)} chei in {target}."),
            T("Check the annotation by eye: if the image was mirrored or flipped during processing, " +
              "the copied WCS will be wrong without any error.",
              "Verifica vizual adnotarea: daca imaginea a fost oglindita/flip-uita " +
              "pe parcursul procesarii, WCS-ul copiat va fi gresit fara sa dea eroare."),
        ],
        dropped);
    }

    /// <summary>
    /// The refusal of a run whose paths name one file twice (R-019), checked in this order: the
    /// source as the destination, then a new file that is the source, then one that is the
    /// destination, whatever its spelling, the destination's own included. A new file that does
    /// not exist yet is neither.
    /// </summary>
    /// <param name="request">The run's request; its paths as given.</param>
    /// <returns>The refusal's line in the run's language, the log's line and the done line's reason, both in English; null when the paths name three files.</returns>
    private static (string Error, string Line, string Reason)? PathClash(WcsCopyRequest request)
    {
        if (FileIdentity.Same(request.Source, request.Dest))
        {
            return (T("The source and the destination are the same file. Nothing to copy.",
                    "Sursa si destinatia sunt acelasi fisier. Nimic de copiat."),
                "the source " + request.Source + " and the destination " + request.Dest + " are the same file",
                "the source and the destination are the same file");
        }

        if (request.Output is not { } output)
        {
            return null;
        }

        if (FileIdentity.Same(output, request.Source))
        {
            return (T($"The new file '{output}' is the source itself: writing it would overwrite the plate-solved source. "
                      + "Choose another name for the new file.",
                    $"Fisierul nou '{output}' este chiar sursa: scrierea lui ar suprascrie sursa plate-solved. "
                    + "Alege alt nume pentru fisierul nou."),
                "the new file " + output + " is the source " + request.Source,
                "the new file is the source");
        }

        if (FileIdentity.Same(output, request.Dest))
        {
            return (T($"The new file '{output}' is the destination itself. Write into the destination instead, "
                      + "or choose another name for the new file.",
                    $"Fisierul nou '{output}' este chiar destinatia. Scrie in destinatie sau alege alt nume pentru fisierul nou."),
                "the new file " + output + " is the destination " + request.Dest,
                "the new file is the destination");
        }

        return null;
    }

    /// <summary>
    /// The one line naming the destination's old plate-solve keys a run drops (R-016), in the
    /// run's language: a preview's with the count in brackets, a copy's as a sentence, in the
    /// singular for one key and, in Romanian, with "de" from 20 keys on (<see cref="Ui.RoCount"/>).
    /// </summary>
    /// <param name="drop">The keys dropped, in the destination's order; empty when none.</param>
    /// <param name="preview">True for a dry run's line, false for a copy's.</param>
    /// <returns>The line, or null when there is no key to drop.</returns>
    private static string? DropLine(List<string> drop, bool preview)
    {
        if (drop.Count == 0)
        {
            return null;
        }

        string names = string.Join(", ", drop);
        int n = drop.Count;
        if (preview)
        {
            return T($"Old plate-solve keys of the destination that would be dropped ({n}): {names}",
                $"Chei vechi de plate solve ale destinatiei care ar fi eliminate ({n}): {names}");
        }

        if (n == 1)
        {
            return T($"Dropped 1 old plate-solve key of the destination: {names}.",
                $"Eliminata 1 cheie veche de plate solve a destinatiei: {names}.");
        }

        return T($"Dropped {n} old plate-solve keys of the destination: {names}.",
            $"Eliminate {RoCount(n)} chei vechi de plate solve ale destinatiei: {names}.");
    }

    /// <summary>
    /// Writes the selection to the diagnostic log, before any card is set or dropped: the count
    /// of keys per list, and at Debug one line per key with the source's cards and the
    /// destination's cards they replace (<c>none</c>, <c>same</c>, or those cards), a long
    /// string's cards joined by <c> + </c> (R-010), then one line per card of the destination
    /// dropped (R-016) and per card of its <c>CHECKSUM</c> (R-018), the one record a wrong WCS
    /// can be diagnosed from (R-042).
    /// </summary>
    /// <param name="present">The keys selected, in the copy's order.</param>
    /// <param name="values">The source's value cards of those keys, in the same order: each key's card and the CONTINUE cards under it.</param>
    /// <param name="dest">The destination's header, still as read.</param>
    /// <param name="includeOptical">True when the optical keys were asked for.</param>
    /// <param name="drop">The destination's keys the copy drops, in its order; empty when none.</param>
    /// <returns>The counts for the done line: <c>(new 14, replaced 1, identical 0, dropped 0)</c>, the dropped ones counted by key.</returns>
    private static string LogSelection(List<string> present, List<IReadOnlyList<string>> values, FitsHeader dest, bool includeOptical, List<string> drop)
    {
        int wcs = present.Count(k => WcsMetadataKeys.WcsKeys.Contains(k));
        int optical = present.Count(k => WcsMetadataKeys.OpticalKeys.Contains(k));
        Log.Debug("selected " + LogText.Int(present.Count) + " keys: " + LogText.Int(wcs) + " WCS, "
                  + LogText.Int(present.Count - wcs - optical) + " SIP, " + LogText.Int(optical)
                  + " optical (optical keys asked: " + LogText.Yes(includeOptical) + ")");

        int added = 0, replaced = 0, identical = 0;
        for (var i = 0; i < present.Count; i++)
        {
            IReadOnlyList<string> old = dest.ValueCards(present[i]);
            string was;
            if (old.Count == 0)
            {
                added++;
                was = "none";
            }
            else if (old.SequenceEqual(values[i], StringComparer.Ordinal))
            {
                identical++;
                was = "same";
            }
            else
            {
                replaced++;
                was = Joined(old);
            }

            if (Log.IsDebugEnabled)
            {
                Log.Debug("card " + present[i] + ": " + Joined(values[i]) + " | dest: " + was);
            }
        }

        if (Log.IsDebugEnabled)
        {
            foreach (var key in drop.Append(Checksum))
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

    /// <summary>A value's cards as the log writes them: each through <see cref="LogText.Card"/>, a long string's joined by <c> + </c> (R-010).</summary>
    /// <param name="cards">The cards of one value, its own first; at least one.</param>
    /// <returns>The text: <c>OBJECT  = 'M42'</c>, or <c>OBJECT  = 'NGC 6960 &amp;' + CONTINUE  'tail'</c>.</returns>
    private static string Joined(IReadOnlyList<string> cards) => string.Join(" + ", cards.Select(LogText.Card));

    /// <summary>A refused outcome: the error line and the warnings raised before it.</summary>
    /// <param name="error">The one line that says why.</param>
    /// <param name="warnings">The warnings raised so far; the outcome keeps them.</param>
    /// <returns>The outcome, with no cards, no target, no notes and nothing dropped.</returns>
    private static WcsCopyOutcome Refused(string error, List<string> warnings)
        => new(WcsCopyKind.Refused, error, warnings, [], null, [], null);
}
